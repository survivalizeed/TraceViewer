using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;

namespace TraceViewer.Core.Analysis
{

    public enum DisasmType
    {
        Setter,
        Storer,
        Manipulator,
        Other
    }

    public class DisasmDescriptor
    {
        public DisasmType type;

        public string write_to = "";
        public List<string> read_from = new List<string>();

        public bool useless = false; // Will be set by the analyzer

        public DisasmDescriptor(DisasmType type)
        {
            this.type = type;
        }

        public DisasmDescriptor() { }
    }


    class DeObfus
    {

        private static Dictionary<string, string[]> registerFamilies = new Dictionary<string, string[]>
        {
            { "raxx", new[] { "rax", "eax", "ax", "ah", "al" } },
            { "rbxx", new[] { "rbx", "ebx", "bx", "bh", "bl" } },
            { "rcxx", new[] { "rcx", "ecx", "cx", "ch", "cl" } },
            { "rdxx", new[] { "rdx", "edx", "dx", "dh", "dl" } },
            { "rspx", new[] { "rsp", "esp", "sp", "spl" } },
            { "rbpx", new[] { "rbp", "ebp", "bp", "bpl" } },
            { "rsix", new[] { "rsi", "esi", "si", "sil" } },
            { "rdix", new[] { "rdi", "edi", "di", "dil" } },
            { "r8x",  new[] { "r8", "r8d", "r8w", "r8b" } },
            { "r9x",  new[] { "r9", "r9d", "r9w", "r9b" } },
            { "r10x", new[] { "r10", "r10d", "r10w", "r10b" } },
            { "r11x", new[] { "r11", "r11d", "r11w", "r11b" } },
            { "r12x", new[] { "r12", "r12d", "r12w", "r12b" } },
            { "r13x", new[] { "r13", "r13d", "r13w", "r13b" } },
            { "r14x", new[] { "r14", "r14d", "r14w", "r14b" } },
            { "r15x", new[] { "r15", "r15d", "r15w", "r15b" } }
        };

        public static void DeObfuscate()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            var TraceRows = TraceHandler.Trace.Trace;

            InputDialog input = new InputDialog("The Deobfuscation tries to detect useless code.\r\nUse at your own risk!\r\nInput an analysis depth iteration count:");
            input.ShowDialog();
            var res = input.GetResult();
            if (!string.IsNullOrEmpty(res))
            {
                try
                {
                    int iterations = Convert.ToInt32(res);
                    Analyze(TraceRows, iterations);
                    window.RefreshView();
                }
                catch (FormatException)
                {
                    MessageDialog messageDialog = new MessageDialog("Invalid input. Use a numerical value!");
                    messageDialog.ShowDialog();
                }
            }
           
        }

        private static void Analyze(List<TraceRow> TraceRows, int iterations)
        {
            WPF_TraceRow.hiddenRows.Clear();
            List<DisasmDescriptor> descriptors = new List<DisasmDescriptor>();
            for (int i = 0; i < TraceRows.Count; i++)
            {
                descriptors.Add(SliceASM(TraceRows[i]));
            }
            for (int h = 0; h < iterations; h++)
            {
                for (int i = 0; i < descriptors.Count; i++)
                {
                    var currentDescriptor = descriptors[i];

                    if (currentDescriptor.type != DisasmType.Setter && currentDescriptor.type != DisasmType.Manipulator)
                        continue;

                    if (string.IsNullOrEmpty(currentDescriptor.write_to))
                        continue;

                    if(currentDescriptor.useless)
                        continue;

                    bool found = false;
                    foreach(var rspx in registerFamilies["rspx"])
                    {
                        if (TraceRows[i].Disasm.Contains(rspx))
                            found = true;
                    }
                    if (found)
                        continue;

                    string writtenRegister = currentDescriptor.write_to;
                    bool isUseless = true;

                    for (int j = i + 1; j < descriptors.Count; j++)
                    {
                        var nextDescriptor = descriptors[j];
                        if (nextDescriptor.type == DisasmType.Other)
                            continue;

                        foreach (var readReg in nextDescriptor.read_from)
                        {
                            if (!nextDescriptor.useless)
                                if (IsSubRegisterOf(writtenRegister, readReg, registerFamilies) || IsSubRegisterOf(readReg, writtenRegister, registerFamilies) || writtenRegister == readReg) // Check if any read register is sub-register or super-register or the same register as writtenRegister
                                {
                                    // Not useless, because it was used
                                    isUseless = false;
                                    break;
                                }
                        }
                        if (!isUseless)
                            break;

                        if (nextDescriptor.type == DisasmType.Setter)
                        {
                            if (IsSubRegisterOf(writtenRegister, nextDescriptor.write_to, registerFamilies) || IsSubRegisterOf(nextDescriptor.write_to, writtenRegister, registerFamilies) || writtenRegister == nextDescriptor.write_to)
                            {
                                // Useless because it was overwritten
                                break;
                            }
                        }

                    }

                    if (isUseless)
                    {
                        currentDescriptor.useless = true;
                        WPF_TraceRow.hiddenRows.Add(i);
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

        public static DisasmType ClassifyInstruction(string instruction)
        {
            string[] setters = { "mov", "lea", "pop", "movabs" , "movsx", "movsxd", "movzx"};
            string[] storeres = { "push" };
            string[] manipulators = { "add", "sub", "mul", "div", "inc", "dec", "neg", "not", "and", "or", 
                "xor", "shl", "shr", "sar", "rol", "ror", "rcl", "rcr", "imul", "idiv", "sal", "sar", "shl", "shr", 
                "bswap", "bsf", "bsr", "bt", "btc", "btr", "bts", "set", "xadd", "adc", "sbb", "lahf", "sahf" };

            if(setters.Contains(instruction))
                return DisasmType.Setter;
            if (storeres.Contains(instruction))
                return DisasmType.Storer;
            if (manipulators.Contains(instruction))
                return DisasmType.Manipulator;

            return DisasmType.Other;
        }
        private static DisasmDescriptor SliceASM(TraceRow traceRow)
        {
            string rawDisassembly = traceRow.Disasm;
            string[] disasmParts = ParseDisassembly(rawDisassembly);

            DisasmDescriptor disasmDescriptor = new DisasmDescriptor();

            disasmDescriptor.type = ClassifyInstruction(disasmParts[0]);

            if (disasmParts.Length > 1 && disasmDescriptor.type != DisasmType.Other)
            {
                if(disasmDescriptor.type != DisasmType.Storer)
                    disasmDescriptor.write_to = disasmParts[1];

                string read_from = disasmParts[1];
                if(disasmParts.Length > 2)
                    read_from = disasmParts[2];

                string[] splitted = read_from.Split(new char[] { '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var split in splitted)
                {
                    if (split.StartsWith("0x"))
                    {
                        disasmDescriptor.read_from.Add("intermediate");
                        continue;
                    }
                    foreach (var family in registerFamilies)
                    {
                        if (family.Value.Contains(split))
                        {
                            disasmDescriptor.read_from.Add(split);
                            break;
                        }
                    }
                }
            }

            return disasmDescriptor;
        }
    }
}