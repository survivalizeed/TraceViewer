using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TraceViewer.Core;

namespace TraceViewer.Core.Diff
{
    public static class TraceNormalizer
    {
        private static readonly Regex HexAddressRegex = new(@"\b0x[0-9a-fA-F]{6,16}\b", RegexOptions.Compiled);

        /// <summary>
        /// Estimates the primary module base address from the execution trace.
        /// Finds the lowest IP executed in typical user-space image ranges and rounds down to 64KB boundary.
        /// </summary>
        public static ulong EstimateModuleBase(TraceData trace)
        {
            if (trace == null || trace.Trace == null || trace.Trace.Count == 0)
                return 0;

            ulong minIp = ulong.MaxValue;
            int sampleCount = Math.Min(trace.Trace.Count, 5000);

            for (int i = 0; i < sampleCount; i++)
            {
                ulong ip = trace.Trace[i].Ip;
                if (ip > 0x10000 && ip < minIp)
                {
                    minIp = ip;
                }
            }

            if (minIp == ulong.MaxValue)
                return 0;

            // Align down to 64KB boundary (Windows allocation granularity)
            return minIp & ~0xFFFFUL;
        }

        /// <summary>
        /// Computes the Relative Virtual Address (RVA) of an instruction pointer.
        /// </summary>
        public static ulong ToRVA(ulong ip, ulong baseAddress)
        {
            if (baseAddress > 0 && ip >= baseAddress)
                return ip - baseAddress;
            return ip;
        }

        /// <summary>
        /// Normalizes a disassembly string by replacing absolute addresses that fall into the module range
        /// with relative RVA representations, so ASLR shifts do not produce false instruction mismatches.
        /// </summary>
        public static string NormalizeDisassembly(string disasm, ulong baseAddress)
        {
            if (string.IsNullOrWhiteSpace(disasm))
                return "";

            if (baseAddress == 0)
                return disasm.Trim();

            return HexAddressRegex.Replace(disasm, match =>
            {
                if (ulong.TryParse(match.Value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out ulong addr))
                {
                    if (addr >= baseAddress && addr < baseAddress + 0x10000000) // Within 256MB of base
                    {
                        ulong rva = addr - baseAddress;
                        return $"<RVA+0x{rva:X}>";
                    }
                }
                return match.Value;
            }).Trim();
        }

        /// <summary>
        /// Gets the initial stack pointer value (RSP) from the first row of a trace.
        /// </summary>
        public static ulong GetInitialRsp(TraceData trace)
        {
            if (trace?.Trace == null || trace.Trace.Count == 0)
                return 0;

            var firstRow = trace.Trace[0];
            if (trace.Regs.TryGetValue("rsp", out int rspIdx) && rspIdx >= 0 && rspIdx < firstRow.Regs.Count)
            {
                byte[] rspBytes = firstRow.Regs[rspIdx];
                if (rspBytes.Length >= 8)
                    return BitConverter.ToUInt64(rspBytes, 0);
            }
            return 0;
        }

        /// <summary>
        /// Normalizes an RSP value relative to initial RSP.
        /// </summary>
        public static long ToRelativeRsp(ulong currentRsp, ulong initialRsp)
        {
            if (initialRsp == 0)
                return (long)currentRsp;
            return (long)currentRsp - (long)initialRsp;
        }

        /// <summary>
        /// Extracts the instruction mnemonic in lowercase.
        /// </summary>
        public static string ExtractMnemonic(string disasm)
        {
            if (string.IsNullOrEmpty(disasm))
                return "";

            int spaceIdx = disasm.IndexOf(' ');
            return spaceIdx < 0 ? disasm.ToLowerInvariant() : disasm[..spaceIdx].ToLowerInvariant();
        }

        /// <summary>
        /// Checks if an instruction is a conditional branch (jcc).
        /// </summary>
        public static bool IsConditionalBranch(string mnemonic)
        {
            return mnemonic is "je" or "jne" or "jz" or "jnz" or "ja" or "jae" or "jb" or "jbe"
                or "jg" or "jge" or "jl" or "jle" or "js" or "jns" or "jo" or "jno"
                or "jp" or "jnp" or "jcxz" or "jecxz" or "jrcxz" or "loop" or "loope" or "loopne";
        }

        /// <summary>
        /// Checks if an instruction is an unconditional jump or return.
        /// </summary>
        public static bool IsUnconditionalBranch(string mnemonic)
        {
            return mnemonic is "jmp" or "ret" or "retn" or "retf";
        }

        /// <summary>
        /// Checks if an instruction is a call.
        /// </summary>
        public static bool IsCall(string mnemonic)
        {
            return mnemonic is "call";
        }
    }
}
