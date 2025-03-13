using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TraceViewer.UserWindows
{

    public partial class QWORDDialog : Window
    {
        private List<Label> byteLabels = new List<Label>();
        private List<Label> wordLabels = new List<Label>();
        private List<Label> dwordLabels = new List<Label>();

        private Label qwordLabel;

        private DisplayType displayType = DisplayType.Hex;

        private string value;

        public QWORDDialog(string value)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.KeyDown += QWORDDialog_KeyDown;
            this.value = value;
            CreateDynamicUI();
            Fill(value);
        }

        private void QWORDDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter)
            {
                this.Close();
            }
        }

        private void CreateDynamicUI()
        {
            // QWORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("QWORD", new string[] { "qword0" }, 1));

            // DWORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("DWORD", new string[] { "dword0", "dword1" }, 2));

            // WORD Row
            mainStackPanel.Children.Add(CreateLabeledRow("WORD", new string[] { "word0", "word1", "word2", "word3" }, 4));

            // BYTE Row
            mainStackPanel.Children.Add(CreateLabeledRow("BYTE", Enumerable.Range(0, 8).Select(i => "byte" + i).ToArray(), 8));

            dwordLabels[0].Opacity = 0.2;

            wordLabels[0].Opacity = 0.2;
            wordLabels[1].Opacity = 0.2;

            byteLabels[0].Opacity = 0.2;
            byteLabels[1].Opacity = 0.2;
            byteLabels[2].Opacity = 0.2;
            byteLabels[3].Opacity = 0.2;
            byteLabels[4].Opacity = 0.2;
            byteLabels[5].Opacity = 0.2;
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

            if (rowLabel == "QWORD") qwordLabel = labels[0];
            else if (rowLabel == "DWORD") dwordLabels = labels;
            else if (rowLabel == "WORD") wordLabels = labels;
            else if (rowLabel == "BYTE") byteLabels = labels;

            return rowGrid;
        }

        private void Fill(string value)
        {
            qwordLabel.Content = value.Substring(0, 16);

            for (int i = 0; i < 2; i++)
            {
                dwordLabels[i].Content = value.Substring(8 * i, 8);
            }

            for (int i = 0; i < 4; i++)
            {
                wordLabels[i].Content = value.Substring(4 * i, 4);
            }

            for (int i = 0; i < 8; i++)
            {
                byteLabels[i].Content = value.Substring(2 * i, 2);
            }
        }

        private void signed_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (displayType == DisplayType.Signed) return;
            displayType = DisplayType.Signed;
            Fill(value);
            qwordLabel.Content = Convert.ToInt64(qwordLabel.Content.ToString(), 16).ToString();
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
            if (displayType == DisplayType.Hex) return;
            displayType = DisplayType.Hex;
            Fill(value);
        }

        private void unsigned_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (displayType == DisplayType.Unsigned) return;
            displayType = DisplayType.Unsigned;
            Fill(value);
            qwordLabel.Content = Convert.ToUInt64(qwordLabel.Content.ToString(), 16).ToString();
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
            if (displayType == DisplayType.Float) return;
            displayType = DisplayType.Float;
            Fill(value);
            qwordLabel.Content = BitConverter.Int64BitsToDouble(Convert.ToInt64(qwordLabel.Content.ToString(), 16)).ToString(CultureInfo.InvariantCulture);
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
            this.Close();
        }
    }
}
