// Ignore Spelling: sudo

using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace sudo
{
    static class Program
    {
        static int Main(string[] args)
        {
            var exe = args.FirstOrDefault();
            var arguments = args.Skip(1).ToCmdArgs();

            // -----------

            Process elevatedHost;

            if (!Pipes.IsChannelOpen($"sudo-host-out"))
                StartProcessHost(0);

            var pid = Process.GetCurrentProcess().GetParentPid();

            // -----------

            int? exitCode = null;

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-out", onData: WriteToConsoleOut));

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-error", onData: WriteToConsoleError));

            Task.Run(() =>
            {
                var controlChannel = Pipes.CreateNotificationChannel($"sudo-host-control");

                controlChannel.writeTo($"{exe}|{arguments}{Environment.NewLine}");
                var elevatedHostId = controlChannel.readFrom();

                var process = Process.GetProcessById(int.Parse(elevatedHostId));
                process.WaitForExit();
                exitCode = process.ExitCode;
            });

            Task.Run(() =>
            {
                var (writeToHostInput, _) = Pipes.CreateNotificationChannel($"sudo-host-input");
                while (true)
                {
                    var line = Console.ReadLine();
                    writeToHostInput(line + Environment.NewLine);
                }
            });

            while (exitCode == null)
                Thread.Sleep(100);

            return exitCode.Value;
        }

        static void WriteToConsoleOut(char x)
        {
            lock (typeof(Console))
            {
                Console.Write(x);
            }
        }

        static void WriteToConsoleError(char x)
        {
            lock (typeof(Console))
            {
                WithConsole(ConsoleColor.Red, () =>
                {
                    var bytes = new[] { x }.GetBytes();
                    Console.OpenStandardError().Write(bytes, 0, bytes.Length);
                });
            }
        }

        static void WithConsole(ConsoleColor color, Action action)
        {
            var oldColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                action();
            }
            finally { Console.ForegroundColor = oldColor; }
        }

        static Process StartProcessHost(int port)
        {
            var process = new Process();

            process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sudo-host.exe");
            process.StartInfo.Arguments = $"{Process.GetCurrentProcess().Id} {port}";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            return process;
        }

        static void OldApproachDoesNotElevateAnyMoreOnWin11()
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"choco";
            p.StartInfo.Verb = "runas";
            p.StartInfo.Arguments = "--version";
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();

            Console.WriteLine(p.StandardOutput.ReadLine());
            p.WaitForExit();
        }
    }
}