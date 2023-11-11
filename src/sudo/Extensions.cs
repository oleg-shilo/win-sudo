// Ignore Spelling: sudo pid

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sudo
{
    // Good NamedPipes sample: https://dev.to/volkanalkilic/using-named-pipes-in-c-for-interprocess-communication-4kp1
    static partial class Pipes
    {
        public static bool IsChannelOpen(string name)
        {
            using (var pipeClient = new NamedPipeClientStream(".", name, PipeDirection.InOut))
            {
                try
                {
                    pipeClient.Connect(10);
                    return pipeClient.IsConnected;
                }
                catch { return false; }
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
        public static void ListenAndRespondToChannel(string name, Action<string> onData, Func<string> writeResponse = null)
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
                            var line = reader.ReadLine();
                            if (line.HasText())
                                onData(line);

                            if (writeResponse != null)
                                using (var writer = new StreamWriter(pipeClient))
                                    writer.WriteLine(writeResponse());
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Starts local server and waits for the remote process to read the data (output).
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public static (Action<string> writeToChannel, Func<string> readFromChannel) CreateNotificationChannel(string name)
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
                    , null
                   // () =>
                   // {
                   //     // using (var reader = new StreamReader(pipeServer))
                   //     // {
                   //     //     return reader.ReadLine();
                   //     // }
                   // }
                   );
        }
    }

    // class PipeClient
    // {
    //     public PipeClient(string name)
    //     {
    //         // Task.Run(() => Run(this.name = name));
    //         this.name = name;
    //     }

    //     string name;
    //     string data;
    //     bool shutDownRequested;

    //     public void Write(string data) => this.data = data;

    //     public void Close() => shutDownRequested = true;

    //     public void Start()
    //         => Task.Run(() => Run(this.name = name));

    //     void Run(string name)
    //     {
    //         using (var pipeClient = new NamedPipeClientStream(".", name, PipeDirection.InOut))
    //         {
    //             if (!pipeClient.IsConnected)
    //                 pipeClient.Connect();

    //             using (var writer = new StreamWriter(pipeClient))
    //             {
    //                 while (!shutDownRequested)
    //                 {
    //                     if (data != null)
    //                     {
    //                         writer.WriteLine(data);
    //                         writer.Flush();
    //                         pipeClient.WaitForPipeDrain();
    //                     }
    //                     else
    //                     {
    //                         Console.WriteLine("No data to send");
    //                     }
    //                     data = null;
    //                     Thread.Sleep(1000);
    //                 }
    //             }
    //         }
    //     }
    // }

    // static partial class Pipes
    // {
    //     public static NamedPipeClientStream Write(this NamedPipeClientStream pipeClient, string data)
    //     {
    //         using (var writer = new StreamWriter(pipeClient))
    //         {
    //             writer.WriteLine(data);
    //             writer.Flush();
    //             pipeClient.WaitForPipeDrain();
    //         }

    //         return pipeClient;
    //     }

    //     public static NamedPipeClientStream CreateClient(string name)
    //     {
    //         var pipeClient = new NamedPipeClientStream(".", name, PipeDirection.InOut);
    //         if (!pipeClient.IsConnected)
    //             pipeClient.Connect();
    //         return pipeClient;
    //     }

    //     public static void RunClient_Original()
    //     {
    //         using (var pipeClient = new NamedPipeClientStream(".", "TestPipe", PipeDirection.InOut))
    //         {
    //             Console.WriteLine("Client is waiting to connect");
    //             if (!pipeClient.IsConnected) { pipeClient.Connect(); }
    //             Console.WriteLine("Client is connected");
    //             using (var reader = new StreamReader(pipeClient))
    //             {
    //                 using (var writer = new StreamWriter(pipeClient))
    //                 {
    //                     var message = string.Empty;
    //                     var running = true;
    //                     while (running)
    //                     {
    //                         Console.WriteLine("Client is waiting for input");
    //                         var chr = reader.Read();
    //                         if (chr >= 32)
    //                         {
    //                             message = message + (char)chr;
    //                             Console.WriteLine("Client: Recieved from server {0}", message);
    //                             switch (message)
    //                             {
    //                                 case "Do you accept (y/n):":
    //                                     writer.WriteLine("y");
    //                                     writer.WriteLine("quit");
    //                                     writer.Flush();
    //                                     break;

    //                                 case "quit":
    //                                     running = false;
    //                                     break;
    //                             }
    //                         }
    //                         else
    //                         {
    //                             message = string.Empty;
    //                             Console.WriteLine("Client: New Line Received from Server");
    //                         }
    //                     }
    //                 }
    //             }
    //         }
    //         Console.WriteLine("Client Quits");
    //     }

    //     static public string data = "test";

    //     public static void RunServer_original(string name, Action<byte[]> onData)
    //     {
    //         var ps = new PipeSecurity();
    //         ps.AddAccessRule(new PipeAccessRule(Environment.UserName, PipeAccessRights.ReadWrite, AccessControlType.Allow));

    //         using (var pipeServer = new NamedPipeServerStream("TestPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None,
    //             1024, 1024, ps))
    //         {
    //             using (var reader = new StreamReader(pipeServer))
    //             {
    //                 using (var writer = new StreamWriter(pipeServer))
    //                 {
    //                     var running = true;
    //                     Console.WriteLine("Server is waiting for a client");
    //                     pipeServer.WaitForConnection();
    //                     Console.WriteLine("Server has connection from client");
    //                     Console.WriteLine("Server: Saying Hi");
    //                     writer.WriteLine("Hello!");
    //                     Console.WriteLine("Server: Prompting for Input");
    //                     writer.Write("Do you accept (y/n):"); //NB: This is a write, not a write line!
    //                     writer.Flush();
    //                     while (running)
    //                     {
    //                         pipeServer.WaitForPipeDrain();
    //                         var message = reader.ReadLine();
    //                         Console.WriteLine("Server: Recieved from client {0}", message);
    //                         switch (message)
    //                         {
    //                             case "quit":
    //                                 writer.WriteLine("quit");
    //                                 running = false;
    //                                 break;

    //                             default:
    //                                 writer.WriteLine("");
    //                                 break;
    //                         }
    //                     }
    //                 }
    //             }
    //         }
    //         Console.WriteLine("Server Quits");
    //     }
    // }

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

        internal static int GetParentPid(this Process process)
        {
            PROCESS_BASIC_INFORMATION pbi;
            var res = NtQueryInformationProcess(process.Handle, 0, out pbi, Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(), out int size);

            return res != 0 ? 0 : pbi.InheritedFromUniqueProcessId.ToInt32();
        }

        public static bool HasText(this string text) => !string.IsNullOrEmpty(text);

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
    }

    static class SocketExtensions
    {
        public static byte[] GetBytes(this char[] data, int? length = null) => Encoding.UTF8.GetBytes(data, 0, length ?? data.Length);

        public static byte[] GetBytes(this string data) => Encoding.UTF8.GetBytes(data);

        public static string GetString(this byte[] data) => Encoding.UTF8.GetString(data);

        public static string ReadAllText(this TcpClient client) => client.ReadAllBytes().GetString();

        public static byte[] ReadAllBytes(this TcpClient client)
        {
            var bytes = new byte[client.ReceiveBufferSize];
            var len = client.GetStream()
                            .Read(bytes, 0, bytes.Length);
            var result = new byte[len];
            Array.Copy(bytes, result, len);
            return result;
        }

        public static void WriteAllBytes(this TcpClient client, byte[] data, int? length = null)
        {
            var stream = client.GetStream();
            stream.Write(data, 0, length ?? data.Length);
            stream.Flush();
        }

        public static void WriteAllText(this TcpClient client, string data) => client.WriteAllBytes(data.GetBytes());
    }
}