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
        public static readonly string[] X64_REGS = { "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15", "rip" };
        public static readonly string[] X32_REGS = { "eax", "ebx", "ecx", "edx", "esi", "edi", "ebp", "esp", "eip" };
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
        public ulong[] Regs { get; set; }
        public string Opcodes { get; set; }
        public List<MemoryAccess> Mem { get; set; }
        public string Regchanges { get; set; }
    }

    public class MemoryAccess
    {
        public string Access { get; set; }
        public ulong Addr { get; set; }
        public ulong Value { get; set; }
    }

    public class TraceLoader
    {

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

                string[] regs;
                string ipReg;
                int pointerSize;
                if (arch == "x64")
                {
                    regs = prefs.X64_REGS;
                    ipReg = "rip";
                    pointerSize = 8;
                }
                else
                {
                    regs = prefs.X32_REGS;
                    ipReg = "eip";
                    pointerSize = 4;
                }

                Dictionary<string, int> regIndexes = new Dictionary<string, int>();
                for (int i = 0; i < regs.Length; i++)
                {
                    regIndexes[regs[i]] = i;
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

                    ulong[] regValues = new ulong[regs.Length];
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

                        List<ulong> registerChangeNewData = new List<ulong>();
                        for (int i = 0; i < registerChanges; i++)
                        {
                            byte[] data = br.ReadBytes(pointerSize);
                            ulong val = pointerSize == 8 ? BitConverter.ToUInt64(data, 0) : BitConverter.ToUInt32(data, 0);
                            registerChangeNewData.Add(val);
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

                        int regId = 0;
                        string regchanges = "";
                        for (int i = 0; i < registerChangePositions.Count; i++)
                        {
                            regId += registerChangePositions[i];
                            int index = regId + i;
                            if (index < regValues.Length)
                            {
                                regValues[index] = registerChangeNewData[i];
                            }
                            if (index < regValues.Length && rowId > 0)
                            {
                                string regName = regs[index];
                                if (regName != ipReg)
                                {
                                    ulong oldValue = traceData.Trace.Last().Regs[index];
                                    ulong newValue = registerChangeNewData[i];
                                    if (oldValue != newValue)
                                    {
                                        if (prefs.TRACE_SHOW_OLD_REG_VALUE)
                                        {
                                            regchanges += $"{regName}: {oldValue:X} -> {newValue:X} ";
                                        }
                                        else
                                        {
                                            regchanges += $"{regName}: {newValue:X} ";
                                        }
                                        if (newValue < 0x7F && newValue > 0x1F)
                                        {
                                            regchanges += $"'{(char)newValue}' ";
                                        }
                                    }
                                }
                            }
                        }

                        ulong ip = regValues[regIndexes[ipReg]];
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
                                        newSlice = slice.Insert(0, "0x");
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

                        if (!string.IsNullOrEmpty(regchanges) && traceData.Trace.Any())
                        {
                            traceData.Trace.Last().Regchanges = regchanges;
                        }

                        TraceRow traceRow = new TraceRow
                        {
                            Id = rowId,
                            Ip = ip,
                            Disasm = disasm,
                            Regs = (ulong[])regValues.Clone(),
                            Opcodes = BitConverter.ToString(opcodes).Replace("-", ""),
                            Mem = mems
                        };
                        traceData.Trace.Add(traceRow);
                        rowId++;
                    }
                }
            }
            return traceData;
        }
    }
}