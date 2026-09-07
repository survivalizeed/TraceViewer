using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using TraceViewer.Core;
using TraceViewer.Core.Analysis;
using TraceViewer.UserControls;

namespace TraceViewer
{
    public partial class WPF_TraceRow : UserControl
    {
        public static HashSet<int> hiddenRows = new();
        public static string highlightedRegisterFamily = "";

        public static ulong stack_alignment_base = 0;
        public static int stack_alignment = 8;

        public static ulong heap_alignment_base = 0;
        public static int heap_alignment = 8;

        private bool hidden = false;
        private float hiddenOpacity = 0.2f;
        private bool isFocused = false;

        private const string HexPrefix = "0x";
        private const string ZeroHexValue = "00";

        private List<byte[]>? registers_x64;
        private List<MemoryAccess>? memoryAccesses;
        private TraceRow? traceRow;
        private string mnemonic = "";
        private MainWindow window;

        public WPF_TraceRow()
        {
            InitializeComponent();
            window = Application.Current.MainWindow as MainWindow
                ?? throw new InvalidOperationException("Main window not found");

            comments.GotFocus += Comments_GotFocus;
            comments.LostFocus += Comments_LostFocus;
        }

        public WPF_TraceRow(TraceRow traceRow, string mnemonicBriefText, string mnemonicText) : this()
        {
            Bind(traceRow, mnemonicBriefText, mnemonicText);
        }

        /// <summary>
        /// Re-binds this existing control to a trace row in-place.
        /// Avoids destroying and re-creating WPF visual trees during scrolling.
        /// </summary>
        public void Bind(TraceRow traceRow, string mnemonicBriefText, string mnemonicText)
        {
            this.traceRow = traceRow;
            this.mnemonic = mnemonicText;
            this.memoryAccesses = traceRow.Mem;
            this.registers_x64 = traceRow.Regs;

            mnemonicBrief.Text = mnemonicBriefText;

            id.Text = traceRow.Id.ToString();
            id.Foreground = Brushes.White;

            address.Text = $"{HexPrefix}{traceRow.Ip:X}";
            address.Foreground = Brushes.White;

            comments.Text = traceRow.comments;

            display_mnemonic_brief(!window._toggleMnemonic);

            SetDisassemblyText(traceRow.Disasm);

            UpdateChanges(traceRow.Regchanges);

            if (traceRow.isBlockStart)
            {
                block_panel.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(traceRow.block))
                    traceRow.block = $"Block_{traceRow.Id}";
                block.Text = traceRow.block;
            }
            else
            {
                block_panel.Visibility = Visibility.Collapsed;
                block.Text = "";
            }

            if (hiddenRows.Contains(traceRow.Id) || DeObfus.deObHiddenRows.Contains(traceRow.Id))
            {
                parent_panel.Opacity = hiddenOpacity;
                hidden = true;
            }
            else
            {
                parent_panel.Opacity = 1.0;
                hidden = false;
            }
        }

        private void SetDisassemblyText(string disassemblyText)
        {
            disasm.Inlines.Clear();
            if (string.IsNullOrEmpty(disassemblyText)) return;

            int start = 0;
            int len = disassemblyText.Length;
            for (int i = 0; i < len; i++)
            {
                char c = disassemblyText[i];
                if (c is ' ' or ',' or ':' or '[' or ']' or '*')
                {
                    if (i > start)
                    {
                        AddDisasmToken(disassemblyText.Substring(start, i - start));
                    }
                    AddDisasmToken(disassemblyText.Substring(i, 1));
                    start = i + 1;
                }
            }
            if (start < len)
            {
                AddDisasmToken(disassemblyText.Substring(start, len - start));
            }
        }

