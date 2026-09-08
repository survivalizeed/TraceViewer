using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using TraceViewer.Core.Analysis;

namespace TraceViewer.Core.Search
{
    public enum SearchMode
    {
        Smart,
        Text,
        HexBytes,
        ValueOrAddress,
        Disassembly
    }

    public class SearchOptions
    {
        public string Query { get; set; } = "";
        public SearchMode Mode { get; set; } = SearchMode.Smart;
        public bool SearchDisasm { get; set; } = true;
        public bool SearchRegisters { get; set; } = true;
        public bool SearchMemory { get; set; } = true;
        public bool SearchComments { get; set; } = true;
        public bool MatchCase { get; set; } = false;
    }

    public class SearchResultItem
    {
        public int RowId { get; set; }
        public ulong Ip { get; set; }
        public string IpAddressHex => $"0x{Ip:X}";
        public string Category { get; set; } = "";
        public string Details { get; set; } = "";
        public string Disasm { get; set; } = "";
        public string Opcodes { get; set; } = "";
    }

    public static class SearchEngine
    {
        private static bool IsPointerOrSystemRegister(string regName)
        {
            return regName is "RSP" or "RBP" or "RIP" or "RFLAGS" or "FLAGS"
                           or "DR0" or "DR1" or "DR2" or "DR3" or "DR6" or "DR7"
                           or "EFLAGS" or "ESP" or "EBP" or "EIP";
        }

        public static List<SearchResultItem> ExecuteSearch(
            TraceData traceData,
            SearchOptions options,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<SearchResultItem>();
            if (traceData == null || traceData.Trace.Count == 0 || string.IsNullOrWhiteSpace(options.Query))
                return results;

            string query = options.Query.Trim();
            var stringComparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // Prepared parsed representations
            byte?[]? hexPattern = TryParseHexPattern(query);
            ulong? numericValue = TryParseNumericValue(query);
            string? hexSubstring = TryExtractHexSubstring(query);

            bool allowHexSubMatch = hexSubstring != null && (
                hexSubstring.Length >= 3 ||
                (hexSubstring.Length == 2 && (query.Contains("0x", StringComparison.OrdinalIgnoreCase) ||
                                              query.Contains('.') || query.Contains('*') ||
                                              options.Mode == SearchMode.HexBytes ||
                                              options.Mode == SearchMode.ValueOrAddress))
            );

            byte[]? hexBytesBE = (hexSubstring != null && hexSubstring.Length % 2 == 0) ? HexStringToByteArray(hexSubstring) : null;
            byte[]? hexBytesLE = (hexBytesBE != null && hexBytesBE.Length > 1) ? hexBytesBE.Reverse().ToArray() : null;

            byte[] asciiBytes = Encoding.ASCII.GetBytes(query);
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(query);

            int totalRows = traceData.Trace.Count;
            bool hasStacks = MemoryHandler.stacks.Count == totalRows;
            bool hasHeaps = MemoryHandler.heaps.Count == totalRows;

            for (int i = 0; i < totalRows; i++)
            {
                if ((i & 0x3FF) == 0) // Every 1024 rows
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report((int)((long)i * 100 / totalRows));
                }

                var row = traceData.Trace[i];
                var rowMatches = new List<(string Category, string Details)>();

                // 1. DISASSEMBLY & OPCODES
                if (options.SearchDisasm)
                {
                    // Disassembly text search
                    if (options.Mode == SearchMode.Disassembly || options.Mode == SearchMode.Smart || options.Mode == SearchMode.Text)
                    {
                        if (row.Disasm.Contains(query, stringComparison))
                        {
                            rowMatches.Add(("Disasm", $"Instruction: {row.Disasm}"));
                        }
                        else if (hexSubstring != null)
                        {
                            // Also check if disasm contains "0x" + hex (e.g. searching "E1" or "00E1" matches "cmp ..., 0xe1")
                            string hexWith0x = "0x" + hexSubstring;
                            if (row.Disasm.Contains(hexWith0x, StringComparison.OrdinalIgnoreCase))
                            {
                                rowMatches.Add(("Disasm", $"Instruction: {row.Disasm}"));
                            }
                            else if (numericValue.HasValue)
                            {
                                string numHex = "0x" + numericValue.Value.ToString("X");
                                if (row.Disasm.Contains(numHex, StringComparison.OrdinalIgnoreCase))
                                {
                                    rowMatches.Add(("Disasm", $"Instruction: {row.Disasm}"));
                                }
                            }
                        }
                    }

                    // Opcode search only when user explicitly asks for hex bytes
                    if (options.Mode == SearchMode.HexBytes && hexPattern != null && !string.IsNullOrEmpty(row.Opcodes))
                    {
                        byte[] opBytes = HexStringToByteArray(row.Opcodes);
                        if (ContainsPattern(opBytes, hexPattern))
                        {
                            rowMatches.Add(("Opcode", $"Opcode match: {row.Opcodes}"));
                        }
                    }

                    // Exact IP match only when searching specifically for value/address and query is formatted as an address
                    if (options.Mode == SearchMode.ValueOrAddress && numericValue.HasValue && query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (row.Ip == numericValue.Value)
                        {
                            rowMatches.Add(("Address", $"RIP: 0x{row.Ip:X}"));
                        }
                    }
                }

                // 2. REGISTER CHANGES (Matches when a register CHANGES to the target value, e.g. "R8: 0x100 -> 0xE1")
                if (options.SearchRegisters && row.Regchanges != null && row.Regchanges.Count >= 5)
                {
                    for (int r = 0; r + 4 < row.Regchanges.Count; r += 6)
                    {
                        string regName = row.Regchanges[r];
                        if (IsPointerOrSystemRegister(regName.ToUpperInvariant()))
                            continue;

                        string oldValStr = row.Regchanges[r + 2]; // e.g. "0x100"
                        string newValStr = row.Regchanges[r + 4]; // e.g. "0xE1"

                        bool changeMatched = false;

                        // Check if user searched for register name directly (e.g. "rbx" or "rax")
                        if (regName.Equals(query, StringComparison.OrdinalIgnoreCase))
                        {
                            changeMatched = true;
                        }

                        // Numeric value match on the new value (what the register became)
                        if (!changeMatched && numericValue.HasValue)
                        {
                            ulong? newNumeric = TryParseNumericValue(newValStr);
                            if (newNumeric.HasValue && newNumeric.Value == numericValue.Value)
                            {
                                changeMatched = true;
                            }
                        }

                        // Hex substring match on new value
                        if (!changeMatched && allowHexSubMatch && hexSubstring != null)
                        {
                            string cleanNew = newValStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) 
                                ? newValStr[2..] 
                                : newValStr;

                            if (cleanNew.Contains(hexSubstring, StringComparison.OrdinalIgnoreCase))
                            {
                                changeMatched = true;
                            }
                        }

                        // Exact query substring match in newValStr
                        if (!changeMatched && newValStr.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            changeMatched = true;
                        }

                        if (changeMatched)
                        {
                            rowMatches.Add(("Register", $"{regName.ToUpperInvariant()}: {oldValStr} -> {newValStr}"));
                        }
                    }
                }

