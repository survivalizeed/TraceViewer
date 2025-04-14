using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        private void BlocksView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            BlocksHandler.FillBlocks();
        }
    }
}
