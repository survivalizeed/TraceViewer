using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void InstructionsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl &&
                itemsControl.Template.FindName("InstructionsViewScrollViewer", itemsControl) is ScrollViewer scrollViewer)
            {
                InstructionsScrollViewer = scrollViewer;
            }
            else
            {
                throw new InvalidOperationException("ScrollViewer not found in template");
            }
            SetInstructionsViewWidth();
        }

        private void TitleLabel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Label label)
            {
                UpdateInstructionViewColumnWidth(label.Name, e.NewSize.Width);
            }
        }

        private void ColumnSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is GridSplitter splitter && splitter.Parent is Grid grid)
            {
                int colIndex = Grid.GetColumn(splitter);
                if (colIndex >= 0 && colIndex < grid.ColumnDefinitions.Count)
                {
                    var colDef = grid.ColumnDefinitions[colIndex];
                    double currentWidth = colDef.ActualWidth > 0 ? colDef.ActualWidth : colDef.Width.Value;
                    double newWidth = Math.Clamp(currentWidth + e.HorizontalChange, colDef.MinWidth, colDef.MaxWidth);
                    colDef.Width = new GridLength(newWidth);

                    string colName = colIndex switch
                    {
                        0 => "Id",
                        1 => "Address",
                        2 => "Disasm",
                        3 => "Changes",
                        4 => "Comments",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(colName))
                    {
                        UpdateInstructionViewColumnWidth(colName, newWidth);
                    }
                }
            }
        }

        public void UpdateInstructionViewColumnWidth(string columnName, double newWidth)
        {
            if (newWidth <= 0) return;
            var gridLen = new GridLength(newWidth);
            foreach (var item in InstructionViewItems)
            {
                switch (columnName)
                {
                    case "Id":
                        item.col0.Width = gridLen;
                        break;
                    case "Address":
                        item.col1.Width = gridLen;
                        break;
                    case "Disasm":
                        item.col2.Width = gridLen;
                        break;
                    case "Changes":
                        item.col3.Width = gridLen;
                        break;
                    case "Comments":
                        item.col4.Width = gridLen;
                        break;
                }
            }
            SetInstructionsViewWidth();
        }

        public void SetInstructionsViewWidth()
        {
            if (InstructionsView == null || Cd0 == null || Cd1 == null || Cd2 == null || Cd3 == null || Cd4 == null)
                return;

            double w0 = Cd0.ActualWidth > 0 ? Cd0.ActualWidth : Cd0.Width.Value;
            double w1 = Cd1.ActualWidth > 0 ? Cd1.ActualWidth : Cd1.Width.Value;
            double w2 = Cd2.ActualWidth > 0 ? Cd2.ActualWidth : Cd2.Width.Value;
            double w3 = Cd3.ActualWidth > 0 ? Cd3.ActualWidth : Cd3.Width.Value;
            double w4 = Cd4.ActualWidth > 0 ? Cd4.ActualWidth : Cd4.Width.Value;
            double totalWidth = w0 + w1 + w2 + w3 + w4;
            if (totalWidth > 0)
            {
                InstructionsView.Width = totalWidth;
            }
        }

        private void InstructionsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            int scrollStep = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ? 15 : 3;
            int delta = e.Delta > 0 ? scrollStep : -scrollStep;
            ScrollControl(delta);
            e.Handled = true;
        }

        private void TraceScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            ScrollTo((int)e.NewValue);
        }

        /// <summary>
        /// Initializes the reusable pool of WPF_TraceRow controls and configures the vertical scrollbar.
        /// </summary>
        public void InitTraceView()
        {
            if (TraceHandler.Trace == null) return;

            int total = TraceHandler.Trace.Trace.Count;
            int visibleCount = Math.Min(TraceHandler.load_count, total);

            // Pool: maintain exactly 'visibleCount' controls in InstructionViewItems
            while (InstructionViewItems.Count < visibleCount)
            {
                InstructionViewItems.Add(new WPF_TraceRow());
            }
            while (InstructionViewItems.Count > visibleCount)
            {
                InstructionViewItems.RemoveAt(InstructionViewItems.Count - 1);
            }

            double w0 = Cd0.ActualWidth > 0 ? Cd0.ActualWidth : Cd0.Width.Value;
            double w1 = Cd1.ActualWidth > 0 ? Cd1.ActualWidth : Cd1.Width.Value;
            double w2 = Cd2.ActualWidth > 0 ? Cd2.ActualWidth : Cd2.Width.Value;
            double w3 = Cd3.ActualWidth > 0 ? Cd3.ActualWidth : Cd3.Width.Value;
            double w4 = Cd4.ActualWidth > 0 ? Cd4.ActualWidth : Cd4.Width.Value;
            foreach (var item in InstructionViewItems)
            {
                item.SetColumnWidths(w0, w1, w2, w3, w4);
            }

            if (TraceScrollBar != null)
            {
                TraceScrollBar.Minimum = 0;
                TraceScrollBar.Maximum = Math.Max(0, total - visibleCount);
                TraceScrollBar.ViewportSize = visibleCount;
                TraceScrollBar.SmallChange = 1;
                TraceScrollBar.LargeChange = Math.Max(1, visibleCount / 2);
                TraceScrollBar.Value = 0;
            }

            SetInstructionsViewWidth();

            CurrentTopIndex = 0;
            ScrollTo(0);
        }

        /// <summary>
        /// Navigates smoothly to the specified row index by updating existing controls in-place.
        /// No UserControls are destroyed or recreated.
        /// </summary>
        public bool ScrollTo(int targetTopIndex)
        {
            if (TraceHandler.Trace == null || InstructionViewItems.Count == 0)
                return false;

            int total = TraceHandler.Trace.Trace.Count;
            int visibleCount = InstructionViewItems.Count;
            int maxTop = Math.Max(0, total - visibleCount);
            int clamped = Math.Clamp(targetTopIndex, 0, maxTop);

            CurrentTopIndex = clamped;
            index = clamped + visibleCount; // Maintained for backwards compatibility

            if (TraceScrollBar != null && Math.Abs(TraceScrollBar.Value - clamped) > 0.01)
            {
                TraceScrollBar.Value = clamped;
            }

            var briefLookup = TraceHandler.BriefLookup;
            var fullLookup = TraceHandler.FullLookup;

            for (int i = 0; i < visibleCount; i++)
            {
                int traceIdx = clamped + i;
                if (traceIdx < total)
                {
                    var row = TraceHandler.Trace.Trace[traceIdx];
                    string instructionMnemonic = row.Disasm.AsSpan().SliceToFirstSpace();
                    string brief = briefLookup?.GetValueOrDefault(instructionMnemonic) ?? "";
                    string full = fullLookup?.GetValueOrDefault(instructionMnemonic) ?? "";
                    InstructionViewItems[i].Bind(row, brief, full);
                }
            }

            // Refresh hover state for whichever row is now under the mouse cursor
            for (int i = 0; i < visibleCount; i++)
            {
                if (InstructionViewItems[i].IsMouseOver)
                {
                    InstructionViewItems[i].OnHover(null, null);
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// Relative or absolute scroll handler.
        /// When 'set' is true, steps is treated as -targetIndex.
        /// When 'set' is false, steps > 0 is scroll up, steps < 0 is scroll down.
        /// </summary>
        public bool ScrollControl(int steps, bool set = false)
        {
            if (TraceHandler.Trace == null)
                return false;

            if (set)
            {
                int target = -steps;
                return ScrollTo(target);
            }

            int targetTop = CurrentTopIndex - steps;
            return ScrollTo(targetTop);
        }
    }
}