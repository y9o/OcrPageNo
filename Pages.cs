using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OcrPageNo
{

    class Pages
    {
        public List<Page> List { get; }
        public int Split { get; set; }
        public Pages(string folder)
        {
            Split = 9;
            var list = System.IO.Directory.GetFiles(folder);
            List = new List<Page>(list.Length);
            int count = 1;
            foreach (var name in list)
            {
                if (name.EndsWith(".jpg") || name.EndsWith(".png") || name.EndsWith(".bmp"))
                {
                    List.Add(new Page(name, count));
                    count++;
                }
            }
        }

        public PageNumber Guess(int count = 10, int thread = 1)
        {
            int start = 0;
            if (count < List.Count)
            {
                start = (int)(List.Count / 2) - (int)(count / 2);
            }
            else
            {
                count = List.Count;
            }
            if (thread < 1)
                thread = System.Environment.ProcessorCount;
            var sem = new SemaphoreSlim(thread);

            var tasks = List.GetRange(start, count).Select(async page =>
            {
                await sem.WaitAsync();
                try
                {
                    return await page.Guess(Split);
                }
                finally
                {
                    sem.Release();
                }
            });
            var r = Task.WhenAll(tasks);
            r.Wait();
            var map = new Dictionary<PageNumber, int>();
            for (int i = 0; i < count; i++)
            {
                var ret = r.Result[i];
                foreach (var t in ret)
                {
                    var first = new PageNumber(t.No - (start + i), t.Pos);
                    if ((start + i) % 2 == 1)
                    {
                        first.Pos = (Position.Top | Position.Bottom | Position.Center) & first.Pos;
                        if ((t.Pos & Position.Left) != 0)
                            first.Pos |= Position.Right;
                        else if ((t.Pos & Position.Right) != 0)
                            first.Pos |= Position.Left;
                    }
                    if (map.ContainsKey(first))
                    {
                        map[first]++;
                    }
                    else
                    {
                        map[first] = 1;
                    }
                }
            }
            var guess = new PageNumber(-9999, Position.None);
            int max = 0;

            foreach (var no in map)
            {
                if (max < no.Value)
                {
                    max = no.Value;
                    guess = no.Key;
                }
            }

            return guess;
        }
        public void Check(PageNumber first, int thread = 1)
        {
            var even = first.Pos;
            var odd = first.Pos & (Position.Top | Position.Bottom | Position.Center);
            if ((even & Position.Left) != 0)
                odd |= Position.Right;
            else if ((even & Position.Right) != 0)
                odd |= Position.Left;

            var tmp = new PageNumber(first.No, even);
            Console.Write("スキャン中...");
            if (thread < 1)
                thread = System.Environment.ProcessorCount;
            var sem = new SemaphoreSlim(thread);
            var tasks = List.Select(async (page, i) =>
            {
                await sem.WaitAsync();
                try
                {
                    if (i % 5 == 0)
                        Console.Write(".");
                    var n = new PageNumber(first.No + i, i % 2 == 0 ? even : odd);
                    await page.Scan(Split, n);
                }
                finally
                {
                    sem.Release();
                }
            });
            var r = Task.WhenAll(tasks);
            r.Wait();
            Console.Write("終了\n");

            int success = 0;
            int notfound = 0;
            tmp = new PageNumber(first.No, even);
            int err = 0;
            int maxno = 0;
            for (int i = 0; i < List.Count; i++)
            {
                var page = List[i];
                tmp.Pos = i % 2 == 0 ? even : odd;
                switch (page.Score)
                {
                    case 0:
                        success++;
                        maxno = page.PageNo.No;
                        break;
                    case 1:
                        notfound++;
                        break;
                    default:
                        Console.Write("★{0,4:d0} {1} OCR№:{2}{3} 想定:{4}{5}\n"
                            , page.Index
                            , Path.GetFileName(page.Filename)
                            , page.PageNo.No
                            , (page.PageNo.Pos & Position.Left) != 0 ? "L" : "R"
                            , tmp.No
                            , (tmp.Pos & Position.Left) != 0 ? "L" : "R");
                        err++;
                        break;
                }
                tmp.No++;
            }
            Console.Write("認識最大㌻№:{4} 画像総数:{0} (認識P:{1} 無記載P:{2} 想定外P:{3})\n", List.Count, success, notfound, err, maxno);
            if (maxno > List.Count)
            {
                Console.WriteLine("★認識ページ番号({0})より、ファイル数({1})が少ないようです。", maxno, List.Count);
            }
        }

    }
}
