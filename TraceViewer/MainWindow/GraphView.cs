using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TraceViewer.Core.Analysis;

namespace TraceViewer
{
    public class Node : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private string _text;
        private double _width = 100;
        private double _height = 50;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(CenterPoint))
            {
            }
        }

        public double X
        {
            get { return _x; }
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
            get { return _y; }
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
            get { return _text; }
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
            get { return _width; }
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
            get { return _height; }
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

        public List<Node> Connections { get; set; } = new List<Node>();
    }

    public class OffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double baseValue && parameter is string offsetStr && double.TryParse(offsetStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double offset))
            {
                return baseValue + offset;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ConnectionInfo
    {
        public Node StartNode { get; }
        public Node EndNode { get; }

        public ConnectionInfo(Node start, Node end)
        {
            StartNode = start;
            EndNode = end;
        }

        public override bool Equals(object obj) => obj is ConnectionInfo other && StartNode == other.StartNode && EndNode == other.EndNode;
        public override int GetHashCode() => HashCode.Combine(StartNode, EndNode);
    }


    public partial class MainWindow : Window
    {
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


        private List<(int, int)> connections;


        public IReadOnlyList<Node> Nodes => nodes.AsReadOnly();

        public void AddNode(Node node, Node connectTo = null)
        {
            if (node == null) return;

            if (nodes.Contains(node)) return;

            if (GraphViewCanvas != null)
            {
                double requiredHeight = node.Y + node.Height + 20;
                if (requiredHeight > GraphViewCanvas.Height)
                {
                    GraphViewCanvas.Height = requiredHeight;
                }
                double requiredWidth = node.X + node.Width + 20;
                if (requiredWidth > GraphViewCanvas.Width)
                {
                    GraphViewCanvas.Width = requiredWidth;
                }
            }

            nodes.Add(node);
            AddNodeToCanvas(node);

            if (connectTo != null && nodes.Contains(connectTo))
            {
                ConnectNodes(connectTo, node);
            }
        }

        private void Timeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UnhighlightAllConnections();
            if (connections != null && connections.Count > (int)e.NewValue)
                HighlightConnection(nodes[connections[(int)e.NewValue].Item1], nodes[connections[(int)e.NewValue].Item2]);
        }

        public void InitializeTimeline(List<(int, int)> connections)
        {
            this.connections = connections;

            Timeline.Maximum = connections.Count;
            Timeline.Minimum = 0;
            Timeline.IsSnapToTickEnabled = true;
            Timeline.TickFrequency = 1;
            Timeline.Value = 0;

            Timeline.ValueChanged += Timeline_ValueChanged;
        }



        public void Clear()
        {
            nodes.Clear();
            if (GraphViewCanvas != null)
            {
                GraphViewCanvas.Children.Clear();
            }
        }

        private void AddNodeToCanvas(Node node)
        {
            if (GraphViewCanvas == null) return;

            var container = new Grid
            {
                Width = node.Width,
                Height = node.Height,
                DataContext = node,
                Tag = node
            };

            var rectangle = new Rectangle
            {
                Fill = (SolidColorBrush)FindResource("ViewBorderBrush"),
                Stroke = (SolidColorBrush)FindResource("ViewBorderHoverBrush"),
                StrokeThickness = 1,
                Tag = "NodeBorder"
            };

            container.MouseDown += NodeElement_MouseDown;
            container.MouseMove += NodeElement_MouseMove;
            container.MouseUp += NodeElement_MouseUp;
            container.IsHitTestVisible = true;

            var label = new Label
            {
                Style = (Style)FindResource("ViewTitles"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
            label.SetBinding(ContentControl.ContentProperty, new Binding("Text") { Mode = BindingMode.OneWay });

            container.Children.Add(rectangle);
            container.Children.Add(label);

            container.SetBinding(Canvas.LeftProperty, new Binding("Left") { Mode = BindingMode.OneWay });
            container.SetBinding(Canvas.TopProperty, new Binding("Top") { Mode = BindingMode.OneWay });

            GraphViewCanvas.Children.Add(container);
            Panel.SetZIndex(container, 1);
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
        private void NodeElement_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element && element.DataContext is Node node)
            {
                if (selectedNode != null)
                {
                    ResetNodeAndConnectionStyles(selectedNode);
                }
                selectedNode = node;
                ChangeNodeAndConnectionStyles(selectedNode, element);

                currentlyDraggingNode = node;
                dragStartPoint = e.GetPosition(GraphViewCanvas);
                initialNodePosition = new Point(currentlyDraggingNode.X, currentlyDraggingNode.Y);
                element.CaptureMouse();
                Panel.SetZIndex(element, 10);
                e.Handled = true;
            }
        }

        private void ResetNodeAndConnectionStyles(Node node)
        {
            if (GraphViewCanvas == null || node == null) return;

            // Reset node border
            foreach (var child in GraphViewCanvas.Children.OfType<Grid>().Where(g => g.Tag == node))
            {
                if (child is Grid nodeGrid)
                {
                    var border = nodeGrid.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag == "NodeBorder");
                    if (border != null)
                    {
                        border.Stroke = (SolidColorBrush)FindResource("ViewBorderHoverBrush");
                    }
                }
            }

            // Reset outgoing connection lines
            foreach (var line in GraphViewCanvas.Children.OfType<Line>())
            {
                if (line.DataContext is ConnectionInfo ci && ci.StartNode == node)
                {
                    line.Stroke = defaultLineBrush;
                }
            }
            foreach (var arrow in GraphViewCanvas.Children.OfType<Polygon>())
            {
                if (arrow.DataContext is ConnectionInfo ci && ci.StartNode == node)
                {
                    arrow.Fill = defaultArrowFillBrush;
                    arrow.Stroke = defaultArrowStrokeBrush;
                }
            }
        }

        private void ChangeNodeAndConnectionStyles(Node node, FrameworkElement nodeElement)
        {
            if (GraphViewCanvas == null || node == null) return;

            if (nodeElement is Grid nodeGrid)
            {
                // Change node border to coral
                var border = nodeGrid.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag == "NodeBorder");
                if (border != null)
                {
                    border.Stroke = highlightBrush;
                }
            }

            // Change outgoing connection lines to yellow
            foreach (var line in GraphViewCanvas.Children.OfType<Line>())
            {
                if (line.DataContext is ConnectionInfo ci && ci.StartNode == node)
                {
                    line.Stroke = highlightBrush;
                    Panel.SetZIndex(line, -1);
                }
            }
            foreach (var arrow in GraphViewCanvas.Children.OfType<Polygon>())
            {
                if (arrow.DataContext is ConnectionInfo ci && ci.StartNode == node)
                {
                    arrow.Fill = Brushes.Red;
                    arrow.Stroke = highlightBrush;
                    Panel.SetZIndex(arrow, -1);
                }
            }
        }

        private void NodeElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentlyDraggingNode != null && e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element)
            {
                Point currentPosition = e.GetPosition(GraphViewCanvas);
                double deltaX = currentPosition.X - dragStartPoint.X;
                double deltaY = currentPosition.Y - dragStartPoint.Y;

                double newX = initialNodePosition.X + deltaX;
                double newY = initialNodePosition.Y + deltaY;

                newX = Math.Max(0, newX);
                newY = Math.Max(0, newY);
                if (GraphViewCanvas != null && GraphViewCanvas.ActualWidth > 0 && GraphViewCanvas.ActualHeight > 0)
                {
                    newX = Math.Min(GraphViewCanvas.ActualWidth - currentlyDraggingNode.Width, newX);
                    newY = Math.Min(GraphViewCanvas.ActualHeight - currentlyDraggingNode.Height, newY);
                }

                currentlyDraggingNode.X = newX;
                currentlyDraggingNode.Y = newY;

                RecalculateConnectionsForNode(currentlyDraggingNode);

                e.Handled = true;
            }
        }

        private void NodeElement_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (currentlyDraggingNode != null && sender is FrameworkElement element)
            {
                element.ReleaseMouseCapture();
                int currentZIndex = Panel.GetZIndex(element);
                if (currentZIndex > 1)
                {
                    Panel.SetZIndex(element, 1);
                }
                currentlyDraggingNode = null;
                RecalculateAllConnections();
                e.Handled = true;
            }
        }

        private void RecalculateAllConnections()
        {
            if (GraphViewCanvas == null) return;

            var shapesToRemove = GraphViewCanvas.Children.OfType<Shape>()
                .Where(shape => shape.DataContext is ConnectionInfo)
                .ToList();

            foreach (var shape in shapesToRemove)
            {
                GraphViewCanvas.Children.Remove(shape);
            }

            var drawnConnections = new HashSet<ConnectionInfo>();

            foreach (var node1 in nodes)
            {
                foreach (var node2 in node1.Connections)
                {
                    if (nodes.Contains(node2))
                    {
                        var connectionInfo = new ConnectionInfo(node1, node2);
                        if (drawnConnections.Add(connectionInfo))
                        {
                            DrawConnection(node1, node2);
                        }
                    }
                }
            }
            if (selectedNode != null)
            {
                foreach (var child in GraphViewCanvas.Children.OfType<Grid>().Where(g => g.Tag == selectedNode))
                {
                    ChangeNodeAndConnectionStyles(selectedNode, child);
                    break;
                }
            }
        }

        private void RecalculateConnectionsForNode(Node node)
        {
            if (GraphViewCanvas == null || node == null) return;

            var connectionsToRedraw = new HashSet<ConnectionInfo>();

            // Identify all ConnectionInfos involving the moved node
            foreach (var shape in GraphViewCanvas.Children.OfType<Shape>().Where(s => s.DataContext is ConnectionInfo))
            {
                var ci = (ConnectionInfo)shape.DataContext;
                if (ci.StartNode == node || ci.EndNode == node)
                {
                    connectionsToRedraw.Add(ci);
                }
            }

            // Remove the shapes for these connections
            foreach (var ciToRemove in connectionsToRedraw.ToList()) // Iterate over a copy to allow removal
            {
                foreach (var shape in GraphViewCanvas.Children.OfType<Shape>().Where(s => s.DataContext is ConnectionInfo && ((ConnectionInfo)s.DataContext).Equals(ciToRemove)).ToList())
                {
                    GraphViewCanvas.Children.Remove(shape);
                }
            }

            // Redraw the connections based on the Connections list of the relevant nodes
            foreach (var n in nodes)
            {
                if (n == node)
                {
                    foreach (var connectedNode in n.Connections)
                    {
                        if (nodes.Contains(connectedNode))
                        {
                            DrawConnection(n, connectedNode);
                        }
                    }
                }
                else if (n.Connections.Contains(node))
                {
                    DrawConnection(n, node);
                }
            }

            if (selectedNode != null)
            {
                foreach (var child in GraphViewCanvas.Children.OfType<Grid>().Where(g => g.Tag == selectedNode))
                {
                    ChangeNodeAndConnectionStyles(selectedNode, child);
                    break;
                }
            }
        }


        public void ConnectNodes(Node node1, Node node2)
        {
            if (node1 == null || node2 == null || node1 == node2) return;
            if (!nodes.Contains(node1) || !nodes.Contains(node2)) return;

            bool connectionAdded = false;
            if (!node1.Connections.Contains(node2))
            {
                node1.Connections.Add(node2);
                connectionAdded = true;
            }

            if (connectionAdded)
            {
                DrawConnection(node1, node2);
            }
        }

        private const double Epsilon = 0.1;
        private const double ArrowSpacing = 120.0f;

        private void DrawConnection(Node node1, Node node2)
        {
            if (GraphViewCanvas == null) return;

            Point startPoint = node1.CenterPoint;
            Point endPoint = node2.CenterPoint;
            ConnectionInfo connectionInfo = new ConnectionInfo(node1, node2);

            Vector direction = endPoint - startPoint;
            Vector normal = new Vector(-direction.Y, direction.X);
            normal.Normalize();

            Point adjustedStartPoint = startPoint;
            Point adjustedEndPoint = endPoint;

            if (node1.GetHashCode() > node2.GetHashCode() && node1.Connections.Contains(node2))
            {
                adjustedStartPoint += normal * ConnectionOffset;
                adjustedEndPoint += normal * ConnectionOffset;
            }
            else if (node2.GetHashCode() > node1.GetHashCode() && node2.Connections.Contains(node1))
            {
                adjustedStartPoint -= normal * ConnectionOffset;
                adjustedEndPoint -= normal * ConnectionOffset;
            }

            Line line = new Line
            {
                Stroke = defaultLineBrush,
                StrokeThickness = 2,
                DataContext = connectionInfo,
                X1 = adjustedStartPoint.X,
                Y1 = adjustedStartPoint.Y,
                X2 = adjustedEndPoint.X,
                Y2 = adjustedEndPoint.Y
            };
            GraphViewCanvas.Children.Add(line);
            Panel.SetZIndex(line, -1);

            double lineLength = direction.Length;
            if (lineLength > Epsilon)
            {
                direction.Normalize();
                int arrowCount = (int)(lineLength / ArrowSpacing);
                for (int i = 1; i <= arrowCount; i++)
                {
                    double distanceAlongLine = i * ArrowSpacing;
                    if (distanceAlongLine < lineLength)
                    {
                        Point arrowTip = adjustedStartPoint + direction * distanceAlongLine;
                        Point arrowSource = arrowTip - direction * 5; // Pfeilrichtung
                        Polygon arrow = CreateArrowhead(arrowTip, arrowSource, connectionInfo);
                        GraphViewCanvas.Children.Add(arrow);
                        Panel.SetZIndex(arrow, 0);
                    }
                }
                // Add one last arrow at the end if needed
                if (arrowCount == 0 || lineLength % ArrowSpacing > ArrowSpacing / 2)
                {
                    Point arrowTip = adjustedEndPoint;
                    Point arrowSource = adjustedEndPoint - direction * 5;
                    Polygon arrow = CreateArrowhead(arrowTip, arrowSource, connectionInfo);
                    GraphViewCanvas.Children.Add(arrow);
                    Panel.SetZIndex(arrow, 0);
                }
            }
        }

        private Polygon CreateArrowhead(Point tipPoint, Point lineSourcePoint, ConnectionInfo connectionInfo)
        {
            double arrowLength = 10;
            double arrowAngle = 25;

            Vector vector = tipPoint - lineSourcePoint;
            if (vector.Length < Epsilon)
            {
                vector = new Vector(1, 0);
            }
            vector.Normalize();

            Point p1 = tipPoint;
            Point basePoint = tipPoint - vector * arrowLength;
            Vector perpendicular = new Vector(-vector.Y, vector.X);

            double arrowWidth = arrowLength * Math.Tan(arrowAngle * Math.PI / 180.0);

            Point p2 = basePoint + perpendicular * arrowWidth;
            Point p3 = basePoint - perpendicular * arrowWidth;

            var arrowPolygon = new Polygon
            {
                Points = new PointCollection { p1, p2, p3 },
                Fill = defaultArrowFillBrush,
                Stroke = defaultArrowStrokeBrush,
                StrokeThickness = 1,
                DataContext = connectionInfo
            };

            return arrowPolygon;
        }

        public void HighlightConnection(Node startNode, Node endNode)
        {
            if (GraphViewCanvas == null || startNode == null || endNode == null) return;

            // Highlight the connection
            foreach (var child in GraphViewCanvas.Children)
            {
                if (child is Line line && line.DataContext is ConnectionInfo connectionInfo)
                {
                    if ((connectionInfo.StartNode == startNode && connectionInfo.EndNode == endNode) ||
                        (connectionInfo.StartNode == endNode && connectionInfo.EndNode == startNode))
                    {
                        line.Stroke = highlightBrush;
                    }
                }
                else if (child is Polygon arrow && arrow.DataContext is ConnectionInfo arrowConnectionInfo)
                {
                    if ((arrowConnectionInfo.StartNode == startNode && arrowConnectionInfo.EndNode == endNode) ||
                        (arrowConnectionInfo.StartNode == endNode && arrowConnectionInfo.EndNode == startNode))
                    {
                        arrow.Fill = Brushes.Red;
                        arrow.Stroke = highlightBrush;
                    }
                }
            }

            foreach (var child in GraphViewCanvas.Children.OfType<Grid>().Where(g => g.Tag == startNode))
            {
                if (child is Grid nodeGrid)
                {
                    var border = nodeGrid.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag == "NodeBorder");
                    if (border != null)
                    {
                        border.Stroke = highlightBrush;
                    }
                }
                break;
            }
        }

        public void UnhighlightAllConnections()
        {
            if (GraphViewCanvas == null) return;

            foreach (var child in GraphViewCanvas.Children)
            {
                if (child is Line line && line.DataContext is ConnectionInfo connectionInfo)
                {
                    line.Stroke = defaultLineBrush;
                }
                else if (child is Polygon arrow && arrow.DataContext is ConnectionInfo arrowConnectionInfo)
                {
                    arrow.Fill = defaultArrowFillBrush;
                    arrow.Stroke = defaultArrowStrokeBrush;
                }
            }

            // Unhighlight all nodes (reset their border color)
            foreach (var child in GraphViewCanvas.Children.OfType<Grid>())
            {
                if (child.Tag is Node node)
                {
                    var border = child.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag == "NodeBorder");
                    if (border != null)
                    {
                        border.Stroke = (SolidColorBrush)FindResource("ViewBorderHoverBrush");
                    }
                }
            }
        }
    }
}