using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using PenumbraModForwarder.Common.Interfaces;
using PenumbraModForwarder.Common.Services;
using PenumbraModForwarder.Watchdog.Extensions;
using PenumbraModForwarder.Watchdog.Imports;
using PenumbraModForwarder.Watchdog.Interfaces;

namespace PenumbraModForwarder.Watchdog;

internal class Program
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private readonly IConfigurationService _configurationService;
    private readonly IProcessManager _processManager;
    private readonly IConfigurationSetup _configurationSetup;
    private readonly IUpdateService _updateService;
    private readonly IRunUpdater _runUpdater;

    public Program(
        IConfigurationService configurationService,
        IProcessManager processManager,
        IConfigurationSetup configurationSetup,
        IUpdateService updateService,
        IRunUpdater runUpdater)
    {
        _configurationService = configurationService;
        _processManager = processManager;
        _configurationSetup = configurationSetup;
        _updateService = updateService;
        _runUpdater = runUpdater;
    }

    private static void Main(string[] args)
    {
        using (new Mutex(true, "PenumbraModForwarder.Launcher", out var isNewInstance))
        {
            if (!isNewInstance)
            {
                Console.WriteLine("Another instance is already running. Exiting...");
                return;
            }

            var services = new ServiceCollection();
            services.AddApplicationServices();
            services.AddSingleton<Program>();

            var serviceProvider = services.BuildServiceProvider();
            var program = serviceProvider.GetRequiredService<Program>();
            program.Run();
        }
    }

    private void Run()
    {
        try
        {
            _configurationSetup.CreateFiles();

            ApplicationBootstrapper.SetWatchdogInitialization();
            Environment.SetEnvironmentVariable("WATCHDOG_INITIALIZED", "true");

            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var semVersion = version == null
                ? "Local Build"
                : $"{version.Major}.{version.Minor}.{version.Build}";

            if (_updateService.NeedsUpdateAsync(semVersion, "CouncilOfTsukuyomi/ModForwarder")
                .GetAwaiter().GetResult())
            {
                _logger.Info("Update detected, launching updater");

                var currentExePath = assembly.Location;
                var installPath = Path.GetDirectoryName(currentExePath) ?? AppContext.BaseDirectory;

                var programToRunAfterInstallation = Path.GetFileName(currentExePath);

                var updateResult = _runUpdater
                    .RunDownloadedUpdaterAsync(
                        semVersion,
                        "CouncilOfTsukuyomi/ModForwarder",
                        installPath,
                        enableSentry: true,
                        programToRunAfterInstallation
                    )
                    .GetAwaiter()
                    .GetResult();

                if (updateResult)
                {
                    _logger.Info("Update detected, exiting application.");
                    LogManager.Shutdown();
                    LogActiveThreads();
                    Environment.Exit(0);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                HideConsoleWindow();
            }
            _processManager.Run();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred.");
            LogManager.Shutdown();
            LogActiveThreads();
            Environment.Exit(1);
        }
    }
    
    private void LogActiveThreads()
    {
        foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
        {
            _logger.Info($"Thread ID: {thread.Id}, State: {thread.ThreadState}, Priority: {thread.PriorityLevel}");
        }
    }
    
    private void HideConsoleWindow()
    {
        var showWindow = (bool)_configurationService.ReturnConfigValue(
            config => config.AdvancedOptions.ShowWatchDogWindow
        );
        if (showWindow)
        {
            _logger.Info("Showing watchdog window");
            return;
        }

        var handle = DllImports.GetConsoleWindow();
        if (handle == IntPtr.Zero) return;

        _logger.Info("Hiding console window");
        DllImports.ShowWindow(handle, DllImports.SW_HIDE);
        _logger.Info("Console window should now be hidden.");
    }
}