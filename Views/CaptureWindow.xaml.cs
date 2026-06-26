using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CCRSnap.Services;
using Point = System.Windows.Point;
namespace CCRSnap.Views;
public partial class CaptureWindow : Window
{
    private readonly IScreenCaptureService _captureService;
    private Bitmap? _fullScreenShot;
    private Point _startPoint;
    private Point _endPoint;
    private bool _isDragging;
    public Bitmap? CapturedImage { get; private set; }
    public bool Cancelled { get; private set; }
    private int _virtualLeft;
    private int _virtualTop;
    private int _virtualWidth;
    private int _virtualHeight;
    public CaptureWindow(IScreenCaptureService captureService)
    {
        _captureService = captureService;
        InitializeComponent();
        // Set window to cover virtual screen
        var bounds = captureService.VirtualScreenBounds;
        _virtualLeft = bounds.Left;
        _virtualTop = bounds.Top;
        _virtualWidth = bounds.Width;
        _virtualHeight = bounds.Height;
        this.Left = _virtualLeft;
        this.Top = _virtualTop;
        this.Width = _virtualWidth;
        this.Height = _virtualHeight;
        // Take the screenshot
        _fullScreenShot = captureService.CaptureFullScreen();
        Magnifier.SetScreenshot(_fullScreenShot);
        // Set background to the screenshot
        SetBackgroundImage();
        // Events
        this.MouseDown += OnMouseDown;
        this.MouseMove += OnMouseMove;
        this.MouseUp += OnMouseUp;
        this.KeyDown += OnKeyDown;
    }
    private void SetBackgroundImage()
    {
        if (_fullScreenShot == null) return;
        using var ms = new MemoryStream();
        _fullScreenShot.Save(ms, ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        this.Background = new ImageBrush(bitmap);
    }
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(this);
            _endPoint = _startPoint;
            _isDragging = true;
            Magnifier.HideMagnifier();
        }
    }
private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (_isDragging)
        {
            _endPoint = pos;
            UpdateSelectionRect();
        }
        else
        {
            // Show magnifier when not dragging
            Magnifier.Update((int)pos.X, (int)pos.Y, _virtualLeft, _virtualTop);
            // Position magnifier near cursor
            double mLeft = pos.X + 20;
            double mTop = pos.Y + 20;
            if (mLeft + 210 > this.Width) mLeft = pos.X - 230;
            if (mTop + 210 > this.Height) mTop = pos.Y - 230;
            Canvas.SetLeft(Magnifier, mLeft);
            Canvas.SetTop(Magnifier, mTop);
        }
    }
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _endPoint = e.GetPosition(this);
        var rect = GetSelectionRect();
        if (rect.Width > 5 && rect.Height > 5)
        {
            // Crop from full screenshot
            var region = new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            CapturedImage = _captureService.CaptureRegion(_fullScreenShot!, region);
        }
        this.DialogResult = true;
        this.Close();
    }
private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancelled = true;
            this.DialogResult = false;
            this.Close();
        }
    }
    private void UpdateSelectionRect()
    {
        var rect = GetSelectionRect();
        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = rect.Width;
        SelectionRect.Height = rect.Height;
        Canvas.SetLeft(SelectionRect, rect.X);
        Canvas.SetTop(SelectionRect, rect.Y);
    }
    private Rect GetSelectionRect()
    {
        double x = Math.Min(_startPoint.X, _endPoint.X);
        double y = Math.Min(_startPoint.Y, _endPoint.Y);
        double w = Math.Abs(_startPoint.X - _endPoint.X);
        double h = Math.Abs(_startPoint.Y - _endPoint.Y);
        return new Rect(x, y, w, h);
    }
    protected override void OnClosed(EventArgs e)
    {
        _fullScreenShot?.Dispose();
        base.OnClosed(e);
    }
}