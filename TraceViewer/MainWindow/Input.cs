using System;
using System.Windows;
using System.Windows.Input;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl+G shortcut for "Go To Row" functionality
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.G && TraceHandler.Trace != null)
            {
                InputDialog input = new InputDialog("Put in a row to go to:");
                input.ShowDialog();
                var res = input.GetResult();
                if (!string.IsNullOrEmpty(res))
                {
                    try
                    {
                        int goto_row = Convert.ToInt32(res);
                        ScrollTo(goto_row);
                    }
                    catch (FormatException)
                    {
                        MessageDialog messageDialog = new MessageDialog("Invalid input. Use a numerical value!");
                        messageDialog.ShowDialog();
                    }
                }
            }

            // Handle F2 shortcut for "Rename Block" on current row
            if (e.Key == Key.F2 && CurrentHoveredRow != null)
            {
                CurrentHoveredRow.PromptRenameBlock();
                e.Handled = true;
            }
        }
    }
}