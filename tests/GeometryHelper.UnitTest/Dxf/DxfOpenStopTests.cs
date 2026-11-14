using System;
using System.IO;
using System.Text;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A DXF opened from a path that can be stopped: the walk over its records looks at a token as it goes, so that a file of
    /// a million entities, whose walk takes a good part of a second, is given up at once and not at its end.
    /// </summary>
    public class DxfOpenStopTests
    {
        /// <summary>The text of a drawing of <paramref name="count"/> lines, 15 groups each.</summary>
        private static byte[] Lines(int count)
        {
            var text = new StringBuilder("0\nSECTION\n2\nENTITIES\n", count * 80);

            for (int i = 0; i < count; i++)
            {
                text.Append("0\nLINE\n5\n")
                    .Append((0x100 + i).ToString("X"))
                    .Append("\n8\n0\n10\n0\n20\n0\n30\n0\n11\n1\n21\n1\n31\n0\n");
            }

            text.Append("0\nENDSEC\n0\nEOF\n");
            return new UTF8Encoding(false).GetBytes(text.ToString());
        }

        private static string Written(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "dxfstop-" + Guid.NewGuid().ToString("N") + ".dxf");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [Fact]
        public void AFileOfTwentyThousandLines_OpenedWithAStoppedToken_IsGivenUpInTheWalkOverItsRecords()
        {
            string path = Written(Lines(20000));

            try
            {
                using (var stopped = new CancellationTokenSource())
                {
                    stopped.Cancel();

                    Assert.Throws<OperationCanceledException>(() => DxfDatabase.Open(path, stopped.Token));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TheScanOfTheHandlesAndTheListOfTheEntities_AreGivenUpWhenTheTokenIsStopped_AndMadeWholeAfter()
        {
            byte[] bytes = Lines(20000);
            DxfFile asked = DxfFile.Open(bytes, bytes.Length);
            DxfFile ahead = DxfFile.Open(bytes, bytes.Length);
            DxfFile plain = DxfFile.Open(bytes, bytes.Length);

            using (var stopped = new CancellationTokenSource())
            {
                stopped.Cancel();

                Assert.Throws<OperationCanceledException>(() => ahead.KnowHandles(stopped.Token));
                Assert.Throws<OperationCanceledException>(() => ahead.ReadDrawingAhead(stopped.Token));
            }

            // Given up, nothing is kept: what is asked of the file next is made whole, as of a file never asked ahead.
            Assert.Equal(plain.WritesHandles, ahead.WritesHandles);
            Assert.Equal(20000, ahead.ModelSpace.Count);

            // Asked ahead with a token not stopped, the list is the one the first ask makes.
            using (var running = new CancellationTokenSource())
            {
                asked.KnowHandles(running.Token);
                asked.ReadDrawingAhead(running.Token);
            }

            Assert.Equal(plain.ModelSpace.Count, asked.ModelSpace.Count);
            Assert.Equal(plain.ToText(), asked.ToText());
        }

        [Fact]
        public void AFileOpenedWithATokenNotStopped_IsTheDrawingOpenWithoutOneGives()
        {
            string path = Written(Lines(20000));

            try
            {
                using (var running = new CancellationTokenSource())
                {
                    DxfDatabase with = DxfDatabase.Open(path, running.Token);
                    DxfDatabase without = DxfDatabase.Open(path);

                    Assert.Equal(without.ToText(), with.ToText());
                    Assert.Equal(20000, with.ModelSpace.Count);
                    Assert.Equal(without.ModelSpace.Count, with.ModelSpace.Count);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TheBytesOfAReadAhead_OpenedWithAStoppedToken_AreGivenUpInTheWalkOverTheirRecords()
        {
            byte[] bytes = Lines(20000);

            using (var stopped = new CancellationTokenSource())
            {
                stopped.Cancel();
                Assert.Throws<OperationCanceledException>(() => DxfDatabase.Open(bytes, bytes.Length, stopped.Token));
            }
        }
    }
}
