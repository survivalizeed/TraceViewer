using System;
using System.Collections.Generic;
using System.Linq;
using TraceViewer.Core;

namespace TraceViewer.Core.Diff
{
    public static class BranchDivergenceAnalyzer
    {
        public static BranchDiffReport Analyze(TraceData traceA, TraceData traceB)
        {
            var report = new BranchDiffReport
            {
                TraceA = traceA,
                TraceB = traceB
            };

            if (traceA?.Trace == null || traceA.Trace.Count == 0 ||
                traceB?.Trace == null || traceB.Trace.Count == 0)
            {
                return report;
            }

            ulong baseA = TraceNormalizer.EstimateModuleBase(traceA);
            ulong baseB = TraceNormalizer.EstimateModuleBase(traceB);

            // 1. Code Coverage Similarity (Jaccard index of visited RVAs)
            var rvaSetA = new HashSet<ulong>(traceA.Trace.Select(r => TraceNormalizer.ToRVA(r.Ip, baseA)));
            var rvaSetB = new HashSet<ulong>(traceB.Trace.Select(r => TraceNormalizer.ToRVA(r.Ip, baseB)));

            int commonRvas = rvaSetA.Intersect(rvaSetB).Count();
            int totalRvas = rvaSetA.Union(rvaSetB).Count();
            report.CodeCoverageScore = totalRvas == 0 ? 100.0 : Math.Round((double)commonRvas / totalRvas * 100.0, 1);

            // 2. Identify Branch Divergences
            var divergences = new List<BranchDivergence>();
            int sharedCount = 0;
            int totalBranches = 0;

            int idxA = 0;
            int idxB = 0;
            int countA = traceA.Trace.Count;
            int countB = traceB.Trace.Count;

            while (idxA < countA && idxB < countB)
            {
                var rowA = traceA.Trace[idxA];
                var rowB = traceB.Trace[idxB];

                ulong rvaA = TraceNormalizer.ToRVA(rowA.Ip, baseA);
                ulong rvaB = TraceNormalizer.ToRVA(rowB.Ip, baseB);

                if (rvaA == rvaB)
                {
                    sharedCount++;
                    string mnemA = TraceNormalizer.ExtractMnemonic(rowA.Disasm);

                    if (TraceNormalizer.IsConditionalBranch(mnemA))
                    {
                        totalBranches++;

                        bool isLastA = (idxA + 1 >= countA);
                        bool isLastB = (idxB + 1 >= countB);

                        if (!isLastA && !isLastB)
                        {
                            var nextA = traceA.Trace[idxA + 1];
                            var nextB = traceB.Trace[idxB + 1];

                            int opLenA = Math.Max(1, rowA.Opcodes.Length / 2);
                            int opLenB = Math.Max(1, rowB.Opcodes.Length / 2);

                            bool takenA = (nextA.Ip != rowA.Ip + (ulong)opLenA);
                            bool takenB = (nextB.Ip != rowB.Ip + (ulong)opLenB);

                            ulong targetRvaA = TraceNormalizer.ToRVA(nextA.Ip, baseA);
                            ulong targetRvaB = TraceNormalizer.ToRVA(nextB.Ip, baseB);

                            // Paths diverged at this branch!
                            if (takenA != takenB || (takenA && takenB && targetRvaA != targetRvaB))
                            {
                                var div = new BranchDivergence
                                {
                                    Index = divergences.Count + 1,
                                    RowIdA = rowA.Id,
                                    RowIdB = rowB.Id,
                                    AddressA = rowA.Ip,
                                    AddressB = rowB.Ip,
                                    Rva = rvaA,
                                    Disasm = rowA.Disasm,
                                    ComparisonDisasm = idxA > 0 ? traceA.Trace[idxA - 1].Disasm : "",
                                    TakenA = takenA,
                                    TakenB = takenB,
                                    TargetIpA = nextA.Ip,
                                    TargetIpB = nextB.Ip
                                };

                                // Analyze Flags & Registers cause
                                ExtractConditionCauses(rowA, rowB, div);

                                // Extract immediate context (5 before, branch, 5 after)
                                ExtractContexts(traceA, traceB, idxA, idxB, div);

                                // Find Reconvergence
                                FindReconvergence(traceA, traceB, idxA + 1, idxB + 1, baseA, baseB, div, out int reconvA, out int reconvB);

                                divergences.Add(div);

                                if (div.Reconverged)
                                {
                                    idxA = reconvA;
                                    idxB = reconvB;
                                    continue;
                                }
                            }
                        }
                    }

                    idxA++;
                    idxB++;
                }
                else
                {
                    // Resynchronization attempt within local window (up to 1,500 instructions)
                    bool synced = false;
                    int maxLook = Math.Min(1500, Math.Max(countA - idxA, countB - idxB));

                    for (int look = 1; look < maxLook; look++)
                    {
                        if (idxA + look < countA && TraceNormalizer.ToRVA(traceA.Trace[idxA + look].Ip, baseA) == rvaB)
                        {
                            idxA += look;
                            synced = true;
                            break;
                        }
                        if (idxB + look < countB && TraceNormalizer.ToRVA(traceB.Trace[idxB + look].Ip, baseB) == rvaA)
                        {
                            idxB += look;
                            synced = true;
                            break;
                        }
                    }

                    if (!synced)
                    {
                        idxA++;
                        idxB++;
                    }
                }
            }

            report.DivergentBranches = divergences;
            report.TotalBranchesEvaluated = totalBranches;
            report.SharedInstructionCount = sharedCount;

            double pathScore = (countA + countB) == 0 ? 100.0 : Math.Round((double)(sharedCount * 2) / (countA + countB) * 100.0, 1);
            report.PathSimilarityScore = Math.Clamp(pathScore, 0.0, 100.0);

            return report;
        }

