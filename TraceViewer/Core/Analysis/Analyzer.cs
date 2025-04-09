using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core.Analysis
{
    class Analyzer
    {
        private static List<int> BlockIDs = new List<int>();

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


        public static void RemoveAnalysis()
        {
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            if (TraceHandler.Trace == null)
                return;

            DeObfus.deObHiddenRows.Clear();

            foreach (var id in BlockIDs)
            {
                var row = TraceHandler.Trace.Trace[id];
                if (row != null && row.block.StartsWith("Block: "))
                {
                    row.block = "";
                    row.isBlockStart = false;
                }
            }
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
                    if (row != null && !row.isBlockStart)
                    {
                        row.block = $"Block: {i} - Execution: {j + 1}/{ids.Count}";
                        row.isBlockStart = true;
                        BlockIDs.Add(row.Id);
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
