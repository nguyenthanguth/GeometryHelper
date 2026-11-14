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
    /// drawings in R2018 and Database.DxfOut at R12 of side databases): which sides of a notch in the outline of a solid fill
    /// AutoCAD draws, which depends on where the loop is walked from. The bodies are built here in code.
    /// </summary>
    public class DxfSaveR12FollowUp7Tests
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

        private static string Line(Record line) =>
            string.Join(
                ",",
                new[] { 10, 20, 11, 21 }.Select(c =>
                    Math.Round(line.Number(c), 2).ToString("0.##", CultureInfo.InvariantCulture)
                )
            );

        /// <summary>The loop as AutoCAD is given it from the corner at <paramref name="start"/>, forward or, with <paramref name="reversed"/>, backward.</summary>
        private static GeoPoint2[] Walk(GeoPoint2[] corners, int start, bool reversed)
        {
            var walk = new List<GeoPoint2>();

            for (int i = 0; i < corners.Length; i++)
            {
                walk.Add(corners[(start + i) % corners.Length]);
            }

            if (reversed)
            {
                GeoPoint2 first = walk[0];
                walk.RemoveAt(0);
                walk.Reverse();
                walk.Insert(0, first);
            }

            return walk.ToArray();
        }

        private static string[] LinesOf(params GeoPoint2[][] loops)
        {
            DxfDatabase database = Drawing();
            DxfHatch hatch = Fill(database, loops.Select((l, i) => i == 0 ? Outer(l) : Hole(l)).ToArray());
            return PiecesOf(database, hatch.Handle).Where(r => r.Type == "LINE").Select(Line).ToArray();
        }

        private static bool Drawn(string[] lines, double x0, double y0, double x1, double y1)
        {
            string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
            return lines.Contains(F(x0) + "," + F(y0) + "," + F(x1) + "," + F(y1))
                || lines.Contains(F(x1) + "," + F(y1) + "," + F(x0) + "," + F(y0));
        }

        // ---- a notch in the outline -----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(1, false, false)]
        [InlineData(2, false, true)]
        [InlineData(3, false, true)]
        [InlineData(4, false, true)]
        [InlineData(0, true, false)]
        [InlineData(1, true, true)]
        [InlineData(2, true, true)]
        [InlineData(3, true, false)]
        [InlineData(4, true, false)]
        public void AnArchWithItsTwoSidesOfEqualReach_HasThemDrawnOrNotAsTheLoopIsWalkedFrom(
            int start,
            bool reversed,
            bool drawn
        )
        {
            // AutoCAD 2021's R12 of (0,0) (50,40) (100,0) (100,60) (0,60): the two sides that run down from the notch at (50, 40) are
            // drawn when the loop starts at (100, 0), (100, 60) or (0, 60), and left out when it starts at (0, 0) or (50, 40): the
            // loop walked the other way round gives the opposite for a notch whose first side runs in from the other hand.
            string[] lines = LinesOf(Walk(Poly(0, 0, 50, 40, 100, 0, 100, 60, 0, 60), start, reversed));

            Assert.Equal(drawn, Drawn(lines, 0, 0, 50, 40));
            Assert.Equal(drawn, Drawn(lines, 50, 40, 100, 0));
            Assert.Equal(drawn ? 6 : 4, lines.Length);
        }

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(2, false, true)]
        [InlineData(3, false, true)]
        [InlineData(4, false, false)]
        [InlineData(1, true, true)]
        [InlineData(3, true, false)]
        public void ANotchFromTheTop_HasItsSidesDrawnOrNotAsTheLoopIsWalkedFrom(int start, bool reversed, bool drawn)
        {
            // The rectangle 100 by 60 with a V cut from its top, (0,0) (100,0) (100,60) (50,20) (0,60), is the arch upside down; its
            // sides at (50, 20) follow the same order as the arch's.
            string[] lines = LinesOf(Walk(Poly(0, 0, 100, 0, 100, 60, 50, 20, 0, 60), start, reversed));

            Assert.Equal(drawn, Drawn(lines, 0, 60, 50, 20));
            Assert.Equal(drawn, Drawn(lines, 50, 20, 100, 60));
        }

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(1, false, false)]
        [InlineData(2, false, false)]
        [InlineData(3, false, true)]
        [InlineData(5, false, true)]
        [InlineData(2, true, true)]
        [InlineData(3, true, true)]
        [InlineData(4, true, false)]
        public void ALevelSideBeforeTheNotch_IsNotCountedAsASideOfTheLoopWhereItIsWalkedFrom(
            int start,
            bool reversed,
            bool drawn
        )
        {
            // (-20,0) (0,0) (50,40) (100,0) (100,60) (-20,60) has a level side first from (-20, 0) and from (100, 60): the side that
            // runs in to the notch is the loop's first side from (-20, 0) all the same, as AutoCAD counts only the sides that rise or fall.
            string[] lines = LinesOf(Walk(Poly(-20, 0, 0, 0, 50, 40, 100, 0, 100, 60, -20, 60), start, reversed));

            Assert.Equal(drawn, Drawn(lines, 0, 0, 50, 40));
            Assert.Equal(drawn, Drawn(lines, 50, 40, 100, 0));
        }

        [Theory]
        [InlineData(0, false, 6)]
        [InlineData(1, false, 6)]
        [InlineData(4, false, 8)]
        [InlineData(2, true, 8)]
        [InlineData(3, true, 6)]
        public void AnArchWithSidesOfUnequalReach_IsWalkedFromItsCornerAsTheOtherArchIs(
            int start,
            bool reversed,
            int lines
        )
        {
            // (0,0) (50,40) (100,10) (100,60) (0,60): the notch at (50, 40) has sides that end at 0 and at 10, and their lines
            // follow the same order: 6 LINEs from (0, 0) and (50, 40) (the sides left out), 8 from (0, 60); walked backward 8 from
            // (100, 10), 6 from (100, 60).
            Assert.Equal(lines, LinesOf(Walk(Poly(0, 0, 50, 40, 100, 10, 100, 60, 0, 60), start, reversed)).Length);
        }

        [Fact]
        public void AFivePointedStar_WalkedFromItsTopPoint_HasTheSidesOfItsLowestNotchDrawn()
        {
            // The star of radius 50 and 20 has one notch with both sides running down, at (0, -20); walked from the top point,
            // anticlockwise, the side on the left comes first, so that its two sides are drawn: 12 LINEs, not 10.
            var corners = new List<GeoPoint2>();

            for (int i = 0; i < 10; i++)
            {
                double radius = i % 2 == 0 ? 50 : 20;
                double angle = Math.PI / 2 + i * Math.PI / 5;
                corners.Add(new GeoPoint2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
            }

            string[] lines = LinesOf(corners.ToArray());

            Assert.Equal(12, lines.Length);
            Assert.Contains("0,-20,-29.39,-40.45", lines);
            Assert.Contains("29.39,-40.45,0,-20", lines);
        }

        [Theory]
        [InlineData(0, 4)]
        [InlineData(4, 4)]
        [InlineData(6, 4)]
        public void AFillWithTwoNotches_KeepsTheirSidesLeftOut_FromTheStartsWhereAutoCadLeavesThemOut(
            int start,
            int lines
        )
        {
            // The W (0,60) (100,60) (100,0) (75,30) (50,0) (25,30) (0,0) has two notches; AutoCAD's choice for them is not a rule of
            // one notch each: from (0, 60), (50, 0) and (0, 0) it leaves the sides of both out (the starts where it draws them stay
            // as they were).
            Assert.Equal(
                lines,
                LinesOf(Walk(Poly(0, 60, 100, 60, 100, 0, 75, 30, 50, 0, 25, 30, 0, 0), start, false)).Length
            );
        }
    }
}
