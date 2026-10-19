using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A point of the scene that is a shape by itself, for the snaps and the picking: a point item, the origin of a ray or a plane.
    /// </summary>
    public readonly struct ScenePoint
    {
        /// <summary>
        /// Initializes a point.
        /// </summary>
        public ScenePoint(int itemIndex, Vec3 at)
        {
            ItemIndex = itemIndex;
            At = at;
        }

        /// <summary>
        /// Gets the index of the item in <see cref="Scene.Items"/>.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets where it is.
        /// </summary>
        public Vec3 At { get; }
    }

    /// <summary>
    /// The exact curves and points of a scene, read from the numbers in the data of its items: the segments of lines, chains, loops, the
    /// edges of faces and bodies and meshes, boxes and triangles, and the arcs, circles and ellipses, whole and in the bulges of a chain.
    /// The snaps, the picking and the measuring work on these and never on the chords the curves are drawn with.
    /// </summary>
    public sealed class ExactGeometry
    {
        /// <summary>
        /// The most curves read from a scene: a million. A scene with more is read as far as it goes, and the rest is left out of the
        /// snaps and the picking of curves, with a warning.
        /// </summary>
        public const int MaxCurves = 1000000;

        private readonly List<SceneCurve> _curves = new List<SceneCurve>();
        private readonly List<ScenePoint> _points = new List<ScenePoint>();
        private readonly List<string> _warnings = new List<string>();
        private bool _full;

        // The edges a face, a body or a mesh has already given, so that an edge two faces share is one curve; only asked whether an edge is in it,
        // never walked, so that nothing follows its order.
        private HashSet<EdgeKey> _seen;

        private ExactGeometry()
        {
        }

        /// <summary>
        /// Gets the curves, in the order of the items and, within an item, of its data.
        /// </summary>
        public IReadOnlyList<SceneCurve> Curves => _curves;

        /// <summary>
        /// Gets the points that are shapes by themselves.
        /// </summary>
        public IReadOnlyList<ScenePoint> Points => _points;

        /// <summary>
        /// Gets what could not be read, one line to an item or to the cap on curves.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Reads the curves and points of a scene.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <param name="primitives">What the scene is drawn with, which gives the rays the length they are drawn to; null leaves the rays out.</param>
        /// <returns>The curves and points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the scene is null.</exception>
        public static ExactGeometry Build(Scene scene, Primitives primitives)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            var result = new ExactGeometry();

            for (int i = 0; i < scene.Items.Count; i++)
            {
                if (result._full)
                {
                    break;
                }

                SceneItem item = scene.Items[i];
                result._seen = item.Kind == "GeoSolid3" || item.Kind == "GeoFace2" || item.Kind == "GeoFace3" || item.Kind == "GeoMesh2" || item.Kind == "GeoMesh3" ? new HashSet<EdgeKey>() : null;

                try
                {
                    result.ReadItem(item, i, primitives != null && i < primitives.Items.Count ? primitives.Items[i] : null);
                }
                catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is ArgumentException || e is KeyNotFoundException)
                {
                    // The drawing has already said what is wrong with the numbers of this item; what was read before it failed stays.
                    result._warnings.Add((item.Name ?? item.Kind) + ": its curves could not all be read for snapping (" + e.Message + ")");
                }
            }

            if (result._full)
            {
                result._warnings.Add("the scene has more than " + MaxCurves + " curves; the snaps and the picking of lines cover the first " + MaxCurves);
            }

            return result;
        }

        private void Add(SceneCurve curve)
        {
            if (curve == null)
            {
                return;
            }

            if (_curves.Count >= MaxCurves)
            {
                _full = true;
                return;
            }

            _curves.Add(curve);
        }

        private void Segment(int item, Vec3 a, Vec3 b)
        {
            if (a.IsFinite && b.IsFinite && (a.X != b.X || a.Y != b.Y || a.Z != b.Z) && (_seen == null || _seen.Add(new EdgeKey(a, b))))
            {
                Add(SceneCurve.Segment(item, a, b));
            }
        }

        private void Chain(int item, Vec3[] points, bool closed)
        {
            int edges = closed ? points.Length : points.Length - 1;

            for (int k = 0; k < edges; k++)
            {
                Segment(item, points[k], points[(k + 1) % points.Length]);
            }
        }

        private void Edges(int item, Vec3[] points, double[] bulges, IReadOnlyList<Vec3> normals, bool closed)
        {
            int edges = closed ? points.Length : points.Length - 1;

            for (int k = 0; k < edges; k++)
            {
                Vec3 from = points[k];
                Vec3 to = points[(k + 1) % points.Length];

                if (!from.IsFinite || !to.IsFinite)
                {
                    continue;
                }

                double bulge = k < bulges.Length ? bulges[k] : 0.0;
                Vec3 normal = normals == null ? new Vec3(0.0, 0.0, 1.0) : k < normals.Count ? normals[k].Normalized() : default(Vec3);
                Add(SceneCurve.Edge(item, from, to, bulge, normal));
            }
        }

        private void ReadItem(SceneItem item, int index, ItemPrimitives drawn)
        {
            JsonElement d = item.Data;

            switch (item.Kind)
            {
                case "GeoPoint2": _points.Add(new ScenePoint(index, DataJson.Point(d, 2))); break;
                case "GeoPoint3": _points.Add(new ScenePoint(index, DataJson.Point(d, 3))); break;
                case "GeoLine2": Segment(index, DataJson.Point(d, "a", 2), DataJson.Point(d, "b", 2)); break;
                case "GeoLine3": Segment(index, DataJson.Point(d, "a", 3), DataJson.Point(d, "b", 3)); break;

                case "GeoRay2":
                case "GeoRay3":
                {
                    Vec3 origin = DataJson.Point(d, "a", item.Kind == "GeoRay2" ? 2 : 3);

                    if (origin.IsFinite)
                    {
                        _points.Add(new ScenePoint(index, origin));
                    }

                    if (drawn != null)
                    {
                        foreach (DrawPolyline line in drawn.Polylines)
                        {
                            Chain(index, line.Points, line.Closed);
                        }
                    }

                    break;
                }

                case "GeoPolyline2": Chain(index, DataJson.Points(d, "points", 2), false); break;
                case "GeoPolygon2": Chain(index, DataJson.Points(d, "points", 2), true); break;
                case "GeoPolyline3": Chain(index, DataJson.Points(d, "points", 3), false); break;
                case "GeoPolygon3": Chain(index, DataJson.Points(d, "points", 3), true); break;

                case "GeoPolylineArc2":
                case "GeoPolygonArc2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    Edges(index, points, DataJson.Bulges(d, points.Length), null, item.Kind == "GeoPolygonArc2");
                    break;
                }

                case "GeoPolylineArc3":
                case "GeoPolygonArc3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    IReadOnlyList<Vec3> normals = DataJson.TryGet(d, "normals", out JsonElement list) ? DataJson.Points(list, 3) : new Vec3[0];
                    Edges(index, points, DataJson.Bulges(d, points.Length), normals, item.Kind == "GeoPolygonArc3");
                    break;
                }

                case "GeoArc2":
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 2), new Vec3(1, 0, 0), new Vec3(0, 1, 0), DataJson.Number(d, "radius"), DataJson.Number(d, "radius"), DataJson.Number(d, "start"), DataJson.Number(d, "sweep")));
                    break;

                case "GeoArc3":
                {
                    ArcTessellation.Axes(DataJson.Point(d, "normal", 3), out Vec3 u, out Vec3 v);
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 3), u, v, DataJson.Number(d, "radius"), DataJson.Number(d, "radius"), DataJson.Number(d, "start"), DataJson.Number(d, "sweep")));
                    break;
                }

                case "GeoCircle2":
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 2), new Vec3(1, 0, 0), new Vec3(0, 1, 0), DataJson.Number(d, "radius"), DataJson.Number(d, "radius"), 0.0, 2.0 * Math.PI));
                    break;

                case "GeoCircle3":
                {
                    ArcTessellation.Axes(DataJson.Point(d, "normal", 3), out Vec3 u, out Vec3 v);
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 3), u, v, DataJson.Number(d, "radius"), DataJson.Number(d, "radius"), 0.0, 2.0 * Math.PI));
                    break;
                }

                case "GeoEllipse2":
                {
                    Vec3 major = DataJson.Point(d, "majorAxis", 2).Normalized();
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 2), major, new Vec3(-major.Y, major.X, 0.0), DataJson.Number(d, "majorRadius"), DataJson.Number(d, "minorRadius"), 0.0, 2.0 * Math.PI));
                    break;
                }

                case "GeoEllipse3":
                {
                    Vec3 major = DataJson.Point(d, "majorAxis", 3).Normalized();
                    Vec3 minor = Vec3.Cross(DataJson.Point(d, "normal", 3).Normalized(), major);
                    Add(SceneCurve.Conic(index, DataJson.Point(d, "center", 3), major, minor, DataJson.Number(d, "majorRadius"), DataJson.Number(d, "minorRadius"), 0.0, 2.0 * Math.PI));
                    break;
                }

                case "GeoRectangle2":
                {
                    Vec3 center = DataJson.Point(d, "center", 2);
                    double halfWidth = DataJson.Number(d, "width") * 0.5;
                    double halfHeight = DataJson.Number(d, "height") * 0.5;
                    double angle = DataJson.Number(d, "angle");
                    double cos = Math.Cos(angle);
                    double sin = Math.Sin(angle);

                    Vec3 Corner(double x, double y) => new Vec3(center.X + x * cos - y * sin, center.Y + x * sin + y * cos, 0.0);

                    Chain(index, new[] { Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight), Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight) }, true);
                    break;
                }

                case "GeoTriangle2": Chain(index, DataJson.Points(d, "points", 2), true); break;
                case "GeoTriangle3": Chain(index, DataJson.Points(d, "points", 3), true); break;

                case "GeoFace2": Face(index, d, 2); break;
                case "GeoFace3": Face(index, d, 3); break;
                case "GeoSolid3": Solid(index, d); break;

                case "GeoMesh2":
                case "GeoMesh3":
                {
                    Vec3[] points = DataJson.Points(d, "points", item.Kind == "GeoMesh2" ? 2 : 3);

                    foreach (int[] face in GeometryDecoder.Faces(d))
                    {
                        if (face.Length < 2)
                        {
                            continue;
                        }

                        for (int k = 0; k < face.Length; k++)
                        {
                            int a = face[k];
                            int b = face[(k + 1) % face.Length];

                            if (a >= 0 && a < points.Length && b >= 0 && b < points.Length)
                            {
                                Segment(index, points[a], points[b]);
                            }
                        }
                    }

                    break;
                }

                case "GeoAabb3":
                {
                    if (DataJson.TryGet(d, "empty", out JsonElement empty) && empty.ValueKind == JsonValueKind.True)
                    {
                        break;
                    }

                    Vec3 min = DataJson.Point(d, "min", 3);
                    Vec3 max = DataJson.Point(d, "max", 3);

                    Box(index, new[]
                    {
                        new Vec3(min.X, min.Y, min.Z), new Vec3(max.X, min.Y, min.Z), new Vec3(max.X, max.Y, min.Z), new Vec3(min.X, max.Y, min.Z),
                        new Vec3(min.X, min.Y, max.Z), new Vec3(max.X, min.Y, max.Z), new Vec3(max.X, max.Y, max.Z), new Vec3(min.X, max.Y, max.Z),
                    });
                    break;
                }

                case "GeoObb3":
                {
                    Vec3 center = DataJson.Point(d, "center", 3);
                    Vec3 axisX = DataJson.Point(d, "axisX", 3);
                    Vec3 axisY = DataJson.Point(d, "axisY", 3);
                    Vec3 axisZ = Vec3.Cross(axisX, axisY);
                    Vec3 dx = axisX * (DataJson.Number(d, "sizeX") * 0.5);
                    Vec3 dy = axisY * (DataJson.Number(d, "sizeY") * 0.5);
                    Vec3 dz = axisZ * (DataJson.Number(d, "sizeZ") * 0.5);

                    Box(index, new[]
                    {
                        center - dx - dy - dz, center + dx - dy - dz, center + dx + dy - dz, center - dx + dy - dz,
                        center - dx - dy + dz, center + dx - dy + dz, center + dx + dy + dz, center - dx + dy + dz,
                    });
                    break;
                }

                case "GeoPlane3":
                {
                    Vec3 origin = DataJson.Point(d, "origin", 3);

                    if (origin.IsFinite)
                    {
                        _points.Add(new ScenePoint(index, origin));
                    }

                    // The outline of the patch and the tick along the normal are what is drawn, and what is picked.
                    if (drawn != null)
                    {
                        foreach (DrawPolyline line in drawn.Polylines)
                        {
                            Chain(index, line.Points, line.Closed);
                        }
                    }

                    break;
                }
            }
        }

        private void Face(int item, JsonElement d, int dimension)
        {
            Chain(item, DataJson.Points(DataJson.Get(d, "outer"), dimension), true);

            foreach (JsonElement hole in DataJson.List(d, "holes"))
            {
                Chain(item, DataJson.Points(hole, dimension), true);
            }
        }

        private void Solid(int item, JsonElement d)
        {
            foreach (JsonElement face in DataJson.List(d, "faces"))
            {
                Face(item, face, 3);
            }

            if (DataJson.TryGet(d, "openings", out JsonElement openings) && openings.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement opening in openings.EnumerateArray())
                {
                    Solid(item, opening);
                }
            }
        }

        /// <summary>
        /// An edge by its two ends, the same whichever way round they are given.
        /// </summary>
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly double _ax;
            private readonly double _ay;
            private readonly double _az;
            private readonly double _bx;
            private readonly double _by;
            private readonly double _bz;

            internal EdgeKey(Vec3 a, Vec3 b)
            {
                bool swap = a.X != b.X ? a.X > b.X : a.Y != b.Y ? a.Y > b.Y : a.Z > b.Z;
                Vec3 first = swap ? b : a;
                Vec3 second = swap ? a : b;
                _ax = first.X + 0.0;
                _ay = first.Y + 0.0;
                _az = first.Z + 0.0;
                _bx = second.X + 0.0;
                _by = second.Y + 0.0;
                _bz = second.Z + 0.0;
            }

            public bool Equals(EdgeKey other)
                => _ax.Equals(other._ax) && _ay.Equals(other._ay) && _az.Equals(other._az) && _bx.Equals(other._bx) && _by.Equals(other._by) && _bz.Equals(other._bz);

            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _ax.GetHashCode();
                    hash = hash * 397 ^ _ay.GetHashCode();
                    hash = hash * 397 ^ _az.GetHashCode();
                    hash = hash * 397 ^ _bx.GetHashCode();
                    hash = hash * 397 ^ _by.GetHashCode();
                    return hash * 397 ^ _bz.GetHashCode();
                }
            }
        }

        private void Box(int item, Vec3[] c)
        {
            Chain(item, new[] { c[0], c[1], c[2], c[3] }, true);
            Chain(item, new[] { c[4], c[5], c[6], c[7] }, true);

            for (int i = 0; i < 4; i++)
            {
                Segment(item, c[i], c[i + 4]);
            }
        }
    }
}
