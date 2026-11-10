using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;

namespace GeometryHelper.Drawing.UnitTest.Builder
{
    /// <summary>
    /// Sources, drawings and pictures for the tests of the drawing as a whole: TC2 of any length, the drawing's entities by
    /// their tags, and PNGs of a drawing's layout and model space to look at.
    /// </summary>
    internal static class BuilderKit
    {
        public static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>
        /// TC2 of a length: an I-section 300 deep, flanges 150 by 12, web 8, from the origin along X, and its end plate 200 by
        /// 340 by 20 with six holes of 22 on its far end; with an extra plate P-EXTRA 100 by 100 by 10 on the top flange at 500
        /// where asked, to be taken out of the model.
        /// </summary>
        public static DrawingSource TC2(double length = 3000, bool extra = false)
        {
            var beamFrame = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis);
            var section = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis);
            GeoSolid3 beam = GeoSolid3.Extrude(ISection(300, 150, 12, 8), section, length, Tol);
            var objects = new List<DrawingObject>
            {
                new DrawingObject("TC2-beam", DrawingObjectKind.Part, beam, beamFrame, "BEAM", "B1", "I300*150*12*8", "S355", "TC2", true),
            };

            var centres = new List<GeoPolygon2>();

            foreach (double z in new[] { -100.0, 0.0, 100.0 })
            {
                foreach (double y in new[] { -50.0, 50.0 })
                {
                    centres.Add(Circle(y, z, 11, 24));
                }
            }

            var plateFrame = new GeoCoordinateSystem3(new GeoPoint3(length, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
            GeoSolid3 plate = GeoSolid3.Extrude(new GeoFace2(Rectangle(-100, -170, 100, 170), centres), plateFrame, 20, Tol);
            objects.Add(new DrawingObject("TC2-endplate", DrawingObjectKind.Part, plate, plateFrame, "PLATE", "P1", "PL20*200", "S355", "TC2"));

            if (extra)
            {
                var extraFrame = new GeoCoordinateSystem3(new GeoPoint3(500, -50, 150), GeoVector3.XAxis, GeoVector3.YAxis);
                GeoSolid3 body = GeoSolid3.Extrude(Rectangle(0, 0, 100, 100), extraFrame, 10, Tol);
                objects.Add(new DrawingObject("P-EXTRA", DrawingObjectKind.Part, body, extraFrame, "PLATE", "P9", "PL10*100", "S355", "TC2"));
            }

            return new DrawingSource(objects);
        }

        /// <summary>The settings of the assembly preset on an A3, which TC2 fits at 1:10.</summary>
        public static DrawingSettings AssemblyOnA3(DrawingExportMode mode = DrawingExportMode.LayoutAndViewports)
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Sheet.Size = Packing.PaperSize.A3;
            settings.ExportMode = mode;
            return settings;
        }

        /// <summary>The entities of a space of a drawing tagged with an element's id, in their order.</summary>
        public static List<DxfEntity> Tagged(IEnumerable<DxfEntity> space, string id)
            => space.Where(e => !e.IsErased && DrawingEntityTags.TryRead(e, out string objectId, out _) && objectId == id).ToList();

        /// <summary>The DIMENSION of an element id in model space.</summary>
        public static DxfDimension Dimension(DxfDatabase database, string id)
            => Tagged(database.ModelSpace.Cast<DxfEntity>(), id).OfType<DxfDimension>().Single();

        /// <summary>The layout the sheet is drawn on.</summary>
        public static DxfLayout SheetLayout(DxfDatabase database)
            => database.Layouts.Single(l => l.LayoutName == DrawingDxfWriter.LayoutName);

        /// <summary>The drawing saved to bytes and read back.</summary>
        public static DxfDatabase SavedAndOpened(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                stream.Position = 0;
                return DxfDatabase.Open(stream);
            }
        }

        /// <summary>The bytes the drawing saves to.</summary>
        public static byte[] Bytes(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>The folder build of the run (<see cref="TestRun"/>), made where missing.</summary>
        public static string Folder()
        {
            return TestRun.Folder("build");
        }

        /// <summary>
        /// Draws a space of a drawing, as the presenter shows it, into a PNG in the folder build of the run or a folder given;
        /// gives the path. The layout is drawn on its paper, model space on what it draws.
        /// </summary>
        public static string SavePicture(DxfDatabase database, string spaceName, string name, string folder = null)
        {
            CadDocument document = CadDocument.From(database, name + ".dxf");
            CadSpace space = document.Spaces.Single(s => s.Name == spaceName);
            DisplayBox box = space.IsModel ? document.GetDisplay(space).Bounds : space.PaperBounds.Union(document.GetDisplay(space).Bounds);
            const int width = 2000, height = 1414;
            var cad = new CadView(width, height);

            if (!box.IsEmpty)
            {
                cad.ZoomTo(box, 0.02);
            }

            if (folder != null)
            {
                Directory.CreateDirectory(folder);
            }

            string path = Path.Combine(folder ?? Folder(), name + ".png");

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, space, cad, new CanvasOptions { Background = 0xFFFFFF });
                canvas.Flush();

                using (SKImage image = SKImage.FromBitmap(bitmap))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (FileStream file = File.Create(path))
                {
                    data.SaveTo(file);
                }
            }

            return path;
        }

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        private static GeoPolygon2 Circle(double x, double y, double radius, int corners)
            => new GeoPolygon2(Enumerable.Range(0, corners).Select(i => P(x + radius * Math.Cos(2 * Math.PI * i / corners), y + radius * Math.Sin(2 * Math.PI * i / corners))));

        private static GeoPolygon2 ISection(double depth, double width, double flange, double web)
        {
            double w = width / 2, d = depth / 2, t = web / 2, f = d - flange;
            return new GeoPolygon2(P(-w, -d), P(w, -d), P(w, -f), P(t, -f), P(t, f), P(w, f), P(w, d), P(-w, d), P(-w, f), P(-t, f), P(-t, -f), P(-w, -f));
        }
    }
}
