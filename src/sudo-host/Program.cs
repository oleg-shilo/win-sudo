// Ignore Spelling: app

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
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
                                                writeResponse: null));

            Task.Run(() =>
                Pipes.ListenToChannel($"sudo-host-input", Launcher.HandleInput));

            while (true)
            {
                Thread.Sleep(1000);
            }
            // $"333");
            // $"{Launcher.process.Id}");

            // execution.Wait();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        // Console.ReadLine();
        // sudoProcess.WaitForProcessExit();
        // try { Process.GetCurrentProcess().Kill(); } catch { }
    }

    static void OnCommand(string command)
    {
        var tokens = command.Split('|');

        if (Launcher.process != null)
            try { Launcher.process.Kill(); } catch { }

        Task.Run(() =>
            Launcher.Run(tokens[0], tokens[1]));
    }

    static void OnUserInput(string data)
    {
        Launcher.process.StandardInput.WriteLine(data);
    }

    static void OnStdInput(byte[] bytes, int length)
    {
        var input = bytes.GetString();

        if (input.StartsWith("$sudo-app:"))
        {
            var tokens = input.Replace("$sudo-app:", "").Split('|');

            if (Launcher.process != null)
                try { Launcher.process.Kill(); } catch { }

            Task.Run(() =>
            {
                Launcher.Run(tokens[0], tokens[1]);
                Process.GetCurrentProcess().Kill();
            });
        }
        else
            Launcher.process.StandardInput.WriteLine(bytes.GetString());
    }
}