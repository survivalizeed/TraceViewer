using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using System.Text.RegularExpressions;
using System.Text.Json;



namespace TraceViewer.Core
{
    public static class REGDUMP
    {
        public static readonly List<Tuple<string, int>> X64_REGS_PARSING = new List<Tuple<string, int>> {
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
            new Tuple<string, int>("", 2), // segments    
            new Tuple<string, int>("", 2), // segments    
            new Tuple<string, int>("", 2), // segments    
            new Tuple<string, int>("", 2), // segments    
            new Tuple<string, int>("", 2), // segments    
            new Tuple<string, int>("", 2), // segments          
            new Tuple<string, int>("dr0", 8),    
            new Tuple<string, int>("dr1", 8),    
            new Tuple<string, int>("dr2", 8),    
            new Tuple<string, int>("dr3", 8),    
            new Tuple<string, int>("dr6", 8),    
            new Tuple<string, int>("dr7", 8),    
            new Tuple<string, int>("", 80), // register area
            new Tuple<string, int>("", 2),
            new Tuple<string, int>("", 2),
            new Tuple<string, int>("", 2),
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4),  //mxcsr
                                            
            new Tuple<string, int>("xmm0l", 8),
            new Tuple<string, int>("xmm0h", 8),

            new Tuple<string, int>("xmm1l", 8),
            new Tuple<string, int>("xmm1h", 8),

            new Tuple<string, int>("xmm2l", 8),
            new Tuple<string, int>("xmm2h", 8),

            new Tuple<string, int>("xmm3l", 8),
            new Tuple<string, int>("xmm3h", 8),

            new Tuple<string, int>("xmm4l", 8),
            new Tuple<string, int>("xmm4h", 8),

            new Tuple<string, int>("xmm5l", 8),
            new Tuple<string, int>("xmm5h", 8),

            new Tuple<string, int>("xmm6l", 8),
            new Tuple<string, int>("xmm6h", 8),

            new Tuple<string, int>("xmm7l", 8),
            new Tuple<string, int>("xmm7h", 8),   

            new Tuple<string, int>("xmm8l", 8),   
            new Tuple<string, int>("xmm8h", 8), 
            
            new Tuple<string, int>("xmm9l", 8),   
            new Tuple<string, int>("xmm9h", 8),   

            new Tuple<string, int>("xmm10l", 8),   
            new Tuple<string, int>("xmm10h", 8),   

            new Tuple<string, int>("xmm11l", 8),   
            new Tuple<string, int>("xmm11h", 8),   

            new Tuple<string, int>("xmm12l", 8),  
            new Tuple<string, int>("xmm12h", 8),  

            new Tuple<string, int>("xmm13l", 8),  
            new Tuple<string, int>("xmm13h", 8),  

            new Tuple<string, int>("xmm14l", 8),  
            new Tuple<string, int>("xmm14h", 8),

            new Tuple<string, int>("xmm15l", 8),
            new Tuple<string, int>("xmm15h", 8),

            new Tuple<string, int>("ymm0ll", 8),   
            new Tuple<string, int>("ymm0hl", 8),
            new Tuple<string, int>("ymm0lh", 8),
            new Tuple<string, int>("ymm0hh", 8),

            new Tuple<string, int>("ymm1ll", 8),
            new Tuple<string, int>("ymm1hl", 8),
            new Tuple<string, int>("ymm1lh", 8),
            new Tuple<string, int>("ymm1hh", 8),

            new Tuple<string, int>("ymm2ll", 8),
            new Tuple<string, int>("ymm2hl", 8),
            new Tuple<string, int>("ymm2lh", 8),
            new Tuple<string, int>("ymm2hh", 8),

            new Tuple<string, int>("ymm3ll", 8),
            new Tuple<string, int>("ymm3hl", 8),
            new Tuple<string, int>("ymm3lh", 8),
            new Tuple<string, int>("ymm3hh", 8),

