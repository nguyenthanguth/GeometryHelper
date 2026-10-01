#nullable enable
using System;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Clipper2's own test of open paths (CSharp/Tests/Tests1/Tests/TestLines.cs of 2.0.0), from MSTest to xUnit:
    /// the cases of Lines.txt, their area and count measured against those stored.
    /// </summary>
    public class TestLines
    {
        [Fact]
        public void TestOpenPaths()
        {
            for (int i = 0; i <= 16; i++)
            {
                Clipper64 c64 = new();
                Paths64 subj = new(), subj_open = new(), clip = new();
                Paths64 solution = new(), solution_open = new();

                Assert.True(ClipperFileIO.LoadTestNum(TestFile.Of("Lines.txt"),
                  i, subj, subj_open, clip, out ClipType clipType, out FillRule fillrule,
                  out long area, out int count, out _),
                    string.Format("Loading test {0} failed.", i));

                c64.AddSubject(subj);
                c64.AddOpenSubject(subj_open);
                c64.AddClip(clip);
                c64.Execute(clipType, fillrule, solution, solution_open);

                if (area > 0)
                {
                    double area2 = Clipper2.Area(solution);
                    double a = area / area2;
                    Assert.True(a > 0.995 && a < 1.005,
                      string.Format("Incorrect area in test {0}", i));
                }

                if (count > 0 && Math.Abs(solution.Count - count) > 0)
                {
                    Assert.True(Math.Abs(solution.Count - count) < 2,
                      string.Format("Incorrect count in test {0}", i));
                }

            } //bottom of num loop
        }
    }
}
