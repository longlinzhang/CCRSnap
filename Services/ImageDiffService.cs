using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace CCRSnap.Services;
public interface IImageDiffService
{
    double Compare(string? oldFilePath, Bitmap newImage, int tolerance);
}
public class ImageDiffService : IImageDiffService
{
    public double Compare(string? oldFilePath, Bitmap newImage, int tolerance)
    {
        if (string.IsNullOrEmpty(oldFilePath) || !File.Exists(oldFilePath))
            return 100.0; // No previous image means "different"
        try
        {
            using var oldBitmap = new Bitmap(oldFilePath);
            return CompareBitmapsFast(oldBitmap, newImage, tolerance);
        }
        catch
        {
            return 100.0;
        }
    }
    private static double CompareBitmapsFast(Bitmap oldBmp, Bitmap newBmp, int tolerance)
    {
        if (oldBmp.Width != newBmp.Width || oldBmp.Height != newBmp.Height)
            return 100.0;
        int width = oldBmp.Width;
        int height = oldBmp.Height;
        int totalPixels = width * height;
        // Lock both bitmaps and compare as raw byte arrays
        var oldRect = new Rectangle(0, 0, width, height);
        var newRect = new Rectangle(0, 0, width, height);
        BitmapData oldData = oldBmp.LockBits(oldRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData newData = newBmp.LockBits(newRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int bytesPerPixel = 4;
            int stride = oldData.Stride;
            byte[] oldRow = new byte[Math.Abs(stride)];
            byte[] newRow = new byte[Math.Abs(stride)];
            int diffCount = 0;
            int strideAbs = Math.Abs(stride);
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(oldData.Scan0, y * stride), oldRow, 0, strideAbs);
                Marshal.Copy(IntPtr.Add(newData.Scan0, y * stride), newRow, 0, strideAbs);
                for (int x = 0; x < width; x++)
                {
                    int offset = x * bytesPerPixel;
                    int dr = Math.Abs(oldRow[offset + 2] - newRow[offset + 2]);
                    int dg = Math.Abs(oldRow[offset + 1] - newRow[offset + 1]);
                    int db = Math.Abs(oldRow[offset] - newRow[offset]);
                    if (dr > tolerance || dg > tolerance || db > tolerance)
                        diffCount++;
                }
            }
            return 100.0 * diffCount / totalPixels;
        }
        finally
        {
            oldBmp.UnlockBits(oldData);
            newBmp.UnlockBits(newData);
        }
    }
}