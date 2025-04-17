using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core.Analysis
{
    internal class BlocksHandler
    {
        public static ObservableCollection<WPF_BlockRow> BlocksItems = new ObservableCollection<WPF_BlockRow>();

        public static void FillBlocks()
        {
            if (TraceHandler.Trace == null)
                return;

            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            var TraceRows = TraceHandler.Trace.Trace;

            window.BlocksViewItemControl.ItemsSource = BlocksItems;

            BlocksItems.Clear();

            foreach (var row in TraceRows)
            {
                if (row.isBlockStart)
                {
                    BlocksItems.Add(new WPF_BlockRow(row.Id.ToString(), row.Ip.ToString(), row.block));
                }
            }

        }
    }
}
