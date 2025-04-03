using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (IsWindows10OrHigher())
            {
                // Enable immersive dark mode for Windows 10 and higher
                var hwnd = new WindowInteropHelper(this).Handle;
                int darkModeEnabled = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeEnabled, sizeof(int));
            }
        }

        private bool IsWindows10OrHigher()
        {
            // Check if the OS version is Windows 10 or higher
            return Environment.OSVersion.Version.Major >= 10;
        }
    }
}