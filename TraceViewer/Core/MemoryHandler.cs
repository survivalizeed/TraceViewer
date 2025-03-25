using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core
{

    class MemoryHandler
    {
        // The range +- rsp in which a memory access is considered to be a stack access
        public static readonly ulong region_size = 0x1000;
        public static List<Dictionary<ulong, ulong>> stacks = new List<Dictionary<ulong, ulong>>();
        public static List<Dictionary<ulong, ulong>> heaps = new List<Dictionary<ulong, ulong>>();

        public static void Clear()
        {
            stacks.Clear();
            heaps.Clear();
        }

        public static void ComposeMemory(TraceData traceData)
        {
            // Both will be snapshoted each iteration and stored in stacks/heaps
            Dictionary<ulong, ulong> stack = new Dictionary<ulong, ulong>();
            Dictionary<ulong, ulong> heap = new Dictionary<ulong, ulong>();

            ulong init_rsp = BitConverter.ToUInt64(traceData.Trace[0].Regs[4], 0);
            for (int i = 0; i < traceData.Trace.Count; i++)
            {          
                var row = traceData.Trace[i];
                var next_row = i + 1 < traceData.Trace.Count ? traceData.Trace[i + 1] : null;
                
                if (next_row == null)
                    continue;
                
                ulong current_rsp = BitConverter.ToUInt64(row.Regs[4], 0);
                ulong updated_rsp = BitConverter.ToUInt64(next_row.Regs[4], 0);

                foreach (var access in row.Mem)
                {
                    if (Math.Abs((long)(access.Addr - init_rsp)) < (long)region_size || Math.Abs((long)(access.Addr - updated_rsp)) < (long)region_size)
                    {
                        int diff = (int)Math.Abs((long)((current_rsp - updated_rsp)));
                        byte[] bytes = Array.Empty<byte>();
                        if (diff == 0)
                        {
                            // Is sliced into four qwords
                            if (row.Disasm.Contains("qword") || row.Disasm.Contains("xmmword") || row.Disasm.Contains("ymmword"))
                            {
                                bytes = BitConverter.GetBytes((ulong)access.Value);
                                diff = 8;
                            }
                            else if (row.Disasm.Contains("dword"))
                            {
                                bytes = BitConverter.GetBytes((uint)access.Value);
                                diff = 4;
                            }
                            else if (row.Disasm.Contains("word"))
                            {
                                bytes = BitConverter.GetBytes((ushort)access.Value);
                                diff = 2;
                            }
                            else if (row.Disasm.Contains("byte"))
                            {
                                bytes = new byte[1] { (byte)access.Value };
                                diff = 1;
                            }
                        }
                        else if (diff == 1)
                            bytes = new byte[1] { (byte)access.Value };
                        else if (diff == 2)
                            bytes = BitConverter.GetBytes((ushort)access.Value);
                        else if (diff == 4)
                            bytes = BitConverter.GetBytes((uint)access.Value);
                        else if (diff == 8)
                            bytes = BitConverter.GetBytes((ulong)access.Value);


                        for (int s = 0; s < diff; s++)
                        {
                            stack[access.Addr + (ulong)s] = bytes[s];
                        }
                        
                    }
                    else
                    {
                        int diff = 0;
                        byte[] bytes = Array.Empty<byte>();
                        if (row.Disasm.Contains("qword") || row.Disasm.Contains("xmmword") || row.Disasm.Contains("ymmword"))
                        {
                            bytes = BitConverter.GetBytes((ulong)access.Value);
                            diff = 8;
                        }
                        else if (row.Disasm.Contains("dword"))
                        {
                            bytes = BitConverter.GetBytes((uint)access.Value);
                            diff = 4;
                        }
                        else if (row.Disasm.Contains("word"))
                        {
                            bytes = BitConverter.GetBytes((ushort)access.Value);
                            diff = 2;
                        }
                        else if (row.Disasm.Contains("byte"))
                        {
                            bytes = new byte[1] { (byte)access.Value };
                            diff = 1;
                        }

                        for (int h = 0; h < diff; h++)
                        {
                            heap[access.Addr + (ulong)h] = bytes[h];
                        }
                    }
                }
                
                // Sort in case there is a read which is not in the correct alignment of the previous sets
                stack = stack.OrderByDescending(pair => pair.Key).ToDictionary();
                stacks.Add(new Dictionary<ulong, ulong>(stack));

                heap = heap.OrderByDescending(pair => pair.Key).ToDictionary();
                heaps.Add(new Dictionary<ulong, ulong>(heap));
            }
        }

    }
}
