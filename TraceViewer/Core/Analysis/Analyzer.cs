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

            if (window.commentKnownObfuscations)
                CommentKnownObfuscations();

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

        private static void CommentKnownObfuscations()
        {
            var TraceRows = TraceHandler.Trace?.Trace;
            if (TraceRows == null)
                return;
            for (int i = 0; i < TraceRows.Count - 1; i++)
            {
                if (TraceRows[i].Disasm.StartsWith("push"))
                    if(TraceRows[i + 1].Disasm.StartsWith("ret"))
                    {
                        string operand = TraceRows[i].Disasm.Split(' ')[1];
                        TraceRows[i].comments = "-----";
                        TraceRows[i + 1].comments = $"Jump to {operand}";
                    }
            }
        }

    }
}