                // 3. MEMORY VALUES & ACCESSES (Searches VALUES stored in memory, not arbitrary address numbers)
                if (options.SearchMemory)
                {
                    if (row.Mem != null && row.Mem.Count > 0)
                    {
                        for (int m = 0; m < row.Mem.Count; m++)
                        {
                            var mem = row.Mem[m];
                            bool memMatched = false;

                            // Exact address match only if user specifically searched in ValueOrAddress mode with an address prefix
                            if (options.Mode == SearchMode.ValueOrAddress && numericValue.HasValue && query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                            {
                                if (mem.Address == numericValue.Value ||
                                    (numericValue.Value >= mem.Address && numericValue.Value < mem.Address + (ulong)Math.Max(1, mem.size)))
                                {
                                    rowMatches.Add(("Memory Addr", $"[0x{mem.Address:X}] (value: 0x{mem.Value:X})"));
                                    memMatched = true;
                                }
                            }

                            // Memory Value numeric match (exact value match to avoid false pointer matches)
                            if (!memMatched && numericValue.HasValue && (options.Mode == SearchMode.ValueOrAddress || options.Mode == SearchMode.Smart))
                            {
                                if (mem.Value == numericValue.Value)
                                {
                                    rowMatches.Add(("Memory Value", $"[0x{mem.Address:X}] = 0x{mem.Value:X}"));
                                    memMatched = true;
                                }
                            }

                            // Hex substring match on memory VALUE (NOT on memory Address to avoid matching all RBP/RSP offsets)
                            if (!memMatched && allowHexSubMatch && hexSubstring != null &&
                                (options.Mode == SearchMode.ValueOrAddress || options.Mode == SearchMode.Smart || options.Mode == SearchMode.HexBytes))
                            {
                                if (mem.Value.ToString("X").Contains(hexSubstring, StringComparison.OrdinalIgnoreCase))
                                {
                                    rowMatches.Add(("Memory Value", $"[0x{mem.Address:X}] = 0x{mem.Value:X}"));
                                    memMatched = true;
                                }
                            }

                            // Value bytes pattern match
                            byte[] memValBytes = BitConverter.GetBytes(mem.Value);
                            int checkSize = mem.size > 0 && mem.size <= 8 ? mem.size : 8;
                            var slice = memValBytes.AsSpan(0, checkSize).ToArray();

                            if (!memMatched && hexPattern != null && (options.Mode == SearchMode.HexBytes || options.Mode == SearchMode.Smart))
                            {
                                if (ContainsPattern(slice, hexPattern))
                                {
                                    rowMatches.Add(("Memory Bytes", $"[0x{mem.Address:X}] = {FormatBytesHex(slice)}"));
                                    memMatched = true;
                                }
                            }

                            if (!memMatched && hexBytesBE != null && (options.Mode == SearchMode.HexBytes || options.Mode == SearchMode.Smart))
                            {
                                if (ContainsSubarray(slice, hexBytesBE) || (hexBytesLE != null && ContainsSubarray(slice, hexBytesLE)))
                                {
                                    rowMatches.Add(("Memory Value", $"[0x{mem.Address:X}] = 0x{mem.Value:X}"));
                                    memMatched = true;
                                }
                            }

                            if (!memMatched && (options.Mode == SearchMode.Text || options.Mode == SearchMode.Smart) && asciiBytes.Length > 0)
                            {
                                if (ContainsSubarray(slice, asciiBytes))
                                {
                                    rowMatches.Add(("Memory Text", $"[0x{mem.Address:X}] contains '{query}'"));
                                }
                                else if (utf8Bytes.Length > 0 && utf8Bytes.Length != asciiBytes.Length && ContainsSubarray(slice, utf8Bytes))
                                {
                                    rowMatches.Add(("Memory Text", $"[0x{mem.Address:X}] contains '{query}'"));
                                }
                            }
                        }
                    }

                    // Search memory delta for current step (values written to Stack/Heap)
                    if (hasStacks && MemoryHandler.stacks[i].Count > 0)
                    {
                        var stackDelta = MemoryHandler.stacks[i];
                        CheckMemoryDelta(stackDelta, "Stack", i, query, asciiBytes, utf8Bytes, hexPattern, hexBytesBE, hexBytesLE, hexSubstring, allowHexSubMatch, numericValue, options, rowMatches);
                    }

                    if (hasHeaps && MemoryHandler.heaps[i].Count > 0)
                    {
                        var heapDelta = MemoryHandler.heaps[i];
                        CheckMemoryDelta(heapDelta, "Heap", i, query, asciiBytes, utf8Bytes, hexPattern, hexBytesBE, hexBytesLE, hexSubstring, allowHexSubMatch, numericValue, options, rowMatches);
                    }
                }

                // 4. COMMENTS & BLOCKS
                if (options.SearchComments)
                {
                    if (!string.IsNullOrEmpty(row.comments))
                    {
                        if (row.comments.Contains(query, stringComparison))
                        {
                            rowMatches.Add(("Comment", row.comments));
                        }
                    }

                    if (!string.IsNullOrEmpty(row.block))
                    {
                        if (row.block.Contains(query, stringComparison))
                        {
                            rowMatches.Add(("Block", row.block));
                        }
                    }
                }

                // Consolidate row matches
                if (rowMatches.Count > 0)
                {
                    var uniqueMatches = rowMatches.DistinctBy(m => m.Details).ToList();
                    string primaryCategory = uniqueMatches[0].Category;
                    string combinedDetails = string.Join("; ", uniqueMatches.Select(m => m.Details));

                    results.Add(new SearchResultItem
                    {
                        RowId = row.Id,
                        Ip = row.Ip,
                        Category = primaryCategory,
                        Details = combinedDetails,
                        Disasm = row.Disasm,
                        Opcodes = row.Opcodes
                    });
                }
            }

