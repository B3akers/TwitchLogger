using Newtonsoft.Json.Linq;
using System.IO.Pipes;
using System.Text;
using TwitchLogger.Website.Interfaces;

namespace TwitchLogger.Website.Services
{
    public class LivetimeServerPipeService : BackgroundService
    {
        private readonly IChannelLiveStats _channelLiveStats;
        public LivetimeServerPipeService(IChannelLiveStats channelLiveStats)
        {
            _channelLiveStats = channelLiveStats;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            byte[] buffer = new byte[1024];
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (NamedPipeServerStream serverPipe = new NamedPipeServerStream("TwitchLogger.NamedPipe"))
                    {
                        await serverPipe.WaitForConnectionAsync(stoppingToken);

                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                var readBytes = await serverPipe.ReadAsync(buffer, stoppingToken);
                                if (readBytes == 0)
                                {
                                    if (!serverPipe.IsConnected)
                                        break;

                                    continue;
                                }

                                var dataSize = buffer.Length;
                                var totalReadBytes = readBytes;

                                while (readBytes == dataSize && (buffer[totalReadBytes - 2] != 0x0D || buffer[totalReadBytes - 1] != 0x0A))
                                {
                                    var newBuffer = new byte[buffer.Length * 2];
                                    Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
                                    buffer = newBuffer;

                                    dataSize = buffer.Length - totalReadBytes;
                                    readBytes = await serverPipe.ReadAsync(buffer, totalReadBytes, dataSize, stoppingToken);
                                    totalReadBytes += readBytes;
                                }

                                var messages = Encoding.UTF8.GetString(buffer, 0, totalReadBytes).Split("\r\n");
                                foreach (var message in messages)
                                {
                                    if (string.IsNullOrEmpty(message))
                                        continue;

                                    try
                                    {
                                        var messageObj = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(message)));
                                        _channelLiveStats.ProcessMessage(messageObj);
                                    }
                                    catch { }
                                }
                            }
                            catch { break; }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
