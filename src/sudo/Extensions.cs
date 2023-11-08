// Ignore Spelling: sudo pid

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sudo
{
    class IpcClient
    {
        public static TcpClient ConnectTo(int port)
        {
            var clientSocket = new TcpClient();
            while (true)
                try
                {
                    clientSocket.Connect(IPAddress.Loopback, port);
                    break;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            return clientSocket;
        }
    }

    class IpcServer
    {
        public static void ListenTo(int port, Action<byte[], int> onDataAvailable)
        {
            var serverSocket = new TcpListener(IPAddress.Loopback, port);
            try
            {
                serverSocket.Start();

                using (TcpClient clientSocket = serverSocket.AcceptTcpClient())
                {
                    while (true)
                    {
                        try
                        {
                            var bytes = new byte[clientSocket.ReceiveBufferSize];
                            var len = clientSocket.GetStream()
                                                  .Read(bytes, 0, bytes.Length);

                            onDataAvailable(bytes, len);
                        }
                        catch (Exception e)
                        {
                            if (e.InnerException is SocketException)
                            {
                            }
                            else
                                Console.WriteLine(">>>" + e.Message);
                            break;
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                if (e.ErrorCode == 10048)
                    Console.WriteLine(">" + e.Message);
                else
                    Console.WriteLine(">>" + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    static class GenericExtensions
    {
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