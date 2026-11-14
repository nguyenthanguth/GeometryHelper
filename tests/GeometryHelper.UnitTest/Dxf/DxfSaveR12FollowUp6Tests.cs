using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What a drawing saved as R12 (AC1009) holds as AutoCAD 2021 writes it, found by probing its console (Database.DxfIn of the
    /// drawings in R2018 and Database.DxfOut at R12 of side databases): a solid fill with holes, where AutoCAD cuts the pieces
    /// beside a hole and which of the hole's sides it draws. The bodies are built here in code.
    /// </summary>
    public class DxfSaveR12FollowUp6Tests
    {
        private static DxfDatabase Drawing() => new DxfDatabase("AC1032");

        private sealed class Record
        {
            internal Record(string type)
            {
                Type = type;
                Groups = new List<KeyValuePair<int, string>>();
            }

            internal string Type { get; }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal string First(int code) => Groups.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();

            internal double Number(int code) => double.Parse(First(code), CultureInfo.InvariantCulture);
        }

        /// <summary>The records of the pieces an INSERT of the drawing stands for: those of its block, from the one after its BLOCK to the ENDBLK.</summary>
        private static List<Record> PiecesOf(DxfDatabase database, string handle)
        {
            string text;

            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1009");
                text = Encoding.GetEncoding(1252).GetString(stream.ToArray());
            }

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var records = new List<Record>();
            bool inBlocks = false;
            bool inEntities = false;
            Record record = null;
            var inserts = new List<Record>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    string name = lines[i + 3];
                    inBlocks = name == "BLOCKS";
                    inEntities = name == "ENTITIES";
                    record = null;
                    i += 2;
                    continue;
                }

                if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    inBlocks = false;
                    inEntities = false;
                    record = null;
                    continue;
                }

                if (!inBlocks && !inEntities)
                {
                    continue;
                }

                if (code == 0)
                {
                    record = new Record(value);

                    if (inBlocks)
                    {
                        records.Add(record);
                    }
                    else if (value == "INSERT")
                    {
                        inserts.Add(record);
                    }

                    continue;
                }

                record?.Groups.Add(new KeyValuePair<int, string>(code, value));
            }

            Record insert = inserts.Single(r => r.First(5) == handle);
            int start = records.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
            Assert.True(start >= 0, "no block " + insert.First(2));
            return records.Skip(start + 1).TakeWhile(r => r.Type != "ENDBLK").ToList();
        }

        private static DxfHatch Fill(DxfDatabase database, params Action<DxfHatch>[] loops)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");

            foreach (Action<DxfHatch> loop in loops)
            {
                loop(hatch);
            }

            database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        private static GeoPoint2[] Poly(params double[] xy) =>
            Enumerable.Range(0, xy.Length / 2).Select(i => new GeoPoint2(xy[2 * i], xy[2 * i + 1])).ToArray();

        private static GeoPoint2[] Rectangle(double x0, double y0, double x1, double y1) =>
            Poly(x0, y0, x1, y0, x1, y1, x0, y1);

        private static Action<DxfHatch> Outer(GeoPoint2[] corners) =>
            hatch => hatch.AppendLoop(DxfHatchLoopTypes.External, corners, new double[corners.Length]);

        private static Action<DxfHatch> Hole(GeoPoint2[] corners) =>
            hatch => hatch.AppendLoop(DxfHatchLoopTypes.Default, corners, new double[corners.Length]);

        private static Action<DxfHatch> Circle(DxfHatchLoopTypes type, double radius) =>
            hatch =>
                hatch.AppendLoop(
                    new DxfHatchLoop(
                        type,
                        new object[] { new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), radius) }
                    )
                );

        private static string Line(Record line) =>
            string.Join(
                ",",
                new[] { 10, 20, 11, 21 }.Select(c =>
                    Math.Round(line.Number(c), 3).ToString("0.###", CultureInfo.InvariantCulture)
                )
            );

        private static (int Solids, string[] Lines) Pieces(params Action<DxfHatch>[] loops)
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, loops);
            List<Record> pieces = PiecesOf(database, hatch.Handle);
            return (pieces.Count(r => r.Type == "SOLID"), pieces.Where(r => r.Type == "LINE").Select(Line).ToArray());
        }

        // ---- holes in a solid fill ----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(25.0, 50.0, 136, 134)]
        [InlineData(10.0, 30.0, 142, 142)]
        public void ADonut_IsCutBesideItsHoleAsAutoCadCutsIt(double inner, double outer, int solids, int lines)
        {
            // AutoCAD 2021's R12 of a circle of 25 in one of 50 is 136 SOLIDs and 134 LINEs, of 10 in 30 142 and 142: a piece beside
            // the hole is cut at the corners of its own two sides only, so that the two chains of corners, which stand at other
            // heights, cut the piece on their side and not the other, and the sides at the hole's lowest corner are drawn.
            (int Solids, string[] Lines) made = Pieces(
                Circle(DxfHatchLoopTypes.External, outer),
                Circle(DxfHatchLoopTypes.Default, inner)
            );

            Assert.Equal(solids, made.Solids);
            Assert.Equal(lines, made.Lines.Length);
        }

        [Fact]
        public void ARectangleWithAQuadrilateralHole_IsCutAtTheCornersOfItsOwnChainOnEachSide_NotAtThoseOfTheOther()
        {
            // The hole's corners stand at 50, 35, 30 and 10; the pieces on its left are cut at 50, 30 and 10 and those on its right
            // at 50, 35 and 10, but the piece on the right is cut at 30 as well, as the loop is walked from the corner at 30 up.
            (int Solids, string[] Lines) made = Pieces(
                Outer(Rectangle(0, 0, 100, 60)),
                Hole(Poly(30, 30, 45, 50, 70, 35, 55, 10))
            );

            Assert.Equal(14, made.Solids);
            Assert.Equal(12, made.Lines.Length);
            Assert.Contains("45,50,30,30", made.Lines);
            Assert.Contains("70,35,45,50", made.Lines);
            Assert.DoesNotContain("30,30,55,10", made.Lines);
        }

        [Fact]
        public void ARectangleWithAQuadrilateralHole_WalkedFromItsTopTheOtherWay_IsCutAtNoCornerOfTheOtherSide()
        {
            // The same hole walked from (45, 50) to (30, 30): the pieces on each side are cut at the corners of their own chain only,
            // 12 SOLIDs, and all four sides of the hole are drawn.
            (int Solids, string[] Lines) made = Pieces(
                Outer(Rectangle(0, 0, 100, 60)),
                Hole(Poly(45, 50, 30, 30, 55, 10, 70, 35))
            );

            Assert.Equal(12, made.Solids);
            Assert.Equal(12, made.Lines.Length);
            Assert.Contains("45,50,30,30", made.Lines);
            Assert.Contains("30,30,55,10", made.Lines);
            Assert.Contains("55,10,70,35", made.Lines);
            Assert.Contains("70,35,45,50", made.Lines);
        }

        [Fact]
        public void ADiamondHole_HasTheSidesRunningToItsTopDrawn_WhenTheLoopRunsOverTheTopFromTheLeft()
        {
            // The corners at 50 and 10 each have two sides that reach as far; the hole walked left, top, right, bottom passes over
            // its top from the left and over its bottom from the right: the upper two sides are drawn, the lower two are not.
            (int Solids, string[] Lines) made = Pieces(
                Outer(Rectangle(0, 0, 100, 60)),
                Hole(Poly(30, 30, 50, 50, 70, 30, 50, 10))
            );

            Assert.Equal(12, made.Solids);
            Assert.Equal(10, made.Lines.Length);
            Assert.Contains("50,50,30,30", made.Lines);
            Assert.Contains("70,30,50,50", made.Lines);
        }

        [Fact]
        public void AFillOnTheNormalMinusZ_HasItsSolidsOnTheExtrusionPlusZ_WrittenAsAutoCadWritesIt()
        {
            // The plane turned over is (0, 0, 1) here, which R12 leaves out of every other record; AutoCAD writes it on these SOLIDs.
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, Outer(Rectangle(0, 0, 100, 60)));
            hatch.Normal = new GeoVector3(0, 0, -1);

            List<Record> pieces = PiecesOf(database, hatch.Handle);

            Record[] solids = pieces.Where(r => r.Type == "SOLID").ToArray();
            Assert.Equal(2, solids.Length);
            Assert.All(solids, solid =>
            {
                Assert.Equal(0.0, solid.Number(210), 9);
                Assert.Equal(0.0, solid.Number(220), 9);
                Assert.Equal(1.0, solid.Number(230), 9);
            });
            Assert.All(pieces.Where(r => r.Type == "LINE"), line => Assert.Null(line.First(230)));
        }

        [Fact]
        public void DotsThatFallOnTheFarEdgeOfARectangle_AreLeftOut_ThoseOnTheNearEdgeAreKept()
        {
            // DOTS at scale 48 on a rectangle 60 by 30: the dots of the lines y = 12 and y = 24 stand every 12 from x = 0, and
            // AutoCAD 2021 writes the five of each that are from 0 to 48, and none at x = 60.
            DxfDatabase database = Drawing();
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "DOTS");
            hatch.SetPatternDefinitions(
                new[] { new DxfPatternDefinition(0.0, 0.0, 0.0, 0.0, 0.25, new[] { 0.0, -0.25 }) }
            );
            hatch.AppendLoop(DxfHatchLoopTypes.External, Rectangle(0, 0, 60, 30), new double[4]);
            hatch.PatternScale = 48.0;
            database.ModelSpace.AppendEntity(hatch);

            string[] dots = PiecesOf(database, hatch.Handle).Where(r => r.Type == "LINE").Select(Line).ToArray();

            Assert.Equal(10, dots.Length);
            Assert.Contains("0,12,0,12", dots);
            Assert.Contains("48,24,48,24", dots);
            Assert.DoesNotContain("60,12,60,12", dots);
            Assert.DoesNotContain("60,24,60,24", dots);
        }

        [Fact]
        public void AHoleWithAPocketOfFillInIt_IsNotCutAtThePocketsTip()
        {
            // The hole is a chevron, with a notch (40, 30) (50, 20) (60, 30) that is the fill's tip pointing down: the pieces on its
            // sides are not cut at the tip's height, 9 SOLIDs and 10 LINEs.
            (int Solids, string[] Lines) made = Pieces(
                Outer(Rectangle(0, 0, 100, 60)),
                Hole(Poly(30, 30, 50, 10, 70, 30, 60, 30, 50, 20, 40, 30))
            );

            Assert.Equal(9, made.Solids);
            Assert.Equal(10, made.Lines.Length);
        }
    }
}
