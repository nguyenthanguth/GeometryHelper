using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Rebuilds a library shape from the data of an item, with the library's own constructors.
    /// </summary>
    internal static class GeometryDecoder
    {
        internal static object Decode(SceneItem item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            JsonElement d = item.Data;

            switch (item.Kind)
            {
                case "GeoPoint2": { Vec3 p = DataJson.Point(d, 2); return new GeoPoint2(p.X, p.Y); }
                case "GeoPoint3": return P3(DataJson.Point(d, 3));
                case "GeoLine2": return new GeoLine2(P2(DataJson.Point(d, "a", 2)), P2(DataJson.Point(d, "b", 2)));
                case "GeoLine3": return new GeoLine3(P3(DataJson.Point(d, "a", 3)), P3(DataJson.Point(d, "b", 3)));
                case "GeoRay2": return new GeoRay2(P2(DataJson.Point(d, "a", 2)), V2(DataJson.Point(d, "b", 2)));
                case "GeoRay3": return new GeoRay3(P3(DataJson.Point(d, "a", 3)), V3(DataJson.Point(d, "b", 3)));
                case "GeoPolyline2": return new GeoPolyline2(DataJson.Points(d, "points", 2).Select(P2));
                case "GeoPolygon2": return new GeoPolygon2(DataJson.Points(d, "points", 2).Select(P2));
                case "GeoPolyline3": return new GeoPolyline3(DataJson.Points(d, "points", 3).Select(P3));
                case "GeoPolygon3": return new GeoPolygon3(DataJson.Points(d, "points", 3).Select(P3));

                case "GeoPolylineArc2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    return new GeoPolylineArc2(points.Select(P2), DataJson.Bulges(d, points.Length));
                }

                case "GeoPolygonArc2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    return new GeoPolygonArc2(points.Select(P2), DataJson.Bulges(d, points.Length));
                }

                case "GeoPolylineArc3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    return new GeoPolylineArc3(points.Select(P3), DataJson.Bulges(d, points.Length), Normals(d, points.Length));
                }

                case "GeoPolygonArc3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    return new GeoPolygonArc3(points.Select(P3), DataJson.Bulges(d, points.Length), Normals(d, points.Length));
                }

                case "GeoArc2":
                    return Arc2(d);

                case "GeoArc3":
                    return Arc3(d);

                case "GeoCircle2": return new GeoCircle2(P2(DataJson.Point(d, "center", 2)), DataJson.Number(d, "radius"));
                case "GeoCircle3": return new GeoCircle3(P3(DataJson.Point(d, "center", 3)), V3(DataJson.Point(d, "normal", 3)), DataJson.Number(d, "radius"));

                case "GeoEllipse2":
                    return new GeoEllipse2(
                        P2(DataJson.Point(d, "center", 2)),
                        V2(DataJson.Point(d, "majorAxis", 2)),
                        DataJson.Number(d, "majorRadius"),
                        DataJson.Number(d, "minorRadius"));

                case "GeoEllipse3":
                    return new GeoEllipse3(
                        P3(DataJson.Point(d, "center", 3)),
                        V3(DataJson.Point(d, "normal", 3)),
                        V3(DataJson.Point(d, "majorAxis", 3)),
                        DataJson.Number(d, "majorRadius"),
                        DataJson.Number(d, "minorRadius"));

                case "GeoRectangle2":
                    return new GeoRectangle2(
                        P2(DataJson.Point(d, "center", 2)),
                        DataJson.Number(d, "width"),
                        DataJson.Number(d, "height"),
                        DataJson.Number(d, "angle"));

                case "GeoTriangle2":
                {
                    Vec3[] c = Corners(d, 2);
                    return new GeoTriangle2(P2(c[0]), P2(c[1]), P2(c[2]));
                }

                case "GeoTriangle3":
                {
                    Vec3[] c = Corners(d, 3);
                    return new GeoTriangle3(P3(c[0]), P3(c[1]), P3(c[2]));
                }

                case "GeoFace2":
                    return new GeoFace2(
                        new GeoPolygon2(DataJson.Points(DataJson.Get(d, "outer"), 2).Select(P2)),
                        DataJson.List(d, "holes").Select(hole => new GeoPolygon2(DataJson.Points(hole, 2).Select(P2))).ToList());

                case "GeoFace3":
                    return Face3(d);

                case "GeoSolid3":
                    return Solid3(d);

                case "GeoMesh2":
                {
                    Vec3[] points = DataJson.Points(d, "points", 2);
                    return Faces(d).Select(face => new GeoPolygon2(face.Select(i => P2(points[i])))).ToArray();
                }

                case "GeoMesh3":
                {
                    Vec3[] points = DataJson.Points(d, "points", 3);
                    return Faces(d).Select(face => new GeoPolygon3(face.Select(i => P3(points[i])))).ToArray();
                }

                case "GeoAabb3":
                {
                    if (DataJson.TryGet(d, "empty", out JsonElement empty) && empty.ValueKind == JsonValueKind.True)
                    {
                        return GeoAabb3.Empty;
                    }

                    return new GeoAabb3(P3(DataJson.Point(d, "min", 3)), P3(DataJson.Point(d, "max", 3)));
                }

                case "GeoObb3":
                    return new GeoObb3(
                        P3(DataJson.Point(d, "center", 3)),
                        DataJson.Number(d, "sizeX"),
                        DataJson.Number(d, "sizeY"),
                        DataJson.Number(d, "sizeZ"),
                        V3(DataJson.Point(d, "axisX", 3)),
                        V3(DataJson.Point(d, "axisY", 3)));

                case "GeoPlane3":
                    return new GeoPlane3(P3(DataJson.Point(d, "origin", 3)), V3(DataJson.Point(d, "normal", 3)));

                case SceneKinds.Text:
                    throw new NotSupportedException("A label has no library shape.");

                default:
                    throw new NotSupportedException("The viewer does not know the kind '" + item.Kind + "'.");
            }
        }

        private static GeoPoint2 P2(Vec3 v) => new GeoPoint2(v.X, v.Y);

        private static GeoPoint3 P3(Vec3 v) => new GeoPoint3(v.X, v.Y, v.Z);

        private static GeoVector2 V2(Vec3 v) => new GeoVector2(v.X, v.Y);

        private static GeoVector3 V3(Vec3 v) => new GeoVector3(v.X, v.Y, v.Z);

        private static Vec3[] Corners(JsonElement d, int dimension)
        {
            Vec3[] corners = DataJson.Points(d, "points", dimension);

            if (corners.Length != 3)
            {
                throw new FormatException("A triangle has three corners.");
            }

            return corners;
        }

        private static IReadOnlyList<GeoVector3> Normals(JsonElement d, int count)
        {
            var normals = new GeoVector3[count];

            if (DataJson.TryGet(d, "normals", out JsonElement list))
            {
                Vec3[] given = DataJson.Points(list, 3);

                for (int i = 0; i < normals.Length && i < given.Length; i++)
                {
                    normals[i] = V3(given[i]);
                }
            }

            return normals;
        }

        private static GeoArc2 Arc2(JsonElement d)
        {
            double sweep = DataJson.Number(d, "sweep");
            double start = DataJson.Number(d, "start");

            return new GeoArc2(P2(DataJson.Point(d, "center", 2)), DataJson.Number(d, "radius"), start, start + sweep, sweep < 0.0);
        }

        private static GeoArc3 Arc3(JsonElement d)
        {
            double sweep = DataJson.Number(d, "sweep");
            double start = DataJson.Number(d, "start");

            return new GeoArc3(
                P3(DataJson.Point(d, "center", 3)),
                V3(DataJson.Point(d, "normal", 3)),
                DataJson.Number(d, "radius"),
                start,
                start + sweep,
                sweep < 0.0);
        }

        internal static GeoFace3 DecodeFace3(JsonElement d) => Face3(d);

        private static GeoFace3 Face3(JsonElement d)
        {
            GeoPolygon3 outer = new GeoPolygon3(DataJson.Points(DataJson.Get(d, "outer"), 3).Select(P3));
            List<GeoPolygon3> holes = DataJson.List(d, "holes").Select(hole => new GeoPolygon3(DataJson.Points(hole, 3).Select(P3))).ToList();

            return new GeoFace3(outer, holes);
        }

        private static GeoSolid3 Solid3(JsonElement d)
        {
            List<GeoFace3> faces = DataJson.List(d, "faces").Select(Face3).ToList();
            List<GeoSolid3> openings = DataJson.TryGet(d, "openings", out JsonElement list)
                ? list.EnumerateArray().Select(Solid3).ToList()
                : new List<GeoSolid3>();

            return new GeoSolid3(faces, openings);
        }

        internal static IEnumerable<int[]> Faces(JsonElement d)
        {
            foreach (JsonElement face in DataJson.List(d, "faces"))
            {
                if (face.ValueKind != JsonValueKind.Array)
                {
                    throw new FormatException("A mesh face is a list of vertex indexes.");
                }

                yield return face.EnumerateArray().Select(i => i.GetInt32()).ToArray();
            }
        }
    }
}
