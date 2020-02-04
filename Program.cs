using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace OcrPageNo
{
    class Program
    {
        class Options
        {
            [Option('t', "thread", Required = false, Default = 2, HelpText = "並列処理。0で自動。メモリも消費します")]
            public int thread { get; set; }

            [Option('g', "guess", Required = false, Default = 10, HelpText = "ページ番号位置を推測するために計測する画像枚数")]
            public int guess { get; set; }

            [Option('s', "split", Required = false, Default = 9, HelpText = "ページ番号の縦位置(9分割して一番上か下)")]
            public int split { get; set; }

            [Value(0, MetaName = "Input", Required = true, HelpText = "入力フォルダ")]
            public string Input { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(scanFolder);

        }

        static void scanFolder(Options op)
        {
#if DEBUG
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
#endif
            if (!OCR.Lang("en-US"))
            {
                Console.WriteLine("OCRにen-USを指定できません。\nWin10設定からen-USをインストールしてください。");
                Environment.Exit(1);
            }
            if (op.guess < 1)
            {
                op.guess = 10;
            }

            if (System.IO.Directory.Exists(op.Input))
            {
                var pages = new Pages(op.Input);
                pages.Split = op.split;
                var guess = pages.Guess(op.guess, op.thread);
                while (guess.Pos == Position.None && --op.split > 3)
                {
                    pages.Split = op.split;
                    guess = pages.Guess(op.guess, op.thread);
                }
                if (guess.Pos == Position.None)
                {
                    Console.WriteLine("ページ番号を検出できませんでした。");
                    Environment.Exit(1);
                }
                Console.Write("1枚目画像はページ番号「{0}」で", guess.No);
                if ((guess.Pos & Position.Top) == Position.Top)
                    Console.Write("上部・");
                if ((guess.Pos & Position.Bottom) == Position.Bottom)
                    Console.Write("下部・");
                if ((guess.Pos & Position.Left) == Position.Left)
                    Console.Write("左側");
                if ((guess.Pos & Position.Right) == Position.Right)
                    Console.Write("右側");
                if ((guess.Pos & Position.Center) == Position.Center)
                    Console.Write("中央");
                Console.WriteLine("に印字されていると想定。");

                pages.Check(guess, op.thread);
            }
            else
            {
                Console.WriteLine("フォルダを指定してください");
                Environment.Exit(1);
            }
#if DEBUG
            sw.Stop();
            Console.WriteLine("{0}ミリ秒", sw.ElapsedMilliseconds);
            Console.WriteLine("何かキーを押してください");
            Console.ReadKey();
#endif
        }
    }
}
