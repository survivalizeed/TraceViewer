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

        public static void GenerateGraph()
        {
            if (TraceHandler.Trace == null || TraceHandler.Trace.Trace.Count == 0)
            {
                MessageBox.Show("Trace ist leer oder nicht geladen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

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




