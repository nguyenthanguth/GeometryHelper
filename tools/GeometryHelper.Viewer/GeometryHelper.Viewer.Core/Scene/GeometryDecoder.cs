using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

            // Each kind is built by a method of its own, so that a type is looked for only when an item of it is decoded.
            switch (item.Kind)
            {
                case "GeoPoint2": return Decode_GeoPoint2(d);
                case "GeoPoint3": return Decode_GeoPoint3(d);
                case "GeoLine2": return Decode_GeoLine2(d);
                case "GeoLine3": return Decode_GeoLine3(d);
                case "GeoRay2": return Decode_GeoRay2(d);
                case "GeoRay3": return Decode_GeoRay3(d);
                case "GeoXLine2": return Decode_GeoXLine2(d);
                case "GeoXLine3": return Decode_GeoXLine3(d);
                case "GeoPolyline2": return Decode_GeoPolyline2(d);
                case "GeoPolygon2": return Decode_GeoPolygon2(d);
                case "GeoPolyline3": return Decode_GeoPolyline3(d);
                case "GeoPolygon3": return Decode_GeoPolygon3(d);
                case "GeoPolylineArc2": return Decode_GeoPolylineArc2(d);
                case "GeoPolygonArc2": return Decode_GeoPolygonArc2(d);
                case "GeoPolylineArc3": return Decode_GeoPolylineArc3(d);
                case "GeoPolygonArc3": return Decode_GeoPolygonArc3(d);
                case "GeoArc2": return Decode_GeoArc2(d);
                case "GeoArc3": return Decode_GeoArc3(d);
                case "GeoCircle2": return Decode_GeoCircle2(d);
                case "GeoCircle3": return Decode_GeoCircle3(d);
                case "GeoEllipse2": return Decode_GeoEllipse2(d);
                case "GeoEllipse3": return Decode_GeoEllipse3(d);
                case "GeoSpline2": return Decode_GeoSpline2(d);
                case "GeoSpline3": return Decode_GeoSpline3(d);
                case "GeoRectangle2": return Decode_GeoRectangle2(d);
                case "GeoTriangle2": return Decode_GeoTriangle2(d);
                case "GeoTriangle3": return Decode_GeoTriangle3(d);
                case "GeoFace2": return Decode_GeoFace2(d);
                case "GeoFace3": return Decode_GeoFace3(d);
                case "GeoSolid3": return Decode_GeoSolid3(d);
                case "GeoMesh2": return Decode_GeoMesh2(d);
                case "GeoMesh3": return Decode_GeoMesh3(d);
                case "GeoAabb3": return Decode_GeoAabb3(d);
                case "GeoObb3": return Decode_GeoObb3(d);
                case "GeoSphere3": return Decode_GeoSphere3(d);
                case "GeoPlane3": return Decode_GeoPlane3(d);
                case SceneKinds.Text:
                    throw new NotSupportedException("A label has no library shape.");

                default:
                    throw new NotSupportedException("The viewer does not know the kind '" + item.Kind + "'.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPoint2(JsonElement d)
        {
            Vec3 p = DataJson.Point(d, 2);
            return new GeoPoint2(p.X, p.Y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPoint3(JsonElement d)
        {
            return P3(DataJson.Point(d, 3));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoLine2(JsonElement d)
        {
            return new GeoLine2(P2(DataJson.Point(d, "a", 2)), P2(DataJson.Point(d, "b", 2)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoLine3(JsonElement d)
        {
            return new GeoLine3(P3(DataJson.Point(d, "a", 3)), P3(DataJson.Point(d, "b", 3)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoRay2(JsonElement d)
        {
            return new GeoRay2(P2(DataJson.Point(d, "a", 2)), V2(DataJson.Point(d, "b", 2)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoRay3(JsonElement d)
        {
            return new GeoRay3(P3(DataJson.Point(d, "a", 3)), V3(DataJson.Point(d, "b", 3)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoXLine2(JsonElement d)
        {
            return new GeoXLine2(P2(DataJson.Point(d, "a", 2)), V2(DataJson.Point(d, "b", 2)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoXLine3(JsonElement d)
        {
            return new GeoXLine3(P3(DataJson.Point(d, "a", 3)), V3(DataJson.Point(d, "b", 3)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolyline2(JsonElement d)
        {
            return new GeoPolyline2(DataJson.Points(d, "points", 2).Select(P2));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolygon2(JsonElement d)
        {
            return new GeoPolygon2(DataJson.Points(d, "points", 2).Select(P2));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolyline3(JsonElement d)
        {
            return new GeoPolyline3(DataJson.Points(d, "points", 3).Select(P3));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolygon3(JsonElement d)
        {
            return new GeoPolygon3(DataJson.Points(d, "points", 3).Select(P3));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolylineArc2(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 2);
            return new GeoPolylineArc2(points.Select(P2), DataJson.Bulges(d, points.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolygonArc2(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 2);
            return new GeoPolygonArc2(points.Select(P2), DataJson.Bulges(d, points.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolylineArc3(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 3);
            return new GeoPolylineArc3(points.Select(P3), DataJson.Bulges(d, points.Length), Normals(d, points.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPolygonArc3(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 3);
            return new GeoPolygonArc3(points.Select(P3), DataJson.Bulges(d, points.Length), Normals(d, points.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoArc2(JsonElement d)
        {
            return Arc2(d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoArc3(JsonElement d)
        {
            return Arc3(d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoCircle2(JsonElement d)
        {
            return new GeoCircle2(P2(DataJson.Point(d, "center", 2)), DataJson.Number(d, "radius"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoCircle3(JsonElement d)
        {
            return new GeoCircle3(P3(DataJson.Point(d, "center", 3)), V3(DataJson.Point(d, "normal", 3)), DataJson.Number(d, "radius"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoEllipse2(JsonElement d)
        {
            return new GeoEllipse2(
                P2(DataJson.Point(d, "center", 2)),
                V2(DataJson.Point(d, "majorAxis", 2)),
                DataJson.Number(d, "majorRadius"),
                DataJson.Number(d, "minorRadius"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoEllipse3(JsonElement d)
        {
            return new GeoEllipse3(
                P3(DataJson.Point(d, "center", 3)),
                V3(DataJson.Point(d, "normal", 3)),
                V3(DataJson.Point(d, "majorAxis", 3)),
                DataJson.Number(d, "majorRadius"),
                DataJson.Number(d, "minorRadius"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoSpline2(JsonElement d)
        {
            return new GeoSpline2(DataJson.Degree(d), DataJson.Points(d, "points", 2).Select(P2), DataJson.Numbers(DataJson.Get(d, "knots")), DataJson.Weights(d));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoSpline3(JsonElement d)
        {
            return new GeoSpline3(DataJson.Degree(d), DataJson.Points(d, "points", 3).Select(P3), DataJson.Numbers(DataJson.Get(d, "knots")), DataJson.Weights(d));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoRectangle2(JsonElement d)
        {
            return new GeoRectangle2(
                P2(DataJson.Point(d, "center", 2)),
                DataJson.Number(d, "width"),
                DataJson.Number(d, "height"),
                DataJson.Number(d, "angle"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoTriangle2(JsonElement d)
        {
            Vec3[] c = Corners(d, 2);
            return new GeoTriangle2(P2(c[0]), P2(c[1]), P2(c[2]));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoTriangle3(JsonElement d)
        {
            Vec3[] c = Corners(d, 3);
            return new GeoTriangle3(P3(c[0]), P3(c[1]), P3(c[2]));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoFace2(JsonElement d)
        {
            return new GeoFace2(
                new GeoPolygon2(DataJson.Points(DataJson.Get(d, "outer"), 2).Select(P2)),
                DataJson.List(d, "holes").Select(hole => new GeoPolygon2(DataJson.Points(hole, 2).Select(P2))).ToList());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoFace3(JsonElement d)
        {
            return Face3(d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoSolid3(JsonElement d)
        {
            return Solid3(d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoMesh2(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 2);
            return Faces(d).Select(face => new GeoPolygon2(face.Select(i => P2(points[i])))).ToArray();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoMesh3(JsonElement d)
        {
            Vec3[] points = DataJson.Points(d, "points", 3);
            return Faces(d).Select(face => new GeoPolygon3(face.Select(i => P3(points[i])))).ToArray();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoAabb3(JsonElement d)
        {
            if (DataJson.TryGet(d, "empty", out JsonElement empty) && empty.ValueKind == JsonValueKind.True)
            {
                return GeoAabb3.Empty;
            }
            return new GeoAabb3(P3(DataJson.Point(d, "min", 3)), P3(DataJson.Point(d, "max", 3)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoObb3(JsonElement d)
        {
            return new GeoObb3(
                P3(DataJson.Point(d, "center", 3)),
                DataJson.Number(d, "sizeX"),
                DataJson.Number(d, "sizeY"),
                DataJson.Number(d, "sizeZ"),
                V3(DataJson.Point(d, "axisX", 3)),
                V3(DataJson.Point(d, "axisY", 3)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoSphere3(JsonElement d)
        {
            return new GeoSphere3(P3(DataJson.Point(d, "center", 3)), DataJson.Number(d, "radius"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Decode_GeoPlane3(JsonElement d)
        {
            return new GeoPlane3(P3(DataJson.Point(d, "origin", 3)), V3(DataJson.Point(d, "normal", 3)));
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
