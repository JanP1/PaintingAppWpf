
using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Media3D;

using System;
using Microsoft.Win32;
using System.Diagnostics.Eventing.Reader;


namespace ProjektGrafika_jan_palmen
{
    public partial class MainWindow : Window
    {


        public System.Windows.Media.Color MyColor { get; set; }

        // Layers

        private Canvas currentLayer;


        private System.Windows.Point startPoint;
        private bool isDrawingLine = false;
        private System.Windows.Media.Color selectedColor = Colors.Black;
        private bool movePointsMode = false;
        private Ellipse dynamicEllipse;

        private bool canErase = false;
        private bool canDrawPolygons = false;
        private Polygon dynamicPolygon;
        private bool isDrawingPolygon = false;
        private List<Polygon> polygons = new List<Polygon>();

        private System.Windows.Shapes.Path currentPath;

        private bool canDrawPath = false;
        private bool isDrawingPath = false;
        private System.Windows.Shapes.Path dynamicPath;
        private System.Windows.Point lastPathPoint;


        private bool canDrawArrow = false;
        private bool canDrawRectangle = false;
        private bool canDrawEllipse = false;
        private bool drawMode = true;
        private Line selectedLine;
        private bool isMovingEndPoint;

        private System.Windows.Point rectangleStartPoint;
        private System.Windows.Shapes.Rectangle dynamicRectangle;
        private bool isDrawingRectangle = false;

        private Arrow dynamicArrow;

        private System.Windows.Point ellipseStartPoint;

        public MainWindow()
        {
            InitializeComponent();
            MyColor = Colors.Blue; // Set your initial color
            DataContext = this;
            currentLayer = paintSurface;
        }


        #region Shapes

        private System.Windows.Shapes.Rectangle DrawRectangle(System.Windows.Point startPoint, System.Windows.Media.Color color)
        {
            System.Windows.Shapes.Rectangle rectangle = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(color),
                Fill = new SolidColorBrush(color),
                StrokeThickness = 2,
                Width = 0,
                Height = 0
            };

            Canvas.SetLeft(rectangle, startPoint.X);
            Canvas.SetTop(rectangle, startPoint.Y);

            return rectangle;
        }

        private void UpdateRectangleSize(System.Windows.Shapes.Rectangle rectangle, System.Windows.Point startPoint, System.Windows.Point endPoint)
        {
            double width = Math.Abs(endPoint.X - startPoint.X);
            double height = Math.Abs(endPoint.Y - startPoint.Y);

            rectangle.Width = width;
            rectangle.Height = height;
            Canvas.SetLeft(rectangle, Math.Min(startPoint.X, endPoint.X));
            Canvas.SetTop(rectangle, Math.Min(startPoint.Y, endPoint.Y));
        }



        private Ellipse DrawEllipse(System.Windows.Point startPoint, System.Windows.Media.Color color)
        {
            Ellipse ellipse = new Ellipse
            {
                Stroke = new SolidColorBrush(color),
                Fill = new SolidColorBrush(color),
                StrokeThickness = 2
            };

            Canvas.SetLeft(ellipse, startPoint.X);
            Canvas.SetTop(ellipse, startPoint.Y);

            return ellipse;
        }

        private void UpdateEllipseSize(Ellipse ellipse, System.Windows.Point startPoint, double radiusX, double radiusY)
        {
            ellipse.Width = radiusX / 0.8;
            ellipse.Height = radiusY / 0.8;
        }



        #endregion

