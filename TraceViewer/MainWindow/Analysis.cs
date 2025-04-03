using System.Windows;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;
            ConfirmDialog confirmDialog = new ConfirmDialog("The Analysis can change comments in your project depending on your settings!\r\nIf you are unsure double check your settings.");
            confirmDialog.ShowDialog();
            if(confirmDialog.GetResult())
                DeObfus.DeObfuscate();
        }

        private void RemoveDeobfuscation_Click(object sender, RoutedEventArgs e)
        {
            DeObfus.deObHiddenRows.Clear();
            RefreshView();
        }

        private void AnalyzerSettings_Click(object sender, RoutedEventArgs e)
        {
            List<(string, Option)> options = new List<(string, Option)>();
            options.Add(("Hide Useless Assignments", uselessAssignmentsAnalysis));
            options.Add(("User Influcence Assignment Analysis", userInflucenceAssignmentAnalysis));
            options.Add(("Comment Known Obfuscations", commentKnownObfuscations));
            options.Add(("Block Slicing", blockSlicing));
            OptionsDialog dialog = new OptionsDialog("Analyzer Settings", options);
            dialog.ShowDialog();
        }
    }
}