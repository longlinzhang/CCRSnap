using System.Drawing;
using System.Drawing.Imaging;

namespace CCRSnap.Services;

public interface IScreenCaptureService
{
    Bitmap CaptureFullScreen();
    Bitmap CaptureScreen(int screenIndex);
    Bitmap CaptureRegion(Bitmap fullScreen, Rectangle region);
    int ScreenCount { get; }
    Rectangle VirtualScreenBounds { get; }
}

public class ScreenCaptureService : IScreenCaptureService
{
    public int ScreenCount => Screen.AllScreens.Length;

    public Rectangle VirtualScreenBounds
    {
        get
        {
            int left = SystemInformation.VirtualScreen.Left;
            int top = SystemInformation.VirtualScreen.Top;
            int width = SystemInformation.VirtualScreen.Width;
            int height = SystemInformation.VirtualScreen.Height;
            return new Rectangle(left, top, width, height);
        }
    }

    public Bitmap CaptureFullScreen()
    {
        var bounds = VirtualScreenBounds;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        return bmp;
    }

    public Bitmap CaptureScreen(int screenIndex)
    {
        var screens = Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length)
            screenIndex = 0;

        var screen = screens[screenIndex];
        var bounds = screen.Bounds;
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        return bmp;
    }

    public Bitmap CaptureRegion(Bitmap fullScreen, Rectangle region)
    {
        var cropped = new Bitmap(region.Width, region.Height);
        using var g = Graphics.FromImage(cropped);
        g.DrawImage(fullScreen, 0, 0, region, GraphicsUnit.Pixel);
        return cropped;
    }
}
