using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using static System.Net.Mime.MediaTypeNames;

namespace TraceViewer
{
    public partial class WPF_TraceRow : UserControl
    {
        public static HashSet<int> hiddenRows = new HashSet<int>();

        public static string highlightedRegisterFamily = "";

        private bool hidden = false;
        private float hiddenOpacity = 0.2f;

        private const string HexPrefix = "0x";
        private const string ChangeSeparator = "; ";
        private const string ChangeArrow = " -> ";
        private const string RegisterValueSeparator = ": ";
        private const string ZeroHexValue = "00";

        private List<byte[]> registers_x64;
        private List<Tuple<string, int>> regs; // Stores register names and sizes, without padding.
        private List<string> highlights = new List<string>(); // Registers that have changed in this row.
        private List<MemoryAccess> memoryAccesses = new List<MemoryAccess>(); // Memory accesses in this row.

        private TraceRow traceRow;

        private string mnemonic; // Full mnemonic string.
        private MainWindow window; // Reference to the main window.

        public WPF_TraceRow(TraceRow traceRow, string mnemonicBriefText, string mnemonicText)
        {
            InitializeComponent();
            window = System.Windows.Application.Current.MainWindow as MainWindow ?? throw new Exception("Main window not found");

            regs = prefs.X64_REGS.ToList();
            regs.RemoveAll(reg => string.IsNullOrEmpty(reg.Item1));
            
            mnemonicBrief.Text = mnemonicBriefText;
            mnemonic = mnemonicText;
            memoryAccesses = traceRow.Mem;
            this.traceRow = traceRow;

            changes.Inlines.Clear();
            highlights.Clear();

            if (traceRow.Regchanges != null)
            {
                // Incase its untraced
                if (traceRow.Regchanges.Count == 1)
                {
                    changes.Inlines.Add(new Run(traceRow.Regchanges[0]) { Foreground = Brushes.White });
                }
                else
                {
                    for (int i = 0; i < traceRow.Regchanges.Count; i += 6)
                    {
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i]) { Foreground = SyntaxHighlighter.Check_Type(traceRow.Regchanges[i]) });
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i + 1]) { Foreground = Brushes.White });
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i + 2]) { Foreground = SyntaxHighlighter.Check_Type(traceRow.Regchanges[i + 2]) });
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i + 3]) { Foreground = Brushes.White });
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i + 4]) { Foreground = SyntaxHighlighter.Check_Type(traceRow.Regchanges[i + 4]) });
                        changes.Inlines.Add(new Run(traceRow.Regchanges[i + 5]) { Foreground = Brushes.White });
                    }
                }
            }

            registers_x64 = traceRow.Regs;

            if (!traceRow.already_swaped)
            {
                SwapRegisters(registers_x64, 2, 3);
                SwapRegisters(registers_x64, 1, 2);
                traceRow.already_swaped = true;
            }

            id.Text = traceRow.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = $"{HexPrefix}{traceRow.Ip:X}"; // Use string interpolation for readability
            address.Foreground = Brushes.White;

            comments.Text = traceRow.comments;

            display_mnemonic_brief(!window._toggleMnemonic);

            SetDisassemblyText(traceRow.Disasm);

            id.Width = window.cd0.Width.Value;
            id_border.Width = window.cd0.Width.Value;

            address.Width = window.cd1.Width.Value;
            address_border.Width = window.cd1.Width.Value;

            disasm.Width = window.cd2.Width.Value;
            disasm_border.Width = window.cd2.Width.Value;

            changes.Width = window.cd3.Width.Value;
            changes_border.Width = window.cd3.Width.Value;

            comments.Width = window.cd4.Width.Value;
            mnemonicBrief.Width = window.cd4.Width.Value;

            if(hiddenRows.Contains(traceRow.Id) || DeObfus.deObHiddenRows.Contains(traceRow.Id))
                parent_panel.Opacity = hiddenOpacity;
        }


        private void SetDisassemblyText(string disassemblyText)
        {
            disasm.Inlines.Clear();
            // Faster than regex
            string[] singleInstructions = Regex.Split(disassemblyText, @"([ ,:\[\]*])");

            foreach (string singleInstruction in singleInstructions)
            {           
                if (highlightedRegisterFamily != "" && Globals.registerFamilies[highlightedRegisterFamily].Contains(singleInstruction))
                    disasm.Inlines.Add(new Run(singleInstruction) { Foreground = SyntaxHighlighter.Check_Type(singleInstruction), 
                        Background = Brushes.DarkRed });
                else
                    disasm.Inlines.Add(new Run(singleInstruction) { Foreground = SyntaxHighlighter.Check_Type(singleInstruction) });
            }
        }

        private void OnHover(object sender, MouseEventArgs e)
        {
            int registerIndex = 0;
            HashSet<string> highlightSet = new HashSet<string>(traceRow.highlights, StringComparer.OrdinalIgnoreCase);

            foreach (WPF_RegisterRow registerRow in window.RegistersView.Items.OfType<WPF_RegisterRow>())
            {
                if (registerRow != null)
                {
                    bool isHighlighted = highlightSet.Contains(registerRow.register.Text);
                    UpdateRegisterDisplay(registerRow, registerIndex, isHighlighted);
                }
                registerIndex++;
            }

            window.memory1.Visibility = Visibility.Collapsed;
            window.memory2.Visibility = Visibility.Collapsed;
            window.memory3.Visibility = Visibility.Collapsed;

            if (memoryAccesses.Count > 0)
            {
                window.memory1.Visibility = Visibility.Visible;
                window.write1.Content = memoryAccesses[0].Access;
                window.address1.Content = $"{HexPrefix}{memoryAccesses[0].Addr:X}";
                window.value1.Content = $"{HexPrefix}{memoryAccesses[0].Value:X}";
            }
            if (memoryAccesses.Count > 1)
            {
                window.memory2.Visibility = Visibility.Visible;
                window.write2.Content = memoryAccesses[1].Access;
                window.address2.Content = $"{HexPrefix}{memoryAccesses[1].Addr:X}";
                window.value2.Content = $"{HexPrefix}{memoryAccesses[1].Value:X}";
            }
            if (memoryAccesses.Count > 2) // Wouldnt know what to do with more than 3 memory accesses
            {
                window.memory3.Visibility = Visibility.Visible;
                window.write3.Content = memoryAccesses[2].Access;
                window.address3.Content = $"{HexPrefix}{memoryAccesses[2].Addr:X}";
                window.value3.Content = $"{HexPrefix}{memoryAccesses[2].Value:X}";
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

        public string ByteArrayToHexString(byte[] bytes, bool zeroRemoval)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return ZeroHexValue;
            }

            StringBuilder hexBuilder = new StringBuilder(bytes.Length * 2); 

            if (zeroRemoval)
            {
                bool leadingZero = true; // Flag to handle leading zeros correctly
                for (int i = bytes.Length - 1; i >= 0; i--) // Iterate in reverse without Reverse()
                {
                    byte b = bytes[i];
                    if (b != 0 || !leadingZero || i == 0) // Keep at least one zero if all bytes are zero
                    {
                        hexBuilder.Append(b.ToString("X2"));
                        leadingZero = false; // No longer leading zero after a non-zero byte or the last byte is processed
                    }
                }
            }
            else
            {
                for (int i = bytes.Length - 1; i >= 0; i--) // Iterate in reverse without Reverse()
                {
                    hexBuilder.Append(bytes[i].ToString("X2"));
                }
            }

            return hexBuilder.Length == 0 ? ZeroHexValue : hexBuilder.ToString(); // Handle empty builder case
        }

        private void SwapRegisters<T>(List<T> list, int index1, int index2)
        {
            (list[index1], list[index2]) = (list[index2], list[index1]);
        }

        private void PreviewOnKeyPressComments(object sender, KeyEventArgs e)
        {
            // Grey out row
            if(e.Key == Key.H && Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                ToggleHide();
            }

            // Navigation
            if (e.Key == Key.Enter)
                FocusNextCommentBox(1);
            
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

        private void ToggleHide()
        {
            if (!hidden)
            {
                hiddenRows.Add(traceRow.Id);
                parent_panel.Opacity = hiddenOpacity;
            }
            else
            {
                hiddenRows.Remove(traceRow.Id);
                DeObfus.deObHiddenRows.Remove(traceRow.Id); // Also remove there so the users input ALWAYS overwrites the deobfuscation
                parent_panel.Opacity = 1;
            }
            hidden = !hidden;
        }

        public void Hide(bool hide)
        {
            if (hide)
            {
                hiddenRows.Add(traceRow.Id);
                parent_panel.Opacity = hiddenOpacity;
            }
            else
            {
                hiddenRows.Remove(traceRow.Id);
                DeObfus.deObHiddenRows.Remove(traceRow.Id); // Also remove there so the users input ALWAYS overwrites the deobfuscation
                parent_panel.Opacity = 1;
            }
            hidden = hide;
        }

        private void FocusNextCommentBox(int direction)
        {
            for (int i = 0; i < window.InstructionViewItems.Count; i++)
            {
                if (this == window.InstructionViewItems[i])
                {
                    int nextIndex = i + direction;
                    if (nextIndex < 0 || nextIndex > window.InstructionViewItems.Count - 1)
                    {
                        if(window.ScrollControl(-direction))
                            FocusNextCommentBox(direction);
                        return;
                    }
                    if (nextIndex >= 0 && nextIndex < window.InstructionViewItems.Count && window.InstructionViewItems[nextIndex] is WPF_TraceRow nextControl)
                    {
                        comments.InvalidateVisual();  // Necessary because the current control is often not updated for some reason
                        comments.UpdateLayout();
                        nextControl.comments.Focus();
                        nextControl.OnHover(null, null); // To refresh the register highlighting
                    }
                    break; // Exit loop once current item is found
                }
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
            window.MainView.Visibility = Visibility.Collapsed;
            window.MnemonicReaderScrollView.Visibility = Visibility.Visible;
            window.MnemonicReader.Content = mnemonic; // Display full mnemonic
        }

        private void TextChangedComments(object sender, TextChangedEventArgs e)
        {
            traceRow.comments = comments.Text;
        }


        private void disasm_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            Point mousePosition = Mouse.GetPosition(disasm);
            TextPointer textPointer = disasm.GetPositionFromPoint(mousePosition, true);

            if (string.IsNullOrWhiteSpace(textPointer.GetTextInRun(LogicalDirection.Forward)))
                return;

            if (textPointer != null)
            {
                TextPointer wordStart = textPointer;
                TextPointer wordEnd = textPointer;

                while (wordStart != null &&
                       wordStart.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text)
                {
                    string textRun = wordStart.GetTextInRun(LogicalDirection.Backward);
                    if (string.IsNullOrEmpty(textRun) || char.IsWhiteSpace(textRun.Last()))
                        break;
                    wordStart = wordStart.GetPositionAtOffset(-1, LogicalDirection.Backward);
                }

                while (wordEnd != null &&
                       wordEnd.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = wordEnd.GetTextInRun(LogicalDirection.Forward);
                    if (string.IsNullOrEmpty(textRun) || char.IsWhiteSpace(textRun.First()))
                        break;
                    wordEnd = wordEnd.GetPositionAtOffset(1, LogicalDirection.Forward);
                }

                var wordRange = new TextRange(wordStart, wordEnd);
                string wordUnderMouse = wordRange.Text.Trim();


                foreach (var registerFamily in Globals.registerFamilies)
                {
                    foreach (var register in registerFamily.Value)
                    {
                        if (wordUnderMouse.Equals(register, StringComparison.OrdinalIgnoreCase))
                        {
                            highlightedRegisterFamily = highlightedRegisterFamily == registerFamily.Key
                                ? ""
                                : registerFamily.Key;

                            window.RefreshView();
                            return;
                        }
                    }
                }
            }
        }

        private void Copy_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            MenuItem? menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                ContextMenu? contextMenu = menuItem.Parent as ContextMenu;
                if (contextMenu != null)
                {
                    if (contextMenu.PlacementTarget is TextBlock sourceTextBlock)
                    {
                        string textToCopy = sourceTextBlock.Text;
                        if (textToCopy == "")
                        {
                            if (sourceTextBlock == disasm)
                                textToCopy = traceRow.Disasm;
                            else if (sourceTextBlock == changes)
                            {
                                foreach (string change in traceRow.Regchanges)
                                {
                                    textToCopy += change;
                                }
                            }
                        }
                        Clipboard.SetText(textToCopy);
                    }
                }
            }
        }

        private void CopyRow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            string changesText = "";
            foreach(string change in traceRow.Regchanges)
            {
                changesText += change;
            }
            Clipboard.SetText($"#: {id.Text} | {address.Text} | {traceRow.Disasm} | {changesText} | {comments.Text}");
        }

        private void AddBookmark_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

        }

        private void ShowOrRemove_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            ToggleHide();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            window.DimmingOverlay.Visibility = Visibility.Visible;
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            window.DimmingOverlay.Visibility = Visibility.Collapsed;
        }
    }
}