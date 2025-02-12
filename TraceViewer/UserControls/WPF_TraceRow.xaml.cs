using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TraceViewer.Core;

namespace TraceViewer
{
    public partial class WPF_TraceRow : UserControl
    {
        private const string HexPrefix = "0x";
        private const string ChangeSeparator = "; ";
        private const string ChangeArrow = " -> ";
        private const string RegisterValueSeparator = ": ";
        private const string ZeroHexValue = "00";

        private List<byte[]> registers_x64;
        private List<Tuple<string, int>> regs; // Stores register names and sizes, without padding.
        private List<string> highlights = new List<string>(); // Registers that have changed in this row.
        private string mnemonic; // Full mnemonic string.
        private MainWindow window; // Reference to the main window.

        public WPF_TraceRow(TraceRow currentTraceRow, TraceRow? nextTraceRow, string mnemonicBriefText, string mnemonicText)
        {
            InitializeComponent();
            regs = prefs.X64_REGS.ToList();
            regs.RemoveAll(reg => string.IsNullOrEmpty(reg.Item1));
            mnemonicBrief.Text = mnemonicBriefText;
            mnemonic = mnemonicText;

            Set(currentTraceRow, nextTraceRow);
            window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");
        }

        public void Set(TraceRow currentTraceRow, TraceRow? nextTraceRow)
        {
            changes.Inlines.Clear();
            highlights.Clear();

            if (nextTraceRow != null)
            {
                DetectRegisterChanges(currentTraceRow, nextTraceRow);
            }

            registers_x64 = currentTraceRow.Regs;
            SwapRegisters(registers_x64, 1, 3);
            SwapRegisters(registers_x64, 2, 3);


            id.Text = currentTraceRow.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = $"{HexPrefix}{currentTraceRow.Ip:X}"; // Use string interpolation for readability
            address.Foreground = Brushes.White;

            SetDisassemblyText(currentTraceRow.Disasm);
        }

        private void DetectRegisterChanges(TraceRow currentTraceRow, TraceRow nextTraceRow)
        {
            for (int i = 0; i < regs.Count; ++i)
            {
                // Compare register values and skip 'rip' register
                if (!nextTraceRow.Regs[i].SequenceEqual(currentTraceRow.Regs[i]) && regs[i].Item1 != "rip")
                {
                    AddRegisterChangeInline(regs[i].Item1, currentTraceRow.Regs[i], nextTraceRow.Regs[i]);
                    highlights.Add(regs[i].Item1); // Mark register for highlight on hover
                }
            }
        }

        private void AddRegisterChangeInline(string regName, byte[] currentRegValue, byte[] nextRegValue)
        {
            string currentRegHex = ByteArrayToHexString(currentRegValue, true);
            string nextRegHex = ByteArrayToHexString(nextRegValue, true);

            changes.Inlines.Add(new Run(regName) { Foreground = SyntaxHighlighter.Check_Type(regName) });
            changes.Inlines.Add(new Run(RegisterValueSeparator) { Foreground = Brushes.White });
            changes.Inlines.Add(new Run($"{HexPrefix}{currentRegHex}") { Foreground = SyntaxHighlighter.Check_Type($"{HexPrefix}{currentRegHex}") });
            changes.Inlines.Add(new Run(ChangeArrow) { Foreground = Brushes.White });
            changes.Inlines.Add(new Run($"{HexPrefix}{nextRegHex}") { Foreground = SyntaxHighlighter.Check_Type($"{HexPrefix}{nextRegHex}") });
            changes.Inlines.Add(new Run(ChangeSeparator) { Foreground = Brushes.White });
        }

        private void SetDisassemblyText(string disassemblyText)
        {
            disasm.Inlines.Clear();
            string[] singleInstructions = Regex.Split(disassemblyText, @"([ ,:\[\]*])");

            foreach (string singleInstruction in singleInstructions)
            {
                disasm.Inlines.Add(new Run(singleInstruction) { Foreground = SyntaxHighlighter.Check_Type(singleInstruction) });
            }
        }

