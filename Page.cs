using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace OcrPageNo
{
    class Page
    {
        public string Filename { get; }
        public int Index { get; }
        public int ScanNo { get; }
        public PageNumber PageNo { get; set; }
        public int Score { get; set; }

        private SoftwareBitmap bitmap;
        public Page(string file, int index)
        {
            Filename = file;
            Index = index;
            var m = Regex.Match(Filename, @"_(?<No>\d+)\.\w+$");
            if (m.Success)
            {
                var no = m.Groups["No"].Value;
                ScanNo = int.Parse(no);
            }
            PageNo = new PageNumber(0, Position.None);
        }
        ~Page()
        {
            Close();
        }
        public async Task Open()
        {
            await Task.Run(() =>
            {
                Close();
                for (int i = 0; i < 5; i++)
                {
                    {
                        try
                        {
                            bitmap = Bitmap.Open(Filename);
                            return;
                        }
                        catch (OutOfMemoryException)
                        {
                            Console.Write("メモリー不足再試行({0}/5)\n", i + 1);

                        }
                        catch (Exception e)
                        {
                            Console.Write("open errr({0}){1}\n", i, e);
                        }
                    }
                }
            });
        }
        public void Close()
        {
            if (bitmap != null)
            {
                bitmap.Dispose();
                bitmap = null;
            }
        }
        public async Task<List<PageNumber>> Guess(int split)
        {
            var _open = false;
            if (bitmap == null)
            {
                await Open();
                if (bitmap == null)
                {
                    Console.WriteLine("画像を開けませんでした。");
                    Environment.Exit(1);
                }
                _open = true;
            }
            var list = new List<PageNumber>();

            int height = bitmap.PixelHeight / split;
            using (var newbmp = Bitmap.Crop(bitmap, 0, 0, bitmap.PixelWidth, height))
            {
                //await Bitmap.Save(newbmp,"test.png");
                var ocr = new OCR();
                var r = await ocr.Run(newbmp);
                int no = 0;
                int left = bitmap.PixelWidth / 3;
                int right = left * 2;
                foreach (var line in r.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        if (int.TryParse(word.Text, out no))
                        {
                            Position pos = Position.Top;
                            if (word.BoundingRect.Left < left)
                            {
                                pos |= Position.Left;
                            }
                            else if (word.BoundingRect.Left > right)
                            {
                                pos |= Position.Right;
                            }
                            else
                            {
                                pos |= Position.Center;
                            }
                            list.Add(new PageNumber(no, pos));
                        }
                    }
                }
            }

            using (var newbmp = Bitmap.Crop(bitmap, 0, bitmap.PixelHeight - height, bitmap.PixelWidth, height))
            {
                if (newbmp == null)
                {
                    Console.WriteLine("null");
                }
                else
                {
                    var ocr = new OCR();
                    var r = await ocr.Run(newbmp);
                    int no = 0;
                    int left = bitmap.PixelWidth / 3;
                    int right = left * 2;
                    foreach (var line in r.Lines)
                    {
                        foreach (var word in line.Words)
                        {
                            if (int.TryParse(word.Text, out no))
                            {
                                Position pos = Position.Bottom;
                                if (word.BoundingRect.Left < left)
                                {
                                    pos |= Position.Left;
                                }
                                else if (word.BoundingRect.Left > right)
                                {
                                    pos |= Position.Right;
                                }
                                else
                                {
                                    pos |= Position.Center;
                                }
                                list.Add(new PageNumber(no, pos));
                            }
                        }
                    }
                }
            }

            if (_open) Close();
            return list;
        }


        public async Task Scan(int split, PageNumber guess)
        {
            var _open = false;
            if (bitmap == null)
            {
                await Open();
                _open = true;
            }
            var list = new List<PageNumber>();

            int height = bitmap.PixelHeight / split;
            int top = 0;
            if ((guess.Pos & Position.Bottom) != 0)
                top = bitmap.PixelHeight - height;
            var side = (guess.Pos & (Position.Top | Position.Bottom));
            var min = 999999;

            var ocr = new OCR();
            using (var newbmp = Bitmap.Crop(bitmap, 0, top, bitmap.PixelWidth, height))
            {
                var r = await ocr.Run(newbmp);
                min = ocrResultCheck(r, min, side, guess);
                if (min != 0 && min != 999999)
                {
                    using (var b = Bitmap.Binarize(newbmp))
                    {
                        r = await ocr.Run(b);
                        min = ocrResultCheck(r, min, side, guess);
                    }
                }
            }
            if (_open) Close();
            switch (min)
            {
                case 0:
                    Score = 0;
                    break;
                case 999999:
                    Score = 1;
                    break;
                default:
                    Score = 2;
                    break;
            }

            return;
        }
        int ocrResultCheck(OcrResult r, int min, Position side, PageNumber guess)
        {
            int no = 0;
            int left = bitmap.PixelWidth / 3;
            int right = left * 2;

            foreach (var line in r.Lines)
            {
                foreach (var word in line.Words)
                {
                    if (int.TryParse(word.Text, out no))
                    {
                        Position pos = side;
                        if (word.BoundingRect.X < left)
                            pos |= Position.Left;
                        else if (word.BoundingRect.X > right)
                            pos |= Position.Right;
                        else
                            pos |= Position.Center;
                        var i = Math.Abs(guess.No - no);
                        if (min > i)
                        {
                            min = i;
                            PageNo.No = no;
                            PageNo.Pos = pos;
                            if (min == 0)
                                break;
                        }
                    }
                }
                if (min == 0)
                    break;
            }
            var yoko = OCR.GetYoko(r);
            foreach (var line in yoko)
            {
                if (int.TryParse(line.Text, out no))
                {
                    Position pos = side;
                    if (line.Rect.X < left)
                        pos |= Position.Left;
                    else if (line.Rect.X > right)
                        pos |= Position.Right;
                    else
                        pos |= Position.Center;
                    var i = Math.Abs(guess.No - no);
                    if (min > i)
                    {
                        min = i;
                        PageNo.No = no;
                        PageNo.Pos = pos;
                        if (min == 0)
                            break;
                    }
                }
            }
            return min;
        }


    }

}
