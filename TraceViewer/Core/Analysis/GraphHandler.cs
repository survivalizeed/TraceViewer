using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TraceViewer.Core.Analysis
{
    public enum EdgeType
    {
        Normal,
        ConditionalTrue,
        ConditionalFalse,
        Call,
        Return
    }

    public class BasicBlock
    {
        public int Id { get; set; }
        public ulong StartIp { get; set; }
        public ulong EndIp { get; set; }
        public string Title { get; set; } = "";
        public int InstructionCount { get; set; }
        public int ExecutionCount { get; set; }
        public int FirstTraceRowId { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 150;
        public double Height { get; set; } = 52;
        public int Layer { get; set; } = -1;
    }

    public class BlockConnection
    {
        public BasicBlock From { get; set; }
        public BasicBlock To { get; set; }
        public EdgeType Type { get; set; }
        public int ExecutionCount { get; set; }

        public BlockConnection(BasicBlock from, BasicBlock to, EdgeType type = EdgeType.Normal, int count = 1)
        {
            From = from;
            To = to;
            Type = type;
            ExecutionCount = count;
        }
    }

    internal class GraphHandler
    {
        public static List<KeyValuePair<ulong, List<int>>>? uniqueIPAccesses;
        public static List<(int startIndex, int endIndex)>? blocks;
        public static List<BasicBlock>? GraphBlocks;
        public static List<BlockConnection>? GraphConnections;

        public static bool GenerateGraph()
        {
            if (TraceHandler.Trace is null || TraceHandler.Trace.Trace.Count == 0) return false;
            var window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            var (blockList, connectionList, uniqueIPs, traceRowToBlockId) = ExtractCFG(TraceHandler.Trace.Trace);

            uniqueIPAccesses = uniqueIPs;
            GraphBlocks = blockList;
            GraphConnections = connectionList;

            // Render graph in window
            window.RenderGraph(blockList, connectionList);

            // Initialize timeline with sequential block transitions
            var timelineConns = new List<(int, int)>();
            for (int i = 0; i < traceRowToBlockId.Length - 1; i++)
            {
                int f = traceRowToBlockId[i];
                int t = traceRowToBlockId[i + 1];
                if (f != t && (timelineConns.Count == 0 || timelineConns[^1] != (f, t)))
                {
                    timelineConns.Add((f, t));
                }
            }

            if (timelineConns.Count > 0)
            {
                window.InitializeTimeline(timelineConns);
            }

            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.FitToView();
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            return true;
        }

        public static (List<BasicBlock> Blocks, List<BlockConnection> Connections, List<KeyValuePair<ulong, List<int>>> UniqueIPs, int[] RowToBlockId) ExtractCFG(List<TraceRow> traceRows)
        {
            if (traceRows == null || traceRows.Count == 0)
                return ([], [], [], []);

            // 1. Build IP occurrence map in trace order
            var ipOccurrences = new Dictionary<ulong, List<int>>(capacity: Math.Max(16, traceRows.Count / 4));
            for (int k = 0; k < traceRows.Count; k++)
            {
                var row = traceRows[k];
                if (!ipOccurrences.TryGetValue(row.Ip, out var list))
                {
                    list = new List<int>(4);
                    ipOccurrences[row.Ip] = list;
                }
                list.Add(row.Id);
            }
            var uniqueIPs = ipOccurrences.ToList();
            uniqueIPAccesses = uniqueIPs;

            // 2. Detect slice points (execution count changes or non-sequential jumps)
            var sliceLocations = new SortedSet<int>();

            for (int idx = 0; idx < uniqueIPs.Count - 1; idx++)
            {
                var current = uniqueIPs[idx];
                var next = uniqueIPs[idx + 1];
                if (current.Value.Count != next.Value.Count)
                {
                    sliceLocations.Add(idx);
                }
            }

            for (int idx = 0; idx < uniqueIPs.Count - 1; idx++)
            {
                if (sliceLocations.Contains(idx) || sliceLocations.Contains(idx + 1))
                    continue;

                var currentEntry = uniqueIPs[idx];
                var nextEntry = uniqueIPs[idx + 1];

                if (currentEntry.Value.Count == nextEntry.Value.Count)
                {
                    for (int k = 0; k < currentEntry.Value.Count; k++)
                    {
                        if (nextEntry.Value[k] - currentEntry.Value[k] != 1)
                        {
                            sliceLocations.Add(idx);
                            break;
                        }
                    }
                }
            }

            // 3. Build blocks from slice points
            var computedBlocks = new List<(int startIndex, int endIndex)>();
            int currentBlockStart = 0;
            var finalSlicePoints = new List<int>(sliceLocations) { uniqueIPs.Count - 1 };
            finalSlicePoints.Sort();

            foreach (int sliceIndex in finalSlicePoints.Distinct())
            {
                if (sliceIndex < currentBlockStart) continue;
                computedBlocks.Add((currentBlockStart, sliceIndex));
                currentBlockStart = sliceIndex + 1;
            }
            computedBlocks = computedBlocks.Where(b => b.startIndex <= b.endIndex).ToList();
            blocks = computedBlocks;

            // 4. Map IP -> block index
            var ipToBlockIndex = new Dictionary<ulong, int>(uniqueIPs.Count);
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                for (int idx = block.startIndex; idx <= block.endIndex; idx++)
                {
                    ipToBlockIndex[uniqueIPs[idx].Key] = blockIndex;
                }
            }

            // Map trace rows to block IDs
            var traceRowToBlockId = new int[traceRows.Count];
            for (int k = 0; k < traceRows.Count; k++)
            {
                traceRowToBlockId[k] = ipToBlockIndex.TryGetValue(traceRows[k].Ip, out int bId) ? bId : 0;
            }

            // 5. Build compact BasicBlock models
            var blockList = new List<BasicBlock>(blocks.Count);
            for (int bIdx = 0; bIdx < blocks.Count; bIdx++)
            {
                var (sIdx, eIdx) = blocks[bIdx];
                ulong sIp = uniqueIPs[sIdx].Key;
                ulong eIp = uniqueIPs[eIdx].Key;
                int execCount = uniqueIPs[sIdx].Value.Count;
                int firstRowId = uniqueIPs[sIdx].Value[0];
                int instrCount = eIdx - sIdx + 1;

                var basicBlock = new BasicBlock
                {
                    Id = bIdx,
                    StartIp = sIp,
                    EndIp = eIp,
                    Title = $"Block {bIdx}",
                    InstructionCount = instrCount,
                    ExecutionCount = execCount,
                    FirstTraceRowId = firstRowId
                };

                blockList.Add(basicBlock);
            }

            // 6. Record directed connections between blocks
            var edgeMap = new Dictionary<(int from, int to), int>();
            for (int k = 0; k < traceRows.Count - 1; k++)
            {
                int from = traceRowToBlockId[k];
                int to = traceRowToBlockId[k + 1];
                if (from != to)
                {
                    var key = (from, to);
                    edgeMap[key] = edgeMap.GetValueOrDefault(key, 0) + 1;
                }
            }

            var connectionList = new List<BlockConnection>();
            foreach (var kvp in edgeMap)
            {
                if (kvp.Key.from < blockList.Count && kvp.Key.to < blockList.Count)
                {
                    connectionList.Add(new BlockConnection(
                        blockList[kvp.Key.from],
                        blockList[kvp.Key.to],
                        EdgeType.Normal,
                        kvp.Value));
                }
            }

            // 7. Compute Flow Layout
            ComputeHierarchicalLayout(blockList, connectionList);

            return (blockList, connectionList, uniqueIPs, traceRowToBlockId);
        }

        public static void ComputeHierarchicalLayout(List<BasicBlock> blocks, List<BlockConnection> connections)
        {
            if (blocks.Count == 0) return;

            const double nodeWidth = 150;
            const double nodeHeight = 52;
            const double gapX = 30;
            const double gapY = 55;
            const int maxCols = 6;

            foreach (var b in blocks)
            {
                b.Width = nodeWidth;
                b.Height = nodeHeight;
                b.Layer = -1;
            }

            // Build adjacency and in-degree maps
            var adj = new Dictionary<int, List<int>>();
            var inDegree = new Dictionary<int, int>();
            foreach (var b in blocks)
            {
                adj[b.Id] = [];
                inDegree[b.Id] = 0;
            }
            foreach (var c in connections)
            {
                if (adj.ContainsKey(c.From.Id) && inDegree.ContainsKey(c.To.Id))
                {
                    adj[c.From.Id].Add(c.To.Id);
                    inDegree[c.To.Id]++;
                }
            }

            // Roots: Block 0 is the starting execution block
            var roots = blocks.Where(b => inDegree[b.Id] == 0 || b.Id == 0).Select(b => b.Id).Distinct().ToList();
            if (roots.Count == 0 && blocks.Count > 0) roots.Add(blocks[0].Id);

            var blockMap = blocks.ToDictionary(b => b.Id);
            var queue = new Queue<int>();
            foreach (var r in roots)
            {
                if (blockMap.TryGetValue(r, out var rootBlock))
                {
                    rootBlock.Layer = 0;
                    queue.Enqueue(r);
                }
            }

            // BFS leveling
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                int currentLayer = blockMap[u].Layer;

                foreach (int v in adj[u])
                {
                    if (blockMap.TryGetValue(v, out var targetBlock))
                    {
                        if (targetBlock.Layer < 0)
                        {
                            targetBlock.Layer = currentLayer + 1;
                            queue.Enqueue(v);
                        }
                        else if (targetBlock.Layer > currentLayer && targetBlock.Layer < currentLayer + 1)
                        {
                            targetBlock.Layer = currentLayer + 1;
                        }
                    }
                }
            }

            int maxLayer = blocks.Max(b => Math.Max(0, b.Layer));
            foreach (var b in blocks)
            {
                if (b.Layer < 0) b.Layer = maxLayer + 1;
            }

            // Group by layer
            var layers = new Dictionary<int, List<BasicBlock>>();
            foreach (var b in blocks)
            {
                if (!layers.TryGetValue(b.Layer, out var list))
                {
                    list = [];
                    layers[b.Layer] = list;
                }
                list.Add(b);
            }

            // Sort nodes within each layer by ID
            foreach (var kvp in layers)
            {
                kvp.Value.Sort((a, b) => a.Id.CompareTo(b.Id));
            }

            // Determine maximum row columns in graph for horizontal centering
            int maxColsInGraph = 1;
            foreach (var kvp in layers)
            {
                int cols = Math.Min(kvp.Value.Count, maxCols);
                if (cols > maxColsInGraph) maxColsInGraph = cols;
            }
            double totalGraphWidth = maxColsInGraph * nodeWidth + (maxColsInGraph - 1) * gapX;

            double currentY = 40;
            var sortedLayers = layers.Keys.OrderBy(k => k).ToList();

            foreach (var layerIdx in sortedLayers)
            {
                var layerNodes = layers[layerIdx];
                int count = layerNodes.Count;
                int numSubRows = (int)Math.Ceiling((double)count / maxCols);

                for (int subRow = 0; subRow < numSubRows; subRow++)
                {
                    int startIdx = subRow * maxCols;
                    int endIdx = Math.Min(startIdx + maxCols, count);
                    int subRowCount = endIdx - startIdx;

                    double subRowWidth = subRowCount * nodeWidth + (subRowCount - 1) * gapX;
                    double startX = Math.Max(50, (totalGraphWidth - subRowWidth) / 2 + 50);

                    for (int i = 0; i < subRowCount; i++)
                    {
                        var node = layerNodes[startIdx + i];
                        node.X = startX + i * (nodeWidth + gapX);
                        node.Y = currentY;
                    }

                    currentY += nodeHeight + gapY;
                }
            }
        }

        public static void RecomputeLayout()
        {
            if (GraphBlocks == null || GraphConnections == null) return;
            ComputeHierarchicalLayout(GraphBlocks, GraphConnections);
        }

        public static void Clear()
        {
            uniqueIPAccesses?.Clear();
            blocks?.Clear();
            GraphBlocks?.Clear();
            GraphConnections?.Clear();
        }
    }
}