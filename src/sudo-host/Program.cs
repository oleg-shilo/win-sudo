// Ignore Spelling: app

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sudo;

static class SudoHost
{
    static int Main(string[] args)
    {
        int operationTimeout = 5000;

        try
        {
            int executingProcess = int.Parse(args[0]);
            int sudoProcess = int.Parse(args[1]);

            Task.Run(() =>
                Launcher.OnOutput = Pipes.CreateNotificationChannel($"sudo-host-out").writeTo);

            Task.Run(() =>
                Launcher.OnError = Pipes.CreateNotificationChannel($"sudo-host-error").writeTo);

            Launcher.WaitForReady(operationTimeout);

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

            return Launcher.Process.ExitCode;
        }
        catch (Exception e)
        {
            Launcher.ReportError($"Sudo-host error", e.Message);
            return -1;
        }
    }
}