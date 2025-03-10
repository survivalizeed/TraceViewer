using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TraceViewer.UserWindows;

namespace TraceViewer.Core.Analysis
{

    public enum DisasmType
    {
        Assigning,
        Manipulating,
        Storing,
        Undef
    }

    public class DisasmDescriptor
    {
        public string val;

        public DisasmType type;

        public DisasmDescriptor(string val, DisasmType type)
        {
            this.val = val;
            this.type = type;
        }

        public DisasmDescriptor() { }
    }


    class DeObfus
    {
        public static void DeObfuscate()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            var TraceRows = TraceHandler.Trace.Trace;

            //HideUselessInstructions(TraceRows);
            //
            //HideObfuscatedUslessInstructions(TraceRows);

            //MessageDialog messageDialog = new MessageDialog("Deobfuscation is experimental. Use at your own risk!");
            //messageDialog.ShowDialog();

            Deob(TraceRows);

            window.RefreshView();
        }

        private static void HideUselessInstructions(List<TraceRow> TraceRows)
        {
            for (int i = 0; i < TraceRows.Count; i++)
            {
                var row = TraceRows[i];
                var next_row = i < TraceRows.Count - 1 ? TraceRows[i + 1] : null;

                bool hasOnlyReadOrEmpty = true;
                foreach (var mem in row.Mem)
                {
                    if (mem.Access != "READ")
                    {
                        hasOnlyReadOrEmpty = false;
                        break;
                    }
                }

                // Hide rows with only rflags changes which don't have any other changes at all
                if (row.Regchanges == null || row.Regchanges.Count == 0 && hasOnlyReadOrEmpty)
                    WPF_TraceRow.hiddenRows.Add(row.Id);
                if (row.Regchanges != null && row.Regchanges.Count == 6 && row.Regchanges[0] == "rflags" && hasOnlyReadOrEmpty)
                    WPF_TraceRow.hiddenRows.Add(row.Id);
            }
        }

        private static void HideObfuscatedUslessInstructions(List<TraceRow> TraceRows)
        {
            for (int i = 0; i < TraceRows.Count; i++)
            {
                var row = TraceRows[i];
                var next_row = i < TraceRows.Count - 1 ? TraceRows[i + 1] : null;
                if (next_row == null) return;
                if (row.Disasm.StartsWith("push") && next_row.Disasm.StartsWith("ret"))
                {
                    WPF_TraceRow.hiddenRows.Add(row.Id);
                    WPF_TraceRow.hiddenRows.Add(next_row.Id);
                    i++;
                }
            }
        }

        private static void Deob(List<TraceRow> TraceRows)
        {
            Dictionary<string, string[]> registerFamilies = new Dictionary<string, string[]>
            {
                { "raxx", new[] { "rax", "eax", "ax", "ah", "al" } },
                { "rbxx", new[] { "rbx", "ebx", "bx", "bh", "bl" } },
                { "rcxx", new[] { "rcx", "ecx", "cx", "ch", "cl" } },
                { "rdxx", new[] { "rdx", "edx", "dx", "dh", "dl" } },
                { "rspx", new[] { "rsp", "esp", "sp", "spl" } },
                { "rbpx", new[] { "rbp", "ebp", "bp", "bpl" } },
                { "rsix", new[] { "rsi", "esi", "si", "sil" } },
                { "rdix", new[] { "rdi", "edi", "di", "dil" } },
                { "r8x", new[]  { "r8", "r8d", "r8w", "r8b" } },
                { "r9x", new[]  { "r9", "r9d", "r9w", "r9b" } },
                { "r10x", new[] { "r10", "r10d", "r10w", "r10b" } },
                { "r11x", new[] { "r11", "r11d", "r11w", "r11b" } },
                { "r12x", new[] { "r12", "r12d", "r12w", "r12b" } },
                { "r13x", new[] { "r13", "r13d", "r13w", "r13b" } },
                { "r14x", new[] { "r14", "r14d", "r14w", "r14b" } },
                { "r15x", new[] { "r15", "r15d", "r15w", "r15b" } }
            };

            for (int i = 0; i < TraceRows.Count; i++)
            {
                var row = TraceRows[i];
                var asm = SliceASM(row);


                if (asm?["instruction"].type == DisasmType.Assigning)
                {
                    if (!asm.ContainsKey("operand1")) continue;
                    string currentOperand = asm["operand1"].val;

                    if (registerFamilies["rspx"].Contains(currentOperand)) // rsp is too hard to track
                        continue;

                    for (int j = 1; i - j >= 0; j++)
                    {
                        if (j == 30) break; // Maximum lookup
                        var prev_row = TraceRows[i - j];
                        var prev_asm = SliceASM(prev_row);

                        if (prev_asm != null && prev_asm.ContainsKey("operand1") && prev_asm.Count > 1)
                        {
                            string prevOperand = prev_asm["operand1"].val;

                            if (IsSubRegisterOf(currentOperand, prevOperand, registerFamilies) && prev_asm["instruction"].type != DisasmType.Storing)
                                WPF_TraceRow.hiddenRows.Add(prev_row.Id);
                            else if (IsSubRegisterOf(currentOperand, prevOperand, registerFamilies) && prev_asm["instruction"].type == DisasmType.Storing)
                                break;
                        }
                    }
                }
            }
        }

