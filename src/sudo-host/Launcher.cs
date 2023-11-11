// Ignore Spelling: app

using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Environment;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sudo;

class Launcher
{
    public static Process process = new Process();
    public static Action<string> onOutput;
    public static Action<string> onError;

    static void ReportError(string message)
    {
        onError?.Invoke(message);
    }

    public static void HandleCommand(string command)
    {
        Task.Run(() =>
        {
            try
            {
                var parts = command.Split(new[] { '|' }, 2);
                Run(parts[0], parts[1]);
            }
            catch (Exception ex) { Console.WriteLine(ex); } // lats line of defense;

            onError($"$(exited:{process.ExitCode}){NewLine}");
        });
    }

    public static void HandleInput(char data)
    {
        try
        {
            process.StandardInput.Write(data);
        }
        catch (Exception ex)
        {
            ReportError(ex.Message + Environment.NewLine);
        }
    }

    public static void Run(string app, string arguments)
    {
        try
        {
            process.StartInfo.FileName = app;
            process.StartInfo.Arguments = arguments;

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.CreateNoWindow = true;
            // process.StartInfo.UseShellExecute = true;
            // process.StartInfo.Verb = "runas";
            process.Start();

            Thread outputThread = new Thread(HandleOutput);
            outputThread.IsBackground = true;
            outputThread.Start();

            Thread errorThread = new Thread(HandleErrors);
            errorThread.IsBackground = true;
            errorThread.Start();

            process.WaitForExit();
            Environment.ExitCode = process.ExitCode;

            outputThread.Join(1000);

            // background threads anyway
            errorThread.Abort();
            outputThread.Abort();
        }
        catch (Exception ex)
        {
            ReportError(ex.Message + Environment.NewLine);
        }
    }

    static void HandleOutput()
    {
        try
        {
            var buffer = new char[255];
            int count = 0;
            while (-1 != (count = process.StandardOutput.Read(buffer, 0, buffer.Length)))
            {
                var bytes = buffer.GetBytes(count);
                var data = bytes.GetString();
                onOutput(data);

                if (process.StandardOutput.EndOfStream)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    static void HandleErrors()
    {
        try
        {
            var chars = new char[255];
            int count = 0;
            while (-1 != (count = process.StandardError.Read(chars, 0, chars.Length)))
            {
                Encoding enc = process.StandardError.CurrentEncoding;
                var bytes = enc.GetBytes(chars, 0, count);
                Console.OpenStandardError().Write(bytes, 0, bytes.Length);
                if (process.StandardError.EndOfStream)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}