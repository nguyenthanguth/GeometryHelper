using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// The line without ends in the viewer (XL-6): its kinds in a scene, its drawing through the box of the scene with a dot at its
    /// origin, its numbers, the snap to the nearest point of it and the pick of it, and the XLINE of a DXF file.
    /// </summary>
    public class XLineTests
    {
        private static GeoAabb3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 20, 10));

        private static readonly double Diagonal = Math.Sqrt(30.0 * 30.0 + 20.0 * 20.0 + 10.0 * 10.0);

        /// <summary>A twentieth of the diagonal of the scene, the margin the line is drawn past the box by.</summary>
        private static double Margin => Diagonal * 0.05;

        private static Scene Read(DxfWriter writer) => SceneImport.ReadText(writer.ToString(), "dxf");

        [Fact]
        public void TheKindsAreKnownAndAWriterTakesBoth()
        {
            Assert.True(SceneKinds.IsKnown("GeoXLine2"));
            Assert.True(SceneKinds.IsKnown("GeoXLine3"));

            Scene scene = new SceneWriter()
                .Add(new GeoXLine2(new GeoPoint2(1, 2), new GeoVector2(0, 3)), name: "flat")
                .Add(new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 4)), name: "solid")
                .Scene;

            Assert.Equal(new[] { "GeoXLine2", "GeoXLine3" }, scene.Items.Select(i => i.Kind).ToArray());
            Assert.Equal(new[] { 1.0, 2.0, 0.0, 1.0 }, Imp.Of(scene.Items[0]));
            Assert.Equal(new[] { 1.0, 2.0, 3.0, 0.0, 0.0, 1.0 }, Imp.Of(scene.Items[1]));
        }

        [Fact]
        public void ALineSurvivesTheJsonOfAScene_AndComesBackAsTheSameLine()
        {
            var flat = new GeoXLine2(new GeoPoint2(10.25, -3.5), new GeoVector2(3, 4));
            var solid = new GeoXLine3(new GeoPoint3(10.25, -3.5, 7.125), new GeoVector3(1, 2, 2));

            Scene read = SceneReader.Read(new SceneWriter().Add(flat).Add(solid).ToJson());

            Assert.Equal(flat, (GeoXLine2)read.Items[0].ToGeometry());
            Assert.Equal(solid, (GeoXLine3)read.Items[1].ToGeometry());
        }

        [Fact]
        public void ALineInASceneWithABox_IsDrawnThroughTheBoxAndAMarginPastIt_WithADotAtItsOrigin()
        {
            Scene scene = new SceneWriter()
                .Add(Box())
                .Add(new GeoXLine3(new GeoPoint3(15, 10, 5), GeoVector3.XAxis))
                .Scene;

            Primitives primitives = Primitives.Build(scene);
            ItemPrimitives line = primitives.Items[1];

            Assert.Equal(new Vec3(15, 10, 5), Assert.Single(line.Points));
            DrawPolyline drawn = Assert.Single(line.Polylines);
            Assert.False(drawn.Closed);
            Assert.Equal(2, drawn.Points.Length);
            Assert.Equal(-Margin, drawn.Points[0].X, 9);
            Assert.Equal(30.0 + Margin, drawn.Points[1].X, 9);
            Assert.All(drawn.Points, p => Assert.Equal(10.0, p.Y));
            Assert.All(drawn.Points, p => Assert.Equal(5.0, p.Z));
        }

        [Fact]
        public void ASlantedLine_IsCutWhereItLeavesTheGrownBox_AndNoFurther()
        {
            Scene scene = new SceneWriter()
                .Add(Box())
                .Add(new GeoXLine3(new GeoPoint3(15, 10, 5), new GeoVector3(1, 1, 1)))
                .Scene;

            DrawPolyline drawn = Assert.Single(Primitives.Build(scene).Items[1].Polylines);

            // Of the three half-sizes of the box, 5 along Z is the least, so Z is where the line leaves the box grown by the margin.
            Assert.Equal(-Margin, Math.Min(drawn.Points[0].Z, drawn.Points[1].Z), 9);
            Assert.Equal(10.0 + Margin, Math.Max(drawn.Points[0].Z, drawn.Points[1].Z), 9);

            foreach (Vec3 p in drawn.Points)
            {
                Assert.InRange(p.X, -Margin - 1E-9, 30.0 + Margin + 1E-9);
                Assert.InRange(p.Y, -Margin - 1E-9, 20.0 + Margin + 1E-9);
                Assert.Equal(p.X - 15.0, p.Y - 10.0, 9);
                Assert.Equal(p.X - 15.0, p.Z - 5.0, 9);
            }
        }

        [Fact]
        public void ALineInThePlane_IsDrawnAtZeroHeight_AndTheBoundsOfTheSceneAreTheBoxStill()
        {
            Scene scene = new SceneWriter()
                .Add(Box())
                .Add(new GeoXLine2(new GeoPoint2(15, 10), new GeoVector2(0, 1)))
                .Scene;

            Primitives primitives = Primitives.Build(scene);
            DrawPolyline drawn = Assert.Single(primitives.Items[1].Polylines);

            Assert.All(drawn.Points, p => Assert.Equal(0.0, p.Z));
            Assert.All(drawn.Points, p => Assert.Equal(15.0, p.X));
            Assert.Equal(-Margin, Math.Min(drawn.Points[0].Y, drawn.Points[1].Y), 9);
            Assert.Equal(20.0 + Margin, Math.Max(drawn.Points[0].Y, drawn.Points[1].Y), 9);

            // It counts for its origin only: the box of the scene is the box.
            Assert.Equal(new Vec3(0, 0, 0), primitives.Bounds.Min);
            Assert.Equal(new Vec3(30, 20, 10), primitives.Bounds.Max);
        }

        [Fact]
        public void ALineAddedToNothingElse_IsDrawnAMillimetreEitherSideOfItsOrigin()
        {
            Scene scene = new SceneWriter().Add(new GeoXLine2(new GeoPoint2(3, 4), new GeoVector2(0, 1))).Scene;

            Primitives primitives = Primitives.Build(scene);
            DrawPolyline drawn = Assert.Single(primitives.Items[0].Polylines);

            Assert.Equal(new Vec3(3, 3, 0), drawn.Points[0]);
            Assert.Equal(new Vec3(3, 5, 0), drawn.Points[1]);
            Assert.Equal(new Vec3(3, 4, 0), primitives.Bounds.Min);
            Assert.Equal(new Vec3(3, 4, 0), primitives.Bounds.Max);
        }

        [Fact]
        public void AnOriginFarFromTheRest_StretchesTheBoxToItself_AndTheLineIsDrawnAcrossAll()
        {
            Scene scene = new SceneWriter()
                .Add(Box())
                .Add(new GeoXLine3(new GeoPoint3(5000, 0, 0), GeoVector3.XAxis))
                .Scene;

            Primitives primitives = Primitives.Build(scene);
            DrawPolyline drawn = Assert.Single(primitives.Items[1].Polylines);
            double size = new Vec3(5000, 20, 10).Length;

            Assert.Equal(5000.0, primitives.Bounds.Max.X);
            Assert.Equal(-size * 0.05, drawn.Points[0].X, 9);
            Assert.Equal(5000.0 + size * 0.05, drawn.Points[1].X, 9);
        }

        [Fact]
        public void TheDirectionIsKeptWhichWayTheLineWasMade_WhateverTheEndsAreCalled()
        {
            Scene scene = new SceneWriter()
                .Add(Box())
                .Add(new GeoXLine3(new GeoPoint3(15, 10, 5), new GeoVector3(-1, 0, 0)))
                .Scene;

            DrawPolyline drawn = Assert.Single(Primitives.Build(scene).Items[1].Polylines);

            Assert.Equal(Math.Round(-Margin, 6), Math.Round(Math.Min(drawn.Points[0].X, drawn.Points[1].X), 6));
            Assert.Equal(Math.Round(30.0 + Margin, 6), Math.Round(Math.Max(drawn.Points[0].X, drawn.Points[1].X), 6));
        }

        [Fact]
        public void TheNumbersOfALine_AreItsOriginAndItsUnitDirection()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoXLine2(new GeoPoint2(1.5, 2), new GeoVector2(0, 7)))
                .Add(new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 9)))
                .Scene;
            Primitives primitives = Primitives.Build(scene);

            var flat = ItemMeasures.Describe(scene.Items[0], scene, primitives.Items[0]).ToDictionary(p => p.Key, p => p.Value);
            var solid = ItemMeasures.Describe(scene.Items[1], scene, primitives.Items[1]).ToDictionary(p => p.Key, p => p.Value);

            Assert.Equal("1.5, 2", flat["Origin"]);
            Assert.Equal("0, 1", flat["Direction"]);
            Assert.Equal("1, 2, 3", solid["Origin"]);
            Assert.Equal("0, 0, 1", solid["Direction"]);
        }

        [Fact]
        public void TheNearestPointOfALine_IsOnTheLineToTheLastDigit_FarFromItsOriginToo()
        {
            // A line along X through the origin; the cursor is two pixels off it, far along the line from the origin.
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(-100, -50), new GeoPoint2(100, 50)))
                .Add(new GeoXLine2(new GeoPoint2(0, 0), new GeoVector2(1, 0))));

            (double x, double y) = view.Pixel(new Vec3(70, 0, 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x, y + 2.0, SnapKinds.Nearest, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal("nearest", hit.Name);
            Assert.Equal(1, hit.ItemIndex);
            Assert.Equal(0.0, hit.Point.Y);
            Assert.InRange(hit.Point.X, 65.0, 75.0);
        }

        [Fact]
        public void TheOriginOfALine_SnapsAsAPoint()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(-100, -50), new GeoPoint2(100, 50)))
                .Add(new GeoXLine2(new GeoPoint2(10, 20), new GeoVector2(1, 0))));

            (double x, double y) = view.Pixel(new Vec3(10, 20, 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 1.5, y - 1.0, SnapKinds.End, SnapEngine.DefaultRadius, view.Scene.EqualPoint, null);

            Assert.Equal(new Vec3(10, 20, 0), hit.Point);
            Assert.Equal(1, hit.ItemIndex);
        }

        [Fact]
        public void ALineIsPickedOnItsDrawnStretch_WithinAFewPixels_AndNotFurther()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(-100, -50), new GeoPoint2(100, 50)))
                .Add(new GeoXLine2(new GeoPoint2(0, 0), new GeoVector2(1, 0))));

            (double x, double y) = view.Pixel(new Vec3(-60, 0, 0));
            PickHit near = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y + 3.0, null, true);
            PickHit far = PickEngine.Pick(view.Primitives, view.Geometry, view.Map, x, y + 60.0, null, true);

            Assert.Equal(1, near.ItemIndex);
            Assert.Equal(PickKind.Curve, near.Kind);
            Assert.True(far == null || far.ItemIndex != 1);
        }

        [Fact]
        public void AnXLineOfADxfFile_IsALineInSpace_ToTheBit_AndIsNotLeftOutWithAWarning()
        {
            var flat = (GeoXLine2)SampleShapes.Make("GeoXLine2", 3);
            var solid = (GeoXLine3)SampleShapes.Make("GeoXLine3", 3);

            Scene a = Read(new DxfWriter().Add(flat, "lines"));
            Scene b = Read(new DxfWriter().Add(solid, "lines"));

            Assert.Equal("GeoXLine3", Assert.Single(a.Items).Kind);
            Assert.Equal("GeoXLine3", Assert.Single(b.Items).Kind);
            Assert.Equal("lines", a.Items[0].Layer);
            Imp.SameBits(new[] { flat.Origin.X, flat.Origin.Y, 0.0, flat.Direction.X, flat.Direction.Y, 0.0 }, Imp.Of(a.Items[0]));
            Imp.SameBits(Imp.Of(solid), Imp.Of(b.Items[0]));
            Assert.Empty(a.LoadWarnings);
            Assert.Empty(b.LoadWarnings);
        }

        [Fact]
        public void AnXLineBesideARay_EachComesBackAsItsOwnKind()
        {
            Scene scene = Read(new DxfWriter()
                .Add(new GeoRay3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis))
                .Add(new GeoXLine3(new GeoPoint3(0, 0, 0), GeoVector3.YAxis)));

            Assert.Equal(new[] { "GeoRay3", "GeoXLine3" }, scene.Items.Select(i => i.Kind).ToArray());
        }
    }
}
