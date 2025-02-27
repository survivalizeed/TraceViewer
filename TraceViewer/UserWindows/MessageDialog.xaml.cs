using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TraceViewer.UserWindows
{
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : Window
    {
        public MessageDialog(string Title, string Prompt)
        {
            InitializeComponent();
            this.SourceInitialized += InputDialog_SourceInitialized;
            this.Title = Title;
            this.Prompt.Content = Prompt;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void InputDialog_SourceInitialized(object? sender, EventArgs e)
        {
            if (IsWindows10OrHigher())
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int darkModeEnabled = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeEnabled, sizeof(int));
            }
        }

        private bool IsWindows10OrHigher()
        {
            var version = Environment.OSVersion.Version;
            return version.Major >= 10;
        }

        private void Ok_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }
    }
}
