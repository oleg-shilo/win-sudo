// Ignore Spelling: app

using System;
using System.Diagnostics;
using static System.Environment;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using sudo;

class Launcher
{
    public static Process Process = new Process();
    static Action<string> onOutput;
    static Action<string> onError;
    public static ManualResetEvent ProcessStartedEvent = new ManualResetEvent(false);
    public static ManualResetEvent ProcessExitedEvent = new ManualResetEvent(false);
    static ManualResetEvent stdOutReady = new ManualResetEvent(false);
    static ManualResetEvent stdErrorReady = new ManualResetEvent(false);

    public static Action<string> OnOutput { get => onOutput; set { onOutput = value; stdOutReady.Set(); } }
    public static Action<string> OnError { get => onError; set { onError = value; stdErrorReady.Set(); } }

    public static bool WaitForReady(int timeout) => WaitHandle.WaitAll(new WaitHandle[] { stdOutReady, stdErrorReady }, timeout);

    public static void ReportError(string context, string message)
    {
        $"{context}: {message}".LogError();
        OnError?.Invoke($"{context}: {message}{NewLine}");
    }

    public static void HandleCommand(string command)
    {
        Task.Run(() =>
        {
            // Debug.Assert(false);
            try
            {
                var subCommands = command.Split('\n', '\r').Where(x => x.HasText()).ToArray();

                var exe_and_args = subCommands[0];
                var workingDir = subCommands[1];

                var parts = exe_and_args.Split(new[] { '|' }, 2);
                Run(parts[0], parts[1], workingDir);
            }
            catch (Exception e)
            {
                ReportError(nameof(HandleCommand), e.Message);
            }
        });
    }

    public static void HandleInput(char data)
    {
        try
        {
            Process.StandardInput.Write(data);
        }
        catch (Exception e)
        {
            ReportError(nameof(HandleInput), e.Message);
        }
    }

    public static int StartedProcessId = 0;

    public static void Run(string app, string arguments, string workingDir)
    {
        // Debug.Assert(false);

        try
        {
            Process.StartInfo.FileName = app;
            Process.StartInfo.Arguments = arguments;
            Process.StartInfo.WorkingDirectory = workingDir;

            Process.StartInfo.UseShellExecute = false;
            Process.StartInfo.RedirectStandardError = true;
            Process.StartInfo.RedirectStandardOutput = true;
            Process.StartInfo.RedirectStandardInput = true;
            Process.StartInfo.CreateNoWindow = true;
            // process.StartInfo.UseShellExecute = true;
            // process.StartInfo.Verb = "runas";
            Process.Start();

            Thread outputThread = new Thread(HandleOutput);
            outputThread.IsBackground = true;
            outputThread.Start();

            Thread errorThread = new Thread(HandleErrors);
            errorThread.IsBackground = true;
            errorThread.Start();

            ProcessStartedEvent.Set();

            Process.WaitForExit();

            ProcessExitedEvent.Set();

            outputThread.Join(1000);

            // background threads anyway
            errorThread.Abort();
            outputThread.Abort();
        }
        catch (Exception e)
        {
            ReportError(nameof(Run), e.Message);
            // let the execution fell through
            ProcessStartedEvent.Set();
        }
    }

    static void HandleOutput()
    {
        try
        {
            var buffer = new char[255];
            int count = 0;
            while (-1 != (count = Process.StandardOutput.Read(buffer, 0, buffer.Length)))
            {
                var bytes = buffer.GetBytes(count);
                var data = bytes.GetString();
                OnOutput(data);

                if (Process.StandardOutput.EndOfStream)
                    break;
            }
        }
        catch (Exception e)
        {
            ReportError(nameof(HandleOutput), e.Message);
        }
    }

    static void HandleErrors()
    {
        try
        {
            var chars = new char[255];
            int count = 0;
            while (-1 != (count = Process.StandardError.Read(chars, 0, chars.Length)))
            {
                Encoding enc = Process.StandardError.CurrentEncoding;
                var bytes = enc.GetBytes(chars, 0, count);
                Console.OpenStandardError().Write(bytes, 0, bytes.Length);
                if (Process.StandardError.EndOfStream)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString()); // last line of defense
        }
    }
}