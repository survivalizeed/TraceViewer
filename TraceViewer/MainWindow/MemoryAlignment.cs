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
        private void StackAlignmentBase_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;
            InputDialog input = new InputDialog("Put in a stack alignment base address:");
            input.ShowDialog();
            var res = input.GetResult();
            if (!string.IsNullOrEmpty(res))
            {
                try
                {
                    ulong base_address = Convert.ToUInt64(res, 16);
                    bool found = false;
                    foreach (var stack in MemoryHandler.stacks)
                    {
                        if (stack.ContainsKey(base_address))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        MessageDialog messageDialog = new MessageDialog("The alignment base address you entered can't be found in\r\nthe current trace!");
                        messageDialog.ShowDialog();
                        return;
                    }
                    WPF_TraceRow.stack_alignment_base = base_address;
                }
                catch (FormatException)
                {
                    MessageDialog messageDialog = new MessageDialog("Invalid input. Use an address in hexadecimal format!");
                    messageDialog.ShowDialog();
                }
            }
        }

        private void StackAlignment_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            InputDialog input = new InputDialog("Put in a stack alignment value:");
            input.ShowDialog();
            var res = input.GetResult();
            if (!string.IsNullOrEmpty(res))
            {
                try
                {
                    int alignment = Convert.ToInt32(res, 16);
                    if (alignment < 1 || alignment > 16)
                    {
                        MessageDialog messageDialog = new MessageDialog("Invalid input. Use a value between 1 and 16!");
                        messageDialog.ShowDialog();
                        return;
                    }
                    WPF_TraceRow.stack_alignment = alignment;
                }
                catch (FormatException)
                {
                    MessageDialog messageDialog = new MessageDialog("Invalid input. Use a value between 1 and 16!");
                    messageDialog.ShowDialog();
                }
            }
        }

        private void HeapAlignmentBase_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;
            InputDialog input = new InputDialog("Put in a heap alignment base address:");
            input.ShowDialog();
            var res = input.GetResult();
            if (!string.IsNullOrEmpty(res))
            {
                try
                {
                    ulong base_address = Convert.ToUInt64(res, 16);
                    bool found = false;
                    foreach (var heap in MemoryHandler.heaps)
                    {
                        if (heap.ContainsKey(base_address))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        MessageDialog messageDialog = new MessageDialog("The alignment base address you entered can't be found in\r\nthe current trace!");
                        messageDialog.ShowDialog();
                        return;
                    }
                    WPF_TraceRow.heap_alignment_base = base_address;
                }
                catch (FormatException)
                {
                    MessageDialog messageDialog = new MessageDialog("Invalid input. Use an address in hexadecimal format!");
                    messageDialog.ShowDialog();
                }
            }
        }

        private void HeapAlignment_Click(object sender, RoutedEventArgs e)
        {
            if (TraceHandler.Trace == null)
                return;

            InputDialog input = new InputDialog("Put in a heap alignment value:");
            input.ShowDialog();
            var res = input.GetResult();
            if (!string.IsNullOrEmpty(res))
            {
                try
                {
                    int alignment = Convert.ToInt32(res, 16);
                    if (alignment < 1 || alignment > 16)
                    {
                        MessageDialog messageDialog = new MessageDialog("Invalid input. Use a value between 1 and 16!");
                        messageDialog.ShowDialog();
                        return;
                    }
                    WPF_TraceRow.heap_alignment = alignment;
                }
                catch (FormatException)
                {
                    MessageDialog messageDialog = new MessageDialog("Invalid input. Use a value between 1 and 16!");
                    messageDialog.ShowDialog();
                }
            }
        }

        private void StackHeapToggle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _toggleStack = !_toggleStack;
            StackHeapToggle.Content = _toggleStack ? "  STACK  " : "  HEAP  ";

            if (_toggleStack)
            {
                StackBorderParent.Visibility = Visibility.Visible;
                HeapBorderParent.Visibility = Visibility.Collapsed;
            }
            else
            {
                StackBorderParent.Visibility = Visibility.Collapsed;
                HeapBorderParent.Visibility = Visibility.Visible;
            }
        }
    }
}