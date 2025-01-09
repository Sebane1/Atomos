namespace PenumbraModForwarder.Watchdog.Interfaces;

public interface IRunUpdater
{
    Task<bool> RunDownloadedUpdaterAsync(CancellationToken ct);
}