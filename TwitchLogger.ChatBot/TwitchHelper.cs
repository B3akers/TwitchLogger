using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchLogger.ChatBot
{
    public static class TwitchHelper
    {
        public static string DataLogDirectory = "data";
        private static bool _fullScan = true;

        public static void Compress(string filePath)
        {
            using (FileStream inputStream =
            new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (FileStream outputStream =
                new FileStream($"{filePath}.gz",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (GZipStream gzip = new GZipStream(outputStream, CompressionMode.Compress))
            {
                inputStream.CopyTo(gzip);
            }

            File.Delete(filePath);
        }

        public static async Task ArchiveLogs()
        {
            try
            {
                if (!Directory.Exists(DataLogDirectory))
                    Directory.CreateDirectory(DataLogDirectory);

                var channels = Directory.GetDirectories(DataLogDirectory);
                var currentTime = DateTimeOffset.UtcNow;

                foreach (var channel in channels)
                {
                    List<string> channelLogs = new List<string>();
                    channelLogs.AddRange(Directory.GetDirectories(Path.Combine(channel, "channel")));
                    channelLogs.AddRange(Directory.GetDirectories(Path.Combine(channel, "user")));

                    foreach (var year in channelLogs)
                    {
                        var yearStr = Path.GetFileName(year);
                        var channelLogMonths = Directory.GetDirectories(year);
                        foreach (var month in channelLogMonths)
                        {
                            var monthStr = Path.GetFileName(month);

                            var timestampEstimated = DateTime.ParseExact($"{yearStr}-{monthStr}", "yyyy-MM", CultureInfo.InvariantCulture);

                            if (!_fullScan && currentTime.Subtract(timestampEstimated) > TimeSpan.FromDays(120))
                                continue;

                            var channelLogDays = Directory.GetFiles(month);
                            var isUserLogs = month.Contains("user");
                            var packed = false;
                            var shouldPack = true;

                            for (var i = 0; i < channelLogDays.Length; i++)
                            {
                                var logFile = channelLogDays[i];

                                if (logFile.EndsWith(".gz"))
                                    continue;

                                if (Path.GetFileName(logFile) == "files.fpl")
                                {
                                    packed = true;
                                    continue;
                                }

                                var dayStr = Path.GetFileName(logFile);

                                if (!isUserLogs)
                                {
                                    var dt = DateTime.ParseExact($"{yearStr}-{monthStr}-{dayStr}", "yyyy-MM-dd", CultureInfo.InvariantCulture);

                                    if (currentTime.Subtract(dt) > TimeSpan.FromDays(7))
                                    {
                                        Compress(logFile);

                                        channelLogDays[i] = logFile + ".gz";
                                    }
                                }
                                else
                                {
                                    if (currentTime.Subtract(timestampEstimated) > TimeSpan.FromDays(90))
                                    {
                                        Compress(logFile);

                                        channelLogDays[i] = logFile + ".gz";
                                    }
                                    else
                                        shouldPack = false;
                                }
                            }

                            if (!packed && isUserLogs && shouldPack)
                            {
                                List<Tuple<ulong, string, long>> listToPack = new List<Tuple<ulong, string, long>>();

                                foreach (var logFile in channelLogDays)
                                {
                                    var fileName = Path.GetFileName(logFile);
                                    fileName = fileName.Substring(0, fileName.LastIndexOf('.'));

                                    listToPack.Add(new Tuple<ulong, string, long>(ulong.Parse(fileName), logFile, new FileInfo(logFile).Length));
                                }

                                listToPack = listToPack.OrderBy(x => x.Item1).ToList();
                                if (listToPack.Count > 0)
                                {
                                    using (FileStream outputStream = new FileStream(Path.Combine(month, "files.fpl"), FileMode.OpenOrCreate, FileAccess.ReadWrite))
                                    {
                                        await outputStream.WriteAsync(BitConverter.GetBytes(listToPack.Count), 0, 4);

                                        long headerSize = 0x4 + (listToPack.Count * 0x18);
                                        long currentFilePointer = 0x0;

                                        foreach (var list in listToPack)
                                        {
                                            var currentOffset = headerSize + currentFilePointer;

                                            await outputStream.WriteAsync(BitConverter.GetBytes(list.Item1), 0, 8);
                                            await outputStream.WriteAsync(BitConverter.GetBytes(currentOffset), 0, 8);
                                            await outputStream.WriteAsync(BitConverter.GetBytes(list.Item3), 0, 8);

                                            currentFilePointer += list.Item3;
                                        }

                                        foreach (var list in listToPack)
                                        {
                                            using (FileStream inputStream = new FileStream(list.Item2, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                                            {
                                                await inputStream.CopyToAsync(outputStream);
                                            }
                                        }
                                    }

                                    foreach (var logFile in channelLogDays)
                                        File.Delete(logFile);
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception es) { Console.WriteLine(es); }

            _fullScan = false;
        }

        public static async Task AddLogToFiles(DateTimeOffset currentDate, string roomId, string userId, string raw)
        {
            var channelDirectoryLog = Path.Combine(DataLogDirectory, roomId, "channel", currentDate.ToString("yyyy"), currentDate.ToString("MM"));
            if (!string.IsNullOrEmpty(channelDirectoryLog) && !string.IsNullOrEmpty(roomId))
            {
                if (!Directory.Exists(channelDirectoryLog))
                    Directory.CreateDirectory(channelDirectoryLog);

                var currentLogFile = Path.Combine(channelDirectoryLog, currentDate.ToString("dd"));

                using (var file = File.Open(currentLogFile, FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write))
                {
                    await file.WriteAsync(Encoding.UTF8.GetBytes($"{raw}\r\n"));
                }
            }

            var userDirectoryLog = Path.Combine(DataLogDirectory, roomId, "user", currentDate.ToString("yyyy"), currentDate.ToString("MM"));
            if (!string.IsNullOrEmpty(userDirectoryLog) && !string.IsNullOrEmpty(userId))
            {
                if (!Directory.Exists(userDirectoryLog))
                    Directory.CreateDirectory(userDirectoryLog);

                var currentLogFile = Path.Combine(userDirectoryLog, userId);

                using (var file = File.Open(currentLogFile, FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write))
                {
                    await file.WriteAsync(Encoding.UTF8.GetBytes($"{raw}\r\n"));
                }
            }
        }
    }
}
