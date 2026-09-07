using System;
using System.Collections.Generic;
using System.Windows;

namespace TraceViewer.Core.Analysis
{
    class Analyzer
    {
        private static readonly List<int> BlockIDs = [];

        public static void Analyze()
        {
            if (TraceHandler.Trace is null) return;
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            DeObfus.DeObfuscate();

            if (window.blockSlicing)
                BlockSlicing();

            if (window.commentKnownObfuscations)
                CommentKnownObfuscations();

            window.RefreshView();
        }

        public static void RemoveAnalysis()
        {
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            if (TraceHandler.Trace is null) return;

            DeObfus.deObHiddenRows.Clear();

            foreach (var id in BlockIDs)
            {
                var row = TraceHandler.Trace.Trace[id];
                if (row is not null && row.block.StartsWith("Block: "))
                {
                    row.block = "";
                    row.isBlockStart = false;
                }
            }
            BlockIDs.Clear();
            window.RefreshView();
        }

        private static void BlockSlicing()
        {
            if (GraphHandler.blocks is null || GraphHandler.uniqueIPAccesses is null)
                return;

            for (int i = 0; i < GraphHandler.blocks.Count; i++)
            {
                var ids = GraphHandler.uniqueIPAccesses[GraphHandler.blocks[i].startIndex].Value;
                for (int j = 0; j < ids.Count; j++)
                {
                    var row = TraceHandler.Trace?.Trace[ids[j]];
                    if (row is not null && !row.isBlockStart)
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
            var traceRows = TraceHandler.Trace?.Trace;
            if (traceRows is null) return;

            for (int i = 0; i < traceRows.Count - 1; i++)
            {
                if (traceRows[i].Disasm.StartsWith("push") && traceRows[i + 1].Disasm.StartsWith("ret"))
                {
                    string operand = traceRows[i].Disasm.Split(' ')[1];
                    traceRows[i].comments = "-----";
                    traceRows[i + 1].comments = $"Jump to {operand}";
                }
            }
        }

        public static void DiffTraces(TraceData other)
        {
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            var trace = TraceHandler.Trace!.Trace;
            int limit = Math.Min(trace.Count, other.Trace.Count);

            for (int i = 0; i < limit; i++)
            {
                var row = trace[i];
                var otherRow = other.Trace[i];
                if (row is not null && otherRow is not null && row.Disasm != otherRow.Disasm)
                    row.comments = $"Diff: {otherRow.Disasm}";
            }
            window.RefreshView();
        }
    }
}
