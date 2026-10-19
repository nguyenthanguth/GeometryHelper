using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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
        /// Measures an item as <see cref="Describe"/> does, with every number written as the 17 digits that read back as the same
        /// double, and nothing else: no box, no warning. This is what two builds of the library are compared by.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="scene">The scene the item is in; the library works at its tolerance.</param>
        /// <returns>
        /// The name and value of each number the library gives, in the order <see cref="Describe"/> shows them, and last a "Library" line where the shape was rebuilt
        /// and measured at 1E-12; none for a label or a kind the viewer does not know.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the item or the scene is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the library refuses the shape at the tolerance of the scene and at 1E-12.</exception>
        /// <exception cref="InvalidOperationException">Thrown likewise.</exception>
        public static IReadOnlyList<KeyValuePair<string, string>> Numbers(SceneItem item, Scene scene)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (item.Kind == SceneKinds.Text || !SceneKinds.IsKnown(item.Kind))
            {
                return new KeyValuePair<string, string>[0];
            }

            bool before = _exact;
            _exact = true;

            try
            {
                List<KeyValuePair<string, string>> measured = scene.Run(
                    () =>
                    {
                        var numbers = new List<KeyValuePair<string, string>>();
                        Measure(item.ToGeometry(), (name, value) => numbers.Add(new KeyValuePair<string, string>(name, value)));
                        return numbers;
                    },
                    out string note);

                // Where the library refused the shape at the tolerance of the scene and the numbers are those at 1E-12, they come with the reason.
                if (note != null)
                {
                    measured.Add(new KeyValuePair<string, string>("Library", note));
                }

                return measured;
            }
            finally
            {
                _exact = before;
            }
        }

        /// <summary>
        /// Gets the box the library gives for a shape, where it gives one; for a curve it is the box of the exact curve.
        /// </summary>
        private static GeoAabb3? BoxOf(object shape)
        {
            switch (shape.GetType().Name)
            {
                case "GeoLine3": return BoxOf_GeoLine3(shape);
                case "GeoPolyline3": return BoxOf_GeoPolyline3(shape);
                case "GeoPolygon3": return BoxOf_GeoPolygon3(shape);
                case "GeoTriangle3": return BoxOf_GeoTriangle3(shape);
                case "GeoFace3": return BoxOf_GeoFace3(shape);
                case "GeoSolid3": return BoxOf_GeoSolid3(shape);
                case "GeoArc3": return BoxOf_GeoArc3(shape);
                case "GeoCircle3": return BoxOf_GeoCircle3(shape);
                case "GeoEllipse3": return BoxOf_GeoEllipse3(shape);
                case "GeoObb3": return BoxOf_GeoObb3(shape);
                case "GeoPolygonArc3": return BoxOf_GeoPolygonArc3(shape);
                case "GeoPolylineArc3": return BoxOf_GeoPolylineArc3(shape);
                default: return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoLine3(object shape) => ((GeoLine3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoPolyline3(object shape) => ((GeoPolyline3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoPolygon3(object shape) => ((GeoPolygon3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoTriangle3(object shape) => ((GeoTriangle3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoFace3(object shape) => ((GeoFace3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoSolid3(object shape) => ((GeoSolid3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoArc3(object shape) => ((GeoArc3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoCircle3(object shape) => ((GeoCircle3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoEllipse3(object shape) => ((GeoEllipse3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoObb3(object shape) => ((GeoObb3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoPolygonArc3(object shape) => ((GeoPolygonArc3)shape).GetAabb();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GeoAabb3? BoxOf_GeoPolylineArc3(object shape) => ((GeoPolylineArc3)shape).GetAabb();

        /// <summary>
        /// Gets how many vertices the library kept of a shape made of a list of them, or null for another shape.
        /// </summary>
        private static int? RebuiltVertices(object shape)
        {
            switch (shape.GetType().Name)
            {
                case "GeoPolyline2": return VertexCountOf_GeoPolyline2(shape);
                case "GeoPolygon2": return VertexCountOf_GeoPolygon2(shape);
                case "GeoPolyline3": return VertexCountOf_GeoPolyline3(shape);
                case "GeoPolygon3": return VertexCountOf_GeoPolygon3(shape);
                case "GeoPolylineArc2": return VertexCountOf_GeoPolylineArc2(shape);
                case "GeoPolygonArc2": return VertexCountOf_GeoPolygonArc2(shape);
                case "GeoPolylineArc3": return VertexCountOf_GeoPolylineArc3(shape);
                case "GeoPolygonArc3": return VertexCountOf_GeoPolygonArc3(shape);
                default: return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolyline2(object shape) => ((GeoPolyline2)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolygon2(object shape) => ((GeoPolygon2)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolyline3(object shape) => ((GeoPolyline3)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolygon3(object shape) => ((GeoPolygon3)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolylineArc2(object shape) => ((GeoPolylineArc2)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolygonArc2(object shape) => ((GeoPolygonArc2)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolylineArc3(object shape) => ((GeoPolylineArc3)shape).VertexCount;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int? VertexCountOf_GeoPolygonArc3(object shape) => ((GeoPolygonArc3)shape).VertexCount;

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
        // The pane shows ten digits; the comparison of two builds keeps all seventeen, which read back as the same double.
        [ThreadStatic]
        private static bool _exact;

        private static string N(double value) => value.ToString(_exact ? "G17" : "G10", CultureInfo.InvariantCulture);

        private static string Deg(double radians) => N(radians * 180.0 / Math.PI) + " deg";

        private static void Measure(object shape, Action<string, string> add)
        {
            switch (shape.GetType().Name)
            {
                case "GeoPoint2":
                    Measure_GeoPoint2(shape, add);
                    break;

                case "GeoPoint3":
                    Measure_GeoPoint3(shape, add);
                    break;

                case "GeoLine2":
                    Measure_GeoLine2(shape, add);
                    break;

                case "GeoLine3":
                    Measure_GeoLine3(shape, add);
                    break;

                case "GeoRay2":
                    Measure_GeoRay2(shape, add);
                    break;

                case "GeoRay3":
                    Measure_GeoRay3(shape, add);
                    break;

                case "GeoPolyline2":
                    Measure_GeoPolyline2(shape, add);
                    break;

                case "GeoPolyline3":
                    Measure_GeoPolyline3(shape, add);
                    break;

                case "GeoPolygon2":
                    Measure_GeoPolygon2(shape, add);
                    break;

                case "GeoPolygon3":
                    Measure_GeoPolygon3(shape, add);
                    break;

                case "GeoPolylineArc2":
                    Measure_GeoPolylineArc2(shape, add);
                    break;

                case "GeoPolygonArc2":
                    Measure_GeoPolygonArc2(shape, add);
                    break;

                case "GeoPolylineArc3":
                    Measure_GeoPolylineArc3(shape, add);
                    break;

                case "GeoPolygonArc3":
                    Measure_GeoPolygonArc3(shape, add);
                    break;

                case "GeoArc2":
                    Measure_GeoArc2(shape, add);
                    break;

                case "GeoArc3":
                    Measure_GeoArc3(shape, add);
                    break;

                case "GeoCircle2":
                    Measure_GeoCircle2(shape, add);
                    break;

                case "GeoCircle3":
                    Measure_GeoCircle3(shape, add);
                    break;

                case "GeoEllipse2":
                    Measure_GeoEllipse2(shape, add);
                    break;

                case "GeoEllipse3":
                    Measure_GeoEllipse3(shape, add);
                    break;

                case "GeoRectangle2":
                    Measure_GeoRectangle2(shape, add);
                    break;

                case "GeoTriangle2":
                    Measure_GeoTriangle2(shape, add);
                    break;

                case "GeoTriangle3":
                    Measure_GeoTriangle3(shape, add);
                    break;

                case "GeoFace2":
                    Measure_GeoFace2(shape, add);
                    break;

                case "GeoFace3":
                    Measure_GeoFace3(shape, add);
                    break;

                case "GeoSolid3":
                    Measure_GeoSolid3(shape, add);
                    break;

                case "GeoPolygon2[]":
                    Measure_GeoPolygon2Array(shape, add);
                    break;

                case "GeoPolygon3[]":
                    Measure_GeoPolygon3Array(shape, add);
                    break;

                case "GeoAabb3":
                    Measure_GeoAabb3(shape, add);
                    break;

                case "GeoObb3":
                    Measure_GeoObb3(shape, add);
                    break;

                case "GeoPlane3":
                    Measure_GeoPlane3(shape, add);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPoint2(object shape, Action<string, string> add)
        {
            GeoPoint2 p = (GeoPoint2)shape;
            add("X", N(p.X));
            add("Y", N(p.Y));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPoint3(object shape, Action<string, string> add)
        {
            GeoPoint3 p = (GeoPoint3)shape;
            add("X", N(p.X));
            add("Y", N(p.Y));
            add("Z", N(p.Z));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoLine2(object shape, Action<string, string> add)
        {
            GeoLine2 l = (GeoLine2)shape;
            add("Length", N(l.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoLine3(object shape, Action<string, string> add)
        {
            GeoLine3 l = (GeoLine3)shape;
            add("Length", N(l.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoRay2(object shape, Action<string, string> add)
        {
            GeoRay2 r = (GeoRay2)shape;
            add("Direction", N(r.Direction.X) + ", " + N(r.Direction.Y));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoRay3(object shape, Action<string, string> add)
        {
            GeoRay3 r = (GeoRay3)shape;
            add("Direction", N(r.Direction.X) + ", " + N(r.Direction.Y) + ", " + N(r.Direction.Z));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolyline2(object shape, Action<string, string> add)
        {
            GeoPolyline2 p = (GeoPolyline2)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolyline3(object shape, Action<string, string> add)
        {
            GeoPolyline3 p = (GeoPolyline3)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygon2(object shape, Action<string, string> add)
        {
            GeoPolygon2 p = (GeoPolygon2)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
            add("Area", N(p.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygon3(object shape, Action<string, string> add)
        {
            GeoPolygon3 p = (GeoPolygon3)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
            add("Area", N(p.Area));
            add("Normal", N(p.Normal.X) + ", " + N(p.Normal.Y) + ", " + N(p.Normal.Z));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolylineArc2(object shape, Action<string, string> add)
        {
            GeoPolylineArc2 p = (GeoPolylineArc2)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygonArc2(object shape, Action<string, string> add)
        {
            GeoPolygonArc2 p = (GeoPolygonArc2)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
            add("Area", N(p.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolylineArc3(object shape, Action<string, string> add)
        {
            GeoPolylineArc3 p = (GeoPolylineArc3)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygonArc3(object shape, Action<string, string> add)
        {
            GeoPolygonArc3 p = (GeoPolygonArc3)shape;
            add("Vertices", p.VertexCount.ToString(CultureInfo.InvariantCulture));
            add("Arcs", p.Bulges.Count(b => b != 0.0).ToString(CultureInfo.InvariantCulture));
            add("Length", N(p.Length));
            add("Area", N(p.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoArc2(object shape, Action<string, string> add)
        {
            GeoArc2 a = (GeoArc2)shape;
            add("Radius", N(a.Radius));
            add("Swept angle", Deg(a.SweptAngle));
            add("Length", N(a.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoArc3(object shape, Action<string, string> add)
        {
            GeoArc3 a = (GeoArc3)shape;
            add("Radius", N(a.Radius));
            add("Swept angle", Deg(a.SweptAngle));
            add("Length", N(a.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoCircle2(object shape, Action<string, string> add)
        {
            GeoCircle2 c = (GeoCircle2)shape;
            add("Radius", N(c.Radius));
            add("Length", N(c.Length));
            add("Area", N(c.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoCircle3(object shape, Action<string, string> add)
        {
            GeoCircle3 c = (GeoCircle3)shape;
            add("Radius", N(c.Radius));
            add("Length", N(c.Length));
            add("Area", N(c.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoEllipse2(object shape, Action<string, string> add)
        {
            GeoEllipse2 e = (GeoEllipse2)shape;
            add("Major radius", N(e.MajorRadius));
            add("Minor radius", N(e.MinorRadius));
            add("Length", N(e.Length));
            add("Area", N(e.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoEllipse3(object shape, Action<string, string> add)
        {
            GeoEllipse3 e = (GeoEllipse3)shape;
            add("Major radius", N(e.MajorRadius));
            add("Minor radius", N(e.MinorRadius));
            add("Length", N(e.Length));
            add("Area", N(e.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoRectangle2(object shape, Action<string, string> add)
        {
            GeoRectangle2 r = (GeoRectangle2)shape;
            add("Width", N(r.Width));
            add("Height", N(r.Height));
            add("Angle", Deg(r.AngleRad));
            add("Area", N(r.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoTriangle2(object shape, Action<string, string> add)
        {
            GeoTriangle2 t = (GeoTriangle2)shape;
            add("Length", N(t.Length));
            add("Area", N(t.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoTriangle3(object shape, Action<string, string> add)
        {
            GeoTriangle3 t = (GeoTriangle3)shape;
            add("Length", N(t.Perimeter));
            add("Area", N(t.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoFace2(object shape, Action<string, string> add)
        {
            GeoFace2 f = (GeoFace2)shape;
            add("Holes", f.Holes.Count.ToString(CultureInfo.InvariantCulture));
            add("Vertices", (f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
            add("Length", N(f.Length));
            add("Area", N(f.Area));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoFace3(object shape, Action<string, string> add)
        {
            GeoFace3 f = (GeoFace3)shape;
            add("Holes", f.Holes.Count.ToString(CultureInfo.InvariantCulture));
            add("Vertices", (f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
            add("Area", N(f.Area));
            add("Normal", N(f.Normal.X) + ", " + N(f.Normal.Y) + ", " + N(f.Normal.Z));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoSolid3(object shape, Action<string, string> add)
        {
            GeoSolid3 s = (GeoSolid3)shape;
            add("Faces", s.Faces.Count.ToString(CultureInfo.InvariantCulture));
            add("Openings", s.Openings.Count.ToString(CultureInfo.InvariantCulture));
            add("Vertices", s.Faces.Sum(f => f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount)).ToString(CultureInfo.InvariantCulture));
            add("Closed", s.IsClosed() ? "yes" : "no");
            add("Volume", N(s.GetVolume()));
            add("Surface area", N(s.GetSurfaceArea()));
            GeoPoint3 centroid = s.GetCentroid();
            add("Centroid", N(centroid.X) + ", " + N(centroid.Y) + ", " + N(centroid.Z));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygon2Array(object shape, Action<string, string> add)
        {
            GeoPolygon2[] faces = (GeoPolygon2[])shape;
            add("Faces", faces.Length.ToString(CultureInfo.InvariantCulture));
            add("Area", N(faces.Sum(f => f.Area)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPolygon3Array(object shape, Action<string, string> add)
        {
            GeoPolygon3[] faces = (GeoPolygon3[])shape;
            add("Faces", faces.Length.ToString(CultureInfo.InvariantCulture));
            add("Area", N(faces.Sum(f => f.Area)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoAabb3(object shape, Action<string, string> add)
        {
            GeoAabb3 b = (GeoAabb3)shape;
            add("Size", N(b.SizeX) + " x " + N(b.SizeY) + " x " + N(b.SizeZ));
            add("Volume", N(b.Volume));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoObb3(object shape, Action<string, string> add)
        {
            GeoObb3 b = (GeoObb3)shape;
            add("Size", N(b.SizeX) + " x " + N(b.SizeY) + " x " + N(b.SizeZ));
            add("Volume", N(b.Volume));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Measure_GeoPlane3(object shape, Action<string, string> add)
        {
            GeoPlane3 p = (GeoPlane3)shape;
            add("Normal", N(p.Normal.X) + ", " + N(p.Normal.Y) + ", " + N(p.Normal.Z));
        }
    }
}
