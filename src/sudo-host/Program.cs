// Ignore Spelling: app

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
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using sudo;

class SudoHost : MarshalByRefObject
{
    static void Main(string[] args)
    {
        if (args.ArgValue("run") == "multi")
        {
            string oldSudoHostId = args.ArgValue("wait");
            if (oldSudoHostId.HasText())
                try
                {
                    Process.GetProcessById(int.Parse(oldSudoHostId)).WaitForExit();
                }
                catch { }
        }

        Run(args);
    }

    static void Run(string[] args)
    {
        int operationTimeout = 5000;
        Mutex mutex = null;
        try
        {
            // int executingProcess = int.Parse(args[0]);
            // int sudoProcess = int.Parse(args[1]);

            Task.Run(() =>
                Launcher.OnOutput = Pipes.CreateNotificationChannel($"sudo-host-out").writeTo);

            mutex = new Mutex(false, $"sudo-host-out");
            mutex.WaitOne(0);

            Task.Run(() =>
                Launcher.OnError = Pipes.CreateNotificationChannel($"sudo-host-error").writeTo);

            Launcher.WaitForReady(operationTimeout * 100);

            Task.Run(() =>
                Pipes.ListenAndRespondToChannel($"sudo-host-control",
                                                onData: Launcher.HandleCommand,
                                                respondWith: () =>
                                                {
                                                    Launcher.ProcessStartedEvent.WaitOne(operationTimeout);
                                                    Launcher.ProcessExitedEvent.WaitOne();
                                                    return Launcher.Process.ExitCode.ToString();
                                                }));

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-input", Launcher.HandleInput));

            Launcher.ProcessExitedEvent.WaitOne();

            if (args.ArgValue("run") == "multi")
                Restart();
        }
        catch (Exception e)
        {
            Launcher.ReportError($"Sudo-host error", e.Message);
        }
        finally
        {
            try
            {
                mutex?.ReleaseMutex();
                mutex?.Close();
                mutex?.Dispose();
                mutex = null;
            }
            catch { }
        }
    }

    static Process Restart()
    {
        var process = new Process();

        process.StartInfo.FileName = Assembly.GetExecutingAssembly().Location;
        process.StartInfo.Arguments = $"-run:multi -wait:{Process.GetCurrentProcess().Id}";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        return process;
    }
}