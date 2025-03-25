using System.Windows;
using System.Windows.Input;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void Comments_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            // Toggle between displaying comments and mnemonic brief in the comments column
            _toggleMnemonic = !_toggleMnemonic;
            InstructionsView.BeginInit();
            foreach (var item in InstructionViewItems)
            {
                item.display_mnemonic_brief(!_toggleMnemonic); // Call display toggle on each item
            }
            InstructionsView.EndInit();
            Comments.Content = !_toggleMnemonic ? "MNEMONIC" : "COMMENTS"; // Update button content
        }
    }
}