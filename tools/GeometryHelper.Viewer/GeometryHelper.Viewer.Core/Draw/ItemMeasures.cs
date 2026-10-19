using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The numbers the properties pane shows for an item: its counts, lengths, areas and volume, read from the library on the
    /// shape rebuilt from the item's data, and its box read from what is drawn.
    /// </summary>
    public static class ItemMeasures
    {
        /// <summary>
        /// Measures an item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="scene">The scene the item is in; the library works at its tolerance.</param>
        /// <param name="drawn">What the item is drawn with, for its box; null leaves the box out.</param>
        /// <returns>
        /// The name and value of each number, in the order to show them. An item the library refuses to rebuild gets one line
        /// that says why, and the box.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the item or the scene is null.</exception>
        public static IReadOnlyList<KeyValuePair<string, string>> Describe(SceneItem item, Scene scene, ItemPrimitives drawn)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            var lines = new List<KeyValuePair<string, string>>();
            void Add(string name, string value) => lines.Add(new KeyValuePair<string, string>(name, value));
            GeoAabb3? libraryBox = null;

            if (item.Kind == SceneKinds.Text)
            {
                Add("Text", item.Data.TryGetProperty("text", out System.Text.Json.JsonElement text) ? text.GetString() : string.Empty);
            }
            else if (!SceneKinds.IsKnown(item.Kind))
            {
                Add("Measures", "the viewer does not know this kind");
            }
            else
            {
                try
                {
                    // The shape is rebuilt and measured at the tolerance of the scene; where the library refuses it there, at 1E-12.
                    var measured = scene.Run(
                        () =>
                        {
                            object shape = item.ToGeometry();
                            var numbers = new List<KeyValuePair<string, string>>();
                            Measure(shape, (name, value) => numbers.Add(new KeyValuePair<string, string>(name, value)));
                            return Tuple.Create(numbers, BoxOf(shape), RebuiltVertices(shape));
                        },
                        out string note);

                    if (note != null)
                    {
                        Add("Library", note);
                    }

                    int raw = RawPointCount(item);

                    if (measured.Item3.HasValue && raw > measured.Item3.Value)
                    {
                        Add(
                            "Warning",
                            "the library keeps " + measured.Item3.Value + " of the " + raw + " vertices: the others are within " + N(note != null ? Scene.MinimalTolerance.EqualPoint : scene.EqualPoint) + " of a neighbour");
                    }

                    lines.AddRange(measured.Item1);
                    libraryBox = measured.Item2;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    Add("Library", "cannot rebuild the shape, even at 1E-12: " + e.Message);
                    Add("Vertices (raw)", RawVertexCount(item).ToString(CultureInfo.InvariantCulture));
                }
            }

            if (libraryBox.HasValue && !libraryBox.Value.IsEmpty)
            {
                GeoAabb3 box = libraryBox.Value;
                Add("Bounds min (library)", new Vec3(box.Min.X, box.Min.Y, box.Min.Z).ToString());
                Add("Bounds max (library)", new Vec3(box.Max.X, box.Max.Y, box.Max.Z).ToString());
                Add("Bounds size", new Vec3(box.SizeX, box.SizeY, box.SizeZ).ToString());
            }
            else if (drawn != null && !drawn.Bounds.IsEmpty)
            {
                Add("Bounds min", drawn.Bounds.Min.ToString());
                Add("Bounds max", drawn.Bounds.Max.ToString());
                Add("Bounds size", drawn.Bounds.Size.ToString());
            }

            return lines;
        }

        /// <summary>
        /// Gets the box the library gives for a shape, where it gives one; for a curve it is the box of the exact curve.
        /// </summary>
        private static GeoAabb3? BoxOf(object shape)
        {
            switch (shape)
            {
                case GeoLine3 s: return s.GetAabb();
                case GeoPolyline3 s: return s.GetAabb();
                case GeoPolygon3 s: return s.GetAabb();
                case GeoTriangle3 s: return s.GetAabb();
                case GeoFace3 s: return s.GetAabb();
                case GeoSolid3 s: return s.GetAabb();
                case GeoArc3 s: return s.GetAabb();
                case GeoCircle3 s: return s.GetAabb();
                case GeoEllipse3 s: return s.GetAabb();
                case GeoObb3 s: return s.GetAabb();
                case GeoPolygonArc3 s: return s.GetAabb();
                case GeoPolylineArc3 s: return s.GetAabb();
                default: return null;
            }
        }

        /// <summary>
        /// Gets how many vertices the library kept of a shape made of a list of them, or null for another shape.
        /// </summary>
        private static int? RebuiltVertices(object shape)
        {
            switch (shape)
            {
                case GeoPolyline2 s: return s.VertexCount;
                case GeoPolygon2 s: return s.VertexCount;
                case GeoPolyline3 s: return s.VertexCount;
                case GeoPolygon3 s: return s.VertexCount;
                case GeoPolylineArc2 s: return s.VertexCount;
                case GeoPolygonArc2 s: return s.VertexCount;
                case GeoPolylineArc3 s: return s.VertexCount;
                case GeoPolygonArc3 s: return s.VertexCount;
                default: return null;
            }
        }

        private static int RawPointCount(SceneItem item)
        {
            switch (item.Kind)
            {
                case "GeoPolyline2":
                case "GeoPolygon2":
                case "GeoPolyline3":
                case "GeoPolygon3":
                case "GeoPolylineArc2":
                case "GeoPolygonArc2":
                case "GeoPolylineArc3":
                case "GeoPolygonArc3":
                    return item.Data.TryGetProperty("points", out System.Text.Json.JsonElement points) && points.ValueKind == System.Text.Json.JsonValueKind.Array ? points.GetArrayLength() : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Counts the vertices in the numbers of an item: every list of two or three numbers that is not a bulge list, a list of
        /// normals or a face of a mesh.
        /// </summary>
        private static int RawVertexCount(SceneItem item)
        {
            bool mesh = item.Kind == "GeoMesh2" || item.Kind == "GeoMesh3";

            int Count(System.Text.Json.JsonElement e)
            {
                if (e.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    int n = e.GetArrayLength();

                    if ((n == 2 || n == 3) && e.EnumerateArray().All(x => x.ValueKind == System.Text.Json.JsonValueKind.Number || x.ValueKind == System.Text.Json.JsonValueKind.String))
                    {
                        return 1;
                    }

                    return e.EnumerateArray().Sum(Count);
                }

                if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    return e.EnumerateObject().Where(p => p.Name != "bulges" && p.Name != "normals" && !(mesh && p.Name == "faces")).Sum(p => Count(p.Value));
                }

                return 0;
            }

            return Count(item.Data);
        }
        private static string N(double value) => value.ToString("G10", CultureInfo.InvariantCulture);

        private static string Deg(double radians) => N(radians * 180.0 / Math.PI) + " deg";

        private static void Measure(object shape, Action<string, string> add)
        {
            switch (shape)
            {
                case GeoPoint2 p:
                    add("X", N(p.X));
                    add("Y", N(p.Y));
                    break;

                case GeoPoint3 p:
                    add("X", N(p.X));
                    add("Y", N(p.Y));
                    add("Z", N(p.Z));
                    break;

                case GeoLine2 l:
                    add("Length", N(l.Length));
                    break;

                case GeoLine3 l:
                    add("Length", N(l.Length));
                    break;

                case GeoRay2 r:
                    add("Direction", N(r.Direction.X) + ", " + N(r.Direction.Y));
                    break;

                case GeoRay3 r:
                    add("Direction", N(r.Direction.X) + ", " + N(r.Direction.Y) + ", " + N(r.Direction.Z));
                    break;

                case GeoPolyline2 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    break;

                case GeoPolyline3 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    break;

                case GeoPolygon2 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    add("Area", N(p.Area));
                    break;

                case GeoPolygon3 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    add("Area", N(p.Area));
                    add("Normal", N(p.Normal.X) + ", " + N(p.Normal.Y) + ", " + N(p.Normal.Z));
                    break;

                case GeoPolylineArc2 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    break;

                case GeoPolygonArc2 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    add("Area", N(p.Area));
                    break;

                case GeoPolylineArc3 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    break;

                case GeoPolygonArc3 p:
                    add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
                    add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
                    add("Length", N(p.Length));
                    add("Area", N(p.Area));
                    break;

                case GeoArc2 a:
                    add("Radius", N(a.Radius));
                    add("Swept angle", Deg(a.SweptAngle));
                    add("Length", N(a.Length));
                    break;

                case GeoArc3 a:
                    add("Radius", N(a.Radius));
                    add("Swept angle", Deg(a.SweptAngle));
                    add("Length", N(a.Length));
                    break;

                case GeoCircle2 c:
                    add("Radius", N(c.Radius));
                    add("Length", N(c.Length));
                    add("Area", N(c.Area));
                    break;

                case GeoCircle3 c:
                    add("Radius", N(c.Radius));
                    add("Length", N(c.Length));
                    add("Area", N(c.Area));
                    break;

                case GeoEllipse2 e:
                    add("Major radius", N(e.MajorRadius));
                    add("Minor radius", N(e.MinorRadius));
                    add("Length", N(e.Length));
                    add("Area", N(e.Area));
                    break;

                case GeoEllipse3 e:
                    add("Major radius", N(e.MajorRadius));
                    add("Minor radius", N(e.MinorRadius));
                    add("Length", N(e.Length));
                    add("Area", N(e.Area));
                    break;

                case GeoRectangle2 r:
                    add("Width", N(r.Width));
                    add("Height", N(r.Height));
                    add("Angle", Deg(r.AngleRad));
                    add("Area", N(r.Area));
                    break;

                case GeoTriangle2 t:
                    add("Length", N(t.Length));
                    add("Area", N(t.Area));
                    break;

                case GeoTriangle3 t:
                    add("Length", N(t.Perimeter));
                    add("Area", N(t.Area));
                    break;

                case GeoFace2 f:
                    add("Holes", f.Holes.Count.ToString(CultureInfo.InvariantCulture));
                    add("Vertices", (f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
                    add("Length", N(f.Length));
                    add("Area", N(f.Area));
                    break;

                case GeoFace3 f:
                    add("Holes", f.Holes.Count.ToString(CultureInfo.InvariantCulture));
                    add("Vertices", (f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
                    add("Area", N(f.Area));
                    add("Normal", N(f.Normal.X) + ", " + N(f.Normal.Y) + ", " + N(f.Normal.Z));
                    break;

                case GeoSolid3 s:
                    add("Faces", s.Faces.Count.ToString(CultureInfo.InvariantCulture));
                    add("Openings", s.Openings.Count.ToString(CultureInfo.InvariantCulture));
                    add("Vertices", s.Faces.Sum(f => f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
                    add("Closed", s.IsClosed() ? "yes" : "no");
                    add("Volume", N(s.GetVolume()));
                    add("Surface area", N(s.GetSurfaceArea()));
                    GeoPoint3 centroid = s.GetCentroid();
                    add("Centroid", N(centroid.X) + ", " + N(centroid.Y) + ", " + N(centroid.Z));
                    break;

                case GeoPolygon2[] faces:
                    add("Faces", faces.Length.ToString(CultureInfo.InvariantCulture));
                    add("Area", N(faces.Sum(f => f.Area)));
                    break;

                case GeoPolygon3[] faces:
                    add("Faces", faces.Length.ToString(CultureInfo.InvariantCulture));
                    add("Area", N(faces.Sum(f => f.Area)));
                    break;

                case GeoAabb3 b:
                    add("Size", N(b.SizeX) + " x " + N(b.SizeY) + " x " + N(b.SizeZ));
                    add("Volume", N(b.Volume));
                    break;

                case GeoObb3 b:
                    add("Size", N(b.SizeX) + " x " + N(b.SizeY) + " x " + N(b.SizeZ));
                    add("Volume", N(b.Volume));
                    break;

                case GeoPlane3 p:
                    add("Normal", N(p.Normal.X) + ", " + N(p.Normal.Y) + ", " + N(p.Normal.Z));
                    break;
            }
        }
    }
}
