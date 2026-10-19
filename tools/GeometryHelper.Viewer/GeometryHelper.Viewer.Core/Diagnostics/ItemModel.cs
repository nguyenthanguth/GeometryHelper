using System;
using System.Collections.Generic;
using System.Text.Json;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// One loop of an item, read from the numbers in its data: a polygon, a chain, a triangle, a loop of a face or of a body, a face of a mesh.
    /// </summary>
    internal sealed class LoopData
    {
        private SceneCurve[] _curves;

        /// <summary>
        /// The kind of item it was read from.
        /// </summary>
        internal string Kind;

        /// <summary>
        /// What loop it is, for a message: "polygon", "face 3 outer", "face 3 hole 1".
        /// </summary>
        internal string Role;

        /// <summary>
        /// The face it belongs to in its body or mesh, or -1.
        /// </summary>
        internal int Face = -1;

        /// <summary>
        /// 0 for an outer loop, the number of the hole otherwise.
        /// </summary>
        internal int Ring;

        internal Vec3[] Points;

        /// <summary>
        /// The bulge of the edge leaving each vertex; nought for a straight one.
        /// </summary>
        internal double[] Bulges;

        /// <summary>
        /// The normal each bulge is read about, for a loop in space with arcs; null in the plane and for straight loops.
        /// </summary>
        internal Vec3[] Normals;

        internal bool Closed = true;

        internal int Dimension = 3;

        /// <summary>
        /// The index of each corner in the vertices of its body or mesh; null where the loop is on its own.
        /// </summary>
        internal int[] Ids;

        /// <summary>
        /// The body or mesh it is a loop of, or null.
        /// </summary>
        internal BodyUnit Unit;

        /// <summary>
        /// How many places the loop crosses or touches itself, or -1 until looked for.
        /// </summary>
        internal int Crossings = -1;

        internal bool HasArcs
        {
            get
            {
                foreach (double b in Bulges)
                {
                    if (b != 0.0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal int EdgeCount => Closed ? Points.Length : Math.Max(0, Points.Length - 1);

        /// <summary>
        /// Gets the exact curve of each edge, as the picking and the snaps read them; null where an edge has no length.
        /// </summary>
        internal SceneCurve[] Curves(int itemIndex)
        {
            if (_curves != null)
            {
                return _curves;
            }

            int edges = EdgeCount;
            var curves = new SceneCurve[edges];

            for (int k = 0; k < edges; k++)
            {
                Vec3 from = Points[k];
                Vec3 to = Points[(k + 1) % Points.Length];

                if (!from.IsFinite || !to.IsFinite)
                {
                    continue;
                }

                double bulge = k < Bulges.Length ? Bulges[k] : 0.0;
                Vec3 normal = Dimension == 2 ? new Vec3(0.0, 0.0, 1.0) : Normals != null && k < Normals.Length ? Normals[k].Normalized() : default(Vec3);
                curves[k] = bulge == 0.0 ? SceneCurve.Segment(itemIndex, from, to) : SceneCurve.Edge(itemIndex, from, to, bulge, normal);

                if (curves[k] == null || (bulge == 0.0 && from.Equals(to)))
                {
                    curves[k] = null;
                }
            }

            _curves = curves;
            return curves;
        }
    }

    /// <summary>
    /// A set of faces that is meant to bound something, or to tile something: a body, an opening of a body, a mesh.
    /// </summary>
    internal sealed class BodyUnit
    {
        internal string Label;

        /// <summary>
        /// Gets whether it is a body, which has to be closed, and not a mesh, whose boundary is its outline.
        /// </summary>
        internal bool IsSolid;

        /// <summary>
        /// -1 for the body itself, the number of the opening otherwise.
        /// </summary>
        internal int Opening = -1;

        internal Vec3[] Vertices;

        /// <summary>
        /// The faces, each with its loops: the outer one first, then the holes.
        /// </summary>
        internal readonly List<List<LoopData>> Faces = new List<List<LoopData>>();

        /// <summary>
        /// What the library said when asked to validate the body, or null where it was not asked or refused.
        /// </summary>
        internal SolidValidation3 Validation;

        /// <summary>
        /// The library body this is, for the calls that need it.
        /// </summary>
        internal GeoSolid3 Solid;

        /// <summary>
        /// Whether the library's reading of thin faces stands for the viewer's own on this body.
        /// </summary>
        internal bool LibrarySlivers;

        /// <summary>
        /// Gets the number of coordinates of its corners: 2 for a face or a mesh in the plane, 3 otherwise.
        /// </summary>
        internal int Dimension => Faces.Count > 0 && Faces[0].Count > 0 ? Faces[0][0].Dimension : 3;
    }

    /// <summary>
    /// What a diagnostic reads of one item: its loops, and the sets of faces among them.
    /// </summary>
    internal sealed class ItemModel
    {
        internal int Index;
        internal SceneItem Item;
        internal string Name;

        /// <summary>
        /// Every loop, including those of the faces of units.
        /// </summary>
        internal readonly List<LoopData> Loops = new List<LoopData>();

        internal readonly List<BodyUnit> Units = new List<BodyUnit>();

        /// <summary>
        /// Whether the item is a mesh, whose faces are not looked at for crossings or winding.
        /// </summary>
        internal bool HasMesh;

        internal static ItemModel Read(SceneItem item, int index)
        {
            var model = new ItemModel { Index = index, Item = item, Name = item.Name ?? item.Kind };
            JsonElement d = item.Data;

            switch (item.Kind)
            {
                case "GeoPolygon2": model.Chain(item.Kind, "polygon", DataJson.Points(d, "points", 2), null, null, true, 2); break;
                case "GeoPolygon3": model.Chain(item.Kind, "polygon", DataJson.Points(d, "points", 3), null, null, true, 3); break;
                case "GeoPolyline2": model.Chain(item.Kind, "chain", DataJson.Points(d, "points", 2), null, null, false, 2); break;
                case "GeoPolyline3": model.Chain(item.Kind, "chain", DataJson.Points(d, "points", 3), null, null, false, 3); break;

                case "GeoPolygonArc2":
                case "GeoPolylineArc2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    model.Chain(item.Kind, item.Kind == "GeoPolygonArc2" ? "polygon" : "chain", points, DataJson.Bulges(d, points.Length), null, item.Kind == "GeoPolygonArc2", 2);
                    break;
                }

                case "GeoPolygonArc3":
                case "GeoPolylineArc3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    Vec3[] normals = DataJson.TryGet(d, "normals", out JsonElement list) ? DataJson.Points(list, 3) : null;
                    model.Chain(item.Kind, item.Kind == "GeoPolygonArc3" ? "polygon" : "chain", points, DataJson.Bulges(d, points.Length), normals, item.Kind == "GeoPolygonArc3", 3);
                    break;
                }

                case "GeoTriangle2": model.Chain(item.Kind, "triangle", DataJson.Points(d, "points", 2), null, null, true, 2); break;
                case "GeoTriangle3": model.Chain(item.Kind, "triangle", DataJson.Points(d, "points", 3), null, null, true, 3); break;

                case "GeoRectangle2":
                {
                    Vec3 center = DataJson.Point(d, "center", 2);
                    double halfWidth = DataJson.Number(d, "width") * 0.5;
                    double halfHeight = DataJson.Number(d, "height") * 0.5;
                    double angle = DataJson.Number(d, "angle");
                    double cos = Math.Cos(angle);
                    double sin = Math.Sin(angle);

                    Vec3 Corner(double x, double y) => new Vec3(center.X + x * cos - y * sin, center.Y + x * sin + y * cos, 0.0);

                    model.Chain(item.Kind, "rectangle", new[] { Corner(-halfWidth, -halfHeight), Corner(halfWidth, -halfHeight), Corner(halfWidth, halfHeight), Corner(-halfWidth, halfHeight) }, null, null, true, 2);
                    break;
                }

                case "GeoFace2":
                case "GeoFace3":
                {
                    int dimension = item.Kind == "GeoFace2" ? 2 : 3;
                    var unit = new BodyUnit { Label = "face", IsSolid = false };
                    model.AddFace(unit, item.Kind, d, dimension, 0, string.Empty);
                    model.Finish(unit);
                    break;
                }

                case "GeoSolid3":
                    model.Solid(item.Kind, d, -1, string.Empty);
                    break;

                case "GeoMesh2":
                case "GeoMesh3":
                    model.Mesh(item.Kind, d, item.Kind == "GeoMesh2" ? 2 : 3);
                    break;
            }

            return model;
        }

        private void Chain(string kind, string role, Vec3[] points, double[] bulges, Vec3[] normals, bool closed, int dimension)
        {
            Loops.Add(new LoopData
            {
                Kind = kind,
                Role = role,
                Points = points,
                Bulges = bulges ?? new double[points.Length],
                Normals = normals,
                Closed = closed,
                Dimension = dimension,
            });
        }

        private void Solid(string kind, JsonElement d, int opening, string prefix)
        {
            var unit = new BodyUnit { Label = opening < 0 ? "body" : "opening " + (opening + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), IsSolid = true, Opening = opening };
            int face = 0;

            foreach (JsonElement f in DataJson.List(d, "faces"))
            {
                AddFace(unit, kind, f, 3, face++, prefix + unit.Label + " ");
            }

            Finish(unit);

            // The openings are bodies of their own, and are checked by asking them.
            if (opening < 0 && DataJson.TryGet(d, "openings", out JsonElement openings) && openings.ValueKind == JsonValueKind.Array)
            {
                int k = 0;

                foreach (JsonElement o in openings.EnumerateArray())
                {
                    Solid(kind, o, k++, prefix);
                }
            }
        }

        private void AddFace(BodyUnit unit, string kind, JsonElement f, int dimension, int face, string prefix)
        {
            var loops = new List<LoopData>();
            var outer = new LoopData { Kind = kind, Role = prefix + "face " + (face + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " outer", Face = face, Ring = 0, Points = DataJson.Points(DataJson.Get(f, "outer"), dimension), Dimension = dimension };
            outer.Bulges = new double[outer.Points.Length];
            loops.Add(outer);
            int ring = 1;

            foreach (JsonElement hole in DataJson.List(f, "holes"))
            {
                var loop = new LoopData { Kind = kind, Role = prefix + "face " + (face + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " hole " + ring.ToString(System.Globalization.CultureInfo.InvariantCulture), Face = face, Ring = ring, Points = DataJson.Points(hole, dimension), Dimension = dimension };
                loop.Bulges = new double[loop.Points.Length];
                loops.Add(loop);
                ring++;
            }

            unit.Faces.Add(loops);
        }

        /// <summary>
        /// Numbers the corners of a unit's loops one after another, and files the loops.
        /// </summary>
        private void Finish(BodyUnit unit)
        {
            int total = 0;

            foreach (List<LoopData> face in unit.Faces)
            {
                foreach (LoopData loop in face)
                {
                    total += loop.Points.Length;
                }
            }

            var vertices = new Vec3[total];
            int next = 0;

            foreach (List<LoopData> face in unit.Faces)
            {
                foreach (LoopData loop in face)
                {
                    loop.Ids = new int[loop.Points.Length];
                    loop.Unit = unit;

                    for (int k = 0; k < loop.Points.Length; k++)
                    {
                        loop.Ids[k] = next;
                        vertices[next++] = loop.Points[k];
                    }

                    Loops.Add(loop);
                }
            }

            unit.Vertices = vertices;
            Units.Add(unit);
        }

        private void Mesh(string kind, JsonElement d, int dimension)
        {
            var unit = new BodyUnit { Label = "mesh", IsSolid = false, Vertices = DataJson.Points(d, "points", dimension) };
            int face = 0;

            foreach (int[] indexes in GeometryDecoder.Faces(d))
            {
                var points = new Vec3[indexes.Length];

                for (int k = 0; k < indexes.Length; k++)
                {
                    if (indexes[k] < 0 || indexes[k] >= unit.Vertices.Length)
                    {
                        throw new FormatException("A mesh face names a vertex that is not there.");
                    }

                    points[k] = unit.Vertices[indexes[k]];
                }

                var loop = new LoopData { Kind = kind, Role = "face " + (face + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Face = face, Points = points, Bulges = new double[points.Length], Dimension = dimension, Ids = indexes, Unit = unit };
                unit.Faces.Add(new List<LoopData> { loop });
                Loops.Add(loop);
                face++;
            }

            HasMesh = true;
            Units.Add(unit);
        }
    }
}
