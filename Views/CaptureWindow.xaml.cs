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
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    public CaptureWindow(IScreenCaptureService captureService)
    {
        _captureService = captureService;
        InitializeComponent();
        // Take screenshot first (physical pixels)
        _fullScreenShot = captureService.CaptureFullScreen();
        Magnifier.SetScreenshot(_fullScreenShot);
        // Get DPI scale (physical vs device-independent pixels)
        var dpi = VisualTreeHelper.GetDpi(this);
        _dpiScaleX = dpi.DpiScaleX;
        _dpiScaleY = dpi.DpiScaleY;
        // Window bounds in DIPs (device-independent pixels)
        this.Left = SystemParameters.VirtualScreenLeft;
        this.Top = SystemParameters.VirtualScreenTop;
        this.Width = SystemParameters.VirtualScreenWidth;
        this.Height = SystemParameters.VirtualScreenHeight;
        // Store physical pixel bounds for CopyFromScreen
        var bounds = captureService.VirtualScreenBounds;
        _virtualLeft = bounds.Left;
        _virtualTop = bounds.Top;
        _virtualWidth = bounds.Width;
        _virtualHeight = bounds.Height;
        SetBackgroundImage();
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
            Magnifier.Update((int)pos.X, (int)pos.Y, _virtualLeft, _virtualTop, _dpiScaleX, _dpiScaleY);
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
        var dipRect = GetSelectionRect();
        if (dipRect.Width > 5 && dipRect.Height > 5)
        {
            // Convert DIP to physical pixels for correct cropping
            var physRect = new Rectangle(
                (int)(dipRect.X * _dpiScaleX),
                (int)(dipRect.Y * _dpiScaleY),
                (int)(dipRect.Width * _dpiScaleX),
                (int)(dipRect.Height * _dpiScaleY));
            CapturedImage = _captureService.CaptureRegion(_fullScreenShot!, physRect);
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