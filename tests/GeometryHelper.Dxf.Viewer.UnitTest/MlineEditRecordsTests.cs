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
    /// MLEDIT's results against the records AutoCAD 2021 left (accoreconsole, <c>Wblock</c> of the multilines into a side
    /// database written by <c>DxfOut</c>): a style of three elements at 0.5, 0 and -0.5, scale 1, justified at the top, so that
    /// the elements stand 0, -0.5 and -1 along the miter; the numbers are those AutoCAD wrote.
    /// </summary>
    public class MlineEditRecordsTests
    {
        private const double Close = 1E-9;

        /// <summary>A drawing whose current style has three elements, with the multilines given each as MLINE draws them (the first point, the others, J for the zero or bottom first).</summary>
        private static CadDocument Drawing(params object[][] lines)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "MLSTYLE", Enter);
            var settings = new DxfMlineStyleSettings();
            settings.Elements.Add(new DxfMlineStyleElement(0.0, DxfColor.ByLayer, "BYLAYER"));
            document.Database.AddMlineStyle("GH3", settings);
            Run(session, "CMLSTYLE", "GH3", "CMLSCALE", "1");

            foreach (object[] line in lines)
            {
                Run(session, "MLINE");
                Run(session, line);
                Run(session, Enter);
            }

            return document;
        }

        private static List<DxfUnknownEntity> Mlines(CadDocument document) =>
            All<DxfUnknownEntity>(document.ModelSpace).Where(m => m.DxfName == "MLINE").ToList();

        /// <summary>The parameters after the offset that an element holds at a vertex, as the record writes them.</summary>
        private static double[] Tail(CadDocument document, int mline, int vertex, int element)
        {
            int at = -1;
            int each = -1;
            var found = new List<double>();

            foreach (DxfTypedValue group in Mlines(document)[mline].Groups)
            {
                if (group.TypeCode == 11)
                {
                    at++;
                    each = -1;
                }
                else if (group.TypeCode == 74)
                {
                    each++;
                }
                else if (group.TypeCode == 41 && at == vertex && each == element)
                {
                    found.Add(double.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
                }
            }

            return found.Skip(1).ToArray();
        }

        private static void Tails(CadDocument document, int mline, int vertex, params double[][] expected)
        {
            for (int e = 0; e < expected.Length; e++)
            {
                double[] actual = Tail(document, mline, vertex, e);
                Assert.True(
                    expected[e].Length == actual.Length
                        && expected[e].Zip(actual, (a, b) => Math.Abs(a - b) < Close).All(same => same),
                    "mline "
                        + mline
                        + " vertex "
                        + vertex
                        + " element "
                        + e
                        + ": AutoCAD wrote ["
                        + string.Join(", ", expected[e])
                        + "], ours is ["
                        + string.Join(", ", actual)
                        + "]"
                );
            }
        }

        private static object[] Line(params double[] xy)
        {
            var points = new List<object>();

            for (int i = 0; i < xy.Length; i += 2)
            {
                points.Add(P(xy[i], xy[i + 1]));
            }

            return points.ToArray();
        }

        private static CadDocument Edit(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, inputs);
            Run(session, Enter);
            return document;
        }

        private static readonly double[] Whole = { 0.0 };
        private static readonly double[] None = new double[0];

        [Fact]
        public void ClosedCross_BreaksAllThreeElementsOfTheFirst()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, 50)),
                "MLEDIT",
                "CC",
                P(20, 0),
                P(50, -30)
            );

            Tails(document, 0, 0, new[] { 0.0, 50.0, 51.0 }, new[] { 0.0, 50.0, 51.0 }, new[] { 0.0, 50.0, 51.0 });
            Tails(document, 1, 0, Whole, Whole, Whole);
        }

        [Fact]
        public void OpenCross_BreaksAllOfTheFirstAndTheOuterElementsOfTheSecond()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, 50)),
                "MLEDIT",
                "OC",
                P(20, 0),
                P(50, -30)
            );

            Tails(document, 0, 0, new[] { 0.0, 50.0, 51.0 }, new[] { 0.0, 50.0, 51.0 }, new[] { 0.0, 50.0, 51.0 });
            Tails(document, 1, 0, new[] { 0.0, 49.0, 50.0 }, Whole, new[] { 0.0, 49.0, 50.0 });
        }

        [Fact]
        public void MergedCross_BreaksOnlyTheOuterElementsOfBoth()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, 50)),
                "MLEDIT",
                "MC",
                P(20, 0),
                P(50, -30)
            );

            Tails(document, 0, 0, new[] { 0.0, 50.0, 51.0 }, Whole, new[] { 0.0, 50.0, 51.0 });
            Tails(document, 1, 0, new[] { 0.0, 49.0, 50.0 }, Whole, new[] { 0.0, 49.0, 50.0 });
        }

        [Fact]
        public void ClosedTee_StopsEveryElementAtTheNearEdge_AndOpenTeeOpensTheNearElementOfTheSecond()
        {
            CadDocument closed = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, -10)),
                "MLEDIT",
                "CT",
                P(50, -30),
                P(20, 0)
            );

            Tails(closed, 1, 0, new[] { 0.0, 49.0 }, new[] { 0.0, 49.0 }, new[] { 0.0, 49.0 });
            Tails(closed, 1, 1, None, None, None);
            Tails(closed, 0, 0, Whole, Whole, Whole);

            CadDocument open = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, -10)),
                "MLEDIT",
                "OT",
                P(50, -30),
                P(20, 0)
            );

            Tails(open, 1, 0, new[] { 0.0, 49.0 }, new[] { 0.0, 49.0 }, new[] { 0.0, 49.0 });
            Tails(open, 0, 0, Whole, Whole, new[] { 0.0, 50.0, 51.0 });
        }

        [Fact]
        public void MergedTee_RunsTheInnerElementOnToTheSecondsInnerElement()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -50, 50, -10)),
                "MLEDIT",
                "MT",
                P(50, -30),
                P(20, 0)
            );

            Tails(document, 1, 0, new[] { 0.0, 49.0 }, new[] { 0.0, 49.5 }, new[] { 0.0, 49.0 });
            Tails(document, 0, 0, Whole, Whole, new[] { 0.0, 50.0, 51.0 });
        }

        [Fact]
        public void TeeAtTheStartOfTheFirst_StartsItsElementsAtTheNearEdge_ClosedAndMerged()
        {
            CadDocument closed = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -10, 50, -50)),
                "MLEDIT",
                "CT",
                P(50, -30),
                P(20, 0)
            );

            Tails(closed, 1, 0, new[] { 1.0 }, new[] { 1.0 }, new[] { 1.0 });

            CadDocument merged = Edit(
                Drawing(Line(0, 0, 100, 0), Line(50, -10, 50, -50)),
                "MLEDIT",
                "MT",
                P(50, -30),
                P(20, 0)
            );

            Tails(merged, 1, 0, new[] { 1.0 }, new[] { 0.5 }, new[] { 1.0 });
            Tails(merged, 0, 0, Whole, Whole, new[] { 0.0, 49.0, 50.0 });
        }

        [Fact]
        public void CornerJoint_AtRightAngles_MitresTheThreeElementsAsAutoCADDoes()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 80, 0), Line(100, -50, 100, 50)),
                "MLEDIT",
                "CJ",
                P(20, 0),
                P(100, -30)
            );

            Tails(document, 0, 0, Whole, Whole, Whole);
            Tails(document, 0, 1, new[] { 0.0, 1.0 }, new[] { 0.0, 0.5 }, None);
            Tails(document, 1, 0, new[] { 0.0, 49.0 }, new[] { 0.0, 49.5 }, Whole);
            Tails(document, 1, 1, None, None, None);
        }

        [Fact]
        public void CornerJoint_AtSixtyDegrees_StopsAndRunsOnByTheDistancesAutoCADWrote()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 80, 0), Line(50, -86.60254037844386, 150, 86.60254037844386)),
                "MLEDIT",
                "CJ",
                P(20, 0),
                P(60, -69.28203229)
            );

            Tails(document, 0, 0, Whole, Whole, new[] { 0.0, 99.42264973081037 });
            Tails(document, 0, 1, new[] { 0.0, 1.154700538379259 }, new[] { 0.0, 0.2886751345948113 }, None);
            Tails(document, 1, 0, new[] { 0.0, 98.84529946162075 }, new[] { 0.0, 99.71132486540518 }, Whole);
            Tails(document, 1, 1, None, None, new[] { 0.0, 0.5773502691896253 });
        }

        [Fact]
        public void CornerJoint_WhereAnElementRunsBackPastTheStartOfTheSecond_StartsBeforeItsVertex()
        {
            // AutoCAD 2021: the second drawn downward at the bottom, so that it stands on the other side of its line: its start
            // is moved to the corner and its elements start -0.5774, 0.2887 and 1.1547 along it.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "MLSTYLE", Enter);
            var settings = new DxfMlineStyleSettings();
            settings.Elements.Add(new DxfMlineStyleElement(0.0, DxfColor.ByLayer, "BYLAYER"));
            document.Database.AddMlineStyle("GH3", settings);
            Run(session, "CMLSTYLE", "GH3", "CMLSCALE", "1");
            Run(session, "MLINE", P(0, 0), P(80, 0), Enter);
            Run(session, "MLINE", "J", "B", P(150, 86.60254037844386), P(50, -86.60254037844386), Enter);

            Run(session, "MLEDIT", "CJ", P(20, 0), P(60, -69.28203229), Enter);

            Tails(
                document,
                1,
                0,
                new[] { -0.5773502691896253 },
                new[] { 0.2886751345948158 },
                new[] { 1.154700538379249 }
            );
            Tails(document, 0, 1, new[] { 0.0, 1.154700538379259 }, new[] { 0.0, 0.2886751345947971 }, None);
            Assert.Equal(
                new[] { 100.0, 0.0 },
                new[]
                {
                    new DxfMlineBuilder(Mlines(document)[1]).Points[0].X,
                    new DxfMlineBuilder(Mlines(document)[1]).Points[0].Y,
                }.Select(v => Math.Round(v, 9))
            );
        }

        [Fact]
        public void ACornerJoint_IsDrawnWhereTheElementsRunOnPastTheEnd()
        {
            CadDocument document = Edit(
                Drawing(Line(0, 0, 80, 0), Line(100, -50, 100, 50)),
                "MLEDIT",
                "CJ",
                P(20, 0),
                P(100, -30)
            );

            DxfLine[] lines = Mlines(document)[0].Explode().Cast<DxfLine>().ToArray();

            // The first element, along y = 0, runs on to x = 101 where the second's far element stands; the third stops at x = 100.
            Assert.Contains(lines, l => Math.Abs(l.EndPoint.X - 101.0) < Close && Math.Abs(l.EndPoint.Y) < Close);
            Assert.Contains(lines, l => Math.Abs(l.EndPoint.X - 100.0) < Close && Math.Abs(l.EndPoint.Y + 1.0) < Close);
        }
    }
}
