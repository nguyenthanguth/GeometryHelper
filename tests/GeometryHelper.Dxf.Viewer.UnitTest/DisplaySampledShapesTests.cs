using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The arcs and circles of a block placed by an INSERT whose scale factors are not alike, which are drawn as the chains of
    /// points they become: the points of a shape of the block are found once for all the INSERTs that place it, and each is the
    /// point as the shape gives it put through the INSERT's matrix, to the bit what one found for each INSERT gave.
    /// </summary>
    public class DisplaySampledShapesTests
    {
        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        private static CadDocument Plan(params string[] inserts)
        {
            string blocks = Block("ROUND", Circle(5, 4, 3), Arc(0, 0, 6, 20, 200), Line(0, 0, 10, 2));
            return CadDocument.From(Drawing(Layer("Steel", 1), blocks, inserts), "round.dxf");
        }

        private static string Unlike(string handle, double x, double y, double sx, double sy, double degrees) =>
            Entity(
                "INSERT",
                "Steel",
                256,
                handle,
                "2",
                "ROUND",
                "10",
                x,
                "20",
                y,
                "30",
                0.0,
                "41",
                sx,
                "42",
                sy,
                "43",
                1.0,
                "50",
                degrees
            );

        private static List<DisplayPolyline2> Chains(DisplayList2 list, string handle) =>
            list.Items.Where(i => i.Handle == handle).OfType<DisplayPolyline2>().Where(p => p.IsSampled).ToList();

        /// <summary>The points of the chain, X and Y in turn, to the bit.</summary>
        private static string Picture(DisplayPolyline2 chain) => string.Join(" ", chain.Xy.Select(Bits));

        [Fact]
        public void AnInsertOfUnlikeScales_DrawsTheCircleAndTheArcAsChains_OfTheShapesPointsPutThroughItsMatrix()
        {
            CadDocument document = Plan(Unlike("A1", 100, 50, 2.0, 0.5, 30.0));
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            List<DisplayPolyline2> chains = Chains(list, "A1");

            // The circle's 180 points and the arc's: its sweep of 180 degrees in steps of two.
            Assert.Equal(2, chains.Count);
            Assert.Equal(360, chains[0].Xy.Count);
            Assert.True(chains[0].Closed);
            Assert.False(chains[1].Closed);

            // The matrix of the INSERT: the block's X axis scaled by 2 and turned 30 degrees, its Y axis by 0.5.
            double cos = Math.Cos(30.0 * Math.PI / 180.0);
            double sin = Math.Sin(30.0 * Math.PI / 180.0);
            var circle = new GeoCircle3(new GeoPoint3(5, 4, 0), new GeoVector3(0, 0, 1), 3.0);

            for (int i = 0; i < 180; i++)
            {
                GeoPoint3 p = circle.GetPointAtParameter(i / 180.0);
                Assert.Equal(100 + 2.0 * cos * p.X - 0.5 * sin * p.Y, chains[0].Xy[2 * i], 9);
                Assert.Equal(50 + 2.0 * sin * p.X + 0.5 * cos * p.Y, chains[0].Xy[2 * i + 1], 9);
            }
        }

        [Fact]
        public void TwoInsertsPlacedAlike_DrawTheSamePointsToTheBit_TheSecondFromTheFirstsSamples()
        {
            CadDocument document = Plan(
                Unlike("A1", 100, 50, 2.0, 0.5, 30.0),
                Unlike("A2", 100, 50, 2.0, 0.5, 30.0),
                Unlike("A3", -20, 7, 1.0, 3.0, 0.0)
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Equal(Chains(list, "A1").Select(Picture), Chains(list, "A2").Select(Picture));
            Assert.NotEqual(Chains(list, "A1").Select(Picture), Chains(list, "A3").Select(Picture));
            Assert.Equal(2, Chains(list, "A3").Count);
        }

        [Fact]
        public void AnInsertDrawnAgainAfterTheRest_IsDrawnAsItWasTheFirstTime()
        {
            CadDocument document = Plan(
                Unlike("A1", 100, 50, 2.0, 0.5, 30.0),
                Unlike("A2", 300, 90, -1.5, 2.5, 75.0),
                Unlike("A3", 0, 0, 1.0, 1.0, 0.0)
            );
            DisplayList2 whole = document.GetDisplay(document.ModelSpace);
            List<string> first = Chains(whole, "A2").Select(Picture).ToList();
            Assert.Equal(2, first.Count);

            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(7, 3, 0));
            Assert.True(
                document
                    .ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, "A1") }, s =>
                        s.Document.FindEntity("A1").TransformBy(move)
                    )
                    .Succeeded
            );
            DisplayList2 patched = document.GetDisplay(document.ModelSpace);
            Assert.Equal(first, Chains(patched, "A2").Select(Picture));

            document.Regen();
            DisplayList2 again = document.GetDisplay(document.ModelSpace);
            Assert.Equal(first, Chains(again, "A2").Select(Picture));
            Assert.Equal(Chains(patched, "A1").Select(Picture), Chains(again, "A1").Select(Picture));
        }

        [Fact]
        public void ACircleOfTheBlockEditedAfterItWasDrawn_IsDrawnWithItsNewRadius()
        {
            CadDocument document = Plan(Unlike("A1", 100, 50, 2.0, 0.5, 0.0));
            DisplayList2 before = document.GetDisplay(document.ModelSpace);
            DisplayPolyline2 old = Chains(before, "A1")[0];

            DxfCircle circle = document.Database.BlockTable["ROUND"].OfType<DxfCircle>().Single();
            circle.Radius = 6.0;
            Assert.Equal(6.0, circle.Radius);
            Assert.Equal(6.0, Assert.IsType<GeoCircle3>(circle.GetGeometry()[0]).Radius);
            document.Regen();
            DisplayList2 after = document.GetDisplay(document.ModelSpace);
            DisplayPolyline2 now = Chains(after, "A1")[0];

            // The first point is the circle's at its start, through the matrix (X doubled, Y halved, nothing turned).
            GeoPoint3 was = new GeoCircle3(new GeoPoint3(5, 4, 0), new GeoVector3(0, 0, 1), 3.0).GetPointAtParameter(
                0.0
            );
            GeoPoint3 is6 = new GeoCircle3(new GeoPoint3(5, 4, 0), new GeoVector3(0, 0, 1), 6.0).GetPointAtParameter(
                0.0
            );
            Assert.Equal(100 + 2.0 * was.X, old.Xy[0], 9);
            Assert.Equal(50 + 0.5 * was.Y, old.Xy[1], 9);
            Assert.Equal(100 + 2.0 * is6.X, now.Xy[0], 9);
            Assert.Equal(50 + 0.5 * is6.Y, now.Xy[1], 9);
            Assert.NotEqual(Picture(old), Picture(now));
        }

        [Fact]
        public void ABlockOfTwoCirclesAndTwoArcs_DrawsEachFromItsOwnSamples_TheSecondInsertToo()
        {
            string blocks = Block(
                "PAIR",
                Circle(5, 4, 3),
                Circle(-2, 1, 5),
                Arc(0, 0, 6, 20, 200),
                Arc(1, 1, 2, 100, 260)
            );
            string insert(string handle, double x) =>
                Entity(
                    "INSERT",
                    "Steel",
                    256,
                    handle,
                    "2",
                    "PAIR",
                    "10",
                    x,
                    "20",
                    0.0,
                    "30",
                    0.0,
                    "41",
                    2.0,
                    "42",
                    0.5,
                    "43",
                    1.0,
                    "50",
                    30.0
                );
            CadDocument document = CadDocument.From(
                Drawing(Layer("Steel", 1), blocks, insert("A1", 100), insert("A2", 100)),
                "pair.dxf"
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            double cos = Math.Cos(30.0 * Math.PI / 180.0);
            double sin = Math.Sin(30.0 * Math.PI / 180.0);
            List<object> shapes = document
                .Database.BlockTable["PAIR"]
                .SelectMany(e => e.GetGeometry())
                .Where(s => s is GeoCircle3 || s is GeoArc3)
                .ToList();

            foreach (string handle in new[] { "A1", "A2" })
            {
                List<DisplayPolyline2> chains = Chains(list, handle);
                Assert.Equal(4, chains.Count);

                for (int k = 0; k < 4; k++)
                {
                    int count = chains[k].Xy.Count / 2;

                    for (int i = 0; i < count; i++)
                    {
                        GeoPoint3 p = shapes[k] is GeoCircle3 circle
                            ? circle.GetPointAtParameter(i / 180.0)
                            : ((GeoArc3)shapes[k]).GetPointAtParameter((double)i / (count - 1));
                        Assert.Equal(100 + 2.0 * cos * p.X - 0.5 * sin * p.Y, chains[k].Xy[2 * i], 9);
                        Assert.Equal(2.0 * sin * p.X + 0.5 * cos * p.Y, chains[k].Xy[2 * i + 1], 9);
                    }
                }
            }
        }

        [Fact]
        public void ACircleOfASpaceUnderAMatrixThatKeepsItACircle_IsStillAnArc()
        {
            CadDocument document = Plan(
                Entity(
                    "INSERT",
                    "Steel",
                    256,
                    "A1",
                    "2",
                    "ROUND",
                    "10",
                    5.0,
                    "20",
                    5.0,
                    "30",
                    0.0,
                    "41",
                    2.0,
                    "42",
                    2.0,
                    "43",
                    2.0,
                    "50",
                    15.0
                )
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.Empty(Chains(list, "A1"));
            Assert.Contains(list.Items, i => i is DisplayArc2 && i.Handle == "A1");
        }
    }
}
