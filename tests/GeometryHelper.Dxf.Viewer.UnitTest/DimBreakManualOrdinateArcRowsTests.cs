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
    /// DIMBREAK Manual on the ordinate and the arc length dimension against AutoCAD 2021's console run through standard input
    /// (16 November), the rows of picks that lie 20 or more units from the dimension's lines, as the rows of
    /// <see cref="DimBreakManualRowsTests"/> do. AutoCAD numbers the leader of an ordinate 0 (a leader jogged to the side has
    /// three legs, 0 the leg from the point, 1 the jog, 2 the last leg), the extension lines of an arc length dimension 0 at the
    /// first end of the length and 1 at the second, and its arc 3. The line broken is the one nearest the first point, the
    /// stretch between the feet of the two points is left out, and both pieces are kept as the points were picked. A pick within
    /// some 15 units of a line AutoCAD's console moves to its middle or its end before it breaks, which depends on its view
    /// and is not here.
    /// </summary>
    public class DimBreakManualOrdinateArcRowsTests
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

        /// <summary>The rows AutoCAD gave: the dimension, the two picks, the lines taken out, the lines put in their place, the arcs taken out and put, the line broken (91).</summary>
        public static readonly IEnumerable<object[]> Rows = new[]
        {
            // The X ordinate of (20, 30) with its leader to (20, 200): picks off the leader by 40, 20 and 25 units.
            new object[]
            {
                "ordx 20 30 20 200",
                "60,70;60,90",
                "20,30.0625,20,200.09",
                "20,30.0625,20,70;20,90,20,200.09",
                "",
                "",
                0,
            },
            // The Y ordinate of (20, 30) with its leader to (250, 30).
            new object[]
            {
                "ordy 20 30 250 30",
                "70,50;90,50",
                "20.0625,30,250.09,30",
                "20.0625,30,70,30;90,30,250.09,30",
                "",
                "",
                0,
            },
            // The X ordinate jogged 60 across: its legs run from (20, 30.0625) to (20, 199.28), across to (80, 199.64) and up to (80, 200.09).
            new object[]
            {
                "ordx 20 30 80 200",
                "60,100;60,120",
                "20,30.0625,20,199.28",
                "20,30.0625,20,100;20,120,20,199.28",
                "",
                "",
                0,
            },
            new object[]
            {
                "ordx 20 30 80 200",
                "60,150;70,160",
                "20,30.0625,20,199.28",
                "20,30.0625,20,150;20,160,20,199.28",
                "",
                "",
                0,
            },
            new object[] { "ordx 20 30 80 200", "100,199.5;100,199.9", "", "", "", "", 1 },
            // The Y ordinate jogged 60 up: legs (20.0625, 30) to (249.28, 30), up to (249.64, 90) and on to (250.09, 90).
            new object[]
            {
                "ordy 20 30 250 90",
                "100,60;140,60",
                "20.0625,30,249.28,30",
                "20.0625,30,100,30;140,30,249.28,30",
                "",
                "",
                0,
            },
            new object[]
            {
                "ordy 20 30 250 90",
                "100,0;140,0",
                "20.0625,30,249.28,30",
                "20.0625,30,100,30;140,30,249.28,30",
                "",
                "",
                0,
            },
            // The arc of 50 from 0 to 90 degrees dimensioned 250 out: rays along the axes from 50.0625 to 250.18, the arc about the centre.
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "100,25;140,35",
                "50.0625,0,250.18,0",
                "50.0625,0,100,0;140,0,250.18,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "25,100;35,140",
                "0,50.0625,0,250.18",
                "0,50.0625,0,100;0,140,0,250.18",
                "",
                "",
                1,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "140,35;100,25",
                "50.0625,0,250.18,0",
                "50.0625,0,100,0;140,0,250.18,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "60,60;80,80",
                "50.0625,0,250.18,0",
                "50.0625,0,60,0;80,0,250.18,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "100,30;30,100",
                "50.0625,0,250.18,0",
                "100,0,250.18,0",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "200,100;150,200",
                "",
                "",
                "0,0,250,0.00072,1.570076",
                "0,0,250,0.00072,0.4636;0,0,250,0.9273,1.570076",
                3,
            },
            new object[]
            {
                "arc 50 0 90 176.7767,176.7767",
                "250,20;240,100",
                "",
                "",
                "0,0,250,0.00072,1.570076",
                "0,0,250,0.00072,0.0798;0,0,250,0.3948,1.570076",
                3,
            },
            // The arc of 50 from 0 to 60 degrees dimensioned 250 out at 30 degrees: rays along 30 degrees through its ends.
            new object[]
            {
                "arc 50 0 60 216.5064,125",
                "100,0;120,0",
                "50.054127,0.03125,228.077021,102.812816",
                "50.054127,0.03125,87.5,21.651;102.5,30.311,228.077021,102.812816",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 60 216.5064,125",
                "130,10;150,20",
                "50.054127,0.03125,228.077021,102.812816",
                "50.054127,0.03125,114.33,37.141;133.66,48.301,228.077021,102.812816",
                "",
                "",
                0,
            },
            new object[]
            {
                "arc 50 0 60 216.5064,125",
                "130,10;200,20",
                "50.054127,0.03125,228.077021,102.812816",
                "50.054127,0.03125,114.33,37.141;171.16,69.952,228.077021,102.812816",
                "",
                "",
                0,
            },
        };

        /// <summary>The style of a blank drawing AutoCAD gives, the words of no height: its console draws none.</summary>
        private static CadDocument Drawing(out CadCommandSession session, double gap)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimtad = 1;
            standard.Dimexo = 0.0625;
            standard.Dimexe = 0.18;
            standard.Dimasz = 0.18;
            standard.Dimtxt = 0.0;
            standard.Dimgap = gap;
            standard.Dimcen = 0.09;
            StandardCurrent(document.Database);
            session = Session(document);
            return document;
        }

        /// <summary>Draws the dimension of a row by its own command: "ordx x y lx ly", "ordy x y lx ly" or "arc radius from to point" (degrees, an arc about the origin).</summary>
        private static DxfDimension Dimension(CadDocument document, CadCommandSession session, string text)
        {
            string[] t = text.Split(' ');

            if (t[0] == "arc")
            {
                document.Database.ModelSpace.AppendEntity(
                    new DxfArc(
                        new GeoPoint3(0, 0, 0),
                        Number(t[1]),
                        Number(t[2]) * Math.PI / 180.0,
                        Number(t[3]) * Math.PI / 180.0
                    )
                );
                string arc = All<DxfArc>(document.ModelSpace).Single().Handle;
                double[] at = Numbers(t[4]);
                Run(session, "DIMARC", new[] { arc }, P(at[0], at[1]));
                return Assert.Single(All<DxfArcDimension>(document.ModelSpace));
            }

            Run(session, "DOR", P(Number(t[1]), Number(t[2])), P(Number(t[3]), Number(t[4])));
            return Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
        }

        /// <summary>The arcs in order of their start, those that meet one on the other made one: the picture's words of no height split an arc in two at their middle, AutoCAD's console, with no words, draws it whole.</summary>
        private static List<double[]> Joined(List<double[]> arcs)
        {
            var joined = new List<double[]>();

            foreach (double[] arc in arcs)
            {
                double[] last = joined.Count == 0 ? null : joined[joined.Count - 1];

                if (last != null && Math.Abs(last[4] - arc[3]) < 1E-6 && Math.Abs(last[2] - arc[2]) < 1E-6)
                {
                    last[4] = arc[4];
                }
                else
                {
                    joined.Add((double[])arc.Clone());
                }
            }

            return joined;
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void AnOrdinateOrAnArcLengthDimensionBrokenBetweenTwoPoints_HasTheSamePiecesAndTheSameLineAsAutoCad2021(
            string dimension,
            string points,
            string goneLines,
            string newLines,
            string goneArcs,
            string newArcs,
            int line
        )
        {
            CadDocument document = Drawing(
                out CadCommandSession session,
                dimension.StartsWith("ord", StringComparison.Ordinal) ? 0.09 : 0.0
            );
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
            List<double[]> arcs = Joined(
                broken
                    .DimBlock.OfType<DxfArc>()
                    .Where(a => !a.IsErased)
                    .Select(a => new[] { a.Center.X, a.Center.Y, a.Radius, a.StartAngle, a.EndAngle })
                    .OrderBy(a => a[3])
                    .ToList()
            );
            List<double[]> put = Shapes(newLines).Select(Ordered).ToList();

            foreach (double[] shape in put)
            {
                Assert.True(Has(lines, shape, 1E-2), "a line is missing: " + string.Join(",", shape));
            }

            foreach (double[] shape in Shapes(goneLines).Select(Ordered))
            {
                Assert.False(
                    Has(lines, shape, 1E-2) && !Has(put, shape, 1E-2),
                    "a line is still there: " + string.Join(",", shape)
                );
            }

            foreach (double[] shape in Shapes(newArcs))
            {
                Assert.True(
                    Has(arcs, shape, 1E-2),
                    "an arc is missing: "
                        + string.Join(",", shape)
                        + " among "
                        + string.Join(
                            ";",
                            arcs.Select(a =>
                                string.Join(",", a.Select(v => v.ToString("0.####", CultureInfo.InvariantCulture)))
                            )
                        )
                );
            }

            foreach (double[] shape in Shapes(goneArcs))
            {
                Assert.False(Has(arcs, shape, 1E-2), "an arc is still there: " + string.Join(",", shape));
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

        [Fact]
        public void ABreakByHandAtTheLeaderOfAnOrdinate_IsBrokenAgainWhereItIsDrawnAnew_ByTheSameLeg()
        {
            // A leader jogged across 60 broken at its leg 0 and drawn anew (DIMEDIT New) is broken in the same leg again: the legs
            // are numbered by where they stand, not by the order the picture holds them in, which a break changes.
            CadDocument document = Drawing(out CadCommandSession session, 0.09);
            string handle = Dimension(document, session, "ordx 20 30 80 200").Handle;
            Run(session, "DIMBREAK", new[] { handle }, "M", P(60, 100), P(60, 120));
            Run(session, "DIMBREAK", new[] { handle }, "M", P(100, 199.5), P(100, 199.9));
            Run(session, "DIMEDIT", "N", "<> X", new[] { handle }, Enter);

            List<double[]> lines = ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
                .Select(l => Ordered(new[] { l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y }))
                .ToList();
            Assert.True(Has(lines, new[] { 20.0, 30.0625, 20.0, 100.0 }, 1E-2));
            Assert.True(Has(lines, new[] { 20.0, 120.0, 20.0, 199.28 }, 1E-2));
            Assert.True(Has(lines, new[] { 20.0, 199.28, 80.0, 199.64 }, 1E-2));
        }
    }
}
