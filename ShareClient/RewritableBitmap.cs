using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Windows.Interop;
using System.Runtime.InteropServices;
namespace System.Windows.Media.Imaging
{
    public static class RewritableBitmap
    {
        #region Enums

        /// <summary>
        /// The interpolation method.
        /// </summary>
        public enum Interpolation
        {
            /// <summary>
            /// The nearest neighbor algorithm simply selects the color of the nearest pixel.
            /// </summary>
            NearestNeighbor = 0,

            /// <summary>
            /// Linear interpolation in 2D using the average of 3 neighboring pixels.
            /// </summary>
            Bilinear,
        }
        #endregion

        #region Resize

        /// <summary>
        /// Creates a new resized WriteableBitmap.
        /// </summary>
        /// <param name="bmp">The WriteableBitmap.</param>
        /// <param name="width">The new desired width.</param>
        /// <param name="height">The new desired height.</param>
        /// <param name="interpolation">The interpolation method that should be used.</param>
        /// <returns>A new WriteableBitmap that is a resized version of the input.</returns>
        public static WriteableBitmap Resize(this WriteableBitmap bmp, int width, int height, Interpolation interpolation)
        {
            // Init vars
            var ws = bmp.PixelWidth;
            var hs = bmp.PixelHeight;
#if SILVERLIGHT
         var ps = bmp.Pixels;
         var result = new WriteableBitmap(width, height);
         var pd = result.Pixels;
#else
            bmp.Lock();
            var result = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);
            result.Lock();
            unsafe
            {
                int* ps = (int*)bmp.BackBuffer;
                int* pd = (int*)result.BackBuffer;
#endif
                float xs = (float)ws / width;
                float ys = (float)hs / height;

                float fracx, fracy, ifracx, ifracy, sx, sy, l0, l1;
                int c, x0, x1, y0, y1;
                byte c1a, c1r, c1g, c1b, c2a, c2r, c2g, c2b, c3a, c3r, c3g, c3b, c4a, c4r, c4g, c4b;
                byte a = 0, r = 0, g = 0, b = 0;

                // Nearest Neighbor
                if (interpolation == Interpolation.NearestNeighbor)
                {
                    var srcIdx = 0;
                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            sx = x * xs;
                            sy = y * ys;
                            x0 = (int)sx;
                            y0 = (int)sy;

                            pd[srcIdx++] = ps[y0 * ws + x0];
                        }
                    }
                }
                // Bilinear
                else if (interpolation == Interpolation.Bilinear)
                {
                    var srcIdx = 0;
                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            sx = x * xs;
                            sy = y * ys;
                            x0 = (int)sx;
                            y0 = (int)sy;

                            // Calculate coordinates of the 4 interpolation points
                            fracx = sx - x0;
                            fracy = sy - y0;
                            ifracx = 1f - fracx;
                            ifracy = 1f - fracy;
                            x1 = x0 + 1;
                            if (x1 >= ws)
                                x1 = x0;
                            y1 = y0 + 1;
                            if (y1 >= hs)
                                y1 = y0;


                            // Read source color
                            c = ps[y0 * ws + x0];
                            c1a = (byte)(c >> 24);
                            c1r = (byte)(c >> 16);
                            c1g = (byte)(c >> 8);
                            c1b = (byte)(c);

                            c = ps[y0 * ws + x1];
                            c2a = (byte)(c >> 24);
                            c2r = (byte)(c >> 16);
                            c2g = (byte)(c >> 8);
                            c2b = (byte)(c);

                            c = ps[y1 * ws + x0];
                            c3a = (byte)(c >> 24);
                            c3r = (byte)(c >> 16);
                            c3g = (byte)(c >> 8);
                            c3b = (byte)(c);

                            c = ps[y1 * ws + x1];
                            c4a = (byte)(c >> 24);
                            c4r = (byte)(c >> 16);
                            c4g = (byte)(c >> 8);
                            c4b = (byte)(c);


                            // Calculate colors
                            // Alpha
                            l0 = ifracx * c1a + fracx * c2a;
                            l1 = ifracx * c3a + fracx * c4a;
                            a = (byte)(ifracy * l0 + fracy * l1);

                            if (a > 0)
                            {
                                // Red
                                l0 = ifracx * c1r * c1a + fracx * c2r * c2a;
                                l1 = ifracx * c3r * c3a + fracx * c4r * c4a;
                                r = (byte)((ifracy * l0 + fracy * l1) / a);

                                // Green
                                l0 = ifracx * c1g * c1a + fracx * c2g * c2a;
                                l1 = ifracx * c3g * c3a + fracx * c4g * c4a;
                                g = (byte)((ifracy * l0 + fracy * l1) / a);

                                // Blue
                                l0 = ifracx * c1b * c1a + fracx * c2b * c2a;
                                l1 = ifracx * c3b * c3a + fracx * c4b * c4a;
                                b = (byte)((ifracy * l0 + fracy * l1) / a);
                            }

                            // Write destination
                            pd[srcIdx++] = (a << 24) | (r << 16) | (g << 8) | b;
                        }
                    }
                }
