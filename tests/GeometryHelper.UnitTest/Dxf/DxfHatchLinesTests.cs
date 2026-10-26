using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The lines a HATCH's pattern draws as data, as AutoCAD's <c>EvaluateHatch</c>, <c>NumberOfHatchLines</c>,
    /// <c>GetHatchLineDataAt</c> and <c>GetHatchLinesData</c> give them: the lines <see cref="DxfHatch.Explode"/> draws, in the
    /// plane of the HATCH, to the bit, worked out again when the HATCH changes; and the colour of a gradient at a value along it,
    /// as AutoCAD's <c>EvaluateGradientColorAt</c>.
    /// </summary>
    public class DxfHatchLinesTests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-9, 1E-9);

        #region The lines of the pattern

        [Fact]
        public void TheLinesOfEveryHatchOfTheDrawings_AreThoseExplodeDraws_InTheirPlane()
        {
            // AC (ANSI31 round a polyline with an arc) and B0 (ANSI37 in a whole ellipse) lie on the floor, where a line's ends
            // are Explode's to the bit; AE lies tilted at the elevation 5, where its ends are Explode's taken into its plane.
            foreach (string text in new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (string handle in new[] { "AC", "B0", "AE" })
                {
                    DxfHatch hatch = ByHandle<DxfHatch>(db, handle);
                    DxfLine[] parts = hatch.Explode().Cast<DxfLine>().ToArray();

                    hatch.EvaluateHatch(false);
                    IReadOnlyList<GeoLine2> lines = hatch.GetHatchLinesData();

                    Assert.NotEmpty(parts);
                    Assert.Equal(parts.Length, hatch.NumberOfHatchLines);
                    Assert.Equal(parts.Length, lines.Count);

                    for (int i = 0; i < parts.Length; i++)
                    {
                        GeoLine2 line = hatch.GetHatchLineDataAt(i);
                        Assert.Equal(Bits(lines[i].StartPoint.X) + Bits(lines[i].StartPoint.Y) + Bits(lines[i].EndPoint.X) + Bits(lines[i].EndPoint.Y), Bits(line.StartPoint.X) + Bits(line.StartPoint.Y) + Bits(line.EndPoint.X) + Bits(line.EndPoint.Y));

                        if (handle != "AE")
                        {
                            Assert.Equal(Bits(parts[i].StartPoint.X) + Bits(parts[i].StartPoint.Y), Bits(line.StartPoint.X) + Bits(line.StartPoint.Y));
                            Assert.Equal(Bits(parts[i].EndPoint.X) + Bits(parts[i].EndPoint.Y), Bits(line.EndPoint.X) + Bits(line.EndPoint.Y));
                            continue;
                        }

                        Assert.True(InPlane(hatch.Normal, parts[i].StartPoint).IsEqualTo(line.StartPoint, Fine), parts[i].StartPoint + " " + line.StartPoint);
                        Assert.True(InPlane(hatch.Normal, parts[i].EndPoint).IsEqualTo(line.EndPoint, Fine), parts[i].EndPoint + " " + line.EndPoint);
                    }
                }
            }
        }

        [Fact]
        public void ASquareFilledWithLinesAt45Degrees_Draws11_And14RoundACircle_EachFromSideToSide()
        {
            // A square 30 by 20, lines at 45 degrees 3.175 apart, as ANSI31: 11 lines; the same round a circle of radius 5 about
            // its middle in the normal style, 14, the three that cross the circle broken in two.
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = Math.PI / 4.0;
            hatch.PatternSpace = 3.175;
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);

            IReadOnlyList<GeoLine2> lines = hatch.GetHatchLinesData();

            Assert.Equal(11, hatch.NumberOfHatchLines);
            Assert.Equal(11, lines.Count);
            Assert.All(lines, l => Assert.Equal(l.StartPoint.X - l.StartPoint.Y, l.EndPoint.X - l.EndPoint.Y, 9));
            Assert.All(lines, l => Assert.True(l.EndPoint.X > l.StartPoint.X));
            bool OnSide(GeoPoint2 p) => Math.Abs(p.X) < 1E-9 || Math.Abs(p.X - 30) < 1E-9 || Math.Abs(p.Y) < 1E-9 || Math.Abs(p.Y - 20) < 1E-9;
            Assert.All(lines, l => Assert.True(OnSide(l.StartPoint) && OnSide(l.EndPoint), l.StartPoint + " " + l.EndPoint));

            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Outermost, new object[] { new GeoCircle3(new GeoPoint3(15, 10, 0), new GeoVector3(0, 0, 1), 5) }));

            Assert.Equal(14, hatch.NumberOfHatchLines);
            Assert.All(hatch.GetHatchLinesData(), l => Assert.True(l.MidPoint.DistanceTo(new GeoPoint2(15, 10)) > 5));
        }

        [Fact]
        public void TheLines_AreWorkedOutAgain_WhenTheLoopsThePatternOrTheStyleChange()
        {
            // A square 30 by 20 of lines at 45 degrees 3.175 apart, 11; a circle of radius 5 appended, spliced into the record,
            // 14; taken out, 11; the lines 6.35 apart, 6; the circle back, 8, the two lines that cross it broken in two; its
            // inside hatched again, ignoring islands, 6.
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = Math.PI / 4.0;
            hatch.PatternSpace = 3.175;
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            db.ModelSpace.AppendEntity(hatch);
            var circle = new DxfHatchLoop(DxfHatchLoopTypes.Outermost, new object[] { new GeoCircle3(new GeoPoint3(15, 10, 0), new GeoVector3(0, 0, 1), 5) });

            Assert.Equal(11, hatch.NumberOfHatchLines);
            hatch.AppendLoop(circle);
            Assert.Equal(14, hatch.NumberOfHatchLines);
            Assert.Equal(14, hatch.GetHatchLinesData().Count);
            hatch.RemoveLoopAt(1);
            Assert.Equal(11, hatch.NumberOfHatchLines);
            hatch.PatternSpace = 6.35;
            Assert.Equal(6, hatch.NumberOfHatchLines);
            hatch.AppendLoop(circle);
            Assert.Equal(8, hatch.NumberOfHatchLines);
            hatch.HatchStyle = DxfHatchStyle.Ignore;
            Assert.Equal(6, hatch.NumberOfHatchLines);
        }

        [Fact]
        public void TheDashesAndDotsOfAPattern_AreItsLines_ADotFromAPointToItself()
        {
            // One pattern line along x, 2 apart, a dash 2, a gap 1, a dot and a gap 1, in the square from (0, 0.5) to (10, 9.5):
            // the lines y = 2, 4, 6 and 8, each a dash from 0 to 2, a dot at 3, a dash from 4 to 6, a dot at 7, a dash from 8 to 10.
            DxfDatabase db = WithRecords(Hatch("B10", "GH_DASHED", "0.0", "0.5", "10.0", "9.5", "2.0", "2.0", "-1.0", "0.0", "-1.0"));
            DxfHatch hatch = ByHandle<DxfHatch>(db, "B10");

            GeoLine2[] lines = hatch.GetHatchLinesData().ToArray();

            Assert.Equal(20, lines.Length);
            Assert.Equal(new[] { 0.0, 2, 3, 3, 4, 6, 7, 7, 8, 10 }, lines.Take(5).SelectMany(l => new[] { Math.Round(l.StartPoint.X, 12), Math.Round(l.EndPoint.X, 12) }));
            Assert.Equal(new[] { 2.0, 4, 6, 8 }, lines.Where((l, i) => i % 5 == 0).Select(l => l.StartPoint.Y));
            Assert.Equal(8, lines.Count(l => l.Length == 0.0));
            Assert.Equal(0.0, hatch.GetHatchLineDataAt(1).Length);
        }

        [Fact]
        public void ASolidFillAndAGradient_DrawNoLines_AndAPatternTheHatchHoldsNoLinesOfIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var named = new DxfHatch();
            named.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            named.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, null);

            foreach (string handle in new[] { "AD", "AF" })
            {
                DxfHatch hatch = ByHandle<DxfHatch>(db, handle);
                hatch.EvaluateHatch(true);

                Assert.Equal(0, hatch.NumberOfHatchLines);
                Assert.Empty(hatch.GetHatchLinesData());
                Assert.Throws<ArgumentOutOfRangeException>(() => hatch.GetHatchLineDataAt(0));
            }

            Assert.Contains("ANSI31", Assert.Throws<NotSupportedException>(() => named.EvaluateHatch(false)).Message, StringComparison.Ordinal);
            Assert.Throws<NotSupportedException>(() => named.NumberOfHatchLines);
            Assert.Throws<NotSupportedException>(() => named.GetHatchLinesData());
            Assert.Throws<NotSupportedException>(() => named.GetHatchLineDataAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ByHandle<DxfHatch>(db, "AC").GetHatchLineDataAt(-1));
        }

        [Fact]
        public void APatternOfMoreThanTheBoundOfLines_IsRefusedAsExplodeRefusesIt()
        {
            // One line of dashes 0.0005 long and 0.001 apart along a stretch 2 000.0005 long: 2 000 001 dashes, one past the 2 000 000.
            DxfDatabase db = WithRecords(Hatch("B10", "GH_DENSE", "0.0", "-0.5", "2000.0005", "0.5", "1000.0", "0.0005", "-0.0005"));
            DxfHatch hatch = ByHandle<DxfHatch>(db, "B10");

            Assert.Contains("2000000", Assert.Throws<InvalidOperationException>(() => hatch.EvaluateHatch(false)).Message, StringComparison.Ordinal);
            Assert.Contains("2000000", Assert.Throws<InvalidOperationException>(() => hatch.NumberOfHatchLines).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AThousandLinesReadByTheirIndex_TakeLessThanTenWorkingsOutOfTheLines_TheFileUnchanged()
        {
            // A square 1 000 by 100 of lines along x 0.005 apart: 20 000 lines, each 1 000 long. Working them out is timed once
            // on a hatch of its own; on another, the count and a thousand lines read by their index are given ten times that, a
            // second at least, and stop there: worked out for each line, they would take a thousand.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch Made()
            {
                var made = new DxfHatch();
                made.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
                made.PatternSpace = 0.005;
                made.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0.001), new GeoPoint2(1000, 0.001), new GeoPoint2(1000, 100.001), new GeoPoint2(0, 100.001) }, null);
                db.ModelSpace.AppendEntity(made);
                return made;
            }

            Made().EvaluateHatch(false);
            DxfHatch timed = Made();
            var watch = Stopwatch.StartNew();
            timed.EvaluateHatch(false);
            long bound = Math.Max(10 * watch.ElapsedTicks, Stopwatch.Frequency);
            DxfHatch hatch = Made();
            string before = db.ToText();
            watch.Restart();

            int count = hatch.NumberOfHatchLines;
            int read = 0;
            double length = 0.0;

            for (; read < 1000 && watch.ElapsedTicks < bound; read++)
            {
                length += hatch.GetHatchLineDataAt(read * 20).Length;
            }

            Assert.Equal(20000, count);
            Assert.Equal(1000, read);
            Assert.Equal(1000000.0, length, 6);
            Assert.Equal(before, db.ToText());
        }

        #endregion

        #region The colour of a gradient

        [Fact]
        public void TheColourOfAGradient_IsItsColoursInProportion_RoundedHalfAwayFromNought_AndItsEndsBeyondThem()
        {
            // AutoCAD's new gradient runs from blue (0, 0, 255) to a pale yellow (255, 255, 153): halfway (127.5, 127.5, 204).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");
            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
            string before = db.ToText();

            DxfColor half = hatch.EvaluateGradientColorAt(0.5f);

            Assert.Equal(0x8080CC, half.ColorValue);
            Assert.Equal(DxfColorMethod.ByColor, half.ColorMethod);
            Assert.Equal(0x0000FF, hatch.EvaluateGradientColorAt(0f).ColorValue);
            Assert.Equal(0xFFFF99, hatch.EvaluateGradientColorAt(1f).ColorValue);
            Assert.Equal(0x0000FF, hatch.EvaluateGradientColorAt(-1f).ColorValue);
            Assert.Equal(0xFFFF99, hatch.EvaluateGradientColorAt(2f).ColorValue);
            Assert.Equal(before, db.ToText());

            // The gradient of ezdxf, red to blue: a quarter along (191.25, 0, 63.75).
            Assert.Equal(0xBF0040, ByHandle<DxfHatch>(db, "AF").EvaluateGradientColorAt(0.25f).ColorValue);
        }

        [Fact]
        public void AColourOfTheIndex_IsTakenByItsRedGreenAndBlue_AndColoursGivenOutOfOrderBySortedValue()
        {
            // Red of the index 1 at 1 and the true green (0, 128, 0) at 0, given in that order: a quarter along (63.75, 96, 0).
            DxfHatch hatch = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AF");
            hatch.SetGradientColors(new[] { new DxfGradientColor(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), 1f), new DxfGradientColor(DxfColor.FromRgb(0, 128, 0), 0f) });

            Assert.Equal(0x406000, hatch.EvaluateGradientColorAt(0.25f).ColorValue);
            DxfColor end = hatch.EvaluateGradientColorAt(1f);
            Assert.Equal(0xFF0000, end.ColorValue);
            Assert.True(end.IsByColor);
        }

        [Fact]
        public void AGradientOfOneColourHoldingThatColourAlone_RunsToItsShadeOrTint_AndOfTwoColoursBetweenThemInEitherMode()
        {
            // Red alone at 0: tinted by 0.75 it runs to (255, 127.5, 127.5) at 1, halfway (255, 63.75, 63.75); shaded by 0.25 to
            // (127.5, 0, 0); by 0.5 it is red throughout. Red alone at 1 runs from its tint at 0. Not of one colour, red alone is
            // red throughout.
            DxfHatch hatch = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AF");
            hatch.SetGradientColors(new[] { new DxfGradientColor(DxfColor.FromRgb(255, 0, 0), 0f) });

            Assert.Equal(0xFF0000, hatch.EvaluateGradientColorAt(1f).ColorValue);

            hatch.GradientOneColorMode = true;
            hatch.ShadeTintValue = 0.75f;
            Assert.Equal(0xFF8080, hatch.EvaluateGradientColorAt(1f).ColorValue);
            Assert.Equal(0xFF4040, hatch.EvaluateGradientColorAt(0.5f).ColorValue);
            Assert.Equal(0xFF0000, hatch.EvaluateGradientColorAt(0f).ColorValue);
            hatch.ShadeTintValue = 0.25f;
            Assert.Equal(0x800000, hatch.EvaluateGradientColorAt(1f).ColorValue);
            hatch.ShadeTintValue = 0.5f;
            Assert.Equal(0xFF0000, hatch.EvaluateGradientColorAt(1f).ColorValue);

            hatch.SetGradientColors(new[] { new DxfGradientColor(DxfColor.FromRgb(255, 0, 0), 1f) });
            hatch.ShadeTintValue = 0.75f;
            Assert.Equal(0xFF8080, hatch.EvaluateGradientColorAt(0f).ColorValue);
            Assert.Equal(0xFF4040, hatch.EvaluateGradientColorAt(0.5f).ColorValue);
            Assert.Equal(0xFF0000, hatch.EvaluateGradientColorAt(1f).ColorValue);

            hatch.SetGradientColors(new[] { new DxfGradientColor(DxfColor.FromRgb(255, 0, 0), 0f), new DxfGradientColor(DxfColor.FromRgb(0, 0, 255), 1f) });
            hatch.ShadeTintValue = 1f;
            Assert.Equal(0x0000FF, hatch.EvaluateGradientColorAt(1f).ColorValue);
        }

        [Fact]
        public void TheColourOfAGradient_IsRefusedForAValueThatIsNoNumber_AndAHatchOfNoGradient()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            Assert.Throws<ArgumentOutOfRangeException>(() => ByHandle<DxfHatch>(db, "AF").EvaluateGradientColorAt(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ByHandle<DxfHatch>(db, "AF").EvaluateGradientColorAt(float.PositiveInfinity));
            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AF").EvaluateGradientColorAt(0.5f));
            Assert.Throws<InvalidOperationException>(() => new DxfHatch().EvaluateGradientColorAt(0.5f));
        }

        #endregion

        /// <summary>A point of the world in the system of a normal, by AutoCAD's arbitrary axis algorithm; its height dropped.</summary>
        private static GeoPoint2 InPlane(GeoVector3 normal, GeoPoint3 p)
        {
            GeoVector3 x = Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0
                ? new GeoVector3(0, 1, 0).CrossProduct(normal).Normalize()
                : new GeoVector3(0, 0, 1).CrossProduct(normal).Normalize();
            GeoVector3 y = normal.CrossProduct(x).Normalize();
            return new GeoPoint2(p.X * x.X + p.Y * x.Y + p.Z * x.Z, p.X * y.X + p.Y * y.Y + p.Z * y.Z);
        }

        /// <summary>A HATCH of one square loop from a corner to another, one pattern line along x of an offset and dashes.</summary>
        private static string Hatch(string handle, string name, string x0, string y0, string x1, string y1, string offset, params string[] dashes)
            => "  0\nHATCH\n  5\n" + handle + "\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbHatch\n 10\n0.0\n 20\n0.0\n 30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n"
                + "  2\n" + name + "\n 70\n0\n 71\n0\n 91\n1\n 92\n3\n 72\n0\n 73\n1\n 93\n4\n 10\n" + x0 + "\n 20\n" + y0 + "\n 10\n" + x1 + "\n 20\n" + y0
                + "\n 10\n" + x1 + "\n 20\n" + y1 + "\n 10\n" + x0 + "\n 20\n" + y1 + "\n 97\n0\n"
                + " 75\n0\n 76\n2\n 52\n0.0\n 41\n1.0\n 77\n0\n 78\n1\n 53\n0.0\n 43\n0.0\n 44\n0.0\n 45\n0.0\n 46\n" + offset + "\n 79\n" + dashes.Length + "\n"
                + string.Concat(dashes.Select(d => " 49\n" + d + "\n")) + " 98\n0\n";

        /// <summary>The drawing of ezdxf's R2018 with entities added at the end of its ENTITIES, its handle seed raised past them.</summary>
        private static DxfDatabase WithRecords(string entities)
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nC00\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            return DxfDatabase.Parse(text.Insert(end, entities));
        }
    }
}
