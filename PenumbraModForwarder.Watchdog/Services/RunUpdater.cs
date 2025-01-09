using System.Diagnostics;
using NLog;
using PenumbraModForwarder.Common.Interfaces;
using PenumbraModForwarder.Watchdog.Interfaces;

namespace PenumbraModForwarder.Watchdog.Services
{
    public class RunUpdater : IRunUpdater
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IDownloadUpdater _downloadUpdater;

        public RunUpdater(IDownloadUpdater downloadUpdater)
        {
            _downloadUpdater = downloadUpdater;
        }

        /// <summary>
        /// Downloads and extracts the updater, then runs the downloaded file.
        /// </summary>
        public async Task<bool> RunDownloadedUpdaterAsync(CancellationToken ct)
        {
            _logger.Info("Starting updater retrieval...");

            var updaterExePath = await _downloadUpdater.DownloadAndExtractLatestUpdaterAsync(ct);
            if (updaterExePath == null)
            {
                _logger.Warn("Updater retrieval failed. No file to run.");
                return false;
            }

            _logger.Info("Updater retrieved at {UpdaterExePath}. Attempting to run.", updaterExePath);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = updaterExePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                Process.Start(processStartInfo);
                _logger.Info("Updater is now running.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while attempting to run the updater file.");
                return false;
            }
        }
    }
}