using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gee.External.Capstone;
using Gee.External.Capstone.X86;
using System.Text.Json;

namespace TraceViewer.Core
{
    /// <summary>
    /// Describes a single register with its name and byte size.
    /// Replaces the old Tuple&lt;string, int&gt; for clarity and reduced allocations.
    /// </summary>
    public readonly record struct RegisterDef(string Name, int Size)
    {
        public bool IsNamed => !string.IsNullOrEmpty(Name);
    }

    public static class REGDUMP
    {
        public static readonly RegisterDef[] X64_REGS_PARSING =
        [
            new("rax", 8),
            new("rcx", 8),
            new("rdx", 8),
            new("rbx", 8),
            new("rsp", 8),
            new("rbp", 8),
            new("rsi", 8),
            new("rdi", 8),
            new("r8", 8),
            new("r9", 8),
            new("r10", 8),
            new("r11", 8),
            new("r12", 8),
            new("r13", 8),
            new("r14", 8),
            new("r15", 8),
            new("rip", 8),
            new("rflags", 8),
            new("", 2), // segments
            new("", 2),
            new("", 2),
            new("", 2),
            new("", 2),
            new("", 2),
            new("dr0", 8),
            new("dr1", 8),
            new("dr2", 8),
            new("dr3", 8),
            new("dr6", 8),
            new("dr7", 8),
            new("", 80), // register area
            new("", 2),
            new("", 2),
            new("", 2),
            new("", 4),
            new("", 4),
            new("", 4),
            new("", 4),
            new("", 4),
            new("", 4), // mxcsr

            new("xmm0l", 8), new("xmm0h", 8),
            new("xmm1l", 8), new("xmm1h", 8),
            new("xmm2l", 8), new("xmm2h", 8),
            new("xmm3l", 8), new("xmm3h", 8),
            new("xmm4l", 8), new("xmm4h", 8),
            new("xmm5l", 8), new("xmm5h", 8),
            new("xmm6l", 8), new("xmm6h", 8),
            new("xmm7l", 8), new("xmm7h", 8),
            new("xmm8l", 8), new("xmm8h", 8),
            new("xmm9l", 8), new("xmm9h", 8),
            new("xmm10l", 8), new("xmm10h", 8),
            new("xmm11l", 8), new("xmm11h", 8),
            new("xmm12l", 8), new("xmm12h", 8),
            new("xmm13l", 8), new("xmm13h", 8),
            new("xmm14l", 8), new("xmm14h", 8),
            new("xmm15l", 8), new("xmm15h", 8),

            new("ymm0ll", 8), new("ymm0hl", 8), new("ymm0lh", 8), new("ymm0hh", 8),
            new("ymm1ll", 8), new("ymm1hl", 8), new("ymm1lh", 8), new("ymm1hh", 8),
            new("ymm2ll", 8), new("ymm2hl", 8), new("ymm2lh", 8), new("ymm2hh", 8),
            new("ymm3ll", 8), new("ymm3hl", 8), new("ymm3lh", 8), new("ymm3hh", 8),
            new("ymm4ll", 8), new("ymm4hl", 8), new("ymm4lh", 8), new("ymm4hh", 8),
            new("ymm5ll", 8), new("ymm5hl", 8), new("ymm5lh", 8), new("ymm5hh", 8),
            new("ymm6ll", 8), new("ymm6hl", 8), new("ymm6lh", 8), new("ymm6hh", 8),
            new("ymm7ll", 8), new("ymm7hl", 8), new("ymm7lh", 8), new("ymm7hh", 8),
            new("ymm8ll", 8), new("ymm8hl", 8), new("ymm8lh", 8), new("ymm8hh", 8),
            new("ymm9ll", 8), new("ymm9hl", 8), new("ymm9lh", 8), new("ymm9hh", 8),
            new("ymm10ll", 8), new("ymm10hl", 8), new("ymm10lh", 8), new("ymm10hh", 8),
            new("ymm11ll", 8), new("ymm11hl", 8), new("ymm11lh", 8), new("ymm11hh", 8),
            new("ymm12ll", 8), new("ymm12hl", 8), new("ymm12lh", 8), new("ymm12hh", 8),
            new("ymm13ll", 8), new("ymm13hl", 8), new("ymm13lh", 8), new("ymm13hh", 8),
            new("ymm14ll", 8), new("ymm14hl", 8), new("ymm14lh", 8), new("ymm14hh", 8),
            new("ymm15ll", 8), new("ymm15hl", 8), new("ymm15lh", 8), new("ymm15hh", 8),

            new("", 8),
            new("", 80),
            new("", 64),
            new("", 8),
            new("", 8),
            new("", 8),
            new("", 4),
            new("", 4),
        ];

