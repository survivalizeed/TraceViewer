using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _saveCancellationTokenSource;
        private CancellationTokenSource _periodicCancellationTokenSource;


        private async void SaveBackgroundWorker()
        {
            _saveCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _saveCancellationTokenSource.Token;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Run(() =>
                    {
                        if (_current_project_path != "")
                        {
                            SaveProjectToFile(_current_project_path);
                        }
                    }, cancellationToken);

                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _saveCancellationTokenSource.Dispose();
                _saveCancellationTokenSource = null;
            }
        }

        private void StopSaveBackgroundTask()
        {
            _saveCancellationTokenSource?.Cancel();
        }

        private async void PeriodicBackgroundWorker()
        {
            _periodicCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _periodicCancellationTokenSource.Token;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Run(() =>
                    {
                        CreateBackUp("periodic_backup", true);
                    }, cancellationToken);

                    await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _periodicCancellationTokenSource.Dispose();
                _periodicCancellationTokenSource = null;
            }
        }

        private void StopPeriodicBackgroundTask()
        {
            _periodicCancellationTokenSource?.Cancel();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SaveBackgroundWorker();
            PeriodicBackgroundWorker();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopSaveBackgroundTask();
            StopPeriodicBackgroundTask();
        }

    }
}