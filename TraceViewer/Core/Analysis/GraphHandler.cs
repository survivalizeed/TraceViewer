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

        public static List<KeyValuePair<ulong, List<int>>>? orderedIpEntries;
        public static List<(int startIndex, int endIndex)>? blocks;

        public static bool GenerateGraph()
        {
            if (TraceHandler.Trace == null)
                return false;
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

            orderedIpEntries = ipOccurrences.ToList();

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
            

            blocks = new List<(int startIndex, int endIndex)>();

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


            var connections = new List<(int, int)>();

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


            List<Node> nodes = new List<Node>(blocks.Count);
            int y = 0;
            int x = 0;
            const int horizontalThreshold = 1500;
            const int nodeHeight = 40;
            const int nodeWidth = 110;
            const int horizontalSpacing = 200;
            const int verticalSpacing = 300;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                ulong startIp = orderedIpEntries[block.startIndex].Key;
                ulong endIp = orderedIpEntries[block.endIndex].Key;
                int instructionCount = block.endIndex - block.startIndex + 1;

                if (x > horizontalThreshold)
                {
                    y += verticalSpacing;
                    x = 0;
                }

                var node = new Node
                {
                    Text = $"Block {i}\r\n{block.endIndex - block.startIndex} instructions",
                    Height = nodeHeight,
                    Width = nodeWidth,
                    X = x,
                    Y = y
                };

                nodes.Add(node);
                window.AddNode(node);

                x += horizontalSpacing;
            }

            foreach (var connection in connections.OrderBy(c => c.Item1).ThenBy(c => c.Item2))
            {
                window.ConnectNodes(nodes[connection.Item1], nodes[connection.Item2]);
            }


            // Sort the connections by controlflow
            var cf_connections = new List<(int, int)>();

            cf_connections.Add(connections.First());

            var node_index = new List<int>(nodes.Count);
            var max_node_index = new List<int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                node_index.Add(0);
                max_node_index.Add(0);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                for(int j = 0; j < connections.Count; j++)
                {
                    if (connections.ElementAt(j).Item1 == i)
                    {
                        max_node_index[i]++;
                    }
                }
            }


            bool addedNewConnection = true;
            while (addedNewConnection)
            {
                addedNewConnection = false;
                var lastConnection = cf_connections.Last();
                var from_node = lastConnection.Item2;
                int outgoingConnectionCount = 0;

                foreach (var connection in connections)
                {
                    if (connection.Item1 == from_node)
                    {
                        if (outgoingConnectionCount == node_index[from_node])
                        {
                            cf_connections.Add(connection);
                            if(node_index[from_node] < max_node_index[from_node])
                                node_index[from_node]++;
                            addedNewConnection = true;
                            break;
                        }
                        outgoingConnectionCount++;
                    }
                }
            }        

            window.InitializeTimeline(cf_connections);

            return true;
        } 

        public static void Clear()
        {
            orderedIpEntries?.Clear();
            blocks?.Clear();
        }

    }
}