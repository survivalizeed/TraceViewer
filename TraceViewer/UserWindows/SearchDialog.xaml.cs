using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TraceViewer.Core;
using TraceViewer.Core.Search;

namespace TraceViewer.UserWindows
{
    public partial class SearchDialog : Window
    {
        private readonly MainWindow _mainWindow;
        private CancellationTokenSource? _searchCts;
        private List<SearchResultItem> _currentResults = [];
        private int _currentResultIndex = -1;
        private string _lastExecutedQuery = "";

        public SearchDialog(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            this.Owner = mainWindow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchQueryTextBox.Focus();
            SearchQueryTextBox.SelectAll();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                CancelActiveSearch();
                this.Hide();
            }
        }

        private SearchOptions BuildOptions()
        {
            return new SearchOptions
            {
                Query = SearchQueryTextBox.Text.Trim(),
                Mode = SearchMode.Smart,
                SearchDisasm = true,
                SearchRegisters = true,
                SearchMemory = true,
                SearchComments = true,
                MatchCase = false
            };
        }

        private async void SearchBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                await ExecuteFindAllAsync();
            }
        }

        private async void SearchQueryTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await ExecuteFindAllAsync();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                this.Hide();
            }
        }

        private void ResultsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                this.Hide();
            }
        }

        public async Task ExecuteFindAllAsync()
        {
            if (TraceHandler.Trace == null || TraceHandler.Trace.Trace.Count == 0)
            {
                StatusLabel.Content = "No trace loaded.";
                return;
            }

            var options = BuildOptions();
            if (string.IsNullOrWhiteSpace(options.Query))
            {
                StatusLabel.Content = "Please enter a search query.";
                return;
            }

            CancelActiveSearch();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            StatusLabel.Content = "Searching...";
            SearchProgressBar.Visibility = Visibility.Visible;
            SearchProgressBar.Value = 0;

            var progress = new Progress<int>(percent =>
            {
                SearchProgressBar.Value = percent;
            });

            var sw = Stopwatch.StartNew();
            try
            {
                var results = await Task.Run(() => SearchEngine.ExecuteSearch(TraceHandler.Trace, options, progress, token), token);
                sw.Stop();

                _currentResults = results;
                _lastExecutedQuery = options.Query;
                _currentResultIndex = -1;

                ResultsListBox.ItemsSource = _currentResults;

                if (results.Count > 0)
                {
                    StatusLabel.Content = $"Found {results.Count} matches in {TraceHandler.Trace.Trace.Count} rows ({sw.ElapsedMilliseconds} ms)";
                    ResultsListBox.SelectedIndex = 0;
                    ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
                }
                else
                {
                    StatusLabel.Content = $"No matches found for '{options.Query}' ({sw.ElapsedMilliseconds} ms)";
                }
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Content = "Search canceled.";
            }
            catch (Exception ex)
            {
                StatusLabel.Content = $"Search error: {ex.Message}";
            }
            finally
            {
                SearchProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsListBox.SelectedItem is SearchResultItem item)
            {
                _currentResultIndex = ResultsListBox.SelectedIndex;
                _mainWindow.NavigateToRow(item.RowId);
                StatusLabel.Content = $"Match {_currentResultIndex + 1} of {_currentResults.Count} (Row {item.RowId})";
            }
        }

        private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsListBox.SelectedItem is SearchResultItem item)
            {
                _mainWindow.NavigateToRow(item.RowId);
            }
        }

        private void CancelActiveSearch()
        {
            if (_searchCts != null)
            {
                _searchCts.Cancel();
                _searchCts.Dispose();
                _searchCts = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            CancelActiveSearch();
            base.OnClosed(e);
        }
    }
}
