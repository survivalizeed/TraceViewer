using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using System.Xml.Linq;
using TraceViewer.Core.Analysis;
using TraceViewer.Core;

namespace TraceViewer
{

    public partial class WPF_Bookmark : UserControl
    {
        public WPF_Bookmark(string id, string address, string disasm)
        {
            InitializeComponent();
            this.id.Text = id;
            if (ulong.TryParse(address, out ulong addressValue))
            {
                this.address.Text = "0x" + addressValue.ToString("X");
            }

            this.disasm.Inlines.Clear();
            string[] singleInstructions = Regex.Split(disasm, @"([ ,:\[\]*])");

            foreach (string singleInstruction in singleInstructions)
            {
                this.disasm.Inlines.Add(new Run(singleInstruction) { Foreground = SyntaxHighlighter.Check_Type(singleInstruction) });
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            window.DisasmViewButton_MouseDown(null, null);

            window.ScrollControl(-Convert.ToInt32(id.Text), true);
        }
    }
}
