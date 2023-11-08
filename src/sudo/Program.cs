// Ignore Spelling: sudo

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace sudo
{
    static class Program
    {
        static int Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("Please specify the executable to run...");
                return 1;
            }
            var stdOutPort = 5050;
            var stdErrPort = stdOutPort + 1;
            var stdInPort = stdOutPort + 2;

            var output = Console.OpenStandardOutput();
            var error = Console.OpenStandardError();

            Task.Run(() => IpcServer.ListenTo(stdOutPort, (bytes, length) => output.Write(bytes, 0, length)));
            Task.Run(() => IpcServer.ListenTo(stdErrPort, (bytes, length) => error.Write(bytes, 0, length)));

            var process = StartProcessHost(stdOutPort);
            var input = IpcClient.ConnectTo(stdInPort);

            input.WriteAllText($"$sudo-app:{args.FirstOrDefault()}|{args.Skip(1).ToCmdArgs()}");

            Task.Run(() =>
                 {
                     while (true)
                     {
                         var line = Console.ReadLine();
                         input.WriteAllText(line);
                     }
                 });

            process.WaitForExit();

            return process.ExitCode;
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

        static void OldApproachDoesNBotElevateAnyMore()
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"choco";
            p.StartInfo.Verb = "runas";
            p.StartInfo.Arguments = "--version";
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            // p.StandardInput.WriteLine("select vdisk 1");

            Console.WriteLine(p.StandardOutput.ReadLine());
            p.WaitForExit();
        }
    }
}