using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TraceViewer.Core.Analysis
{
    internal class BlocksHandler
    {
        public static ObservableCollection<WPF_BlockRow> BlocksItems = new();

        public static void FillBlocks()
        {
            if (TraceHandler.Trace is null) return;

            var window = System.Windows.Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            window.BlocksViewItemControl.ItemsSource = BlocksItems;
            BlocksItems.Clear();

            foreach (var row in TraceHandler.Trace.Trace)
            {
                if (row.isBlockStart)
                    BlocksItems.Add(new WPF_BlockRow(row.Id.ToString(), row.Ip.ToString(), row.block));
            }
        }
    }
}
