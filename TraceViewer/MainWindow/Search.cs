using System;
using System.Threading.Tasks;
using System.Windows;
using TraceViewer.Core;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private SearchDialog? _searchDialog;

        public void OpenSearchDialog()
        {
            if (TraceHandler.Trace == null || TraceHandler.Trace.Trace.Count == 0)
            {
                var msg = new MessageDialog("Please open a trace before searching.");
                msg.ShowDialog();
                return;
            }

            if (_searchDialog == null || !_searchDialog.IsLoaded)
            {
                _searchDialog = new SearchDialog(this);
            }

            _searchDialog.Show();
            _searchDialog.Activate();
        }

        public void SearchFind_Click(object sender, RoutedEventArgs e)
        {
            OpenSearchDialog();
        }

        public void NavigateToRow(int rowId)
        {
            if (TraceHandler.Trace == null || rowId < 0 || rowId >= TraceHandler.Trace.Trace.Count)
                return;

            // Switch to disassembler view if currently on notes/blocks/etc.
            DisasmViewButton_MouseDown(null, null);

            int visibleCount = InstructionViewItems.Count;
            int targetTop = Math.Max(0, rowId - visibleCount / 2);
            ScrollTo(targetTop);

            int relIndex = rowId - CurrentTopIndex;
            if (relIndex >= 0 && relIndex < InstructionViewItems.Count)
            {
                CurrentHoveredTraceRowId = -1;
                InstructionViewItems[relIndex].OnHover(null, null);
            }
        }
    }
}
