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
    public partial class WPF_RegisterRow : UserControl
    {
        public WPF_RegisterRow()
        {
            InitializeComponent();
        }

        public void Set(string register, string value)
        {
            this.register.Text = register;
            this.hex.Text = value;
        }
    }
}
