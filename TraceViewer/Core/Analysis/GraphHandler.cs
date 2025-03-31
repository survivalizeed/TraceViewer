using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TraceViewer.Core.Analysis
{


    internal class GraphHandler
    {
        public class BasicBlock
        {
            public int BlockId { get; } // Eindeutige ID für diesen Block
            public int StartTraceId { get; } // ID der ersten TraceRow in diesem Block
            public int EndTraceId { get; }   // ID der letzten TraceRow in diesem Block
            public ulong StartIp { get; }    // IP der ersten TraceRow

            // Optional: Liste der TraceRows im Block (kann Speicherintensiv sein)
            // public List<TraceRow> Rows { get; } = new List<TraceRow>();

            public BasicBlock(int blockId, int startTraceId, int endTraceId, ulong startIp)
            {
                BlockId = blockId;
                StartTraceId = startTraceId;
                EndTraceId = endTraceId;
                StartIp = startIp;
            }

            public override string ToString()
            {
                return $"Block {BlockId} [TraceID: {StartTraceId}-{EndTraceId}, StartIP: 0x{StartIp:X}]";
            }
        }

        // Repräsentiert eine Verbindung (Kante) zwischen zwei Blöcken
        public class BlockConnection
        {
            public int FromBlockId { get; }
            public int ToBlockId { get; }

            public BlockConnection(int fromBlockId, int toBlockId)
            {
                FromBlockId = fromBlockId;
                ToBlockId = toBlockId;
            }

            public override string ToString()
            {
                return $"Connection: Block {FromBlockId} -> Block {ToBlockId}";
            }

            // Für Verwendung in HashSet etc.
            public override bool Equals(object? obj)
            {
                return obj is BlockConnection connection &&
                       FromBlockId == connection.FromBlockId &&
                       ToBlockId == connection.ToBlockId;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(FromBlockId, ToBlockId);
            }
        }

        // Ergebnis der Analyse
        public class GraphResult
        {
            public List<BasicBlock> Blocks { get; } = new List<BasicBlock>();
            public HashSet<BlockConnection> Connections { get; } = new HashSet<BlockConnection>();
            public Dictionary<int, int> TraceIdToBlockIdMap { get; } = new Dictionary<int, int>();
        }


        public static void GenerateGraph()
        {
            if (TraceHandler.Trace == null || TraceHandler.Trace.Trace.Count == 0)
            {
                MessageBox.Show("Trace ist leer oder nicht geladen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var traceRows = TraceHandler.Trace.Trace;
            var result = new GraphResult();

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

            SortedSet<int> slice_locations = new SortedSet<int>();

            // Split based on access count
            foreach (var occ in ipOccurrences)
            {
                var currentIndex = ipOccurrences.Keys.ToList().IndexOf(occ.Key);
                if (currentIndex < ipOccurrences.Count - 1)
                {
                    var next = ipOccurrences.ElementAt(currentIndex + 1);
                    if (occ.Value.Count != next.Value.Count)
                    {
                        slice_locations.Add(currentIndex);
                    }
                }
            }


            // Split based on execution flow
            for (int i = 0; i < ipOccurrences.Count; i++)
            {
                if (ipOccurrences.ElementAt(i).Value.Count > 1 && !slice_locations.Contains(i))
                {
                    for (int j = i; j < ipOccurrences.Count - 1; j++)
                    {
                        if (slice_locations.Contains(j + 1))
                            break;
                        for (int k = 0; k < ipOccurrences.ElementAt(j).Value.Count; k++)
                        {
                            if (ipOccurrences.ElementAt(j + 1).Value[k] - ipOccurrences.ElementAt(j).Value[k] != 1)
                            {
                                slice_locations.Add(j);
                            }
                        }
                    }
                }
            }

            

        }

    }
}




