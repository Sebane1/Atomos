using System.Reactive;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PenumbraModForwarder.UI.Interfaces;
using PenumbraModForwarder.UI.Views;
using ReactiveUI;

namespace PenumbraModForwarder.UI.Controllers;

public class TrayIconController : ITrayIconController
{
    public TrayIconController()
    {
        ShowCommand = ReactiveCommand.Create(ShowMainWindow);
        ExitCommand = ReactiveCommand.Create(ExitApplication);
    }

    public ReactiveCommand<Unit, Unit> ShowCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }
    
    public void ShowMainWindow()
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                desktop.MainWindow = new MainWindow();
            }
            desktop.MainWindow.Show();
            desktop.MainWindow.WindowState = WindowState.Normal;
        }
    }
    
    public void ExitApplication()
    {
        if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}