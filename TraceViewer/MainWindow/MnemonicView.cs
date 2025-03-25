using System.Windows;
using System.Windows.Input;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void MnemonicReader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            BigMnemonicViewInactive(); // Deactivate big mnemonic view
        }

        private void BigMnemonicViewInactive()
        {
            // Hide big mnemonic view and restore focus to main view
            MnemonicReaderScrollView.ScrollToVerticalOffset(0); // Reset scroll position
            MnemonicReaderScrollView.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;
        }
    }
}