        #region HSV
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTextFields();
        }

        public static System.Windows.Media.Color HsvToRgb(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);
            value = value * 255;

            byte v = Convert.ToByte(Math.Min(255, Math.Max(0, value)));
            byte p = Convert.ToByte(Math.Min(255, Math.Max(0, value * (1 - saturation))));
            byte q = Convert.ToByte(Math.Min(255, Math.Max(0, value * (1 - f * saturation))));
            byte t = Convert.ToByte(Math.Min(255, Math.Max(0, value * (1 - (1 - f) * saturation))));

            switch (hi)
            {
                case 0:
                    return System.Windows.Media.Color.FromArgb(255, v, t, p);
                case 1:
                    return System.Windows.Media.Color.FromArgb(255, q, v, p);
                case 2:
                    return System.Windows.Media.Color.FromArgb(255, p, v, t);
                case 3:
                    return System.Windows.Media.Color.FromArgb(255, p, q, v);
                case 4:
                    return System.Windows.Media.Color.FromArgb(255, t, p, v);
                default:
                    return System.Windows.Media.Color.FromArgb(255, v, p, q);
            }
        }



        private void UpdateTextFields()
        {
            // Update the text fields with the current values of the sliders
            hueTextBox.Text = $"{hueSlider.Value:0}";
            saturationTextBox.Text = $"{saturationSlider.Value:0}";
            valueTextBox.Text = $"{valueSlider.Value:0}";

            MyColor = HsvToRgb(double.Parse(hueTextBox.Text), double.Parse(saturationTextBox.Text) / 100.0, double.Parse(valueTextBox.Text) / 100.0);

            selectedColor = MyColor;

            rValue.Text = MyColor.R.ToString();
            gValue.Text = MyColor.G.ToString();
            bValue.Text = MyColor.B.ToString();

            pickedCollor.Background = new SolidColorBrush(MyColor);
        }
        #endregion

        #region Filter

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }
        private void ApplyFilter()
        {
            Canvas canvas = currentLayer;
            // Create a writable bitmap from the canvas
            RenderTargetBitmap renderTarget = new RenderTargetBitmap((int)canvas.ActualWidth, (int)canvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(canvas);

            // Create a new WriteableBitmap to hold the filtered image
            WriteableBitmap filteredBitmap = new WriteableBitmap(renderTarget);

            // Define your custom linear filter matrix
            float[,] filterMatrix = new float[,]
            {
        { -1, -1, -1 },
        { -1,  9, -1 },
        { -1, -1, -1 }
            };

            int width = filteredBitmap.PixelWidth;
            int height = filteredBitmap.PixelHeight;

            // Apply the filter to each pixel
            int bytesPerPixel = (filteredBitmap.Format.BitsPerPixel + 7) / 8;
            int stride = filteredBitmap.PixelWidth * bytesPerPixel;
            byte[] pixels = new byte[stride * filteredBitmap.PixelHeight];
            filteredBitmap.CopyPixels(pixels, stride, 0);

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int offset = y * stride + x * bytesPerPixel;

                    float sumR = 0, sumG = 0, sumB = 0;

                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            int neighborOffset = (y + i) * stride + (x + j) * bytesPerPixel;

                            sumB += pixels[neighborOffset] * filterMatrix[i + 1, j + 1];
                            sumG += pixels[neighborOffset + 1] * filterMatrix[i + 1, j + 1];
                            sumR += pixels[neighborOffset + 2] * filterMatrix[i + 1, j + 1];
                        }
                    }

                    pixels[offset] = (byte)Math.Max(0, Math.Min(255, sumB));
                    pixels[offset + 1] = (byte)Math.Max(0, Math.Min(255, sumG));
                    pixels[offset + 2] = (byte)Math.Max(0, Math.Min(255, sumR));
                }
            }

            // Create a new Image element to display the filtered image
            Image filteredImage = new Image();
            filteredImage.Source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);

            // Remove the old background image from the canvas
            canvas.Children.Clear();

            // Add the new filtered image as a child of the canvas
            canvas.Children.Add(filteredImage);
        }




        #endregion


        #region Rendering
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a RenderTargetBitmap and render your layers
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap((int)1920, (int)1080, 96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(paintSurface); // Render the first layer
                renderBitmap.Render(foregroundCanvas); // Render the second layer

                // Convert Rendered Image to PNG
                PngBitmapEncoder pngEncoder = new PngBitmapEncoder();
                pngEncoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                // Prompt user to choose the location and filename for saving the PNG file
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "PNG Files (*.png)|*.png";
                if (saveFileDialog.ShowDialog() == true)
                {
                    // Save the encoded image to the selected file
                    using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        pngEncoder.Save(stream);
                    }

                    MessageBox.Show("Export successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting layers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        private void Canvas_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            if (drawMode)
            {
                if (canDrawPath)
                {
                    if (e.ClickCount == 2)
                    {
                        // End the current path on double-click
                        currentPath = null;
                    }
                    else
                    {
                        // Start drawing a new path or continue the existing one
                        if (currentPath == null)
                        {
                            currentPath = new System.Windows.Shapes.Path
                            {
                                Stroke = new SolidColorBrush(selectedColor),
                                StrokeThickness = 2,
                                Data = new PathGeometry(new PathFigureCollection())
                            };
                            currentLayer.Children.Add(currentPath);
                        }

                        var pathGeometry = (currentPath.Data as PathGeometry);
                        var pathFigure = pathGeometry.Figures.LastOrDefault();

                        if (pathFigure == null)
                        {
                            pathFigure = new PathFigure { StartPoint = e.GetPosition(currentLayer) };
                            pathGeometry.Figures.Add(pathFigure);
                        }
                        else
                        {
                            pathFigure.Segments.Add(new LineSegment(e.GetPosition(currentLayer), true));
                        }
                    }
                }
                else if (canDrawArrow)
                {
                    // Start drawing an arrow
                    Arrow arrow = new Arrow
                    {
                        Stroke = new SolidColorBrush(selectedColor),
                        StrokeThickness = 2,
                        X1 = e.GetPosition(currentLayer).X,
                        Y1 = e.GetPosition(currentLayer).Y,
                        X2 = e.GetPosition(currentLayer).X,
                        Y2 = e.GetPosition(currentLayer).Y,
                        ArrowLength = 10 // You can adjust this length as needed
                    };
                    currentLayer.Children.Add(arrow);
                    dynamicArrow = arrow;
                }
                else if (canErase)
                {
                    EraseWithEraser(e.GetPosition(currentLayer));
                }
                else if (canDrawPolygons)
                {
                    if (e.ClickCount == 2 && dynamicPolygon != null)
                    {
                        // Finish drawing the current polygon
                        polygons.Add(dynamicPolygon);
                        dynamicPolygon = null;
                    }
                    else
                    {
                        // Start drawing a new polygon or continue existing one
                        if (dynamicPolygon == null)
                        {
                            dynamicPolygon = new Polygon  // Replace Polyline with Polygon here
                            {
                                Stroke = new SolidColorBrush(selectedColor),
                                StrokeThickness = 2,
                                Fill = new SolidColorBrush(selectedColor)
                            };
                            currentLayer.Children.Add(dynamicPolygon);
                        }

                        dynamicPolygon.Points.Add(e.GetPosition(currentLayer));
                    }
                }

                else if (isDrawingLine)
                {
                    Line line = new Line
                    {
                        Stroke = new SolidColorBrush(selectedColor),
                        X1 = e.GetPosition(currentLayer).X,
                        Y1 = e.GetPosition(currentLayer).Y,
                        X2 = e.GetPosition(currentLayer).X,
                        Y2 = e.GetPosition(currentLayer).Y,
                        StrokeThickness = 2
                    };
                    currentLayer.Children.Add(line);
                }

                else if (canDrawEllipse)
                {
                    ellipseStartPoint = e.GetPosition(currentLayer);
                    dynamicEllipse = DrawEllipse(ellipseStartPoint, selectedColor);
                    currentLayer.Children.Add(dynamicEllipse);

                }
                else if (canDrawRectangle)
                {
                    isDrawingRectangle = true;
                    rectangleStartPoint = e.GetPosition(currentLayer);
                    dynamicRectangle = DrawRectangle(rectangleStartPoint, selectedColor);
                    currentLayer.Children.Add(dynamicRectangle);
                }
                else
                {

                    Polyline polyline = new Polyline
                    {
                        Stroke = new SolidColorBrush(selectedColor),
                        StrokeThickness = 2
                    };
                    polyline.Points.Add(e.GetPosition(currentLayer));
                    currentLayer.Children.Add(polyline);
                }
            }
            else
            {

                startPoint = e.GetPosition(currentLayer);
                selectedLine = FindNearestLine(startPoint);

                if (selectedLine != null)
                {
                    isMovingEndPoint = Math.Abs(selectedLine.X2 - startPoint.X) < Math.Abs(selectedLine.X1 - startPoint.X) &&
                                       Math.Abs(selectedLine.Y2 - startPoint.Y) < Math.Abs(selectedLine.Y1 - startPoint.Y);

                    Mouse.OverrideCursor = Cursors.Hand;
                }
            }
        }

        private void Canvas_MouseMove_1(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (drawMode)
                {
                    if (canDrawPath && currentPath != null)
                    {
                        // Continue drawing the current path with straight lines
                        var pathGeometry = (currentPath.Data as PathGeometry);
                        var pathFigure = pathGeometry.Figures.LastOrDefault();

                        if (pathFigure != null)
                        {
                            pathFigure.Segments.Add(new LineSegment(e.GetPosition(currentLayer), true));
                        }
                    }
                    else if (canDrawArrow && dynamicArrow != null)
                    {
                        // Continue drawing the arrow
                        dynamicArrow.X2 = e.GetPosition(currentLayer).X;
                        dynamicArrow.Y2 = e.GetPosition(currentLayer).Y;
                    }
                    else if (canErase)
                    {
                        EraseWithEraser(e.GetPosition(currentLayer));
                    }
                    else if (canDrawPolygons && isDrawingPolygon && dynamicPolygon != null)
                    {
                        // Continue drawing the polygon
                        dynamicPolygon.Points.Add(e.GetPosition(currentLayer));
                    }
                    else if (isDrawingLine)
                    {
                        Line line = (Line)currentLayer.Children[currentLayer.Children.Count - 1];
                        line.X2 = e.GetPosition(currentLayer).X;
                        line.Y2 = e.GetPosition(currentLayer).Y;
                    }
                    else if (canDrawEllipse && dynamicEllipse != null)
                    {
                        double radiusX = Math.Abs(e.GetPosition(currentLayer).X - ellipseStartPoint.X);
                        double radiusY = Math.Abs(e.GetPosition(currentLayer).Y - ellipseStartPoint.Y);

                        UpdateEllipseSize(dynamicEllipse, ellipseStartPoint, radiusX, radiusY);
                    }
                    else if (canDrawRectangle && dynamicRectangle != null)
                    {
                        UpdateRectangleSize(dynamicRectangle, rectangleStartPoint, e.GetPosition(currentLayer));
                    }
                    else
                    {
                        if (currentLayer.Children.Count > 0)
                        {
                            var lastShape = currentLayer.Children[currentLayer.Children.Count - 1];

                            if (lastShape is Polyline polyline)
                            {
                                polyline.Points.Add(e.GetPosition(currentLayer));
                            }
                            else if (lastShape is Polygon polygon)
                            {
                                // Handle drawing on polygons if needed
                                // Example: polygon.Points.Add(e.GetPosition(currentLayer));
                            }
                        }
                    }
                }
                else if (selectedLine != null)
                {
                    if (isMovingEndPoint)
                    {
                        selectedLine.X2 = e.GetPosition(currentLayer).X;
                        selectedLine.Y2 = e.GetPosition(currentLayer).Y;
                    }
                    else
                    {
                        selectedLine.X1 = e.GetPosition(currentLayer).X;
                        selectedLine.Y1 = e.GetPosition(currentLayer).Y;
                    }
                }
            }
        }

        private void Canvas_MouseUp_1(object sender, MouseButtonEventArgs e)
        {
            if (drawMode)
            {
                if (canDrawPath && isDrawingPath && dynamicPath != null)
                {
                    // Stop drawing the current path
                    isDrawingPath = false;
                    lastPathPoint = new System.Windows.Point(); // Reset the last path point
                }
                else if (canDrawArrow && dynamicArrow != null)
                {
                    // Stop drawing the arrow
                    dynamicArrow = null;
                }

                else if (canDrawPolygons && dynamicPolygon != null)
                {
                    // Stop drawing the current polygon (if any)
                    polygons.Add(dynamicPolygon);
                    dynamicPolygon = null;
                }
                else if (canDrawEllipse && dynamicEllipse != null)
                {
                    // Stop drawing dynamic ellipse
                    dynamicEllipse = null;
                }

            }

            if (!drawMode && selectedLine != null)
            {

                selectedLine = null;
                Mouse.OverrideCursor = null;
            }
        }




        private Line FindNearestLine(System.Windows.Point point)
        {
            foreach (var item in currentLayer.Children.OfType<Line>())
            {
                if (IsPointNearLine(point, item))
                {
                    return item;
                }
            }
            return null;
        }

        private bool IsPointNearLine(System.Windows.Point point, Line line)
        {
            double threshold = 10.0;

            double distanceToStart = Math.Sqrt(Math.Pow(point.X - line.X1, 2) + Math.Pow(point.Y - line.Y1, 2));
            double distanceToEnd = Math.Sqrt(Math.Pow(point.X - line.X2, 2) + Math.Pow(point.Y - line.Y2, 2));

            return distanceToStart <= threshold || distanceToEnd <= threshold;
        }

        private void MovePointsButton_Click(object sender, RoutedEventArgs e)
        {

            drawMode = !drawMode;


            selectedLine = null;
            if (drawMode)
            {
                Mouse.OverrideCursor = null;
            }
            else
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        private void ToggleLineTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawEllipse = false;
            isDrawingLine = true;
            canDrawPath = false;
            canDrawRectangle = false;
            canDrawPolygons = false;
            isDrawingPolygon = false;
            canErase = false;

            canDrawArrow = false;

        }

        private void ToggleStrokeTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawRectangle = false;
            canDrawEllipse = false;
            isDrawingLine = false;
            canDrawPath = false;
            canDrawPolygons = false;
            isDrawingPolygon = false;
            canErase = false;

            canDrawArrow = false;

        }
        private void ToggleEllipseTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawRectangle = false;
            isDrawingLine = false;
            canDrawEllipse = true;
            canDrawPath = false;
            canDrawPolygons = false;
            isDrawingPolygon = false;
            canErase = false;

            canDrawArrow = false;

        }

        private void ToggleRectangleTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawRectangle = true;
            isDrawingLine = false;
            canDrawEllipse = false;
            canDrawPath = false;
            canDrawPolygons = false;
            isDrawingPolygon = false;
            canErase = false;

            canDrawArrow = false;

        }
        private void TogglePolygonTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawPolygons = true;
            canDrawRectangle = false;
            isDrawingLine = false;
            canDrawEllipse = false;
            canDrawPath = false;
            canErase = false;

            canDrawArrow = false;

        }
        private void TogglePathTool_Click(object sender, RoutedEventArgs e)
        {
            canDrawArrow = false;

            canDrawPath = true;
            isDrawingPath = false;
            canDrawPolygons = false;
            canDrawRectangle = false;
            isDrawingLine = false;
            canDrawEllipse = false;
            canErase = false;

        }
        private void ToggleClearAll_Click(object sender, RoutedEventArgs e)
        {
            currentLayer.Children.Clear();
        }



        private void BackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            // Switch to the background layer
            backStack.Background = new SolidColorBrush(System.Windows.Media.Colors.Aqua);
            foreStack.Background = new SolidColorBrush(System.Windows.Media.Colors.Black);
            currentLayer = paintSurface;
        }

        private void ForegroundButton_Click(object sender, RoutedEventArgs e)
        {
            foreStack.Background = new SolidColorBrush(System.Windows.Media.Colors.Aqua);
            backStack.Background = new SolidColorBrush(System.Windows.Media.Colors.Black);
            currentLayer = foregroundCanvas;
        }

        private void EraserToggleButton_Click(object sender, RoutedEventArgs e)
        {
            canDrawPath = false;
            isDrawingPath = false;
            canDrawPolygons = false;
            canDrawRectangle = false;
            isDrawingLine = false;
            canDrawEllipse = false;
            canErase = true;
            canDrawArrow = false;


        }
        private void ToggleArrowButton_Click(object sender, RoutedEventArgs e)
        {
            canDrawPath = false;
            isDrawingPath = false;
            canDrawPolygons = false;
            canDrawRectangle = false;
            isDrawingLine = false;
            canDrawEllipse = false;
            canErase = false;
            canDrawArrow = true;
        }
        private void EraseWithEraser(System.Windows.Point position)
        {
            // Erase elements on the currently selected layer in the vicinity of the eraser position
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(currentLayer, position);

            if (hitTestResult != null && hitTestResult.VisualHit is UIElement elementToRemove)
            {
                currentLayer.Children.Remove(elementToRemove);
            }
        }

        private void ColorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = (RadioButton)sender;

            if (Enum.TryParse(radioButton.Tag.ToString(), out KnownColor knownColor))
            {
                selectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(knownColor.ToString());
            }
        }
    }
    public class Arrow : Shape
    {
        public static readonly DependencyProperty X1Property =
            DependencyProperty.Register("X1", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Y1Property =
            DependencyProperty.Register("Y1", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty X2Property =
            DependencyProperty.Register("X2", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Y2Property =
            DependencyProperty.Register("Y2", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ArrowLengthProperty =
            DependencyProperty.Register("ArrowLength", typeof(double), typeof(Arrow), new PropertyMetadata(10.0));

        public double X1
        {
            get { return (double)GetValue(X1Property); }
            set { SetValue(X1Property, value); }
        }

        public double Y1
        {
            get { return (double)GetValue(Y1Property); }
            set { SetValue(Y1Property, value); }
        }

        public double X2
        {
            get { return (double)GetValue(X2Property); }
            set { SetValue(X2Property, value); }
        }

        public double Y2
        {
            get { return (double)GetValue(Y2Property); }
            set { SetValue(Y2Property, value); }
        }

        public double ArrowLength
        {
            get { return (double)GetValue(ArrowLengthProperty); }
            set { SetValue(ArrowLengthProperty, value); }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext context = geometry.Open())
                {
                    DrawArrow(context);
                }
                geometry.Freeze();
                return geometry;
            }
        }

        private void DrawArrow(StreamGeometryContext context)
        {
            double angle = Math.Atan2(Y2 - Y1, X2 - X1);

            context.BeginFigure(new System.Windows.Point(X1, Y1), false, false);
            context.LineTo(new System.Windows.Point(X2, Y2), true, false);

            // Calculate arrowhead points
            double arrowX1 = X2 - ArrowLength * Math.Cos(angle - Math.PI / 6);
            double arrowY1 = Y2 - ArrowLength * Math.Sin(angle - Math.PI / 6);

            double arrowX2 = X2 - ArrowLength * Math.Cos(angle + Math.PI / 6);
            double arrowY2 = Y2 - ArrowLength * Math.Sin(angle + Math.PI / 6);

            context.LineTo(new System.Windows.Point(arrowX1, arrowY1), true, false);
            context.LineTo(new System.Windows.Point(arrowX2, arrowY2), true, false);
            context.LineTo(new System.Windows.Point(X2, Y2), true, false);
        }
    }
}