        private void OnHover(object sender, MouseEventArgs e)
        {
            int registerIndex = 0;
            HashSet<string> highlightSet = new HashSet<string>(highlights, StringComparer.OrdinalIgnoreCase);

            foreach (WPF_RegisterRow registerRow in window.RegistersView.Items.OfType<WPF_RegisterRow>())
            {
                if (registerRow != null)
                {
                    bool isHighlighted = highlightSet.Contains(registerRow.register.Text);
                    UpdateRegisterDisplay(registerRow, registerIndex, isHighlighted);
                }
                registerIndex++;
            }
        }

        private void UpdateRegisterDisplay(WPF_RegisterRow registerRow, int registerIndex, bool isHighlighted)
        {
            if (isHighlighted)
            {
                registerRow.register.Foreground = Brushes.Red;
                registerRow.value.Foreground = Brushes.Red;
            }
            else
            {
                registerRow.register.Foreground = Brushes.Coral;
                registerRow.value.Foreground = Brushes.DarkGoldenrod;
            }
            registerRow.value.Text = $"{HexPrefix}{ByteArrayToHexString(registers_x64[registerIndex], false)}";
        }

        private string ByteArrayToHexString(byte[] bytes, bool zeroRemoval)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ZeroHexValue;
            }

            string hex = string.Concat(bytes.Reverse().Select(b => b.ToString("X2")));

            if (zeroRemoval)
            {
                hex = string.Concat(bytes.Reverse().Where(b => b != 0x00).Select(b => b.ToString("X2")));
                if (string.IsNullOrEmpty(hex))
                {
                    return ZeroHexValue;
                }
            }
            return hex;
        }

        private void SwapRegisters<T>(List<T> list, int index1, int index2)
        {
            (list[index1], list[index2]) = (list[index2], list[index1]);
        }

        private void OnDoubleClickComments(object sender, MouseButtonEventArgs e)
        {
            ActivateBigCommentEdit();
        }

        private void OnKeyPressComments(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ActivateBigCommentEdit();
        }

        private void PreviewOnKeyPressComments(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    FocusNextCommentBox(5);
                else
                    FocusNextCommentBox(1);

            else if (e.Key == Key.Up)
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    FocusNextCommentBox(-5);
                else
                    FocusNextCommentBox(-1);   
        }

        private void FocusNextCommentBox(int direction)
        {
            for (int i = 0; i < window.InstructionViewItems.Count; i++)
            {
                if (this == window.InstructionViewItems[i])
                {
                    int nextIndex = i + direction;
                    if (nextIndex >= 0 && nextIndex < window.InstructionViewItems.Count && window.InstructionViewItems[nextIndex] is WPF_TraceRow nextControl)
                    {
                        nextControl.comments.Focus();
                    }
                    break; // Exit loop once current item is found
                }
            }
        }

        private void ActivateBigCommentEdit()
        {
            window.DisassemblerView.Visibility = Visibility.Collapsed;
            window.CommentContentGridHitbox.Visibility = Visibility.Visible;

            // Safely access the comment_content TextBox
            if (window.CommentContentGridHitbox.Children.Count > 0 && window.CommentContentGridHitbox.Children[0] is DockPanel dockPanel 
                && dockPanel.Children.Count > 0 && dockPanel.Children[0] is TextBox comment_content)
            {
                comment_content.Text = this.comments.Text;
                comment_content.Focus();
                comment_content.SelectionStart = comment_content.Text.Length;
                window.CurrentCommentContentPartner = comments; // Set partner for text update
            }
        }

        public void display_mnemonic_brief(bool displayMnemonicBrief)
        {
            mnemonicBrief.Visibility = displayMnemonicBrief ? Visibility.Visible : Visibility.Collapsed;
            comments.Visibility = displayMnemonicBrief ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnDoubleClickMnemonic(object sender, MouseButtonEventArgs e)
        {
            ActivateBigMnemonicView();
        }

        private void OnKeyPressMnemonic(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ActivateBigMnemonicView();
            }
        }

        private void ActivateBigMnemonicView()
        {
            window._disassemblerViewOffset = window.InstructionsScrollViewer.VerticalOffset; // Save scroll position
            window.DisassemblerView.Visibility = Visibility.Collapsed;
            window.MnemonicReaderScrollView.Visibility = Visibility.Visible;
            window.MnemonicReader.Content = mnemonic; // Display full mnemonic
        }

    }
}