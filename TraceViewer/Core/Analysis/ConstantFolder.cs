using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TraceViewer.Core.Analysis
{
    public static class ConstantFolder
    {
        public static HashSet<int> FoldedHiddenRows { get; } = [];
        public static Dictionary<int, string> AddedComments { get; } = [];

        private static readonly Regex ImmediateOperandRegex = new(@"\b0x[0-9a-fA-F]+\b|\b\d+\b", RegexOptions.Compiled);

        /// <summary>
        /// Analyzes the trace for multi-step arithmetic/bitwise chains computing a constant.
        /// Folds intermediate obfuscated calculations and annotates the resolved constant.
        /// </summary>
        public static int FoldConstants(TraceData traceData)
        {
            if (traceData == null || traceData.Trace == null || traceData.Trace.Count == 0)
                return 0;

            FoldedHiddenRows.Clear();
            AddedComments.Clear();

            var trace = traceData.Trace;
            int totalChainsFolded = 0;

            // Track active constant chains: RegisterFamily -> (CurrentValue, List of Row IDs)
            var activeChains = new Dictionary<string, (string RegName, ulong Value, List<int> StepRowIds)>();

            for (int i = 0; i < trace.Count; i++)
            {
                var row = trace[i];
                var desc = DeObfus.SliceASM(row);
                string[] parts = DeObfus.ParseDisassembly(row.Disasm);
                if (parts.Length == 0) continue;

                string mnem = parts[0].ToLowerInvariant();
                string op1 = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                string op2 = parts.Length > 2 ? parts[2].ToLowerInvariant() : "";

                // If control flow break (block start), flush existing chains
                if (row.isBlockStart)
                {
                    activeChains.Clear();
                }

                // Check if this instruction reads any tracked constant registers as inputs
                foreach (var rf in desc.read_from)
                {
                    if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var fam))
                    {
                        // Check if this is a self-manipulator on the same register using an immediate or unary op
                        bool isSelfManipulator = desc.type == DisasmType.Manipulator &&
                                                 !string.IsNullOrEmpty(desc.write_to) &&
                                                 DeObfus._regToFamily.TryGetValue(desc.write_to.ToLowerInvariant(), out var writeFam) &&
                                                 writeFam == fam &&
                                                 IsImmediateOrUnary(mnem, op2);

                        if (!isSelfManipulator && activeChains.TryGetValue(fam, out var chain))
                        {
                            // Register is being consumed by a non-folding instruction!
                            // Finalize folding if chain length >= 2
                            if (chain.StepRowIds.Count >= 2)
                            {
                                CommitFoldedChain(chain, trace);
                                totalChainsFolded++;
                            }
                            activeChains.Remove(fam);
                        }
                    }
                }

                // Check if this instruction starts a new constant load
                // e.g. `mov reg, 0x...` or `xor reg, reg` (zeroing)
                if (desc.type == DisasmType.Setter && !string.IsNullOrEmpty(desc.write_to) && desc.write_to != "memory")
                {
                    if (DeObfus._regToFamily.TryGetValue(desc.write_to.ToLowerInvariant(), out var targetFam))
                    {
                        // If it had a previous chain, finalize it if valid
                        if (activeChains.TryGetValue(targetFam, out var prevChain))
                        {
                            if (prevChain.StepRowIds.Count >= 2)
                            {
                                CommitFoldedChain(prevChain, trace);
                                totalChainsFolded++;
                            }
                            activeChains.Remove(targetFam);
                        }

                        // Check if loaded from immediate or zeroing idiom
                        bool isZeroing = (mnem is "xor" or "sub" or "pxor") && op1 == op2;
                        bool isImmediate = !string.IsNullOrEmpty(op2) && ImmediateOperandRegex.IsMatch(op2);

                        if (isZeroing || isImmediate)
                        {
                            ulong groundTruthVal = GetRegisterValue(row, desc.write_to, traceData);
                            activeChains[targetFam] = (desc.write_to, groundTruthVal, new List<int> { row.Id });
                        }
                    }
                }
                // Check if this instruction modifies an existing constant chain with an immediate or unary op
                else if (desc.type == DisasmType.Manipulator && !string.IsNullOrEmpty(desc.write_to) && desc.write_to != "memory")
                {
                    if (DeObfus._regToFamily.TryGetValue(desc.write_to.ToLowerInvariant(), out var targetFam))
                    {
                        if (activeChains.TryGetValue(targetFam, out var existingChain))
                        {
                            if (IsImmediateOrUnary(mnem, op2))
                            {
                                ulong groundTruthVal = GetRegisterValue(row, desc.write_to, traceData);
                                existingChain.StepRowIds.Add(row.Id);
                                activeChains[targetFam] = (desc.write_to, groundTruthVal, existingChain.StepRowIds);
                            }
                            else
                            {
                                // Overwritten or modified by dynamic non-constant operation
                                if (existingChain.StepRowIds.Count >= 2)
                                {
                                    CommitFoldedChain(existingChain, trace);
                                    totalChainsFolded++;
                                }
                                activeChains.Remove(targetFam);
                            }
                        }
                    }
                }
            }

            // Flush remaining chains at end of trace
            foreach (var kvp in activeChains)
            {
                if (kvp.Value.StepRowIds.Count >= 2)
                {
                    CommitFoldedChain(kvp.Value, trace);
                    totalChainsFolded++;
                }
            }
            activeChains.Clear();

            return totalChainsFolded;
        }

        private static bool IsImmediateOrUnary(string mnem, string op2)
        {
            if (mnem is "not" or "neg" or "bswap" or "inc" or "dec") return true;
            return !string.IsNullOrEmpty(op2) && ImmediateOperandRegex.IsMatch(op2);
        }

        private static void CommitFoldedChain((string RegName, ulong Value, List<int> StepRowIds) chain, List<TraceRow> trace)
        {
            int stepCount = chain.StepRowIds.Count;
            if (stepCount < 2) return;

            // Intermediate steps (all except the last one) are marked as hidden
            for (int s = 0; s < stepCount - 1; s++)
            {
                int rowId = chain.StepRowIds[s];
                FoldedHiddenRows.Add(rowId);
                DeObfus.deObHiddenRows.Add(rowId);
            }

            // Last instruction receives the folded comment annotation
            int lastRowId = chain.StepRowIds[stepCount - 1];
            var lastRow = trace.FirstOrDefault(r => r.Id == lastRowId);
            if (lastRow != null)
            {
                string tag = $"[Folded: {chain.RegName.ToUpperInvariant()} = 0x{chain.Value:X} ({stepCount} steps)]";
                if (!string.IsNullOrWhiteSpace(lastRow.comments))
                {
                    if (!lastRow.comments.Contains("[Folded:"))
                    {
                        AddedComments[lastRow.Id] = lastRow.comments;
                        lastRow.comments = $"{lastRow.comments} | {tag}";
                    }
                }
                else
                {
                    AddedComments[lastRow.Id] = "";
                    lastRow.comments = tag;
                }
            }
        }

        private static ulong GetRegisterValue(TraceRow row, string regName, TraceData traceData)
        {
            string lower = regName.ToLowerInvariant();
            if (traceData.Regs != null && traceData.Regs.TryGetValue(lower, out int idx))
            {
                if (idx >= 0 && idx < row.Regs.Count && row.Regs[idx].Length >= 8)
                {
                    return BitConverter.ToUInt64(row.Regs[idx], 0);
                }
            }

            // If subregister, lookup parent 64-bit register
            if (DeObfus._regToFamily.TryGetValue(lower, out var fam) &&
                DeObfus.registerFamilies.TryGetValue(fam, out var members) &&
                members.Length > 0)
            {
                string parent64 = members[0];
                if (traceData.Regs != null && traceData.Regs.TryGetValue(parent64, out int parentIdx))
                {
                    if (parentIdx >= 0 && parentIdx < row.Regs.Count && row.Regs[parentIdx].Length >= 8)
                    {
                        ulong fullVal = BitConverter.ToUInt64(row.Regs[parentIdx], 0);
                        int subIdx = Array.IndexOf(members, lower);
                        return subIdx switch
                        {
                            1 => fullVal & 0xFFFFFFFF, // 32-bit
                            2 => fullVal & 0xFFFF,     // 16-bit
                            3 when members.Length == 5 => (fullVal >> 8) & 0xFF, // ah, bh, ch, dh
                            _ => fullVal & 0xFF        // 8-bit low (al, bl, cl, etc.)
                        };
                    }
                }
            }

            return 0;
        }

        public static void RemoveFoldedConstants(TraceData traceData)
        {
            if (traceData?.Trace == null) return;

            foreach (var kvp in AddedComments)
            {
                var row = traceData.Trace.FirstOrDefault(r => r.Id == kvp.Key);
                if (row != null)
                {
                    row.comments = kvp.Value;
                }
            }
            AddedComments.Clear();

            foreach (var id in FoldedHiddenRows)
            {
                DeObfus.deObHiddenRows.Remove(id);
            }
            FoldedHiddenRows.Clear();
        }
    }
}
