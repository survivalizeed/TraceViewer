using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TraceViewer.Core.Analysis
{
    internal class GraphHandler
    {
        public static List<KeyValuePair<ulong, List<int>>>? uniqueIPAccesses;
        public static List<(int startIndex, int endIndex)>? blocks;

        public static bool GenerateGraph()
        {
            if (TraceHandler.Trace is null) return false;
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            window.GraphViewClear();

            var traceRows = TraceHandler.Trace.Trace;

            // Build IP occurrence map using TryAdd pattern
            var ipOccurrences = new Dictionary<ulong, List<int>>(capacity: traceRows.Count / 4);
            for (int i = 0; i < traceRows.Count; i++)
            {
                var row = traceRows[i];
                if (!ipOccurrences.TryGetValue(row.Ip, out var list))
                {
                    list = new List<int>(4);
                    ipOccurrences[row.Ip] = list;
                }
                list.Add(row.Id);
            }

            uniqueIPAccesses = ipOccurrences.ToList();

            var sliceLocations = new SortedSet<int>();

            // Detect slice points where execution count changes
            for (int idx = 0; idx < uniqueIPAccesses.Count - 1; idx++)
            {
                var current = uniqueIPAccesses[idx];
                var next = uniqueIPAccesses[idx + 1];
                if (current.Value.Count != next.Value.Count)
                    sliceLocations.Add(idx);
            }

            // Split based on non-sequential execution flow
            for (int i = 0; i < uniqueIPAccesses.Count - 1; i++)
            {
                if (sliceLocations.Contains(i) || sliceLocations.Contains(i + 1))
                    continue;

                var currentEntry = uniqueIPAccesses[i];
                var nextEntry = uniqueIPAccesses[i + 1];

                if (currentEntry.Value.Count == nextEntry.Value.Count)
                {
                    for (int k = 0; k < currentEntry.Value.Count; k++)
                    {
                        if (nextEntry.Value[k] - currentEntry.Value[k] != 1)
                        {
                            sliceLocations.Add(i);
                            break;
                        }
                    }
                }
            }

            // Build blocks from slice points
            blocks = new List<(int, int)>();
            int currentBlockStart = 0;
            var finalSlicePoints = new List<int>(sliceLocations) { uniqueIPAccesses.Count - 1 };
            finalSlicePoints.Sort();

            foreach (int sliceIndex in finalSlicePoints.Distinct())
            {
                if (sliceIndex < currentBlockStart) continue;
                blocks.Add((currentBlockStart, sliceIndex));
                currentBlockStart = sliceIndex + 1;
            }

            blocks = blocks.Where(b => b.startIndex <= b.endIndex).ToList();

            // Map IP → block index
            var ipToBlockIndex = new Dictionary<ulong, int>(uniqueIPAccesses.Count);
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                for (int i = block.startIndex; i <= block.endIndex; i++)
                    ipToBlockIndex[uniqueIPAccesses[i].Key] = blockIndex;
            }

            // Map trace ID → row index (for connection resolution)
            var traceIdToRowIndex = new Dictionary<int, int>(traceRows.Count);
            for (int i = 0; i < traceRows.Count; i++)
                traceIdToRowIndex[traceRows[i].Id] = i;

            // Find connections between blocks
            var connections = new List<(int, int)>();
            for (int currentBlockIndex = 0; currentBlockIndex < blocks.Count; currentBlockIndex++)
            {
                var currentBlock = blocks[currentBlockIndex];
                var lastIpEntry = uniqueIPAccesses[currentBlock.endIndex];

                foreach (int traceId in lastIpEntry.Value)
                {
                    if (traceIdToRowIndex.TryGetValue(traceId, out int currentRowIndex))
                    {
                        int nextRowIndex = currentRowIndex + 1;
                        if (nextRowIndex < traceRows.Count)
                        {
                            ulong nextIp = traceRows[nextRowIndex].Ip;
                            if (ipToBlockIndex.TryGetValue(nextIp, out int targetBlockIndex) && targetBlockIndex != currentBlockIndex)
                                connections.Add((currentBlockIndex, targetBlockIndex));
                        }
                    }
                }
            }

            // Create graph nodes
            var nodes = new List<Node>(blocks.Count);
            int y = 0, x = 0;
            const int horizontalThreshold = 1500;
            const int nodeHeight = 40;
            const int nodeWidth = 110;
            const int horizontalSpacing = 200;
            const int verticalSpacing = 300;

            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                if (x > horizontalThreshold)
                {
                    y += verticalSpacing;
                    x = 0;
                }

                var node = new Node
                {
                    Text = $"Block {i}\r\n{block.endIndex - block.startIndex + 1} instructions",
                    Height = nodeHeight,
                    Width = nodeWidth,
                    X = x,
                    Y = y
                };

                nodes.Add(node);
                window.AddNode(node);
                x += horizontalSpacing;
            }

            foreach (var (from, to) in connections.OrderBy(c => c.Item1).ThenBy(c => c.Item2))
                window.ConnectNodes(nodes[from], nodes[to]);

            if (connections.Count == 0) return false;

            // Build control-flow ordered connections for timeline
            var cfConnections = new List<(int, int)> { connections[0] };
            var nodeIndex = new int[nodes.Count];
            var maxNodeIndex = new int[nodes.Count];

            // Count outgoing connections per node (using indexed access instead of ElementAt)
            for (int j = 0; j < connections.Count; j++)
                maxNodeIndex[connections[j].Item1]++;

            bool addedNew = true;
            while (addedNew)
            {
                addedNew = false;
                var lastConn = cfConnections[^1];
                int fromNode = lastConn.Item2;
                int outgoing = 0;

                for (int j = 0; j < connections.Count; j++)
                {
                    if (connections[j].Item1 == fromNode)
                    {
                        if (outgoing == nodeIndex[fromNode])
                        {
                            cfConnections.Add(connections[j]);
                            if (nodeIndex[fromNode] < maxNodeIndex[fromNode])
                                nodeIndex[fromNode]++;
                            addedNew = true;
                            break;
                        }
                        outgoing++;
                    }
                }
            }

            window.InitializeTimeline(cfConnections);
            return true;
        }

        public static void Clear()
        {
            uniqueIPAccesses?.Clear();
            blocks?.Clear();
        }
    }
}