using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CCRSnap.Controls;

public partial class MagnifierControl : System.Windows.Controls.UserControl
{
    private Bitmap? _screenShot;

    public MagnifierControl()
    {
        InitializeComponent();
    }

    public void SetScreenshot(Bitmap screenshot)
    {
        _screenShot = screenshot;
    }

    public void Update(int mouseX, int mouseY, int virtualScreenLeft, int virtualScreenTop, double scaleX = 1.0, double scaleY = 1.0)
    {
        if (_screenShot == null) return;

        int size = 50; // Source region size
        int zoom = 2;  // Zoom factor
        int destSize = size * zoom;

        // Adjust bounds relative to virtual screen
        int relX = mouseX;
        int relY = mouseY;

        int srcX = relX - size / 2;
        int srcY = relY - size / 2;

        srcX = Math.Clamp(srcX, 0, _screenShot.Width - size);
        srcY = Math.Clamp(srcY, 0, _screenShot.Height - size);

        try
        {
            using var srcRegion = _screenShot.Clone(
                new Rectangle(srcX, srcY, size, size),
                _screenShot.PixelFormat);

            using var zoomed = new Bitmap(destSize, destSize);
            using (var g = Graphics.FromImage(zoomed))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(srcRegion, 0, 0, destSize, destSize);
            }

            using var ms = new MemoryStream();
            zoomed.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            ZoomedImage.Source = bitmap;
        }
        catch { }

        CoordText.Text = $"{mouseX + virtualScreenLeft}, {mouseY + virtualScreenTop}";
        RootBorder.Visibility = Visibility.Visible;
    }

    public void HideMagnifier()
    {
        RootBorder.Visibility = Visibility.Collapsed;
    }
}