            new Tuple<string, int>("ymm4ll", 8),
            new Tuple<string, int>("ymm4hl", 8),
            new Tuple<string, int>("ymm4lh", 8),
            new Tuple<string, int>("ymm4hh", 8),

            new Tuple<string, int>("ymm5ll", 8),
            new Tuple<string, int>("ymm5hl", 8),
            new Tuple<string, int>("ymm5lh", 8),
            new Tuple<string, int>("ymm5hh", 8),

            new Tuple<string, int>("ymm6ll", 8),
            new Tuple<string, int>("ymm6hl", 8),
            new Tuple<string, int>("ymm6lh", 8),
            new Tuple<string, int>("ymm6hh", 8),

            new Tuple<string, int>("ymm7ll", 8),
            new Tuple<string, int>("ymm7hl", 8),
            new Tuple<string, int>("ymm7lh", 8),
            new Tuple<string, int>("ymm7hh", 8),

            new Tuple<string, int>("ymm8ll", 8),
            new Tuple<string, int>("ymm8hl", 8),
            new Tuple<string, int>("ymm8lh", 8),
            new Tuple<string, int>("ymm8hh", 8),

            new Tuple<string, int>("ymm9ll", 8),
            new Tuple<string, int>("ymm9hl", 8),
            new Tuple<string, int>("ymm9lh", 8),
            new Tuple<string, int>("ymm9hh", 8),

            new Tuple<string, int>("ymm10ll", 8),
            new Tuple<string, int>("ymm10hl", 8),
            new Tuple<string, int>("ymm10lh", 8),
            new Tuple<string, int>("ymm10hh", 8),

            new Tuple<string, int>("ymm11ll", 8),
            new Tuple<string, int>("ymm11hl", 8),
            new Tuple<string, int>("ymm11lh", 8),
            new Tuple<string, int>("ymm11hh", 8),

            new Tuple<string, int>("ymm12ll", 8),
            new Tuple<string, int>("ymm12hl", 8),
            new Tuple<string, int>("ymm12lh", 8),
            new Tuple<string, int>("ymm12hh", 8),

            new Tuple<string, int>("ymm13ll", 8),
            new Tuple<string, int>("ymm13hl", 8),
            new Tuple<string, int>("ymm13lh", 8),
            new Tuple<string, int>("ymm13hh", 8),

            new Tuple<string, int>("ymm14ll", 8),
            new Tuple<string, int>("ymm14hl", 8),
            new Tuple<string, int>("ymm14lh", 8),
            new Tuple<string, int>("ymm14hh", 8),

            new Tuple<string, int>("ymm15ll", 8),
            new Tuple<string, int>("ymm15hl", 8),
            new Tuple<string, int>("ymm15lh", 8),
            new Tuple<string, int>("ymm15hh", 8),

            new Tuple<string, int>("", 8),        
            new Tuple<string, int>("", 80),       
            new Tuple<string, int>("", 64),       
            new Tuple<string, int>("", 8),        
            new Tuple<string, int>("", 8),        
            new Tuple<string, int>("", 8),        
            new Tuple<string, int>("", 4),
            new Tuple<string, int>("", 4)
        };

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
            
