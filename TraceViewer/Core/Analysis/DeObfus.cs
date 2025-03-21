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
        User,
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

        public static void DeObfuscate()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            var TraceRows = TraceHandler.Trace.Trace;

            MessageDialog input = new MessageDialog("The Deobfuscation tries to detect useless code.\r\nUse at your own risk!");
            input.ShowDialog();
            
            Analyze(TraceRows);
            window.RefreshView();
           
        }

        private static void Analyze(List<TraceRow> TraceRows)
        {
            List<DisasmDescriptor> descriptors = new List<DisasmDescriptor>();
            for (int i = 0; i < TraceRows.Count; i++)
            {
                descriptors.Add(SliceASM(TraceRows[i]));
            }

            bool found_something_useless = false;
            do
            {
                found_something_useless = false;
                for (int i = 0; i < descriptors.Count; i++)
                {
                    var currentDescriptor = descriptors[i];

                    if (currentDescriptor.type != DisasmType.Setter && currentDescriptor.type != DisasmType.Manipulator)
                        continue;

                    if (string.IsNullOrEmpty(currentDescriptor.write_to) || currentDescriptor.useless)
                        continue;

                    bool found = false;
                    foreach (var rspx in Globals.registerFamilies["rspx"]) // rsp won't be touched as its too hard to track
                    {
                        if (TraceRows[i].Disasm.Contains(rspx))
                            found = true;
                    }
                    foreach (var ripx in Globals.registerFamilies["ripx"]) // rip won't be touched as its too hard to track
                    {
                        if (TraceRows[i].Disasm.Contains(ripx))
                            found = true;
                    }
                    if(currentDescriptor.write_to == "memory")
                    {
                        found = true;
                    }
                    if (found)
                        continue;


                    string writtenRegister = currentDescriptor.write_to;
                    bool isUseless = false;

                    for (int j = i + 1; j < descriptors.Count; j++)
                    {
                        var nextDescriptor = descriptors[j];
                        if (nextDescriptor.type == DisasmType.Other)
                            continue;

                        foreach (var readReg in nextDescriptor.read_from)
                        {
                            // Can't use nextDescriptor as proof that current instruction is useful if nextDescriptor is marked as useless
                            if (!nextDescriptor.useless)
                                if (IsSubRegisterOf(writtenRegister, readReg, Globals.registerFamilies) || IsSubRegisterOf(readReg, writtenRegister, Globals.registerFamilies) || writtenRegister == readReg) // Check if any read register is sub-register or super-register or the same register as writtenRegister
                                {
                                    goto leave;
                                }
                        }

                        if (nextDescriptor.type == DisasmType.Setter)
                        {
                            if (IsSubRegisterOf(writtenRegister, nextDescriptor.write_to, Globals.registerFamilies) || IsSubRegisterOf(nextDescriptor.write_to, writtenRegister, Globals.registerFamilies) || writtenRegister == nextDescriptor.write_to)
                            {
                                // Useless because it was overwritten
                                isUseless = true;
                                break;
                            }
                        }

                    }
                leave:
                    if (isUseless)
                    {
                        found_something_useless = true;
                        currentDescriptor.useless = true;
                        WPF_TraceRow.hiddenRows.Add(i);
                    }
                }
            } while (found_something_useless);
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

            string[] users = { "cmp", "test", "jmp", "je", "jz", "jne", "jnz", "jg", "jnle", "jge", 
                "jnl", "jl", "jnge", "jle", "jng", "ja", "jnbe", "jae", "jnb", "jb", "jnae", "jbe", "jna", 
                "jo", "jno", "js", "jns", "jp", "jpe", "jnp", "jpo", "loop", "loope", "loopz", "loopne", "loopnz", "jcxz", "jecxz" };

            string[] manipulators = { "add", "sub", "mul", "div", "inc", "dec", "neg", "not", "and", "or", 
                "xor", "shl", "shr", "sar", "rol", "ror", "rcl", "rcr", "imul", "idiv", "sal", "sar", "shl", "shr", 
                "bswap", "bsf", "bsr", "bt", "btc", "btr", "bts", "set", "xadd", "adc", "sbb", "lahf", "sahf", "setne", "setl", 
                "setae"};

            if(setters.Contains(instruction))
                return DisasmType.Setter;
            if (users.Contains(instruction))
                return DisasmType.User;
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
                if (disasmDescriptor.type != DisasmType.User)
                {
                    if (disasmParts[1].Contains('['))
                        disasmDescriptor.write_to = "memory";
                    else
                        disasmDescriptor.write_to = disasmParts[1];
                }
                if (disasmDescriptor.type == DisasmType.User)
                {
                    // Both registers are read from. Aka. cmp or test
                    disasmDescriptor.read_from.AddRange(SplitReader(disasmParts[1]));
                    if(disasmParts.Length > 2)
                        disasmDescriptor.read_from.AddRange(SplitReader(disasmParts[2]));
                }
                else
                {
                    string read_from = disasmParts[1];
                    if (disasmParts.Length > 2)
                            read_from = disasmParts[2];

                    // Extra case for pop, because the read would have been the same as write which would have made it non setting
                    if (disasmParts[0] == "pop")
                        disasmDescriptor.read_from.Add("memory");
                    else
                        disasmDescriptor.read_from.AddRange(SplitReader(read_from));
                }
            }

            AdditionalInstructions(disasmParts[0], disasmDescriptor);

            return disasmDescriptor;
        }

        private static void AdditionalInstructions(string instruction, DisasmDescriptor disasmDescriptor)
        {
            if (disasmDescriptor.type != DisasmType.Other)
                return;

            if(instruction == "cdqe")
            {
                disasmDescriptor.type = DisasmType.Manipulator;
                disasmDescriptor.write_to = "rax";
                disasmDescriptor.read_from.Add("eax");
            }
            else if (instruction == "cwde")
            {
                disasmDescriptor.type = DisasmType.Manipulator;
                disasmDescriptor.write_to = "eax";
                disasmDescriptor.read_from.Add("ax");
            }

        }

        private static List<string> SplitReader(string read_from)
        {
            List<string> dD = new List<string>();
            string[] splitted = read_from.Split(new char[] { '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var split in splitted)
            {
                foreach (var family in Globals.registerFamilies)
                {
                    if (family.Value.Contains(split))
                    {
                        dD.Add(split);
                        break;
                    }
                }
            }
            return dD;
        } 
    }
}