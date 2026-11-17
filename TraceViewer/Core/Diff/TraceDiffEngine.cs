using System;
using System.Collections.Generic;
using System.Linq;
using TraceViewer.Core;

namespace TraceViewer.Core.Diff
{
    public class TraceDiffEngine
    {
        private class BlockExecution
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public ulong StartRva { get; set; }
            public ulong EndRva { get; set; }
            public int Count => EndIndex - StartIndex + 1;
            public uint Hash { get; set; }
            public string LastMnemonic { get; set; } = "";
            public bool EndsWithBranch { get; set; }
        }

        public static DiffResult Compare(TraceData traceA, TraceData traceB, DiffSettings? settings = null)
        {
            settings ??= new DiffSettings();

            var result = new DiffResult
            {
                TraceA = traceA,
                TraceB = traceB
            };

            if (traceA?.Trace == null || traceA.Trace.Count == 0 ||
                traceB?.Trace == null || traceB.Trace.Count == 0)
            {
                return result;
            }

            ulong baseA = settings.NormalizeASLR ? TraceNormalizer.EstimateModuleBase(traceA) : 0;
            ulong baseB = settings.NormalizeASLR ? TraceNormalizer.EstimateModuleBase(traceB) : 0;

            ulong rsp0A = settings.NormalizeStack ? TraceNormalizer.GetInitialRsp(traceA) : 0;
            ulong rsp0B = settings.NormalizeStack ? TraceNormalizer.GetInitialRsp(traceB) : 0;

            // 1. Partition both traces into basic block executions
            var blocksA = ExtractBlockExecutions(traceA, baseA);
            var blocksB = ExtractBlockExecutions(traceB, baseB);

            // 2. Perform LCS alignment on block executions
            var alignedBlockPairs = AlignBlocksLCS(blocksA, blocksB);

            // 3. Expand block alignment into instruction-level aligned pairs
            var alignedRows = new List<DiffRowPair>(Math.Max(traceA.Trace.Count, traceB.Trace.Count));
            int pairIndex = 0;

            int lastIdxA = 0;
            int lastIdxB = 0;

            foreach (var (blkIdxA, blkIdxB) in alignedBlockPairs)
            {
                int targetStartA = blkIdxA >= 0 ? blocksA[blkIdxA].StartIndex : traceA.Trace.Count;
                int targetStartB = blkIdxB >= 0 ? blocksB[blkIdxB].StartIndex : traceB.Trace.Count;

                // Handle unmatched gap before this matched block pair
                if (targetStartA > lastIdxA || targetStartB > lastIdxB)
                {
                    AlignGapRegion(traceA, traceB, lastIdxA, targetStartA - 1, lastIdxB, targetStartB - 1,
                        baseA, baseB, rsp0A, rsp0B, settings, alignedRows, ref pairIndex);
                }

                // Handle the matched block pair
                if (blkIdxA >= 0 && blkIdxB >= 0)
                {
                    var bA = blocksA[blkIdxA];
                    var bB = blocksB[blkIdxB];

                    // Check if previous row was a divergence that now reconverged
                    bool isReconvergence = alignedRows.Count > 0 && alignedRows[^1].HasDifference;

                    int len = Math.Min(bA.Count, bB.Count);
                    for (int i = 0; i < len; i++)
                    {
                        var rowA = traceA.Trace[bA.StartIndex + i];
                        var rowB = traceB.Trace[bB.StartIndex + i];

                        var pair = CreateRowPair(pairIndex++, rowA, rowB, baseA, baseB, rsp0A, rsp0B, settings);
                        if (i == 0 && isReconvergence && pair.Type == DiffType.Match)
                        {
                            pair.Type = DiffType.ReconvergencePoint;
                            pair.Summary = "Paths reconverge at common block";
                        }
                        alignedRows.Add(pair);
                    }

                    // Excess in block A if any
                    for (int i = len; i < bA.Count; i++)
                    {
                        var rowA = traceA.Trace[bA.StartIndex + i];
                        alignedRows.Add(new DiffRowPair
                        {
                            PairIndex = pairIndex++,
                            RowA = rowA,
                            RowB = null,
                            Type = DiffType.UniqueA,
                            Summary = "Extra instruction in Block A"
                        });
                    }

                    // Excess in block B if any
                    for (int i = len; i < bB.Count; i++)
                    {
                        var rowB = traceB.Trace[bB.StartIndex + i];
                        alignedRows.Add(new DiffRowPair
                        {
                            PairIndex = pairIndex++,
                            RowA = null,
                            RowB = rowB,
                            Type = DiffType.UniqueB,
                            Summary = "Extra instruction in Block B"
                        });
                    }

                    lastIdxA = bA.EndIndex + 1;
                    lastIdxB = bB.EndIndex + 1;
                }
            }

            // Handle trailing instructions
            if (lastIdxA < traceA.Trace.Count || lastIdxB < traceB.Trace.Count)
            {
                AlignGapRegion(traceA, traceB, lastIdxA, traceA.Trace.Count - 1, lastIdxB, traceB.Trace.Count - 1,
                    baseA, baseB, rsp0A, rsp0B, settings, alignedRows, ref pairIndex);
            }

            // 4. Compute statistics, mark divergence points and indices
            result.AlignedRows = alignedRows;
            PostProcessResult(result);

            return result;
        }

