// Ignore Spelling: sudo pid envars

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Environment;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;

namespace sudo
{
    // Good NamedPipes sample: https://dev.to/volkanalkilic/using-named-pipes-in-c-for-interprocess-communication-4kp1
    static partial class Pipes
    {
        public static bool IsChannelOpen(string name)
        {
            using (var mutex = new Mutex(false, name))
            {
                if (!mutex.WaitOne(0))
                    return true;
                else
                    return false;
            }
        }

        /// <summary>
        /// Connects to the remote server and listens to the data (input).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="onData">The on data.</param>
        public static void ListenToChannel(string name, Action<char> onData)
        {
            using (var pipeClient = new NamedPipeClientStream(".", name, PipeDirection.InOut))
            {
                if (!pipeClient.IsConnected)
                    pipeClient.Connect();

                using (var reader = new StreamReader(pipeClient))
                {
                    var message = string.Empty;
                    var running = true;
                    while (running)
                    {
                        var chr = reader.Read();
                        if (chr > 0)
                            onData((char)chr);
                    }
                }
            }
        }

        /// <summary>
        /// Connects to the remote server and listens to the data (input).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="onData">The on data.</param>
        public static void ListenAndRespondToChannel(string name, Action<string> onData, Func<string> respondWith = null)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", name, PipeDirection.InOut))
                {
                    if (!pipeClient.IsConnected)
                        pipeClient.Connect();

                    using (var reader = new StreamReader(pipeClient))
                    {
                        var message = string.Empty;
                        var running = true;
                        while (running)
                        {
                            string line;
                            var allLines = new StringBuilder();
                            while ((line = reader.ReadLine()).HasText())
                            {
                                allLines.AppendLine(line);
                            }

                            if (allLines.ToString().HasText())
                                onData(allLines.ToString());

                            if (respondWith != null)
                                using (var writer = new StreamWriter(pipeClient))
                                    writer.WriteLine(respondWith());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // nothing we can do about it. If this does not work than we cannot communicate anything to the caller
            }
        }

        /// <summary>
        /// Starts local server and waits for the remote process to read the data (output).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public static (Action<string> writeTo, Func<string> readFrom) CreateNotificationChannel(string name)
        {
            var ps = new PipeSecurity();
            ps.AddAccessRule(new PipeAccessRule(Environment.UserName, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            var pipeServer = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None, 1024, 1024, ps);
            pipeServer.WaitForConnection();

            return (data =>
                    {
                        var bytes = data.GetBytes();
                        pipeServer.Write(bytes, 0, bytes.Length);
                        pipeServer.WaitForPipeDrain();
                    }
                    ,
                    () =>
                    {
                        using (var reader = new StreamReader(pipeServer))
                        {
                            return reader.ReadLine();
                        }
                    }
                   );
        }
    }

    static class Logger
    {
        static int pid = Process.GetCurrentProcess().Id;

        public static string LogFile
            => Path.Combine(GetFolderPath(SpecialFolder.ApplicationData),
                            "win-sudo",
                            $"{Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}.last-run.log");

        static Logger()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
            if (File.Exists(LogFile)) File.Delete(LogFile);
            LogInfo("Started");
        }

        public static void LogInfo(this object message)
            => File.AppendAllText(LogFile, $"{DateTime.Now.ToString("s")}|{pid}|INFO: {message}");

        public static void LogError(this object message)
            => File.AppendAllText(LogFile, $"{DateTime.Now.ToString("s")}|{pid}|ERROR: {message}");
    }

    static class GenericExtensions
    {
        [DllImport("ntdll.dll")]
        static extern int NtQueryInformationProcess(
               IntPtr processHandle,
               int processInformationClass,
               out PROCESS_BASIC_INFORMATION processInformation,
               int processInformationLength,
               out int returnLength);

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        public static bool IsRunning(this Process process)
        {
            try
            {
                return !process.HasExited;
            }
            catch { return false; }
        }

        public static Process GetParent(this Process process)
        {
            try
            {
                var pid = process.GetParentPid();
                if (pid != 0)
                    return Process.GetProcessById(pid);
            }
            catch { }

            return null;
        }

        public static int GetParentPid(this Process process)
        {
            PROCESS_BASIC_INFORMATION pbi;
            var res = NtQueryInformationProcess(process.Handle, 0, out pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out int size);

            return res != 0 ? 0 : pbi.InheritedFromUniqueProcessId.ToInt32();
        }

        public static bool HasText(this string text) => !string.IsNullOrEmpty(text);

        public static string Serialize(this IDictionary currentEnvars)
        {
            var envars = string.Join(NewLine, currentEnvars.Keys.Cast<string>().Select(k => $"{k}|{currentEnvars[k]}"));
            return envars;
        }

        public static Dictionary<string, string> DeserializeEnVars(this IEnumerable<string> envars)
            => envars.Where(x => x.HasText())
                     .Select(x => x.Split(new[] { '|' }, 2))
                     .ToDictionary(x => x.FirstOrDefault(), x => x.LastOrDefault());

        public static int ToInt(this string text) => int.Parse(text);

        public static string ArgValue(this string[] args, string name)
            => args.FirstOrDefault(x => x.StartsWith($"-{name}:"))?.Split(new[] { ':' }, 2).LastOrDefault();

        public static bool ArgPresent(this string[] args, string name)
            => args.Any(x => x.StartsWith($"-{name}"));

        public static void WaitForProcessExit(this int pid)
        {
            while (true)
            {
                try
                {
                    if (Process.GetProcessById(pid) == null)
                        break;
                    Thread.Sleep(1000);
                }
                catch
                {
                    break;
                }
            }
        }

        public static string ToCmdArgs(this IEnumerable<string> args)
        => string.Join(" ",
                       args.Select(x => (x.Contains(" ") || x.Contains("\t")) ? $"\"{x}\"" : x)
                           .ToArray());

        public static byte[] GetBytes(this char[] data, int? length = null) => Encoding.UTF8.GetBytes(data, 0, length ?? data.Length);

        public static byte[] GetBytes(this string data) => Encoding.UTF8.GetBytes(data);

        public static string GetString(this byte[] data) => Encoding.UTF8.GetString(data);
    }
}