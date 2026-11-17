using Microsoft.Win32;
using System.Windows;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void StartAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;
            ConfirmDialog confirmDialog = new ConfirmDialog("The Analysis can make mistakes!\r\n" +
                "The useless assignment detection may flag instructions as useless which would be needed in a untraced branch.\r\n\n\r" +
                "Make sure to only use this on actually obfuscated code!", null, 230);
            confirmDialog.ShowDialog();
            if (confirmDialog.GetResult())
                Analyzer.Analyze();
        }

        private void AnalyzerSettings_Click(object sender, RoutedEventArgs e)
        {
            List<(string, Option)> options = new List<(string, Option)>();
            options.Add(("Hide Useless Assignments", uselessAssignmentsAnalysis));
            options.Add(("Constant Folding", constantFolding));
            options.Add(("Comment Known Obfuscations", commentKnownObfuscations));
            options.Add(("Block Slicing", blockSlicing));
            OptionsDialog dialog = new OptionsDialog("Analyzer Settings", options, 680, 170);
            dialog.ShowDialog();
        }

        private void Diff_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
            {
                MessageBox.Show("Please load a primary trace first before launching diff analysis.", "Trace Diff", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Trace Files (*.trace64)|*.trace64",
                Title = "Select Secondary Trace File for Diffing",
                FilterIndex = 1,
                Multiselect = false
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var diffTrace = TraceLoader.OpenX64dbgTrace(openFileDialog.FileName);
                    DiffViewControl.LoadAndDiff(TraceHandler.Trace, diffTrace);
                    DiffViewButton_MouseDown(null, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load trace for diffing: {ex.Message}", "Diff Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveAnalysis_Click(object sender, RoutedEventArgs e)
        {
            Analyzer.RemoveAnalysis();
        }

        private void ClearSlice_Click(object sender, RoutedEventArgs e)
        {
            BackwardSlicer.ClearSlice();
            RefreshView();
        }

    }
}