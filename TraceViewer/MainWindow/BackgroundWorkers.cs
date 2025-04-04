using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;


        private async void SaveBackgroundWorker()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

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

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void StopBackgroundTask()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SaveBackgroundWorker();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopBackgroundTask();
        }
    }
}