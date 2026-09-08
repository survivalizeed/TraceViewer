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

        private static readonly HashSet<string> _flagModifyingMnemonics = new(StringComparer.OrdinalIgnoreCase)
        {
            "cmp", "test", "add", "sub", "adc", "sbb", "neg", "and", "or", "xor",
            "inc", "dec", "shl", "shr", "sar", "rol", "ror", "imul", "mul", "div", "idiv"
        };

        private static readonly HashSet<string> _pureFlagSetters = new(StringComparer.OrdinalIgnoreCase)
        {
            "cmp", "test"
        };

        /// <summary>
        /// Computes a high-precision semantic backward data-flow slice starting from targetRowId.
        /// Isolates all instructions in the execution trace that causally contributed to the target's inputs or state.
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
            string[] targetParts = DeObfus.ParseDisassembly(targetRow.Disasm);
            string targetMnem = targetParts.Length > 0 ? targetParts[0].ToLowerInvariant() : "";
            string targetOp1 = targetParts.Length > 1 ? targetParts[1].ToLowerInvariant() : "";
            string targetOp2 = targetParts.Length > 2 ? targetParts[2].ToLowerInvariant() : "";

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
            else
            {
                // Seed based on the instruction type:

                // 1. Conditional branches / cmov / setcc -> seed flags
                if (IsConditional(targetMnem))
                {
                    liveFlags = true;
                    desc = $"Condition Flags for {targetRow.Disasm} (#{targetRowId})";

                    // CMOV also reads operands
                    if (targetMnem.StartsWith("cmov"))
                    {
                        AddReaderRegisters(targetDesc.read_from, liveRegFamilies);
                    }
                }
                // 2. Pure flag setters (cmp, test) -> seed both operands
                else if (_pureFlagSetters.Contains(targetMnem))
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies);
                    if ((targetOp1.Contains('[') || targetOp2.Contains('[')) && targetRow.Mem != null && targetRow.Mem.Count > 0)
                    {
                        liveMemAddresses.Add(targetRow.Mem[0].Address);
                    }
                    desc = $"Operands of {targetRow.Disasm} (#{targetRowId})";
                }
                // 3. Memory store / push -> seed the stored value and memory pointer
                else if (targetDesc.write_to == "memory" || targetMnem is "push" or "pushfq" or "pushf")
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies);
                    desc = $"Stored Value for {targetRow.Disasm} (#{targetRowId})";
                }
                // 4. Instructions with inputs (arithmetic, moves, loads, calls, returns)
                else
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies);

                    // If target read from memory (e.g. mov rax, [rbx+8], pop rax):
                    if ((targetOp2.Contains('[') || targetMnem == "pop" || targetDesc.read_from.Contains("memory"))
                        && targetRow.Mem != null && targetRow.Mem.Count > 0)
                    {
                        liveMemAddresses.Add(targetRow.Mem[0].Address);
                    }

                    // If target is a pure constant setter with no inputs (e.g. xor eax, eax / mov rax, 1)
                    if (liveRegFamilies.Count == 0 && liveMemAddresses.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(targetDesc.write_to) && targetDesc.write_to != "memory")
                        {
                            if (DeObfus._regToFamily.TryGetValue(targetDesc.write_to.ToLowerInvariant(), out var fam))
                            {
                                liveRegFamilies.Add(fam);
                            }
                        }
                    }

                    desc = $"Inputs of {targetRow.Disasm} (#{targetRowId})";
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
                string[] parts = DeObfus.ParseDisassembly(row.Disasm);
                string mnem = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
                string op1 = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                string op2 = parts.Length > 2 ? parts[2].ToLowerInvariant() : "";

                bool contributes = false;

                // -------------------------------------------------------------
                // 1. Check Flag Dependency
                // -------------------------------------------------------------
                if (liveFlags)
                {
                    bool modifiesFlags = _flagModifyingMnemonics.Contains(mnem)
                        || row.highlights.Contains("rflags")
                        || row.highlights.Contains("eflags");

                    if (modifiesFlags)
                    {
                        contributes = true;
                        liveFlags = false; // Flag producer found!

                        // Add all inputs needed for this flag setting instruction
                        AddReaderRegisters(descriptor.read_from, liveRegFamilies);

                        // If it read memory (e.g. cmp qword ptr [rax], 0)
                        if ((op1.Contains('[') || op2.Contains('[')) && row.Mem != null && row.Mem.Count > 0)
                        {
                            liveMemAddresses.Add(row.Mem[0].Address);
                        }
                    }
                }

                // -------------------------------------------------------------
                // 2. Check Memory Write Dependency
                // -------------------------------------------------------------
                if (liveMemAddresses.Count > 0)
                {
                    bool isMemWrite = descriptor.write_to == "memory"
                        || mnem is "push" or "pushfq" or "pushf"
                        || mnem == "call";

                    if (isMemWrite && row.Mem != null && row.Mem.Count > 0)
                    {
                        ulong writeAddr = row.Mem[0].Address;

                        // Check if writeAddr matches any tracked address within 8-byte range
                        ulong matchedTrackedAddr = 0;
                        bool matched = false;
                        foreach (var tracked in liveMemAddresses)
                        {
                            if (Math.Abs((long)(tracked - writeAddr)) < 8)
                            {
                                matchedTrackedAddr = tracked;
                                matched = true;
                                break;
                            }
                        }

                        if (matched)
                        {
                            contributes = true;

                            // If this was a complete write (Setter, push, call), remove the memory address
                            if (descriptor.type == DisasmType.Setter || mnem is "push" or "pushfq" or "call")
                            {
                                liveMemAddresses.Remove(matchedTrackedAddr);
                            }

                            // Add inputs needed for the memory write
                            AddReaderRegisters(descriptor.read_from, liveRegFamilies);
                        }
                    }
                }

                // -------------------------------------------------------------
                // 3. Check Register Write Dependency
                // -------------------------------------------------------------
                if (liveRegFamilies.Count > 0)
                {
                    var writtenRegs = GetWrittenRegisters(row, descriptor, mnem, op1);

                    foreach (var written in writtenRegs)
                    {
                        if (DeObfus._regToFamily.TryGetValue(written.ToLowerInvariant(), out var writtenFam))
                        {
                            if (liveRegFamilies.Contains(writtenFam))
                            {
                                contributes = true;

                                // Full overwrite check (64-bit or 32-bit zero-extended)
                                bool isFullOverwrite = IsFullOverwrite(written);
                                bool isPureSetter = descriptor.type == DisasmType.Setter
                                    || IsZeroingIdiom(mnem, op1, op2);

                                if (isFullOverwrite && isPureSetter)
                                {
                                    liveRegFamilies.Remove(writtenFam);
                                }

                                // Add all inputs read by this instruction
                                AddReaderRegisters(descriptor.read_from, liveRegFamilies);

                                // If this instruction read memory, add the memory address
                                if ((op2.Contains('[') || mnem == "pop" || descriptor.read_from.Contains("memory"))
                                    && row.Mem != null && row.Mem.Count > 0)
                                {
                                    liveMemAddresses.Add(row.Mem[0].Address);
                                }

                                // If this was a CMOV, it also depended on flags
                                if (mnem.StartsWith("cmov"))
                                {
                                    liveFlags = true;
                                }

                                break;
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

        private static bool IsConditional(string mnem)
        {
            if (mnem.StartsWith('j') && mnem != "jmp") return true;
            if (mnem.StartsWith("cmov")) return true;
            if (mnem.StartsWith("set") && mnem.Length > 3) return true;
            return false;
        }

        private static bool IsZeroingIdiom(string mnem, string op1, string op2)
        {
            if (mnem is "xor" or "sub" or "pxor" or "xorps" or "xorpd")
            {
                return !string.IsNullOrEmpty(op1) && op1.Equals(op2, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static bool IsFullOverwrite(string reg)
        {
            if (DeObfus._regToFamilyIndex.TryGetValue(reg.ToLowerInvariant(), out int idx))
            {
                // 0 = 64-bit (rax), 1 = 32-bit (eax, zeroes upper 32 in x86-64)
                return idx <= 1;
            }
            return false;
        }

        private static void AddReaderRegisters(IEnumerable<string> readFrom, HashSet<string> liveRegFamilies)
        {
            foreach (var rf in readFrom)
            {
                if (rf == "memory") continue;
                if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var fam))
                {
                    // Avoid polluting the slice with stack pointer adjustments unless explicitly requested
                    if (fam != "rsp")
                    {
                        liveRegFamilies.Add(fam);
                    }
                }
            }
        }

        private static List<string> GetWrittenRegisters(TraceRow row, DisasmDescriptor descriptor, string mnem, string op1)
        {
            var written = new List<string>(4);

            if (!string.IsNullOrEmpty(descriptor.write_to) && descriptor.write_to != "memory")
            {
                written.Add(descriptor.write_to);
            }

            // Check row highlights for runtime register changes
            foreach (var h in row.highlights)
            {
                if (h != "rflags" && h != "eflags" && h != "rip" && !written.Contains(h))
                {
                    written.Add(h);
                }
            }

            // Implicit instruction register writes
            switch (mnem)
            {
                case "mul" or "imul" when string.IsNullOrEmpty(op1) || !op1.Contains(','):
                    if (!written.Contains("rax")) written.Add("rax");
                    if (!written.Contains("rdx")) written.Add("rdx");
                    break;
                case "div" or "idiv":
                    if (!written.Contains("rax")) written.Add("rax");
                    if (!written.Contains("rdx")) written.Add("rdx");
                    break;
                case "cdq":
                    if (!written.Contains("edx")) written.Add("edx");
                    break;
                case "cqo":
                    if (!written.Contains("rdx")) written.Add("rdx");
                    break;
                case "cdqe":
                    if (!written.Contains("rax")) written.Add("rax");
                    break;
                case "cbw" or "cwd" or "cwde":
                    if (!written.Contains("ax") && !written.Contains("eax")) written.Add("eax");
                    break;
            }

            return written;
        }
    }
}
