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
                "The useless assignment detection may flag overwritten function arguments as useless.\r\n\n\r" +
                "Make sure to only use this on actually obfuscated code!", null, 230);
            confirmDialog.ShowDialog();
            if (confirmDialog.GetResult())
                Analyzer.Analyze();
        }

        private void AnalyzerSettings_Click(object sender, RoutedEventArgs e)
        {
            List<(string, Option)> options = new List<(string, Option)>();
            options.Add(("Hide Useless Assignments", uselessAssignmentsAnalysis));
            options.Add(("Comment Known Obfuscations", commentKnownObfuscations));
            options.Add(("Block Slicing", blockSlicing));
            OptionsDialog dialog = new OptionsDialog("Analyzer Settings", options, 680, 170);
            dialog.ShowDialog();
        }

        private void RemoveAnalysis_Click(object sender, RoutedEventArgs e)
        {
            Analyzer.RemoveAnalysis();
        }

    }
}