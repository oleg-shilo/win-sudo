// Ignore Spelling: app

using System;
using System.Diagnostics;
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
            Program.appOutputSocket.WriteAllText(ex.Message + Environment.NewLine);
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
                var bytes = Encoding.UTF8.GetBytes(buffer, 0, count);
                Program.appOutputSocket.WriteAllBytes(bytes);

                if (process.StandardOutput.EndOfStream)
                    break;
            }
        }
        catch
        {
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
        catch (Exception)
        {
        }
    }
}