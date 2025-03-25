using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void Fpu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            // Toggle FPU registers visibility
            _toggleFpu = !_toggleFpu;
            Fpu.Foreground = _toggleFpu ? Brushes.White : Brushes.Gray;

            Visibility fpuVisibility = _toggleFpu ? Visibility.Visible : Visibility.Collapsed;

            foreach (var item in RegisterViewItems)
            {
                if (item.registerType == RegisterType.FPU)
                {
                    item.Visibility = fpuVisibility;
                }
            }
        }
    }
}