        private static void ExtractConditionCauses(TraceRow rowA, TraceRow rowB, BranchDivergence div)
        {
            var flagDiffs = new List<string>();
            var regDiffs = new List<string>();

            var namedRegs = REGDUMP.X64_REGS;
            int count = Math.Min(namedRegs.Length, Math.Min(rowA.Regs.Count, rowB.Regs.Count));

            for (int i = 0; i < count; i++)
            {
                string regName = namedRegs[i].Name;
                if (string.IsNullOrEmpty(regName) || regName == "rip" || regName == "rsp")
                    continue;

                byte[] bA = rowA.Regs[i];
                byte[] bB = rowB.Regs[i];

                if (!bA.AsSpan().SequenceEqual(bB))
                {
                    if (regName == "rflags" && bA.Length >= 8 && bB.Length >= 8)
                    {
                        ulong fA = BitConverter.ToUInt64(bA, 0);
                        ulong fB = BitConverter.ToUInt64(bB, 0);

                        int zfA = (int)((fA >> 6) & 1);
                        int zfB = (int)((fB >> 6) & 1);
                        if (zfA != zfB) flagDiffs.Add($"ZF: {zfA} vs {zfB}");

                        int cfA = (int)((fA >> 0) & 1);
                        int cfB = (int)((fB >> 0) & 1);
                        if (cfA != cfB) flagDiffs.Add($"CF: {cfA} vs {cfB}");

                        int sfA = (int)((fA >> 7) & 1);
                        int sfB = (int)((fB >> 7) & 1);
                        if (sfA != sfB) flagDiffs.Add($"SF: {sfA} vs {sfB}");

                        int ofA = (int)((fA >> 11) & 1);
                        int ofB = (int)((fB >> 11) & 1);
                        if (ofA != ofB) flagDiffs.Add($"OF: {ofA} vs {ofB}");
                    }
                    else if (regName is "rax" or "rbx" or "rcx" or "rdx" or "rsi" or "rdi" or "r8" or "r9" or "r10")
                    {
                        ulong vA = BitConverter.ToUInt64(bA, 0);
                        ulong vB = BitConverter.ToUInt64(bB, 0);
                        regDiffs.Add($"{regName.ToUpperInvariant()}: 0x{vA:X} vs 0x{vB:X}");
                        div.DifferingRegsList.Add($"{regName.ToUpperInvariant()}: 0x{vA:X} (A) vs 0x{vB:X} (B)");
                    }
                }
            }

            div.FlagCause = flagDiffs.Count > 0 ? string.Join(", ", flagDiffs) : "";
            div.RegisterCause = regDiffs.Count > 0 ? string.Join(", ", regDiffs.Take(2)) : "";
        }

        private static void ExtractContexts(TraceData traceA, TraceData traceB, int idxA, int idxB, BranchDivergence div)
        {
            // Context A: 4 before, branch, 4 after
            int startA = Math.Max(0, idxA - 4);
            int endA = Math.Min(traceA.Trace.Count - 1, idxA + 5);
            for (int i = startA; i <= endA; i++)
            {
                var r = traceA.Trace[i];
                div.ContextA.Add((r.Id, r.Ip, r.Disasm, i == idxA));
            }

            // Context B: 4 before, branch, 4 after
            int startB = Math.Max(0, idxB - 4);
            int endB = Math.Min(traceB.Trace.Count - 1, idxB + 5);
            for (int i = startB; i <= endB; i++)
            {
                var r = traceB.Trace[i];
                div.ContextB.Add((r.Id, r.Ip, r.Disasm, i == idxB));
            }
        }

        private static void FindReconvergence(
            TraceData traceA, TraceData traceB,
            int startA, int startB,
            ulong baseA, ulong baseB,
            BranchDivergence div,
            out int outReconvA, out int outReconvB)
        {
            outReconvA = -1;
            outReconvB = -1;

            int maxScan = 3000;
            int limitA = Math.Min(traceA.Trace.Count, startA + maxScan);
            int limitB = Math.Min(traceB.Trace.Count, startB + maxScan);

            // Build map of RVAs in Trace A
            var rvaMapA = new Dictionary<ulong, int>();
            for (int i = startA; i < limitA; i++)
            {
                ulong rva = TraceNormalizer.ToRVA(traceA.Trace[i].Ip, baseA);
                rvaMapA.TryAdd(rva, i);
            }

            // Find first RVA in Trace B that matches Trace A
            for (int j = startB; j < limitB; j++)
            {
                ulong rva = TraceNormalizer.ToRVA(traceB.Trace[j].Ip, baseB);
                if (rvaMapA.TryGetValue(rva, out int matchA))
                {
                    div.Reconverged = true;
                    div.StepsA = matchA - startA;
                    div.StepsB = j - startB;
                    div.ReconvergeAddress = traceA.Trace[matchA].Ip;
                    outReconvA = matchA;
                    outReconvB = j;
                    return;
                }
            }

            div.Reconverged = false;
        }
    }
}
