using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A viewport clipped by an entity, as AutoCAD 2021's VPCLIP clips it (probe GhVc1): the entity is group 340 of the
    /// viewport with flag 0x10000 of group 90, the viewport is among the entity's reactors, and the frame is the box of the
    /// entity with the view kept in place.
    /// </summary>
    public class DxfViewportClipTests
    {
        /// <summary>A layout of R2018 holding one viewport 100 by 80 about (100, 100) showing 160 high about the origin, at 1:2.</summary>
        private static DxfViewport Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet)
        {
            db = new DxfDatabase("AC1027");
            DxfLayout layout = db.Layouts.Single(l => l.LayoutName == "Layout1");
            layout.Initialize();
            sheet = layout.BlockTableRecord;
            var viewport = new DxfViewport
            {
                CenterPoint = new GeoPoint3(100, 100, 0),
                Width = 100,
                Height = 80,
                ViewHeight = 160,
            };
            sheet.AppendEntity(viewport);
            viewport.On = true;
            return viewport;
        }

        private static DxfPolyline Polygon(params double[] xyb)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < xyb.Length / 3; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(xyb[3 * i], xyb[3 * i + 1]), xyb[3 * i + 2], 0.0, 0.0);
            }

            polyline.Closed = true;
            return polyline;
        }

        private static DxfPolyline LShape() =>
            Polygon(60, 70, 0, 140, 70, 0, 140, 100, 0, 100, 100, 0, 100, 130, 0, 60, 130, 0);

        [Fact]
        public void AViewportClippedToAnLShape_TakesTheBoxOfTheShape_AndKeepsItsView()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            DxfPolyline shape = LShape();
            sheet.AppendEntity(shape);

            viewport.ClipTo(shape);

            // AutoCAD 2021: the L of 80 by 60 about (100, 100) made the viewport 80 by 60 there, 120 high, its flag 0x10000 on.
            Assert.True(viewport.NonRectClipOn);
            Assert.Same(shape, viewport.NonRectClipEntity);
            Assert.Equal(new GeoPoint3(100, 100, 0), viewport.CenterPoint);
            Assert.Equal(80.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.Equal(120.0, viewport.ViewHeight, 9);
            Assert.Equal(0.0, viewport.ViewCenter.X, 9);
            Assert.Equal(0.0, viewport.ViewCenter.Y, 9);
            Assert.Equal(0.5, viewport.CustomScale, 9);
            Assert.Contains(viewport.Handle, shape.GetPersistentReactorIds());
        }

        [Fact]
        public void ARectangleOffCentre_MovesTheViewCentreBySteps_OverTheScale()
        {
            DxfViewport viewport = Viewport(out _, out DxfBlockTableRecord sheet);
            DxfPolyline box = Polygon(70, 70, 0, 150, 70, 0, 150, 140, 0, 70, 140, 0);
            sheet.AppendEntity(box);

            viewport.ClipTo(box);

            // AutoCAD 2021: centre (110, 105), 80 by 70, view centre (20, 10), view height 140, still 1:2.
            Assert.Equal(new GeoPoint3(110, 105, 0), viewport.CenterPoint);
            Assert.Equal(80.0, viewport.Width, 9);
            Assert.Equal(70.0, viewport.Height, 9);
            Assert.Equal(20.0, viewport.ViewCenter.X, 9);
            Assert.Equal(10.0, viewport.ViewCenter.Y, 9);
            Assert.Equal(140.0, viewport.ViewHeight, 9);
            Assert.Equal(0.5, viewport.CustomScale, 9);
        }

        [Fact]
        public void ACircle_AnEllipseAndAPolylineWithBulges_GiveTheirExactBoxes()
        {
            // AutoCAD 2021, each on a viewport 100 by 80 about (100, 100) at 1:2.
            DxfViewport round = Viewport(out _, out DxfBlockTableRecord sheet);
            var circle = new DxfCircle(new GeoPoint3(100, 100, 0), GeoVector3.ZAxis, 30);
            sheet.AppendEntity(circle);
            round.ClipTo(circle);
            Assert.Equal(60.0, round.Width, 9);
            Assert.Equal(60.0, round.Height, 9);
            Assert.Equal(120.0, round.ViewHeight, 9);

            DxfViewport oval = Viewport(out _, out sheet);
            var ellipse = new DxfEllipse(
                new GeoPoint3(100, 100, 0),
                GeoVector3.ZAxis,
                new GeoVector3(35, 0, 0),
                0.6,
                0.0,
                2.0 * Math.PI
            );
            sheet.AppendEntity(ellipse);
            oval.ClipTo(ellipse);
            Assert.Equal(70.0, oval.Width, 9);
            Assert.Equal(42.0, oval.Height, 9);
            Assert.Equal(84.0, oval.ViewHeight, 9);

            // Bulges 0.5 under the bottom edge (60, 70) to (140, 70) and -0.4 inside the right one: the box is 80 by 80, 20
            // below the corner, centred on (100, 90), the view centre 20 below the origin in model units.
            DxfViewport bent = Viewport(out _, out sheet);
            DxfPolyline curved = Polygon(60, 70, 0.5, 140, 70, 0, 140, 130, -0.4, 60, 130, 0);
            sheet.AppendEntity(curved);
            bent.ClipTo(curved);
            Assert.Equal(80.0, bent.Width, 9);
            Assert.Equal(80.0, bent.Height, 9);
            Assert.Equal(90.0, bent.CenterPoint.Y, 9);
            Assert.Equal(-20.0, bent.ViewCenter.Y, 9);
            Assert.Equal(160.0, bent.ViewHeight, 9);
        }

        [Fact]
        public void AnOpenPolyline_ALine_AndAnEntityOfAnotherLayout_AreRefused()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            var open = new DxfPolyline();
            open.AddVertexAt(0, new GeoPoint2(60, 70), 0.0, 0.0, 0.0);
            open.AddVertexAt(1, new GeoPoint2(140, 70), 0.0, 0.0, 0.0);
            open.AddVertexAt(2, new GeoPoint2(140, 130), 0.0, 0.0, 0.0);
            sheet.AppendEntity(open);
            var line = new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(10, 10, 0) };
            sheet.AppendEntity(line);
            DxfPolyline inModel = LShape();
            db.ModelSpace.AppendEntity(inModel);
            string before = db.ToText();

            Assert.Throws<ArgumentNullException>(() => viewport.ClipTo(null));
            Assert.Throws<ArgumentException>(() => viewport.ClipTo(open));
            Assert.Throws<ArgumentException>(() => viewport.ClipTo(line));
            Assert.Throws<ArgumentException>(() => viewport.ClipTo(inModel));
            Assert.Throws<ArgumentException>(() => viewport.ClipTo(viewport));
            Assert.Equal(before, db.ToText());
            Assert.False(viewport.NonRectClipOn);
            Assert.Null(viewport.NonRectClipEntity);
        }

        [Fact]
        public void AViewportClippedAgain_LetsTheFirstBoundaryGo_AndKeepsItAsAnEntity()
        {
            DxfViewport viewport = Viewport(out _, out DxfBlockTableRecord sheet);
            DxfPolyline first = LShape();
            var second = new DxfCircle(new GeoPoint3(100, 100, 0), GeoVector3.ZAxis, 30);
            sheet.AppendEntity(first);
            sheet.AppendEntity(second);

            viewport.ClipTo(first);
            viewport.ClipTo(second);

            // AutoCAD 2021: the first entity stays in the layout, without the viewport among its reactors.
            Assert.Same(second, viewport.NonRectClipEntity);
            Assert.False(first.IsErased);
            Assert.DoesNotContain(viewport.Handle, first.GetPersistentReactorIds());
            Assert.Contains(viewport.Handle, second.GetPersistentReactorIds());
        }

        [Fact]
        public void RemovingTheClip_ErasesTheBoundary_AndKeepsTheFrame()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            DxfPolyline shape = LShape();
            sheet.AppendEntity(shape);
            viewport.ClipTo(shape);

            viewport.RemoveClip();

            // AutoCAD 2021, Delete of VPCLIP: the boundary erased, the viewport a rectangle still 80 by 60 about (100, 100).
            Assert.True(shape.IsErased);
            Assert.False(viewport.NonRectClipOn);
            Assert.Null(viewport.NonRectClipEntity);
            Assert.Equal(80.0, viewport.Width, 9);
            Assert.Equal(60.0, viewport.Height, 9);
            Assert.DoesNotContain(" 90\n98304\n340\n", db.ToText(), StringComparison.Ordinal);
            viewport.RemoveClip();
            Assert.False(viewport.NonRectClipOn);
        }

        [Fact]
        public void AStretchedBoundary_TakesTheFrameWithIt_ThroughFitToClip()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            DxfPolyline box = Polygon(70, 70, 0, 150, 70, 0, 150, 140, 0, 70, 140, 0);
            sheet.AppendEntity(box);
            viewport.ClipTo(box);

            box.SetPointAt(2, new GeoPoint2(170, 150));
            Assert.True(viewport.FitToClip());

            // AutoCAD 2021, the third corner dragged to (170, 150): centre (120, 110), 100 by 80, view centre (40, 20), 160 high.
            Assert.Equal(new GeoPoint3(120, 110, 0), viewport.CenterPoint);
            Assert.Equal(100.0, viewport.Width, 9);
            Assert.Equal(80.0, viewport.Height, 9);
            Assert.Equal(40.0, viewport.ViewCenter.X, 9);
            Assert.Equal(20.0, viewport.ViewCenter.Y, 9);
            Assert.Equal(160.0, viewport.ViewHeight, 9);
            string text = db.ToText();
            Assert.False(viewport.FitToClip());
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AClippedViewport_SurvivesADxfRoundTrip_WithItsBoundaryAndItsReactor()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            DxfPolyline shape = LShape();
            sheet.AppendEntity(shape);
            viewport.ClipTo(shape);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());

            DxfViewport read = ByHandle<DxfViewport>(again, viewport.Handle);
            Assert.True(read.NonRectClipOn);
            DxfEntity boundary = Assert.IsType<DxfPolyline>(read.NonRectClipEntity);
            Assert.Equal(shape.Handle, boundary.Handle);
            Assert.Contains(viewport.Handle, boundary.GetPersistentReactorIds());
            Assert.Equal(db.ToText(), again.ToText());
        }

        [Fact]
        public void AClippedViewport_SurvivesADwgRoundTrip_WithItsBoundary()
        {
            DxfViewport viewport = Viewport(out DxfDatabase db, out DxfBlockTableRecord sheet);
            var shape = new DxfCircle(new GeoPoint3(100, 100, 0), GeoVector3.ZAxis, 30);
            sheet.AppendEntity(shape);
            viewport.ClipTo(shape);

            DxfDatabase again;

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(db, stream);
                stream.Position = 0;
                again = DwgReader.Open(stream);
            }

            DxfViewport read = again
                .Layouts.Single(l => l.LayoutName == "Layout1")
                .GetViewports()
                .Single(v => v.NonRectClipOn);
            DxfCircle boundary = Assert.IsType<DxfCircle>(read.NonRectClipEntity);
            Assert.Equal(30.0, boundary.Radius, 9);
            Assert.Equal(60.0, read.Width, 9);
            Assert.Equal(60.0, read.Height, 9);
            Assert.Contains(read.Handle, boundary.GetPersistentReactorIds());
        }
    }
}
