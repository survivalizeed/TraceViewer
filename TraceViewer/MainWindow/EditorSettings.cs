using System.Windows;
using System.Windows.Input;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void EditorSettings_Click(object sender, RoutedEventArgs e)
        {
            List<(string, Option)> options = new List<(string, Option)>();
            options.Add(("Address Based Commenting", addressBasedCommenting));
            OptionsDialog optionsDialog = new OptionsDialog("SETTINGS", options, 500, 200);
            optionsDialog.ShowDialog();

        }
    }
}
