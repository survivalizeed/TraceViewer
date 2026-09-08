using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TraceViewer.Core;

namespace TraceViewer.UserWindows
{
    public static class DisasmHighlighter
    {
        public static readonly DependencyProperty DisasmTextProperty =
            DependencyProperty.RegisterAttached(
                "DisasmText",
                typeof(string),
                typeof(DisasmHighlighter),
                new PropertyMetadata(null, OnDisasmTextChanged));

        public static string? GetDisasmText(DependencyObject obj) => (string?)obj.GetValue(DisasmTextProperty);
        public static void SetDisasmText(DependencyObject obj, string? value) => obj.SetValue(DisasmTextProperty, value);

        private static void OnDisasmTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                string? text = e.NewValue as string;
                if (string.IsNullOrEmpty(text)) return;

                int start = 0;
                int len = text.Length;
                for (int i = 0; i < len; i++)
                {
                    char c = text[i];
                    if (c is ' ' or ',' or ':' or '[' or ']' or '*' or '+' or '-')
                    {
                        if (i > start)
                        {
                            string token = text.Substring(start, i - start);
                            textBlock.Inlines.Add(new Run(token) { Foreground = SyntaxHighlighter.GetBrush(token) });
                        }
                        string sep = text.Substring(i, 1);
                        textBlock.Inlines.Add(new Run(sep) { Foreground = SyntaxHighlighter.GetBrush(sep) });
                        start = i + 1;
                    }
                }
                if (start < len)
                {
                    string token = text.Substring(start, len - start);
                    textBlock.Inlines.Add(new Run(token) { Foreground = SyntaxHighlighter.GetBrush(token) });
                }
            }
        }
    }
}
