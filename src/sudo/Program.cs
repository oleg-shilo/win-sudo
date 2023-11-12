// Ignore Spelling: sudo

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using sudo;

// TODO:
// + support two modes (single-run vs multi-run)
// - in single-run use GUID for naming channels
// - in sudo-host monitor
//   - sudo and exit (if in a single-run mode) if it's terminated
// - on the first run open url with readme file
// - implement logging to the file
// - implement CLI runtime help
//   - clean
//   - config:run=multi
//   - config:run=single
//   - config:idle_timeout=3
// - implement CLI clean sudo-host instances
// - implement settings file
// - implement timeout for multi-run mode
// - root process and exit if it's terminated
// - Embed sudo-host as a resource, and distribute on a first run
// - Documentation
static class Sudo
{
    static int Main(string[] args)
    {
        try
        {
            var exe = args.FirstOrDefault();
            var arguments = args.Skip(1).ToCmdArgs();

            // -----------

            if (!Pipes.IsChannelOpen($"sudo-host-out"))
            {
                Console.WriteLine("Starting new sudo-host...");
                StartProcessHost();
            }
            else
                Console.WriteLine("Reusing sudo-host...");

            var pid = Process.GetCurrentProcess().GetParentPid();

            // -----------

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-out", onData: WriteToConsoleOut));

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-error", onData: WriteToConsoleError));

            int? exitCode = null;
            Task.Run(() =>
            {
                var controlChannel = Pipes.CreateNotificationChannel($"sudo-host-control");

                controlChannel.writeTo($"{exe}|{arguments}{Environment.NewLine}");
                var reportedExitCode = controlChannel.readFrom();
                exitCode = int.Parse(reportedExitCode);
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

            while (!exitCode.HasValue)
                Thread.Sleep(300);

            return exitCode.Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return -1;
        }
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

    static Process StartProcessHost()
    {
        var process = new Process();

        process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sudo-host.exe");
        // process.StartInfo.Arguments = "-run:multi";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        return process;
    }
}