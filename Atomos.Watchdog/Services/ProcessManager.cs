﻿using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using Atomos.Watchdog.Interfaces;
using NLog;

namespace Atomos.Watchdog.Services;

public class ProcessManager : IProcessManager, IDisposable
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly bool _isDevMode;
    private readonly string _solutionDirectory;
    private readonly int _port;
    private Process _uiProcess;
    private Process _backgroundServiceProcess;
    private bool _isShuttingDown;
    private readonly int _maxRestartAttempts = 3;
    private int _backgroundServiceRestartCount;

    public ProcessManager()
    {
        _isDevMode = Environment.GetEnvironmentVariable("DEV_MODE") == "true";

        if (_isDevMode)
        {
            _solutionDirectory = GetSolutionDirectory();
            _logger.Info("Solution Directory: {SolutionDirectory}", _solutionDirectory);
        }
        else
        {
            _solutionDirectory = AppContext.BaseDirectory;
        }

        _logger.Info("Running in {Mode} mode.", _isDevMode ? "DEV" : "PROD");

        _port = FindRandomAvailablePort();
        SetupShutdownHandlers();
    }

    public void Run()
    {
        try
        {
            _logger.Info("Starting Penumbra Mod Forwarder");

            _backgroundServiceProcess = StartProcess("Atomos.BackgroundWorker", _port.ToString());
            _uiProcess = StartProcess("Atomos.UI", _port.ToString());
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Failed to start Penumbra Mod Forwarder");
            ShutdownChildProcesses();
            Environment.Exit(1);
        }

        MonitorProcesses(_uiProcess, _backgroundServiceProcess);
    }

    private int FindRandomAvailablePort()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            return ((System.Net.IPEndPoint)socket.LocalEndPoint).Port;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to find an available port");
            throw;
        }
    }

    private Process StartProcess(string projectName, string port)
    {
        _logger.Info("Starting: {ProjectName}", projectName);
        return _isDevMode
            ? StartDevProcess(projectName, port)
            : StartProdProcess($"{projectName}.exe", port);
    }

    private Process StartDevProcess(string projectName, string port)
    {
        string projectDirectory = Path.Combine(_solutionDirectory, projectName);
        string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.csproj");

        if (!File.Exists(projectFilePath))
        {
            _logger.Error("Project file not found at {ProjectFilePath}", projectFilePath);
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectFilePath}\" -- {port}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = projectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        // Copy environment variables
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            startInfo.EnvironmentVariables[entry.Key.ToString()] = entry.Value?.ToString();
        }

        // Force DEV mode environment variables
        startInfo.EnvironmentVariables["WATCHDOG_INITIALIZED"] = "true";
        startInfo.EnvironmentVariables["DEV_MODE"] = "true";

        var process = new Process { StartInfo = startInfo };
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Debug("[{ProjectName} STDOUT]: {Data}", projectName, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Error("[{ProjectName} STDERR]: {Data}", projectName, e.Data);
            }
        };

        process.Exited += (sender, e) =>
        {
            _logger.Info("{ProjectName} exited with code {ExitCode}", projectName, process.ExitCode);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start {ProjectName}", projectName);
            throw;
        }

        return process;
    }

    private Process StartProdProcess(string executableName, string port)
    {
        string executablePath = Path.Combine(AppContext.BaseDirectory, executableName);
        string executableDir = Path.GetDirectoryName(executablePath);

        _logger.Info("Executing executable in PROD Mode: {ExecutablePath}", executablePath);

        if (!File.Exists(executablePath))
        {
            _logger.Error("Executable not found at {ExecutablePath}", executablePath);
            throw new FileNotFoundException($"Executable not found: {executablePath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = port,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = executableDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        // Copy environment variables (including custom ones)
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            startInfo.EnvironmentVariables[entry.Key.ToString()] = entry.Value?.ToString();
        }

        // Mark that the background process is under watchdog's control
        startInfo.EnvironmentVariables["WATCHDOG_INITIALIZED"] = "true";

        var process = new Process { StartInfo = startInfo };
        process.EnableRaisingEvents = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Debug("[{ExecutableName} STDOUT]: {Data}", executableName, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Error("[{ExecutableName} STDERR]: {Data}", executableName, e.Data);
            }
        };

        process.Exited += (sender, e) =>
        {
            _logger.Info("{ExecutableName} exited with code {ExitCode}", executableName, process.ExitCode);
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _logger.Info("Started {ExecutableName} (PID: {ProcessId})", executableName, process.Id);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start {ExecutableName}", executableName);
            throw;
        }

        return process;
    }

    private void SetupShutdownHandlers()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            ShutdownChildProcesses();
        };

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            ShutdownChildProcesses();
        };
    }

    private void ShutdownChildProcesses()
    {
        if (_isShuttingDown) return;

        _isShuttingDown = true;
        _logger.Info("Initiating graceful shutdown of child processes...");

        try
        {
            if (_uiProcess != null && !_uiProcess.HasExited)
            {
                _logger.Info("Closing UI Process (PID: {ProcessId})", _uiProcess.Id);
                _uiProcess.CloseMainWindow();
                _uiProcess.WaitForExit(5000);

                if (!_uiProcess.HasExited)
                {
                    _logger.Warn("UI Process did not exit gracefully, killing process.");
                    _uiProcess.Kill();
                }
            }

            if (_backgroundServiceProcess != null && !_backgroundServiceProcess.HasExited)
            {
                _logger.Info("Closing Background Worker Process (PID: {ProcessId})", _backgroundServiceProcess.Id);

                // Attempt a graceful shutdown via standard input
                if (_backgroundServiceProcess.StartInfo.RedirectStandardInput)
                {
                    _logger.Info("Sending 'shutdown' command to Background Worker via standard input.");
                    try
                    {
                        _backgroundServiceProcess.StandardInput.WriteLine("shutdown");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Unable to send 'shutdown' to Background Worker");
                    }
                }
                
                _backgroundServiceProcess.CloseMainWindow();

                // Wait for up to 5 seconds for the process to exit
                _backgroundServiceProcess.WaitForExit(5000);

                if (!_backgroundServiceProcess.HasExited)
                {
                    _logger.Warn("Background Worker Process did not exit gracefully, killing process.");
                    _backgroundServiceProcess.Kill();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during shutdown");
        }
        finally
        {
            _uiProcess?.Dispose();
            _backgroundServiceProcess?.Dispose();
            _logger.Info("Child processes shutdown complete");
            // Don't call Environment.Exit(0) here - let the main thread handle the exit
        }
    }

    private void MonitorProcesses(Process uiProcess, Process backgroundServiceProcess)
    {
        while (!_isShuttingDown)
        {
            if (uiProcess.HasExited)
            {
                _logger.Info(
                    "UI Process {ProcessId} exited with code {ExitCode}",
                    uiProcess.Id,
                    uiProcess.ExitCode
                );

                if (backgroundServiceProcess != null && !backgroundServiceProcess.HasExited)
                {
                    _logger.Info(
                        "Terminating Background Service (PID: {ProcessId}) due to UI exit",
                        backgroundServiceProcess.Id
                    );
                    ShutdownChildProcesses();
                }
                break;
            }

            if (backgroundServiceProcess.HasExited)
            {
                _logger.Warn("Background Service exited unexpectedly!");

                if (_backgroundServiceRestartCount >= _maxRestartAttempts)
                {
                    _logger.Error("Maximum restart attempts reached for Background Service. Shutting down.");
                    ShutdownChildProcesses();
                    break;
                }

                _backgroundServiceRestartCount++;
                _logger.Info(
                    "Restarting Background Service (Attempt {Attempt}/{MaxAttempts})",
                    _backgroundServiceRestartCount,
                    _maxRestartAttempts
                );

                backgroundServiceProcess = StartProcess("Atomos.BackgroundWorker", _port.ToString());
                _backgroundServiceProcess = backgroundServiceProcess;
            }

            Thread.Sleep(1000);
        }
    }

    private string GetSolutionDirectory()
    {
        string currentDir = AppContext.BaseDirectory;
        while (currentDir != null)
        {
            string solutionFile = Directory.GetFiles(currentDir, "*.sln").FirstOrDefault();
            if (solutionFile != null)
            {
                return currentDir;
            }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new InvalidOperationException("Could not find solution directory");
    }

    public void Dispose()
    {
        ShutdownChildProcesses();
    }
}