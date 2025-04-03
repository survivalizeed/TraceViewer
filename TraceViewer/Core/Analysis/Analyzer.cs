using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceViewer.Core.Analysis
{
    class Analyzer
    {

        public static void Analyze()
        {
            if (TraceHandler.Trace == null)
                return;
            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            DeObfus.DeObfuscate();

            if (window.blockSlicing)
                BlockSlicing();


        }

        private static void BlockSlicing()
        {

        }
    }
}
