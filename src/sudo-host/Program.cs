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

class SudoHost
{
    static Mutex mutex = null;

    static void Main(string[] args)
    {
        string oldSudoHostId = args.ArgValue("wait");
        if (oldSudoHostId.HasText())
            try
            {
                // Debug.Assert(false);
                Process.GetProcessById(oldSudoHostId.ToInt()).WaitForExit();
            }
            catch { }

        Run(args);
    }

    static void Run(string[] args)
    {
        int operationTimeout = 5000;

        string channelId = args.ArgValue("channel");
        int requesterPid = args.ArgValue("requester").ToInt(); // a terminal running sudo process

        try
        {
            var config = Config.Load();

            mutex = new Mutex(false, channelId);
            mutex.WaitOne(0);

            MonitorProcess(requesterPid, onExit: () =>
                                         {
                                             ReleaseMutex();
                                             Process.GetCurrentProcess().Kill();
                                         });

            Task.Run(() =>
                Launcher.OnOutput = Pipes.CreateNotificationChannel($"{channelId}:sudo-host-out").writeTo);

            Task.Run(() =>
                Launcher.OnError = Pipes.CreateNotificationChannel($"{channelId}:sudo-host-error").writeTo);

            var waitTillReadyTimeout = (config.multi_run ? config.IdleTimeoutInMilliseconds : operationTimeout);

            if (Launcher.WaitForReady(waitTillReadyTimeout)) // sud is also ready. subscribed for all notification channels
            {
                Task.Run(() =>
                    Pipes.ListenAndRespondToChannel($"{channelId}:sudo-host-control",
                                                    onData: Launcher.HandleCommand,
                                                    respondWith: () =>
                                                    {
                                                        Launcher.ProcessStartedEvent.WaitOne(operationTimeout);
                                                        Launcher.ProcessExitedEvent.WaitOne();
                                                        return Launcher.Process.ExitCode.ToString();
                                                    }));

                Task.Run(() =>
                    Pipes.ListenToChannel($"{channelId}:sudo-host-input", Launcher.HandleInput));

                Launcher.ProcessExitedEvent.WaitOne();

                if (config.multi_run)
                    Restart(channelId, requesterPid); // note channelId and requesterPid are not always the same (e.g. in run-single mode)
            }
        }
        catch (Exception e)
        {
            Launcher.ReportError($"Sudo-host error", e.Message);
        }
        finally
        {
            ReleaseMutex();
        }
    }

    static void ReleaseMutex()
    {
        try
        {
            mutex?.ReleaseMutex();
            mutex?.Dispose();
        }
        catch { }
    }

    static Process Restart(string channelId, int requester)
    {
        var process = new Process();

        process.StartInfo.FileName = Assembly.GetExecutingAssembly().Location;
        process.StartInfo.Arguments = $"-channel:{channelId} -wait:{Process.GetCurrentProcess().Id} -requester:{requester}";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        return process;
    }

    static void MonitorProcess(int pid, Action onExit)
    {
        Task.Run(() =>
        {
            var terminal = Process.GetProcessById(pid);
            terminal.WaitForExit();
            onExit();
        });
    }
}