using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The work of speed16 in the display: a chain of two vertices (a LINE, the first and the last straight run of a closed
    /// polyline with an arc) made of its four numbers where it was made of an array of them to be dropped, and a closed
    /// polyline with a bulge drawn from its loop with no array of normals asked for. Each is checked against what the
    /// entities drew before, item for item, and held by the bytes allocated, which do not vary with the machine as a time
    /// does.
    /// </summary>
    public class DisplaySpeed23Tests
    {
        private static string Handle(int i) => (0x100 + i).ToString("X", CultureInfo.InvariantCulture);

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static List<string> Described(DisplayPolyline2 item)
        {
            var list = new DisplayList2();
            list.Add(item);
            return DisplayPatchTests.Describe(list);
        }

        #region A chain of two vertices

        [Fact]
        public void AChainOfTwoVerticesMadeOfFourNumbers_IsTheChainMadeOfTheirArray_InEveryTurnOfItsCorners()
        {
            double nan = double.NaN;
            double[][] cases =
            {
                new[] { 0.0, 0.0, 10.0, 5.0 },
                new[] { 10.0, 5.0, 0.0, 0.0 },
                new[] { 0.0, 5.0, 10.0, 0.0 },
                new[] { 10.0, 0.0, 0.0, 5.0 },
                new[] { 0.0, 3.0, 8.0, 3.0 },
                new[] { 8.0, 3.0, 0.0, 3.0 },
                new[] { 2.0, 0.0, 2.0, 9.0 },
                new[] { 2.0, 9.0, 2.0, 0.0 },
                new[] { 4.0, 4.0, 4.0, 4.0 },
                new[] { 0.0, -0.0, -0.0, 0.0 },
                new[] { -0.0, 0.0, 0.0, -0.0 },
                new[] { nan, 0.0, 10.0, 5.0 },
                new[] { 0.0, nan, 10.0, 5.0 },
                new[] { 0.0, 0.0, nan, 5.0 },
                new[] { 0.0, 0.0, 10.0, nan },
                new[] { double.PositiveInfinity, 0.0, 10.0, 5.0 },
                new[] { 1e300, -1e300, -1e300, 1e300 },
                new[] { -1234.5678, 91011.1213, 1415.1617, -1819.2021 },
            };

            foreach (double[] c in cases)
            {
                var made = new DisplayPolyline2(c[0], c[1], c[2], c[3], null, "A1", "0");
                var wanted = new DisplayPolyline2(new[] { c[0], c[1], c[2], c[3] }, false, null, "A1", "0");
                string step = string.Join(", ", c.Select(d => d.ToString("R", CultureInfo.InvariantCulture)));

                Assert.Equal(wanted.Count, made.Count);
                Assert.False(made.Closed);
                Assert.False(made.IsSampled);
                Assert.Equal(Bits(wanted.Bounds.MinX), Bits(made.Bounds.MinX));
                Assert.Equal(Bits(wanted.Bounds.MinY), Bits(made.Bounds.MinY));
                Assert.Equal(Bits(wanted.Bounds.MaxX), Bits(made.Bounds.MaxX));
                Assert.Equal(Bits(wanted.Bounds.MaxY), Bits(made.Bounds.MaxY));
                Assert.Equal(Described(wanted), Described(made));

                for (int v = 0; v < 2; v++)
                {
                    Assert.True(Bits(wanted.VertexX(v)) == Bits(made.VertexX(v)), step + " X " + v);
                    Assert.True(Bits(wanted.VertexY(v)) == Bits(made.VertexY(v)), step + " Y " + v);
                }

                Assert.Equal(Bits(wanted.DistanceTo(3.0, 1.0)), Bits(made.DistanceTo(3.0, 1.0)));
                Assert.Equal(
                    wanted.Touches(new DisplayBox(1.0, 1.0, 2.0, 2.0)),
                    made.Touches(new DisplayBox(1.0, 1.0, 2.0, 2.0))
                );
            }
        }

        [Fact]
        public void ALineAndTheStraightRunsOfAClosedPolylineWithAnArc_AreDrawnItemForItem_AsEachDrawnByABuilderOfItsOwn()
        {
            // Lines up, down, level and upright, a zero one, and a polyline whose arc leaves a run of two vertices on each side.
            var entities = new List<string>();

            for (int i = 0; i < 60; i++)
            {
                double x = i * 30.0;
                entities.Add(Line(x, 0, x + 20, 12, "0", handle: Handle(i * 5)));
                entities.Add(Line(x + 20, 12, x, 0, "0", handle: Handle(i * 5 + 1)));
                entities.Add(Line(x, 5, x + 20, 5, "0", handle: Handle(i * 5 + 2)));
                entities.Add(Line(x, 0, x, 0, "0", handle: Handle(i * 5 + 3)));
                entities.Add(
                    Polyline("0", Handle(i * 5 + 4), true, x, 20, 0.0, x + 20, 20, 0.5, x + 20, 40, 0.0, x, 40, 0.0)
                );
            }

            CadDocument document = CadDocument.From(
                Drawing(string.Empty, string.Empty, entities.ToArray()),
                "chains.dxf"
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            var alone = new DisplayList2();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                var builder = new DisplayBuilder(document.Database, document);

                foreach (DisplayItem2 item in builder.BuildLoose(new[] { entity }))
                {
                    alone.Add(item);
                }
            }

            Assert.True(list.Count > 300, "items " + list.Count);
            Assert.Equal(DisplayPatchTests.Describe(alone), DisplayPatchTests.Describe(list));

            // A line is a chain of two vertices whose box is its two corners; the run of a polyline of 4 vertices that is an arc and
            // a chain is one of three (the straight edges after the arc) and one of two (before it).
            Assert.Contains(list.Items.OfType<DisplayPolyline2>(), p => p.Count == 2 && p.Xy.Count == 4);
            Assert.Contains(list.Items.OfType<DisplayPolyline2>(), p => p.Count == 3);
            Assert.Contains(list.Items.OfType<DisplayPolyline2>(), p =>
                p.Count == 2 && !p.Closed && p.Handle == Handle(4)
            );
        }

        #endregion

        #region A closed polyline with a bulge

        private static CadDocument OneLoop(string extrusion)
        {
            string polyline =
                Polyline("0", "A1", true, 0.0, 0.0, 0.0, 20.0, 0.0, 0.5, 20.0, 20.0, 0.0, 0.0, 20.0, 0.0) + extrusion;
            return CadDocument.From(Drawing(string.Empty, string.Empty, polyline), "loop.dxf");
        }

        [Fact]
        public void ALoopOfArcsOnTheUnderSide_IsDrawnWithItsArcTurnedAsItsNormalSaysIt()
        {
            // The same loop in the plane seen from above and from below (the normal (0, 0, -1)): the second is the first as a
            // mirror shows it, the arc leaving (20, 0) towards (20, 20) with a bulge of 0.5 now the other way round.
            DisplayArc2 above = Assert.Single(ArcsOf(OneLoop(string.Empty)));
            DisplayArc2 below = Assert.Single(ArcsOf(OneLoop("210\n0\n220\n0\n230\n-1\n")));

            // Above: from (20, 0) to (20, 20), half a chord 10 and a rise of 5 to the right of the way: radius 12.5, centre
            // 7.5 inside the chord, at (12.5, 10).
            Assert.Equal(12.5, above.Radius, 9);
            Assert.Equal(12.5, above.Cx, 9);
            Assert.Equal(10.0, above.Cy, 9);
            Assert.True(above.Sweep > 0.0);

            // Below: the file's 20 along x is -20 in the world, and the arc turns the other way.
            Assert.Equal(12.5, below.Radius, 9);
            Assert.Equal(-12.5, below.Cx, 9);
            Assert.Equal(10.0, below.Cy, 9);
            Assert.True(below.Sweep < 0.0);
            Assert.Equal(Math.Abs(above.Sweep), Math.Abs(below.Sweep), 12);
        }

        private static IEnumerable<DisplayArc2> ArcsOf(CadDocument document) =>
            document.GetDisplay(document.ModelSpace).Items.OfType<DisplayArc2>();

        [Fact]
        public void ALoopWhoseNormalsWereAskedForBeforeItIsDrawn_IsDrawnItemForItem_AsOneWhoseNormalsWereNot()
        {
            CadDocument asked = OneLoop(string.Empty);
            CadDocument plain = OneLoop(string.Empty);
            var loop = (GeoPolygonArc3)asked.ModelSpace.Block.Cast<DxfEntity>().Single().GetGeometry().Single();
            _ = loop.Normals;

            Assert.Equal(
                DisplayPatchTests.Describe(plain.GetDisplay(plain.ModelSpace)),
                DisplayPatchTests.Describe(asked.GetDisplay(asked.ModelSpace))
            );
        }

        #endregion

        #region Garbage

        private static long BytesOfAColdDisplay(string entities, int count, out DisplayList2 list)
        {
            CadDocument document = CadDocument.From(
                Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, entities),
                "garbage.dxf"
            );

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            list = document.GetDisplay(document.ModelSpace);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / count;
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandLines_MakesFewerThanFourHundredAndFortyBytesALine()
        {
            // The line read from its record, its chain (80 bytes), the handle, the entity, the run and the item's place: 473 bytes a
            // line under .NET Framework before speed16, which also made the array of its four numbers (56 bytes) for the chain to
            // drop; 417 after.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 300) * 30.0;
                double y = (i / 300) * 30.0;
                text.Append(Line(x, y, x + 20, y + 12, i % 3 == 0 ? "A" : "B", handle: Handle(i)));
            }

            long used = BytesOfAColdDisplay(text.ToString(), count, out DisplayList2 list);

            Assert.Equal(count, list.Items.OfType<DisplayPolyline2>().Count());
            Assert.True(used < 440, "allocated " + used + " bytes a line");
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandClosedPolylinesWithABulge_MakesFewerThanNineHundredAndEightyBytesAPolyline()
        {
            // A closed polyline with an arc is a loop whose arrays are kept (vertices, bulges), a chain of two vertices before its
            // arc and one of three after: 1 060 bytes a polyline under .NET Framework before speed16 and 908 after it, which made the array of
            // normals (112 bytes) and that of the chain of two (56) as well.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 300) * 30.0;
                double y = (i / 300) * 30.0;
                text.Append(
                    Polyline("A", Handle(i), true, x, y, 0.0, x + 20, y, 0.5, x + 20, y + 20, 0.0, x, y + 20, 0.0)
                );
            }

            long used = BytesOfAColdDisplay(text.ToString(), count, out DisplayList2 list);

            Assert.True(list.Count >= 3 * count, "items " + list.Count);
            Assert.True(used < 980, "allocated " + used + " bytes a polyline");
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandTextsInTheStyleStandard_MakesNoStringForTheStyle()
        {
            // The style Standard read as a string of 40 bytes for each text: 728 bytes a text under .NET Framework before speed16, 680 after.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(
                    "0\nTEXT\n5\n"
                        + Handle(i)
                        + "\n8\n"
                        + (i % 3 == 0 ? "A" : "B")
                        + "\n10\n"
                        + Number((i % 300) * 30.0)
                        + "\n20\n"
                        + Number((i / 300) * 30.0)
                        + "\n30\n0\n40\n2.5\n1\nT"
                        + i.ToString(CultureInfo.InvariantCulture)
                        + "\n7\nStandard\n"
                );
            }

            long used = BytesOfAColdDisplay(text.ToString(), count, out DisplayList2 list);

            Assert.Equal(count, list.Items.OfType<DisplayText2>().Count());
            Assert.True(used < 705, "allocated " + used + " bytes a text");
        }

        #endregion
    }
}
