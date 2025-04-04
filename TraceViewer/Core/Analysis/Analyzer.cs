using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core.Analysis
{
    class Analyzer
    {

        public static void Analyze()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            DeObfus.DeObfuscate();

            if (window.blockSlicing)
                BlockSlicing();

            window.RefreshView();
        }

        private static void BlockSlicing()
        {
            for (int i = 0; i < GraphHandler.blocks?.Count; i++)
            {
                var ids = GraphHandler.uniqueIPAccesses?[GraphHandler.blocks[i].startIndex].Value;
                for (int j = 0; j < ids?.Count; j++)
                {
                    var row = TraceHandler.Trace?.Trace[ids[j]];
                    if (row != null)
                    {
                        row.comments = $"Block: {i} - Execution: {j + 1}/{ids.Count}";
                    }
                }
            }
        }
    }
}
