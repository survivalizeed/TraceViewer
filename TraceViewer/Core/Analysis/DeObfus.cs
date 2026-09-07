using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace TraceViewer.Core.Analysis
{
    public enum DisasmType
    {
        Setter,
        User,
        Manipulator,
        Other
    }

    public class DisasmDescriptor
    {
        public DisasmType type;
        public string write_to = "";
        public List<string> read_from = [];
        public bool useless = false;

        public DisasmDescriptor(DisasmType type) => this.type = type;
        public DisasmDescriptor() { }
    }

    class DeObfus
    {
        public static HashSet<int> deObHiddenRows = [];

        // Unified register families — single source of truth
        public static readonly FrozenDictionary<string, string[]> registerFamilies = new Dictionary<string, string[]>
        {
            { "raxx", ["rax", "eax", "ax", "ah", "al"] },
            { "rbxx", ["rbx", "ebx", "bx", "bh", "bl"] },
            { "rcxx", ["rcx", "ecx", "cx", "ch", "cl"] },
            { "rdxx", ["rdx", "edx", "dx", "dh", "dl"] },
            { "rspx", ["rsp", "esp", "sp", "spl"] },
            { "rbpx", ["rbp", "ebp", "bp", "bpl"] },
            { "rsix", ["rsi", "esi", "si", "sil"] },
            { "rdix", ["rdi", "edi", "di", "dil"] },
            { "r8x",  ["r8",  "r8d", "r8w", "r8b"] },
            { "r9x",  ["r9",  "r9d", "r9w", "r9b"] },
            { "r10x", ["r10", "r10d", "r10w", "r10b"] },
            { "r11x", ["r11", "r11d", "r11w", "r11b"] },
            { "r12x", ["r12", "r12d", "r12w", "r12b"] },
            { "r13x", ["r13", "r13d", "r13w", "r13b"] },
            { "r14x", ["r14", "r14d", "r14w", "r14b"] },
            { "r15x", ["r15", "r15d", "r15w", "r15b"] },
            { "ripx", ["rip", "eip"] },
            { "xmm0x", ["xmm0"] }, { "xmm1x", ["xmm1"] },
            { "xmm2x", ["xmm2"] }, { "xmm3x", ["xmm3"] },
            { "xmm4x", ["xmm4"] }, { "xmm5x", ["xmm5"] },
            { "xmm6x", ["xmm6"] }, { "xmm7x", ["xmm7"] },
            { "xmm8x", ["xmm8"] }, { "xmm9x", ["xmm9"] },
            { "xmm10x", ["xmm10"] }, { "xmm11x", ["xmm11"] },
            { "xmm12x", ["xmm12"] }, { "xmm13x", ["xmm13"] },
            { "xmm14x", ["xmm14"] }, { "xmm15x", ["xmm15"] },
            { "ymm0x", ["ymm0"] }, { "ymm1x", ["ymm1"] },
            { "ymm2x", ["ymm2"] }, { "ymm3x", ["ymm3"] },
            { "ymm4x", ["ymm4"] }, { "ymm5x", ["ymm5"] },
            { "ymm6x", ["ymm6"] }, { "ymm7x", ["ymm7"] },
            { "ymm8x", ["ymm8"] }, { "ymm9x", ["ymm9"] },
            { "ymm10x", ["ymm10"] }, { "ymm11x", ["ymm11"] },
            { "ymm12x", ["ymm12"] }, { "ymm13x", ["ymm13"] },
            { "ymm14x", ["ymm14"] }, { "ymm15x", ["ymm15"] },
        }.ToFrozenDictionary();

        public static FrozenDictionary<string, string[]> registerFamiliesSSE => registerFamilies;

        internal static readonly FrozenDictionary<string, string> _regToFamily;
        internal static readonly FrozenDictionary<string, int> _regToFamilyIndex;

        // Classification sets
        private static readonly FrozenSet<string> _setters = new HashSet<string>
        {
            "mov", "lea", "pop", "movabs", "movsx", "movsxd", "movzx", "tzcnt", "lzcnt", "popcnt", "movbe"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _users = new HashSet<string>
        {
            "cmp", "test", "jmp", "je", "jz", "jne", "jnz", "jg", "jnle", "jge",
            "jnl", "jl", "jnge", "jle", "jng", "ja", "jnbe", "jae", "jnb", "jb",
            "jnae", "jbe", "jna", "jo", "jno", "js", "jns", "jp", "jpe", "jnp",
            "jpo", "loop", "loope", "loopz", "loopne", "loopnz", "jcxz", "jecxz", "jrcxz"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _conditionalBranches = new HashSet<string>
        {
            "je", "jz", "jne", "jnz", "jg", "jnle", "jge", "jnl", "jl", "jnge",
            "jle", "jng", "ja", "jnbe", "jae", "jnb", "jb", "jnae", "jbe", "jna",
            "jo", "jno", "js", "jns", "jp", "jpe", "jnp", "jpo", "loop", "loope",
            "loopz", "loopne", "loopnz", "jcxz", "jecxz", "jrcxz"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _cfOnlyConsumers = new HashSet<string>
        {
            "jc", "jnc", "jb", "jnb", "jae", "jnae",
            "cmovc", "cmovnc", "cmovb", "cmovnb", "cmovae", "cmovnae",
            "setc", "setnc", "setb", "setnb", "setae", "setnae",
            "adc", "sbb", "rcl", "rcr"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _cfAndZfConsumers = new HashSet<string>
        {
            "jbe", "jna", "ja", "jnbe",
            "cmovbe", "cmovna", "cmova", "cmovnbe",
            "setbe", "setna", "seta", "setnbe"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _otherFlagConsumers = new HashSet<string>
        {
            "jz", "je", "jnz", "jne", "js", "jns", "jo", "jno", "jp", "jpe", "jnp", "jpo",
            "jg", "jnle", "jge", "jnl", "jl", "jnge", "jle", "jng",
            "loop", "loope", "loopz", "loopne", "loopnz", "jcxz", "jecxz", "jrcxz",
            "cmove", "cmovz", "cmovne", "cmovnz", "cmovg", "cmovnle", "cmovge", "cmovnl",
            "cmovl", "cmovnge", "cmovle", "cmovng", "cmovo", "cmovno", "cmovs", "cmovns",
            "cmovp", "cmovpe", "cmovnp", "cmovpo",
            "sete", "setz", "setne", "setnz", "setg", "setnle", "setge", "setnl",
            "setl", "setnge", "setle", "setng", "seto", "setno", "sets", "setns",
            "setp", "setpe", "setnp", "setpo"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _pureFlagSetters = new HashSet<string>
        {
            "cmp", "test", "bt", "clc", "stc", "cmc", "cld", "std"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _manipulators = new HashSet<string>
        {
            "add", "sub", "mul", "div", "inc", "dec", "neg", "not", "and", "or",
            "xor", "shl", "shr", "sar", "rol", "ror", "rcl", "rcr", "imul", "idiv",
            "sal", "bswap", "bsf", "bsr", "btc", "btr", "bts",
            "xadd", "adc", "sbb", "sahf", "shld", "shrd"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _allRegisters;

        private static readonly Regex InstructionRegex = new(@"^(\S+)", RegexOptions.Compiled);
        private static readonly Regex MemoryAddressRegex = new(@"^\[.*?\]", RegexOptions.Compiled);
        private static readonly Regex ImmediateRegex = new(@"^0x[0-9a-fA-F]+\b", RegexOptions.Compiled);
        private static readonly Regex RegisterRegex = new(@"^\b[a-zA-Z0-9]+\b", RegexOptions.Compiled);
        private static readonly Regex DelimiterRegex = new(@"^[,\s]+", RegexOptions.Compiled);
        private static readonly Regex SizePrefixRegex = new(@"\b(qword|dword|word|byte|xmmword|ymmword|zmmword|tbyte|ptr)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        static DeObfus()
        {
            var regToFamily = new Dictionary<string, string>();
            var regToIndex = new Dictionary<string, int>();
            foreach (var family in registerFamilies)
            {
                for (int i = 0; i < family.Value.Length; i++)
                {
                    regToFamily.TryAdd(family.Value[i], family.Key);
                    regToIndex.TryAdd(family.Value[i], i);
                }
            }
            _regToFamily = regToFamily.ToFrozenDictionary();
            _regToFamilyIndex = regToIndex.ToFrozenDictionary();
            _allRegisters = regToFamily.Keys.ToFrozenSet();
        }

        public static void DeObfuscate()
        {
            if (TraceHandler.Trace is null || TraceHandler.Trace.Trace.Count == 0)
                return;

            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            deObHiddenRows.Clear();

            var traceRows = TraceHandler.Trace.Trace;

            if (window.uselessAssignmentsAnalysis)
            {
                var descriptors = new DisasmDescriptor[traceRows.Count];
                for (int i = 0; i < traceRows.Count; i++)
                    descriptors[i] = SliceASM(traceRows[i]);

                int passIteration = 0;
                bool changed;
                do
                {
                    int prevHiddenCount = deObHiddenRows.Count;

                    // Pass 1: Peephole Identity & Self-Cancelling Inverses (add/sub, inc/dec, not/not, xor/xor, xchg/xchg, mov rax, rax)
                    EliminatePeepholeJunk(traceRows, descriptors);

                    // Pass 2: EFLAGS Liveness (Separate CF, Other Flags, and DF tracking)
                    EliminateDeadFlagModifications(traceRows, descriptors);

                    // Pass 3: Forward Dead-Store Elimination (Useless Overwrites before read)
                    EliminateForwardUselessAssignments(traceRows, descriptors);

                    // Pass 4: Transitive Backward Dead-Store Elimination
                    EliminateDeadStores(traceRows, descriptors);

                    // Pass 5: Discarded Stack Operations (Pushes never read, discarded via add rsp / lea rsp or dead pops)
                    EliminateDiscardedStackOperations(traceRows, descriptors);

                    changed = deObHiddenRows.Count > prevHiddenCount;
                    passIteration++;
                } while (changed && passIteration < 5);
            }
        }

        /// <summary>
        /// Pass 1: Detects and eliminates identity instructions and inverse self-cancelling pairs.
        /// </summary>
        private static void EliminatePeepholeJunk(List<TraceRow> traceRows, DisasmDescriptor[] descriptors)
        {
            int count = traceRows.Count;
            for (int i = 0; i < count; i++)
            {
                if (descriptors[i].useless) continue;

                string[] parts = ParseDisassembly(traceRows[i].Disasm);
                if (parts.Length == 0) continue;

                string mnem1 = parts[0].ToLowerInvariant();
                string op1_1 = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
                string op1_2 = parts.Length > 2 ? parts[2].ToLowerInvariant() : "";

                // 1. Identity / NOP-equivalent instructions
                if (IsIdentityInstruction(mnem1, op1_1, op1_2))
                {
                    descriptors[i].useless = true;
                    deObHiddenRows.Add(traceRows[i].Id);
                    continue;
                }

                // 2. Inverse / Self-Cancelling Pairs within a lookahead window
                int maxLookahead = Math.Min(count, i + 8);
                for (int j = i + 1; j < maxLookahead; j++)
                {
                    if (descriptors[j].useless) continue;

                    string[] parts2 = ParseDisassembly(traceRows[j].Disasm);
                    if (parts2.Length == 0) break;

                    string mnem2 = parts2[0].ToLowerInvariant();
                    string op2_1 = parts2.Length > 1 ? parts2[1].ToLowerInvariant() : "";
                    string op2_2 = parts2.Length > 2 ? parts2[2].ToLowerInvariant() : "";

                    // Stop lookahead if a branch or call is crossed
                    if (_conditionalBranches.Contains(mnem2) || mnem2.StartsWith("call") || mnem2.StartsWith("ret"))
                        break;

                    if (AreInversePair(mnem1, op1_1, op1_2, mnem2, op2_1, op2_2))
                    {
                        string targetReg = op1_1;
                        bool intermediateConflict = false;
                        for (int k = i + 1; k < j; k++)
                        {
                            if (descriptors[k].useless) continue;

                            // For push/pop pairs, intermediate stack operations invalidate the cancellation
                            if (mnem1 is "push" or "pushfq" or "pushf")
                            {
                                string kDisasm = traceRows[k].Disasm.ToLowerInvariant();
                                if (kDisasm.StartsWith("call") || kDisasm.StartsWith("ret") ||
                                    kDisasm.StartsWith("push") || kDisasm.StartsWith("pop") ||
                                    descriptors[k].write_to == "memory" ||
                                    descriptors[k].write_to == "rsp" ||
                                    descriptors[k].read_from.Any(r => AreRelatedRegisters(r, "rsp")))
                                {
                                    intermediateConflict = true;
                                    break;
                                }
                            }

                            // For cmc flag pairs, intermediate flag consumers invalidate the cancellation
                            if (mnem1 == "cmc")
                            {
                                string kDisasm = traceRows[k].Disasm.ToLowerInvariant();
                                var kParts = ParseDisassembly(kDisasm);
                                string kMnem = kParts.Length > 0 ? kParts[0].ToLowerInvariant() : "";
                                if (_cfOnlyConsumers.Contains(kMnem) || _cfAndZfConsumers.Contains(kMnem) || kMnem is "pushf" or "pushfq")
                                {
                                    intermediateConflict = true;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(targetReg) &&
                                (descriptors[k].read_from.Any(r => AreRelatedRegisters(r, targetReg)) ||
                                 AreRelatedRegisters(descriptors[k].write_to, targetReg)))
                            {
                                intermediateConflict = true;
                                break;
                            }

                            if (mnem1 == "xchg" && !string.IsNullOrEmpty(op1_2))
                            {
                                if (descriptors[k].read_from.Any(r => AreRelatedRegisters(r, op1_2)) ||
                                    AreRelatedRegisters(descriptors[k].write_to, op1_2))
                                {
                                    intermediateConflict = true;
                                    break;
                                }
                            }
                        }

                        if (!intermediateConflict)
                        {
                            descriptors[i].useless = true;
                            descriptors[j].useless = true;
                            deObHiddenRows.Add(traceRows[i].Id);
                            deObHiddenRows.Add(traceRows[j].Id);
                            break;
                        }
                    }

                    // If an intermediate instruction touched op1_1, stop searching for inverse of op1_1
                    if (!string.IsNullOrEmpty(op1_1) &&
                        (descriptors[j].read_from.Any(r => AreRelatedRegisters(r, op1_1)) ||
                         AreRelatedRegisters(descriptors[j].write_to, op1_1)))
                    {
                        break;
                    }
                }
            }
        }

        private static bool IsIdentityInstruction(string mnemonic, string op1, string op2)
        {
            if (mnemonic is "nop" or "fnop")
                return true;

            if (mnemonic is "xchg" && op1 == op2 && !string.IsNullOrEmpty(op1))
                return true;

            // 64-bit identity mov (e.g. mov rax, rax) — only 64-bit preserves all bits without clearing upper 32
            if (mnemonic is "mov" && op1 == op2 && !string.IsNullOrEmpty(op1) && Is64BitRegister(op1))
                return true;

            string normalizedOp2 = op2.Replace(" ", "");
            if (mnemonic is "lea" && (normalizedOp2 == $"[{op1}]" || normalizedOp2 == $"[{op1}+0]" ||
                                      normalizedOp2 == $"[{op1}-0]" || normalizedOp2 == $"[{op1}+0x0]"))
                return true;

            // Neutral arithmetic with 0
            if (mnemonic is "add" or "sub" or "xor" or "or" or "shl" or "shr" or "sar" or "rol" or "ror")
            {
                if (op2 is "0" or "0x0" or "0x00" or "0h")
                    return true;
            }

            // Multiplication by 1
            if (mnemonic is "imul" && (op2 is "1" or "0x1" or "0x01" or "1h"))
                return true;

            // AND with all 1s
            if (mnemonic is "and" && (op2 is "-1" or "0xffffffff" or "0xffffffffffffffff" or "0ffffffffh"))
                return true;

            return false;
        }

        private static bool AreInversePair(string mnem1, string op1_1, string op1_2,
                                           string mnem2, string op2_1, string op2_2)
        {
            // Self-cancelling flag operations
            if (mnem1 == "cmc" && mnem2 == "cmc")
                return true;

            if (string.IsNullOrEmpty(op1_1) || string.IsNullOrEmpty(op2_1))
                return false;

            // 0. xchg reg1, reg2 / xchg reg1, reg2 (or xchg reg2, reg1)
            if (mnem1 == "xchg" && mnem2 == "xchg")
            {
                return (op1_1 == op2_1 && op1_2 == op2_2) || (op1_1 == op2_2 && op1_2 == op2_1);
            }

            if (op1_1 != op2_1)
                return false;

            // 1. inc / dec
            if ((mnem1 == "inc" && mnem2 == "dec") || (mnem1 == "dec" && mnem2 == "inc"))
                return true;

            // 2. not / not
            if (mnem1 == "not" && mnem2 == "not")
                return true;

            // 3. neg / neg
            if (mnem1 == "neg" && mnem2 == "neg")
                return true;

            // 4. bswap / bswap
            if (mnem1 == "bswap" && mnem2 == "bswap")
                return true;

            // 5. add reg, X / sub reg, X with same operand
            if ((mnem1 == "add" && mnem2 == "sub") || (mnem1 == "sub" && mnem2 == "add"))
            {
                if (!string.IsNullOrEmpty(op1_2) && op1_2 == op2_2)
                    return true;
            }

            // 6. xor reg, imm / xor reg, imm with same immediate
            if (mnem1 == "xor" && mnem2 == "xor")
            {
                if (!string.IsNullOrEmpty(op1_2) && op1_2 == op2_2)
                    return true;
            }

            // 7. rol reg, X / ror reg, X with same operand
            if ((mnem1 == "rol" && mnem2 == "ror") || (mnem1 == "ror" && mnem2 == "rol"))
            {
                if (!string.IsNullOrEmpty(op1_2) && op1_2 == op2_2)
                    return true;
            }

            // 8. push reg / pop reg or pushfq / popfq
            if ((mnem1 == "push" && mnem2 == "pop") ||
                (mnem1 == "pushfq" && mnem2 == "popfq") ||
                (mnem1 == "pushf" && mnem2 == "popf"))
                return true;

            return false;
        }

        /// <summary>
        /// Pass 2: EFLAGS Liveness Analysis.
        /// Identifies pure flag operations (cmc, clc, stc, cld, std, bt, cmp, test) whose computed flags are never read.
        /// Distinguishes between Carry Flag (CF), other arithmetic flags (ZF, SF, OF, PF), and Direction Flag (DF).
        /// </summary>
        private static void EliminateDeadFlagModifications(List<TraceRow> traceRows, DisasmDescriptor[] descriptors)
        {
            bool cfLive = false;
            bool otherFlagsLive = false;
            bool dfLive = false;

            for (int i = traceRows.Count - 1; i >= 0; i--)
            {
                if (descriptors[i].useless) continue;

                string[] parts = ParseDisassembly(traceRows[i].Disasm);
                if (parts.Length == 0) continue;

                string mnem = parts[0].ToLowerInvariant();

                // 1. Full flag stack push
                if (mnem is "pushf" or "pushfq")
                {
                    cfLive = true;
                    otherFlagsLive = true;
                    dfLive = true;
                    continue;
                }

                // 2. Instructions consuming Carry Flag only
                if (_cfOnlyConsumers.Contains(mnem))
                {
                    cfLive = true;
                    continue;
                }

                // 3. Instructions consuming both Carry Flag and Zero Flag
                if (_cfAndZfConsumers.Contains(mnem))
                {
                    cfLive = true;
                    otherFlagsLive = true;
                    continue;
                }

                // 4. Instructions consuming other arithmetic flags (ZF, SF, OF, PF)
                if (_otherFlagConsumers.Contains(mnem))
                {
                    otherFlagsLive = true;
                    continue;
                }

                // 5. String instructions consume Direction Flag
                if (mnem.StartsWith("movs") || mnem.StartsWith("stos") || mnem.StartsWith("lods") ||
                    mnem.StartsWith("scas") || mnem.StartsWith("cmps"))
                {
                    dfLive = true;
                }

                // 6. Flag producers / modifiers
                if (mnem == "cmc")
                {
                    if (!cfLive)
                    {
                        // Carry flag is never read before being clobbered or trace end!
                        descriptors[i].useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                    }
                    // cmc reads CF and inverts CF, so if cfLive was true, it remains true before cmc
                    continue;
                }

                if (mnem is "clc" or "stc")
                {
                    if (!cfLive)
                    {
                        descriptors[i].useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                    }
                    else
                    {
                        cfLive = false; // Satisfied by clc/stc
                    }
                    continue;
                }

                if (mnem is "cld" or "std")
                {
                    if (!dfLive)
                    {
                        descriptors[i].useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                    }
                    else
                    {
                        dfLive = false; // Satisfied by cld/std
                    }
                    continue;
                }

                if (mnem is "cmp" or "test" or "bt")
                {
                    if (!cfLive && !otherFlagsLive)
                    {
                        // Neither CF nor other flags are read!
                        descriptors[i].useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                    }
                    else
                    {
                        cfLive = false;
                        otherFlagsLive = false;
                    }
                    continue;
                }

                if (mnem is "sahf")
                {
                    // sahf sets CF, ZF, SF, AF, PF from AH
                    cfLive = false;
                    otherFlagsLive = false;
                    continue;
                }

                if (mnem is "popf" or "popfq")
                {
                    cfLive = false;
                    otherFlagsLive = false;
                    dfLive = false;
                    continue;
                }

                // 7. Inc and Dec modify ZF, SF, OF, AF, PF, but preserve CF
                if (mnem is "inc" or "dec")
                {
                    otherFlagsLive = false;
                    continue;
                }

                // 8. Arithmetic/logic manipulators overwrite all standard flags
                if (_manipulators.Contains(mnem) && mnem is not "not")
                {
                    cfLive = false;
                    otherFlagsLive = false;
                }
            }
        }

        /// <summary>
        /// Pass 3: Forward Dead-Store Elimination.
        /// Identifies any assignment that is overwritten by a subsequent Setter without any intervening read.
        /// </summary>
        private static void EliminateForwardUselessAssignments(List<TraceRow> traceRows, DisasmDescriptor[] descriptors)
        {
            bool foundSomethingUseless;
            do
            {
                foundSomethingUseless = false;
                for (int i = 0; i < traceRows.Count; i++)
                {
                    var currentDescriptor = descriptors[i];

                    if (currentDescriptor.useless || string.IsNullOrEmpty(currentDescriptor.write_to) || currentDescriptor.write_to == "memory")
                        continue;

                    if (currentDescriptor.type is not (DisasmType.Setter or DisasmType.Manipulator))
                        continue;

                    // Skip direct modifications of RSP or RIP
                    if (_regToFamily.TryGetValue(currentDescriptor.write_to, out var fam) &&
                        (fam == "rspx" || fam == "ripx"))
                        continue;

                    string writtenRegister = currentDescriptor.write_to;
                    bool isUseless = false;

                    for (int j = i + 1; j < traceRows.Count; j++)
                    {
                        var nextDescriptor = descriptors[j];
                        if (nextDescriptor.type == DisasmType.Other)
                            continue;

                        // Check if any read register uses the written register (instruction is useful)
                        bool isUsed = false;
                        if (!nextDescriptor.useless)
                        {
                            foreach (var readReg in nextDescriptor.read_from)
                            {
                                if (AreRelatedRegisters(writtenRegister, readReg))
                                {
                                    isUsed = true;
                                    break;
                                }
                            }
                        }

                        if (isUsed)
                            break; // Register is used — not useless

                        // If overwritten by a Setter before being read, it is useless!
                        if (nextDescriptor.type == DisasmType.Setter &&
                            OverwritesRegister(writtenRegister, nextDescriptor.write_to))
                        {
                            isUseless = true;
                            break;
                        }
                    }

                    if (isUseless)
                    {
                        currentDescriptor.useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                        foundSomethingUseless = true;
                    }
                }
            } while (foundSomethingUseless);
        }

        /// <summary>
        /// Pass 4: Transitive Backward Dead-Store Elimination.
        /// Performs fast O(N) backward liveness tracking, eliminating calculations whose results are never read.
        /// </summary>
        private static void EliminateDeadStores(List<TraceRow> traceRows, DisasmDescriptor[] descriptors)
        {
            var liveFamilies = new HashSet<string>();

            // Initially assume general-purpose registers are live at trace boundaries
            foreach (var fam in registerFamilies.Keys)
                liveFamilies.Add(fam);

            bool changed;
            int passCount = 0;
            const int maxPasses = 3;

            do
            {
                changed = false;
                passCount++;

                for (int i = traceRows.Count - 1; i >= 0; i--)
                {
                    if (descriptors[i].useless) continue;

                    var desc = descriptors[i];
                    string disasm = traceRows[i].Disasm.ToLowerInvariant();

                    // Calls, syscalls, returns have external side effects
                    if (disasm.StartsWith("call") || disasm.StartsWith("syscall") ||
                        disasm.StartsWith("sysenter") || disasm.StartsWith("int") ||
                        disasm.StartsWith("ret"))
                    {
                        foreach (var readReg in desc.read_from)
                        {
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        }
                        continue;
                    }

                    // Memory writes: keep instruction alive
                    if (desc.write_to == "memory")
                    {
                        foreach (var readReg in desc.read_from)
                        {
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        }
                        continue;
                    }

                    // Pure users (like cmp, test, or conditional branches): mark read registers as live
                    if (desc.type == DisasmType.User || string.IsNullOrEmpty(desc.write_to))
                    {
                        foreach (var readReg in desc.read_from)
                        {
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        }
                        continue;
                    }

                    // Instruction writes to a register
                    string writtenReg = desc.write_to;
                    if (!_regToFamily.TryGetValue(writtenReg, out var writtenFamily))
                    {
                        foreach (var readReg in desc.read_from)
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        continue;
                    }

                    // NEVER hide writes directly to RSP or RIP (stack frame & control flow management)
                    if (writtenFamily == "rspx" || writtenFamily == "ripx")
                    {
                        liveFamilies.Add(writtenFamily);
                        foreach (var readReg in desc.read_from)
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        continue;
                    }

                    // Is the written register family live?
                    if (!liveFamilies.Contains(writtenFamily))
                    {
                        // The computed value is NEVER read!
                        desc.useless = true;
                        deObHiddenRows.Add(traceRows[i].Id);
                        changed = true;
                        // Inputs are NOT marked live, transitively cascading dead-code elimination!
                    }
                    else
                    {
                        // Value is needed!
                        // In x86-64, writing to 64-bit (idx 0) or 32-bit (idx 1) completely overwrites the entire family.
                        // Writing to 16-bit (idx 2) or 8-bit (idx >= 3) preserves the remaining bits, so the family is NOT killed.
                        bool isFullOverwrite = desc.type == DisasmType.Setter &&
                                               _regToFamilyIndex.TryGetValue(writtenReg, out int idx) && idx <= 1;

                        if (isFullOverwrite)
                        {
                            liveFamilies.Remove(writtenFamily);
                        }

                        // Mark inputs as live
                        foreach (var readReg in desc.read_from)
                        {
                            if (_regToFamily.TryGetValue(readReg, out var fam))
                                liveFamilies.Add(fam);
                        }
                    }
                }
            } while (changed && passCount < maxPasses);
        }

        /// <summary>
        /// Pass 5: Discarded Stack Operations.
        /// Identifies pushes whose values are never read and whose stack allocations are simply discarded
        /// via add rsp / lea rsp or pops into dead registers.
        /// </summary>
        private static void EliminateDiscardedStackOperations(List<TraceRow> traceRows, DisasmDescriptor[] descriptors)
        {
            int count = traceRows.Count;
            for (int i = 0; i < count; i++)
            {
                if (descriptors[i].useless) continue;

                string disasm1 = traceRows[i].Disasm.ToLowerInvariant();
                string[] parts1 = ParseDisassembly(disasm1);
                if (parts1.Length == 0) continue;

                string mnem1 = parts1[0].ToLowerInvariant();
                if (mnem1 is not ("push" or "pushfq" or "pushf"))
                    continue;

                int stackDiff = -8;
                var pushedIndices = new List<int> { i };
                var cleanupIndices = new List<int>();
                bool conflict = false;

                int maxLookahead = Math.Min(count, i + 32);
                for (int j = i + 1; j < maxLookahead; j++)
                {
                    if (descriptors[j].useless)
                    {
                        // If an intermediate pop was already marked useless, it accounted for +8 stack cleanup!
                        string uDisasm = traceRows[j].Disasm.ToLowerInvariant();
                        if (uDisasm.StartsWith("pop ") || uDisasm == "popfq" || uDisasm == "popf")
                        {
                            stackDiff += 8;
                            cleanupIndices.Add(j);
                            if (stackDiff == 0) break;
                        }
                        continue;
                    }

                    string disasm2 = traceRows[j].Disasm.ToLowerInvariant();
                    if (disasm2.StartsWith("call") || disasm2.StartsWith("ret") ||
                        disasm2.StartsWith("syscall") || disasm2.StartsWith("sysenter") ||
                        disasm2.StartsWith("int ") || _conditionalBranches.Any(b => disasm2.StartsWith(b)))
                    {
                        conflict = true;
                        break;
                    }

                    string[] parts2 = ParseDisassembly(disasm2);
                    if (parts2.Length == 0) { conflict = true; break; }
                    string mnem2 = parts2[0].ToLowerInvariant();
                    string op2_1 = parts2.Length > 1 ? parts2[1].ToLowerInvariant() : "";
                    string op2_2 = parts2.Length > 2 ? parts2[2].ToLowerInvariant() : "";

                    // Another push: adds to depth
                    if (mnem2 is "push" or "pushfq" or "pushf")
                    {
                        stackDiff -= 8;
                        pushedIndices.Add(j);
                        continue;
                    }

                    // Stack cleanup via add rsp, imm
                    if (mnem2 == "add" && op2_1 == "rsp" && TryParseOffset(op2_2, out int addOffset))
                    {
                        stackDiff += addOffset;
                        cleanupIndices.Add(j);
                        if (stackDiff == 0) break;
                        if (stackDiff > 0) { conflict = true; break; }
                        continue;
                    }

                    // Stack cleanup via lea rsp, [rsp + imm]
                    if (mnem2 == "lea" && op2_1 == "rsp" && TryParseLeaRspOffset(op2_2, out int leaOffset))
                    {
                        stackDiff += leaOffset;
                        cleanupIndices.Add(j);
                        if (stackDiff == 0) break;
                        if (stackDiff > 0) { conflict = true; break; }
                        continue;
                    }

                    // Any intermediate instruction referencing rsp (memory read or arithmetic) invalidates discard
                    if (descriptors[j].read_from.Any(r => AreRelatedRegisters(r, "rsp")) ||
                        descriptors[j].write_to == "rsp" ||
                        disasm2.Contains("[rsp") || disasm2.Contains("[esp"))
                    {
                        conflict = true;
                        break;
                    }
                }

                if (!conflict && stackDiff == 0)
                {
                    bool flagsNeeded = false;
                    foreach (var cIdx in cleanupIndices)
                    {
                        if (descriptors[cIdx].useless) continue;
                        string cDisasm = traceRows[cIdx].Disasm.ToLowerInvariant();
                        if (cDisasm.StartsWith("add "))
                        {
                            if (AreFlagsConsumedBeforeOverwrite(traceRows, descriptors, cIdx))
                            {
                                flagsNeeded = true;
                                break;
                            }
                        }
                    }

                    if (!flagsNeeded)
                    {
                        foreach (var pIdx in pushedIndices)
                        {
                            descriptors[pIdx].useless = true;
                            deObHiddenRows.Add(traceRows[pIdx].Id);
                        }
                        foreach (var cIdx in cleanupIndices)
                        {
                            descriptors[cIdx].useless = true;
                            deObHiddenRows.Add(traceRows[cIdx].Id);
                        }
                    }
                }
            }
        }

        private static bool TryParseOffset(string op, out int offset)
        {
            offset = 0;
            if (string.IsNullOrEmpty(op)) return false;
            op = op.Trim();
            if (op.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(op[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
            return int.TryParse(op, out offset);
        }

        private static bool TryParseLeaRspOffset(string op2, out int offset)
        {
            offset = 0;
            if (string.IsNullOrEmpty(op2)) return false;
            string norm = op2.Replace(" ", "").ToLowerInvariant();
            if (norm.StartsWith("[rsp+") && norm.EndsWith("]"))
            {
                string numPart = norm.Substring(5, norm.Length - 6);
                return TryParseOffset(numPart, out offset);
            }
            return false;
        }

        private static bool AreFlagsConsumedBeforeOverwrite(List<TraceRow> traceRows, DisasmDescriptor[] descriptors, int cIdx)
        {
            int maxLookahead = Math.Min(traceRows.Count, cIdx + 16);
            for (int k = cIdx + 1; k < maxLookahead; k++)
            {
                if (descriptors[k].useless) continue;
                string d = traceRows[k].Disasm.ToLowerInvariant();
                string[] p = ParseDisassembly(d);
                if (p.Length == 0) continue;
                string m = p[0].ToLowerInvariant();

                if (_conditionalBranches.Contains(m) || _cfOnlyConsumers.Contains(m) ||
                    _cfAndZfConsumers.Contains(m) || _otherFlagConsumers.Contains(m) ||
                    m is "pushf" or "pushfq")
                    return true;

                if (d.StartsWith("call") || d.StartsWith("ret") || d.StartsWith("syscall") || d.StartsWith("int"))
                    return true;

                // Arithmetic instruction that overwrites flags
                if (_manipulators.Contains(m) && m is not "not" or "inc" or "dec")
                    return false;
                if (m is "cmp" or "test" or "sahf" or "popf" or "popfq")
                    return false;
            }
            return false;
        }

        private static bool IsZeroingIdiom(string mnemonic, string op1, string op2)
        {
            if (string.IsNullOrEmpty(op1) || string.IsNullOrEmpty(op2))
                return false;
            if (op1 != op2)
                return false;

            return mnemonic is "xor" or "sub" or "pxor" or "xorps" or "xorpd" or "vpxor" or "vxorps" or "vxorpd";
        }

        private static bool Is64BitRegister(string reg)
        {
            return _regToFamilyIndex.TryGetValue(reg, out int idx) && idx == 0;
        }

        private static bool ContainsAnyRegister(string disasm, string familyKey)
        {
            if (!registerFamilies.TryGetValue(familyKey, out var regs))
                return false;
            foreach (var reg in regs)
                if (disasm.Contains(reg))
                    return true;
            return false;
        }

        private static bool AreRelatedRegisters(string reg1, string reg2)
        {
            if (reg1 == reg2) return true;
            if (!_regToFamily.TryGetValue(reg1, out var family1)) return false;
            if (!_regToFamily.TryGetValue(reg2, out var family2)) return false;
            return family1 == family2;
        }

        private static bool OverwritesRegister(string writtenReg, string setterReg)
        {
            if (!_regToFamily.TryGetValue(writtenReg, out var fam1) ||
                !_regToFamily.TryGetValue(setterReg, out var fam2) ||
                fam1 != fam2)
                return false;

            if (!_regToFamilyIndex.TryGetValue(writtenReg, out int writtenIdx) ||
                !_regToFamilyIndex.TryGetValue(setterReg, out int setterIdx))
                return writtenReg == setterReg;

            // In x86-64, writes to 64-bit (idx 0) or 32-bit (idx 1, zero-extends to 64-bit) overwrite the entire family!
            if (setterIdx <= 1)
                return true;

            // Otherwise, setterIdx must be <= writtenIdx (e.g. 16-bit idx 2 overwrites 16-bit idx 2 or 8-bit idx 3/4)
            return setterIdx <= writtenIdx;
        }

        public static string[] ParseDisassembly(string rawDisassembly)
        {
            string stripped = SizePrefixRegex.Replace(rawDisassembly, "");
            var parts = new List<string>();
            string remaining = stripped.Trim();

            Match instructionMatch = InstructionRegex.Match(remaining);
            if (instructionMatch.Success)
            {
                parts.Add(instructionMatch.Groups[1].Value);
                remaining = remaining[instructionMatch.Length..].Trim();
            }
            else if (!string.IsNullOrEmpty(remaining))
            {
                parts.Add(remaining);
                return [.. parts];
            }
            else
                return [.. parts];

            while (!string.IsNullOrEmpty(remaining))
            {
                Match memMatch = MemoryAddressRegex.Match(remaining);
                if (memMatch.Success) { parts.Add(memMatch.Value); remaining = remaining[memMatch.Length..].Trim(); continue; }

                Match immMatch = ImmediateRegex.Match(remaining);
                if (immMatch.Success) { parts.Add(immMatch.Value); remaining = remaining[immMatch.Length..].Trim(); continue; }

                Match regMatch = RegisterRegex.Match(remaining);
                if (regMatch.Success) { parts.Add(regMatch.Value); remaining = remaining[regMatch.Length..].Trim(); continue; }

                Match delimMatch = DelimiterRegex.Match(remaining);
                if (delimMatch.Success) { remaining = remaining[delimMatch.Length..].Trim(); continue; }

                remaining = remaining[1..].Trim();
            }
            return [.. parts];
        }

        public static DisasmType ClassifyInstruction(string instruction)
        {
            if (_setters.Contains(instruction)) return DisasmType.Setter;
            if (_users.Contains(instruction)) return DisasmType.User;
            if (_manipulators.Contains(instruction)) return DisasmType.Manipulator;
            return DisasmType.Other;
        }

        internal static DisasmDescriptor SliceASM(TraceRow traceRow)
        {
            string[] disasmParts = ParseDisassembly(traceRow.Disasm);
            if (disasmParts.Length == 0)
                return new DisasmDescriptor(DisasmType.Other);

            string mnemonic = disasmParts[0].ToLowerInvariant();
            string op1 = disasmParts.Length > 1 ? disasmParts[1].ToLowerInvariant() : "";
            string op2 = disasmParts.Length > 2 ? disasmParts[2].ToLowerInvariant() : "";
            string op3 = disasmParts.Length > 3 ? disasmParts[3].ToLowerInvariant() : "";

            var descriptor = new DisasmDescriptor { type = ClassifyInstruction(mnemonic) };

            // 1. Check for Zeroing Idiom: xor reg, reg / sub reg, reg / pxor reg, reg
            if (IsZeroingIdiom(mnemonic, op1, op2))
            {
                descriptor.type = DisasmType.Setter;
                descriptor.write_to = op1;
                descriptor.read_from = []; // Zeroing has no input dependencies!
                return descriptor;
            }

            // 2. Pure flag setters (cmp, test)
            if (_pureFlagSetters.Contains(mnemonic))
            {
                descriptor.type = DisasmType.User;
                descriptor.write_to = "";
                descriptor.read_from.AddRange(SplitReader(op1));
                if (!string.IsNullOrEmpty(op2))
                    descriptor.read_from.AddRange(SplitReader(op2));
                return descriptor;
            }

            // 3. Conditional branches and flag consumers
            if (_conditionalBranches.Contains(mnemonic))
            {
                descriptor.type = DisasmType.User;
                descriptor.write_to = "";
                if (!string.IsNullOrEmpty(op1))
                    descriptor.read_from.AddRange(SplitReader(op1));
                return descriptor;
            }

            // 4. Setters (mov, lea, pop, movzx, movsx, movabs)
            if (_setters.Contains(mnemonic))
            {
                descriptor.type = DisasmType.Setter;
                descriptor.write_to = op1.Contains('[') ? "memory" : op1;
                if (mnemonic == "pop")
                {
                    descriptor.read_from.Add("memory");
                }
                else if (mnemonic == "lea")
                {
                    descriptor.read_from.AddRange(SplitReader(op2));
                }
                else
                {
                    if (op1.Contains('['))
                        descriptor.read_from.AddRange(SplitReader(op1));
                    descriptor.read_from.AddRange(SplitReader(op2));
                }
                return descriptor;
            }

            // 5. Manipulators (add, sub, inc, dec, not, neg, and, or, xor, shl, etc.)
            if (_manipulators.Contains(mnemonic))
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = op1.Contains('[') ? "memory" : op1;

                // A manipulator ALWAYS reads its destination register!
                descriptor.read_from.AddRange(SplitReader(op1));

                if (!string.IsNullOrEmpty(op2))
                    descriptor.read_from.AddRange(SplitReader(op2));
                if (!string.IsNullOrEmpty(op3))
                    descriptor.read_from.AddRange(SplitReader(op3));
                return descriptor;
            }

            // 6. Stack push
            if (mnemonic is "push" or "pushfq" or "pushf")
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = "memory";
                if (!string.IsNullOrEmpty(op1))
                    descriptor.read_from.AddRange(SplitReader(op1));
                return descriptor;
            }

            if (mnemonic is "popfq" or "popf")
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = "";
                descriptor.read_from.Add("memory");
                return descriptor;
            }

            // 7. Exchange
            if (mnemonic == "xchg")
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = op1.Contains('[') ? "memory" : op1;
                descriptor.read_from.AddRange(SplitReader(op1));
                descriptor.read_from.AddRange(SplitReader(op2));
                return descriptor;
            }

            // 8. Conditional moves (cmovcc)
            if (mnemonic.StartsWith("cmov") && mnemonic.Length <= 8)
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = op1.Contains('[') ? "memory" : op1;
                descriptor.read_from.AddRange(SplitReader(op1));
                descriptor.read_from.AddRange(SplitReader(op2));
                return descriptor;
            }

            AdditionalInstructions(mnemonic, op1, op2, descriptor);
            return descriptor;
        }

        private static void AdditionalInstructions(string instruction, string op1, string op2, DisasmDescriptor descriptor)
        {
            if (descriptor.type != DisasmType.Other) return;

            // SetCC instructions (sete, setne, setz, seta, setb, etc.)
            if (instruction.StartsWith("set") && instruction.Length <= 6 && instruction != "set")
            {
                descriptor.type = DisasmType.Setter;
                descriptor.write_to = op1.Contains('[') ? "memory" : op1;
                if (op1.Contains('[')) descriptor.read_from.AddRange(SplitReader(op1));
                return;
            }

            // Sign extension / conversion instructions
            switch (instruction)
            {
                case "cdqe" or "cltq" or "clq":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "rax";
                    descriptor.read_from.Add("eax");
                    return;
                case "cwde" or "cwtl":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "eax";
                    descriptor.read_from.Add("ax");
                    return;
                case "cbw" or "cbtw":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "ax";
                    descriptor.read_from.Add("al");
                    return;
                case "cqo" or "cqto":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "rdx";
                    descriptor.read_from.Add("rax");
                    return;
                case "cdq" or "cltd":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "edx";
                    descriptor.read_from.Add("eax");
                    return;
                case "cwd" or "cwtd":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "dx";
                    descriptor.read_from.Add("ax");
                    return;
                case "lahf":
                    descriptor.type = DisasmType.Setter;
                    descriptor.write_to = "ah";
                    return;
                case "div" or "idiv":
                    descriptor.type = DisasmType.Manipulator;
                    descriptor.write_to = "rax";
                    descriptor.read_from.AddRange(SplitReader(op1));
                    descriptor.read_from.Add("rax");
                    descriptor.read_from.Add("rdx");
                    return;
            }
        }

        private static List<string> SplitReader(string readFrom)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(readFrom))
                return result;

            string[] parts = readFrom.Split(['[', ']', ' ', '+', '*', '-', ',', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string p = part.ToLowerInvariant();
                if (_allRegisters.Contains(p))
                    result.Add(p);
            }
            return result;
        }
    }
}