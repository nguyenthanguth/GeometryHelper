using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    /// <summary>
    /// Bodies, views and checks for the dimension tests, built in code; and a PNG of a view with its dimensions to look at.
    /// </summary>
    internal static class AnnotationKit
    {
        /// <summary>A part made in code: its id, its body, the world's frame as its own unless one is given.</summary>
        public static DrawingObject Part(
            string id,
            GeoSolid3 body,
            bool isMain = false,
            GeoCoordinateSystem3? frame = null
        ) => new DrawingObject(id, DrawingObjectKind.Part, body, frame ?? GeoCoordinateSystem3.Global, isMain: isMain);

        /// <summary>
        /// A flat bar lying in the world's XY from the origin, its holes of 22 through it, faceted into 24 sides: a beam's web
        /// seen from above as a draughtsman dimensions it.
        /// </summary>
        public static DrawingObject Bar(double length, double width, params GeoPoint2[] holes) =>
            Part("bar", PlateWithHoles(length, width, 20, 22, 24, holes), true);

        /// <summary>The objects of a test body.</summary>
        public static DrawingObject[] Objects(TestBody body) => body.Parts.Select(p => p.Object).ToArray();

        /// <summary>The front, top and right views of a drawing frame, as the drawing makes them.</summary>
        public static (string Name, GeoCoordinateSystem3 Frame)[] MainViews(GeoCoordinateSystem3 drawingFrame) =>
            new[]
            {
                ("front", StandardViews.GetFrame(drawingFrame, ViewKind.Front).CoordinateSystem),
                ("top", StandardViews.GetFrame(drawingFrame, ViewKind.Top).CoordinateSystem),
                ("right", StandardViews.GetFrame(drawingFrame, ViewKind.Right).CoordinateSystem),
            };

        public static ProjectedView2 Project(IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame) =>
            ViewProjector.Project(objects.Select(o => new ProjectionInput(o.Id, o.Solid)), frame, Tol);

        public static IReadOnlyList<DrawingDimension> Dimension(
            IReadOnlyList<DrawingObject> objects,
            GeoCoordinateSystem3 frame,
            DimensionSettings settings = null
        ) => Annotator.Dimension(Project(objects, frame), objects, frame, settings ?? new DimensionSettings(), Tol);

        /// <summary>The texts of the dimensions of a kind along one of the view's axes, in their order along it.</summary>
        public static string[] Texts(IEnumerable<DrawingDimension> dimensions, DimensionKind kind, bool alongX) =>
            dimensions
                .Where(d => d.Kind == kind && (d.Direction.X > 0.5) == alongX)
                .OrderBy(d => alongX ? d.Points[0].ViewPoint.X : d.Points[0].ViewPoint.Y)
                .ThenBy(d => alongX ? d.Points[1].ViewPoint.X : d.Points[1].ViewPoint.Y)
                .Select(d => d.Text)
                .ToArray();

        /// <summary>Asserts that a number is another to the point tolerance.</summary>
        public static void Near(double expected, double actual) =>
            Assert.True(Math.Abs(expected - actual) <= Tol.EqualPoint, $"Expected {expected}, got {actual}.");

        /// <summary>
        /// Writes a view and its dimensions into a new drawing at 1:1 and draws the drawing, as the presenter shows it, into a
        /// PNG in the folder annot of the run (<see cref="TestRun"/>); gives the path. The DXF is saved beside it.
        /// </summary>
        public static string SavePicture(ProjectedView2 view, IEnumerable<DrawingDimension> dimensions, string name)
        {
            var database = new DxfDatabase("AC1027");
            view.WriteTo(database);
            var placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), 1.0);

            foreach (DrawingDimension dimension in dimensions)
            {
                dimension.WriteTo(database, placement);
            }

            CadDocument document = CadDocument.From(database, name + ".dxf");
            DisplayBox box = document.GetDisplay(document.ModelSpace).Bounds;
            const int width = 1600,
                height = 1200;
            var cad = new CadView(width, height);

            if (!box.IsEmpty)
            {
                cad.ZoomTo(box, 0.05);
            }

            string folder = TestRun.Folder("annot");
            string path = Path.Combine(folder, name + ".png");

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(
                    canvas,
                    document,
                    document.ModelSpace,
                    cad,
                    new CanvasOptions { Background = 0xFFFFFF }
                );
                canvas.Flush();

                using (SKImage image = SKImage.FromBitmap(bitmap))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (FileStream file = File.Create(path))
                {
                    data.SaveTo(file);
                }
            }

            database.Save(Path.Combine(folder, name + ".dxf"));
            return path;
        }
    }
}