            progress?.Report(100);
            return results;
        }

        private static void CheckMemoryDelta(
            Dictionary<ulong, byte> delta,
            string memoryName,
            int rowId,
            string query,
            byte[] asciiBytes,
            byte[] utf8Bytes,
            byte?[]? hexPattern,
            byte[]? hexBytesBE,
            byte[]? hexBytesLE,
            string? hexSubstring,
            bool allowHexSubMatch,
            ulong? numericValue,
            SearchOptions options,
            List<(string Category, string Details)> rowMatches)
        {
            if (delta.Count == 0) return;

            // Check if delta addresses contain numeric target only in explicit ValueOrAddress mode with address format
            if (options.Mode == SearchMode.ValueOrAddress && numericValue.HasValue && query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (delta.TryGetValue(numericValue.Value, out byte val))
                {
                    rowMatches.Add(($"{memoryName} Delta", $"Target addr 0x{numericValue.Value:X} written: 0x{val:X2}"));
                }
            }

            // Check hex bytes (both big-endian and little-endian)
            if (hexBytesBE != null && (options.Mode == SearchMode.HexBytes || options.Mode == SearchMode.Smart || options.Mode == SearchMode.ValueOrAddress))
            {
                if (ContainsConsecutiveBytes(delta, hexBytesBE))
                {
                    rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: 0x{hexSubstring}"));
                }
                else if (hexBytesLE != null && ContainsConsecutiveBytes(delta, hexBytesLE))
                {
                    rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: 0x{hexSubstring}"));
                }
            }

            // Check wildcard byte pattern
            if (hexPattern != null && (options.Mode == SearchMode.HexBytes || options.Mode == SearchMode.Smart))
            {
                if (ContainsConsecutivePattern(delta, hexPattern))
                {
                    rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: byte pattern"));
                }
            }

            // Check text
            if (options.Mode == SearchMode.Text || options.Mode == SearchMode.Smart)
            {
                if (asciiBytes.Length > 0 && ContainsConsecutiveBytes(delta, asciiBytes))
                {
                    rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: '{query}'"));
                }
                else if (utf8Bytes.Length > 0 && utf8Bytes.Length != asciiBytes.Length && ContainsConsecutiveBytes(delta, utf8Bytes))
                {
                    rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: '{query}'"));
                }
            }

            // Check contiguous delta chunks (matching StackView QWORD display)
            if (allowHexSubMatch && hexSubstring != null && delta.Count >= 1 &&
                (options.Mode == SearchMode.Smart || options.Mode == SearchMode.ValueOrAddress || options.Mode == SearchMode.HexBytes))
            {
                CheckDeltaContiguousChunks(delta, memoryName, hexSubstring, rowMatches);
            }
        }