        public static readonly RegisterDef[] X64_REGS =
        [
            new("rax", 8),
            new("rcx", 8),
            new("rdx", 8),
            new("rbx", 8),
            new("rsp", 8),
            new("rbp", 8),
            new("rsi", 8),
            new("rdi", 8),
            new("r8", 8),
            new("r9", 8),
            new("r10", 8),
            new("r11", 8),
            new("r12", 8),
            new("r13", 8),
            new("r14", 8),
            new("r15", 8),
            new("rip", 8),
            new("rflags", 8),

            new("dr0", 8),
            new("dr1", 8),
            new("dr2", 8),
            new("dr3", 8),
            new("dr6", 8),
            new("dr7", 8),

            new("xmm0", 16),
            new("xmm1", 16),
            new("xmm2", 16),
            new("xmm3", 16),
            new("xmm4", 16),
            new("xmm5", 16),
            new("xmm6", 16),
            new("xmm7", 16),
            new("xmm8", 16),
            new("xmm9", 16),
            new("xmm10", 16),
            new("xmm11", 16),
            new("xmm12", 16),
            new("xmm13", 16),
            new("xmm14", 16),
            new("xmm15", 16),

            new("ymm0", 32),
            new("ymm1", 32),
            new("ymm2", 32),
            new("ymm3", 32),
            new("ymm4", 32),
            new("ymm5", 32),
            new("ymm6", 32),
            new("ymm7", 32),
            new("ymm8", 32),
            new("ymm9", 32),
            new("ymm10", 32),
            new("ymm11", 32),
            new("ymm12", 32),
            new("ymm13", 32),
            new("ymm14", 32),
            new("ymm15", 32),
        ];

        /// <summary>Pre-computed name→index mapping for fast register lookup during parsing.</summary>
        public static readonly FrozenDictionary<string, int> RegNameToIndex;

