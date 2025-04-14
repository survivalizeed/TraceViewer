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
        GraphView
    }
    public partial class MainWindow : Window
    {
       
        public void DisasmViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e != null)
                if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonActive(DisasmViewButtonBorder); // Set Disassembler view button as active
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonInactive(BlocksViewButtonBorder); // Deactivate Blocks view button
            SetViewButtonInactive(BookmarksViewButtonBorder); // Deactivate Bookmarks view button
            SetViewButtonInactive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.DisassemblerView); // Set UI state to Disassembler view
        }

        private void NotesViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonActive(NotesViewButtonBorder); // Set Notes view button as active
            SetViewButtonInactive(BlocksViewButtonBorder); // Deactivate Blocks view button
            SetViewButtonInactive(BookmarksViewButtonBorder); // Deactivate Bookmarks view button
            SetViewButtonInactive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.NotesView); // Set UI state to Notes view
        }

        private void BlocksViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonActive(BlocksViewButtonBorder); // Activate Blocks view button
            SetViewButtonInactive(BookmarksViewButtonBorder); // Deactivate Bookmarks view button
            SetViewButtonInactive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.BlocksView); // Set UI state to Notes view
        }

        private void BookmarksViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonInactive(BlocksViewButtonBorder); // Deactivate Blocks view button
            SetViewButtonActive(BookmarksViewButtonBorder); // Set Bookmarks view button as active
            SetViewButtonInactive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.BookmarksView); // Set UI state to Bookmarks view
        }

        private void GraphViewButton_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            SetViewButtonInactive(DisasmViewButtonBorder); // Deactivate Disassembler view button
            SetViewButtonInactive(NotesViewButtonBorder); // Deactivate Notes view button
            SetViewButtonInactive(BlocksViewButtonBorder); // Deactivate Blocks view button
            SetViewButtonInactive(BookmarksViewButtonBorder); // Set Bookmarks view button as active
            SetViewButtonActive(GraphViewButtonBorder);
            SetCurrentUIState(UIState.GraphView); // Set UI state to Bookmarks view
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
        }
    }
}