#if !SILVERLIGHT
            }
            result.AddDirtyRect(new Int32Rect(0, 0, width, height));
            result.Unlock();
            bmp.Unlock();
#endif
            return result;
        }

        #endregion



        public static WriteableBitmap ResizeWritableBitmap(this WriteableBitmap wBitmap, int reqWidth, int reqHeight)
        {
            int Stride = wBitmap.PixelWidth * ((wBitmap.Format.BitsPerPixel + 7) / 8);
            int NumPixels = Stride * wBitmap.PixelHeight;
            ushort[] ArrayOfPixels = new ushort[NumPixels];


            wBitmap.CopyPixels(ArrayOfPixels, Stride, 0);

            int OriWidth = (int)wBitmap.PixelWidth;
            int OriHeight = (int)wBitmap.PixelHeight;

            double nXFactor = (double)OriWidth / (double)reqWidth;
            double nYFactor = (double)OriHeight / (double)reqHeight;

            double fraction_x, fraction_y, one_minus_x, one_minus_y;
            int ceil_x, ceil_y, floor_x, floor_y;

            ushort pix1, pix2, pix3, pix4;
            int nStride = reqWidth * ((wBitmap.Format.BitsPerPixel + 7) / 8);
            int nNumPixels = reqWidth * reqHeight;
            ushort[] newArrayOfPixels = new ushort[nNumPixels];
            /*Core Part*/
            /* Code project article :
Image Processing for Dummies with C# and GDI+ Part 2 - Convolution Filters By Christian Graus</a>

            href=<a href="http://www.codeproject.com/KB/GDI-plus/csharpfilters.aspx"></a>
            */
            for (int y = 0; y < reqHeight; y++)
            {
                for (int x = 0; x < reqWidth; x++)
                {
                    // Setup
                    floor_x = (int)Math.Floor(x * nXFactor);
                    floor_y = (int)Math.Floor(y * nYFactor);

                    ceil_x = floor_x + 1;
                    if (ceil_x >= OriWidth) ceil_x = floor_x;

                    ceil_y = floor_y + 1;
                    if (ceil_y >= OriHeight) ceil_y = floor_y;

                    fraction_x = x * nXFactor - floor_x;
                    fraction_y = y * nYFactor - floor_y;

                    one_minus_x = 1.0 - fraction_x;
                    one_minus_y = 1.0 - fraction_y;

                    pix1 = ArrayOfPixels[floor_x + floor_y * OriWidth];
                    pix2 = ArrayOfPixels[ceil_x + floor_y * OriWidth];
                    pix3 = ArrayOfPixels[floor_x + ceil_y * OriWidth];
                    pix4 = ArrayOfPixels[ceil_x + ceil_y * OriWidth];

                    ushort g1 = (ushort)(one_minus_x * pix1 + fraction_x * pix2);
                    ushort g2 = (ushort)(one_minus_x * pix3 + fraction_x * pix4);
                    ushort g = (ushort)(one_minus_y * (double)(g1) + fraction_y * (double)(g2));
                    newArrayOfPixels[y * reqWidth + x] = g;
                }
            }
            /*End of Core Part*/
            WriteableBitmap newWBitmap = new WriteableBitmap(reqWidth, reqHeight, 96, 96, PixelFormats.Gray16, null);
            Int32Rect Imagerect = new Int32Rect(0, 0, reqWidth, reqHeight);
            int newStride = reqWidth * ((PixelFormats.Gray16.BitsPerPixel + 7) / 8);
            newWBitmap.WritePixels(Imagerect, newArrayOfPixels, newStride, 0);
            return newWBitmap;
        }

    }
}