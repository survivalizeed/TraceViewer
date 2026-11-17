using System;
using System.Collections.Generic;
using System.Linq;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;

namespace TraceViewer.Core.Diff
{
    public static class DifferentialSlicer
    {
        private static readonly object _sliceLock = new();

        /// <summary>
        /// Executes a differential backward slice on Trace A and Trace B starting from the specified rows.
        /// Isolates the causal data-flow roots that produced differing values or branch decisions.
        /// </summary>
        public static DifferentialSliceResult Run(
            TraceData traceA, int rowIdA,
            TraceData traceB, int rowIdB,
            string? targetReg = null)
        {
            var result = new DifferentialSliceResult
            {
                RowIdA = rowIdA,
                RowIdB = rowIdB,
                TargetRegisterOrCondition = targetReg ?? "Condition/Branch"
            };

            if (traceA?.Trace == null || traceB?.Trace == null)
            {
                result.RootCauseDescription = "Trace data is invalid or empty.";
                return result;
            }

            lock (_sliceLock)
            {
                // Preserve existing slice state in main window if any
                var savedActive = BackwardSlicer.IsActive;
                var savedIncluded = new HashSet<int>(BackwardSlicer.IncludedRows);
                var savedTarget = BackwardSlicer.TargetRowId;
                var savedDesc = BackwardSlicer.TargetDescription;

                try
                {
                    // 1. Slice in Trace A
                    var sliceA = BackwardSlicer.RunSlice(traceA, rowIdA, targetReg);
                    result.TargetDescriptionA = sliceA.TargetDesc;
                    result.SliceRowIdsA = new HashSet<int>(BackwardSlicer.IncludedRows);

                    var rowsA = traceA.Trace.Where(r => result.SliceRowIdsA.Contains(r.Id)).OrderBy(r => r.Id).ToList();
                    result.SliceRowsA = rowsA;

                    // 2. Slice in Trace B
                    var sliceB = BackwardSlicer.RunSlice(traceB, rowIdB, targetReg);
                    result.TargetDescriptionB = sliceB.TargetDesc;
                    result.SliceRowIdsB = new HashSet<int>(BackwardSlicer.IncludedRows);

                    var rowsB = traceB.Trace.Where(r => result.SliceRowIdsB.Contains(r.Id)).OrderBy(r => r.Id).ToList();
                    result.SliceRowsB = rowsB;

                    result.Success = sliceA.Success && sliceB.Success;

                    // 3. Compare slices to find shared operations vs unique roots
                    ulong baseA = TraceNormalizer.EstimateModuleBase(traceA);
                    ulong baseB = TraceNormalizer.EstimateModuleBase(traceB);

                    var setB_Signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in rowsB)
                    {
                        ulong rva = TraceNormalizer.ToRVA(r.Ip, baseB);
                        string norm = TraceNormalizer.NormalizeDisassembly(r.Disasm, baseB);
                        setB_Signatures.Add($"{rva:X}:{norm}");
                    }

                    var setA_Signatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in rowsA)
                    {
                        ulong rva = TraceNormalizer.ToRVA(r.Ip, baseA);
                        string norm = TraceNormalizer.NormalizeDisassembly(r.Disasm, baseA);
                        setA_Signatures.Add($"{rva:X}:{norm}");
                    }

                    var uniqueA = new List<TraceRow>();
                    var sharedA = new List<TraceRow>();
                    foreach (var r in rowsA)
                    {
                        ulong rva = TraceNormalizer.ToRVA(r.Ip, baseA);
                        string norm = TraceNormalizer.NormalizeDisassembly(r.Disasm, baseA);
                        if (setB_Signatures.Contains($"{rva:X}:{norm}"))
                            sharedA.Add(r);
                        else
                            uniqueA.Add(r);
                    }

                    var uniqueB = new List<TraceRow>();
                    foreach (var r in rowsB)
                    {
                        ulong rva = TraceNormalizer.ToRVA(r.Ip, baseB);
                        string norm = TraceNormalizer.NormalizeDisassembly(r.Disasm, baseB);
                        if (!setA_Signatures.Contains($"{rva:X}:{norm}"))
                            uniqueB.Add(r);
                    }

                    result.SharedSliceRowsA = sharedA;
                    result.UniqueSliceRowsA = uniqueA;
                    result.UniqueSliceRowsB = uniqueB;

                    // 4. Synthesize explanation of root cause
                    var rootA = rowsA.Count > 0 ? rowsA[0] : null;
                    var rootB = rowsB.Count > 0 ? rowsB[0] : null;

                    string rootDescA = rootA != null ? $"[#{rootA.Id}] {rootA.Disasm}" : "None";
                    string rootDescB = rootB != null ? $"[#{rootB.Id}] {rootB.Disasm}" : "None";

                    result.RootCauseDescription =
                        $"Differential Slice for '{result.TargetRegisterOrCondition}':\n" +
                        $"• Trace A: {rowsA.Count} instructions contributing (Root: {rootDescA})\n" +
                        $"• Trace B: {rowsB.Count} instructions contributing (Root: {rootDescB})\n" +
                        $"• Unique to A: {uniqueA.Count} instructions | Unique to B: {uniqueB.Count} instructions | Shared: {sharedA.Count} instructions";
                }
                finally
                {
                    // Restore main window slice state so background diff slicing doesn't break primary trace view
                    BackwardSlicer.ClearSlice();
                    if (savedActive)
                    {
                        foreach (var id in savedIncluded)
                            BackwardSlicer.IncludedRows.Add(id);
                    }
                }
            }

            return result;
        }
    }
}
