using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;


namespace TraceViewer.Core.Analysis
{
    internal class GraphHandler
    {

        public static HashSet<(int,int)> connections = new HashSet<(int, int)>();

        public static void GenerateGraph()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            var traceRows = TraceHandler.Trace.Trace;
            var ipOccurrences = new Dictionary<ulong, List<int>>();

            for (int i = 0; i < traceRows.Count; i++)
            {
                var row = traceRows[i];
                if (!ipOccurrences.ContainsKey(row.Ip))
                {
                    ipOccurrences[row.Ip] = new List<int>();
                }
                ipOccurrences[row.Ip].Add(row.Id);
            }

            var orderedIpEntries = ipOccurrences.ToList();

            SortedSet<int> slice_locations = new SortedSet<int>();

            foreach (var kvp in orderedIpEntries.Select((Value, Index) => new { Value, Index }))
            {
                var occ = kvp.Value;
                var currentIndex = kvp.Index;

                if (currentIndex < orderedIpEntries.Count - 1)
                {
                    var next = orderedIpEntries[currentIndex + 1];
                    if (occ.Value.Count != next.Value.Count)
                    {
                        slice_locations.Add(currentIndex);
                    }
                }
            }

            // Split based on execution flow
            for (int i = 0; i < orderedIpEntries.Count - 1; i++)
            {
                if (slice_locations.Contains(i) || slice_locations.Contains(i + 1)) 
                    continue;

                var currentEntry = orderedIpEntries[i];
                var nextEntry = orderedIpEntries[i + 1];

                if (currentEntry.Value.Count == nextEntry.Value.Count)
                {
                    for (int k = 0; k < currentEntry.Value.Count; k++)
                    {
                        if (nextEntry.Value[k] - currentEntry.Value[k] != 1)
                        {
                            slice_locations.Add(i);
                            break;
                        }
                    }
                }                
            }
            

            var blocks = new List<(int startIndex, int endIndex)>();

            int currentBlockStartIndex = 0;
            var finalSlicePoints = slice_locations.ToList();
            finalSlicePoints.Add(orderedIpEntries.Count - 1);
            finalSlicePoints.Sort(); 

            foreach (int sliceIndex in finalSlicePoints.Distinct())
            {
                if (sliceIndex < currentBlockStartIndex) 
                    continue; 

                blocks.Add((currentBlockStartIndex, sliceIndex));
                currentBlockStartIndex = sliceIndex + 1;
            }

            blocks = blocks.Where(b => b.startIndex <= b.endIndex).ToList();


            var ipToBlockIndexMap = new Dictionary<ulong, int>();
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                for (int i = block.startIndex; i <= block.endIndex; i++)
                {
                    ipToBlockIndexMap[orderedIpEntries[i].Key] = blockIndex;
                }
            }

            // Likely uneccessary but still here if there is ever a change to the indices of the trace rows
            var traceIdToRowIndexMap = new Dictionary<int, int>(traceRows.Count);
            for (int i = 0; i < traceRows.Count; i++)
            {
                traceIdToRowIndexMap[traceRows[i].Id] = i;
            }


            

            for (int currentBlockIndex = 0; currentBlockIndex < blocks.Count; currentBlockIndex++)
            {
                var currentBlock = blocks[currentBlockIndex];
                int lastEntryIndexInBlock = currentBlock.endIndex;

                var lastIpEntry = orderedIpEntries[lastEntryIndexInBlock];
                List<int> lastIpTraceIds = lastIpEntry.Value;

                foreach (int traceId in lastIpTraceIds)
                {
                    if (traceIdToRowIndexMap.TryGetValue(traceId, out int currentRowIndex))
                    {
                        int nextRowIndex = currentRowIndex + 1;
                        if (nextRowIndex < traceRows.Count)
                        {
                            TraceRow nextTraceRow = traceRows[nextRowIndex];
                            ulong nextIp = nextTraceRow.Ip;
                            if (ipToBlockIndexMap.TryGetValue(nextIp, out int targetBlockIndex))
                                if (targetBlockIndex != currentBlockIndex)
                                    connections.Add((currentBlockIndex, targetBlockIndex));

                        }
                    }
                }
            }


            List<Node> nodes = new List<Node>();
            for (int i = 0; i < blocks.Count; i++)
            {
                Node node = new Node();
                var block = blocks[i];

                ulong startIp = orderedIpEntries[block.startIndex].Key;
                ulong endIp = orderedIpEntries[block.endIndex].Key;
                int instructionCount = block.endIndex - block.startIndex + 1;
                
                node.Text = $"Block {i}\r\n{instructionCount} instructions\r\n0x{startIp:X} - 0x{endIp:X}";
                node.Height = 100;
                node.Width = 300;
                node.X = 600;
                node.Y = i * 100;
                nodes.Add(node);
                window.AddNode(node);
            }

            foreach (var connection in connections.OrderBy(c => c.Item1).ThenBy(c => c.Item2))
            {
                window.ConnectNodes(nodes[connection.Item1], nodes[connection.Item2]);
            }
        } 

    }
}