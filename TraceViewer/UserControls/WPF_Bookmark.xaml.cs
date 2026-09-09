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
        public int RowId { get; set; }

        public WPF_Bookmark(string id, string address, string disasm, string comment)
        {
            InitializeComponent();

            if (int.TryParse(id, out int parsedId))
            {
                RowId = parsedId;
            }
            this.id.Text = id ?? "";

            if (!string.IsNullOrEmpty(address))
            {
                if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    this.address.Text = address;
                }
                else if (ulong.TryParse(address, out ulong addressValue))
                {
                    this.address.Text = $"0x{addressValue:X}";
                }
                else
                {
                    this.address.Text = address;
                }
            }
            else
            {
                this.address.Text = "";
            }

            this.disasm.Inlines.Clear();
            if (!string.IsNullOrEmpty(disasm))
            {
                string[] singleInstructions = Regex.Split(disasm, @"([ ,:\[\]*])");
                foreach (string singleInstruction in singleInstructions)
                {
                    if (string.IsNullOrEmpty(singleInstruction)) continue;
                    this.disasm.Inlines.Add(new Run(singleInstruction) { Foreground = SyntaxHighlighter.Check_Type(singleInstruction) });
                }
            }

            this.comment.Text = comment ?? "";
            this.comment.TextChanged += Comment_TextChanged;
        }

        private void Comment_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (RowId >= 0 && TraceHandler.Trace?.Trace != null && RowId < TraceHandler.Trace.Trace.Count)
            {
                TraceHandler.Trace.Trace[RowId].comments = comment.Text;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            JumpToDisasm();
        }

        private void JumpToDisasm_Click(object sender, RoutedEventArgs e)
        {
            JumpToDisasm();
        }

        private void JumpToDisasm()
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.DisasmViewButton_MouseDown(null, null);
                mainWindow.ScrollTo(RowId);
            }
        }

        private void RemoveBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.BookmarkViewItems.Remove(this);
            }
        }
    }
}
