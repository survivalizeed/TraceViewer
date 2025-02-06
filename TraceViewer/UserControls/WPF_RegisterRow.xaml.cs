using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TraceViewer
{
    public class Registers
    {
        public static string[] x64 = new string[] {
            "RAX", "RBX", "RCX", "RDX", "RBP", "RSP", 
            "RSI", "RDI",  "R8",  "R9", "R10", "R11", 
            "R12", "R13", "R14", "R15", "DR0", "RIP", 
            "DR1","DR2", "DR3", "DR6", "DR7"};

    }
    public partial class WPF_RegisterRow : UserControl
    {
        public WPF_RegisterRow()
        {
            InitializeComponent();
        }
    }
}
