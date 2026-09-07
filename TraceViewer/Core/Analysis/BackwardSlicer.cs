using System;
using System.Collections.Generic;
using System.Linq;

namespace TraceViewer.Core.Analysis
{
    public static class BackwardSlicer
    {
        public static bool IsActive { get; private set; }
        public static HashSet<int> IncludedRows { get; } = [];
        public static int TargetRowId { get; private set; } = -1;
        public static string TargetDescription { get; private set; } = "";

        /// <summary>
        /// Computes a semantic backward slice starting from targetRowId.
        /// Isolates all instructions in the execution trace that causally contributed to the target value.
        /// </summary>
        public static (bool Success, int SlicedCount, string TargetDesc) RunSlice(TraceData trace, int targetRowId, string? specificReg = null)
        {
            if (trace == null || trace.Trace == null || trace.Trace.Count == 0)
                return (false, 0, "No trace loaded.");

            int targetIndex = trace.Trace.FindIndex(r => r.Id == targetRowId);
            if (targetIndex < 0)
                return (false, 0, $"Target row #{targetRowId} not found.");

            var targetRow = trace.Trace[targetIndex];
            var targetDesc = DeObfus.SliceASM(targetRow);

            var liveRegFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var liveMemAddresses = new HashSet<ulong>();
            bool liveFlags = false;

            string desc = "";
            if (!string.IsNullOrWhiteSpace(specificReg))
            {
                if (DeObfus._regToFamily.TryGetValue(specificReg.ToLowerInvariant(), out var fam))
                {
                    liveRegFamilies.Add(fam);
                    desc = $"{specificReg.ToUpperInvariant()} (Row #{targetRowId})";
                }
            }

            if (liveRegFamilies.Count == 0)
            {
                // If target writes to a register, track that register
                if (!string.IsNullOrEmpty(targetDesc.write_to) && targetDesc.write_to != "memory")
                {
                    if (DeObfus._regToFamily.TryGetValue(targetDesc.write_to.ToLowerInvariant(), out var fam))
                    {
                        liveRegFamilies.Add(fam);
                        desc = $"{targetDesc.write_to.ToUpperInvariant()} (written at #{targetRowId})";
                    }
                }
                // If target writes to memory, track that memory address
                else if (targetDesc.write_to == "memory" && targetRow.Mem != null && targetRow.Mem.Count > 0)
                {
                    foreach (var mem in targetRow.Mem)
                    {
                        liveMemAddresses.Add(mem.Address);
                    }
                    desc = $"Memory [0x{targetRow.Mem[0].Address:X}] (Row #{targetRowId})";
                }

                // If target is a conditional jump / flag consumer, track EFLAGS!
                string[] targetParts = DeObfus.ParseDisassembly(targetRow.Disasm);
                string targetMnem = targetParts.Length > 0 ? targetParts[0].ToLowerInvariant() : "";
                if (targetMnem.StartsWith('j') || targetMnem.StartsWith("cmov") || targetMnem.StartsWith("set"))
                {
                    liveFlags = true;
                    if (string.IsNullOrEmpty(desc))
                        desc = $"Flags for {targetRow.Disasm} (Row #{targetRowId})";
                }

                // If still nothing (e.g. cmp, test, ret, push), track all its inputs!
                if (liveRegFamilies.Count == 0 && liveMemAddresses.Count == 0 && !liveFlags)
                {
                    foreach (var rf in targetDesc.read_from)
                    {
                        if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var fam))
                            liveRegFamilies.Add(fam);
                    }
                    if (targetRow.Mem != null)
                    {
                        foreach (var mem in targetRow.Mem)
                            liveMemAddresses.Add(mem.Address);
                    }
                    desc = $"Inputs of {targetRow.Disasm} (Row #{targetRowId})";
                }
            }

            IncludedRows.Clear();
            IncludedRows.Add(targetRow.Id);

            // Walk backwards from targetIndex - 1 down to 0
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                if (liveRegFamilies.Count == 0 && liveMemAddresses.Count == 0 && !liveFlags)
                    break;

                var row = trace.Trace[i];
                var descriptor = DeObfus.SliceASM(row);

                bool contributes = false;

                // 1. Check if this instruction sets flags when flags are live
                string[] parts = DeObfus.ParseDisassembly(row.Disasm);
                string mnem = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
                if (liveFlags && (descriptor.type is DisasmType.Manipulator or DisasmType.User || mnem is "cmp" or "test" or "sahf"))
                {
                    contributes = true;
                    liveFlags = false; // Flag producer found!

                    // Add inputs needed for this flag setting instruction
                    foreach (var rf in descriptor.read_from)
                    {
                        if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var readFam))
                            liveRegFamilies.Add(readFam);
                    }
                    if (row.Mem != null)
                    {
                        foreach (var m in row.Mem)
                            liveMemAddresses.Add(m.Address);
                    }
                }

                // 2. Check register write
                if (!string.IsNullOrEmpty(descriptor.write_to) && descriptor.write_to != "memory")
                {
                    if (DeObfus._regToFamily.TryGetValue(descriptor.write_to.ToLowerInvariant(), out var writtenFam))
                    {
                        if (liveRegFamilies.Contains(writtenFam))
                        {
                            contributes = true;

                            // If this is a full overwrite (Setter or zeroing idiom), remove writtenFam
                            if (descriptor.type == DisasmType.Setter)
                            {
                                liveRegFamilies.Remove(writtenFam);
                            }

                            // Add all input registers read by this instruction
                            foreach (var rf in descriptor.read_from)
                            {
                                if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var readFam))
                                {
                                    liveRegFamilies.Add(readFam);
                                }
                            }

                            // If this instruction read memory, add that memory address to liveMemAddresses
                            if (row.Mem != null && row.Mem.Count > 0)
                            {
                                foreach (var m in row.Mem)
                                {
                                    liveMemAddresses.Add(m.Address);
                                }
                            }
                        }
                    }
                }

                // 3. Check memory write
                if (descriptor.write_to == "memory" && row.Mem != null && row.Mem.Count > 0)
                {
                    bool wroteToTrackedMem = false;
                    foreach (var m in row.Mem)
                    {
                        if (liveMemAddresses.Contains(m.Address))
                        {
                            wroteToTrackedMem = true;
                            liveMemAddresses.Remove(m.Address); // Satisfied!
                        }
                    }

                    if (wroteToTrackedMem)
                    {
                        contributes = true;
                        // Add input registers needed for the memory write
                        foreach (var rf in descriptor.read_from)
                        {
                            if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var readFam))
                            {
                                liveRegFamilies.Add(readFam);
                            }
                        }
                    }
                }

                if (contributes)
                {
                    IncludedRows.Add(row.Id);
                }
            }

            IsActive = true;
            TargetRowId = targetRow.Id;
            TargetDescription = desc;

            return (true, IncludedRows.Count, desc);
        }

        public static void ClearSlice()
        {
            IsActive = false;
            IncludedRows.Clear();
            TargetRowId = -1;
            TargetDescription = "";
        }
    }
}
