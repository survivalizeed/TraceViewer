using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using System.Text.RegularExpressions;

namespace TraceViewer.Core
{
    public static class prefs
    {
        public static readonly List<Tuple<string, int>> X64_REGS = new List<Tuple<string, int>> {
            new Tuple<string, int>("rax", 8), 
            new Tuple<string, int>("rcx", 8),
            new Tuple<string, int>("rdx", 8),
            new Tuple<string, int>("rbx", 8),
            new Tuple<string, int>("rsp", 8),
            new Tuple<string, int>("rbp", 8),
            new Tuple<string, int>("rsi", 8),
            new Tuple<string, int>("rdi", 8),
            new Tuple<string, int>("r8", 8),
            new Tuple<string, int>("r9", 8),
            new Tuple<string, int>("r10", 8),
            new Tuple<string, int>("r11", 8),
            new Tuple<string, int>("r12", 8),
            new Tuple<string, int>("r13", 8),
            new Tuple<string, int>("r14", 8),
            new Tuple<string, int>("r15", 8),
            new Tuple<string, int>("rip", 8),
            new Tuple<string, int>("rflags", 8),
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 2), // Segment Registers --> Here skipped
            new Tuple<string, int>("", 4), // Alignment padding
            new Tuple<string, int>("dr0", 8),
            new Tuple<string, int>("dr1", 8),
            new Tuple<string, int>("dr2", 8),
            new Tuple<string, int>("dr3", 8),
            new Tuple<string, int>("dr6", 8),
            new Tuple<string, int>("dr7", 8),
            new Tuple<string, int>("", 80), // Byte Registers --> Here skipped
            new Tuple<string, int>("", 26), //x87FPU Registers --> Here skipped
            new Tuple<string, int>("", 4), // mxcsr --> Here skipped
            new Tuple<string, int>("", 2), // Alignment padding
            new Tuple<string, int>("xmm0", 16),
            new Tuple<string, int>("xmm1", 16),
            new Tuple<string, int>("xmm2", 16),
            new Tuple<string, int>("xmm3", 16),
            new Tuple<string, int>("xmm4", 16),
            new Tuple<string, int>("xmm5", 16),
            new Tuple<string, int>("xmm6", 16),
            new Tuple<string, int>("xmm7", 16),
            new Tuple<string, int>("xmm8", 16),
            new Tuple<string, int>("xmm9", 16),
            new Tuple<string, int>("xmm10", 16),
            new Tuple<string, int>("xmm11", 16),
            new Tuple<string, int>("xmm12", 16),
            new Tuple<string, int>("xmm13", 16),
            new Tuple<string, int>("xmm14", 16),
            new Tuple<string, int>("xmm15", 16),
            new Tuple<string, int>("ymm0", 32),
            new Tuple<string, int>("ymm1", 32),
            new Tuple<string, int>("ymm2", 32),
            new Tuple<string, int>("ymm3", 32),
            new Tuple<string, int>("ymm4", 32),
            new Tuple<string, int>("ymm5", 32),
            new Tuple<string, int>("ymm6", 32),
            new Tuple<string, int>("ymm7", 32),
            new Tuple<string, int>("ymm8", 32),
            new Tuple<string, int>("ymm9", 32),
            new Tuple<string, int>("ymm10", 32),
            new Tuple<string, int>("ymm11", 32),
            new Tuple<string, int>("ymm12", 32),
            new Tuple<string, int>("ymm13", 32),
            new Tuple<string, int>("ymm14", 32),
            new Tuple<string, int>("ymm15", 32),

        };
        public static readonly string[] X32_REGS = { "eax", "ebx", "ecx", "edx", "ebp", "esp", "esi", "edi", "eip" };
        public const bool TRACE_SHOW_OLD_REG_VALUE = true;
    }

    public class TraceData
    {
        public string Filename { get; set; }
        public string Arch { get; set; }
        public string IpReg { get; set; }
        public Dictionary<string, int> Regs { get; set; }
        public int PointerSize { get; set; }
        public List<TraceRow> Trace { get; set; }
    }

    public class TraceRow
    {
        public int Id { get; set; }
        public ulong Ip { get; set; }
        public string Disasm { get; set; }
        public List<byte[]> Regs { get; set; }
        public string Opcodes { get; set; }
        public List<MemoryAccess> Mem { get; set; }
        public List<string> Regchanges { get; set; }

        public List<string> highlights = new List<string>();
    }

    public class MemoryAccess
    {
        public string Access { get; set; }
        public ulong Addr { get; set; }
        public ulong Value { get; set; }
    }

    public class TraceLoader
    {
        private const string HexPrefix = "0x";
        private const string ChangeSeparator = "; ";
        private const string ChangeArrow = " -> ";
        private const string RegisterValueSeparator = ": ";
        private const string ZeroHexValue = "00";

        public TraceData OpenX64dbgTrace(string filename)
        {
            TraceData traceData = new TraceData();
            traceData.Filename = filename;

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                byte[] magic = br.ReadBytes(4);
                if (!magic.SequenceEqual(Encoding.ASCII.GetBytes("TRAC")))
                {
                    throw new Exception("Error, wrong file format.");
                }

                int jsonLength = BitConverter.ToInt32(br.ReadBytes(4), 0);
                byte[] jsonBlob = br.ReadBytes(jsonLength);
                string jsonStr = Encoding.UTF8.GetString(jsonBlob);
                JObject jsonObj = JObject.Parse(jsonStr);
                string arch = jsonObj["arch"].ToString();

                List<Tuple<string, int>> regs;
                string ipReg;
                int pointerSize;
                //if (arch == "x64")
                //{
                    regs = prefs.X64_REGS;
                    ipReg = "rip";
                    pointerSize = 8;
                //}
                //else
                //{
                //    //regs = prefs.X32_REGS;
                //    ipReg = "eip";
                //    pointerSize = 4;
                //}

                Dictionary<string, int> regIndexes = new Dictionary<string, int>();
                for (int i = 0; i < regs.Count; i++)
                {
                    regIndexes[regs[i].Item1] = i;
                }

                traceData.Arch = arch;
                traceData.IpReg = ipReg;
                traceData.Regs = regIndexes;
                traceData.PointerSize = pointerSize;
                traceData.Trace = new List<TraceRow>();

                X86DisassembleMode mode = arch == "x64" ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32;
                using (var dis = CapstoneDisassembler.CreateX86Disassembler(mode))
                {
                    dis.EnableInstructionDetails = true;
                    dis.DisassembleSyntax = DisassembleSyntax.Intel;

                    List<byte[]> regValues = new List<byte[]>();
                    for (int i = 0; i < regs.Count; i++)
                    {
                        regValues.Add(new byte[regs[i].Item2]);
                    }
                    int rowId = 0;

                    while (fs.Position < fs.Length)
                    {
                        int nextByte = fs.ReadByte();
                        if (nextByte != 0x00)
                        {
                            break;
                        }

                        byte registerChanges = br.ReadByte();
                        byte memoryAccesses = br.ReadByte();
                        byte flagsAndOpcodeSize = br.ReadByte();
                        int threadIdBit = flagsAndOpcodeSize >> 7 & 1;
                        int opcodeSize = flagsAndOpcodeSize & 15;
                        uint threadId = 0;
                        if (threadIdBit > 0)
                        {
                            threadId = br.ReadUInt32();
                        }
                        byte[] opcodes = br.ReadBytes(opcodeSize);

                        List<int> registerChangePositions = new List<int>();
                        for (int i = 0; i < registerChanges; i++)
                        {
                            registerChangePositions.Add(br.ReadByte());
                        }

                        bool initial_state = true; // if it the first row, the changed registers are all 0 because the layout is 8 byted.
                                                   // here we want to have precise slicing of the registers so we need to know which register has which size
                        for (int i = 0; i < registerChanges; i++)
                        {
                            if (registerChangePositions[i] != 0)
                            {
                                initial_state = false;
                            }
                        }

                        if (initial_state) // so if its all 0 we can just set the registerChangePositions to 0,1,2,3,4,5,6,7 to get exact information
                        {
                            for (int i = 0; i < registerChanges; i++)
                            {
                                registerChangePositions[i] = i;
                            }
                        }

                        List<byte[]> registerChangeNewData = new List<byte[]>();
                        byte[] buffer = br.ReadBytes(8 * registerChanges); // load everything into a buffer to not fuck up any offsets. Even if not all data is used

                        using (MemoryStream ms = new MemoryStream(buffer))
                        using (BinaryReader reader = new BinaryReader(ms))
                        {
                            for (int i = 0; i < registerChanges; i++)
                            {
                                if (registerChangePositions[i] < regs.Count)
                                {
                                    byte[] data = reader.ReadBytes(regs[registerChangePositions[i]].Item2);
                                    registerChangeNewData.Add(data);
                                }
                            }

                        }
                        List<byte> memoryAccessFlags = new List<byte>();
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            memoryAccessFlags.Add(br.ReadByte());
                        }

                        List<ulong> memoryAccessAddresses = new List<ulong>();
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte[] data = br.ReadBytes(pointerSize);
                            ulong addr = pointerSize == 8 ? BitConverter.ToUInt64(data, 0) : BitConverter.ToUInt32(data, 0);
                            memoryAccessAddresses.Add(addr);
                        }

                        List<ulong> memoryAccessOldData = new List<ulong>();
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte[] data = br.ReadBytes(pointerSize);
                            ulong val = pointerSize == 8 ? BitConverter.ToUInt64(data, 0) : BitConverter.ToUInt32(data, 0);
                            memoryAccessOldData.Add(val);
                        }

                        List<ulong> memoryAccessNewData = new List<ulong>();
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            if ((memoryAccessFlags[i] & 1) == 0)
                            {
                                byte[] data = br.ReadBytes(pointerSize);
                                ulong val = pointerSize == 8 ? BitConverter.ToUInt64(data, 0) : BitConverter.ToUInt32(data, 0);
                                memoryAccessNewData.Add(val);
                            }
                        }

                        if (initial_state) // change back to 0 so the position parsing wont fuck up
                        {
                            for (int i = 0; i < registerChanges; i++)
                            {
                                registerChangePositions[i] = 0;
                            }
                        }

                        int regId = 0;
                        List<string> regchanges = new List<string>();
                        for (int i = 0; i < registerChangePositions.Count; i++)
                        {
                            regId += registerChangePositions[i];
                            int index = regId + i;
                            if (index < regValues.Count)
                            {
                                regValues[index] = registerChangeNewData[i];
                            }
                        }

                        ulong ip = BitConverter.ToUInt64(regValues[regIndexes[ipReg]], 0);
                        string disasm = "";
                        var instructions = dis.Disassemble(opcodes, (long)ip);
                        foreach (var instr in instructions)
                        {
                            disasm = instr.Mnemonic + " ";
                            if (!string.IsNullOrEmpty(instr.Operand))
                            {
                                string[] sliced = Regex.Split(instr.Operand, @"([ ,:\[\]*])");
                                foreach(string slice in sliced)
                                {
                                    string newSlice = slice;
                                    if (Regex.IsMatch(slice, @"^[0-9A-F]$"))
                                    {
                                        newSlice = newSlice.ToUpper();
                                        newSlice = slice.Insert(0, "0x");    
                                    }
                                    else if(newSlice.StartsWith("0x"))
                                    {
                                        newSlice = "0x" + newSlice.Substring(2).ToUpper();
                                    }
                                    disasm += newSlice;
                                }
                                
                            }
                        }

                        List<MemoryAccess> mems = new List<MemoryAccess>();
                        int newDataCounter = 0;
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte flag = memoryAccessFlags[i];
                            ulong value = memoryAccessOldData[i];
                            string access = "READ";
                            if ((flag & 1) == 0)
                            {
                                value = memoryAccessNewData[newDataCounter];
                                access = "WRITE";
                                newDataCounter++;
                            }

                            if (disasm.Contains("dword"))
                                value &= 0xFFFFFFFF;
                            else if (disasm.Contains("word"))
                                value &= 0xFFFF;
                            else if (disasm.Contains("byte"))
                                value &= 0xFF;

                            mems.Add(new MemoryAccess
                            {
                                Access = access,
                                Addr = memoryAccessAddresses[i],
                                Value = value
                            });
                        }

                        if (traceData.Trace.Any())
                        {
                            traceData.Trace.Last().Regchanges = regchanges;
                        }

                        List<byte[]> cloned_regValues = new List<byte[]>();
                        for (int i = 0; i < regValues.Count; i++)
                        {
                            if (regs[i].Item1 == "")
                            {
                                continue;
                            }
                            cloned_regValues.Add((byte[])regValues[i].Clone());
                        }

                        TraceRow traceRow = new TraceRow
                        {
                            Id = rowId,
                            Ip = ip,
                            Disasm = disasm,
                            Regs = cloned_regValues,
                            Opcodes = BitConverter.ToString(opcodes).Replace("-", ""),
                            Mem = mems
                        };
                        traceData.Trace.Add(traceRow);
                        rowId++;
                    }
                }
                var registers = prefs.X64_REGS.ToList();
                registers.RemoveAll(reg => string.IsNullOrEmpty(reg.Item1));

                for (int i = 0; i < traceData.Trace.Count - 1; i++)
                {
                    for (int j = 0; j < registers.Count; ++j)
                    {
                        // Compare register values and skip 'rip' register
                        if (!traceData.Trace[i + 1].Regs[j].SequenceEqual(traceData.Trace[i].Regs[j]) && regs[j].Item1 != "rip")
                        {
                            string currentRegHex = ByteArrayToHexString(traceData.Trace[i].Regs[j]);
                            string nextRegHex = ByteArrayToHexString(traceData.Trace[i + 1].Regs[j]);

                            traceData.Trace[i].Regchanges.Add(regs[j].Item1);
                            traceData.Trace[i].Regchanges.Add(RegisterValueSeparator);
                            traceData.Trace[i].Regchanges.Add(HexPrefix + currentRegHex);
                            traceData.Trace[i].Regchanges.Add(ChangeArrow);
                            traceData.Trace[i].Regchanges.Add(HexPrefix + nextRegHex);
                            traceData.Trace[i].Regchanges.Add(ChangeSeparator);
                            traceData.Trace[i].highlights.Add(regs[j].Item1); // Mark register for highlight on hover
                        }
                    }
                }
            }

            return traceData;
        }
        private string ByteArrayToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ZeroHexValue;
            }
            StringBuilder hexBuilder = new StringBuilder(bytes.Length * 2);
            bool leadingZero = true; // Flag to handle leading zeros correctly
            for (int i = bytes.Length - 1; i >= 0; i--) // Iterate in reverse without Reverse()
            {
                byte b = bytes[i];
                if (b != 0 || !leadingZero || i == 0) // Keep at least one zero if all bytes are zero
                {
                    hexBuilder.Append(b.ToString("X2"));
                    leadingZero = false; // No longer leading zero after a non-zero byte or the last byte is processed
                }
            }
            return hexBuilder.Length == 0 ? ZeroHexValue : hexBuilder.ToString(); // Handle empty builder case
        }
    }

}