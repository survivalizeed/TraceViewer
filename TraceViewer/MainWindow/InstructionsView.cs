using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void InstructionsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the ScrollViewer within the InstructionsView template
            if (sender is ItemsControl itemsControl &&
                itemsControl.Template.FindName("InstructionsViewScrollViewer", itemsControl) is ScrollViewer scrollViewer)
            {
                InstructionsScrollViewer = scrollViewer;
            }
            else
            {
                throw new InvalidOperationException("ScrollViewer not found in template");
            }
        }

        private void TitleLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Label label)
            {
                UpdateInstructionViewColumnWidth(label.Name, e.NewSize.Width);
            }
        }

        private void UpdateInstructionViewColumnWidth(string columnName, double newWidth)
        {
            InstructionsView.BeginInit();
            try
            {
                foreach (var item in InstructionViewItems)
                {
                    // Update column widths based on label name
                    switch (columnName)
                    {

                        case "Id":
                            item.id.Width = newWidth;
                            item.id_border.Width = newWidth;
                            break;
                        case "Address":
                            item.address.Width = newWidth;
                            item.address_border.Width = newWidth;
                            break;
                        case "Disasm":
                            item.disasm.Width = newWidth;
                            item.disasm_border.Width = newWidth;
                            break;
                        case "Changes":
                            item.changes.Width = newWidth;
                            item.changes_border.Width = newWidth;
                            break;
                        case "Comments":
                            item.comments.Width = newWidth;
                            item.mnemonicBrief.Width = newWidth; // Assuming comments and mnemonicBrief share column width
                            break;
                    }
                }
            }
            finally
            {
                InstructionsView.EndInit();
                SetInstructionsViewWidth();
            }
        }

        private void SetInstructionsViewWidth()
        {
            // Calculate and set the minimum and maximum width of InstructionsView based on column widths
            double totalWidth = Cd0.Width.Value + Cd1.Width.Value + Cd2.Width.Value + Cd3.Width.Value + Cd4.Width.Value + Cd5.Width.Value + 8; // Add a small buffer
            if (totalWidth > 0)
            {
                InstructionsView.MinWidth = totalWidth;
                InstructionsView.MaxWidth = totalWidth;
            }
        }

        private void InstructionsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Handle mouse wheel scrolling, with Ctrl key for faster scrolling
            int scrollStep = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? 15 : 3;
            int delta = e.Delta > 0 ? scrollStep : -scrollStep;
            ScrollControl(delta);
        }

        static int index = TraceHandler.load_count; // Initial index for trace loading

        public bool ScrollControl(int steps, bool set = false)
        {
            if (TraceHandler.Trace == null)
                return false;

            if (set)
                index = TraceHandler.load_count;

            int absSteps = Math.Abs(steps);
            if (absSteps > TraceHandler.load_count)
            {
                int fullPageSteps = absSteps / TraceHandler.load_count - 1; // Calculate full page jumps
                int increment = (steps < 0) ? TraceHandler.load_count : -TraceHandler.load_count; // Determine increment direction
                index += increment * fullPageSteps; // Adjust index by full pages

                // Ensure index stays within bounds
                if (index < TraceHandler.load_count) index = TraceHandler.load_count * 2;
                if (index > TraceHandler.Trace.Trace.Count)
                    index = TraceHandler.Trace.Trace.Count - TraceHandler.load_count;

                steps %= TraceHandler.load_count; // Remaining steps after full page jumps
                steps -= increment; // Load one new page for refresh
            }

            bool returnValue = false;
            if (steps > 0) // Scroll Up
            {
                for (int i = 0; i < steps; i++)
                {
                    if (InstructionViewItems.Count > 0 && index - TraceHandler.load_count - 1 >= 0)
                    {
                        InstructionViewItems.RemoveAt(InstructionViewItems.Count - 1); // Remove last item
                        TraceHandler.LoadRange(index - TraceHandler.load_count - 1, index - TraceHandler.load_count, true); // Load new item at top
                        index--;
                        returnValue = true;
                    }
                }
            }
            else if (steps < 0) // Scroll Down
            {
                for (int i = 0; i < Math.Abs(steps); i++)
                {
                    if (InstructionViewItems.Count > 0 && index < TraceHandler.Trace.Trace.Count)
                    {
                        InstructionViewItems.RemoveAt(0); // Remove first item
                        TraceHandler.LoadRange(index, index + 1, false); // Load new item at bottom
                        index++;
                        returnValue = true;
                    }
                }
            }

            // Refresh view after setting index directly if no scroll happened within load_count range
            if (absSteps < TraceHandler.load_count && set)
            {
                RefreshView();
            }

            return returnValue; // Indicate if scroll action was possible
        }
    }
}