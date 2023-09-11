using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Web;

namespace TwitchLogger.ChatBot
{
    namespace TwitchIrcClient
    {
        public enum BotStatus
        {
            None,
            Connecting,
            WaitingForResponse,
            Reconnecting,
            Online,
            WrongPassword,
            WrongPasswordFormat,
            ProxyFailed,
            ProxyLimit,
            Timeout,
            Disconnected
        };

        public enum TwitchChatMessageType
        {
            PRIVMSG,
            USERNOTICE,
            UNKNOWN
        }

        public class TwitchChatMessage : EventArgs
        {
            public Dictionary<string, string> SenderInfo { get; set; }
            public string Message { get; set; }
            public string Channel { get; set; }
            public TwitchChatMessageType MessageType { get; set; }
            public string Raw { get; set; }
        }

        class TwitchIrcBot
        {
            const string ip = "irc.chat.twitch.tv";
            const int port = 6667;

            public event Func<object, TwitchChatMessage, Task> OnMessage;
            public HashSet<string> ChannelLists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private TcpClient _tcpClient = null;

            private StreamReader _streamReader = null;
            private StreamWriter _streamWriter = null;

            private Random _random = new Random();

            private BotStatus _botStatus = BotStatus.None;

            private string _username;
            private string _password;

            private BufferBlock<TwitchChatMessage> _messages = new();
            private CancellationTokenSource _connectionToken;

            public BotStatus Status
            {
                get
                {
                    return _botStatus;
                }
            }

            public string Login
            {
                get
                {
                    return _username;
                }
            }

            public TwitchIrcBot(string username, string password)
            {
                _username = username;
                _password = password;
            }

            private async Task InvokeOnMessage(TwitchChatMessage args)
            {
                Func<object, TwitchChatMessage, Task> handler = OnMessage;

                if (handler == null)
                {
                    return;
                }

                Delegate[] invocationList = handler.GetInvocationList();
                Task[] handlerTasks = new Task[invocationList.Length];

                for (int i = 0; i < invocationList.Length; i++)
                {
                    handlerTasks[i] = ((Func<object, TwitchChatMessage, Task>)invocationList[i])(this, args);
                }

                await Task.WhenAll(handlerTasks);
            }

            public async Task Start()
            {
                if (_connectionToken != null)
                    _connectionToken.Dispose();

                _connectionToken = new();

                await Connect();

                if (_botStatus == BotStatus.Online)
                {
                    _ = Task.Run(MessageProcessing, _connectionToken.Token);
                }

                Console.WriteLine($"[{_username}] connect status: {_botStatus}");
            }

            public Task Disconnect()
            {
                Console.WriteLine($"[{_username}] disconnecting...");

                if (_connectionToken != null)
                    _connectionToken.Cancel();

                if (_tcpClient != null)
                    _tcpClient.Close();

                if (_streamReader != null)
                    _streamReader.Close();

                if (_streamWriter != null)
                    _streamWriter.Close();

                _botStatus = BotStatus.Disconnected;

                return Task.CompletedTask;
            }

            public async Task JoinChannel(string channel)
            {
                ChannelLists.Add(channel);
                await _streamWriter.WriteLineAsync($"JOIN {channel}");
            }

            public async Task LeaveChannel(string channel)
            {
                ChannelLists.Remove(channel);
                await _streamWriter.WriteLineAsync($"PART {channel}");
            }

            public Dictionary<string, List<DateTime>> rateLimiter = new(StringComparer.OrdinalIgnoreCase);

            public async Task SendMessage(string channel, string message)
            {
                List<DateTime> list;

                if (!rateLimiter.TryGetValue(channel, out list))
                {
                    list = new List<DateTime>();
                    rateLimiter[channel] = list;
                }

                list.Add(DateTime.UtcNow);
                list.RemoveAll(x => x < DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5)));

                if (list.Count > 4)
                    return;

                await _streamWriter.WriteLineAsync($"PRIVMSG {channel} :{message}");
            }

            private async Task MessageProcessing()
            {
                try
                {
                    while (!_connectionToken.Token.IsCancellationRequested)
                    {
                        await InvokeOnMessage(await _messages.ReceiveAsync(_connectionToken.Token));
                    }
                }
                catch { }
            }

            public TwitchChatMessageType GetMessageType(string commandType)
            {
                switch (commandType)
                {
                    case "PRIVMSG":
                        return TwitchChatMessageType.PRIVMSG;
                    case "USERNOTICE":
                        return TwitchChatMessageType.USERNOTICE;
                }

                return TwitchChatMessageType.PRIVMSG;
            }