        private static List<BlockExecution> ExtractBlockExecutions(TraceData trace, ulong baseAddress)
        {
            var blocks = new List<BlockExecution>();
            var rows = trace.Trace;
            if (rows == null || rows.Count == 0) return blocks;

            int start = 0;
            uint hash = 2166136261;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                string mnem = TraceNormalizer.ExtractMnemonic(row.Disasm);

                // Update FNV-1a hash with mnemonic
                foreach (char c in mnem)
                {
                    hash ^= (byte)c;
                    hash *= 16777619;
                }

                bool isBranch = TraceNormalizer.IsConditionalBranch(mnem) ||
                                TraceNormalizer.IsUnconditionalBranch(mnem) ||
                                TraceNormalizer.IsCall(mnem);

                bool isLast = (i == rows.Count - 1);
                bool nextIsNonSequential = false;

                if (!isLast)
                {
                    int opcodeBytes = (row.Opcodes.Length) / 2;
                    ulong expectedNextIp = row.Ip + (ulong)Math.Max(1, opcodeBytes);
                    if (rows[i + 1].Ip != expectedNextIp)
                    {
                        nextIsNonSequential = true;
                    }
                }

                if (isBranch || nextIsNonSequential || isLast)
                {
                    blocks.Add(new BlockExecution
                    {
                        StartIndex = start,
                        EndIndex = i,
                        StartRva = TraceNormalizer.ToRVA(rows[start].Ip, baseAddress),
                        EndRva = TraceNormalizer.ToRVA(rows[i].Ip, baseAddress),
                        Hash = hash,
                        LastMnemonic = mnem,
                        EndsWithBranch = isBranch
                    });

                    start = i + 1;
                    hash = 2166136261;
                }
            }

