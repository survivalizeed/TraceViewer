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

namespace TraceViewer.UserControls
{

    public partial class InputDialog : Window
    {
        private string result = "";

        public InputDialog(string Prompt)
        {
            InitializeComponent();
            this.SourceInitialized += InputDialog_SourceInitialized;
            this.Prompt.Content = Prompt;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Input.Focus();
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
            result = Input.Text;
            this.Close();
        }

        private void Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        public string GetResult()
        {
            string tmp = result;
            result = "";
            return tmp;
        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                Ok_MouseDown(null, null);
            }
            if (e.Key == Key.Escape)
            {
                Cancel_MouseDown(null, null);
            }
        }
    }
}
