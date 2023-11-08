// Ignore Spelling: app

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sudo;

static class Program
{
    static public TcpClient appOutputSocket;
    static public TcpClient appErrorSocket;
    static public int basePort = 0;

    static void Main(string[] args)
    {
        int parentProcess = int.Parse(args[0]);
        int basePort = int.Parse(args[1]);

        var elevatedApp = args.Skip(2).FirstOrDefault();
        var arguments = args.Skip(3);

        appOutputSocket = IpcClient.ConnectTo(basePort);
        appErrorSocket = IpcClient.ConnectTo(basePort + 1);

        Task.Run(() => IpcServer.ListenTo(basePort + 2, OnStdInput));

        parentProcess.WaitForProcessExit();
        try { Process.GetCurrentProcess().Kill(); } catch { }
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