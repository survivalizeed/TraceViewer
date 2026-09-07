using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TraceViewer.UserControls
{
    public partial class InputDialog : Window
    {
        private string result = "";
        public bool IsSuccess { get; private set; } = false;

        public InputDialog(string Prompt, double? width = null, double? height = null)
        {
            InitializeComponent();
            this.PromptText.Text = Prompt;
            this.Owner = Application.Current.MainWindow;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Input.CaretBrush = Brushes.White;
            this.Input.Focus();
            if (width != null)
                this.Width = (double)width;
            if (height != null)
                this.Height = (double)height;
        }

        public InputDialog(string Prompt, string defaultText, double? width = null, double? height = null)
            : this(Prompt, width, height)
        {
            if (!string.IsNullOrEmpty(defaultText))
            {
                this.Input.Text = defaultText;
                this.Input.SelectAll();
            }
        }

        private void Ok_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                result = Input.Text;
                IsSuccess = true;
                this.Close();
            }
        }

        private void Cancel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IsSuccess = false;
                this.Close();
            }
        }

        public string GetResult()
        {
            string tmp = result;
            result = "";
            return tmp;
        }

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                result = Input.Text;
                IsSuccess = true;
                this.Close();
            }
            if (e.Key == Key.Escape)
            {
                IsSuccess = false;
                this.Close();
            }
        }
    }
}
