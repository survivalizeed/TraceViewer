using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using TraceViewer.Core.Analysis;

namespace TraceViewer
{
    public class Node : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private string _text = "";
        private double _width = 150;
        private double _height = 52;

        public BasicBlock? Block { get; set; }
        public int BlockId => Block?.Id ?? 0;
        public ulong StartIp => Block?.StartIp ?? 0;
        public int InstructionCount => Block?.InstructionCount ?? 0;
        public int ExecutionCount => Block?.ExecutionCount ?? 1;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public double X
        {
            get => _x;
            set
            {
                if (_x != value)
                {
                    _x = value;
                    OnPropertyChanged(nameof(X));
                    OnPropertyChanged(nameof(Left));
                    OnPropertyChanged(nameof(CenterPoint));
                }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (_y != value)
                {
                    _y = value;
                    OnPropertyChanged(nameof(Y));
                    OnPropertyChanged(nameof(Top));
                    OnPropertyChanged(nameof(CenterPoint));
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    OnPropertyChanged(nameof(Width));
                    OnPropertyChanged(nameof(CenterPoint));
                }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    OnPropertyChanged(nameof(Height));
                    OnPropertyChanged(nameof(CenterPoint));
                }
            }
        }

        public double Left => X;
        public double Top => Y;
        public Point CenterPoint => new Point(X + Width / 2, Y + Height / 2);

        public List<NodeConnection> Connections { get; } = [];
        public List<NodeConnection> IncomingConnections { get; } = [];
    }

    public class NodeConnection
    {
        public Node StartNode { get; }
        public Node EndNode { get; }
        public EdgeType Type { get; }
        public int ExecutionCount { get; }

        public NodeConnection(Node start, Node end, EdgeType type = EdgeType.Normal, int count = 1)
        {
            StartNode = start;
            EndNode = end;
            Type = type;
            ExecutionCount = count;
        }

        public override bool Equals(object? obj) =>
            obj is NodeConnection other && StartNode == other.StartNode && EndNode == other.EndNode && Type == other.Type;

        public override int GetHashCode() => HashCode.Combine(StartNode, EndNode, Type);
    }

    public partial class MainWindow : Window
    {
        private Point _panStartPoint;
        private bool _isPanning = false;
        private bool _hasPanned = false;

        private Brush GetViewBorderBrush() => TryFindResource("ViewBorderBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17));
        private Brush GetViewBorderHoverBrush() => TryFindResource("ViewBorderHoverBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
        private Brush GetViewBorderBrighterBrush() => TryFindResource("ViewBorderBrighterBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        private Brush GetPrimaryForegroundBrush() => TryFindResource("PrimaryForegroundBrush") as Brush ?? Brushes.White;
        private Brush GetHighlightBrush() => highlightBrush ?? Brushes.Coral;

        private static DropShadowEffect CreateCoralGlow() => new()
        {
            Color = Color.FromRgb(0xFF, 0x7F, 0x50), // Coral
            BlurRadius = 12,
            ShadowDepth = 0,
            Opacity = 0.75
        };

        public void RenderGraph(List<BasicBlock> blockList, List<BlockConnection> connectionList)
        {
            GraphViewClear();

            var newNodes = new List<Node>(blockList.Count);
            for (int i = 0; i < blockList.Count; i++)
            {
                var block = blockList[i];
                var node = new Node
                {
                    Block = block,
                    Text = block.Title,
                    Height = block.Height,
                    Width = block.Width,
                    X = block.X,
                    Y = block.Y
                };
                newNodes.Add(node);
                AddNode(node);
            }

            var blockIdToNode = newNodes.ToDictionary(n => n.BlockId);
            foreach (var conn in connectionList)
            {
                if (blockIdToNode.TryGetValue(conn.From.Id, out var fromNode) &&
                    blockIdToNode.TryGetValue(conn.To.Id, out var toNode))
                {
                    ConnectNodes(fromNode, toNode, conn.Type, conn.ExecutionCount);
                }
            }
        }

        public void AddNode(Node node, Node? connectTo = null)
        {
            if (node == null) return;
            if (nodes.Contains(node)) return;

            nodes.Add(node);
            AddNodeToCanvas(node);

            if (connectTo != null && nodes.Contains(connectTo))
            {
                ConnectNodes(connectTo, node);
            }
        }

        private void AddNodeToCanvas(Node node)
        {
            if (GraphViewCanvas == null) return;

            var container = new Border
            {
                Width = node.Width,
                Height = node.Height,
                Background = GetViewBorderBrush(),
                BorderBrush = GetViewBorderHoverBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                DataContext = node,
                Tag = node,
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var mainGrid = new Grid
            {
                Margin = new Thickness(8, 6, 8, 6)
            };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top row: Block title (left) & Execution count badge (right)
            var topDock = new DockPanel { LastChildFill = false };

            string titleStr = node.Block != null ? $"Block {node.Block.Id}" : node.Text;
            var titleBlock = new TextBlock
            {
                Text = titleStr,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = GetPrimaryForegroundBrush(),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            topDock.Children.Add(titleBlock);

            int execCount = node.ExecutionCount;
            var execBlock = new TextBlock
            {
                Text = execCount > 1 ? $"{execCount:N0}x" : "1x",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = execCount > 1 ? FontWeights.Bold : FontWeights.Normal,
                Foreground = execCount > 1 ? GetHighlightBrush() : GetViewBorderBrighterBrush(),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(execBlock, Dock.Right);
            topDock.Children.Add(execBlock);

            Grid.SetRow(topDock, 0);
            mainGrid.Children.Add(topDock);

            // Bottom row: Start address & Instruction count
            string ipHex = node.StartIp.ToString("X");
            string shortIp = ipHex.Length > 8 ? ipHex[^8..] : ipHex;
            int instrCount = node.InstructionCount;

            var bottomBlock = new TextBlock
            {
                Text = $"0x{shortIp} • {instrCount} instrs",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9.5,
                Foreground = GetViewBorderBrighterBrush(),
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(bottomBlock, 1);
            mainGrid.Children.Add(bottomBlock);

            container.Child = mainGrid;

            // Events
            container.MouseDown += NodeElement_MouseDown;
            container.MouseMove += NodeElement_MouseMove;
            container.MouseUp += NodeElement_MouseUp;
            container.MouseEnter += NodeElement_MouseEnter;
            container.MouseLeave += NodeElement_MouseLeave;

            container.SetBinding(Canvas.LeftProperty, new Binding("Left") { Mode = BindingMode.OneWay });
            container.SetBinding(Canvas.TopProperty, new Binding("Top") { Mode = BindingMode.OneWay });

            GraphViewCanvas.Children.Add(container);
            Panel.SetZIndex(container, 1);
        }

        private void NodeElement_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is Node n && n != selectedNode)
            {
                border.BorderBrush = Brushes.White;
            }
        }

        private void NodeElement_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is Node n && n != selectedNode)
            {
                if (selectedNode != null)
                {
                    var successors = new HashSet<Node>(selectedNode.Connections.Select(c => c.EndNode));
                    var predecessors = new HashSet<Node>(selectedNode.IncomingConnections.Select(c => c.StartNode));
                    if (successors.Contains(n))
                        border.BorderBrush = Brushes.White;
                    else if (predecessors.Contains(n))
                        border.BorderBrush = GetViewBorderHoverBrush();
                    else
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                }
                else
                {
                    border.BorderBrush = GetViewBorderHoverBrush();
                }
            }
        }

        public void ConnectNodes(Node node1, Node node2, EdgeType type = EdgeType.Normal, int executionCount = 1)
        {
            if (node1 == null || node2 == null) return;
            if (!nodes.Contains(node1) || !nodes.Contains(node2)) return;

            var existing = node1.Connections.FirstOrDefault(c => c.EndNode == node2 && c.Type == type);
            if (existing == null)
            {
                var conn = new NodeConnection(node1, node2, type, executionCount);
                node1.Connections.Add(conn);
                node2.IncomingConnections.Add(conn);
                DrawConnection(node1, node2, type, executionCount);
            }
        }

        private void DrawConnection(Node node1, Node node2, EdgeType type, int executionCount)
        {
            if (GraphViewCanvas == null) return;

            var conn = new NodeConnection(node1, node2, type, executionCount);

            bool isSelfLoop = (node1 == node2);
            bool isBackEdge = !isSelfLoop && (node2.Y < node1.Y - 10);
            bool isHorizontal = !isSelfLoop && !isBackEdge && Math.Abs(node2.Y - node1.Y) <= 10;

            Point startPoint;
            Point endPoint;
            PointCollection arrowPoints;

            var pathGeometry = new PathGeometry();
            var figure = new PathFigure { IsClosed = false };

            if (isSelfLoop)
            {
                startPoint = new Point(node1.X + node1.Width, node1.Y + node1.Height * 0.75);
                endPoint = new Point(node1.X + node1.Width, node1.Y + node1.Height * 0.25);
                figure.StartPoint = startPoint;

                Point cp1 = new Point(node1.X + node1.Width + 35, node1.Y + node1.Height * 0.90);
                Point cp2 = new Point(node1.X + node1.Width + 35, node1.Y + node1.Height * 0.10);
                figure.Segments.Add(new BezierSegment(cp1, cp2, endPoint, true));

                arrowPoints = new PointCollection
                {
                    new Point(endPoint.X, endPoint.Y),
                    new Point(endPoint.X + 7, endPoint.Y - 4),
                    new Point(endPoint.X + 7, endPoint.Y + 4)
                };
            }
            else if (isBackEdge)
            {
                // Loop back-edge: exit cleanly from top of node1 and enter bottom of node2
                startPoint = new Point(node1.X + node1.Width * 0.70, node1.Y);
                endPoint = new Point(node2.X + node2.Width * 0.30, node2.Y + node2.Height);
                figure.StartPoint = startPoint;

                double dy = Math.Min(90, (startPoint.Y - endPoint.Y) * 0.5);
                Point cp1 = new Point(startPoint.X, startPoint.Y - dy);
                Point cp2 = new Point(endPoint.X, endPoint.Y + dy);
                figure.Segments.Add(new BezierSegment(cp1, cp2, endPoint, true));

                arrowPoints = new PointCollection
                {
                    new Point(endPoint.X, endPoint.Y),
                    new Point(endPoint.X - 4, endPoint.Y + 7),
                    new Point(endPoint.X + 4, endPoint.Y + 7)
                };
            }
            else if (isHorizontal)
            {
                bool leftToRight = node2.X > node1.X;
                startPoint = new Point(node1.X + (leftToRight ? node1.Width : 0), node1.Y + node1.Height * 0.5);
                endPoint = new Point(node2.X + (leftToRight ? 0 : node2.Width), node2.Y + node2.Height * 0.5);
                figure.StartPoint = startPoint;

                double dx = Math.Min(40, Math.Abs(endPoint.X - startPoint.X) * 0.3);
                Point cp1 = new Point(startPoint.X + (leftToRight ? dx : -dx), startPoint.Y - 20);
                Point cp2 = new Point(endPoint.X + (leftToRight ? -dx : dx), endPoint.Y - 20);
                figure.Segments.Add(new BezierSegment(cp1, cp2, endPoint, true));

                arrowPoints = new PointCollection
                {
                    new Point(endPoint.X, endPoint.Y),
                    new Point(endPoint.X + (leftToRight ? -7 : 7), endPoint.Y - 4),
                    new Point(endPoint.X + (leftToRight ? -7 : 7), endPoint.Y + 4)
                };
            }
            else
            {
                // Normal downward edge
                startPoint = new Point(node1.X + node1.Width * 0.5, node1.Y + node1.Height);
                endPoint = new Point(node2.X + node2.Width * 0.5, node2.Y);
                figure.StartPoint = startPoint;

                double dy = Math.Min(80, (endPoint.Y - startPoint.Y) * 0.5);
                Point cp1 = new Point(startPoint.X, startPoint.Y + dy);
                Point cp2 = new Point(endPoint.X, endPoint.Y - dy);
                figure.Segments.Add(new BezierSegment(cp1, cp2, endPoint, true));

                arrowPoints = new PointCollection
                {
                    new Point(endPoint.X, endPoint.Y),
                    new Point(endPoint.X - 4, endPoint.Y - 7),
                    new Point(endPoint.X + 4, endPoint.Y - 7)
                };
            }

            pathGeometry.Figures.Add(figure);

            var strokeBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            var arrowBrush = GetHighlightBrush();

            string tooltip = executionCount > 1
                ? $"Transition • {executionCount:N0}x"
                : "Transition";

            var path = new Path
            {
                Data = pathGeometry,
                Stroke = strokeBrush,
                StrokeThickness = 1.5,
                Opacity = 0.55,
                DataContext = conn,
                ToolTip = tooltip,
                Tag = "GraphEdge"
            };

            GraphViewCanvas.Children.Add(path);
            Panel.SetZIndex(path, 0);

            var arrow = new Polygon
            {
                Points = arrowPoints,
                Fill = arrowBrush,
                Stroke = arrowBrush,
                StrokeThickness = 1,
                Opacity = 0.65,
                DataContext = conn,
                ToolTip = tooltip,
                Tag = "GraphArrow"
            };

            GraphViewCanvas.Children.Add(arrow);
            Panel.SetZIndex(arrow, 0);
        }

        private void NodeElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Node node)
            {
                if (e.ClickCount == 2)
                {
                    int targetRow = -1;
                    if (node.Block != null && node.Block.FirstTraceRowId >= 0)
                    {
                        targetRow = node.Block.FirstTraceRowId;
                    }
                    else if (GraphHandler.blocks != null && node.BlockId < GraphHandler.blocks.Count && GraphHandler.uniqueIPAccesses != null)
                    {
                        var b = GraphHandler.blocks[node.BlockId];
                        if (b.startIndex < GraphHandler.uniqueIPAccesses.Count)
                        {
                            var ids = GraphHandler.uniqueIPAccesses[b.startIndex].Value;
                            if (ids.Count > 0) targetRow = ids[0];
                        }
                    }

                    if (targetRow >= 0)
                    {
                        DisasmViewButton_MouseDown(null, null);
                        ScrollTo(targetRow);
                        e.Handled = true;
                        return;
                    }
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (selectedNode != null && selectedNode != node)
                    {
                        ResetNodeAndConnectionStyles(selectedNode);
                    }
                    selectedNode = node;
                    ChangeNodeAndConnectionStyles(selectedNode);

                    currentlyDraggingNode = node;
                    dragStartPoint = e.GetPosition(GraphViewCanvas);
                    initialNodePosition = new Point(currentlyDraggingNode.X, currentlyDraggingNode.Y);
                    element.CaptureMouse();
                    Panel.SetZIndex(element, 10);
                    e.Handled = true;
                }
            }
        }

        private void NodeElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentlyDraggingNode != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(GraphViewCanvas);
                double deltaX = currentPosition.X - dragStartPoint.X;
                double deltaY = currentPosition.Y - dragStartPoint.Y;

                currentlyDraggingNode.X = Math.Max(10, initialNodePosition.X + deltaX);
                currentlyDraggingNode.Y = Math.Max(10, initialNodePosition.Y + deltaY);

                RecalculateConnectionsForNode(currentlyDraggingNode);
                e.Handled = true;
            }
        }

        private void NodeElement_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (currentlyDraggingNode != null && sender is FrameworkElement element)
            {
                element.ReleaseMouseCapture();
                Panel.SetZIndex(element, 1);
                currentlyDraggingNode = null;
                e.Handled = true;
            }
        }

        private void ResetNodeAndConnectionStyles(Node? node = null)
        {
            if (GraphViewCanvas == null) return;

            var defaultBorderBrush = GetViewBorderHoverBrush();

            foreach (var child in GraphViewCanvas.Children.OfType<Border>().Where(b => b.Tag is Node))
            {
                child.BorderBrush = defaultBorderBrush;
                child.BorderThickness = new Thickness(1);
                child.Opacity = 1.0;
                child.Effect = null;
            }

            var lineBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            var arrowBrush = GetHighlightBrush();

            foreach (var path in GraphViewCanvas.Children.OfType<Path>())
            {
                path.Stroke = lineBrush;
                path.Opacity = 0.55;
                path.StrokeThickness = 1.5;
            }
            foreach (var arrow in GraphViewCanvas.Children.OfType<Polygon>())
            {
                arrow.Fill = arrowBrush;
                arrow.Stroke = arrowBrush;
                arrow.Opacity = 0.65;
            }
        }

        private void ChangeNodeAndConnectionStyles(Node node)
        {
            if (GraphViewCanvas == null || node == null) return;

            var successors = new HashSet<Node>(node.Connections.Select(c => c.EndNode));
            var predecessors = new HashSet<Node>(node.IncomingConnections.Select(c => c.StartNode));

            var hlBrush = GetHighlightBrush();
            var hoverBrush = GetViewBorderHoverBrush();
            var dimmedBorder = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));

            foreach (var child in GraphViewCanvas.Children.OfType<Border>())
            {
                if (child.Tag is Node n)
                {
                    if (n == node)
                    {
                        child.BorderBrush = hlBrush; // Coral
                        child.BorderThickness = new Thickness(2);
                        child.Opacity = 1.0;
                        child.Effect = CreateCoralGlow();
                    }
                    else if (successors.Contains(n))
                    {
                        // Successor: Bright white border
                        child.BorderBrush = Brushes.White;
                        child.BorderThickness = new Thickness(1.5);
                        child.Opacity = 1.0;
                        child.Effect = null;
                    }
                    else if (predecessors.Contains(n))
                        // Predecessor: BorderHoverBrush
                    {
                        child.BorderBrush = hoverBrush;
                        child.BorderThickness = new Thickness(1.5);
                        child.Opacity = 0.9;
                        child.Effect = null;
                    }
                    else
                    {
                        child.BorderBrush = dimmedBorder;
                        child.BorderThickness = new Thickness(1);
                        child.Opacity = 0.35;
                        child.Effect = null;
                    }
                }
            }

            var activeConnections = new HashSet<NodeConnection>(node.Connections.Concat(node.IncomingConnections));

            foreach (var path in GraphViewCanvas.Children.OfType<Path>())
            {
                if (path.DataContext is NodeConnection conn)
                {
                    if (activeConnections.Contains(conn))
                    {
                        path.Stroke = hlBrush; // Coral
                        path.Opacity = 1.0;
                        path.StrokeThickness = 2.5;
                    }
                    else
                    {
                        path.Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                        path.Opacity = 0.12;
                        path.StrokeThickness = 1.0;
                    }
                }
            }

            foreach (var arrow in GraphViewCanvas.Children.OfType<Polygon>())
            {
                if (arrow.DataContext is NodeConnection conn)
                {
                    if (activeConnections.Contains(conn))
                    {
                        arrow.Fill = hlBrush; // Coral
                        arrow.Stroke = hlBrush;
                        arrow.Opacity = 1.0;
                    }
                    else
                    {
                        arrow.Opacity = 0.12;
                    }
                }
            }
        }

        private void RecalculateConnectionsForNode(Node node)
        {
            if (GraphViewCanvas == null || node == null) return;

            var relevantConnections = node.Connections.Concat(node.IncomingConnections).Distinct().ToList();

            var pathsToRemove = GraphViewCanvas.Children.OfType<Path>()
                .Where(p => p.DataContext is NodeConnection nc && relevantConnections.Contains(nc)).ToList();
            var arrowsToRemove = GraphViewCanvas.Children.OfType<Polygon>()
                .Where(a => a.DataContext is NodeConnection nc && relevantConnections.Contains(nc)).ToList();

            foreach (var p in pathsToRemove) GraphViewCanvas.Children.Remove(p);
            foreach (var a in arrowsToRemove) GraphViewCanvas.Children.Remove(a);

            foreach (var conn in relevantConnections)
            {
                DrawConnection(conn.StartNode, conn.EndNode, conn.Type, conn.ExecutionCount);
            }

            if (selectedNode != null)
            {
                ChangeNodeAndConnectionStyles(selectedNode);
            }
        }

        public void GraphViewClear()
        {
            nodes?.Clear();
            connections?.Clear();
            selectedNode = null;
            currentlyDraggingNode = null;
            if (GraphViewCanvas != null)
            {
                GraphViewCanvas.Children.Clear();
            }
        }

        // --- Interactive Pan & Zoom Handlers ---

        private void GraphViewContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the click target is within a node
            bool isNodeClick = false;
            DependencyObject? curr = e.OriginalSource as DependencyObject;
            while (curr != null && curr != GraphViewContainer)
            {
                if (curr is FrameworkElement fe && fe.DataContext is Node)
                {
                    isNodeClick = true;
                    break;
                }
                curr = VisualTreeHelper.GetParent(curr);
            }

            if (!isNodeClick && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
            {
                _isPanning = true;
                _hasPanned = false;
                _panStartPoint = e.GetPosition(GraphViewContainer);
                GraphViewContainer.CaptureMouse();
                GraphViewContainer.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        private void GraphViewContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point current = e.GetPosition(GraphViewContainer);
                double dx = current.X - _panStartPoint.X;
                double dy = current.Y - _panStartPoint.Y;

                if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
                {
                    _hasPanned = true;
                }

                _panStartPoint = current;

                GraphTranslateTransform.X += dx;
                GraphTranslateTransform.Y += dy;
                e.Handled = true;
            }
        }

        private void GraphViewContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
            {
                _isPanning = false;
                GraphViewContainer.ReleaseMouseCapture();
                GraphViewContainer.Cursor = Cursors.Arrow;

                // Only if user simply clicked empty canvas without dragging/panning at all
                if (!_hasPanned && e.ChangedButton == MouseButton.Left)
                {
                    if (selectedNode != null)
                    {
                        ResetNodeAndConnectionStyles(selectedNode);
                        selectedNode = null;
                    }
                }

                e.Handled = true;
            }
        }

        private void GraphViewContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mousePos = e.GetPosition(GraphViewContainer);
            double zoomFactor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            ZoomAtPoint(mousePos, zoomFactor);
            e.Handled = true;
        }

        private void ZoomAtPoint(Point center, double zoomFactor)
        {
            double currentScale = GraphScaleTransform.ScaleX;
            double newScale = Math.Clamp(currentScale * zoomFactor, 0.15, 3.0);
            double actualFactor = newScale / currentScale;

            GraphTranslateTransform.X = center.X - (center.X - GraphTranslateTransform.X) * actualFactor;
            GraphTranslateTransform.Y = center.Y - (center.Y - GraphTranslateTransform.Y) * actualFactor;

            GraphScaleTransform.ScaleX = newScale;
            GraphScaleTransform.ScaleY = newScale;
        }

        public void FitToView()
        {
            if (nodes == null || nodes.Count == 0) return;

            double minX = nodes.Min(n => n.X);
            double minY = nodes.Min(n => n.Y);
            double maxX = nodes.Max(n => n.X + n.Width);
            double maxY = nodes.Max(n => n.Y + n.Height);

            double graphW = maxX - minX;
            double graphH = maxY - minY;
            if (graphW <= 0 || graphH <= 0) return;

            double viewW = GraphViewContainer.ActualWidth > 0 ? GraphViewContainer.ActualWidth : 1200;
            double viewH = GraphViewContainer.ActualHeight > 0 ? GraphViewContainer.ActualHeight : 800;

            double margin = 50;
            double scaleX = (viewW - margin * 2) / graphW;
            double scaleY = (viewH - margin * 2) / graphH;
            double scale = Math.Clamp(Math.Min(scaleX, scaleY), 0.2, 1.1);

            GraphScaleTransform.ScaleX = scale;
            GraphScaleTransform.ScaleY = scale;

            GraphTranslateTransform.X = (viewW - graphW * scale) / 2 - minX * scale;
            GraphTranslateTransform.Y = (viewH - graphH * scale) / 2 - minY * scale;
        }

        // --- Timeline Interaction ---

        public void InitializeTimeline(List<(int, int)> timelineConnections)
        {
            this.connections = timelineConnections;

            Timeline.Maximum = Math.Max(0, timelineConnections.Count - 1);
            Timeline.Minimum = 0;
            Timeline.IsSnapToTickEnabled = true;
            Timeline.TickFrequency = 1;
            Timeline.Value = 0;

            Timeline.ValueChanged -= Timeline_ValueChanged;
            Timeline.ValueChanged += Timeline_ValueChanged;
        }

        private void Timeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int idx = (int)e.NewValue;
            if (connections != null && idx < connections.Count && idx >= 0)
            {
                var (fromId, toId) = connections[idx];
                var blockMap = nodes.ToDictionary(n => n.BlockId);
                if (blockMap.TryGetValue(fromId, out var fromNode) && blockMap.TryGetValue(toId, out var toNode))
                {
                    HighlightTimelineStep(fromNode, toNode);
                }
            }
        }

        private void HighlightTimelineStep(Node fromNode, Node toNode)
        {
            if (GraphViewCanvas == null) return;

            if (selectedNode != null && selectedNode != toNode)
            {
                ResetNodeAndConnectionStyles(selectedNode);
            }
            selectedNode = toNode;

            var hlBrush = GetHighlightBrush();
            var dimmedBorder = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));

            foreach (var child in GraphViewCanvas.Children.OfType<Border>().Where(b => b.Tag is Node))
            {
                var n = (Node)child.Tag;
                if (n == toNode)
                {
                    child.BorderBrush = hlBrush; // Coral
                    child.BorderThickness = new Thickness(2);
                    child.Opacity = 1.0;
                    child.Effect = CreateCoralGlow();
                }
                else if (n == fromNode)
                {
                    child.BorderBrush = Brushes.White;
                    child.BorderThickness = new Thickness(1.5);
                    child.Opacity = 0.95;
                    child.Effect = null;
                }
                else
                {
                    child.BorderBrush = dimmedBorder;
                    child.BorderThickness = new Thickness(1);
                    child.Opacity = 0.35;
                    child.Effect = null;
                }
            }

            foreach (var path in GraphViewCanvas.Children.OfType<Path>())
            {
                if (path.DataContext is NodeConnection conn)
                {
                    if (conn.StartNode == fromNode && conn.EndNode == toNode)
                    {
                        path.Stroke = hlBrush;
                        path.Opacity = 1.0;
                        path.StrokeThickness = 3.0;
                    }
                    else
                    {
                        path.Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                        path.Opacity = 0.10;
                        path.StrokeThickness = 1.0;
                    }
                }
            }

            foreach (var arrow in GraphViewCanvas.Children.OfType<Polygon>())
            {
                if (arrow.DataContext is NodeConnection conn)
                {
                    if (conn.StartNode == fromNode && conn.EndNode == toNode)
                    {
                        arrow.Fill = hlBrush;
                        arrow.Stroke = hlBrush;
                        arrow.Opacity = 1.0;
                    }
                    else
                    {
                        arrow.Opacity = 0.10;
                    }
                }
            }
        }

        private void StepLeftTimeline_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (Timeline.Value > 0)
            {
                Timeline.Value -= 1;
            }
        }

        private void StepRightTimeline_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (Timeline.Value < Timeline.Maximum)
            {
                Timeline.Value += 1;
            }
        }
    }
}