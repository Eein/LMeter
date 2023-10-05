using System;
using System.Diagnostics;
using System.Linq;


namespace LMeter.Runtime;

public static class ProcessLauncher
{
    public static void LaunchTotallyNotCef
    (
        string exePath,
        string cactbotUrl,
        ushort httpPort,
        bool enableAudio,
        bool bypassWebSocket
    )
    {
        if (Process.GetProcessesByName("TotallyNotCef").Any()) return;

        var process = new Process();
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += new DataReceivedEventHandler(OnStdOutMessage);
        process.ErrorDataReceived += new DataReceivedEventHandler(OnStdErrMessage);
        process.Exited += (_, _) => LMeterLogger.Logger?.Info
        (
            $"{exePath} exited with code {process?.ExitCode}"
        );

        process.StartInfo.FileName = exePath;
        process.StartInfo.Arguments =
            cactbotUrl + " " + httpPort + " " + (enableAudio ? 1 : 0) + " " + (bypassWebSocket ? 0 : 1);

        LMeterLogger.Logger?.Info($"EXE : {process.StartInfo.FileName}");
        LMeterLogger.Logger?.Info($"ARGS: {process.StartInfo.Arguments}");

        process.StartInfo.EnvironmentVariables["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
        process.StartInfo.EnvironmentVariables.Remove("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception e)
        {
            // Prefer not crashing to not starting this process
            LMeterLogger.Logger?.Info(e.ToString());
        }
    }

    public static void LaunchInstallFixDll(string winNewDllPath, string winOldDllPath)
    {
        var linNewDllPath = WineChecker.WindowsFullPathToLinuxPath(winNewDllPath);
        var linOldDllPath = WineChecker.WindowsFullPathToLinuxPath(winOldDllPath);
        if (linNewDllPath == null || linOldDllPath == null)
        {
            LMeterLogger.Logger?.Error("Could not install DLL fix.");
        }

        var process = new Process();
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => LMeterLogger.Logger?.Info
        (
            $"Process exited with code {process?.ExitCode}"
        );

        process.StartInfo.FileName = "/usr/bin/env";
        process.StartInfo.Arguments = $"mv {linNewDllPath} {linOldDllPath}";

        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = false;
        process.StartInfo.RedirectStandardOutput = false;

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            // Prefer not crashing to not starting this process
            LMeterLogger.Logger?.Info(e.ToString());
        }
    }

    private static void OnStdErrMessage(object? sender, DataReceivedEventArgs e) =>
        LMeterLogger.Logger?.Debug($"STDERR: {e.Data}\n");

    private static void OnStdOutMessage(object? sender, DataReceivedEventArgs e) =>
        LMeterLogger.Logger?.Verbose($"STDOUT: {e.Data}\n");
}