        private void AddDisasmToken(string token)
        {
            if (!string.IsNullOrEmpty(highlightedRegisterFamily) &&
                DeObfus.registerFamiliesSSE.TryGetValue(highlightedRegisterFamily, out var family) &&
                family.Contains(token))
            {
                disasm.Inlines.Add(new Run(token)
                {
                    Foreground = SyntaxHighlighter.Check_Type(token),
                    Background = Brushes.DarkRed
                });
            }
            else
            {
                disasm.Inlines.Add(new Run(token)
                {
                    Foreground = SyntaxHighlighter.Check_Type(token)
                });
            }
        }

        private void UpdateChanges(List<string>? regChanges)
        {
            changes.Inlines.Clear();
            if (regChanges == null || regChanges.Count == 0) return;

            if (regChanges.Count == 1)
            {
                changes.Inlines.Add(new Run(regChanges[0]) { Foreground = Brushes.White });
            }
            else
            {
                for (int i = 0; i + 5 < regChanges.Count; i += 6)
                {
                    changes.Inlines.Add(new Run(regChanges[i]) { Foreground = SyntaxHighlighter.Check_Type(regChanges[i]) });
                    changes.Inlines.Add(new Run(regChanges[i + 1]) { Foreground = Brushes.White });
                    changes.Inlines.Add(new Run(regChanges[i + 2]) { Foreground = SyntaxHighlighter.Check_Type(regChanges[i + 2]) });
                    changes.Inlines.Add(new Run(regChanges[i + 3]) { Foreground = Brushes.White });
                    changes.Inlines.Add(new Run(regChanges[i + 4]) { Foreground = SyntaxHighlighter.Check_Type(regChanges[i + 4]) });
                    changes.Inlines.Add(new Run(regChanges[i + 5]) { Foreground = Brushes.White });
                }
            }
        }

        private void OnRowMouseEnter(object sender, MouseEventArgs e)
        {
            OnHover(sender, e);
        }

        public void OnHover(object? sender, MouseEventArgs? e)
        {
            if (traceRow == null) return;

            // Deduplication: do not recalculate if mouse is still hovering on the same row
            if (window.CurrentHoveredRow == this) return;
            window.CurrentHoveredRow = this;

            HashSet<string>? highlightSet = traceRow.highlights.Count > 0
                ? new HashSet<string>(traceRow.highlights, StringComparer.OrdinalIgnoreCase)
                : null;

            int registerIndex = 0;
            foreach (WPF_RegisterRow registerRow in window.RegistersView.Items.OfType<WPF_RegisterRow>())
            {
                if (registerRow != null)
                {
                    bool isHighlighted = highlightSet != null && highlightSet.Contains(registerRow.register.Text);
                    UpdateRegisterDisplay(registerRow, registerIndex, isHighlighted);
                }
                registerIndex++;
            }

            if (window._toggleStack)
                UpdateStack();
            else
                UpdateHeap();
        }

        private static int MapDisplayIndexToRegIndex(int displayIndex) => displayIndex switch
        {
            1 => 3, // RBX
            2 => 1, // RCX
            3 => 2, // RDX
            _ => displayIndex
        };

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

