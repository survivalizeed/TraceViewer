using System;
using System.Collections.Generic;
using System.Linq;

namespace TraceViewer.Core.Analysis
{
    public readonly record struct MemoryRange(ulong Start, int Size)
    {
        public ulong End => Start + (ulong)Math.Max(1, Size);

        public bool Overlaps(MemoryRange other)
        {
            return Start < other.End && other.Start < End;
        }

        public bool Contains(MemoryRange other)
        {
            return Start <= other.Start && End >= other.End;
        }

        public List<MemoryRange> Subtract(MemoryRange written)
        {
            var remaining = new List<MemoryRange>();
            if (!Overlaps(written))
            {
                remaining.Add(this);
                return remaining;
            }

            if (written.Start > Start)
            {
                remaining.Add(new MemoryRange(Start, (int)(written.Start - Start)));
            }

            if (written.End < End)
            {
                remaining.Add(new MemoryRange(written.End, (int)(End - written.End)));
            }

            return remaining;
        }

        public override string ToString() => $"[0x{Start:X}..0x{End:X} ({Size}b)]";
    }

    public record struct MemAccessDetails(
        MemoryRange Range,
        bool IsWrite,
        bool IsRead,
        string? ValueReg = null,
        ulong? ImmValue = null);

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
        /// Tracks data-flow dynamically through registers and stack memory slots across stack manipulations
        /// (push, pop, sub rsp, add rsp, mov [rsp+...], etc.) without polluting the slice with stack adjustments.
        /// </summary>
        public static (bool Success, int SlicedCount, string TargetDesc) RunSlice(TraceData trace, int targetRowId, string? specificReg = null)
        {
            if (trace == null || trace.Trace == null || trace.Trace.Count == 0)
                return (false, 0, "No trace loaded.");

            int targetIndex = trace.Trace.FindIndex(r => r.Id == targetRowId);
            if (targetIndex < 0)
                return (false, 0, $"Target row #{targetRowId} not found.");

            int pointerSize = trace.PointerSize > 0 ? trace.PointerSize : 8;
            var targetRow = trace.Trace[targetIndex];
            var targetDesc = DeObfus.SliceASM(targetRow);
            string[] targetParts = DeObfus.ParseDisassembly(targetRow.Disasm);
            string targetMnem = targetParts.Length > 0 ? targetParts[0].ToLowerInvariant() : "";
            string targetOp1 = targetParts.Length > 1 ? targetParts[1].ToLowerInvariant() : "";
            string targetOp2 = targetParts.Length > 2 ? targetParts[2].ToLowerInvariant() : "";

            var liveRegFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var liveMemRanges = new List<MemoryRange>();
            bool liveFlags = false;

            string desc = "";

            var targetMemAccesses = GetMemoryAccesses(targetRow, targetDesc, targetMnem, targetOp1, targetOp2, pointerSize);

            if (!string.IsNullOrWhiteSpace(specificReg))
            {
                string specRegLower = specificReg.ToLowerInvariant();

                // If target row produced specificReg by reading from memory (e.g. mov r12, [rsp+0x40] or pop r12):
                var readMemForReg = targetMemAccesses.FirstOrDefault(a => a.IsRead);
                var writtenRegsAtTarget = GetWrittenRegisters(targetRow, targetDesc, targetMnem, targetOp1);

                if (writtenRegsAtTarget.Any(w => w.Equals(specRegLower, StringComparison.OrdinalIgnoreCase)) &&
                    readMemForReg.Range.Size > 0)
                {
                    liveMemRanges.Add(readMemForReg.Range);
                    string snippet = targetOp2.Contains('[') ? targetOp2 : $"[0x{readMemForReg.Range.Start:X}]";
                    desc = $"{specificReg.ToUpperInvariant()} from {snippet} (0x{readMemForReg.Range.Start:X}) (Row #{targetRowId})";
                }
                else
                {
                    if (DeObfus._regToFamily.TryGetValue(specRegLower, out var fam))
                    {
                        liveRegFamilies.Add(fam);
                        desc = $"{specificReg.ToUpperInvariant()} (Row #{targetRowId})";
                    }
                }
            }
            else
            {
                // Seed based on target instruction semantics:

                // 1. Conditional branches / cmov / setcc -> seed flags
                if (IsConditional(targetMnem))
                {
                    liveFlags = true;
                    desc = $"Condition Flags for {targetRow.Disasm} (#{targetRowId})";

                    if (targetMnem.StartsWith("cmov"))
                    {
                        AddReaderRegisters(targetDesc.read_from, liveRegFamilies, targetOp1, targetOp2);
                    }
                }
                // 2. Pure flag setters (cmp, test) -> seed operands
                else if (_pureFlagSetters.Contains(targetMnem))
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies, targetOp1, targetOp2);
                    foreach (var readAcc in targetMemAccesses.Where(a => a.IsRead))
                    {
                        liveMemRanges.Add(readAcc.Range);
                    }
                    desc = $"Operands of {targetRow.Disasm} (#{targetRowId})";
                }
                // 3. Memory store / push -> seed the stored value
                else if (targetDesc.write_to == "memory" || targetMnem is "push" or "pushfq" or "pushf")
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies, targetOp1, targetOp2);
                    foreach (var readAcc in targetMemAccesses.Where(a => a.IsRead))
                    {
                        liveMemRanges.Add(readAcc.Range);
                    }
                    desc = $"Stored Value for {targetRow.Disasm} (#{targetRowId})";
                }
                // 4. Instructions with inputs or memory reads (mov r12, [rsp+0x40], pop, add, etc.)
                else
                {
                    AddReaderRegisters(targetDesc.read_from, liveRegFamilies, targetOp1, targetOp2);

                    var readAccesses = targetMemAccesses.Where(a => a.IsRead).ToList();
                    if (readAccesses.Count > 0)
                    {
                        foreach (var readAcc in readAccesses)
                        {
                            liveMemRanges.Add(readAcc.Range);
                        }

                        var first = readAccesses[0];
                        string memSnippet = targetOp2.Contains('[') ? targetOp2 : (targetOp1.Contains('[') ? targetOp1 : $"[0x{first.Range.Start:X}]");
                        desc = $"Value from {memSnippet} (0x{first.Range.Start:X}) for {targetRow.Disasm} (#{targetRowId})";
                    }
                    else
                    {
                        desc = $"Inputs of {targetRow.Disasm} (#{targetRowId})";
                    }

                    // Pure constant setter with no inputs (e.g. xor eax, eax / mov rax, 1)
                    if (liveRegFamilies.Count == 0 && liveMemRanges.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(targetDesc.write_to) && targetDesc.write_to != "memory")
                        {
                            if (DeObfus._regToFamily.TryGetValue(targetDesc.write_to.ToLowerInvariant(), out var fam))
                            {
                                liveRegFamilies.Add(fam);
                                desc = $"{targetDesc.write_to.ToUpperInvariant()} (Row #{targetRowId})";
                            }
                        }
                    }
                }
            }

            IncludedRows.Clear();
            IncludedRows.Add(targetRow.Id);

            // Walk backwards from targetIndex - 1 down to 0
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                if (liveRegFamilies.Count == 0 && liveMemRanges.Count == 0 && !liveFlags)
                    break;

                var row = trace.Trace[i];
                var descriptor = DeObfus.SliceASM(row);
                string[] parts = DeObfus.ParseDisassembly(row.Disasm);
                string mnem = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
                string op1 = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                string op2 = parts.Length > 2 ? parts[2].ToLowerInvariant() : "";

                bool contributes = false;
                var memAccesses = GetMemoryAccesses(row, descriptor, mnem, op1, op2, pointerSize);

                // -------------------------------------------------------------
                // 1. Check Memory Write Dependency (Stack slots & Memory cells)
                // -------------------------------------------------------------
                if (liveMemRanges.Count > 0)
                {
                    var writeAccesses = memAccesses.Where(a => a.IsWrite).ToList();
                    foreach (var writeAcc in writeAccesses)
                    {
                        var overlappingRanges = liveMemRanges.Where(r => r.Overlaps(writeAcc.Range)).ToList();
                        if (overlappingRanges.Count > 0)
                        {
                            contributes = true;

                            bool isOverwrite = descriptor.type == DisasmType.Setter
                                || mnem is "push" or "pushfq" or "pushf" or "call"
                                || IsZeroingIdiom(mnem, op1, op2);

                            if (isOverwrite)
                            {
                                var nextLiveRanges = new List<MemoryRange>();
                                foreach (var r in liveMemRanges)
                                {
                                    if (r.Overlaps(writeAcc.Range))
                                    {
                                        nextLiveRanges.AddRange(r.Subtract(writeAcc.Range));
                                    }
                                    else
                                    {
                                        nextLiveRanges.Add(r);
                                    }
                                }
                                liveMemRanges = nextLiveRanges;
                            }

                            // Follow data-flow of the value stored into this memory slot
                            if (!string.IsNullOrEmpty(writeAcc.ValueReg) &&
                                DeObfus._regToFamily.TryGetValue(writeAcc.ValueReg.ToLowerInvariant(), out var valFam))
                            {
                                if (!IsStackPointerFamily(valFam))
                                {
                                    liveRegFamilies.Add(valFam);
                                }
                            }
                            else if (writeAcc.Range.Size > 0)
                            {
                                // If stored from another memory location (e.g. push [rax]), track source memory
                                foreach (var readAcc in memAccesses.Where(a => a.IsRead))
                                {
                                    liveMemRanges.Add(readAcc.Range);
                                }
                            }
                        }
                    }
                }

                // -------------------------------------------------------------
                // 2. Check Register Write Dependency
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

                                bool isFullOverwrite = IsFullOverwrite(written);
                                bool isPureSetter = descriptor.type == DisasmType.Setter
                                    || mnem == "pop"
                                    || IsZeroingIdiom(mnem, op1, op2);

                                if (isFullOverwrite && isPureSetter)
                                {
                                    liveRegFamilies.Remove(writtenFam);
                                }

                                // Add input registers (excluding rsp and base rbp in stack addressing)
                                AddReaderRegisters(descriptor.read_from, liveRegFamilies, op1, op2);

                                // If this instruction read memory (e.g. mov rax, [rsp+0x40], pop rax):
                                // Add memory read range to track back to earlier stack writes
                                foreach (var readAcc in memAccesses.Where(a => a.IsRead))
                                {
                                    liveMemRanges.Add(readAcc.Range);
                                }

                                if (mnem.StartsWith("cmov"))
                                {
                                    liveFlags = true;
                                }

                                break;
                            }
                        }
                    }
                }

                // -------------------------------------------------------------
                // 3. Check Flag Dependency
                // -------------------------------------------------------------
                if (liveFlags)
                {
                    bool modifiesFlags = _flagModifyingMnemonics.Contains(mnem)
                        || row.highlights.Contains("rflags")
                        || row.highlights.Contains("eflags");

                    if (modifiesFlags)
                    {
                        contributes = true;
                        liveFlags = false;

                        AddReaderRegisters(descriptor.read_from, liveRegFamilies, op1, op2);

                        foreach (var readAcc in memAccesses.Where(a => a.IsRead))
                        {
                            liveMemRanges.Add(readAcc.Range);
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

        private static bool IsStackPointerFamily(string fam) => fam is "rsp" or "rspx";
        private static bool IsFramePointerFamily(string fam) => fam is "rbp" or "rbpx";

        private static void AddReaderRegisters(
            IEnumerable<string> readFrom,
            HashSet<string> liveRegFamilies,
            string op1 = "",
            string op2 = "")
        {
            bool hasRbpStackMemory = (op1.Contains("rbp", StringComparison.OrdinalIgnoreCase) && op1.Contains('['))
                                  || (op2.Contains("rbp", StringComparison.OrdinalIgnoreCase) && op2.Contains('['));

            foreach (var rf in readFrom)
            {
                if (rf == "memory") continue;
                if (DeObfus._regToFamily.TryGetValue(rf.ToLowerInvariant(), out var fam))
                {
                    // Avoid polluting the slice with stack pointer adjustments
                    if (IsStackPointerFamily(fam)) continue;

                    // Avoid polluting with frame pointer if rbp was merely the base address for stack memory
                    if (IsFramePointerFamily(fam) && hasRbpStackMemory) continue;

                    liveRegFamilies.Add(fam);
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

        /// <summary>
        /// Retrieves the current 64-bit value of a register from the trace row register dump,
        /// handling sub-registers (32-bit, 16-bit, 8-bit).
        /// </summary>
        public static ulong GetRegisterValue(TraceRow row, string regName)
        {
            if (string.IsNullOrWhiteSpace(regName) || row.Regs == null || row.Regs.Count == 0)
                return 0;

            string lower = regName.ToLowerInvariant();
            if (!DeObfus._regToFamily.TryGetValue(lower, out var family))
                return 0;

            int regIdx = family switch
            {
                "raxx" or "rax" => 0,
                "rcxx" or "rcx" => 1,
                "rdxx" or "rdx" => 2,
                "rbxx" or "rbx" => 3,
                "rspx" or "rsp" => 4,
                "rbpx" or "rbp" => 5,
                "rsix" or "rsi" => 6,
                "rdix" or "rdi" => 7,
                "r8x" or "r8" => 8,
                "r9x" or "r9" => 9,
                "r10x" or "r10" => 10,
                "r11x" or "r11" => 11,
                "r12x" or "r12" => 12,
                "r13x" or "r13" => 13,
                "r14x" or "r14" => 14,
                "r15x" or "r15" => 15,
                "ripx" or "rip" => 16,
                _ => -1
            };

            if (regIdx < 0 || regIdx >= row.Regs.Count)
                return 0;

            var bytes = row.Regs[regIdx];
            if (bytes == null || bytes.Length < 8)
                return 0;

            ulong val = BitConverter.ToUInt64(bytes, 0);

            if (DeObfus._regToFamilyIndex.TryGetValue(lower, out int subIdx))
            {
                return subIdx switch
                {
                    1 => val & 0xFFFFFFFF,      // 32-bit (eax, esp)
                    2 => val & 0xFFFF,          // 16-bit (ax, sp)
                    3 => val & 0xFF,            // 8-bit low (al, spl)
                    4 => (val >> 8) & 0xFF,     // 8-bit high (ah, ch, dh, bh)
                    _ => val                    // 64-bit (rax, rsp)
                };
            }
            return val;
        }

        /// <summary>
        /// Evaluates the effective 64-bit virtual memory address for an expression like [rsp + 0x40], [rbp - 8], [rax + rcx*8 + 0x20].
        /// </summary>
        public static ulong? EvaluateMemoryOperand(string operand, TraceRow row)
        {
            if (string.IsNullOrWhiteSpace(operand)) return null;

            int openIdx = operand.IndexOf('[');
            int closeIdx = operand.IndexOf(']', openIdx + 1);
            if (openIdx < 0 || closeIdx <= openIdx) return null;

            string expr = operand.Substring(openIdx + 1, closeIdx - openIdx - 1).Trim();
            if (string.IsNullOrEmpty(expr)) return null;

            var tokens = TokenizeAddressExpression(expr);
            if (tokens.Count == 0) return null;

            long total = 0;
            foreach (var (isPositive, token) in tokens)
            {
                long termVal = 0;
                string t = token.Trim().ToLowerInvariant();

                if (t.Contains('*'))
                {
                    string[] parts = t.Split('*', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        if (long.TryParse(parts[0], out long scale1))
                        {
                            ulong rVal = GetRegisterValue(row, parts[1]);
                            termVal = unchecked((long)rVal) * scale1;
                        }
                        else if (long.TryParse(parts[1], out long scale2))
                        {
                            ulong rVal = GetRegisterValue(row, parts[0]);
                            termVal = unchecked((long)rVal) * scale2;
                        }
                    }
                }
                else if (t.StartsWith("0x") && long.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out long hexVal))
                {
                    termVal = hexVal;
                }
                else if (long.TryParse(t, out long decVal))
                {
                    termVal = decVal;
                }
                else
                {
                    ulong rVal = GetRegisterValue(row, t);
                    termVal = unchecked((long)rVal);
                }

                if (isPositive)
                    total += termVal;
                else
                    total -= termVal;
            }

            return unchecked((ulong)total);
        }

        private static List<(bool isPositive, string token)> TokenizeAddressExpression(string expr)
        {
            var result = new List<(bool, string)>();
            int i = 0;
            bool currentPositive = true;

            while (i < expr.Length)
            {
                char c = expr[i];
                if (c == '+')
                {
                    currentPositive = true;
                    i++;
                }
                else if (c == '-')
                {
                    currentPositive = false;
                    i++;
                }
                else if (char.IsWhiteSpace(c))
                {
                    i++;
                }
                else
                {
                    int start = i;
                    while (i < expr.Length && expr[i] != '+' && expr[i] != '-')
                    {
                        i++;
                    }
                    string token = expr.Substring(start, i - start).Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        result.Add((currentPositive, token));
                    }
                }
            }

            return result;
        }

        public static int InferSizeFromDisasm(string disasm, int pointerSize)
        {
            if (string.IsNullOrEmpty(disasm)) return pointerSize;
            string lower = disasm.ToLowerInvariant();
            if (lower.Contains("byte ptr") || lower.Contains(" byte ")) return 1;
            if (lower.Contains("word ptr") && !lower.Contains("dword") && !lower.Contains("qword")) return 2;
            if (lower.Contains("dword ptr") || lower.Contains(" dword ")) return 4;
            if (lower.Contains("qword ptr") || lower.Contains(" qword ")) return 8;
            if (lower.Contains("xmmword ptr") || lower.Contains(" xmmword ")) return 16;
            if (lower.Contains("ymmword ptr") || lower.Contains(" ymmword ")) return 32;
            return pointerSize;
        }

        /// <summary>
        /// Discovers all memory access ranges (reads and writes) performed by an instruction row,
        /// combining runtime register evaluation, implicit stack operations, and explicit memory accesses.
        /// </summary>
        private static List<MemAccessDetails> GetMemoryAccesses(
            TraceRow row, DisasmDescriptor descriptor, string mnem, string op1, string op2, int pointerSize)
        {
            var list = new List<MemAccessDetails>(4);

            // 1. Implicit stack operations
            if (mnem is "push" or "pushfq" or "pushf")
            {
                ulong currentRsp = GetRegisterValue(row, "rsp");
                ulong writeAddr = currentRsp - (ulong)pointerSize;
                string? valReg = null;
                if (!string.IsNullOrEmpty(op1) && !op1.Contains('[') && DeObfus._regToFamily.ContainsKey(op1.ToLowerInvariant()))
                {
                    valReg = op1;
                }
                list.Add(new MemAccessDetails(new MemoryRange(writeAddr, pointerSize), IsWrite: true, IsRead: false, ValueReg: valReg));

                // If pushing from memory: e.g. push qword ptr [rax]
                if (op1.Contains('['))
                {
                    ulong? readAddr = EvaluateMemoryOperand(op1, row);
                    if (readAddr.HasValue)
                    {
                        int size = InferSizeFromDisasm(row.Disasm, pointerSize);
                        list.Add(new MemAccessDetails(new MemoryRange(readAddr.Value, size), IsWrite: false, IsRead: true));
                    }
                }
            }
            else if (mnem is "pop" or "popfq" or "popf")
            {
                ulong currentRsp = GetRegisterValue(row, "rsp");
                list.Add(new MemAccessDetails(new MemoryRange(currentRsp, pointerSize), IsWrite: false, IsRead: true));

                // If popping into memory: e.g. pop qword ptr [rax]
                if (op1.Contains('['))
                {
                    ulong? writeAddr = EvaluateMemoryOperand(op1, row);
                    if (writeAddr.HasValue)
                    {
                        int size = InferSizeFromDisasm(row.Disasm, pointerSize);
                        list.Add(new MemAccessDetails(new MemoryRange(writeAddr.Value, size), IsWrite: true, IsRead: false));
                    }
                }
            }
            else if (mnem == "call")
            {
                ulong currentRsp = GetRegisterValue(row, "rsp");
                ulong writeAddr = currentRsp - (ulong)pointerSize;
                list.Add(new MemAccessDetails(new MemoryRange(writeAddr, pointerSize), IsWrite: true, IsRead: false));
            }
            else if (mnem is "ret" or "retn" or "retf")
            {
                ulong currentRsp = GetRegisterValue(row, "rsp");
                list.Add(new MemAccessDetails(new MemoryRange(currentRsp, pointerSize), IsWrite: false, IsRead: true));
            }
            else if (mnem != "lea")
            {
                // 2. Explicit memory operands
                if (op1.Contains('['))
                {
                    ulong? addr = EvaluateMemoryOperand(op1, row);
                    if (addr.HasValue)
                    {
                        int size = InferSizeFromDisasm(row.Disasm, pointerSize);
                        bool isWrite = descriptor.type is DisasmType.Setter or DisasmType.Manipulator;
                        bool isRead = descriptor.type is DisasmType.User or DisasmType.Manipulator;
                        string? valReg = isWrite && !string.IsNullOrEmpty(op2) && !op2.Contains('[') && DeObfus._regToFamily.ContainsKey(op2.ToLowerInvariant()) ? op2 : null;
                        list.Add(new MemAccessDetails(new MemoryRange(addr.Value, size), IsWrite: isWrite, IsRead: isRead, ValueReg: valReg));
                    }
                }

                if (op2.Contains('['))
                {
                    ulong? addr = EvaluateMemoryOperand(op2, row);
                    if (addr.HasValue)
                    {
                        int size = InferSizeFromDisasm(row.Disasm, pointerSize);
                        list.Add(new MemAccessDetails(new MemoryRange(addr.Value, size), IsWrite: false, IsRead: true));
                    }
                }
            }

            // 3. Supplement / cross-reference with row.Mem
            if (row.Mem != null && row.Mem.Count > 0)
            {
                for (int i = 0; i < row.Mem.Count; i++)
                {
                    var mem = row.Mem[i];
                    bool alreadyExists = list.Any(a => a.Range.Start == mem.Address);
                    if (!alreadyExists)
                    {
                        int size = mem.size > 0 ? mem.size : InferSizeFromDisasm(row.Disasm, pointerSize);
                        bool isWrite = mem.IsWrite || descriptor.write_to == "memory" || mnem is "push" or "pushfq" or "call";
                        bool isRead = !isWrite || descriptor.type == DisasmType.Manipulator;
                        string? valReg = isWrite && !string.IsNullOrEmpty(op2) && DeObfus._regToFamily.ContainsKey(op2.ToLowerInvariant()) ? op2 : null;
                        list.Add(new MemAccessDetails(new MemoryRange(mem.Address, size), IsWrite: isWrite, IsRead: isRead, ValueReg: valReg));
                    }
                }
            }

            return list;
        }
    }
}
