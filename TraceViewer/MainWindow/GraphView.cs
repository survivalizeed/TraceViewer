using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using TraceViewer.Core;
using TraceViewer.UserControls;
using TraceViewer.UserWindows;
using System.Windows.Data;

namespace TraceViewer
{
    public class Node : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private string _text;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public double X
        {
            get { return _x; }
            set
            {
                _x = value;
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Left));
                OnPropertyChanged(nameof(CenterPoint));
            }
        }

        public double Y
        {
            get { return _y; }
            set
            {
                _y = value;
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(Top));
                OnPropertyChanged(nameof(CenterPoint));
            }
        }

        public string Text
        {
            get { return _text; }
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        public double Width { get; set; } = 100;
        public double Height { get; set; } = 50;

        public double Left => X;
        public double Top => Y;
        public Point CenterPoint => new Point(X + Width / 2, Y + Height / 2);

        public List<Node> Connections { get; set; } = new List<Node>();
    }

    public partial class MainWindow : Window
    {
        private List<Node> nodes = new List<Node>();
        private Node currentlyDraggingNode = null;
        private Point dragStartPoint;


        public Node AddNode(string Text, Point position, Node? connect)
        {
            var node = new Node { X = position.X, Y = position.Y, Text = Text };
            nodes.Add(node);
            AddNodeToCanvas(node);
            if (connect != null)
                ConnectNodes(node, connect);
            return node;
        }

        private void AddNodeToCanvas(Node node)
        {
            var rectangle = new Rectangle
            {
                Width = node.Width,
                Height = node.Height,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            Canvas.SetLeft(rectangle, node.X);
            Canvas.SetTop(rectangle, node.Y);
            rectangle.DataContext = node;
            rectangle.MouseDown += Rectangle_MouseDown;
            rectangle.MouseMove += Rectangle_MouseMove;
            rectangle.MouseUp += Rectangle_MouseUp;

            var textBlock = new TextBlock
            {
                Text = node.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.DataContext = node;
            Canvas.SetLeft(textBlock, node.X);
            Canvas.SetTop(textBlock, node.Y);
            textBlock.Width = node.Width;
            textBlock.Height = node.Height;
            textBlock.IsHitTestVisible = false;

            // Eigenschaften binden
            System.Windows.Data.Binding leftBinding = new System.Windows.Data.Binding("Left") { Mode = System.Windows.Data.BindingMode.OneWay };
            rectangle.SetBinding(Canvas.LeftProperty, leftBinding);
            textBlock.SetBinding(Canvas.LeftProperty, leftBinding);

            System.Windows.Data.Binding topBinding = new System.Windows.Data.Binding("Top") { Mode = System.Windows.Data.BindingMode.OneWay };
            rectangle.SetBinding(Canvas.TopProperty, topBinding);
            textBlock.SetBinding(Canvas.TopProperty, topBinding);

            System.Windows.Data.Binding textBinding = new System.Windows.Data.Binding("Text") { Mode = System.Windows.Data.BindingMode.OneWay };
            textBlock.SetBinding(TextBlock.TextProperty, textBinding);

            GraphViewCanvas.Children.Add(rectangle);
            GraphViewCanvas.Children.Add(textBlock);
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                currentlyDraggingNode = (sender as FrameworkElement).DataContext as Node;
                dragStartPoint = e.GetPosition(GraphViewCanvas);
                (sender as Rectangle).CaptureMouse();
            }
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (currentlyDraggingNode != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(GraphViewCanvas);
                double deltaX = currentPosition.X - dragStartPoint.X;
                double deltaY = currentPosition.Y - dragStartPoint.Y;

                currentlyDraggingNode.X += deltaX;
                currentlyDraggingNode.Y += deltaY;

                dragStartPoint = currentPosition;
            }
        }

        private void Rectangle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (currentlyDraggingNode != null)
            {
                (sender as Rectangle).ReleaseMouseCapture();
                currentlyDraggingNode = null;
                // Redraw connections for the moved node
                foreach (var connection in nodes.Where(n => n.Connections.Contains(currentlyDraggingNode)))
                {
                    RedrawConnections(connection);
                }
                if (currentlyDraggingNode != null && currentlyDraggingNode.Connections.Any())
                {
                    RedrawConnections(currentlyDraggingNode);
                }
            }
        }

        private void RedrawConnections(Node node)
        {
            var linesToRemove = GraphViewCanvas.Children.OfType<Line>()
                .Where(line => line.DataContext is Tuple<Node, Node> &&
                               (line.DataContext as Tuple<Node, Node>).Item1 == node ||
                               (line.DataContext as Tuple<Node, Node>).Item2 == node)
                .ToList();

            foreach (var line in linesToRemove)
            {
                GraphViewCanvas.Children.Remove(line);
            }

            foreach (var connectedNode in node.Connections)
            {
                DrawConnection(node, connectedNode);
            }
        }

        private void ConnectNodes(Node node1, Node node2)
        {
            if (!node1.Connections.Contains(node2))
            {
                node1.Connections.Add(node2);
                DrawConnection(node1, node2);
            }
            if (!node2.Connections.Contains(node1))
            {
                node2.Connections.Add(node1);
            }
        }

        private void DrawConnection(Node node1, Node node2)
        {
            Point startPoint = node1.CenterPoint;
            Point endPoint = node2.CenterPoint;

            var line1 = new Line { Stroke = Brushes.White, StrokeThickness = 2, DataContext = Tuple.Create(node1, node2) };
            var line2 = new Line { Stroke = Brushes.White, StrokeThickness = 2, DataContext = Tuple.Create(node1, node2) };

            
            if (Math.Abs(startPoint.X - endPoint.X) > Math.Abs(startPoint.Y - endPoint.Y))
            {
                line1.SetBinding(Line.X1Property, new Binding("CenterPoint.X") { Source = node1 });
                line1.SetBinding(Line.Y1Property, new Binding("CenterPoint.Y") { Source = node1 });
                line1.SetBinding(Line.X2Property, new Binding("CenterPoint.X") { Source = node2 });
                line1.SetBinding(Line.Y2Property, new Binding("CenterPoint.Y") { Source = node1 });

                line2.SetBinding(Line.X1Property, new Binding("CenterPoint.X") { Source = node2 });
                line2.SetBinding(Line.Y1Property, new Binding("CenterPoint.Y") { Source = node1 });
                line2.SetBinding(Line.X2Property, new Binding("CenterPoint.X") { Source = node2 });
                line2.SetBinding(Line.Y2Property, new Binding("CenterPoint.Y") { Source = node2 });
            }
            else
            {
                line1.SetBinding(Line.X1Property, new Binding("CenterPoint.X") { Source = node1 });
                line1.SetBinding(Line.Y1Property, new Binding("CenterPoint.Y") { Source = node1 });
                line1.SetBinding(Line.X2Property, new Binding("CenterPoint.X") { Source = node1 });
                line1.SetBinding(Line.Y2Property, new Binding("CenterPoint.Y") { Source = node2 });

                line2.SetBinding(Line.X1Property, new Binding("CenterPoint.X") { Source = node1 });
                line2.SetBinding(Line.Y1Property, new Binding("CenterPoint.Y") { Source = node2 });
                line2.SetBinding(Line.X2Property, new Binding("CenterPoint.X") { Source = node2 });
                line2.SetBinding(Line.Y2Property, new Binding("CenterPoint.Y") { Source = node2 });
            }

            GraphViewCanvas.Children.Add(line1);
            GraphViewCanvas.Children.Add(line2);
            Panel.SetZIndex(line1, -1);
            Panel.SetZIndex(line2, -1);
        }
    }
}