            if (registers_x64 != null)
            {
                int mappedIdx = MapDisplayIndexToRegIndex(registerIndex);
                if (mappedIdx >= 0 && mappedIdx < registers_x64.Count)
                {
                    registerRow.value.Text = $"{HexPrefix}{HexUtils.ByteArrayToHexString(registers_x64[mappedIdx], false)}";
                }
            }
        }

        void UpdateStack()
        {
            if (traceRow == null || traceRow.Id - 1 < 0 || MemoryHandler.stacks.Count == 0)
                return;

            var stack = MemoryHandler.GetMemoryStateAt(traceRow.Id - 1, true);
            if (stack == null || stack.Count == 0)
                return;

            var flowDoc = new FlowDocument();
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            flowDoc.Blocks.Add(paragraph);

            ulong updated_rsp = 0;
            if (TraceHandler.Trace != null && traceRow.Id >= 0 && traceRow.Id < TraceHandler.Trace.Trace.Count)
            {
                var regs = TraceHandler.Trace.Trace[traceRow.Id].Regs;
                if (regs != null && regs.Count > 4)
                    updated_rsp = BitConverter.ToUInt64(regs[4], 0);
            }

            var memAccesses = traceRow.Mem;
            int alignment_counter = 0;
            var composedBuilder = new StringBuilder(16);
            ulong? blockStartAddress = null;
            int rsp_index = 0;

            Action<ulong, string> WriteLine = (address, data) =>
            {
                ulong startAddr = address - (ulong)Math.Max(0, alignment_counter - 1);
                bool isAccessLine = false;
                if (memAccesses != null)
                {
                    for (int m = 0; m < memAccesses.Count; m++)
                    {
                        if (memAccesses[m].Address >= startAddr && memAccesses[m].Address < address + 1)
                        {
                            isAccessLine = true;
                            break;
                        }
                    }
                }

                var addrBrush = isAccessLine ? Brushes.Red : Brushes.DarkGoldenrod;
                var dataBrush = isAccessLine ? Brushes.Red : Brushes.White;

                paragraph.Inlines.Add(new Run($"{HexPrefix}{address:X} : ") { Foreground = addrBrush });
                paragraph.Inlines.Add(new Run($"{HexPrefix}{data}") { Foreground = dataBrush });

                if (address == stack_alignment_base)
                {
                    paragraph.Inlines.Add(new Run(" (BASE)") { Foreground = Brushes.Red });
                }

                if (rsp_index != 0)
                {
                    paragraph.Inlines.Add(new Run($"  <--- RSP (past {rsp_index}th byte)") { Foreground = Brushes.Coral });
                }

                paragraph.Inlines.Add(new LineBreak());
                composedBuilder.Clear();
                alignment_counter = 0;
                rsp_index = 0;
            };

            KeyValuePair<ulong, byte> prev = default;
            bool hasPrev = false;

            foreach (var entry in stack)
            {
                if (!blockStartAddress.HasValue)
                {
                    blockStartAddress = entry.Key;
                }

                if (hasPrev && prev.Key - entry.Key > 1)
                {
                    if (composedBuilder.Length > 0)
                    {
                        WriteLine(prev.Key, composedBuilder.ToString());
                    }

                    long difference = (long)prev.Key - (long)entry.Key;
                    paragraph.Inlines.Add(new Run($"PADDING : 0x{difference - 1:X}") { Foreground = Brushes.Gray });
                    if (updated_rsp > entry.Key && updated_rsp < prev.Key)
                    {
                        paragraph.Inlines.Add(new Run($" <--- RSP (past byte 0x{prev.Key - updated_rsp:X})") { Foreground = Brushes.Coral });
                    }
                    paragraph.Inlines.Add(new LineBreak());
                    blockStartAddress = entry.Key;
                    composedBuilder.Clear();
                    alignment_counter = 0;
                    rsp_index = 0;
                }

                composedBuilder.Append(entry.Value.ToString("X2"));
                alignment_counter++;

                if (stack_alignment_base + (ulong)stack_alignment == entry.Key)
                {
                    if (alignment_counter > 0)
                    {
                        WriteLine(entry.Key, composedBuilder.ToString());
                    }
                    blockStartAddress = null;
                }
                else if (alignment_counter >= stack_alignment)
                {
                    if (blockStartAddress.HasValue)
                    {
                        WriteLine(blockStartAddress.Value, composedBuilder.ToString());
                    }
                    blockStartAddress = null;
                }

                if (entry.Key == updated_rsp)
                {
                    rsp_index = alignment_counter;
                }

                prev = entry;
                hasPrev = true;
            }

            if (composedBuilder.Length > 0 && hasPrev)
            {
                WriteLine(prev.Key, composedBuilder.ToString());
            }

            window.StackView.Document = flowDoc;
        }

        void UpdateHeap()
        {
            if (traceRow == null || traceRow.Id - 1 < 0 || MemoryHandler.heaps.Count == 0)
                return;

            var heap = MemoryHandler.GetMemoryStateAt(traceRow.Id - 1, false);
            if (heap == null || heap.Count == 0)
                return;

            var flowDoc = new FlowDocument();
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            flowDoc.Blocks.Add(paragraph);

            var memAccesses = traceRow.Mem;
            int alignment_counter = 0;
            var composedBuilder = new StringBuilder(16);
            ulong? blockStartAddress = null;

            Action<ulong, string> WriteLine = (address, data) =>
            {
                ulong startAddr = address - (ulong)Math.Max(0, alignment_counter - 1);
                bool isAccessLine = false;
                if (memAccesses != null)
                {
                    for (int m = 0; m < memAccesses.Count; m++)
                    {
                        if (memAccesses[m].Address >= startAddr && memAccesses[m].Address < address + 1)
                        {
                            isAccessLine = true;
                            break;
                        }
                    }
                }

                var addrBrush = isAccessLine ? Brushes.Red : Brushes.DarkGoldenrod;
                var dataBrush = isAccessLine ? Brushes.Red : Brushes.White;

                paragraph.Inlines.Add(new Run($"{HexPrefix}{address:X} : ") { Foreground = addrBrush });
                paragraph.Inlines.Add(new Run($"{HexPrefix}{data}") { Foreground = dataBrush });

                if (address == heap_alignment_base)
                {
                    paragraph.Inlines.Add(new Run(" (BASE)") { Foreground = Brushes.Red });
                }

                paragraph.Inlines.Add(new LineBreak());
                composedBuilder.Clear();
                alignment_counter = 0;
            };

            KeyValuePair<ulong, byte> prev = default;
            bool hasPrev = false;

            foreach (var entry in heap)
            {
                if (!blockStartAddress.HasValue)
                {
                    blockStartAddress = entry.Key;
                }

                if (hasPrev && prev.Key - entry.Key > 1)
                {
                    if (composedBuilder.Length > 0)
                    {
                        WriteLine(prev.Key, composedBuilder.ToString());
                    }

                    long difference = (long)prev.Key - (long)entry.Key;
                    paragraph.Inlines.Add(new Run($"PADDING : 0x{difference - 1:X}") { Foreground = Brushes.Gray });
                    paragraph.Inlines.Add(new LineBreak());
                    blockStartAddress = entry.Key;
                    composedBuilder.Clear();
                    alignment_counter = 0;
                }

                composedBuilder.Append(entry.Value.ToString("X2"));
                alignment_counter++;

                if (heap_alignment_base + (ulong)heap_alignment == entry.Key)
                {
                    if (alignment_counter > 0)
                    {
                        WriteLine(entry.Key, composedBuilder.ToString());
                    }
                    blockStartAddress = null;
                }
                else if (alignment_counter >= heap_alignment)
                {
                    if (blockStartAddress.HasValue)
                    {
                        WriteLine(blockStartAddress.Value, composedBuilder.ToString());
                    }
                    blockStartAddress = null;
                }

                prev = entry;
                hasPrev = true;
            }

            if (composedBuilder.Length > 0 && hasPrev)
            {
                WriteLine(prev.Key, composedBuilder.ToString());
            }

            window.HeapView.Document = flowDoc;
        }

        private void ToggleHide()
        {
            if (traceRow == null) return;

            if (!hidden)
            {
                var index = GraphHandler.uniqueIPAccesses?.FindIndex(x => x.Key == traceRow.Ip);
                if (index != null && index >= 0)
                {
                    var ids = GraphHandler.uniqueIPAccesses![(int)index].Value;
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
                if (index != null && index >= 0)
                {
                    var ids = GraphHandler.uniqueIPAccesses![(int)index].Value;
                    foreach (var id in ids)
                    {
                        hiddenRows.Remove(id);
                        DeObfus.deObHiddenRows.Remove(id);
                        foreach (var instruction in window.InstructionViewItems)
                        {
                            if (instruction.id.Text == id.ToString())
                            {
                                instruction.parent_panel.Opacity = 1.0;
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
                    if (nextIndex < 0 || nextIndex >= window.InstructionViewItems.Count)
                    {
                        if (window.ScrollControl(-direction))
                            FocusNextCommentBox(direction);
                        return;
                    }
                    if (window.InstructionViewItems[nextIndex] is WPF_TraceRow nextControl)
                    {
                        nextControl.comments.Focus();
                        nextControl.OnHover(null, null);
                    }
                    break;
                }
            }
        }

        private void PreviewOnKeyPressComments(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.H && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                ToggleHide();
            }

            if (e.Key == Key.Enter)
                FocusNextCommentBox(1);

            if (e.Key == Key.Down)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    FocusNextCommentBox(5);
                else
                    FocusNextCommentBox(1);
            }
            else if (e.Key == Key.Up)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    FocusNextCommentBox(-5);
                else
                    FocusNextCommentBox(-1);
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
            window.MnemonicReader.Content = mnemonic;
        }

        private void TextChangedComments(object sender, TextChangedEventArgs e)
        {
            if (traceRow == null) return;
            traceRow.comments = comments.Text;
            if (window.addressBasedCommenting && isFocused)
            {
                var index = GraphHandler.uniqueIPAccesses?.FindIndex(x => x.Key == traceRow.Ip);
                if (index != null && index >= 0 && TraceHandler.Trace != null)
                {
                    var ids = GraphHandler.uniqueIPAccesses![(int)index].Value;
                    foreach (var id in ids)
                    {
                        if (id >= 0 && id < TraceHandler.Trace.Trace.Count)
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

            if (textPointer == null || string.IsNullOrWhiteSpace(textPointer.GetTextInRun(LogicalDirection.Forward)))
                return;

            TextPointer wordStart = textPointer;
            TextPointer wordEnd = textPointer;

            while (wordStart != null && wordStart.GetPointerContext(LogicalDirection.Backward) == TextPointerContext.Text)
            {
                string textRun = wordStart.GetTextInRun(LogicalDirection.Backward);
                if (string.IsNullOrEmpty(textRun) || char.IsWhiteSpace(textRun.Last()))
                    break;
                wordStart = wordStart.GetPositionAtOffset(-1, LogicalDirection.Backward);
            }

            while (wordEnd != null && wordEnd.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                string textRun = wordEnd.GetTextInRun(LogicalDirection.Forward);
                if (string.IsNullOrEmpty(textRun) || char.IsWhiteSpace(textRun.First()))
                    break;
                wordEnd = wordEnd.GetPositionAtOffset(1, LogicalDirection.Forward);
            }

            if (wordStart != null && wordEnd != null)
            {
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

        public void SetColumnWidths(double w0, double w1, double w2, double w3, double w4)
        {
            if (w0 > 0) col0.Width = new GridLength(w0);
            if (w1 > 0) col1.Width = new GridLength(w1);
            if (w2 > 0) col2.Width = new GridLength(w2);
            if (w3 > 0) col3.Width = new GridLength(w3);
            if (w4 > 0) col4.Width = new GridLength(w4);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
            {
                string textToCopy = "";
                if (contextMenu.PlacementTarget is TextBlock sourceTextBlock)
                {
                    textToCopy = sourceTextBlock.Text;
                    if (textToCopy == "" && traceRow != null)
                    {
                        if (sourceTextBlock == disasm)
                            textToCopy = traceRow.Disasm;
                        else if (sourceTextBlock == changes && traceRow.Regchanges != null)
                            textToCopy = string.Concat(traceRow.Regchanges);
                    }
                }
                else if (contextMenu.PlacementTarget is TextBox tb)
                {
                    textToCopy = tb.SelectedText.Length > 0 ? tb.SelectedText : tb.Text;
                }
                else if (traceRow != null)
                {
                    textToCopy = traceRow.Disasm;
                }

                if (!string.IsNullOrEmpty(textToCopy))
                    Clipboard.SetText(textToCopy);
            }
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (traceRow == null) return;
            string changesText = traceRow.Regchanges != null ? string.Concat(traceRow.Regchanges) : "";
            Clipboard.SetText($"#: {id.Text} | {address.Text} | {traceRow.Disasm} | {changesText} | {comments.Text}");
        }

        private void MarkUnmarkAsBlockStart_Click(object sender, RoutedEventArgs e)
        {
            if (traceRow == null) return;
            if (traceRow.isBlockStart)
            {
                traceRow.isBlockStart = false;
                traceRow.block = "";
                block_panel.Visibility = Visibility.Collapsed;
                block.Text = "";
            }
            else
            {
                string defaultName = string.IsNullOrWhiteSpace(traceRow.block) ? $"Block {traceRow.Id}" : traceRow.block;
                var input = new InputDialog("Enter block name:", defaultName);
                input.ShowDialog();
                if (input.IsSuccess)
                {
                    string chosen = input.GetResult();
                    if (string.IsNullOrWhiteSpace(chosen))
                        chosen = defaultName;

                    traceRow.isBlockStart = true;
                    traceRow.block = chosen;
                    block.Text = chosen;
                    block_panel.Visibility = Visibility.Visible;

                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                    {
                        block.Focus();
                        block.SelectAll();
                    });
                }
            }
        }

        private void RenameBlock_Click(object sender, RoutedEventArgs e)
        {
            PromptRenameBlock();
        }

        public void PromptRenameBlock()
        {
            if (traceRow == null) return;
            if (traceRow.isBlockStart)
            {
                var input = new InputDialog("Enter block name:", traceRow.block);
                input.ShowDialog();
                if (input.IsSuccess)
                {
                    string chosen = input.GetResult();
                    traceRow.block = chosen;
                    block.Text = chosen;
                }
            }
            else
            {
                MarkUnmarkAsBlockStart_Click(this, new RoutedEventArgs());
            }
        }

        private void AddOrRemoveBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (traceRow == null) return;
            string idStr = traceRow.Id.ToString();
            var existing = window.BookmarkViewItems.FirstOrDefault(x => x.id.Text == idStr);
            if (existing != null)
            {
                window.BookmarkViewItems.Remove(existing);
            }
            else
            {
                window.BookmarkViewItems.Add(new WPF_Bookmark(idStr, traceRow.Ip.ToString(), traceRow.Disasm, traceRow.comments));
            }
        }

        private void ShowOrRemove_Click(object sender, RoutedEventArgs e)
        {
            ToggleHide();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            window.DimmingOverlay.Visibility = Visibility.Visible;
            if (sender is ContextMenu cm && traceRow != null)
            {
                var markItem = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == "MarkUnmarkAsBlockStart");
                var renameItem = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == "RenameBlockMenuItem");

                if (markItem != null)
                {
                    markItem.Header = traceRow.isBlockStart ? "Remove Block Start" : "Mark As Block Start...";
                }
                if (renameItem != null)
                {
                    renameItem.Visibility = traceRow.isBlockStart ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            window.DimmingOverlay.Visibility = Visibility.Collapsed;
        }

        private void block_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (traceRow == null || !traceRow.isBlockStart) return;
            PromptRenameBlock();
            e.Handled = true;
        }

        private void block_GotFocus(object sender, RoutedEventArgs e)
        {
            block.SelectAll();
        }

        private void block_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (traceRow != null && traceRow.isBlockStart)
                    traceRow.block = block.Text;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (traceRow != null)
                    block.Text = traceRow.block;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void block_LostFocus(object sender, RoutedEventArgs e)
        {
            if (traceRow != null && traceRow.isBlockStart)
            {
                if (string.IsNullOrWhiteSpace(block.Text))
                {
                    block.Text = $"Block_{traceRow.Id}";
                    traceRow.block = block.Text;
                }
            }
        }

        private void block_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (traceRow != null && traceRow.isBlockStart)
                traceRow.block = block.Text;
        }
    }
}