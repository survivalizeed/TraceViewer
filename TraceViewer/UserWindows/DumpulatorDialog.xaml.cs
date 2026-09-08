using System.Windows;
using System.Windows.Input;

namespace TraceViewer.UserWindows
{
    public partial class DumpulatorDialog : Window
    {
        public DumpulatorDialog(MainWindow? mainWindow = null)
        {
            InitializeComponent();
            if (mainWindow != null)
            {
                DumpulatorMainControl.SetMainWindow(mainWindow);
            }
        }

        public void LoadScript(string script, string dumpPath)
        {
            DumpulatorMainControl.SetScriptCode(script);
            DumpulatorMainControl.SetDumpPath(dumpPath);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void MinimizeBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ToggleMaximize();
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Close();
            }
        }
    }
}
