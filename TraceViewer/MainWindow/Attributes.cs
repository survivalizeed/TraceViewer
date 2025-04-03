using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace TraceViewer
{
    public partial class MainWindow : Window
    {
        // MainWindow
        private bool _toggleFpu = true;

        public bool _toggleMnemonic = true;
        public bool _toggleStack = true;

        private string _current_project_path = "";
        private string original_title = "survivalizeed's Trace Viewer";
        public ScrollViewer InstructionsScrollViewer { get; private set; }
        public ObservableCollection<WPF_TraceRow> InstructionViewItems = new();
        public ObservableCollection<WPF_RegisterRow> RegisterViewItems = new();


        // Appearance
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;


        // Graph
        private List<Node> nodes = new List<Node>();
        private Node currentlyDraggingNode = null;
        private Point dragStartPoint;
        private Point initialNodePosition;
        private Node selectedNode = null;
        private Brush defaultLineBrush = Brushes.White;
        private Brush defaultArrowFillBrush = Brushes.Coral;
        private Brush defaultArrowStrokeBrush = Brushes.Coral;
        private Brush highlightBrush = Brushes.Coral;

        private const double ConnectionOffset = 3.0;
        private const double Epsilon = 0.1;
        private const double ArrowSpacing = 120.0f;

        private List<(int, int)> connections;
        public IReadOnlyList<Node> Nodes => nodes.AsReadOnly();


        // UIState
        private bool graphGenerated = false;

        private readonly DropShadowEffect glowEffect = new DropShadowEffect
        {
            Color = Colors.White,
            BlurRadius = 10,
            ShadowDepth = 0,
            Opacity = 0.8
        };

    }
}
