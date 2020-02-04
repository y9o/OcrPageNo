using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace OcrPageNo
{
    class OCRYokoText
    {
        public string Text;
        public Windows.Foundation.Rect Rect;
        public OCRYokoText(OcrWord word)
        {
            Text = word.Text;
            Rect = word.BoundingRect;
        }
    }
    class OCR
    {
        static Windows.Globalization.Language language = null;
        OcrEngine engine;
        public OCR()
        {
            engine = OcrEngine.TryCreateFromLanguage(language);
        }
        public static bool Lang(string lang)
        {
            try
            {
                var langtag = new Windows.Globalization.Language(lang);
                if (!OcrEngine.IsLanguageSupported(langtag))
                {
                    return false;
                }
                var engine = OcrEngine.TryCreateFromLanguage(langtag);
                if (engine == null)
                {
                    return false;
                }
                language = langtag;
            }
            catch
            {
                return false;
            }
            return true;
        }
        public async Task<OcrResult> Run(SoftwareBitmap bmp)
        {
            var result = await engine.RecognizeAsync(bmp);
            return result;
        }
        static public List<OCRYokoText> GetYoko(OcrResult result)
        {
            var list = new List<OCRYokoText>();
            foreach (var line in result.Lines)
            {
                foreach (var word in line.Words)
                {
                    bool flag = false;
                    for (var i = 0; i < list.Count; i++)
                    {
                        var t = list[i];
                        var y1 = Math.Abs(t.Rect.Y - word.BoundingRect.Y);
                        if (word.BoundingRect.Height - y1 > ((double)word.BoundingRect.Height) * 0.5)
                        {
                            var width = word.BoundingRect.Width / word.Text.Length;
                            var x1 = (t.Rect.X + t.Rect.Width) - (word.BoundingRect.X - width);
                            if (x1 >= 0 && x1 < width)
                            {
                                t.Text += word.Text;
                                t.Rect = word.BoundingRect;
                                list[i] = t;
                                flag = true;
                            }
                        }
                    }
                    if (!flag)
                    {
                        var t = new OCRYokoText(word);
                        list.Add(t);
                    }
                }
            }
            return list;
        }
    }
}