            private async Task StreamReader()
            {
                var cancelToken = new CancellationTokenSource();

                try
                {
                    long lastRecivedPong = 0;

                    var token = cancelToken.Token;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!token.IsCancellationRequested)
                            {
                                await _streamWriter.WriteLineAsync("PING twitch.tv");
                                await Task.Delay(15000, token);
                            }
                        }
                        catch { }
                    }, token);

                    while (!_connectionToken.Token.IsCancellationRequested)
                    {
                        string command = await _streamReader.ReadLineAsync();

                        if (!string.IsNullOrEmpty(command))
                        {
                            var commandArgs = command.Split(' ');

                            if (commandArgs[0] == "PING")
                            {
                                await _streamWriter.WriteLineAsync($"PONG {commandArgs[1]}");
                            }
                            else if (commandArgs.Length > 1 && commandArgs[1] == "PONG")
                            {
                                lastRecivedPong = Environment.TickCount64;
                            }
                            else if (commandArgs.Length > 2 && commandArgs[0].StartsWith("@"))
                            {
                                Dictionary<string, string> senderInfos = new();

                                var messageInfos = commandArgs[0].Substring(1).Split(';');
                                foreach (var info in messageInfos)
                                {
                                    var splitInfo = info.Split('=');
                                    senderInfos[splitInfo[0]] = splitInfo[1];
                                }

                                if (commandArgs[2] == "NOTICE")
                                {
                                    Console.WriteLine(command);
                                }

                                if (commandArgs[2] == "PRIVMSG" || commandArgs[2] == "USERNOTICE")
                                {
                                    string channel = commandArgs[3].Trim();
                                    string message = commandArgs.Length > 4 ? string.Join(' ', commandArgs.Skip(4)).Substring(1) : "";

                                    senderInfos["user-login"] = commandArgs[1].Substring(1, commandArgs[1].IndexOf('!') - 1);

                                    await _messages.SendAsync(new TwitchChatMessage
                                    {
                                        Message = message,
                                        SenderInfo = senderInfos,
                                        Channel = channel,
                                        MessageType = GetMessageType(commandArgs[2]),
                                        Raw = command
                                    });
                                }
                                else
                                {
                                    await _messages.SendAsync(new TwitchChatMessage
                                    {
                                        Message = string.Empty,
                                        SenderInfo = senderInfos,
                                        Channel = string.Empty,
                                        MessageType = TwitchChatMessageType.UNKNOWN,
                                        Raw = command
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (Environment.TickCount64 - lastRecivedPong > 30000)
                            {
                                throw new Exception("Connection lost");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    cancelToken.Cancel();
                    cancelToken.Dispose();
                    cancelToken = null;

                    if (!_connectionToken.Token.IsCancellationRequested)
                    {
                        await Connect();
                        Console.WriteLine($"[{_username}] connect status: {_botStatus}");

                        foreach (var channel in ChannelLists)
                        {
                            await JoinChannel(channel);
                            await Task.Delay(1200);
                        }
                    }
                }

                if (cancelToken != null)
                {
                    cancelToken.Cancel();
                    cancelToken.Dispose();
                }
            }

            private async Task Connect()
            {
                Console.WriteLine("Connecting...");

                _botStatus = BotStatus.Connecting;

                try
                {
                    //Close last connection
                    //
                    if (_tcpClient != null)
                        _tcpClient.Close();

                    if (_streamReader != null)
                        _streamReader.Close();

                    if (_streamWriter != null)
                        _streamWriter.Close();

                    _tcpClient = new TcpClient();
                    await _tcpClient.ConnectAsync(ip, port);

                    _streamReader = new StreamReader(_tcpClient.GetStream());
                    _streamWriter = new StreamWriter(_tcpClient.GetStream()) { NewLine = "\r\n", AutoFlush = true };

                    //Login
                    //

                    if (!string.IsNullOrEmpty(_password))
                    {
                        await _streamWriter.WriteLineAsync($"PASS oauth:{_password}");
                        await _streamWriter.WriteLineAsync($"NICK {_username}");
                    }
                    else
                    {
                        await _streamWriter.WriteLineAsync("PASS SCHMOOPIIE");
                        await _streamWriter.WriteLineAsync($"NICK justinfan{_random.Next(10000, 99999)}");
                    }
                    _botStatus = BotStatus.WaitingForResponse;
                }
                catch { }

                string result = null;

                var oldstreamReader = _streamReader;
                var receiveTask = Task.Run(async () => { try { result = await oldstreamReader.ReadLineAsync(); } catch { } });

                if (await Task.WhenAny(receiveTask, Task.Delay(10000)) != receiveTask)
                    result = null;

                if (!string.IsNullOrEmpty(result) && _botStatus == BotStatus.WaitingForResponse)
                {
                    var response = result.Split(' ');

                    if (response[1] == "001")
                    {
                        _botStatus = BotStatus.Online;

                        await _streamWriter.WriteLineAsync("CAP REQ :twitch.tv/tags twitch.tv/commands");

                        _ = Task.Run(StreamReader, _connectionToken.Token);
                    }
                    else if (response[1] == "NOTICE")
                    {
                        if (response[3] == ":Improperly formatted auth")
                            _botStatus = BotStatus.WrongPasswordFormat;
                        else if (response[3] == ":Login authentication failed")
                            _botStatus = BotStatus.WrongPassword;
                        else
                            throw new Exception($"Unhandled message {response}");
                    }
                }
                else
                {
                    _botStatus = BotStatus.Reconnecting;

                    Console.WriteLine("Reconnecting...");

                    await Task.Delay(4000);
                    await Connect();
                }
            }
        }
    }
}
