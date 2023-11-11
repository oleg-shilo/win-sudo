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
    static void Main(string[] args)
    {
        try
        {
            int executingProcess = int.Parse(args[0]);
            int sudoProcess = int.Parse(args[1]);

            Task.Run(() =>
                (Launcher.onOutput, _) = Pipes.CreateNotificationChannel($"sudo-host-out"));

            Task.Run(() =>
                (Launcher.onError, _) = Pipes.CreateNotificationChannel($"sudo-host-error"));

            while (Launcher.onOutput == null && Launcher.onError == null)
                Thread.Sleep(50);

            Task.Run(() =>
                Pipes.ListenAndRespondToChannel($"sudo-host-control",
                                                onData: Launcher.HandleCommand,
                                                respondWith: Launcher.RunningProcessId));

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-input", Launcher.HandleInput));

            while (Launcher.StartedProcessId == 0)
            {
                Thread.Sleep(100);
            }
            Launcher.process.WaitForExit();
        }
        catch (Exception e)
        {
            // may want to find the way to report/log in the future
        }
    }
}