            new Tuple<string, int>("dr0", 8),
            new Tuple<string, int>("dr1", 8),
            new Tuple<string, int>("dr2", 8),
            new Tuple<string, int>("dr3", 8),
            new Tuple<string, int>("dr6", 8),
            new Tuple<string, int>("dr7", 8),

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
    }

    public class TraceData
    {
        public string Filename { get; set; }
        public string Arch { get; set; }
        public string IpReg { get; set; }
        public Dictionary<string, int> Regs { get; set; } // Stores Name -> Index mapping from prefs.X64_REGS
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
        public string comments = "";
        public bool already_swaped = false;
        public bool isBlockStart = false;
        public string block = "";
    }

    public class MemoryAccess
    {
        public ulong Address { get; set; }
        public ulong Value { get; set; }
        public int size { get; set; } // Set by the MemoryHandler
    }

    public static class TraceLoader
    {
        private const string HexPrefix = "0x";
        private const string ChangeSeparator = "; ";
        private const string ChangeArrow = " -> ";
        private const string RegisterValueSeparator = ": ";
        private const string ZeroHexValue = "0";

        public static TraceData OpenX64dbgTrace(string filename)
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
                string arch;
                try
                {
                    using var jsonDoc = JsonDocument.Parse(jsonStr);
                    arch = jsonDoc.RootElement.GetProperty("arch").GetString() ?? "unknown";
                }
                catch (JsonException jsonEx)
                {
                    throw new Exception($"Error parsing JSON header: {jsonEx.Message}", jsonEx);
                }

                List<Tuple<string, int>> regs = REGDUMP.X64_REGS_PARSING;
                string ipReg;
                int pointerSize;
                if (arch == "x64")
                {
                    ipReg = "rip";
                    pointerSize = 8;
                }
                else
                {
                    throw new NotSupportedException($"Architecture '{arch}' not fully supported.");
                }

                Dictionary<string, int> regNameToIndexMap = new Dictionary<string, int>();
                for (int i = 0; i < regs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(regs[i].Item1))
                    {
                        if (!regNameToIndexMap.ContainsKey(regs[i].Item1))
                        {
                            regNameToIndexMap.Add(regs[i].Item1, i);
                        }
                    }
                }
                if (!regNameToIndexMap.ContainsKey(""))
                {
                    regNameToIndexMap.Add("", -1);
                }

                traceData.Arch = arch;
                traceData.IpReg = ipReg;
                traceData.Regs = regNameToIndexMap;
                traceData.PointerSize = pointerSize;
                traceData.Trace = new List<TraceRow>();

                X86DisassembleMode mode = (arch == "x64") ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32;
                using (var dis = CapstoneDisassembler.CreateX86Disassembler(mode))
                {
                    dis.EnableInstructionDetails = true;
                    dis.DisassembleSyntax = DisassembleSyntax.Intel;

                    List<byte[]> regValues = new List<byte[]>();
                    for (int i = 0; i < regs.Count; i++)
                    {
                        int size = regs[i].Item2 > 0 ? regs[i].Item2 : 0;
                        regValues.Add(new byte[size]);
                    }
                    int rowId = 0;


                    int counter = 0;
                    while (fs.Position < fs.Length)
                    {
                        try
                        {
                            byte blockType = br.ReadByte();
                            if (blockType != 0x00)
                                break;
                            byte registerChangesCount = br.ReadByte();
                            byte memoryAccesses = br.ReadByte();
                            byte flagsAndOpcodeSize = br.ReadByte();
                            int threadIdBit = flagsAndOpcodeSize >> 7 & 1;
                            int opcodeSize = flagsAndOpcodeSize & 15;
                            uint threadId = 0;
                            if (threadIdBit > 0)
                            {
                                if (fs.Position + 4 > fs.Length) 
                                    throw new EndOfStreamException("Unexpected EOF reading ThreadId.");
                                threadId = br.ReadUInt32();
                            }
                            if (opcodeSize < 0 || opcodeSize > 15) 
                                throw new InvalidDataException($"Invalid opcode size: {opcodeSize}");
                            if (fs.Position + opcodeSize > fs.Length) 
                                throw new EndOfStreamException("Unexpected EOF reading opcodes.");
                            byte[] opcodes = br.ReadBytes(opcodeSize);


                            if (fs.Position + registerChangesCount > fs.Length) 
                                throw new EndOfStreamException("Unexpected EOF reading relative positions.");
                            List<int> registerChangeRelativePositions = new List<int>();
                            for (int i = 0; i < registerChangesCount; i++) { registerChangeRelativePositions.Add(br.ReadByte()); }

                            long expectedDataBytes = (long)pointerSize * registerChangesCount;
                            if (fs.Position + expectedDataBytes > fs.Length) 
                                throw new EndOfStreamException($"Unexpected EOF reading register data buffer. Need {expectedDataBytes}, have {fs.Length - fs.Position}.");
                            byte[] buffer = br.ReadBytes((int)expectedDataBytes);

                            int currentAbsoluteIndex = -1;
                            using (MemoryStream ms = new MemoryStream(buffer))
                            using (BinaryReader reader = new BinaryReader(ms))
                            {
                                for (int i = 0; i < registerChangesCount; i++)
                                {
                                    int relativeIndex = registerChangeRelativePositions[i];
                                    currentAbsoluteIndex = (currentAbsoluteIndex + 1) + relativeIndex;

                                    if (reader.BaseStream.Position + pointerSize > reader.BaseStream.Length) 
                                        throw new EndOfStreamException("Internal error: reading past register data buffer.");
                                    byte[] dataChunk = reader.ReadBytes(pointerSize);

                                    if (currentAbsoluteIndex >= 0 && currentAbsoluteIndex < regs.Count)
                                    {
                                        int targetRegSize = regs[currentAbsoluteIndex].Item2;
                                        string regName = regs[currentAbsoluteIndex].Item1;

                                        if (string.IsNullOrEmpty(regName)) 
                                            continue;

                                        if (regValues[currentAbsoluteIndex].Length != targetRegSize)
                                        {                                        
                                            continue;
                                        }

                                        if (targetRegSize == pointerSize)
                                        {
                                            regValues[currentAbsoluteIndex] = dataChunk;
                                        }
                                        else if (targetRegSize > pointerSize)
                                        {
                                            Array.Copy(dataChunk, 0, regValues[currentAbsoluteIndex], 0, pointerSize);
                                        }
                                        else
                                        {
                                            Array.Copy(dataChunk, 0, regValues[currentAbsoluteIndex], 0, targetRegSize); 
                                        }
                                    }
                                }
                            }

                            if (fs.Position + memoryAccesses > fs.Length) 
                                throw new EndOfStreamException("Unexpected EOF reading memory flags.");
                            List<byte> memoryAccessFlags = new List<byte>();
                            
                            for (int i = 0; i < memoryAccesses; i++) 
                                memoryAccessFlags.Add(br.ReadByte()); 

                            long memAddrBytes = (long)memoryAccesses * pointerSize;
                            
                            if (fs.Position + memAddrBytes > fs.Length) 
                                throw new EndOfStreamException("Unexpected EOF reading memory addresses.");
                            
                            List<ulong> memoryAccessAddresses = new List<ulong>();                         
                            for (int i = 0; i < memoryAccesses; i++) 
                            { 
                                byte[] d = br.ReadBytes(pointerSize); 
                                memoryAccessAddresses.Add(pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0)); 
                            }

                            long memOldBytes = (long)memoryAccesses * pointerSize;
                            if (fs.Position + memOldBytes > fs.Length) 
                                throw new EndOfStreamException("Unexpected EOF reading old memory data.");
                            
                            List<ulong> memoryAccessOldData = new List<ulong>();
                            for (int i = 0; i < memoryAccesses; i++) 
                            { 
                                byte[] d = br.ReadBytes(pointerSize); 
                                memoryAccessOldData.Add(pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0)); 
                            }

                            List<ulong> memoryAccessNewData = new List<ulong>();
                            int writeCount = memoryAccessFlags.Count(flag => (flag & 1) == 0);
                            long memNewBytes = (long)writeCount * pointerSize;
                            if (fs.Position + memNewBytes > fs.Length)
                                throw new EndOfStreamException($"Unexpected EOF reading new memory data. Need {memNewBytes}, have {fs.Length - fs.Position}.");
                            
                            for (int i = 0; i < memoryAccesses; i++) 
                            { 
                                if ((memoryAccessFlags[i] & 1) == 0) 
                                { 
                                    byte[] d = br.ReadBytes(pointerSize); 
                                    memoryAccessNewData.Add(pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0)); 
                                } 
                            }


                            ulong ip = 0;
                            if (regNameToIndexMap.TryGetValue(ipReg, out int ipIndex) && ipIndex >= 0 && ipIndex < regValues.Count) 
                                ip = BitConverter.ToUInt64(regValues[ipIndex], 0); 

                            string disasm = "";
                            try
                            {
                                var instructions = dis.Disassemble(opcodes, (long)ip);
                                foreach (var instr in instructions)
                                {
                                    disasm = instr.Mnemonic + " ";
                                    if (!string.IsNullOrEmpty(instr.Operand))
                                    {
                                        string[] sliced = Regex.Split(instr.Operand, @"([ ,:\[\]*])");
                                        foreach (string slice in sliced)
                                        {
                                            string newSlice = slice;
                                            if (newSlice == "*")
                                            {
                                                newSlice = " * "; // To fix the missing spacing for mul. Not sure why capstone doesnt do this...
                                            }
                                            else if (Regex.IsMatch(slice, @"^[0-9A-F]$"))
                                            {
                                                newSlice = newSlice.ToUpper();
                                                newSlice = slice.Insert(0, "0x");
                                            }
                                            else if (newSlice.StartsWith("0x"))
                                            {
                                                newSlice = "0x" + newSlice.Substring(2).ToUpper();
                                            }
                                            disasm += newSlice;
                                        }

                                    }
                                }                      
                            }
                            catch (Exception ex) 
                            { 
                                disasm = " disassembly_error"; 
                            }

                            List<MemoryAccess> mems = new List<MemoryAccess>();
                            int newDataCounter = 0;
                            for (int i = 0; i < memoryAccesses; i++)
                            {
                                byte flag = memoryAccessFlags[i];
                                string access = ((flag & 1) == 0) ? "WRITE" : "READ";
                                ulong value = (access == "WRITE") ? memoryAccessNewData[newDataCounter++] : memoryAccessOldData[i];
                                if (disasm.Contains("ymmword")) value &= 0xFFFFFFFFFFFFFFFF;
                                else if (disasm.Contains("xmmword")) value &= 0xFFFFFFFFFFFFFFFF;
                                else if (disasm.Contains("qword")) value &= 0xFFFFFFFFFFFFFFFF;
                                else if (disasm.Contains("dword")) value &= 0xFFFFFFFF;
                                else if (disasm.Contains("word")) value &= 0xFFFF;
                                else if (disasm.Contains("byte")) value &= 0xFF;
                                mems.Add(new MemoryAccess {Address = memoryAccessAddresses[i], Value = value });
                            }


                            List<byte[]> initialClonedValues = new List<byte[]>();
                            List<Tuple<string, int>> initialClonedNames = new List<Tuple<string, int>>();
                            for (int i = 0; i < regs.Count; i++)
                            {
                                if (regValues != null && i < regValues.Count && regs[i] != null && !string.IsNullOrEmpty(regs[i].Item1))
                                {
                                    initialClonedValues.Add((byte[])regValues[i].Clone());
                                    initialClonedNames.Add(regs[i]);
                                }
                            }

                            List<byte[]> processedRegValues = new List<byte[]>();
                            List<Tuple<string, int>> processedNamedRegisters = new List<Tuple<string, int>>();

                            int currentIndex = 0;
                            while (currentIndex < initialClonedNames.Count)
                            {
                                string regName = initialClonedNames[currentIndex].Item1;

                                if (regName.StartsWith("xmm"))
                                {
                                    if (currentIndex + 1 < initialClonedValues.Count)
                                    {
                                        byte[] full_xmm = new byte[16];
                                        Array.Copy(initialClonedValues[currentIndex], 0, full_xmm, 0, 8);
                                        Array.Copy(initialClonedValues[currentIndex + 1], 0, full_xmm, 8, 8);

                                        processedRegValues.Add(full_xmm);
                                        processedNamedRegisters.Add(initialClonedNames[currentIndex]);

                                        currentIndex += 2;
                                    }
                                }
                                else if (regName.StartsWith("ymm"))
                                {
                                    if (currentIndex + 3 < initialClonedValues.Count)
                                    {
                                        byte[] full_ymm = new byte[32];

                                        Array.Copy(initialClonedValues[currentIndex], 0, full_ymm, 0, 8);    
                                        Array.Copy(initialClonedValues[currentIndex + 1], 0, full_ymm, 8, 8);
                                        Array.Copy(initialClonedValues[currentIndex + 2], 0, full_ymm, 16, 8); 
                                        Array.Copy(initialClonedValues[currentIndex + 3], 0, full_ymm, 24, 8); 

                                        processedRegValues.Add(full_ymm);
                                        processedNamedRegisters.Add(initialClonedNames[currentIndex]);

                                        currentIndex += 4;
                                    }
                                }
                                else
                                {
                                    processedRegValues.Add(initialClonedValues[currentIndex]);
                                    processedNamedRegisters.Add(initialClonedNames[currentIndex]);
                                    currentIndex++;
                                }
                            }


                            if(REGDUMP.X64_REGS.Count != processedNamedRegisters.Count)
                            {
                                throw new Exception("Processed registers don't match the final X64_REGS.");
                            }

                            TraceRow traceRow = new TraceRow
                            {
                                Id = rowId,
                                Ip = ip,
                                Disasm = disasm.Trim(),
                                Regs = processedRegValues,
                                Opcodes = BitConverter.ToString(opcodes).Replace("-", ""),
                                Mem = mems,
                                Regchanges = new List<string>()
                            };
                            traceData.Trace.Add(traceRow);
                            rowId++;
                        }
                        catch (EndOfStreamException eofEx)
                        {                          
                            break;
                        }
                        catch (Exception ex)
                        {
                            break;
                        }
                        counter++;
                    }
                }
            }

            if (traceData.Trace.Count > 0)
            {
                var namedRegistersForComparison = REGDUMP.X64_REGS.Where(reg => !string.IsNullOrEmpty(reg.Item1)).ToList();

                for (int i = 0; i < traceData.Trace.Count - 1; i++)
                {
                    TraceRow currentRow = traceData.Trace[i];
                    TraceRow nextRow = traceData.Trace[i + 1];

                    if (currentRow.Regchanges == null) 
                        currentRow.Regchanges = new List<string>();

                    for (int j = 0; j < namedRegistersForComparison.Count; ++j)
                    {
                        string regName = namedRegistersForComparison[j].Item1;

                        if (j < currentRow.Regs.Count && j < nextRow.Regs.Count)
                        {
                            if (!nextRow.Regs[j].SequenceEqual(currentRow.Regs[j]) && regName != traceData.IpReg)
                            {
                                string currentRegHex = ByteArrayToHexString(currentRow.Regs[j]);
                                string nextRegHex = ByteArrayToHexString(nextRow.Regs[j]);

                                currentRow.Regchanges.Add(regName);
                                currentRow.Regchanges.Add(RegisterValueSeparator);
                                currentRow.Regchanges.Add(HexPrefix + currentRegHex);
                                currentRow.Regchanges.Add(ChangeArrow);
                                currentRow.Regchanges.Add(HexPrefix + nextRegHex);
                                currentRow.Regchanges.Add(ChangeSeparator);
                                currentRow.highlights.Add(regName);
                            }
                        }
                    }
                }

                var lastRow = traceData.Trace.Last();
                if (lastRow.Regchanges == null) lastRow.Regchanges = new List<string>();
                if (!lastRow.Regchanges.Any())
                {
                    lastRow.Regchanges.Add("UNTRACED");
                }
            }

            return traceData;
        }


        private static string ByteArrayToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return ZeroHexValue; 
            StringBuilder hexBuilder = new StringBuilder(bytes.Length * 2);
            bool leadingZero = true;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                byte b = bytes[i];
                if (b != 0 || !leadingZero || i == 0)
                {
                    hexBuilder.Append(b.ToString("X2"));
                    leadingZero = false;
                }
            }
            return hexBuilder.Length == 0 ? ZeroHexValue : hexBuilder.ToString();
        }

    }
}
