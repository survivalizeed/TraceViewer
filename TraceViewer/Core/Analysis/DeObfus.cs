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

        // Unified register families — single source of truth (previously duplicated as registerFamilies + registerFamiliesSSE)
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

        // Alias for backward compatibility — the old registerFamiliesSSE is now the same as registerFamilies
        public static FrozenDictionary<string, string[]> registerFamiliesSSE => registerFamilies;

        // Pre-computed reverse lookup: register name → family key (O(1) instead of O(n*m))
        private static readonly FrozenDictionary<string, string> _regToFamily;

        // Pre-computed: register name → index within its family
        private static readonly FrozenDictionary<string, int> _regToFamilyIndex;

        // Instruction classification sets — FrozenSet for O(1) lookup
        private static readonly FrozenSet<string> _setters = new HashSet<string>
        {
            "mov", "lea", "pop", "movabs", "movsx", "movsxd", "movzx"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _users = new HashSet<string>
        {
            "cmp", "test", "jmp", "je", "jz", "jne", "jnz", "jg", "jnle", "jge",
            "jnl", "jl", "jnge", "jle", "jng", "ja", "jnbe", "jae", "jnb", "jb",
            "jnae", "jbe", "jna", "jo", "jno", "js", "jns", "jp", "jpe", "jnp",
            "jpo", "loop", "loope", "loopz", "loopne", "loopnz", "jcxz", "jecxz"
        }.ToFrozenSet();

        private static readonly FrozenSet<string> _manipulators = new HashSet<string>
        {
            "add", "sub", "mul", "div", "inc", "dec", "neg", "not", "and", "or",
            "xor", "shl", "shr", "sar", "rol", "ror", "rcl", "rcr", "imul", "idiv",
            "sal", "bswap", "bsf", "bsr", "bt", "btc", "btr", "bts", "set",
            "xadd", "adc", "sbb", "lahf", "sahf", "setne", "setl", "setae"
        }.ToFrozenSet();

        // Pre-computed set of all known registers for fast SplitReader lookup
        private static readonly FrozenSet<string> _allRegisters;

        // Pre-compiled regex patterns (compiled once instead of per-call)
        private static readonly Regex InstructionRegex = new(@"^(\S+)", RegexOptions.Compiled);
        private static readonly Regex MemoryAddressRegex = new(@"^\[.*?\]", RegexOptions.Compiled);
        private static readonly Regex ImmediateRegex = new(@"^0x[0-9a-fA-F]+\b", RegexOptions.Compiled);
        private static readonly Regex RegisterRegex = new(@"^\b[a-zA-Z0-9]+\b", RegexOptions.Compiled);
        private static readonly Regex DelimiterRegex = new(@"^[,\s]+", RegexOptions.Compiled);

        static DeObfus()
        {
            // Build reverse lookup
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
            if (TraceHandler.Trace is null)
                return;
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            var TraceRows = TraceHandler.Trace.Trace;

            if (window.uselessAssignmentsAnalysis)
                HideUselessAssignments(TraceRows);
        }

        private static void HideUselessAssignments(List<TraceRow> TraceRows)
        {
            var descriptors = new DisasmDescriptor[TraceRows.Count];
            for (int i = 0; i < TraceRows.Count; i++)
                descriptors[i] = SliceASM(TraceRows[i]);

            bool foundSomethingUseless;
            do
            {
                foundSomethingUseless = false;
                for (int i = 0; i < descriptors.Length; i++)
                {
                    var currentDescriptor = descriptors[i];

                    if (string.IsNullOrEmpty(currentDescriptor.write_to) || currentDescriptor.useless)
                        continue;

                    if (currentDescriptor.type is not (DisasmType.Setter or DisasmType.Manipulator))
                        continue;

                    // Skip rsp/rip and memory writes
                    if (currentDescriptor.write_to == "memory")
                        continue;

                    string disasm = TraceRows[i].Disasm;
                    if (ContainsAnyRegister(disasm, "rspx") || ContainsAnyRegister(disasm, "ripx"))
                        continue;

                    string writtenRegister = currentDescriptor.write_to;
                    bool isUseless = false;

                    for (int j = i + 1; j < descriptors.Length; j++)
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

                        if (nextDescriptor.type == DisasmType.Setter &&
                            AreRelatedRegisters(writtenRegister, nextDescriptor.write_to))
                        {
                            isUseless = true;
                            break;
                        }
                    }

                    if (isUseless)
                    {
                        foundSomethingUseless = true;
                        currentDescriptor.useless = true;
                        deObHiddenRows.Add(i);
                    }
                }
            } while (foundSomethingUseless);
        }

        /// <summary>
        /// Checks if disassembly contains any register from the given family.
        /// </summary>
        private static bool ContainsAnyRegister(string disasm, string familyKey)
        {
            if (!registerFamilies.TryGetValue(familyKey, out var regs))
                return false;
            foreach (var reg in regs)
                if (disasm.Contains(reg))
                    return true;
            return false;
        }

        /// <summary>
        /// Checks if two registers are related (same family — either sub-register or same register).
        /// Uses pre-computed O(1) reverse lookup instead of iterating all families.
        /// </summary>
        private static bool AreRelatedRegisters(string reg1, string reg2)
        {
            if (reg1 == reg2) return true;
            if (!_regToFamily.TryGetValue(reg1, out var family1)) return false;
            if (!_regToFamily.TryGetValue(reg2, out var family2)) return false;
            return family1 == family2;
        }

        private static bool IsSubRegisterOf(string widerReg, string narrowerReg)
        {
            if (!_regToFamily.TryGetValue(widerReg, out var family1)) return false;
            if (!_regToFamily.TryGetValue(narrowerReg, out var family2)) return false;
            if (family1 != family2) return false;
            return _regToFamilyIndex[widerReg] <= _regToFamilyIndex[narrowerReg];
        }

        public static string[] ParseDisassembly(string rawDisassembly)
        {
            string[] sizePrefixes = ["qword", "dword", "word", "byte", "ptr"];
            string stripped = rawDisassembly;
            foreach (string prefix in sizePrefixes)
                stripped = stripped.Replace(prefix, "");

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

        private static DisasmDescriptor SliceASM(TraceRow traceRow)
        {
            string[] disasmParts = ParseDisassembly(traceRow.Disasm);
            var descriptor = new DisasmDescriptor { type = ClassifyInstruction(disasmParts[0]) };

            if (disasmParts.Length > 1 && descriptor.type != DisasmType.Other)
            {
                if (descriptor.type != DisasmType.User)
                {
                    descriptor.write_to = disasmParts[1].Contains('[') ? "memory" : disasmParts[1];
                }

                if (descriptor.type == DisasmType.User)
                {
                    descriptor.read_from.AddRange(SplitReader(disasmParts[1]));
                    if (disasmParts.Length > 2)
                        descriptor.read_from.AddRange(SplitReader(disasmParts[2]));
                }
                else
                {
                    string readFrom = disasmParts.Length > 2 ? disasmParts[2] : disasmParts[1];
                    if (disasmParts[0] == "pop")
                        descriptor.read_from.Add("memory");
                    else
                        descriptor.read_from.AddRange(SplitReader(readFrom));
                }
            }

            AdditionalInstructions(disasmParts[0], descriptor);
            return descriptor;
        }

        private static void AdditionalInstructions(string instruction, DisasmDescriptor descriptor)
        {
            if (descriptor.type != DisasmType.Other) return;

            if (instruction == "cdqe")
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = "rax";
                descriptor.read_from.Add("eax");
            }
            else if (instruction == "cwde")
            {
                descriptor.type = DisasmType.Manipulator;
                descriptor.write_to = "eax";
                descriptor.read_from.Add("ax");
            }
        }

        private static List<string> SplitReader(string readFrom)
        {
            var result = new List<string>();
            string[] parts = readFrom.Split(['[', ']', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (_allRegisters.Contains(part))
                    result.Add(part);
            }
            return result;
        }

        public class RFlags
        {
            private readonly ulong _rflagsValue;

            public RFlags(ulong rflags) => _rflagsValue = rflags;

            // Status Flags
            public bool CarryFlag => (_rflagsValue & (1UL << 0)) != 0;
            public bool ParityFlag => (_rflagsValue & (1UL << 2)) != 0;
            public bool AdjustFlag => (_rflagsValue & (1UL << 4)) != 0;
            public bool ZeroFlag => (_rflagsValue & (1UL << 6)) != 0;
            public bool SignFlag => (_rflagsValue & (1UL << 7)) != 0;
            public bool OverflowFlag => (_rflagsValue & (1UL << 11)) != 0;

            // Control Flags
            public bool TrapFlag => (_rflagsValue & (1UL << 8)) != 0;
            public bool InterruptEnableFlag => (_rflagsValue & (1UL << 9)) != 0;
            public bool DirectionFlag => (_rflagsValue & (1UL << 10)) != 0;
        }

        private static void HideUselessFlagModifications(List<TraceRow> TraceRows)
        {
            foreach (var traceRow in TraceRows)
            {
                RFlags current_rflags = new RFlags(BitConverter.ToUInt64(traceRow.Regs[17]));
            }
        }
    }
}