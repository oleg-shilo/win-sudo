// Ignore Spelling: sudo

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using sudo;

static class Sudo
{
    static int Main(string[] args)
    {
        if (HandleCommands(args))
            return 0;

        try
        {
            var config = Config.Load();

            var exe = args.FirstOrDefault();
            var arguments = args.Skip(1).ToCmdArgs();

            // -----------

            var parentPid = Process.GetCurrentProcess().GetParentPid();
            var channelId = config.multi_run ? parentPid.ToString() : Guid.NewGuid().ToString();

            if (!Pipes.IsChannelOpen($"{channelId}"))
                StartProcessHost(channelId, parentPid);

            // -----------

            Task.Run(() =>
                Pipes.ListenToChannel($"{channelId}:sudo-host-out", onData: WriteToConsoleOut));

            Task.Run(() =>
                Pipes.ListenToChannel($"{channelId}:sudo-host-error", onData: WriteToConsoleError));

            int? exitCode = null;
            Task.Run(() =>
            {
                var controlChannel = Pipes.CreateNotificationChannel($"{channelId}:sudo-host-control");

                controlChannel.writeTo($"{exe}|{arguments}{Environment.NewLine}");
                var reportedExitCode = controlChannel.readFrom();
                exitCode = reportedExitCode.ToInt();
            });

            Task.Run(() =>
            {
                var (writeToHostInput, _) = Pipes.CreateNotificationChannel($"{channelId}:sudo-host-input");
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

    static Process StartProcessHost(string channelId, int requester)
    {
        // note channelId and requester are not always the same (e.g. in run-single mode)

        var process = new Process();

        process.StartInfo.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sudo-host.exe");
        process.StartInfo.Arguments = $"-channel:{channelId} -requester:{requester}";
        process.StartInfo.UseShellExecute = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        return process;
    }

    static bool HandleCommands(string[] args)
    {
        var header = $@"Widows equivalent of Linux 'sudo'. Version {Assembly.GetExecutingAssembly().GetName().Version}
Copyright (C) 2023 Oleg Shilo. www.csscript.net (github.com/oleg-shilo/win-sudo)";
        if (args.ArgPresent("config"))
        {
            var config = Config.Load();

            var command = args.ArgValue("config");

            if (command != null)
                if (command == "run=multi")
                    config.multi_run = true;
                else if (command == "run=single")
                    config.multi_run = false;
                else if (command.StartsWith("idle-timeout="))
                    config.idle_timeout = command.Split('=').LastOrDefault().ToInt();

            config.Save();

            config = Config.Load();
            Console.WriteLine($"{(command != null ? "Updated" : "Current")} configuration:");
            Console.WriteLine("----------------------");
            Console.WriteLine(config.Serialize(userFriendly: true));
        }
        else if (args.ArgPresent("log"))
        {
            Console.WriteLine(Logger.LogFile);
        }
        else if (args.ArgPresent("stop"))
        {
            foreach (var p in Process.GetProcessesByName("sudo-host").Where(p => p.Id != Process.GetCurrentProcess().Id))
                try
                {
                    p.Kill();
                }
                catch { }
        }
        else if (args.ArgPresent("help") || args.ArgPresent("?"))
        {
            Console.WriteLine($@"{header}.
Usage: sudo <executable> [arguments]> | command

Note, before removing this app from the system ensure no its active instance is running in background.
Use 'sudo -stop' command for that.

Commands:
-stop
    Terminates all currently active sudo sessions in all terminals. Only applicable for 'multi' run mode.

-config
    Prints the current configuration.

 -config:run=<single|multi>
    Sets run mode to
      single - displays UAC prompt for every execution
      multi  - displays UAC prompt only once for the terminal/shell session.
               It displays UAC prompt again after the terminal session being idle for longer than 'idle-timeout' config value.
    At the end prints the current configuration.
    Default value is 'single'.

 -config:idle-timeout=<minutes>
    Sets new value for the terminal session idle timeout. Only applicable for `multi` run mode.
    Default is 5 minutes.

-log
    Prints location of log file(s).");
        }
        else if (!File.Exists(Config.ConfigFile) || !args.Any()) // first run on this OS/user-profile
        {
            Console.WriteLine($@"{header}
Use this utility to elevate any process from the Windows terminal/command-prompt:

Usage:  sudo <executable> [arguments] | command

Examples:  sudo choco install <product>
           sudo -config

By default, it displays UAC prompt every time you execute it.
If you prefer a Linux user experience when sudo prompts only the first time it runs, you can achieve this by changing the configuration:

  sudo -config:run=multi

CLI documentation: sudo -?
Project documentation: https://github.com/oleg-shilo/win-sudo");

            Config.Load(); // to ensure the config file is created
        }
        else
        {
            return false;
        }

        return true;
    }
}