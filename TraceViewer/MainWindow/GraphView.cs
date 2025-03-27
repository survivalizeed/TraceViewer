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

    public class OffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double baseValue && parameter is string offsetStr && double.TryParse(offsetStr, out double offset))
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

    public partial class MainWindow : Window
    {
        private List<Node> nodes = new List<Node>();
        private Node currentlyDraggingNode = null;
        private Point dragStartPoint;

        // Annahme: GraphViewCanvas ist im XAML als Canvas definiert.


        public Node AddNode(string text, Point position, Node connect)
        {
            var node = new Node { X = position.X, Y = position.Y, Text = text };
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

            // Bindings
            var leftBinding = new Binding("Left") { Mode = BindingMode.OneWay };
            rectangle.SetBinding(Canvas.LeftProperty, leftBinding);
            textBlock.SetBinding(Canvas.LeftProperty, leftBinding);

            var topBinding = new Binding("Top") { Mode = BindingMode.OneWay };
            rectangle.SetBinding(Canvas.TopProperty, topBinding);
            textBlock.SetBinding(Canvas.TopProperty, topBinding);

            var textBinding = new Binding("Text") { Mode = BindingMode.OneWay };
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
                // Statt nur die Verbindungen des verschobenen Knotens neu zu zeichnen,
                // werden hier alle Verbindungen neu berechnet.
                RecalculateAllConnections();
            }
        }

        private void RecalculateAllConnections()
        {
            // Entferne alle Linien aus dem Canvas, deren DataContext ein Tuple<Node, Node> ist.
            var linesToRemove = GraphViewCanvas.Children.OfType<Line>()
                .Where(line => line.DataContext is Tuple<Node, Node>)
                .ToList();

            foreach (var line in linesToRemove)
            {
                GraphViewCanvas.Children.Remove(line);
            }

            // Zeichne alle Verbindungen neu. Damit nicht doppelt gezeichnet wird,
            // wird nur für eine Richtung (z. B. wenn der Index des aktuellen Knotens kleiner ist als der des verbundenen Knotens) gezeichnet.
            for (int i = 0; i < nodes.Count; i++)
            {
                foreach (var connectedNode in nodes[i].Connections)
                {
                    if (nodes.IndexOf(connectedNode) > i)
                    {
                        DrawConnection(nodes[i], connectedNode);
                    }
                }
            }
        }

        private void RedrawConnections(Node node)
        {
            var linesToRemove = GraphViewCanvas.Children.OfType<Line>()
                .Where(line => line.DataContext is Tuple<Node, Node> tuple &&
                               (tuple.Item1 == node || tuple.Item2 == node))
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

            bool horizontalFirst = Math.Abs(startPoint.X - endPoint.X) > Math.Abs(startPoint.Y - endPoint.Y);
            double offset = 100; 

            if (horizontalFirst)
            {
                bool collision = CheckHorizontalCollision(startPoint.Y, startPoint.X, endPoint.X, node1, node2) ||
                                 CheckVerticalCollision(endPoint.X, startPoint.Y, endPoint.Y, node1, node2);

                double midY = collision ? FindAdjustedY(startPoint, endPoint, node1, node2, offset) : startPoint.Y;
                CreateHorizontalVerticalPath(node1, node2, startPoint, endPoint, midY);
            }
            else
            {
                bool collision = CheckVerticalCollision(startPoint.X, startPoint.Y, endPoint.Y, node1, node2) ||
                                 CheckHorizontalCollision(endPoint.Y, startPoint.X, endPoint.X, node1, node2);

                double midX = collision ? FindAdjustedX(startPoint, endPoint, node1, node2, offset) : startPoint.X;
                CreateVerticalHorizontalPath(node1, node2, startPoint, endPoint, midX);
            }
        }

        private bool CheckHorizontalCollision(double y, double x1, double x2, Node exclude1, Node exclude2)
        {
            foreach (var node in nodes)
            {
                if (node == exclude1 || node == exclude2) continue;

                double nodeTop = node.Y;
                double nodeBottom = node.Y + node.Height;
                double nodeLeft = node.X;
                double nodeRight = node.X + node.Width;

                if (y >= nodeTop && y <= nodeBottom)
                {
                    if (Math.Max(x1, x2) >= nodeLeft && Math.Min(x1, x2) <= nodeRight)
                        return true;
                }
            }
            return false;
        }

        private bool CheckVerticalCollision(double x, double y1, double y2, Node exclude1, Node exclude2)
        {
            foreach (var node in nodes)
            {
                if (node == exclude1 || node == exclude2) continue;

                double nodeLeft = node.X;
                double nodeRight = node.X + node.Width;
                double nodeTop = node.Y;
                double nodeBottom = node.Y + node.Height;

                if (x >= nodeLeft && x <= nodeRight)
                {
                    if (Math.Max(y1, y2) >= nodeTop && Math.Min(y1, y2) <= nodeBottom)
                        return true;
                }
            }
            return false;
        }

        private double FindAdjustedY(Point start, Point end, Node node1, Node node2, double offset)
        {
            double adjustedY = start.Y + offset;
            if (!CheckHorizontalCollision(adjustedY, start.X, end.X, node1, node2) &&
                !CheckVerticalCollision(end.X, adjustedY, end.Y, node1, node2))
                return adjustedY;

            adjustedY = start.Y - offset;
            if (!CheckHorizontalCollision(adjustedY, start.X, end.X, node1, node2) &&
                !CheckVerticalCollision(end.X, adjustedY, end.Y, node1, node2))
                return adjustedY;

            return start.Y;
        }

        private double FindAdjustedX(Point start, Point end, Node node1, Node node2, double offset)
        {
            double adjustedX = start.X + offset;
            if (!CheckVerticalCollision(adjustedX, start.Y, end.Y, node1, node2) &&
                !CheckHorizontalCollision(end.Y, adjustedX, end.X, node1, node2))
                return adjustedX;

            adjustedX = start.X - offset;
            if (!CheckVerticalCollision(adjustedX, start.Y, end.Y, node1, node2) &&
                !CheckHorizontalCollision(end.Y, adjustedX, end.X, node1, node2))
                return adjustedX;

            return start.X;
        }

        private void CreateHorizontalVerticalPath(Node node1, Node node2, Point start, Point end, double midY)
        {
            var line1 = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                DataContext = Tuple.Create(node1, node2)
            };
            line1.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X"));
            line1.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y"));
            line1.SetBinding(Line.X2Property, new Binding("Item1.CenterPoint.X"));
            line1.SetBinding(Line.Y2Property, new Binding("Item1.CenterPoint.Y")
            {
                Converter = new OffsetConverter(),
                ConverterParameter = (midY - start.Y).ToString()
            });

            var line2 = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                DataContext = Tuple.Create(node1, node2)
            };
            line2.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X"));
            line2.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y")
            {
                Converter = new OffsetConverter(),
                ConverterParameter = (midY - start.Y).ToString()
            });
            line2.SetBinding(Line.X2Property, new Binding("Item2.CenterPoint.X"));
            line2.SetBinding(Line.Y2Property, new Binding("Item1.CenterPoint.Y")
            {
                Converter = new OffsetConverter(),
                ConverterParameter = (midY - start.Y).ToString()
            });

            var line3 = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                DataContext = Tuple.Create(node1, node2)
            };
            line3.SetBinding(Line.X1Property, new Binding("Item2.CenterPoint.X"));
            line3.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y")
            {
                Converter = new OffsetConverter(),
                ConverterParameter = (midY - start.Y).ToString()
            });
            line3.SetBinding(Line.X2Property, new Binding("Item2.CenterPoint.X"));
            line3.SetBinding(Line.Y2Property, new Binding("Item2.CenterPoint.Y"));

            GraphViewCanvas.Children.Add(line1);
            GraphViewCanvas.Children.Add(line2);
            GraphViewCanvas.Children.Add(line3);
            Panel.SetZIndex(line1, -1);
            Panel.SetZIndex(line2, -1);
            Panel.SetZIndex(line3, -1);
        }

        private void CreateVerticalHorizontalPath(Node node1, Node node2, Point start, Point end, double midX)
        {
            var line1 = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                DataContext = Tuple.Create(node1, node2)
            };
            line1.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X"));
            line1.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y"));
            line1.SetBinding(Line.X2Property, new Binding("Item1.CenterPoint.X"));
            line1.SetBinding(Line.Y2Property, new Binding("Item2.CenterPoint.Y"));

            var line2 = new Line
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                DataContext = Tuple.Create(node1, node2)
            };
            line2.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X"));
            line2.SetBinding(Line.Y1Property, new Binding("Item2.CenterPoint.Y"));
            line2.SetBinding(Line.X2Property, new Binding("Item2.CenterPoint.X"));
            line2.SetBinding(Line.Y2Property, new Binding("Item2.CenterPoint.Y"));

            if (Math.Abs(midX - start.X) > 1)
            {
                var line0 = new Line
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    DataContext = Tuple.Create(node1, node2)
                };
                line0.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X"));
                line0.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y"));
                line0.SetBinding(Line.X2Property, new Binding("Item1.CenterPoint.X")
                {
                    Converter = new OffsetConverter(),
                    ConverterParameter = (midX - start.X).ToString()
                });
                line0.SetBinding(Line.Y2Property, new Binding("Item1.CenterPoint.Y"));

                var line1a = new Line
                {
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    DataContext = Tuple.Create(node1, node2)
                };
                line1a.SetBinding(Line.X1Property, new Binding("Item1.CenterPoint.X")
                {
                    Converter = new OffsetConverter(),
                    ConverterParameter = (midX - start.X).ToString()
                });
                line1a.SetBinding(Line.Y1Property, new Binding("Item1.CenterPoint.Y"));
                line1a.SetBinding(Line.X2Property, new Binding("Item1.CenterPoint.X")
                {
                    Converter = new OffsetConverter(),
                    ConverterParameter = (midX - start.X).ToString()
                });
                line1a.SetBinding(Line.Y2Property, new Binding("Item2.CenterPoint.Y"));

                GraphViewCanvas.Children.Add(line0);
                GraphViewCanvas.Children.Add(line1a);
                GraphViewCanvas.Children.Add(line2);
                Panel.SetZIndex(line0, -1);
                Panel.SetZIndex(line1a, -1);
                Panel.SetZIndex(line2, -1);
            }
            else
            {
                GraphViewCanvas.Children.Add(line1);
                GraphViewCanvas.Children.Add(line2);
                Panel.SetZIndex(line1, -1);
                Panel.SetZIndex(line2, -1);
            }
        }
    }
}
