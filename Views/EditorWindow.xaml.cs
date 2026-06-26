using System.IO;
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CCRSnap.Models;
using CCRSnap.Services;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
namespace CCRSnap.Views;
public partial class EditorWindow : Window
{
    private Bitmap _workingBitmap; // GDI+ bitmap for operations
    private Bitmap _originalBitmap;
    private readonly ISettingsService _settingsService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IWeComPushService? _weComPushService;
    private enum DrawTool { None, Rectangle, Ellipse, Arrow, Text, Mosaic }
    private DrawTool _currentTool = DrawTool.None;
    private System.Windows.Media.Color _currentColor = Colors.Red;
    private int _penSize = 2;
    private Point _drawStart;
    private bool _isDrawing;
    private UIElement? _previewShape;
    private readonly Stack<List<UIElement>> _undoStack = new();
    private bool _modified;
    public EditorWindow(Bitmap capturedImage, ISettingsService settingsService,
        IFileStorageService fileStorageService, IWeComPushService? weComPushService = null)
    {
        _workingBitmap = new Bitmap(capturedImage);
        _originalBitmap = capturedImage;
        _settingsService = settingsService;
        _fileStorageService = fileStorageService;
        _weComPushService = weComPushService;
        InitializeComponent();
        Loaded += OnLoaded;
        Loaded += (_, _) => ApplyLanguage();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateImageDisplay();
        OverlayCanvas.Visibility = Visibility.Visible;
        OverlayCanvas.MouseDown += OverlayCanvas_MouseDown;
        OverlayCanvas.MouseMove += OverlayCanvas_MouseMove;
        OverlayCanvas.MouseUp += OverlayCanvas_MouseUp;
    }
    private void UpdateImageDisplay()
    {
        DisplayImage.Source = BitmapToImageSource(_workingBitmap);
        DisplayImage.Width = _workingBitmap.Width;
        DisplayImage.Height = _workingBitmap.Height;
        OverlayCanvas.Width = _workingBitmap.Width;
        OverlayCanvas.Height = _workingBitmap.Height;
    }
    private static ImageSource BitmapToImageSource(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
    #region Tool Selection
    private void OnToolSelected(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            foreach (var child in ToolBar.Items)
            {
                if (child is ToggleButton tb && tb != btn)
                    tb.IsChecked = false;
            }
            btn.IsChecked = true;
            _currentTool = btn.Tag switch
            {
                "Rectangle" => DrawTool.Rectangle,
                "Ellipse" => DrawTool.Ellipse,
                "Arrow" => DrawTool.Arrow,
                "Text" => DrawTool.Text,
                "Mosaic" => DrawTool.Mosaic,
                _ => DrawTool.None
            };
            StatusItem.Content = $"工具: {btn.Content}";
        }
    }
    private void OnColor(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog();
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _currentColor = System.Windows.Media.Color.FromArgb(
                dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
        }
    }
    private void OnUndo(object sender, RoutedEventArgs e)
    {
        UndoLast();
    }
    private void OnSave(object sender, RoutedEventArgs e)
    {
        SaveToFile();
    }
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        using var finalBmp = BakeOverlays();
        var img = System.Drawing.Image.FromHbitmap(finalBmp.GetHbitmap());
        System.Windows.Forms.Clipboard.SetImage(img);
        img.Dispose();
        StatusItem.Content = "已复制到剪贴板";
    }
    private void OnComplete(object sender, RoutedEventArgs e)
    {
        CompleteAndClose();
    }
    #endregion
    #region Mouse Drawing
    private void OverlayCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentTool == DrawTool.None) return;
        _drawStart = e.GetPosition(OverlayCanvas);
        _isDrawing = true;
    }
