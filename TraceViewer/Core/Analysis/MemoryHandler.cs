using TraceViewer.Core;

namespace TraceViewer.Core.Analysis
{
    internal class MemoryHandler
    {
        public static readonly ulong region_size = 0x1000;

        public static List<Dictionary<ulong, byte>> stacks = new List<Dictionary<ulong, byte>>();
        public static List<Dictionary<ulong, byte>> heaps = new List<Dictionary<ulong, byte>>();

        public static Dictionary<ulong, byte> initialStack = new Dictionary<ulong, byte>();
        public static Dictionary<ulong, byte> initialHeap = new Dictionary<ulong, byte>();

        public static void Clear()
        {
            stacks.Clear();
            heaps.Clear();
            initialStack.Clear();
            initialHeap.Clear();
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
                ulong updated_rsp = next_row != null ? BitConverter.ToUInt64(next_row.Regs[4], 0) : current_rsp;

                Dictionary<ulong, byte> currentStackDelta = new Dictionary<ulong, byte>();
                Dictionary<ulong, byte> currentHeapDelta = new Dictionary<ulong, byte>();

                foreach (var access in row.Mem)
                {
                    bool isStackAccess = Math.Abs((long)(access.Addr - init_rsp)) < (long)region_size || Math.Abs((long)(access.Addr - updated_rsp)) < (long)region_size;

                    (byte[] bytes, int diff) = GetAccessBytesAndSize(access, row, current_rsp, updated_rsp, isStackAccess);

                    if (bytes.Length == 0 || diff == 0) continue;

                    var targetDeltaDictionary = isStackAccess ? currentStackDelta : currentHeapDelta;

                    for (int k = 0; k < diff; k++)
                    {
                        targetDeltaDictionary[access.Addr + (ulong)k] = bytes[k];
                    }
                }

                stacks.Add(currentStackDelta);
                heaps.Add(currentHeapDelta);
            }
        }

        public static Dictionary<ulong, byte> GetMemoryStateAt(int stepIndex, bool stack)
        {
            if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));

            var currentStack = new Dictionary<ulong, byte>(initialStack);
            var currentHeap = new Dictionary<ulong, byte>(initialHeap);

            int limit = Math.Min(stepIndex + 1, stacks.Count);
            for (int i = 0; i < limit; i++)
            {
                if (stack)
                {
                    foreach (var kvp in stacks[i])
                    {
                        currentStack[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    foreach (var kvp in heaps[i])
                    {
                        currentHeap[kvp.Key] = kvp.Value;
                    }
                }
            }
            if (stack)
            {
                currentStack = currentStack.OrderByDescending(pair => pair.Key).ToDictionary();
                return currentStack;
            }
            currentHeap = currentHeap.OrderByDescending(pair => pair.Key).ToDictionary();
            return currentHeap;
        }
        private static (byte[] bytes, int diff) GetAccessBytesAndSize(MemoryAccess access, TraceRow row, ulong current_rsp, ulong updated_rsp, bool isStackAccess)
        {
            int diff = 0;
            byte[] bytes = Array.Empty<byte>();

            if (isStackAccess)
            {
                int rsp_diff_val = (int)Math.Abs((long)(current_rsp - updated_rsp));
                if (rsp_diff_val == 1 || rsp_diff_val == 2 || rsp_diff_val == 4 || rsp_diff_val == 8)
                {
                    diff = rsp_diff_val;
                    switch (diff)
                    {
                        case 1: bytes = new byte[1] { (byte)access.Value }; break;
                        case 2: bytes = BitConverter.GetBytes((ushort)access.Value); break;
                        case 4: bytes = BitConverter.GetBytes((uint)access.Value); break;
                        case 8: bytes = BitConverter.GetBytes(access.Value); break;
                    }
                    return (bytes, diff);
                }
            }

            if (row.Disasm.Contains("ymmword"))
            {
                bytes = BitConverter.GetBytes(access.Value); diff = 8;
            }
            else if (row.Disasm.Contains("xmmword"))
            {
                bytes = BitConverter.GetBytes(access.Value); diff = 8;
            }
            else if (row.Disasm.Contains("qword"))
            {
                bytes = BitConverter.GetBytes(access.Value); diff = 8;
            }
            else if (row.Disasm.Contains("dword"))
            {
                bytes = BitConverter.GetBytes((uint)access.Value); diff = 4;
            }
            else if (row.Disasm.Contains("word"))
            {
                bytes = BitConverter.GetBytes((ushort)access.Value); diff = 2;
            }
            else if (row.Disasm.Contains("byte"))
            {
                bytes = new byte[1] { (byte)access.Value }; diff = 1;
            }

            return (bytes, diff);
        }

    }
}