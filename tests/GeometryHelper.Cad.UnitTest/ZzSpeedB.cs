using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    public sealed class ZzSpeedB
    {
        [Fact]
        public void Speed()
        {
            var text = new StringBuilder("0\nSECTION\n2\nENTITIES\n");
            int n = 20000;

            for (int i = 0; i < n; i++)
            {
                text.Append("0\nLINE\n5\n")
                    .Append((0x1000 + i).ToString("X"))
                    .Append("\n8\n0\n10\n")
                    .Append(i)
                    .Append("\n20\n0\n30\n0\n11\n")
                    .Append(i + 1)
                    .Append("\n21\n1\n31\n0\n");
            }

            text.Append("0\nENDSEC\n0\nEOF\n");
            CadDocument document = CadDocument.From(DxfDatabase.Parse(text.ToString()), "big.dxf");
            CadCommandSession session = Session(document);
            var sb = new StringBuilder();

            string Time(string form)
            {
                var watch = Stopwatch.StartNew();
                session.Feed(form);
                watch.Stop();
                return form + " " + watch.ElapsedMilliseconds + " ms -> " + session.History[session.History.Count - 1];
            }

            sb.AppendLine(Time("(sslength (ssget \"X\"))"));
            sb.AppendLine(Time("(sslength (ssget \"X\" '((0 . \"LINE\"))))"));
            sb.AppendLine(Time("(sslength (ssget \"X\" '((8 . \"0\"))))"));
            sb.AppendLine(Time("(sslength (ssget \"X\" '((-4 . \">,>,*\") (10 100.0 0.0 0.0))))"));
            sb.AppendLine(
                Time("(progn (setq e (entnext)) (setq k 0) (while e (setq d (entget e) e (entnext e) k (1+ k))) k)")
            );
            sb.AppendLine(
                Time(
                    "(progn (setq s (ssget \"X\")) (setq k 0 i 0) (while (< i (sslength s)) (entget (ssname s i)) (setq i (1+ i))) i)"
                )
            );
            sb.AppendLine(Time("(progn (setq i 0) (repeat 200000 (setq x (sin 0.5) y (cos 0.5))) i)"));
            File.WriteAllText(@"D:\GitHub\GeometryHelper\.claude\scratch\lisp-l2-b\speed.txt", sb.ToString());
        }
    }
}