        static REGDUMP()
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < X64_REGS_PARSING.Length; i++)
            {
                if (X64_REGS_PARSING[i].IsNamed && !map.ContainsKey(X64_REGS_PARSING[i].Name))
                    map[X64_REGS_PARSING[i].Name] = i;
            }
            map.TryAdd("", -1);
            RegNameToIndex = map.ToFrozenDictionary();
        }
    }

    public class TraceData
    {
        public string Filename { get; set; } = "";
        public string Arch { get; set; } = "";
        public string IpReg { get; set; } = "";
        public IReadOnlyDictionary<string, int> Regs { get; set; } = new Dictionary<string, int>();
        public int PointerSize { get; set; }
        public List<TraceRow> Trace { get; set; } = [];
    }

    public class TraceRow
    {
        public int Id { get; set; }
        public ulong Ip { get; set; }
        public string Disasm { get; set; } = "";
        public List<byte[]> Regs { get; set; } = [];
        public string Opcodes { get; set; } = "";
        public List<MemoryAccess> Mem { get; set; } = [];
        public List<string> Regchanges { get; set; } = [];
        public List<string> highlights = [];
        public string comments = "";
        public bool alreadySwapped = false;
        public bool isBlockStart = false;
        public string block = "";
    }

    public class MemoryAccess
    {
        public ulong Address { get; set; }
        public ulong Value { get; set; }
        public int size { get; set; } // Set by the MemoryHandler
        public bool IsWrite { get; set; }
    }

    public static class TraceLoader
    {
        private static readonly byte[] TracMagic = "TRAC"u8.ToArray();
        private const string HexPrefix = "0x";
        private const string ChangeSeparator = "; ";
        private const string ChangeArrow = " -> ";
        private const string RegisterValueSeparator = ": ";

        public static TraceData OpenX64dbgTrace(string filename)
        {
            var traceData = new TraceData { Filename = filename };

            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536);
            using var br = new BinaryReader(fs);

            // Validate magic bytes using span comparison
            byte[] magic = br.ReadBytes(4);
            if (!magic.AsSpan().SequenceEqual(TracMagic))
                throw new InvalidDataException("Error, wrong file format.");

            int jsonLength = br.ReadInt32();
            byte[] jsonBlob = br.ReadBytes(jsonLength);
            string arch;
            try
            {
                using var jsonDoc = JsonDocument.Parse(jsonBlob);
                arch = jsonDoc.RootElement.GetProperty("arch").GetString() ?? "unknown";
            }
            catch (JsonException jsonEx)
            {
                throw new InvalidDataException($"Error parsing JSON header: {jsonEx.Message}", jsonEx);
            }

            var regs = REGDUMP.X64_REGS_PARSING;
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

            var regNameToIndexMap = REGDUMP.RegNameToIndex;

            traceData.Arch = arch;
            traceData.IpReg = ipReg;
            traceData.Regs = regNameToIndexMap;
            traceData.PointerSize = pointerSize;

            X86DisassembleMode mode = (arch == "x64") ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32;
            using var dis = CapstoneDisassembler.CreateX86Disassembler(mode);
            dis.EnableInstructionDetails = true;
            dis.DisassembleSyntax = DisassembleSyntax.Intel;

            // Pre-allocate register value buffers (reused and cloned per row)
            var regValues = new byte[regs.Length][];
            for (int i = 0; i < regs.Length; i++)
            {
                int size = regs[i].Size > 0 ? regs[i].Size : 0;
                regValues[i] = new byte[size];
            }

            int rowId = 0;
            var traceRows = new List<TraceRow>(capacity: 4096);

            // Pre-allocate reusable buffers to avoid per-row allocations
            var registerChangePositions = new int[256]; // max 255 register changes

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

                    if (threadIdBit > 0)
                    {
                        if (fs.Position + 4 > fs.Length)
                            throw new EndOfStreamException("Unexpected EOF reading ThreadId.");
                        br.ReadUInt32(); // threadId (currently unused)
                    }

                    if (opcodeSize < 0 || opcodeSize > 15)
                        throw new InvalidDataException($"Invalid opcode size: {opcodeSize}");
                    if (fs.Position + opcodeSize > fs.Length)
                        throw new EndOfStreamException("Unexpected EOF reading opcodes.");
                    byte[] opcodes = br.ReadBytes(opcodeSize);

                    if (fs.Position + registerChangesCount > fs.Length)
                        throw new EndOfStreamException("Unexpected EOF reading relative positions.");
                    for (int i = 0; i < registerChangesCount; i++)
                        registerChangePositions[i] = br.ReadByte();

                    long expectedDataBytes = (long)pointerSize * registerChangesCount;
                    if (fs.Position + expectedDataBytes > fs.Length)
                        throw new EndOfStreamException($"Unexpected EOF reading register data buffer. Need {expectedDataBytes}, have {fs.Length - fs.Position}.");
                    byte[] buffer = br.ReadBytes((int)expectedDataBytes);

                    // Process register changes directly from buffer without MemoryStream/BinaryReader
                    int currentAbsoluteIndex = -1;
                    int bufferOffset = 0;
                    for (int i = 0; i < registerChangesCount; i++)
                    {
                        int relativeIndex = registerChangePositions[i];
                        currentAbsoluteIndex = (currentAbsoluteIndex + 1) + relativeIndex;

                        if (currentAbsoluteIndex >= 0 && currentAbsoluteIndex < regs.Length)
                        {
                            int targetRegSize = regs[currentAbsoluteIndex].Size;
                            string regName = regs[currentAbsoluteIndex].Name;

                            if (!string.IsNullOrEmpty(regName))
                            {
                                if (regValues[currentAbsoluteIndex].Length == targetRegSize)
                                {
                                    int copyLen = Math.Min(pointerSize, targetRegSize);
                                    Buffer.BlockCopy(buffer, bufferOffset, regValues[currentAbsoluteIndex], 0, copyLen);
                                }
                            }
                        }
                        bufferOffset += pointerSize;
                    }

                    // Memory access parsing
                    byte[]? memoryAccessFlags = null;
                    ulong[]? memoryAccessAddresses = null;
                    ulong[]? memoryAccessOldData = null;
                    ulong[]? memoryAccessNewData = null;

                    if (memoryAccesses > 0)
                    {
                        if (fs.Position + memoryAccesses > fs.Length)
                            throw new EndOfStreamException("Unexpected EOF reading memory flags.");

                        memoryAccessFlags = new byte[memoryAccesses];
                        for (int i = 0; i < memoryAccesses; i++)
                            memoryAccessFlags[i] = br.ReadByte();

                        long memAddrBytes = (long)memoryAccesses * pointerSize;
                        if (fs.Position + memAddrBytes > fs.Length)
                            throw new EndOfStreamException("Unexpected EOF reading memory addresses.");

                        memoryAccessAddresses = new ulong[memoryAccesses];
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte[] d = br.ReadBytes(pointerSize);
                            memoryAccessAddresses[i] = pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0);
                        }

                        long memOldBytes = (long)memoryAccesses * pointerSize;
                        if (fs.Position + memOldBytes > fs.Length)
                            throw new EndOfStreamException("Unexpected EOF reading old memory data.");

                        memoryAccessOldData = new ulong[memoryAccesses];
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte[] d = br.ReadBytes(pointerSize);
                            memoryAccessOldData[i] = pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0);
                        }

                        int writeCount = 0;
                        for (int i = 0; i < memoryAccesses; i++)
                            if ((memoryAccessFlags[i] & 1) == 0) writeCount++;

                        long memNewBytes = (long)writeCount * pointerSize;
                        if (fs.Position + memNewBytes > fs.Length)
                            throw new EndOfStreamException($"Unexpected EOF reading new memory data. Need {memNewBytes}, have {fs.Length - fs.Position}.");

                        memoryAccessNewData = new ulong[writeCount];
                        int newIdx = 0;
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            if ((memoryAccessFlags[i] & 1) == 0)
                            {
                                byte[] d = br.ReadBytes(pointerSize);
                                memoryAccessNewData[newIdx++] = pointerSize == 8 ? BitConverter.ToUInt64(d, 0) : BitConverter.ToUInt32(d, 0);
                            }
                        }
                    }

                    // Get IP from register values
                    ulong ip = 0;
                    if (regNameToIndexMap.TryGetValue(ipReg, out int ipIndex) && ipIndex >= 0 && ipIndex < regValues.Length)
                        ip = BitConverter.ToUInt64(regValues[ipIndex], 0);

                    // Disassemble
                    string disasm = "";
                    try
                    {
                        var instructions = dis.Disassemble(opcodes, (long)ip);
                        foreach (var instr in instructions)
                        {
                            disasm = instr.Mnemonic + " ";
                            if (!string.IsNullOrEmpty(instr.Operand))
                            {
                                // Manual split instead of Regex.Split for hot path
                                foreach (ReadOnlySpan<char> slice in SplitOperand(instr.Operand))
                                {
                                    string sliceStr = slice.ToString();
                                    if (sliceStr == "*")
                                    {
                                        disasm += " * ";
                                    }
                                    else if (IsShortHex(sliceStr))
                                    {
                                        disasm += "0x" + sliceStr.ToUpper();
                                    }
                                    else if (sliceStr.StartsWith("0x"))
                                    {
                                        disasm += "0x" + sliceStr[2..].ToUpper();
                                    }
                                    else
                                    {
                                        disasm += sliceStr;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        disasm = " disassembly_error";
                    }

                    // Build memory accesses list
                    var mems = new List<MemoryAccess>(memoryAccesses);
                    if (memoryAccesses > 0 && memoryAccessFlags != null && memoryAccessAddresses != null && memoryAccessOldData != null)
                    {
                        int newDataCounter = 0;
                        for (int i = 0; i < memoryAccesses; i++)
                        {
                            byte flag = memoryAccessFlags[i];
                            bool isWrite = (flag & 1) == 0;
                            ulong value = isWrite && memoryAccessNewData != null ? memoryAccessNewData[newDataCounter++] : memoryAccessOldData[i];
                            value = MaskValueBySize(value, disasm);
                            mems.Add(new MemoryAccess { Address = memoryAccessAddresses[i], Value = value, IsWrite = isWrite });
                        }
                    }

                    // Clone and merge register values for the final row
                    var processedRegValues = new List<byte[]>(REGDUMP.X64_REGS.Length);
                    int currentIndex = 0;

                    // Build list of named-only clones first
                    var namedValues = new List<byte[]>();
                    var namedDefs = new List<RegisterDef>();
                    for (int i = 0; i < regs.Length; i++)
                    {
                        if (regs[i].IsNamed)
                        {
                            namedValues.Add((byte[])regValues[i].Clone());
                            namedDefs.Add(regs[i]);
                        }
                    }

                    // Merge XMM (2x8→16) and YMM (4x8→32)
                    currentIndex = 0;
                    while (currentIndex < namedDefs.Count)
                    {
                        string regName = namedDefs[currentIndex].Name;

                        if (regName.StartsWith("xmm") && currentIndex + 1 < namedValues.Count)
                        {
                            var full = new byte[16];
                            Buffer.BlockCopy(namedValues[currentIndex], 0, full, 0, 8);
                            Buffer.BlockCopy(namedValues[currentIndex + 1], 0, full, 8, 8);
                            processedRegValues.Add(full);
                            currentIndex += 2;
                        }
                        else if (regName.StartsWith("ymm") && currentIndex + 3 < namedValues.Count)
                        {
                            var full = new byte[32];
                            Buffer.BlockCopy(namedValues[currentIndex], 0, full, 0, 8);
                            Buffer.BlockCopy(namedValues[currentIndex + 1], 0, full, 8, 8);
                            Buffer.BlockCopy(namedValues[currentIndex + 2], 0, full, 16, 8);
                            Buffer.BlockCopy(namedValues[currentIndex + 3], 0, full, 24, 8);
                            processedRegValues.Add(full);
                            currentIndex += 4;
                        }
                        else
                        {
                            processedRegValues.Add(namedValues[currentIndex]);
                            currentIndex++;
                        }
                    }

                    if (REGDUMP.X64_REGS.Length != processedRegValues.Count)
                        throw new InvalidDataException("Processed registers don't match the final X64_REGS.");

                    var traceRow = new TraceRow
                    {
                        Id = rowId,
                        Ip = ip,
                        Disasm = disasm.Trim(),
                        Regs = processedRegValues,
                        Opcodes = HexUtils.BytesToHexNoDash(opcodes),
                        Mem = mems,
                        Regchanges = []
                    };
                    traceRows.Add(traceRow);
                    rowId++;
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }

            traceData.Trace = traceRows;

            // Compute register changes between consecutive rows
            if (traceRows.Count > 0)
            {
                var namedRegs = REGDUMP.X64_REGS;

                for (int i = 0; i < traceRows.Count - 1; i++)
                {
                    var currentRow = traceRows[i];
                    var nextRow = traceRows[i + 1];

                    for (int j = 0; j < namedRegs.Length; ++j)
                    {
                        string regName = namedRegs[j].Name;
                        if (j < currentRow.Regs.Count && j < nextRow.Regs.Count)
                        {
                            if (!nextRow.Regs[j].AsSpan().SequenceEqual(currentRow.Regs[j]) && regName != traceData.IpReg)
                            {
                                string currentHex = HexUtils.ByteArrayToHexString(currentRow.Regs[j]);
                                string nextHex = HexUtils.ByteArrayToHexString(nextRow.Regs[j]);

                                currentRow.Regchanges.Add(regName);
                                currentRow.Regchanges.Add(RegisterValueSeparator);
                                currentRow.Regchanges.Add(HexPrefix + currentHex);
                                currentRow.Regchanges.Add(ChangeArrow);
                                currentRow.Regchanges.Add(HexPrefix + nextHex);
                                currentRow.Regchanges.Add(ChangeSeparator);
                                currentRow.highlights.Add(regName);
                            }
                        }
                    }
                }

                var lastRow = traceRows[^1];
                if (lastRow.Regchanges.Count == 0)
                    lastRow.Regchanges.Add("UNTRACED");
            }

            return traceData;
        }

        /// <summary>
        /// Split operand string by delimiters [ ,:\[\]*] without Regex.
        /// Returns each token (including delimiter tokens) as separate strings.
        /// </summary>
        private static List<string> SplitOperand(string operand)
        {
            var parts = new List<string>();
            int start = 0;
            for (int i = 0; i < operand.Length; i++)
            {
                char c = operand[i];
                if (c is ' ' or ',' or ':' or '[' or ']' or '*')
                {
                    if (i > start)
                        parts.Add(operand[start..i]);
                    parts.Add(operand[i..(i + 1)]);
                    start = i + 1;
                }
            }
            if (start < operand.Length)
                parts.Add(operand[start..]);
            return parts;
        }

        /// <summary>Checks if a string is a single hex digit (0-9, A-F).</summary>
        private static bool IsShortHex(string s)
        {
            if (s.Length != 1) return false;
            char c = s[0];
            return c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f');
        }

        /// <summary>Mask memory value by the size indicated in the disassembly.</summary>
        private static ulong MaskValueBySize(ulong value, string disasm)
        {
            // ymmword, xmmword, qword all get full 64-bit (can't represent more in ulong anyway)
            if (disasm.Contains("dword")) return value & 0xFFFFFFFF;
            if (disasm.Contains("word") && !disasm.Contains("mmword") && !disasm.Contains("qword") && !disasm.Contains("dword"))
                return value & 0xFFFF;
            if (disasm.Contains("byte")) return value & 0xFF;
            return value;
        }
    }
}
