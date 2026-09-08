using System;
using System.Collections.Generic;
using System.Linq;

namespace TraceViewer.Core.Analysis
{
    public class MemoryHandler
    {
        public static readonly ulong region_size = 0x1000;

        // Delta-based storage: one dictionary per trace row
        public static List<Dictionary<ulong, byte>> stacks = [];
        public static List<Dictionary<ulong, byte>> heaps = [];

        public static Dictionary<ulong, byte> initialStack = [];
        public static Dictionary<ulong, byte> initialHeap = [];

        // Snapshot cache: every N steps we cache the full state for O(N) max replay instead of O(total)
        private const int SnapshotInterval = 256;
        private static Dictionary<int, Dictionary<ulong, byte>> _stackSnapshots = [];
        private static Dictionary<int, Dictionary<ulong, byte>> _heapSnapshots = [];

        public static void Clear()
        {
            stacks.Clear();
            heaps.Clear();
            initialStack.Clear();
            initialHeap.Clear();
            _stackSnapshots.Clear();
            _heapSnapshots.Clear();
        }

        public static void ComposeMemory(TraceData traceData)
        {
            Clear();

            if (traceData.Trace.Count == 0) return;

            ulong init_rsp = BitConverter.ToUInt64(traceData.Trace[0].Regs[4], 0);

            for (int i = 0; i < traceData.Trace.Count; i++)
            {
                var row = traceData.Trace[i];
                var next_row = i + 1 < traceData.Trace.Count ? traceData.Trace[i + 1] : null;

                ulong current_rsp = BitConverter.ToUInt64(row.Regs[4], 0);
                ulong updated_rsp = next_row is not null ? BitConverter.ToUInt64(next_row.Regs[4], 0) : current_rsp;

                var currentStackDelta = new Dictionary<ulong, byte>();
                var currentHeapDelta = new Dictionary<ulong, byte>();

                for (int j = 0; j < row.Mem.Count; j++)
                {
                    var access = row.Mem[j];
                    bool isStackAccess = Math.Abs((long)(access.Address - init_rsp)) < (long)region_size
                                     || Math.Abs((long)(access.Address - updated_rsp)) < (long)region_size;

                    (byte[] bytes, int diff) = GetAccessBytesAndSize(access, row, current_rsp, updated_rsp, isStackAccess);
                    access.size = diff;

                    if (bytes.Length == 0 || diff == 0) continue;

                    var targetDict = isStackAccess ? currentStackDelta : currentHeapDelta;
                    for (int k = 0; k < diff; k++)
                        targetDict[access.Address + (ulong)k] = bytes[k];
                }

                stacks.Add(currentStackDelta);
                heaps.Add(currentHeapDelta);
            }

            // Pre-build snapshots at regular intervals
            BuildSnapshots();
        }

        /// <summary>
        /// Pre-builds cumulative snapshots at regular intervals.
        /// This allows GetMemoryStateAt() to replay at most SnapshotInterval deltas
        /// instead of all deltas from the beginning.
        /// </summary>
        private static void BuildSnapshots()
        {
            _stackSnapshots.Clear();
            _heapSnapshots.Clear();

            var cumulativeStack = new Dictionary<ulong, byte>(initialStack);
            var cumulativeHeap = new Dictionary<ulong, byte>(initialHeap);

            int count = stacks.Count;
            for (int i = 0; i < count; i++)
            {
                foreach (var kvp in stacks[i])
                    cumulativeStack[kvp.Key] = kvp.Value;
                foreach (var kvp in heaps[i])
                    cumulativeHeap[kvp.Key] = kvp.Value;

                if ((i + 1) % SnapshotInterval == 0)
                {
                    _stackSnapshots[i] = new Dictionary<ulong, byte>(cumulativeStack);
                    _heapSnapshots[i] = new Dictionary<ulong, byte>(cumulativeHeap);
                }
            }
        }

        /// <summary>
        /// Returns the full memory state at the given step index.
        /// Uses cached snapshots to avoid replaying all deltas from the start.
        /// Worst case replays SnapshotInterval deltas instead of stepIndex deltas.
        /// </summary>
        public static Dictionary<ulong, byte> GetMemoryStateAt(int stepIndex, bool stack)
        {
            if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));

            var deltas = stack ? stacks : heaps;
            var snapshots = stack ? _stackSnapshots : _heapSnapshots;
            var initial = stack ? initialStack : initialHeap;

            // Find the nearest snapshot at or before stepIndex
            int snapshotIndex = -1;
            Dictionary<ulong, byte>? snapshot = null;

            // Find highest snapshot key <= stepIndex
            int candidate = (stepIndex / SnapshotInterval) * SnapshotInterval - 1;
            while (candidate >= 0)
            {
                if (snapshots.TryGetValue(candidate, out snapshot))
                {
                    snapshotIndex = candidate;
                    break;
                }
                candidate -= SnapshotInterval;
            }

            // Start from snapshot or initial state
            var result = snapshot is not null
                ? new Dictionary<ulong, byte>(snapshot)
                : new Dictionary<ulong, byte>(initial);

            // Replay deltas from snapshot+1 to stepIndex
            int startFrom = snapshotIndex + 1;
            int limit = Math.Min(stepIndex + 1, deltas.Count);

            for (int i = startFrom; i < limit; i++)
            {
                foreach (var kvp in deltas[i])
                    result[kvp.Key] = kvp.Value;
            }

            // Return ordered by descending key (matching original behavior)
            return result.OrderByDescending(pair => pair.Key).ToDictionary();
        }

        private static (byte[] bytes, int diff) GetAccessBytesAndSize(
            MemoryAccess access, TraceRow row,
            ulong current_rsp, ulong updated_rsp, bool isStackAccess)
        {
            int diff = 0;
            byte[] bytes = [];

            if (isStackAccess)
            {
                int rspDiff = (int)Math.Abs((long)(current_rsp - updated_rsp));
                if (rspDiff is 1 or 2 or 4 or 8)
                {
                    diff = rspDiff;
                    bytes = diff switch
                    {
                        1 => [(byte)access.Value],
                        2 => BitConverter.GetBytes((ushort)access.Value),
                        4 => BitConverter.GetBytes((uint)access.Value),
                        8 => BitConverter.GetBytes(access.Value),
                        _ => []
                    };
                    return (bytes, diff);
                }
            }

            string disasm = row.Disasm;
            if (disasm.Contains("ymmword") || disasm.Contains("xmmword") || disasm.Contains("qword"))
            {
                bytes = BitConverter.GetBytes(access.Value); diff = 8;
            }
            else if (disasm.Contains("dword"))
            {
                bytes = BitConverter.GetBytes((uint)access.Value); diff = 4;
            }
            else if (disasm.Contains("word"))
            {
                bytes = BitConverter.GetBytes((ushort)access.Value); diff = 2;
            }
            else if (disasm.Contains("byte"))
            {
                bytes = [(byte)access.Value]; diff = 1;
            }

            return (bytes, diff);
        }
    }
}