using System;
using Atomos.UI.Extensions;
using Atomos.UI.Interfaces;
using Atomos.UI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommonLib.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Atomos.UI.Views
{
    public partial class MainWindow : Window
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IConfigurationService _configuration;
        private readonly ITaskbarFlashService? _taskbarFlashService;

        public MainWindow()
        {
            InitializeComponent();
        }
        
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
    
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.WindowSize = new Size(e.NewSize.Width, e.NewSize.Height);
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
    
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.WindowSize = new Size(Bounds.Width, Bounds.Height);
            }
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            
            // Stop flashing when window gets focus
            _taskbarFlashService?.StopFlashing();
            _logger.Debug("Window got focus, stopped taskbar flashing");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        public MainWindow(IConfigurationService configuration)
        {
            _configuration = configuration;
            
            // Try to get the taskbar flash service from the service provider
            try
            {
                _taskbarFlashService = Program.ServiceProvider?.GetService<ITaskbarFlashService>();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Could not get ITaskbarFlashService from service provider");
            }
            
            InitializeComponent();

            // Subscribe to window events to stop flashing
            this.Activated += (s, e) =>
            {
                _taskbarFlashService?.StopFlashing();
                _logger.Debug("Window activated, stopped taskbar flashing");
            };

            this.PointerPressed += (s, e) =>
            {
                _taskbarFlashService?.StopFlashing();
            };

            var titleBar = this.FindControl<Grid>("TitleBar");
            titleBar.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginMoveDrag(e);
                }
            };

            // Direct event handling for window controls
            this.Get<Button>("MinimizeButton").Click += (s, e) =>
            {
                _logger.Info("Minimize button clicked");
                if ((bool)_configuration.ReturnConfigValue(x => x.UI.MinimizeToTray)) {
                    HiddenWindows.HideMainWindow();
                } else {
                    // Minimize to taskbar
                    WindowState = WindowState.Minimized;
                }
            };

            this.Get<Button>("CloseButton").Click += (s, e) =>
            {
                if ((bool)_configuration.ReturnConfigValue(x => x.UI.CloseToTray)) 
                {
                    HiddenWindows.HideMainWindow();
                    _logger.Info("Close button clicked, however close to tray is enabled. Use tray to fully close application.");
                } else 
                {
                    Close();
                    _logger.Info("Close button clicked");
                }
            };
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            _logger.Info("Window closing");
            
            // Stop flashing when window is closing
            _taskbarFlashService?.StopFlashing();
        }
    }
}