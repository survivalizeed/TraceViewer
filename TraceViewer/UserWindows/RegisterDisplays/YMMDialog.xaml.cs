using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TraceViewer.UserWindows
{
    public enum DisplayType
    {
        Hex,
        Signed,
        Unsigned,
        Float
    };

    public partial class YMMDialog : Window
    {
        private List<Label> byteLabels = new List<Label>();
        private List<Label> wordLabels = new List<Label>();
        private List<Label> dwordLabels = new List<Label>();
        private List<Label> qwordLabels = new List<Label>();

        private Label ymm0Label;
        private Label xmm0Label;
        private Label xmm1Label;

        private DisplayType displayType = DisplayType.Hex;

        public YMMDialog(string value)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            KeyDown += YMMDialog_KeyDown;
            CreateDynamicUI();
            Fill(value);
        }

        private void YMMDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter)
            {
                this.Close();
            }
        }

        private void CreateDynamicUI()
        {
            // YMM Row
            mainStackPanel.Children.Add(CreateLabeledRow("YMM", new string[] { "ymm0" }, 1));

            // XMM Row
            mainStackPanel.Children.Add(CreateLabeledRow("XMM", new string[] { "xmm0", "xmm1" }, 2));

            // QWORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("QWORD", new string[] { "qword0", "qword1", "qword2", "qword3" }, 4));

            // DWORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("DWORD", new string[] { "dword0", "dword1", "dword2", "dword3", "dword4", "dword5", "dword6", "dword7" }, 8));

            // WORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("WORD", Enumerable.Range(0, 16).Select(i => "word" + i).ToArray(), 16));

            // BYTE Row
            mainStackPanel.Children.Add(CreateLabeledRow("BYTE", Enumerable.Range(0, 32).Select(i => "byte" + i).ToArray(), 32));
        }

        private Grid CreateLabeledRow(string rowLabel, string[] labelNames, int columnCount)
        {
            Grid rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            for (int i = 0; i < columnCount; i++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            Label rowHeaderLabel = new Label
            {
                Content = rowLabel,
                Style = (Style)FindResource("memory_view_label"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetColumn(rowHeaderLabel, 0);
            rowGrid.Children.Add(rowHeaderLabel);

            List<Label> labels = new List<Label>();
            for (int i = 0; i < labelNames.Length; i++)
            {
                Brush color = Brushes.White;
                if (labelNames[i].StartsWith("ymm"))
                    color = (SolidColorBrush)FindResource("ymm");
                if (labelNames[i].StartsWith("xmm"))
                    color = (SolidColorBrush)FindResource("xmm");
                if (labelNames[i].StartsWith("qword"))
                    color = (SolidColorBrush)FindResource("qword");
                if (labelNames[i].StartsWith("dword"))
                    color = (SolidColorBrush)FindResource("dword");
                if (labelNames[i].StartsWith("word"))
                    color = (SolidColorBrush)FindResource("word");
                if (labelNames[i].StartsWith("byte"))
                    color = (SolidColorBrush)FindResource("byte");

                Label label = new Label
                {
                    Name = labelNames[i],
                    Content = labelNames[i].Substring(0, labelNames[i].Length - (i.ToString().Length)),
                    BorderBrush = color,
                    BorderThickness = new Thickness(0.5),
                    Style = (Style)FindResource("memory_view_label"),
                    Foreground = color,
                    Margin = new Thickness(0, 5, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(label, i + 1);
                rowGrid.Children.Add(label);
                labels.Add(label);
            }

            if (rowLabel == "YMM") ymm0Label = labels[0];
            else if (rowLabel == "XMM") { xmm0Label = labels[0]; xmm1Label = labels[1]; }
            else if (rowLabel == "QWORD") qwordLabels = labels;
            else if (rowLabel == "DWORD") dwordLabels = labels;
            else if (rowLabel == "WORD") wordLabels = labels;
            else if (rowLabel == "BYTE") byteLabels = labels;

            return rowGrid;
        }

        private void Fill(string value)
        {
            ymm0Label.Content = value.Substring(0, 64);
            xmm0Label.Content = value.Substring(0, 32);
            xmm1Label.Content = value.Substring(32, 32);

            for (int i = 0; i < 4; i++)
            {
                qwordLabels[i].Content = value.Substring(16 * i, 16);
            }

            for (int i = 0; i < 8; i++)
            {
                dwordLabels[i].Content = value.Substring(8 * i, 8);
            }

            for (int i = 0; i < 16; i++)
            {
                wordLabels[i].Content = value.Substring(4 * i, 4);
            }

            for (int i = 0; i < 32; i++)
            {
                byteLabels[i].Content = value.Substring(2 * i, 2);
            }
        }

        private void signed_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (displayType == DisplayType.Signed) return;
            displayType = DisplayType.Signed;
            Fill(ymm0Label.Content.ToString());
            foreach (var qword in qwordLabels)
            {
                qword.Content = Convert.ToInt64(qword.Content.ToString(), 16).ToString();
            }
            foreach (var dword in dwordLabels)
            {
                dword.Content = Convert.ToInt32(dword.Content.ToString(), 16).ToString();
            }
            foreach (var word in wordLabels)
            {
                word.Content = Convert.ToInt16(word.Content.ToString(), 16).ToString();
            }
            foreach (var byte_ in byteLabels)
            {
                byte_.Content = Convert.ToSByte(byte_.Content.ToString(), 16).ToString();
            }
        }

        private void hexadecimal_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (displayType == DisplayType.Hex) return;
            displayType = DisplayType.Hex;
            Fill(ymm0Label.Content.ToString());
        }

        private void unsigned_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (displayType == DisplayType.Unsigned) return;
            displayType = DisplayType.Unsigned;
            Fill(ymm0Label.Content.ToString());
            foreach (var qword in qwordLabels)
            {
                qword.Content = Convert.ToUInt64(qword.Content.ToString(), 16).ToString();
            }
            foreach (var dword in dwordLabels)
            {
                dword.Content = Convert.ToUInt32(dword.Content.ToString(), 16).ToString();
            }
            foreach (var word in wordLabels)
            {
                word.Content = Convert.ToUInt16(word.Content.ToString(), 16).ToString();
            }
            foreach (var byte_ in byteLabels)
            {
                byte_.Content = Convert.ToByte(byte_.Content.ToString(), 16).ToString();
            }
        }

        private void float_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (displayType == DisplayType.Float) return;
            displayType = DisplayType.Float;
            Fill(ymm0Label.Content.ToString());
            foreach (var qword in qwordLabels)
            {
                qword.Content = BitConverter.Int64BitsToDouble(Convert.ToInt64(qword.Content.ToString(), 16)).ToString(CultureInfo.InvariantCulture);
            }
            foreach (var dword in dwordLabels)
            {
                dword.Content = BitConverter.ToSingle(BitConverter.GetBytes(Convert.ToInt32(dword.Content.ToString(), 16)), 0).ToString(CultureInfo.InvariantCulture);
            }
            foreach (var word in wordLabels)
            {
                word.Content = Convert.ToUInt16(word.Content.ToString(), 16).ToString();
            }
            foreach (var byte_ in byteLabels)
            {
                byte_.Content = Convert.ToByte(byte_.Content.ToString(), 16).ToString();
            }
        }

        private void ok_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) 
                this.Close();
        }
    }
}
