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
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    /// <summary>
    /// What the tests of marks and symbols share: a view marked with its dimensions, the checks that a leader ends on its
    /// object, and a PNG of a drawing to look at.
    /// </summary>
    internal static class MarksKit
    {
        /// <summary>A view of objects, its dimensions and its marks, at a scale.</summary>
        internal sealed class Marked
        {
            public DrawingObject[] Objects;
            public GeoCoordinateSystem3 Frame;
            public ProjectedView2 View;
            public IReadOnlyList<DrawingDimension> Dimensions;
            public IReadOnlyList<ObjectMark> Marks;
            public double Scale;
        }

        /// <summary>Projects, dimensions and marks a view with the default settings at a scale.</summary>
        public static Marked Mark(IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, double scale, MarkSettings settings = null, MarkContent content = null, bool dimensions = true)
        {
            DrawingObject[] list = objects.ToArray();
            ProjectedView2 view = AnnotationKit.Project(list, frame);
            IReadOnlyList<DrawingDimension> made = dimensions ? Annotator.Dimension(view, list, frame, new DimensionSettings { Scale = scale }, Tol) : new DrawingDimension[0];
            IReadOnlyList<ObjectMark> marks = Marker.Mark(view, list, frame, made, settings ?? new MarkSettings(), content ?? new MarkContent(), scale, Tol);
            return new Marked { Objects = list, Frame = frame, View = view, Dimensions = made, Marks = marks, Scale = scale };
        }

        /// <summary>
        /// Says, for each mark whose leader does not end on its object, why: its end further than the point tolerance from
        /// every visible line of the object, and, for a bolt seen along its axis, not at its centre inside its circle.
        /// </summary>
        public static List<string> LeadersOffTheirObjects(Marked marked)
        {
            var found = new List<string>();

            foreach (ObjectMark mark in marked.Marks)
            {
                GeoPoint2 end = mark.Anchor;
                ProjectedEdge2[] edges = marked.View.VisibleEdges.Where(e => e.ObjectIds.Contains(mark.ObjectId)).ToArray();
                bool onLine = edges.Any(e => Distance(e, end) <= Tol.EqualPoint);
                bool inCircle = edges.Any(e => e.IsCircle && e.Arc.Center.DistanceTo(end) < e.Arc.Radius);

                if (!onLine && !(mark.Kind == MarkKind.Bolt && inCircle))
                {
                    found.Add(mark.Id + " ends at (" + end.X + ", " + end.Y + "), off " + mark.ObjectId);
                }

                if (!mark.ObjectIds.Contains(mark.ObjectId))
                {
                    found.Add(mark.Id + " ends on " + mark.ObjectId + ", which it does not stand for");
                }
            }

            return found;
        }

        /// <summary>The distance from a point to a line of a view.</summary>
        public static double Distance(ProjectedEdge2 edge, GeoPoint2 p)
        {
            if (!edge.IsArc)
            {
                return edge.Line.DistanceTo(p);
            }

            return edge.Arc.DistanceTo(p, Tol);
        }

        /// <summary>A mark as text, its numbers to the bit, for comparing two runs.</summary>
        public static string Signature(ObjectMark m)
        {
            string Bits(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16");
            return m.Id + " " + m.Kind + " " + m.Text + " " + m.ObjectId + " " + Bits(m.TextBox.Center.X) + Bits(m.TextBox.Center.Y)
                + " " + string.Join(",", m.Leader.Select(p => Bits(p.X) + Bits(p.Y)));
        }

        /// <summary>
        /// Draws a drawing, as the presenter shows it, into a PNG under %TEMP%/gk/drawing/marks, and saves the DXF beside it;
        /// gives the path of the PNG.
        /// </summary>
        public static string SavePicture(DxfDatabase database, string name)
        {
            CadDocument document = CadDocument.From(database, name + ".dxf");
            DisplayBox box = document.GetDisplay(document.ModelSpace).Bounds;
            const int width = 1600, height = 1200;
            var cad = new CadView(width, height);

            if (!box.IsEmpty)
            {
                cad.ZoomTo(box, 0.05);
            }

            string folder = Path.Combine(Path.GetTempPath(), "gk", "drawing", "marks");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png");

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, document.ModelSpace, cad, new CanvasOptions { Background = 0xFFFFFF });
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

        /// <summary>A weld object: a small box at a point, its kind, side, size and length in its attributes.</summary>
        public static DrawingObject Weld(string id, GeoPoint3 at, string type, string side, string size, string length, string parent = null, string secondary = null)
        {
            var attributes = new List<KeyValuePair<string, string>>();

            void Add(string name, string value)
            {
                if (value != null)
                {
                    attributes.Add(new KeyValuePair<string, string>(name, value));
                }
            }

            Add(WeldSymbol.TypeAttribute, type);
            Add(WeldSymbol.SideAttribute, side);
            Add(WeldSymbol.SizeAttribute, size);
            Add(WeldSymbol.LengthAttribute, length);
            Add(WeldSymbol.SecondaryPartAttribute, secondary);
            return new DrawingObject(id, DrawingObjectKind.Weld, Box(at.X - 5, at.Y - 5, at.Z - 5, at.X + 5, at.Y + 5, at.Z + 5), GeoCoordinateSystem3.Global, parentId: parent, attributes: attributes);
        }
    }
}