private void OverlayCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDrawing) return;
        var current = e.GetPosition(OverlayCanvas);
        if (_previewShape != null)
        {
            OverlayCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }
        _previewShape = CreateShape(_drawStart, current, true);
        if (_previewShape != null)
            OverlayCanvas.Children.Add(_previewShape);
    }
    private void OverlayCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        var end = e.GetPosition(OverlayCanvas);
        if (_previewShape != null)
        {
            OverlayCanvas.Children.Remove(_previewShape);
            _previewShape = null;
        }
        if (_currentTool == DrawTool.Mosaic)
        {
            ApplyMosaic(_drawStart, end);
            return;
        }
        if (_currentTool == DrawTool.Text)
        {
            AddTextShape(_drawStart);
            return;
        }
        var finalShape = CreateShape(_drawStart, end, false);
        var shapeRect = GetRect(_drawStart, end);
        if (finalShape is System.Windows.Shapes.Rectangle || finalShape is System.Windows.Shapes.Ellipse)
        {
            Canvas.SetLeft(finalShape, shapeRect.X);
            Canvas.SetTop(finalShape, shapeRect.Y);
        }
        if (finalShape != null)
        {
            SaveUndoState();
            OverlayCanvas.Children.Add(finalShape);
            _modified = true;
        }
    }
    #endregion
    #region Shape Creation
    private UIElement? CreateShape(Point start, Point end, bool isPreview)
    {
        var rect = GetRect(start, end);
        double strokeThickness = isPreview ? 1 : _penSize;
        var color = isPreview
            ? System.Windows.Media.Color.FromArgb(128, _currentColor.R, _currentColor.G, _currentColor.B)
            : _currentColor;
        var brush = new SolidColorBrush(color);
        switch (_currentTool)
        {
            case DrawTool.Rectangle:
                return new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Stroke = brush,
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = isPreview ? new DoubleCollection { 4, 2 } : null
                };
            case DrawTool.Ellipse:
                return new System.Windows.Shapes.Ellipse
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Stroke = brush,
                    StrokeThickness = strokeThickness,
                    StrokeDashArray = isPreview ? new DoubleCollection { 4, 2 } : null
                };
            case DrawTool.Arrow:
                return CreateArrowElement(start, end, strokeThickness, color);
            default:
                return null;
        }
    }
    private UIElement CreateArrowElement(Point start, Point end, double thickness, System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        var canvas = new Canvas();
        // Line
        var line = new System.Windows.Shapes.Line
        {
            X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
            Stroke = brush, StrokeThickness = thickness
        };
        canvas.Children.Add(line);
        // Arrowhead
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double arrowSize = 12;
        var p1 = new Point(
            end.X - arrowSize * Math.Cos(angle - 0.4),
            end.Y - arrowSize * Math.Sin(angle - 0.4));
        var p2 = new Point(
            end.X - arrowSize * Math.Cos(angle + 0.4),
            end.Y - arrowSize * Math.Sin(angle + 0.4));
        var polygon = new Polygon
        {
            Points = new PointCollection { end, p1, p2 },
            Fill = brush,
            Stroke = brush,
            StrokeThickness = 1
        };
        canvas.Children.Add(polygon);
        return canvas;
    }
    private void AddTextShape(Point location)
    {
        string text = Microsoft.VisualBasic.Interaction.InputBox("输入文字:", "文字工具", "", -1, -1);
        if (string.IsNullOrEmpty(text)) return;
        SaveUndoState();
        var tb = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(_currentColor),
            FontSize = 14 * _penSize,
            FontFamily = new System.Windows.Media.FontFamily("Arial")
        };
        Canvas.SetLeft(tb, location.X);
        Canvas.SetTop(tb, location.Y);
        OverlayCanvas.Children.Add(tb);
        _modified = true;
    }
    private static Rect GetRect(Point p1, Point p2)
    {
        double x = Math.Min(p1.X, p2.X);
        double y = Math.Min(p1.Y, p2.Y);
        double w = Math.Abs(p1.X - p2.X);
        double h = Math.Abs(p1.Y - p2.Y);
        return new Rect(x, y, w, h);
    }
    #endregion
    #region Mosaic
    private void ApplyMosaic(Point start, Point end)
    {
        var rect = GetRect(start, end);
        int x = (int)rect.X, y = (int)rect.Y;
        int w = (int)rect.Width, h = (int)rect.Height;
        if (w < 4 || h < 4) return;
        SaveUndoState();
        int blockSize = 8;
        using var g = Graphics.FromImage(_workingBitmap);
        for (int by = y; by < y + h; by += blockSize)
        {
            for (int bx = x; bx < x + w; bx += blockSize)
            {
            int bw = Math.Min(blockSize, x + w - bx);
            int bh = Math.Min(blockSize, y + h - by);
                var avg = GetAverageColor(_workingBitmap, bx, by, bw, bh);
                using var brush = new SolidBrush(avg);
                g.FillRectangle(brush, bx, by, bw, bh);
            }
        }
        UpdateImageDisplay();
        _modified = true;
    }
    private static System.Drawing.Color GetAverageColor(Bitmap bmp, int x, int y, int w, int h)
    {
        int r = 0, g = 0, b = 0, count = 0;
        for (int py = y; py < y + h && py < bmp.Height; py++)
            for (int px = x; px < x + w && px < bmp.Width; px++)
            {
                var c = bmp.GetPixel(px, py);
                r += c.R; g += c.G; b += c.B; count++;
            }
        if (count == 0) return System.Drawing.Color.Transparent;
        return System.Drawing.Color.FromArgb(r / count, g / count, b / count);
    }
    #endregion
    #region Undo
    private void SaveUndoState()
    {
        var elements = OverlayCanvas.Children.Cast<UIElement>().ToList();
        _undoStack.Push(elements);
    }
    private void UndoLast()
    {
        if (_undoStack.Count == 0) return;
        var prevState = _undoStack.Pop();
        OverlayCanvas.Children.Clear();
        foreach (var el in prevState)
            OverlayCanvas.Children.Add(el);
    }
    #endregion
    #region Final Rendering
    private Bitmap BakeOverlays()
    {
        // Start with the working bitmap
        var result = new Bitmap(_workingBitmap);
        using var g = Graphics.FromImage(result);
        // Draw overlay shapes onto the bitmap
        foreach (var child in OverlayCanvas.Children)
        {
            if (child is Rectangle rect)
                DrawWpfShape(g, rect);
            else if (child is System.Windows.Shapes.Ellipse ellipse)
                DrawWpfShape(g, ellipse);
            else if (child is Canvas arrowCanvas)
                DrawArrowCanvas(g, arrowCanvas);
            else if (child is TextBlock tb)
                DrawTextBlock(g, tb);
        }
        return result;
    }
    private void DrawWpfShape(Graphics g, Shape shape)
    {
        var left = Canvas.GetLeft(shape);
        var top = Canvas.GetTop(shape);
        var color = ((SolidColorBrush)shape.Stroke).Color;
        var penColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        float thickness = (float)shape.StrokeThickness;
        using var pen = new System.Drawing.Pen(penColor, thickness);
        if (shape is Rectangle)
            g.DrawRectangle(pen, (float)left, (float)top, (float)shape.Width, (float)shape.Height);
        else if (shape is System.Windows.Shapes.Ellipse)
            g.DrawEllipse(pen, (float)left, (float)top, (float)shape.Width, (float)shape.Height);
    }
    private void DrawArrowCanvas(Graphics g, Canvas canvas)
    {
        foreach (var child in canvas.Children)
        {
            if (child is System.Windows.Shapes.Line line)
            {
                var c = ((SolidColorBrush)line.Stroke).Color;
                using var pen = new System.Drawing.Pen(
                    System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B), (float)line.StrokeThickness);
                g.DrawLine(pen, (float)line.X1, (float)line.Y1, (float)line.X2, (float)line.Y2);
            }
            else if (child is Polygon polygon)
            {
                var c = ((SolidColorBrush)polygon.Fill).Color;
                var pts = polygon.Points.Select(p => new System.Drawing.PointF((float)p.X, (float)p.Y)).ToArray();
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
                g.FillPolygon(brush, pts);
            }
        }
    }
    private void DrawTextBlock(Graphics g, TextBlock tb)
    {
        var left = Canvas.GetLeft(tb);
        var top = Canvas.GetTop(tb);
        var color = ((SolidColorBrush)tb.Foreground).Color;
        using var brush = new SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B));
        using var font = new System.Drawing.Font("Arial", (float)tb.FontSize);
        g.DrawString(tb.Text, font, brush, (float)left, (float)top);
    }
    #endregion
    #region Save / Complete
    private void SaveToFile()
    {
        using var dlg = new System.Windows.Forms.SaveFileDialog();
        dlg.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            using var final = BakeOverlays();
            final.Save(dlg.FileName);
            StatusItem.Content = $"已保存: {dlg.FileName}";
        }
    }
    private void CompleteAndClose()
    {
        try
        {
            using var finalBmp = BakeOverlays();
            string path = _fileStorageService.SaveImage(finalBmp, _settingsService.Settings, ".part");
            StatusItem.Content = $"已保存: {path}";
            // Push to WeCom if configured
            if (_settingsService.Settings.PushToWeCom &&
                !string.IsNullOrWhiteSpace(_settingsService.Settings.WebhookUrl) &&
                _weComPushService != null)
            {
                _ = _weComPushService.PushImageAsync(finalBmp, _settingsService.Settings.WebhookUrl);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
        DialogResult = true;
        Close();
    }
    private void ApplyLanguage()
    {
        bool zh = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("zh");
        this.Title = zh ? "截图编辑" : "Image Editor";
        BtnRectangle.Content = zh ? "矩形" : "Rectangle";
        BtnEllipse.Content = zh ? "椭圆" : "Ellipse";
        BtnArrow.Content = zh ? "箭头" : "Arrow";
        BtnText.Content = zh ? "文字" : "Text";
        BtnMosaic.Content = zh ? "马赛克" : "Mosaic";
        BtnUndo.Content = zh ? "撤销" : "Undo";
        BtnSave.Content = zh ? "保存" : "Save";
        BtnCopy.Content = zh ? "复制" : "Copy";
        BtnComplete.Content = zh ? "完成" : "Done";
        BtnColor.Content = zh ? "颜色" : "Color";
        LabelThickness.Content = zh ? "粗细:" : "Size:";
        StatusItem.Content = zh ? "就绪" : "Ready";
    }

    protected override void OnClosed(EventArgs e)
    {
        _workingBitmap?.Dispose();
        _originalBitmap?.Dispose();
        base.OnClosed(e);
    }
    #endregion
    private async void OnOCR(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Settings;
        if (string.IsNullOrEmpty(s.DeepSeekApiKey))
        {
            System.Windows.MessageBox.Show("请先在主窗口设置中配置DeepSeek API Key（获取: https://platform.deepseek.com）", "OCR 配置");
            return;
        }
        StatusItem.Content = "OCR 识别中...";
        try
        {
            var ocr = new Services.OcrService();
            var text = await ocr.RecognizeTextAsync(_workingBitmap, s.DeepSeekApiKey);
            StatusItem.Content = "OCR: " + (text.Length > 60 ? text[..60] + "..." : text);
            System.Windows.MessageBox.Show(text, "OCR 识别结果");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"OCR 失败: {ex.Message}", "错误"); }
    }

    private async void OnTranslate(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Settings;
        if (string.IsNullOrEmpty(s.DeepSeekApiKey))
        {
            System.Windows.MessageBox.Show("请先配置 DeepSeek API Key", "翻译配置");
            return;
        }
        StatusItem.Content = "翻译中...";
        try
        {
            var ocr = new Services.OcrService();
            var tms = new Services.TranslationService();
            var text = await ocr.RecognizeTextAsync(_workingBitmap, s.DeepSeekApiKey);
            if (string.IsNullOrEmpty(text) || text.Contains("请先"))
            { System.Windows.MessageBox.Show(text == "" ? "无法识别文字" : text, "提示"); return; }
            var translated = await tms.TranslateAsync(text, s.DeepSeekApiKey, null);
            StatusItem.Content = "翻译: " + (translated.Length > 60 ? translated[..60] + "..." : translated);
            System.Windows.MessageBox.Show($"原文:\n{text}\n\n翻译:\n{translated}", "翻译结果");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"翻译失败: {ex.Message}", "错误"); }
    }
}
