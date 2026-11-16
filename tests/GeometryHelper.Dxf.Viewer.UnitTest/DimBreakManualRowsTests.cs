using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIMBREAK Manual against AutoCAD 2021's console run through standard input (16 November), 45 of some 400 breaks compared:
    /// each row is a dimension, the two points picked, the lines of its picture the break took out and those it put in their
    /// place (the same for the arcs of an angular one), and the line AutoCAD says it broke (91: 0 and 1 the extension lines,
    /// 3 the dimension line or the arc). The line broken is the one nearest the first point; both points are carried along
    /// it, and the stretch between them is left out where at least one falls on what the dimension draws of the line.
    /// Rows where AutoCAD, picking in a view, took a point near the dimension's own origin for that origin are not here.
    /// </summary>
    public class DimBreakManualRowsTests
    {
        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static double[] Numbers(string text) => text.Split(',').Select(Number).ToArray();

        private static List<double[]> Shapes(string text) =>
            text.Length == 0 ? new List<double[]>() : text.Split(';').Select(Numbers).ToList();

        /// <summary>A line as its ends in order, the lesser x first (the lesser y where they are level), so that two are compared as sets.</summary>
        private static double[] Ordered(double[] s) =>
            s[0] > s[2] + 1E-9 || (Math.Abs(s[0] - s[2]) <= 1E-9 && s[1] > s[3]) ? new[] { s[2], s[3], s[0], s[1] } : s;

        private static bool Has(IEnumerable<double[]> shapes, double[] shape, double tolerance) =>
            shapes.Any(s =>
                s.Length == shape.Length && s.Zip(shape, (a, b) => Math.Abs(a - b) <= tolerance).All(x => x)
            );

        /// <summary>The rows AutoCAD gave: the dimension, the points picked, the lines and arcs taken out, the lines and arcs put in their place, the line broken.</summary>
        public static readonly IEnumerable<object[]> Rows = new[]
        {
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "30,20;40,20",
                "0.18,20,99.82,20",
                "0.18,20,30,20;40,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "40,20;30,20",
                "0.18,20,99.82,20",
                "0.18,20,30,20;40,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "0,8;0,12",
                "0,0.0625,0,20.18",
                "0,0.0625,0,8;0,12,0,20.18",
                "",
                "",
                0,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "100,8;100,12",
                "100,0.0625,100,20.18",
                "100,0.0625,100,8;100,12,100,20.18",
                "",
                "",
                1,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "0,10;100,10",
                "0,0.0625,0,20.18",
                "0,0.0625,0,10;0,10,0,20.18",
                "",
                "",
                0,
            },
            new object[]
            {
                "lin 0 0 100 0 50 -20 0",
                "30,-20;40,-20",
                "0.18,-20,99.82,-20",
                "0.18,-20,30,-20;40,-20,99.82,-20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 -20 0",
                "0,-8;0,-12",
                "0,-20.18,0,-0.0625",
                "0,-20.18,0,-12;0,-8,0,-0.0625",
                "",
                "",
                0,
            },
            new object[]
            {
                "ali 0 0 100 0 50 20",
                "30,20;40,20",
                "0.18,20,99.82,20",
                "0.18,20,30,20;40,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "ali 0 0 80 60 50 -10",
                "40,20;45,25",
                "22.944,-30.292,102.656,29.492",
                "22.944,-30.292,58,-4;63.6,0.2,102.656,29.492",
                "",
                "",
                3,
            },
            new object[]
            {
                "ali 0 0 80 60 50 -10",
                "86,58;80,60",
                "80.0375,59.95,102.908,29.456",
                "83.12,55.84,102.908,29.456",
                "",
                "",
                1,
            },
            new object[] { "lin 0 0 100 0 50 20 0", "30,20;0,10", "0.18,20,99.82,20", "30,20,99.82,20", "", "", 3 },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "0,10;30,20",
                "0,0.0625,0,20.18",
                "0,0.0625,0,10;0,20,0,20.18",
                "",
                "",
                0,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "100,10;30,20",
                "100,0.0625,100,20.18",
                "100,0.0625,100,10;100,20,100,20.18",
                "",
                "",
                1,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "30,19;45,14",
                "0.18,20,99.82,20",
                "0.18,20,30,20;45,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "50,100;60,100",
                "0.18,20,99.82,20",
                "0.18,20,50,20;60,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "50,-30;60,30",
                "0.18,20,99.82,20",
                "0.18,20,50,20;60,20,99.82,20",
                "",
                "",
                3,
            },
            new object[] { "lin 0 0 100 0 50 20 0", "40,20;120,20", "0.18,20,99.82,20", "0.18,20,40,20", "", "", 3 },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "10,20;90,20",
                "0.18,20,99.82,20",
                "0.18,20,10,20;90,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "60,20;10,20",
                "0.18,20,99.82,20",
                "0.18,20,10,20;60,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 0 0 100 0 50 20 0",
                "5,20;60,20",
                "0.18,20,99.82,20",
                "0.18,20,5,20;60,20,99.82,20",
                "",
                "",
                3,
            },
            new object[]
            {
                "lin 1000 1000 1100 1000 1050 1020 0",
                "1000,1010;1000,1030",
                "1000,1000.0625,1000,1020.18",
                "1000,1000.0625,1000,1010",
                "",
                "",
                0,
            },
            new object[] { "lin 1000 1000 1100 1000 1050 1020 0", "1000,1000;1000,1020.2", "", "", "", "", 0 },
            new object[]
            {
                "lin 1000 1000 1100 1000 1050 1020 0",
                "1000,1000.07;1000,1020.2",
                "1000,1000.0625,1000,1020.18",
                "1000,1000.0625,1000,1000.07",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "60,60;84.852297,0.296192",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.003491;0,0,84.852814,0.785398,1.568675",
                3,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "60,60;84.529923,7.39541",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.087266;0,0,84.852814,0.785398,1.568675",
                3,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "60,60;0.296192,84.852297",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.785398;0,0,84.852814,1.567306,1.568675",
                3,
            },
            new object[] { "ang3 0 0 50 0 0 50 60 60", "60,60;0.148096,84.852685", "", "", "", "", 3 },
            new object[] { "ang3 0 0 50 0 0 50 60 60", "84.852685,0.148096;60,60", "", "", "", "", 3 },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "42.426407,42.426407;103.923048,60",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.523599;0,0,84.852814,0.785398,1.568675",
                3,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "55,0;70,0",
                "50.0625,0,85.032814,0",
                "50.0625,0,55,0;70,0,85.032814,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "45,0;70,0",
                "50.0625,0,85.032814,0",
                "70,0,85.032814,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "70,0;95,0",
                "50.0625,0,85.032814,0",
                "50.0625,0,70,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "78.3,32.5;60,60",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.39343;0,0,84.852814,0.785398,1.568675",
                3,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "70,0;75,0",
                "50.0625,0,85.032814,0",
                "50.0625,0,70,0;75,0,85.032814,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "84,12;12,84",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.141897;0,0,84.852814,1.428899,1.568675",
                3,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 60 60",
                "60,60;60,60",
                "",
                "",
                "0,0,84.852814,0.002121,1.568675",
                "0,0,84.852814,0.002121,0.785398;0,0,84.852814,0.785398,1.568675",
                3,
            },
            new object[] { "rad 0 0 30 0 10", "25,0;35,0", "30.18,0,40,0", "35,0,40,0", "", "", 3 },
            new object[] { "rad 0 0 21.2132 21.2132 10", "10,10;15,15", "", "", "", "", 3 },
            new object[] { "dia 30 0 -30 0 10", "10,0;15,0", "", "", "", "", 3 },
            new object[]
            {
                "ang3 0 0 0 50 50 0 60 60",
                "70,0;75,0",
                "50.0625,0,85.032814,0",
                "50.0625,0,70,0;75,0,85.032814,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "ang3 0 0 0 50 50 0 60 60",
                "0,70;0,75",
                "0,50.0625,0,85.032814",
                "0,50.0625,0,70;0,75,0,85.032814",
                "",
                "",
                1,
            },
            new object[]
            {
                "ang3 0 0 50 0 0 50 -60 -60",
                "-60,-60;-70,-50",
                "",
                "",
                "0,0,84.852814,1.572918,6.281064",
                "0,0,84.852814,1.572918,3.761842;0,0,84.852814,3.926991,6.281064",
                3,
            },
            new object[] { "rad 0 0 30 0 10", "35,0;50,0", "30.18,0,40,0", "30.18,0,35,0", "", "", 3 },
            new object[] { "rad 0 0 30 0 10", "45,0;50,0", "", "", "", "", 3 },
            new object[] { "dia 30 0 -30 0 10", "35,0;-35,0", "30.18,0,40,0", "35,0,40,0", "", "", 3 },
        };

        /// <summary>Draws the dimension of a row by its own command, in the dimension style of a blank drawing AutoCAD gives.</summary>
        private static DxfDimension Dimension(CadDocument document, CadCommandSession session, string text)
        {
            string[] t = text.Split(' ');
            double[] n = t.Skip(1).Select(Number).ToArray();

            switch (t[0])
            {
                case "lin":
                    Run(session, "DLI", P(n[0], n[1]), P(n[2], n[3]), P(n[4], n[5]));
                    return Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
                case "ali":
                    Run(session, "DAL", P(n[0], n[1]), P(n[2], n[3]), P(n[4], n[5]));
                    return Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
                case "ang3":
                    Run(session, "DIMANGULAR", Enter, P(n[0], n[1]), P(n[2], n[3]), P(n[4], n[5]), P(n[6], n[7]));
                    return Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
                default:
                {
                    // rad cx cy x y leader / dia x y x2 y2 leader: a circle through the chord point, the words put the leader beyond it.
                    bool radius = t[0] == "rad";
                    var chord = new GeoPoint2(radius ? n[2] : n[0], radius ? n[3] : n[1]);
                    var other = new GeoPoint2(radius ? n[0] : n[2], radius ? n[1] : n[3]);
                    GeoPoint2 centre = radius
                        ? other
                        : new GeoPoint2((chord.X + other.X) / 2.0, (chord.Y + other.Y) / 2.0);
                    double r = (chord - centre).Length;
                    GeoVector2 outward = (chord - centre) / r;
                    document.Database.ModelSpace.AppendEntity(
                        new DxfCircle(new GeoPoint3(centre.X, centre.Y, 0.0), new GeoVector3(0.0, 0.0, 1.0), r)
                    );
                    GeoPoint2 where = chord + outward * n[4];
                    Run(session, radius ? "DIMRADIUS" : "DIMDIAMETER", P(chord.X, chord.Y), P(where.X, where.Y));
                    return Assert.Single(All<DxfDimension>(document.ModelSpace));
                }
            }
        }

        /// <summary>A drawing with the style of the rows and one angular dimension from 0 to 90 degrees on an arc of 84.85, broken by hand from 22.5 to 45 degrees.</summary>
        private static CadDocument BrokenAngular(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimexo = 0.0625;
            standard.Dimexe = 0.18;
            standard.Dimasz = 0.18;
            standard.Dimtxt = 0.0;
            standard.Dimgap = 0.0;
            StandardCurrent(document.Database);
            session = Session(document);
            handle = Dimension(document, session, "ang3 0 0 50 0 0 50 60 60").Handle;
            Run(session, "DIMBREAK", new[] { handle }, "M", P(78.3, 32.5), P(60, 60));
            return document;
        }

        private static List<DxfArc> ArcsOf(CadDocument document, string handle) =>
            ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased)
                .OrderBy(a => a.StartAngle)
                .ToList();

        [Fact]
        public void AnAngularDimensionBrokenByHand_IsBrokenAgainWhereItIsDrawnAnew_AndWholeAgainWhereTheBreakIsRemoved()
        {
            // AutoCAD 2021 keeps the break of an arc as it keeps one of a line (71 2, the line broken 3) and draws the arc in
            // two where the dimension is drawn anew: the points picked at 22.5 and 45 degrees on an arc of 84.85.
            CadDocument document = BrokenAngular(out CadCommandSession session, out string handle);
            Run(session, "DIMEDIT", "N", "<> X", new[] { handle }, Enter);
            List<DxfArc> again = ArcsOf(document, handle);
            Assert.Equal(2, again.Count);
            Assert.Equal(0.39343, again[0].EndAngle, 4);
            Assert.Equal(Math.PI / 4.0, again[1].StartAngle, 4);

            Run(session, "DIMBREAK", new[] { handle }, "R");
            Assert.Single(ArcsOf(document, handle));
            Assert.DoesNotContain("AcDbBreakPointRef", document.Database.ToText());
        }

        [Fact]
        public void ARadiusBrokenByHand_IsNotBrokenAtItsCentreMark_AndTheMarkStaysWhole()
        {
            // AutoCAD 2021, picking from (0, 0) to (35, 0) on a radius of 30 whose line is drawn from 30.18 to 40, breaks the line
            // out to 35 and leaves the centre mark, which lies along the same line, as it was.
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimasz = 0.18;
            standard.Dimtxt = 0.0;
            standard.Dimgap = 0.0;
            standard.Dimcen = 0.09;
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            string handle = Dimension(document, session, "rad 0 0 30 0 10").Handle;

            Run(session, "DIMBREAK", new[] { handle }, "M", P(0, 0), P(35, 0));

            List<double[]> lines = ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased)
                .Select(l => Ordered(new[] { l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y }))
                .ToList();
            Assert.True(Has(lines, new[] { -0.09, 0, 0.09, 0 }, 1E-6));
            Assert.True(Has(lines, new[] { 0.0, -0.09, 0.0, 0.09 }, 1E-6));
            Assert.True(Has(lines, new[] { 35.0, 0.0, 40.0, 0.0 }, 1E-6));
            Assert.False(Has(lines, new[] { 30.18, 0.0, 40.0, 0.0 }, 1E-6));
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void ADimensionBrokenBetweenTwoPoints_HasTheSamePiecesAndTheSameLineAsAutoCad2021(
            string dimension,
            string points,
            string goneLines,
            string newLines,
            string goneArcs,
            string newArcs,
            int line
        )
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimtad = 1;
            standard.Dimexo = 0.0625;
            standard.Dimexe = 0.18;
            standard.Dimasz = 0.18;
            // Words of no height, with no gap round them: AutoCAD's console draws no words, so the lines run whole to the arrows.
            standard.Dimtxt = 0.0;
            standard.Dimgap = 0.0;
            standard.Dimcen = 0.09;
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            string handle = Dimension(document, session, dimension).Handle;
            string[] p = points.Split(';');
            double[] first = Numbers(p[0]);
            double[] second = Numbers(p[1]);

            Run(session, "DIMBREAK", new[] { handle }, "M", P(first[0], first[1]), P(second[0], second[1]));

            var broken = (DxfDimension)document.FindEntity(handle);
            List<double[]> lines = broken
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
                .Select(l => Ordered(new[] { l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y }))
                .ToList();
            List<double[]> arcs = broken
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased)
                .Select(a => new[] { a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle })
                .ToList();
            List<double[]> put = Shapes(newLines).Select(Ordered).ToList();

            foreach (double[] shape in put)
            {
                Assert.True(Has(lines, shape, 1E-3), "a line is missing: " + string.Join(",", shape));
            }

            foreach (double[] shape in Shapes(goneLines).Select(Ordered))
            {
                Assert.False(
                    Has(lines, shape, 1E-3) && !Has(put, shape, 1E-3),
                    "a line is still there: " + string.Join(",", shape)
                );
            }

            foreach (double[] shape in Shapes(newArcs))
            {
                Assert.True(Has(arcs, shape, 1E-3), "an arc is missing: " + string.Join(",", shape));
            }

            foreach (double[] shape in Shapes(goneArcs))
            {
                Assert.False(Has(arcs, shape, 1E-3), "an arc is still there: " + string.Join(",", shape));
            }

            string[] text = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            int at = Array.FindIndex(text, s => s.Trim() == "AcDbBreakPointRef");
            Assert.True(at >= 0, "no break kept");
            int found = -1;

            for (int i = at + 1; i + 1 < text.Length; i += 2)
            {
                if (text[i].Trim() == "91")
                {
                    found = int.Parse(text[i + 1].Trim(), CultureInfo.InvariantCulture);
                    break;
                }
            }

            Assert.Equal(line, found);
        }
    }
}