        private static void CheckDeltaContiguousChunks(
            Dictionary<ulong, byte> delta,
            string memoryName,
            string hexSubstring,
            List<(string Category, string Details)> rowMatches)
        {
            ulong? runStart = null;
            ulong prevKey = 0;
            var runBytes = new List<byte>(16);

            foreach (var kvp in delta.OrderBy(k => k.Key))
            {
                if (!runStart.HasValue)
                {
                    runStart = kvp.Key;
                    prevKey = kvp.Key;
                    runBytes.Add(kvp.Value);
                }
                else if (kvp.Key == prevKey + 1)
                {
                    prevKey = kvp.Key;
                    runBytes.Add(kvp.Value);
                }
                else
                {
                    CheckRun(runStart.Value, runBytes, memoryName, hexSubstring, rowMatches);
                    runStart = kvp.Key;
                    prevKey = kvp.Key;
                    runBytes.Clear();
                    runBytes.Add(kvp.Value);
                }
            }

            if (runStart.HasValue && runBytes.Count > 0)
            {
                CheckRun(runStart.Value, runBytes, memoryName, hexSubstring, rowMatches);
            }
        }

        private static void CheckRun(
            ulong startAddr,
            List<byte> bytes,
            string memoryName,
            string hexSubstring,
            List<(string Category, string Details)> rowMatches)
        {
            if (bytes.Count == 0) return;

            // StackView / big-endian order: highest address byte first
            var sbBE = new StringBuilder(bytes.Count * 2);
            for (int i = bytes.Count - 1; i >= 0; i--)
                sbBE.Append(bytes[i].ToString("X2"));

            if (sbBE.ToString().Contains(hexSubstring, StringComparison.OrdinalIgnoreCase))
            {
                rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: [0x{startAddr:X}] = 0x{sbBE}"));
                return;
            }

            // Little-endian order: lowest address byte first
            var sbLE = new StringBuilder(bytes.Count * 2);
            for (int i = 0; i < bytes.Count; i++)
                sbLE.Append(bytes[i].ToString("X2"));

            if (sbLE.ToString().Contains(hexSubstring, StringComparison.OrdinalIgnoreCase))
            {
                rowMatches.Add(($"{memoryName} Write", $"{memoryName} Write: [0x{startAddr:X}] = 0x{sbLE}"));
            }
        }

