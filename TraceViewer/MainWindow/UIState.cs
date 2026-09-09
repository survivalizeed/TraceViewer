using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Media;
using System.Windows;
using TraceViewer.Core.Analysis;

namespace TraceViewer
{
    public enum UIState
    {
        DisassemblerView,
        NotesView,
        BlocksView,
        BookmarksView,
        GraphView,
        DumpulatorView
    }
    public partial class MainWindow : Window
    {
        private void DeactivateAllViewButtons()
        {
            SetViewButtonInactive(DisasmViewButtonBorder);
            SetViewButtonInactive(NotesViewButtonBorder);
            SetViewButtonInactive(BlocksViewButtonBorder);
            SetViewButtonInactive(BookmarksViewButtonBorder);
            SetViewButtonInactive(GraphViewButtonBorder);
            SetViewButtonInactive(DumpulatorViewButtonBorder);
        }

        public void DisasmViewButton_MouseDown(object? sender = null, System.Windows.Input.MouseButtonEventArgs? e = null)
        {
            if (e != null && e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(DisasmViewButtonBorder);
            SetCurrentUIState(UIState.DisassemblerView);
        }

        private void NotesViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(NotesViewButtonBorder);
            SetCurrentUIState(UIState.NotesView);
        }

        private void BlocksViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(BlocksViewButtonBorder);
            SetCurrentUIState(UIState.BlocksView);
        }

        private void BookmarksViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(BookmarksViewButtonBorder);
            SetCurrentUIState(UIState.BookmarksView);
        }

        private void GraphViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.GraphView);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitToView();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void DumpulatorViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            DeactivateAllViewButtons();
            SetViewButtonActive(DumpulatorViewButtonBorder);
            DumpulatorViewControl.SetMainWindow(this);
            DumpulatorViewControl.UpdateTraceContextDisplay();
            DumpulatorViewControl.TryAutoDetectDumpFile();
            SetCurrentUIState(UIState.DumpulatorView);
        }

        private void SetViewButtonActive(Border buttonBorder)
        {
            buttonBorder.Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
            buttonBorder.Effect = glowEffect;
        }

        private void SetViewButtonInactive(Border buttonBorder)
        {
            buttonBorder.Background = Brushes.Transparent;
            buttonBorder.Effect = null;
        }

        private void SetCurrentUIState(UIState uiState)
        {
            // Set visibility of different UI views based on UIState enum
            DisassemblerView.Visibility = uiState == UIState.DisassemblerView ? Visibility.Visible : Visibility.Collapsed;
            NotesView.Visibility = uiState == UIState.NotesView ? Visibility.Visible : Visibility.Collapsed;
            BlocksView.Visibility = uiState == UIState.BlocksView ? Visibility.Visible : Visibility.Collapsed;
            BookmarksView.Visibility = uiState == UIState.BookmarksView ? Visibility.Visible : Visibility.Collapsed;
            GraphView.Visibility = uiState == UIState.GraphView ? Visibility.Visible : Visibility.Collapsed;
            DumpulatorView.Visibility = uiState == UIState.DumpulatorView ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}