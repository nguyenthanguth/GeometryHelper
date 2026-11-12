using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A viewport clipped by an entity, copied as AutoCAD 2021's <c>WblockCloneObjects</c> and <c>DeepCloneObjects</c> copy it
    /// (probe GhVp): into another drawing the pair comes along whole, the copy of the viewport naming the copy of the entity
    /// (group 340) and listed among its reactors alone; within one drawing a viewport copied alone shares the entity of its
    /// original, which does not list it, and an entity copied alone is a plain entity with no reactor.
    /// </summary>
    public class DxfViewportCloneTests
    {
        /// <summary>A layout of R2018 with a viewport 100 by 80 about (100, 100) at 1:2, clipped to a circle of radius 30 about (110, 105).</summary>
        private static DxfViewport Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle)
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
            circle = new DxfCircle(new GeoPoint3(110, 105, 0), GeoVector3.ZAxis, 30);
            sheet.AppendEntity(circle);
            viewport.ClipTo(circle);
            return viewport;
        }

        private static DxfBlockTableRecord OtherSheet()
        {
            var other = new DxfDatabase("AC1027");
            DxfLayout layout = other.Layouts.Single(l => l.LayoutName == "Layout1");
            layout.Initialize();
            return layout.BlockTableRecord;
        }

        private static DxfEntity[] Entities(DxfBlockTableRecord sheet) => sheet.Where(e => !e.IsErased).ToArray();

        [Fact]
        public void AViewportAndItsBoundary_CopiedTogetherIntoAnotherDrawing_KeepTheirPair()
        {
            DxfViewport viewport = Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle);
            DxfBlockTableRecord target = OtherSheet();

            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { viewport, circle }, target);

            var madeViewport = Assert.IsType<DxfViewport>(copies[0]);
            var madeCircle = Assert.IsType<DxfCircle>(copies[1]);
            Assert.True(madeViewport.NonRectClipOn);
            Assert.Same(madeCircle, madeViewport.NonRectClipEntity);
            Assert.Equal(new[] { madeViewport.Handle }, madeCircle.GetPersistentReactorIds());
            Assert.Equal(60.0, madeViewport.Width, 9);
            Assert.Equal(20.0, madeViewport.ViewCenter.X, 9);
            Assert.Equal(10.0, madeViewport.ViewCenter.Y, 9);
            Assert.Equal(2, Entities(target).Count(e => e is DxfCircle || (e is DxfViewport v && v.NonRectClipOn)));
            Assert.Same(circle, viewport.NonRectClipEntity);
            Assert.Equal(new[] { viewport.Handle }, circle.GetPersistentReactorIds());
        }

        [Fact]
        public void AViewportCopiedAloneIntoAnotherDrawing_BringsItsBoundary_AfterTheViewport()
        {
            DxfViewport viewport = Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle);
            DxfBlockTableRecord target = OtherSheet();

            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { viewport }, target);

            var madeViewport = Assert.IsType<DxfViewport>(Assert.Single(copies));
            var madeCircle = Assert.IsType<DxfCircle>(madeViewport.NonRectClipEntity);
            Assert.NotSame(circle, madeCircle);
            Assert.Same(target, madeCircle.Owner);
            Assert.Equal(new[] { madeViewport.Handle }, madeCircle.GetPersistentReactorIds());
            Assert.Equal(110.0, madeCircle.Center.X, 9);
            Assert.Equal(30.0, madeCircle.Radius, 9);

            // AutoCAD 2021 numbers the viewport first and its boundary next.
            DxfEntity[] listed = Entities(target)
                .Where(e => e is DxfCircle || (e is DxfViewport v && v.NonRectClipOn))
                .ToArray();
            Assert.Same(madeViewport, listed[0]);
            Assert.Same(madeCircle, listed[1]);
            Assert.Equal(new[] { viewport.Handle }, circle.GetPersistentReactorIds());
        }

        [Fact]
        public void AViewportAndItsBoundary_CopiedTogetherWithinTheDrawing_KeepTheirPair()
        {
            DxfViewport viewport = Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle);

            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { viewport, circle }, sheet);

            var madeViewport = Assert.IsType<DxfViewport>(copies[0]);
            var madeCircle = Assert.IsType<DxfCircle>(copies[1]);
            Assert.Same(madeCircle, madeViewport.NonRectClipEntity);
            Assert.Equal(new[] { madeViewport.Handle }, madeCircle.GetPersistentReactorIds());
            Assert.Same(circle, viewport.NonRectClipEntity);
            Assert.Equal(new[] { viewport.Handle }, circle.GetPersistentReactorIds());
        }

        [Fact]
        public void AViewportCopiedAloneWithinTheDrawing_SharesTheBoundaryOfItsOriginal_WhichDoesNotListIt()
        {
            DxfViewport viewport = Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle);

            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { viewport }, sheet);

            var madeViewport = Assert.IsType<DxfViewport>(Assert.Single(copies));
            Assert.True(madeViewport.NonRectClipOn);
            Assert.Same(circle, madeViewport.NonRectClipEntity);
            Assert.Equal(new[] { viewport.Handle }, circle.GetPersistentReactorIds());
        }

        [Fact]
        public void ABoundaryCopiedAlone_IsAPlainCircle_WithNoReactor()
        {
            Clipped(out DxfDatabase db, out DxfBlockTableRecord sheet, out DxfCircle circle);
            DxfBlockTableRecord target = OtherSheet();

            foreach (DxfBlockTableRecord into in new[] { sheet, target })
            {
                IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new DxfEntity[] { circle }, into);

                var made = Assert.IsType<DxfCircle>(Assert.Single(copies));
                Assert.Empty(made.GetPersistentReactorIds());
            }
        }
    }
}
