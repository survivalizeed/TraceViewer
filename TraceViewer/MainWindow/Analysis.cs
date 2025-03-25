using System.Windows;
using TraceViewer.Core.Analysis;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void RemoveUselessAssignments_Click(object sender, RoutedEventArgs e)
        {
            DeObfus.DeObfuscate();
        }

        private void RemoveDeobfuscation_Click(object sender, RoutedEventArgs e)
        {
            DeObfus.deObHiddenRows.Clear();
            RefreshView();
        }
    }
}