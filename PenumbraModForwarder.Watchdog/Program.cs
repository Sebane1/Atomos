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
        bool isNewInstance;
        using (new Mutex(true, "PenumbraModForwarder.Launcher", out isNewInstance))
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
            program.Run(args);
        }
    }

    public void Run(string[] args)
    {
        // Determine if Sentry should be enabled
        var enableSentry = (bool)_configurationService.ReturnConfigValue(
            c => c.Common.EnableSentry
        );

        if (enableSentry)
        {
            DependencyInjection.EnableSentryLogging();
        }
        else
        {
            DependencyInjection.DisableSentryLogging();
        }

        _configurationSetup.CreateFiles();

        // Set initialization flag before starting processes
        ApplicationBootstrapper.SetWatchdogInitialization();

        // Set the environment variable for child processes
        Environment.SetEnvironmentVariable("WATCHDOG_INITIALIZED", "true");

        // Hide the console window on Windows if configured
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            HideConsoleWindow();
        }

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var semVersion = version == null
            ? "Local Build"
            : $"{version.Major}.{version.Minor}.{version.Build}";

        // Check for update
        if (_updateService.NeedsUpdateAsync(semVersion, "CouncilOfTsukuyomi/ModForwarder")
            .GetAwaiter().GetResult())
        {
            _logger.Info("Update detected, launching updater");

            // Gather install path from current assembly location
            var currentExePath = assembly.Location;
            var installPath = Path.GetDirectoryName(currentExePath) ?? string.Empty;
            
            var programToRunAfterInstallation = Path.GetFileName(currentExePath);

            // Pass the required arguments to the updater
            _runUpdater.RunDownloadedUpdaterAsync(
                semVersion,                                        
                "CouncilOfTsukuyomi/ModForwarder",          
                installPath,                                        
                enableSentry                          
                // programToRunAfterInstallation            
            ).GetAwaiter().GetResult();

            Environment.Exit(0);
        }

        // Proceed if no update is required
        _processManager.Run();
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