        private static bool IsSubRegisterOf(string widerReg, string narrowerReg, Dictionary<string, string[]> registerFamilies)
        {
            foreach (var family in registerFamilies)
            {
                if (family.Value.Contains(widerReg) && family.Value.Contains(narrowerReg))
                {
                    int widerRegIndex = Array.IndexOf(family.Value, widerReg);
                    int narrowerRegIndex = Array.IndexOf(family.Value, narrowerReg);
                    return widerRegIndex <= narrowerRegIndex;
                }
            }
            return false;
        }


        public static string[] ParseDisassembly(string rawDisassembly)
        {
            string[] sizePrefixes = { "qword", "dword", "word", "byte", "ptr" };
            string sizePrefixStrippedDisassembly = rawDisassembly;
            foreach (string prefix in sizePrefixes)
            {
                sizePrefixStrippedDisassembly = sizePrefixStrippedDisassembly.Replace(prefix, "");
            }

            List<string> parts = new List<string>();
            string remainingDisassembly = sizePrefixStrippedDisassembly.Trim();

            Match instructionMatch = Regex.Match(remainingDisassembly, @"^(\S+)");
            if (instructionMatch.Success)
            {
                parts.Add(instructionMatch.Groups[1].Value);
                remainingDisassembly = remainingDisassembly.Substring(instructionMatch.Length).Trim();
            }
            else if (!string.IsNullOrEmpty(remainingDisassembly))
            {
                parts.Add(remainingDisassembly);
                return parts.ToArray();
            }
            else
                return parts.ToArray();


            while (!string.IsNullOrEmpty(remainingDisassembly))
            {
                Match memoryAddressMatch = Regex.Match(remainingDisassembly, @"^\[.*?\]");
                if (memoryAddressMatch.Success)
                {
                    parts.Add(memoryAddressMatch.Value);
                    remainingDisassembly = remainingDisassembly.Substring(memoryAddressMatch.Length).Trim();
                    continue;
                }

                Match immediateMatch = Regex.Match(remainingDisassembly, @"^0x[0-9a-fA-F]+\b");
                if (immediateMatch.Success)
                {
                    parts.Add(immediateMatch.Value);
                    remainingDisassembly = remainingDisassembly.Substring(immediateMatch.Length).Trim();
                    continue;
                }

                Match registerMatch = Regex.Match(remainingDisassembly, @"^\b[a-zA-Z0-9]+\b");
                if (registerMatch.Success)
                {
                    parts.Add(registerMatch.Value);
                    remainingDisassembly = remainingDisassembly.Substring(registerMatch.Length).Trim();
                    continue;
                }

                Match delimiterMatch = Regex.Match(remainingDisassembly, @"^[,\s]+");
                if (delimiterMatch.Success)
                {
                    remainingDisassembly = remainingDisassembly.Substring(delimiterMatch.Length).Trim();
                    continue;
                }
                remainingDisassembly = remainingDisassembly.Substring(1).Trim();
            }
            return parts.ToArray();
        }

        private static Dictionary<string, DisasmDescriptor> SliceASM(TraceRow traceRow)
        {
            string rawDisassembly = traceRow.Disasm;

            string[] disasmParts = ParseDisassembly(rawDisassembly);

            Dictionary<string, DisasmDescriptor> disasmSlices = new Dictionary<string, DisasmDescriptor>();

            if (disasmParts.Length > 0 && !string.IsNullOrEmpty(disasmParts[0]))
            {
                DisasmDescriptor disasmDescriptor = new DisasmDescriptor();
                disasmDescriptor.val = disasmParts[0];

                // Has to be extended
                if (disasmDescriptor.val.StartsWith("mov") || disasmDescriptor.val.StartsWith("lea") || disasmDescriptor.val == "pop")
                    disasmDescriptor.type = DisasmType.Assigning;
                else if (disasmDescriptor.val.StartsWith("push"))
                    disasmDescriptor.type = DisasmType.Storing;
                else
                    disasmDescriptor.type = DisasmType.Undef;

                disasmSlices["instruction"] = disasmDescriptor;

                if (disasmParts.Length > 1 && !string.IsNullOrEmpty(disasmParts[1]))
                {
                    disasmSlices["operand1"] = new DisasmDescriptor(disasmParts[1], DisasmType.Undef);
                }

                if (disasmParts.Length > 2 && !string.IsNullOrEmpty(disasmParts[2]))
                {
                    disasmSlices["operand2"] = new DisasmDescriptor(disasmParts[2], DisasmType.Undef);
                }
                return disasmSlices;
            }
            return null;
        }
    }
}