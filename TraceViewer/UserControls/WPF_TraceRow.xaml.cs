using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Provider;
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

        public static ulong stack_alignment_base = 0; // By default should be the first address being in a strict 8 byte layout (aka. calls, pushes, etc.)
        public static int stack_alignment = 8;


        public static ulong heap_alignment_base = 0;
        public static int heap_alignment = 8;

        private bool hidden = false;
        private float hiddenOpacity = 0.2f;

        private bool isFocused = false;

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

            regs = REGDUMP.X64_REGS;
            
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

            id.Width = window.Cd0.Width.Value;
            id_border.Width = window.Cd0.Width.Value;

            address.Width = window.Cd1.Width.Value;
            address_border.Width = window.Cd1.Width.Value;

            disasm.Width = window.Cd2.Width.Value;
            disasm_border.Width = window.Cd2.Width.Value;

            changes.Width = window.Cd3.Width.Value;
            changes_border.Width = window.Cd3.Width.Value;

            comments.Width = window.Cd4.Width.Value;
            mnemonicBrief.Width = window.Cd4.Width.Value;

            comments.GotFocus += Comments_GotFocus;
            comments.LostFocus += Comments_LostFocus;

            if (traceRow.isBlockStart)
            {
                block_panel.Visibility = Visibility.Visible;
                block.Text = traceRow.block;
            }
            if (hiddenRows.Contains(traceRow.Id) || DeObfus.deObHiddenRows.Contains(traceRow.Id))
            {
                parent_panel.Opacity = hiddenOpacity;
                hidden = true;
            }
        }


        private void SetDisassemblyText(string disassemblyText)
        {
            disasm.Inlines.Clear();
            string[] singleInstructions = Regex.Split(disassemblyText, @"([ ,:\[\]*])");

            foreach (string singleInstruction in singleInstructions)
            {           
                if (highlightedRegisterFamily != "" && DeObfus.registerFamiliesSSE[highlightedRegisterFamily].Contains(singleInstruction))
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

            if(window._toggleStack)
                UpdateStack();
            else
                UpdateHeap();

        }

        void UpdateStack()
        {
            FlowDocument flowDoc = new FlowDocument();
            Paragraph paragraph = new Paragraph();
            flowDoc.Blocks.Add(paragraph);

            if (traceRow.Id - 1 < 0)
                return;

            int alignment_counter = 0;
            string composed = "";
            ulong? blockStartAddress = null;
            ulong? blockEndAddress = null;

            int rsp_index = 0;

            Action<ulong, string> WriteLine = (address, data) => {
                Run addressRun = new Run($"{HexPrefix}{address:X} : ");
                addressRun.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                paragraph.Inlines.Add(addressRun);
                Run run = new Run($"{HexPrefix}{data}");
                run.Foreground = System.Windows.Media.Brushes.White;
                paragraph.Inlines.Add(run);
                if (address == stack_alignment_base)
                {
                    Run baseMark = new Run($" (BASE)");
                    baseMark.Foreground = System.Windows.Media.Brushes.Red;
                    paragraph.Inlines.Add(baseMark);
                }
                if (rsp_index != 0)
                {
                    Run rspRun = new Run($"  <--- RSP (past {rsp_index}th byte)");
                    rspRun.Foreground = System.Windows.Media.Brushes.Coral;
                    paragraph.Inlines.Add(rspRun);
                }
                paragraph.Inlines.Add(new LineBreak());
                composed = "";
                alignment_counter = 0;
                rsp_index = 0;
            };

            if (MemoryHandler.stacks.Count == 0)
                return;

            var stack = MemoryHandler.GetMemoryStateAt(traceRow.Id - 1, true).ToList();


            ulong updated_rsp = BitConverter.ToUInt64(TraceHandler.Trace.Trace[traceRow.Id].Regs[4], 0);

            for (int i = 0; i < stack.Count; i++)
            {
                var entry = stack[i];
                blockEndAddress = entry.Key;

                if (!blockStartAddress.HasValue)
                {
                    blockStartAddress = entry.Key;
                }

                if (i > 0)
                {
                    var previous_entry = stack[i - 1];
                    if (previous_entry.Key - entry.Key > 1)
                    {
                        if (!string.IsNullOrEmpty(composed))
                        {
                            WriteLine(previous_entry.Key, composed);
                        }

                        long difference = (long)previous_entry.Key - (long)entry.Key;
                        Run paddingDataRun = new Run($"PADDING : 0x{difference - 1:X}");
                        paddingDataRun.Foreground = System.Windows.Media.Brushes.Gray;
                        paragraph.Inlines.Add(paddingDataRun);
                        if (updated_rsp > entry.Key && updated_rsp < previous_entry.Key)
                        {
                            Run rspIndicatorRun = new Run($" <--- RSP (past byte 0x{previous_entry.Key - updated_rsp:X})");
                            rspIndicatorRun.Foreground = System.Windows.Media.Brushes.Coral;
                            paragraph.Inlines.Add(rspIndicatorRun);
                        }
                        paragraph.Inlines.Add(new LineBreak());
                        blockStartAddress = entry.Key; 
                        composed = ""; 
                        alignment_counter = 0;
                        rsp_index = 0;
                    }
                }

                composed += $"{entry.Value:X2}";
                alignment_counter++;

                if (stack_alignment_base + (ulong)stack_alignment == entry.Key)
                {
                    if (alignment_counter > 0 && blockEndAddress.HasValue)
                    {
                        WriteLine(blockEndAddress.Value, composed);
                    }
                }

                if (entry.Key == updated_rsp)
                    rsp_index = alignment_counter;

                if (alignment_counter == stack_alignment)
                {
                    if (blockEndAddress.HasValue && !string.IsNullOrEmpty(composed))
                    {
                        WriteLine(blockEndAddress.Value, composed);
                    }
                    blockStartAddress = i < stack.Count - 1 ? stack[i + 1].Key : (ulong?)null;
                }
            }

            if (!string.IsNullOrEmpty(composed) && blockEndAddress.HasValue)
            {
                WriteLine(blockEndAddress.Value, composed);
            }
            window.StackView.Document = flowDoc;
        }


        void UpdateHeap()
        {

            FlowDocument flowDoc = new FlowDocument();
            Paragraph paragraph = new Paragraph();
            flowDoc.Blocks.Add(paragraph);

            if (traceRow.Id - 1 < 0)
                return;

            int alignment_counter = 0;
            string composed = "";
            ulong? blockStartAddress = null;
            ulong? blockEndAddress = null;

            Action<ulong, string> WriteLine = (address, data) => {
                Run addressRun = new Run($"{HexPrefix}{address:X} : ");
                addressRun.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                Run dataRun = new Run($"{HexPrefix}{data}");
                dataRun.Foreground = System.Windows.Media.Brushes.White;
                paragraph.Inlines.Add(addressRun);
                paragraph.Inlines.Add(dataRun);
                if (address == heap_alignment_base)
                {
                    Run baseMark = new Run($" (BASE)");
                    baseMark.Foreground = System.Windows.Media.Brushes.Red;
                    paragraph.Inlines.Add(baseMark);
                }
                paragraph.Inlines.Add(new LineBreak());
                composed = "";
                alignment_counter = 0;
            };

            if (MemoryHandler.heaps.Count == 0)
                return;


            var heap = MemoryHandler.GetMemoryStateAt(traceRow.Id - 1, false).ToList();


            for (int i = 0; i < heap.Count; i++)
            {
                var entry = heap[i];
                blockEndAddress = entry.Key;

                if (!blockStartAddress.HasValue)
                {
                    blockStartAddress = entry.Key;
                }

                if (i > 0)
                {
                    var previous_entry = heap[i - 1];
                    if (previous_entry.Key - entry.Key > 1)
                    {
                        if (!string.IsNullOrEmpty(composed)) 
                        {
                            WriteLine(previous_entry.Key, composed);
                        }

                        long difference = (long)previous_entry.Key - (long)entry.Key;
                        Run paddingDataRun = new Run($"PADDING : 0x{difference - 1:X}");
                        paddingDataRun.Foreground = System.Windows.Media.Brushes.Gray;
                        paragraph.Inlines.Add(paddingDataRun);
                        paragraph.Inlines.Add(new LineBreak());
                        blockStartAddress = entry.Key;
                        composed = "";
                        alignment_counter = 0;
                    }
                }

                composed += $"{entry.Value:X2}";
                alignment_counter++;

                if (heap_alignment_base + (ulong)heap_alignment == entry.Key)
                {
                    if (alignment_counter > 0 && blockEndAddress.HasValue)
                    {
                        WriteLine(blockEndAddress.Value, composed);
                    }
                }

                if (alignment_counter == heap_alignment)
                {
                    if (blockEndAddress.HasValue && !string.IsNullOrEmpty(composed))
                    {
                        WriteLine(blockEndAddress.Value, composed);
                    }
                    blockStartAddress = i < heap.Count - 1 ? heap[i + 1].Key : (ulong?)null;
                }
            }

            if (!string.IsNullOrEmpty(composed) && blockEndAddress.HasValue)
            {
                WriteLine(blockEndAddress.Value, composed);
            }

            window.HeapView.Document = flowDoc;
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
                registerRow.value.Foreground = Brushes.White;
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
                var index = GraphHandler.uniqueIPAccesses?.FindIndex(x => x.Key == traceRow.Ip);
                if (index != null)
                {
                    var ids = GraphHandler.uniqueIPAccesses[(int)index].Value;
                    foreach (var id in ids)
                    {
                        hiddenRows.Add(id);
                        foreach (var instruction in window.InstructionViewItems)
                        {
                            if (instruction.id.Text == id.ToString())
                            {
                                instruction.parent_panel.Opacity = hiddenOpacity;
                            }
                        }
                    }
                }
            }
            else
            {
                var index = GraphHandler.uniqueIPAccesses?.FindIndex(x => x.Key == traceRow.Ip);
                if (index != null)
                {
                    var ids = GraphHandler.uniqueIPAccesses[(int)index].Value;
                    foreach (var id in ids)
                    {
                        hiddenRows.Remove(id);
                        DeObfus.deObHiddenRows.Remove(id); // Also remove there so the users input ALWAYS overwrites the deobfuscation
                        foreach (var instruction in window.InstructionViewItems)
                        {
                            if (instruction.id.Text == id.ToString())
                            {
                                instruction.parent_panel.Opacity = 1;
                            }
                        }
                    }
                }
            }
            hidden = !hidden;
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
            if (window.addressBasedCommenting && isFocused)
            {
                var index = GraphHandler.uniqueIPAccesses?.FindIndex(x => x.Key == traceRow.Ip);
                if (index != null)
                {
                    var ids = GraphHandler.uniqueIPAccesses[(int)index].Value;
                    foreach (var id in ids)
                    {
                        TraceHandler.Trace.Trace[id].comments = comments.Text;
                        foreach (var instruction in window.InstructionViewItems)
                        {
                            if (instruction.id.Text == id.ToString())
                            {
                                instruction.comments.Text = comments.Text;
                            }
                        }
                    }
                }
            }
        }

        private void Comments_LostFocus(object sender, RoutedEventArgs e)
        {
            isFocused = false;
        }

        private void Comments_GotFocus(object sender, RoutedEventArgs e)
        {
            isFocused = true;
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


                foreach (var registerFamily in DeObfus.registerFamiliesSSE)
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

        private void MarkUnmarkAsBlockStart_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if(traceRow.isBlockStart)
            {
                traceRow.isBlockStart = false;
                block_panel.Visibility = Visibility.Collapsed;
                block.Text = "";
            }
            else
            {
                traceRow.isBlockStart = true;
                block_panel.Visibility = Visibility.Visible;
                block.Focus();
            }
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

        private void block_TextChanged(object sender, TextChangedEventArgs e)
        {
            traceRow.block = block.Text;
        }
    }
}