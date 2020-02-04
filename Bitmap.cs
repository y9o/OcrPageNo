using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace OcrPageNo
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    class Bitmap
    {
        static public SoftwareBitmap Convert(SoftwareBitmap src)
        {
            return SoftwareBitmap.Convert(src, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        static public SoftwareBitmap Rotate270(SoftwareBitmap src)
        {
            var imageData = new byte[src.PixelWidth * src.PixelHeight * 4];
            int rotateHeight = src.PixelWidth;
            int rotateWidth = src.PixelHeight;
            var dst = new SoftwareBitmap(BitmapPixelFormat.Bgra8, rotateWidth, rotateHeight);
            unsafe
            {
                uint capacity;
                byte* dataInBytes;
                byte* dstDataInBytes;
                using (var buffer = src.LockBuffer(BitmapBufferAccessMode.Read))
                using (var dstBuffer = dst.LockBuffer(BitmapBufferAccessMode.Write))
                using (var read = buffer.CreateReference())
                using (var write = dstBuffer.CreateReference())
                {
                    {
                        ((IMemoryBufferByteAccess)read).GetBuffer(out dataInBytes, out capacity);
                        ((IMemoryBufferByteAccess)write).GetBuffer(out dstDataInBytes, out capacity);
                        BitmapPlaneDescription rBufferLayout = buffer.GetPlaneDescription(0);
                        BitmapPlaneDescription wBufferLayout = dstBuffer.GetPlaneDescription(0);
                        for (int i = 0; i < rBufferLayout.Height; i++)
                        {
                            for (int j = 0; j < rBufferLayout.Width; j++)
                            {
                                int dstOffset = wBufferLayout.StartIndex + wBufferLayout.Stride * (wBufferLayout.Height - 1 - j) + 4 * i;
                                int srcOffset = rBufferLayout.StartIndex + rBufferLayout.Stride * i + 4 * j;
                                dstDataInBytes[dstOffset] = dataInBytes[srcOffset];
                                dstDataInBytes[dstOffset + 1] = dataInBytes[srcOffset + 1];
                                dstDataInBytes[dstOffset + 2] = dataInBytes[srcOffset + 2];
                                dstDataInBytes[dstOffset + 3] = dataInBytes[srcOffset + 3];
                            }
                        }
                    }
                }
            }
            return dst;
        }

        static public SoftwareBitmap Binarize(SoftwareBitmap src)
        {
            int imageSize = src.PixelWidth * src.PixelHeight * 4;
            var imageData = new byte[imageSize];
            int width = src.PixelWidth;
            int height = src.PixelHeight;
            int graySize = width * height;
            byte[] grayData = new byte[graySize];
            int pos = 0;
            unsafe
            {
                using (var buffer = src.LockBuffer(BitmapBufferAccessMode.Read))
                {
                    uint capacity;
                    byte* dataInBytes;
                    using (var read = buffer.CreateReference())
                    {
                        ((IMemoryBufferByteAccess)read).GetBuffer(out dataInBytes, out capacity);
                        BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                        for (int i = 0; i < bufferLayout.Height; i++)
                        {
                            for (int j = 0; j < bufferLayout.Width; j++)
                            {
                                var offset = bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j;
                                byte grayScale = (byte)((dataInBytes[offset + 2] * 0.299) + (dataInBytes[offset + 1] * 0.587) + (dataInBytes[offset] * 0.114));
                                grayData[pos++] = grayScale;
                            }
                        }
                    }
                }
            }
            byte threshold = (byte)otsu_th(grayData, graySize);
            pos = 0;
            var dst = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height);
            unsafe
            {
                uint capacity;
                byte* dstDataInBytes;
                using (var dstBuffer = dst.LockBuffer(BitmapBufferAccessMode.Write))
                using (var write = dstBuffer.CreateReference())
                {
                    {
                        ((IMemoryBufferByteAccess)write).GetBuffer(out dstDataInBytes, out capacity);
                        BitmapPlaneDescription wBufferLayout = dstBuffer.GetPlaneDescription(0);
                        for (int i = 0; i < height; i++)
                        {
                            for (int j = 0; j < width; j++)
                            {
                                byte d = grayData[pos++] > threshold ? (byte)0xFF : (byte)0;
                                int dstOffset = wBufferLayout.StartIndex + wBufferLayout.Stride * i + 4 * j;
                                dstDataInBytes[dstOffset] = d;
                                dstDataInBytes[dstOffset + 1] = d;
                                dstDataInBytes[dstOffset + 2] = d;
                                dstDataInBytes[dstOffset + 3] = 0xFF;
                            }
                        }
                    }
                }
            }
            return dst;
        }

        static int otsu_th(byte[] image, int size)
        {
            //https://waka.cis.k.hosei.ac.jp/otsu_th.c
            var hist = new int[256];
            var prob = new double[256];
            var omega = new double[256];
            var myu = new double[256];
            var sigma = new double[256];
            double max_sigma = 0.0;
            int threshold = 0;
            for (int i = 0; i < 256; i++)
                hist[i] = 0;
            for (int i = 0; i < size; i++)
            {
                hist[image[i]]++;
            }

            for (int i = 0; i < 256; i++)
            {
                prob[i] = (double)hist[i] / size;
            }

            omega[0] = prob[0];
            myu[0] = 0.0;
            for (int i = 1; i < 256; i++)
            {
                omega[i] = omega[i - 1] + prob[i];
                myu[i] = myu[i - 1] + i * prob[i];
            }

            for (int i = 0; i < 256 - 1; i++)
            {
                if (omega[i] != 0.0 && omega[i] != 1.0)
                {
                    var xs = myu[256 - 1] * omega[i] - myu[i];
                    sigma[i] = xs * xs / (omega[i] * (1.0 - omega[i]));
                }
                else
                {
                    sigma[i] = 0.0;
                }
                if (sigma[i] > max_sigma)
                {
                    max_sigma = sigma[i];
                    threshold = i;
                }
            }
            return threshold;
        }



        static unsafe public SoftwareBitmap Crop(SoftwareBitmap src, int left, int top, int width, int height)
        {
            if (left < 0 || left + width > src.PixelWidth || width < 1)
            {
                return null;
            }
            if (top < 0 || top + height > src.PixelHeight || height < 1)
            {
                return null;
            }
            var dst = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height);

            {
                uint capacity;
                byte* dataInBytes;
                byte* dstDataInBytes;
                using (var buffer = src.LockBuffer(BitmapBufferAccessMode.Read))
                using (var dstBuffer = dst.LockBuffer(BitmapBufferAccessMode.Write))
                using (var read = buffer.CreateReference())
                using (var write = dstBuffer.CreateReference())
                {
                    {
                        ((IMemoryBufferByteAccess)read).GetBuffer(out dataInBytes, out capacity);
                        ((IMemoryBufferByteAccess)write).GetBuffer(out dstDataInBytes, out capacity);
                        BitmapPlaneDescription rBufferLayout = buffer.GetPlaneDescription(0);
                        BitmapPlaneDescription wBufferLayout = dstBuffer.GetPlaneDescription(0);

                        for (int y = 0; y < height; y++)
                        {
                            int srcOffset = rBufferLayout.StartIndex + rBufferLayout.Stride * (top + y) + 4 * left;
                            int dstOffset = wBufferLayout.StartIndex + wBufferLayout.Stride * y;
                            byte* _src = (byte*)dataInBytes + srcOffset;
                            byte* _dst = (byte*)dstDataInBytes + dstOffset;
                            for (int i = 0; i < width * 4; i++)
                                _dst[i] = _src[i];
                        }
                    }
                }
            }
            return dst;
        }

        static public async Task<bool> Save(SoftwareBitmap src, string file)
        {
            using (var mem = new InMemoryRandomAccessStream())
            {
                var wEncoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, mem);
                wEncoder.SetSoftwareBitmap(src);
                try
                {
                    await wEncoder.FlushAsync();
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                    return false;
                }
                using (var dest = new FileStream(file, FileMode.Create, FileAccess.Write))
                {

                    mem.Seek(0);
                    using (var dataReader = new DataReader(mem))
                    {
                        var buf = new byte[mem.Size];
                        await dataReader.LoadAsync((uint)mem.Size);
                        dataReader.ReadBytes(buf);
                        dest.Write(buf, 0, (int)mem.Size);
                    }
                }
            }
            return true;
        }

        static public SoftwareBitmap Open(string file)
        {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                var width = frame.PixelWidth;
                var height = frame.PixelHeight;
                var imageData = new byte[width * height * 4];
                if (frame.Format == System.Windows.Media.PixelFormats.Bgra32 || frame.Format == System.Windows.Media.PixelFormats.Bgr32)
                {
                    frame.CopyPixels(imageData, width * 4, 0);
                }
                else
                {
                    var formatConvertedBitmap = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
                    formatConvertedBitmap.CopyPixels(imageData, width * 4, 0);
                }
                return SoftwareBitmap.CreateCopyFromBuffer(imageData.AsBuffer(), BitmapPixelFormat.Bgra8, width, height);
            }
        }

    }
}
