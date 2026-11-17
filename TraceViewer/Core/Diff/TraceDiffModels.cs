using System;
using System.Collections.Generic;
using TraceViewer.Core;

namespace TraceViewer.Core.Diff
{
    public enum DiffType
    {
        Match,              // Identical instruction and matching register/memory state
        StateDiff,          // Identical instruction, but register or memory values differ
        DivergencePoint,    // Branch where execution paths split (e.g. conditional jump taken vs fallthrough)
        ReconvergencePoint, // Point where two diverging paths rejoin
        UniqueA,            // Instruction only executed in Trace A (gap in Trace B)
        UniqueB             // Instruction only executed in Trace B (gap in Trace A)
    }

    public enum DiffFilterMode
    {
        AllRows,
        DifferencesOnly,
        DivergencesOnly,
        StateDiffsOnly
    }

    public class DiffSettings
    {
        public bool NormalizeASLR { get; set; } = true;
        public bool NormalizeStack { get; set; } = true;
        public bool CompareRegisters { get; set; } = true;
        public bool CompareMemory { get; set; } = true;
        public bool IgnoreFlags { get; set; } = false;
        public bool DetectLoops { get; set; } = true;
    }

    public class DiffRowPair
    {
        public int PairIndex { get; set; }
        public TraceRow? RowA { get; set; }
        public TraceRow? RowB { get; set; }
        public DiffType Type { get; set; }

        public List<string> ChangedRegs { get; set; } = [];
        public List<string> StateDetails { get; set; } = [];
        public string Summary { get; set; } = "";

        public bool IsDivergence => Type == DiffType.DivergencePoint;
        public bool IsReconvergence => Type == DiffType.ReconvergencePoint;
        public bool HasDifference => Type != DiffType.Match;

        public bool IsGapA => RowA == null;
        public bool IsGapB => RowB == null;

        public int DisplayIdA => RowA?.Id ?? -1;
        public int DisplayIdB => RowB?.Id ?? -1;

        public ulong DisplayIpA => RowA?.Ip ?? 0;
        public ulong DisplayIpB => RowB?.Ip ?? 0;

        public string DisasmA => RowA?.Disasm ?? "[GAP]";
        public string DisasmB => RowB?.Disasm ?? "[GAP]";
    }

    public class DiffResult
    {
        public TraceData TraceA { get; set; } = new();
        public TraceData TraceB { get; set; } = new();
        public List<DiffRowPair> AlignedRows { get; set; } = [];

        public int TotalRowsA => TraceA.Trace?.Count ?? 0;
        public int TotalRowsB => TraceB.Trace?.Count ?? 0;

        public int MatchCount { get; set; }
        public int StateDiffCount { get; set; }
        public int DivergenceCount { get; set; }
        public int UniqueACount { get; set; }
        public int UniqueBCount { get; set; }

        public int FirstDivergenceIndex { get; set; } = -1;
        public List<int> DivergenceIndices { get; set; } = [];
        public List<int> DifferenceIndices { get; set; } = [];

        public double MatchPercentage => AlignedRows.Count == 0 
            ? 100.0 
            : Math.Round((double)MatchCount / AlignedRows.Count * 100.0, 1);
    }

    public class DifferentialSliceResult
    {
        public bool Success { get; set; }
        public string TargetRegisterOrCondition { get; set; } = "";
        public int RowIdA { get; set; }
        public int RowIdB { get; set; }

        public HashSet<int> SliceRowIdsA { get; set; } = [];
        public HashSet<int> SliceRowIdsB { get; set; } = [];

        public List<TraceRow> SliceRowsA { get; set; } = [];
        public List<TraceRow> SliceRowsB { get; set; } = [];

        public List<TraceRow> SharedSliceRowsA { get; set; } = [];
        public List<TraceRow> UniqueSliceRowsA { get; set; } = [];
        public List<TraceRow> UniqueSliceRowsB { get; set; } = [];

        public string RootCauseDescription { get; set; } = "";
        public string TargetDescriptionA { get; set; } = "";
        public string TargetDescriptionB { get; set; } = "";
    }

    public class BranchDivergence
    {
        public int Index { get; set; }
        public int RowIdA { get; set; }
        public int RowIdB { get; set; }
        public ulong AddressA { get; set; }
        public ulong AddressB { get; set; }
        public ulong Rva { get; set; }
        public string Disasm { get; set; } = "";
        public string ComparisonDisasm { get; set; } = "";

        public bool TakenA { get; set; }
        public bool TakenB { get; set; }
        public ulong TargetIpA { get; set; }
        public ulong TargetIpB { get; set; }

        public string DecisionA => TakenA ? $"JUMP ➔ 0x{TargetIpA:X}" : "FALLTHROUGH";
        public string DecisionB => TakenB ? $"JUMP ➔ 0x{TargetIpB:X}" : "FALLTHROUGH";

        public string FlagCause { get; set; } = "";
        public string RegisterCause { get; set; } = "";
        public string SummaryCause => string.IsNullOrEmpty(RegisterCause)
            ? FlagCause
            : (string.IsNullOrEmpty(FlagCause) ? RegisterCause : $"{FlagCause} | {RegisterCause}");

        public bool Reconverged { get; set; }
        public int StepsA { get; set; }
        public int StepsB { get; set; }
        public ulong ReconvergeAddress { get; set; }
        public string ReconvergenceSummary => Reconverged
            ? $"Reconverged (+{StepsA} / +{StepsB} ops ➔ 0x{ReconvergeAddress:X})"
            : "Terminal Divergence (No Reconvergence)";

        public List<(int RowId, ulong Ip, string Disasm, bool IsBranch)> ContextA { get; set; } = [];
        public List<(int RowId, ulong Ip, string Disasm, bool IsBranch)> ContextB { get; set; } = [];
        public List<string> DifferingRegsList { get; set; } = [];
    }

    public class BranchDiffReport
    {
        public TraceData TraceA { get; set; } = new();
        public TraceData TraceB { get; set; } = new();

        public double PathSimilarityScore { get; set; }
        public double CodeCoverageScore { get; set; }

        public List<BranchDivergence> DivergentBranches { get; set; } = [];
        public int TotalBranchesEvaluated { get; set; }
        public int TotalInstructionsA => TraceA.Trace?.Count ?? 0;
        public int TotalInstructionsB => TraceB.Trace?.Count ?? 0;
        public int SharedInstructionCount { get; set; }
    }
}