        private static bool ContainsConsecutiveBytes(Dictionary<ulong, byte> delta, byte[] target)
        {
            if (target.Length == 0 || delta.Count < target.Length) return false;

            byte first = target[0];
            foreach (var kvp in delta)
            {
                if (kvp.Value == first)
                {
                    bool match = true;
                    for (int j = 1; j < target.Length; j++)
                    {
                        if (!delta.TryGetValue(kvp.Key + (ulong)j, out byte nextByte) || nextByte != target[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
            }
            return false;
        }

        private static bool ContainsConsecutivePattern(Dictionary<ulong, byte> delta, byte?[] pattern)
        {
            if (pattern.Length == 0 || delta.Count < pattern.Length) return false;

            byte? first = pattern[0];
            foreach (var kvp in delta)
            {
                if (!first.HasValue || kvp.Value == first.Value)
                {
                    bool match = true;
                    for (int j = 1; j < pattern.Length; j++)
                    {
                        if (!delta.TryGetValue(kvp.Key + (ulong)j, out byte nextByte))
                        {
                            match = false;
                            break;
                        }
                        if (pattern[j].HasValue && pattern[j]!.Value != nextByte)
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return true;
                }
            }
            return false;
        }

        public static string? TryExtractHexSubstring(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string cleaned = text.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[2..];

            // Remove wildcards, dots, spaces, commas, colons, hyphens, quotes
            cleaned = cleaned.Replace(".", "")
                             .Replace("*", "")
                             .Replace("?", "")
                             .Replace(" ", "")
                             .Replace(",", "")
                             .Replace(";", "")
                             .Replace(":", "")
                             .Replace("-", "")
                             .Replace("_", "")
                             .Replace("'", "")
                             .Replace("\"", "");

            if (cleaned.Length < 2) return null;

            foreach (char c in cleaned)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return null;
            }

            return cleaned.ToUpperInvariant();
        }

        public static byte?[]? TryParseHexPattern(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string cleaned = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
                                 .Replace(",", " ")
                                 .Replace(";", " ")
                                 .Trim();

            string[] tokens = cleaned.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return null;

            // If single token without spaces, e.g. "414243" or "41"
            if (tokens.Length == 1 && tokens[0].Length >= 2 && (tokens[0].Length % 2 == 0))
            {
                string s = tokens[0];
                var list = new List<byte?>();
                for (int i = 0; i < s.Length; i += 2)
                {
                    string pair = s.Substring(i, 2);
                    if (pair == "??" || pair == "**")
                    {
                        list.Add(null);
                    }
                    else if (byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    {
                        list.Add(b);
                    }
                    else
                    {
                        return null;
                    }
                }
                return list.Count > 0 ? list.ToArray() : null;
            }

            var pattern = new List<byte?>();
            foreach (var t in tokens)
            {
                if (t == "?" || t == "??" || t == "*")
                {
                    pattern.Add(null);
                }
                else if (byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    pattern.Add(b);
                }
                else
                {
                    return null;
                }
            }

            return pattern.Count > 0 ? pattern.ToArray() : null;
        }

        public static ulong? TryParseNumericValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string trimmed = text.Trim();

            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hexVal))
                    return hexVal;
            }

            if (trimmed.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                if (ulong.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hexVal))
                    return hexVal;
            }

            if (ulong.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong decVal))
            {
                return decVal;
            }

            if (ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong rawHexVal))
            {
                return rawHexVal;
            }

            return null;
        }

        private static bool ContainsPattern(byte[] source, byte?[] pattern)
        {
            if (source.Length < pattern.Length) return false;

            int limit = source.Length - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    byte? expected = pattern[j];
                    if (expected.HasValue && source[i + j] != expected.Value)
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        private static bool ContainsSubarray(byte[] source, byte[] target)
        {
            if (target.Length == 0 || source.Length < target.Length) return false;

            int limit = source.Length - target.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < target.Length; j++)
                {
                    if (source[i + j] != target[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return [];
            int length = hex.Length / 2;
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                if (byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    bytes[i] = b;
            }
            return bytes;
        }

        private static string FormatBytesHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static string FormatAsciiPreview(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var sb = new StringBuilder(bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
            }
            return sb.ToString();
        }
    }
}