            return blocks;
        }

        private static List<(int IdxA, int IdxB)> AlignBlocksLCS(List<BlockExecution> blocksA, List<BlockExecution> blocksB)
        {
            int n = blocksA.Count;
            int m = blocksB.Count;

            // LCS dynamic programming table for blocks
            // Since block count is typically small (< 5,000), a standard DP matrix is very fast
            int[,] dp = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (IsBlockEquivalent(blocksA[i], blocksB[j]))
                    {
                        dp[i + 1, j + 1] = dp[i, j] + 1;
                    }
                    else
                    {
                        dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                }
            }

            // Backtrack to extract matched pairs
            var matches = new List<(int, int)>(dp[n, m]);
            int currI = n;
            int currJ = m;

            while (currI > 0 && currJ > 0)
            {
                if (IsBlockEquivalent(blocksA[currI - 1], blocksB[currJ - 1]))
                {
                    matches.Add((currI - 1, currJ - 1));
                    currI--;
                    currJ--;
                }
                else if (dp[currI - 1, currJ] >= dp[currI, currJ - 1])
                {
                    currI--;
                }
                else
                {
                    currJ--;
                }
            }

            matches.Reverse();
            return matches;
        }

        private static bool IsBlockEquivalent(BlockExecution a, BlockExecution b)
        {
            return a.StartRva == b.StartRva && a.Hash == b.Hash && a.Count == b.Count;
        }

        private static void AlignGapRegion(
            TraceData traceA, TraceData traceB,
            int startA, int endA, int startB, int endB,
            ulong baseA, ulong baseB, ulong rsp0A, ulong rsp0B,
            DiffSettings settings, List<DiffRowPair> outPairs, ref int pairIndex)
        {
            int countA = endA >= startA ? (endA - startA + 1) : 0;
            int countB = endB >= startB ? (endB - startB + 1) : 0;

            if (countA == 0 && countB == 0) return;

            // Check if this gap starts immediately at a divergence point (conditional jump)
            bool isDivergence = false;
            string divergenceReason = "";
            if (outPairs.Count > 0 && outPairs[^1].RowA != null && outPairs[^1].RowB != null)
            {
                string lastMnemA = TraceNormalizer.ExtractMnemonic(outPairs[^1].RowA!.Disasm);
                string lastMnemB = TraceNormalizer.ExtractMnemonic(outPairs[^1].RowB!.Disasm);
                if (TraceNormalizer.IsConditionalBranch(lastMnemA) || TraceNormalizer.IsConditionalBranch(lastMnemB))
                {
                    isDivergence = true;
                    divergenceReason = $"Branch '{lastMnemA.ToUpperInvariant()}' took different paths";
                }
            }

            // For small gap regions (<= 500 rows), use Myers/LCS on instruction level
            if (countA > 0 && countB > 0 && countA <= 500 && countB <= 500)
            {
                int[,] dp = new int[countA + 1, countB + 1];
                for (int i = 0; i < countA; i++)
                {
                    for (int j = 0; j < countB; j++)
                    {
                        var rA = traceA.Trace[startA + i];
                        var rB = traceB.Trace[startB + j];
                        ulong rvaA = TraceNormalizer.ToRVA(rA.Ip, baseA);
                        ulong rvaB = TraceNormalizer.ToRVA(rB.Ip, baseB);

                        if (rvaA == rvaB && rA.Disasm.Equals(rB.Disasm, StringComparison.OrdinalIgnoreCase))
                        {
                            dp[i + 1, j + 1] = dp[i, j] + 1;
                        }
                        else
                        {
                            dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                        }
                    }
                }

                // Backtrack fine-grained gap
                var diffOps = new List<(int? aIdx, int? bIdx)>();
                int cA = countA;
                int cB = countB;
                while (cA > 0 || cB > 0)
                {
                    if (cA > 0 && cB > 0)
                    {
                        var rA = traceA.Trace[startA + cA - 1];
                        var rB = traceB.Trace[startB + cB - 1];
                        ulong rvaA = TraceNormalizer.ToRVA(rA.Ip, baseA);
                        ulong rvaB = TraceNormalizer.ToRVA(rB.Ip, baseB);

                        if (rvaA == rvaB && rA.Disasm.Equals(rB.Disasm, StringComparison.OrdinalIgnoreCase))
                        {
                            diffOps.Add((startA + cA - 1, startB + cB - 1));
                            cA--;
                            cB--;
                            continue;
                        }
                    }

                    if (cB > 0 && (cA == 0 || dp[cA, cB - 1] >= dp[cA - 1, cB]))
                    {
                        diffOps.Add((null, startB + cB - 1));
                        cB--;
                    }
                    else if (cA > 0 && (cB == 0 || dp[cA, cB - 1] < dp[cA - 1, cB]))
                    {
                        diffOps.Add((startA + cA - 1, null));
                        cA--;
                    }
                }

                diffOps.Reverse();

                bool firstInGap = true;
                foreach (var op in diffOps)
                {
                    if (op.aIdx.HasValue && op.bIdx.HasValue)
                    {
                        var rowA = traceA.Trace[op.aIdx.Value];
                        var rowB = traceB.Trace[op.bIdx.Value];
                        outPairs.Add(CreateRowPair(pairIndex++, rowA, rowB, baseA, baseB, rsp0A, rsp0B, settings));
                    }
                    else if (op.aIdx.HasValue)
                    {
                        var rowA = traceA.Trace[op.aIdx.Value];
                        var pair = new DiffRowPair
                        {
                            PairIndex = pairIndex++,
                            RowA = rowA,
                            RowB = null,
                            Type = (firstInGap && isDivergence) ? DiffType.DivergencePoint : DiffType.UniqueA,
                            Summary = (firstInGap && isDivergence) ? divergenceReason : "Trace A specific branch"
                        };
                        outPairs.Add(pair);
                    }
                    else if (op.bIdx.HasValue)
                    {
                        var rowB = traceB.Trace[op.bIdx.Value];
                        var pair = new DiffRowPair
                        {
                            PairIndex = pairIndex++,
                            RowA = null,
                            RowB = rowB,
                            Type = (firstInGap && isDivergence) ? DiffType.DivergencePoint : DiffType.UniqueB,
                            Summary = (firstInGap && isDivergence) ? divergenceReason : "Trace B specific branch"
                        };
                        outPairs.Add(pair);
                    }
                    firstInGap = false;
                }
                return;
            }

            // Fallback for larger gap: emit UniqueA then UniqueB
            bool flagDiv = isDivergence;
            for (int i = 0; i < countA; i++)
            {
                var rowA = traceA.Trace[startA + i];
                outPairs.Add(new DiffRowPair
                {
                    PairIndex = pairIndex++,
                    RowA = rowA,
                    RowB = null,
                    Type = (flagDiv && i == 0) ? DiffType.DivergencePoint : DiffType.UniqueA,
                    Summary = (flagDiv && i == 0) ? divergenceReason : "Trace A specific execution"
                });
            }

            for (int j = 0; j < countB; j++)
            {
                var rowB = traceB.Trace[startB + j];
                outPairs.Add(new DiffRowPair
                {
                    PairIndex = pairIndex++,
                    RowA = null,
                    RowB = rowB,
                    Type = (flagDiv && countA == 0 && j == 0) ? DiffType.DivergencePoint : DiffType.UniqueB,
                    Summary = (flagDiv && countA == 0 && j == 0) ? divergenceReason : "Trace B specific execution"
                });
            }
        }

        private static DiffRowPair CreateRowPair(
            int index, TraceRow rowA, TraceRow rowB,
            ulong baseA, ulong baseB, ulong rsp0A, ulong rsp0B,
            DiffSettings settings)
        {
            var pair = new DiffRowPair
            {
                PairIndex = index,
                RowA = rowA,
                RowB = rowB
            };

            ulong rvaA = TraceNormalizer.ToRVA(rowA.Ip, baseA);
            ulong rvaB = TraceNormalizer.ToRVA(rowB.Ip, baseB);

            string normDisasmA = TraceNormalizer.NormalizeDisassembly(rowA.Disasm, baseA);
            string normDisasmB = TraceNormalizer.NormalizeDisassembly(rowB.Disasm, baseB);

            bool disasmMatches = normDisasmA.Equals(normDisasmB, StringComparison.OrdinalIgnoreCase) ||
                                 rowA.Disasm.Equals(rowB.Disasm, StringComparison.OrdinalIgnoreCase);

            var changedRegs = new List<string>();
            var stateDetails = new List<string>();

            // 1. Compare Registers
            if (settings.CompareRegisters && rowA.Regs != null && rowB.Regs != null)
            {
                var namedRegs = REGDUMP.X64_REGS;
                int count = Math.Min(namedRegs.Length, Math.Min(rowA.Regs.Count, rowB.Regs.Count));

                for (int j = 0; j < count; j++)
                {
                    string regName = namedRegs[j].Name;
                    if (string.IsNullOrEmpty(regName) || regName == "rip")
                        continue;

                    if (settings.IgnoreFlags && regName == "rflags")
                        continue;

                    byte[] bA = rowA.Regs[j];
                    byte[] bB = rowB.Regs[j];

                    if (regName == "rsp" && settings.NormalizeStack)
                    {
                        ulong rawRspA = BitConverter.ToUInt64(bA, 0);
                        ulong rawRspB = BitConverter.ToUInt64(bB, 0);
                        long relA = TraceNormalizer.ToRelativeRsp(rawRspA, rsp0A);
                        long relB = TraceNormalizer.ToRelativeRsp(rawRspB, rsp0B);

                        if (relA != relB)
                        {
                            changedRegs.Add(regName);
                            stateDetails.Add($"RSP: [rel {relA:+#;-#;0}] vs [rel {relB:+#;-#;0}]");
                        }
                        continue;
                    }

                    if (!bA.AsSpan().SequenceEqual(bB))
                    {
                        changedRegs.Add(regName);
                        string hexA = HexUtils.ByteArrayToHexString(bA);
                        string hexB = HexUtils.ByteArrayToHexString(bB);

                        if (regName == "rflags" && bA.Length >= 8 && bB.Length >= 8)
                        {
                            ulong fA = BitConverter.ToUInt64(bA, 0);
                            ulong fB = BitConverter.ToUInt64(bB, 0);
                            string flagDiff = FormatFlagsDiff(fA, fB);
                            stateDetails.Add($"FLAGS: {flagDiff}");
                        }
                        else
                        {
                            stateDetails.Add($"{regName.ToUpperInvariant()}: 0x{hexA} vs 0x{hexB}");
                        }
                    }
                }
            }

            // 2. Compare Memory accesses
            if (settings.CompareMemory)
            {
                int memCountA = rowA.Mem?.Count ?? 0;
                int memCountB = rowB.Mem?.Count ?? 0;

                if (memCountA != memCountB)
                {
                    stateDetails.Add($"MEM ACCESS COUNT: A has {memCountA}, B has {memCountB}");
                }
                else if (memCountA > 0)
                {
                    for (int m = 0; m < memCountA; m++)
                    {
                        var mA = rowA.Mem![m];
                        var mB = rowB.Mem![m];

                        if (mA.Value != mB.Value)
                        {
                            stateDetails.Add($"MEM[0x{mA.Address:X}] VAL: 0x{mA.Value:X} vs 0x{mB.Value:X}");
                        }
                    }
                }
            }

            pair.ChangedRegs = changedRegs;
            pair.StateDetails = stateDetails;

            if (rvaA == rvaB && disasmMatches)
            {
                if (changedRegs.Count > 0 || (settings.CompareMemory && stateDetails.Any(d => d.StartsWith("MEM"))))
                {
                    pair.Type = DiffType.StateDiff;
                    pair.Summary = $"{changedRegs.Count} differing register(s)";
                }
                else
                {
                    pair.Type = DiffType.Match;
                    pair.Summary = "Identical";
                }
            }
            else
            {
                pair.Type = DiffType.StateDiff;
                pair.Summary = $"Disassembly mismatch: '{rowA.Disasm}' vs '{rowB.Disasm}'";
            }

            return pair;
        }

        private static string FormatFlagsDiff(ulong fA, ulong fB)
        {
            // CF=0, ZF=6, SF=7, OF=11
            var flags = new (string name, int bit)[]
            {
                ("ZF", 6), ("CF", 0), ("SF", 7), ("OF", 11)
            };

            var diffs = new List<string>();
            foreach (var (name, bit) in flags)
            {
                int valA = (int)((fA >> bit) & 1);
                int valB = (int)((fB >> bit) & 1);
                if (valA != valB)
                {
                    diffs.Add($"{name}({valA} vs {valB})");
                }
            }

            return diffs.Count > 0 ? string.Join(", ", diffs) : $"0x{fA:X} vs 0x{fB:X}";
        }

        private static void PostProcessResult(DiffResult result)
        {
            var rows = result.AlignedRows;
            result.DivergenceIndices.Clear();
            result.DifferenceIndices.Clear();

            int matches = 0;
            int stateDiffs = 0;
            int divergences = 0;
            int uniqueA = 0;
            int uniqueB = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var pair = rows[i];
                switch (pair.Type)
                {
                    case DiffType.Match:
                        matches++;
                        break;
                    case DiffType.StateDiff:
                        stateDiffs++;
                        result.DifferenceIndices.Add(i);
                        break;
                    case DiffType.DivergencePoint:
                        divergences++;
                        result.DivergenceIndices.Add(i);
                        result.DifferenceIndices.Add(i);
                        if (result.FirstDivergenceIndex == -1)
                        {
                            result.FirstDivergenceIndex = i;
                        }
                        break;
                    case DiffType.UniqueA:
                        uniqueA++;
                        result.DifferenceIndices.Add(i);
                        break;
                    case DiffType.UniqueB:
                        uniqueB++;
                        result.DifferenceIndices.Add(i);
                        break;
                    case DiffType.ReconvergencePoint:
                        // Counted with matches or minor differences
                        matches++;
                        break;
                }
            }

            result.MatchCount = matches;
            result.StateDiffCount = stateDiffs;
            result.DivergenceCount = divergences;
            result.UniqueACount = uniqueA;
            result.UniqueBCount = uniqueB;
        }
    }
}
