using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Turns a library shape into the numbers that rebuild it, as <see cref="SceneKinds"/> sets them out. Each shape is
    /// written as the library holds it, not as it was built, so a rebuilt shape starts from the same doubles.
    /// </summary>
    internal static class GeometryEncoder
    {
        /// <summary>
        /// Gets the kind of a shape the viewer draws, or null for any other object.
        /// </summary>
        internal static string KindOf(object geometry)
        {
            // Only a type of the library is a shape: a class of a script that happens to be called GeoPoint2 is not.
            if (geometry == null || !IsLibraryType(geometry))
            {
                return null;
            }

            string name = geometry.GetType().Name;
            return SceneKinds.IsKnown(name) && name != SceneKinds.Text ? name : null;
        }

        /// <summary>
        /// Writes the data of a shape; the shape must be one <see cref="KindOf"/> names.
        /// </summary>
        internal static JsonElement Encode(object geometry)
        {
            using (var w = new DataWriter())
            {
                Write(w, geometry);
                return w.Finish();
            }
        }

        private static bool IsLibraryType(object geometry) => geometry.GetType().Assembly == typeof(Tolerance).Assembly;

        private static void Write(DataWriter w, object geometry)
        {
            if (!IsLibraryType(geometry))
            {
                throw new ArgumentException("The viewer cannot write a " + geometry.GetType().FullName + ", which is not a shape of the library.", nameof(geometry));
            }

            // The kind is read from the name of the type and each kind is written by a method of its own, so that a type is looked for only when a shape of it is
            // written: a build of the library that lacks a type (GeoRay2 is new in 12.2) still runs the scripts that do not use it.
            switch (geometry.GetType().Name)
            {
                case "GeoPoint2":
                    WriteGeoPoint2(w, geometry);
                    break;

                case "GeoPoint3":
                    WriteGeoPoint3(w, geometry);
                    break;

                case "GeoLine2":
                    WriteGeoLine2(w, geometry);
                    break;

                case "GeoLine3":
                    WriteGeoLine3(w, geometry);
                    break;

                case "GeoRay2":
                    WriteGeoRay2(w, geometry);
                    break;

                case "GeoRay3":
                    WriteGeoRay3(w, geometry);
                    break;

                case "GeoXLine2":
                    WriteGeoXLine2(w, geometry);
                    break;

                case "GeoXLine3":
                    WriteGeoXLine3(w, geometry);
                    break;

                case "GeoPolyline2":
                    WriteGeoPolyline2(w, geometry);
                    break;

                case "GeoPolygon2":
                    WriteGeoPolygon2(w, geometry);
                    break;

                case "GeoPolyline3":
                    WriteGeoPolyline3(w, geometry);
                    break;

                case "GeoPolygon3":
                    WriteGeoPolygon3(w, geometry);
                    break;

                case "GeoPolylineArc2":
                    WriteGeoPolylineArc2(w, geometry);
                    break;

                case "GeoPolygonArc2":
                    WriteGeoPolygonArc2(w, geometry);
                    break;

                case "GeoPolylineArc3":
                    WriteGeoPolylineArc3(w, geometry);
                    break;

                case "GeoPolygonArc3":
                    WriteGeoPolygonArc3(w, geometry);
                    break;

                case "GeoArc2":
                    WriteGeoArc2(w, geometry);
                    break;

                case "GeoArc3":
                    WriteGeoArc3(w, geometry);
                    break;

                case "GeoCircle2":
                    WriteGeoCircle2(w, geometry);
                    break;

                case "GeoCircle3":
                    WriteGeoCircle3(w, geometry);
                    break;

                case "GeoEllipse2":
                    WriteGeoEllipse2(w, geometry);
                    break;

                case "GeoEllipse3":
                    WriteGeoEllipse3(w, geometry);
                    break;

                case "GeoRectangle2":
                    WriteGeoRectangle2(w, geometry);
                    break;

                case "GeoTriangle2":
                    WriteGeoTriangle2(w, geometry);
                    break;

                case "GeoTriangle3":
                    WriteGeoTriangle3(w, geometry);
                    break;

                case "GeoFace2":
                    WriteGeoFace2(w, geometry);
                    break;

                case "GeoFace3":
                    WriteGeoFace3(w, geometry);
                    break;

                case "GeoSolid3":
                    WriteGeoSolid3(w, geometry);
                    break;

                case "GeoMesh2":
                    WriteGeoMesh2(w, geometry);
                    break;

                case "GeoMesh3":
                    WriteGeoMesh3(w, geometry);
                    break;

                case "GeoAabb3":
                    WriteGeoAabb3(w, geometry);
                    break;

                case "GeoObb3":
                    WriteGeoObb3(w, geometry);
                    break;

                case "GeoPlane3":
                    WriteGeoPlane3(w, geometry);
                    break;

                default:
                    throw new ArgumentException("The viewer cannot write a " + geometry.GetType().FullName + ".", nameof(geometry));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPoint2(DataWriter w, object geometry)
        {
            GeoPoint2 p = (GeoPoint2)geometry;
            w.Point(p.X, p.Y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPoint3(DataWriter w, object geometry)
        {
            GeoPoint3 p = (GeoPoint3)geometry;
            w.Point(p.X, p.Y, p.Z);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoLine2(DataWriter w, object geometry)
        {
            GeoLine2 l = (GeoLine2)geometry;
            w.BeginObject();
            w.Field("a", l.StartPoint.X, l.StartPoint.Y);
            w.Field("b", l.EndPoint.X, l.EndPoint.Y);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoLine3(DataWriter w, object geometry)
        {
            GeoLine3 l = (GeoLine3)geometry;
            w.BeginObject();
            w.Field("a", l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z);
            w.Field("b", l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoRay2(DataWriter w, object geometry)
        {
            GeoRay2 r = (GeoRay2)geometry;
            w.BeginObject();
            w.Field("a", r.Origin.X, r.Origin.Y);
            w.Field("b", r.Direction.X, r.Direction.Y);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoRay3(DataWriter w, object geometry)
        {
            GeoRay3 r = (GeoRay3)geometry;
            w.BeginObject();
            w.Field("a", r.Origin.X, r.Origin.Y, r.Origin.Z);
            w.Field("b", r.Direction.X, r.Direction.Y, r.Direction.Z);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoXLine2(DataWriter w, object geometry)
        {
            GeoXLine2 x = (GeoXLine2)geometry;
            w.BeginObject();
            w.Field("a", x.Origin.X, x.Origin.Y);
            w.Field("b", x.Direction.X, x.Direction.Y);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoXLine3(DataWriter w, object geometry)
        {
            GeoXLine3 x = (GeoXLine3)geometry;
            w.BeginObject();
            w.Field("a", x.Origin.X, x.Origin.Y, x.Origin.Z);
            w.Field("b", x.Direction.X, x.Direction.Y, x.Direction.Z);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolyline2(DataWriter w, object geometry)
        {
            GeoPolyline2 p = (GeoPolyline2)geometry;
            WritePoints(w, p.Vertices);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolygon2(DataWriter w, object geometry)
        {
            GeoPolygon2 p = (GeoPolygon2)geometry;
            WritePoints(w, p.Vertices);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolyline3(DataWriter w, object geometry)
        {
            GeoPolyline3 p = (GeoPolyline3)geometry;
            WritePoints(w, p.Vertices);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolygon3(DataWriter w, object geometry)
        {
            GeoPolygon3 p = (GeoPolygon3)geometry;
            WritePoints(w, p.Vertices);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolylineArc2(DataWriter w, object geometry)
        {
            GeoPolylineArc2 p = (GeoPolylineArc2)geometry;
            WritePoints(w, p.Vertices, p.Bulges);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolygonArc2(DataWriter w, object geometry)
        {
            GeoPolygonArc2 p = (GeoPolygonArc2)geometry;
            WritePoints(w, p.Vertices, p.Bulges);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolylineArc3(DataWriter w, object geometry)
        {
            GeoPolylineArc3 p = (GeoPolylineArc3)geometry;
            WritePoints(w, p.Vertices, p.Bulges, p.Normals);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPolygonArc3(DataWriter w, object geometry)
        {
            GeoPolygonArc3 p = (GeoPolygonArc3)geometry;
            WritePoints(w, p.Vertices, p.Bulges, p.Normals);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoArc2(DataWriter w, object geometry)
        {
            GeoArc2 a = (GeoArc2)geometry;
            w.BeginObject();
            w.Field("center", a.Center.X, a.Center.Y);
            w.Field("radius", a.Radius);
            w.Field("start", a.StartAngle);
            w.Field("sweep", a.SweptAngle);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoArc3(DataWriter w, object geometry)
        {
            GeoArc3 a = (GeoArc3)geometry;
            w.BeginObject();
            w.Field("center", a.Center.X, a.Center.Y, a.Center.Z);
            w.Field("normal", a.Normal.X, a.Normal.Y, a.Normal.Z);
            w.Field("radius", a.Radius);
            w.Field("start", a.StartAngle);
            w.Field("sweep", a.SweptAngle);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoCircle2(DataWriter w, object geometry)
        {
            GeoCircle2 c = (GeoCircle2)geometry;
            w.BeginObject();
            w.Field("center", c.Center.X, c.Center.Y);
            w.Field("radius", c.Radius);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoCircle3(DataWriter w, object geometry)
        {
            GeoCircle3 c = (GeoCircle3)geometry;
            w.BeginObject();
            w.Field("center", c.Center.X, c.Center.Y, c.Center.Z);
            w.Field("normal", c.Normal.X, c.Normal.Y, c.Normal.Z);
            w.Field("radius", c.Radius);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoEllipse2(DataWriter w, object geometry)
        {
            GeoEllipse2 e = (GeoEllipse2)geometry;
            w.BeginObject();
            w.Field("center", e.Center.X, e.Center.Y);
            w.Field("majorAxis", e.MajorAxis.X, e.MajorAxis.Y);
            w.Field("majorRadius", e.MajorRadius);
            w.Field("minorRadius", e.MinorRadius);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoEllipse3(DataWriter w, object geometry)
        {
            GeoEllipse3 e = (GeoEllipse3)geometry;
            w.BeginObject();
            w.Field("center", e.Center.X, e.Center.Y, e.Center.Z);
            w.Field("normal", e.Normal.X, e.Normal.Y, e.Normal.Z);
            w.Field("majorAxis", e.MajorAxis.X, e.MajorAxis.Y, e.MajorAxis.Z);
            w.Field("majorRadius", e.MajorRadius);
            w.Field("minorRadius", e.MinorRadius);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoRectangle2(DataWriter w, object geometry)
        {
            GeoRectangle2 r = (GeoRectangle2)geometry;
            w.BeginObject();
            w.Field("center", r.Center.X, r.Center.Y);
            w.Field("width", r.Width);
            w.Field("height", r.Height);
            w.Field("angle", r.AngleRad);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoTriangle2(DataWriter w, object geometry)
        {
            GeoTriangle2 t = (GeoTriangle2)geometry;
            w.BeginObject();
            w.Name("points");
            w.BeginArray();
            w.Point(t.A.X, t.A.Y);
            w.Point(t.B.X, t.B.Y);
            w.Point(t.C.X, t.C.Y);
            w.EndArray();
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoTriangle3(DataWriter w, object geometry)
        {
            GeoTriangle3 t = (GeoTriangle3)geometry;
            w.BeginObject();
            w.Name("points");
            w.BeginArray();
            w.Point(t.A.X, t.A.Y, t.A.Z);
            w.Point(t.B.X, t.B.Y, t.B.Z);
            w.Point(t.C.X, t.C.Y, t.C.Z);
            w.EndArray();
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoFace2(DataWriter w, object geometry)
        {
            GeoFace2 f = (GeoFace2)geometry;
            w.BeginObject();
            w.Name("outer");
            WriteLoop(w, f.Boundary.Vertices);
            w.Name("holes");
            w.BeginArray();

            foreach (GeoPolygon2 hole in f.Holes)
            {
                WriteLoop(w, hole.Vertices);
            }

            w.EndArray();
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoFace3(DataWriter w, object geometry)
        {
            GeoFace3 f = (GeoFace3)geometry;
            WriteFace(w, f);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoSolid3(DataWriter w, object geometry)
        {
            GeoSolid3 s = (GeoSolid3)geometry;
            WriteSolid(w, s);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoMesh2(DataWriter w, object geometry)
        {
            GeoMesh2 m = (GeoMesh2)geometry;
            w.BeginObject();
            w.Name("kind");
            w.Text(m.Kind.ToString());
            w.Name("points");
            w.BeginArray();

            foreach (GeoPoint2 p in m.Vertices)
            {
                w.Point(p.X, p.Y);
            }

            w.EndArray();
            WriteFaces(w, m.FaceCount, m.GetFaceIndices);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoMesh3(DataWriter w, object geometry)
        {
            GeoMesh3 m = (GeoMesh3)geometry;
            w.BeginObject();
            w.Name("kind");
            w.Text(m.Kind.ToString());
            w.Name("points");
            w.BeginArray();

            foreach (GeoPoint3 p in m.Vertices)
            {
                w.Point(p.X, p.Y, p.Z);
            }

            w.EndArray();
            WriteFaces(w, m.FaceCount, m.GetFaceIndices);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoAabb3(DataWriter w, object geometry)
        {
            GeoAabb3 b = (GeoAabb3)geometry;
            w.BeginObject();

            if (b.IsEmpty)
            {
                w.Name("empty");
                w.Flag(true);
            }
            else
            {
                w.Field("min", b.Min.X, b.Min.Y, b.Min.Z);
                w.Field("max", b.Max.X, b.Max.Y, b.Max.Z);
            }

            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoObb3(DataWriter w, object geometry)
        {
            GeoObb3 b = (GeoObb3)geometry;
            w.BeginObject();
            w.Field("center", b.Center.X, b.Center.Y, b.Center.Z);
            w.Field("sizeX", b.SizeX);
            w.Field("sizeY", b.SizeY);
            w.Field("sizeZ", b.SizeZ);
            w.Field("axisX", b.AxisX.X, b.AxisX.Y, b.AxisX.Z);
            w.Field("axisY", b.AxisY.X, b.AxisY.Y, b.AxisY.Z);
            w.EndObject();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteGeoPlane3(DataWriter w, object geometry)
        {
            GeoPlane3 p = (GeoPlane3)geometry;
            w.BeginObject();
            w.Field("origin", p.Origin.X, p.Origin.Y, p.Origin.Z);
            w.Field("normal", p.Normal.X, p.Normal.Y, p.Normal.Z);
            w.EndObject();
        }

        /// <summary>
        /// Writes the data of a label.
        /// </summary>
        internal static JsonElement EncodeText(GeoPoint3 at, string text, double height)
        {
            using (var w = new DataWriter())
            {
                w.BeginObject();
                w.Field("at", at.X, at.Y, at.Z);
                w.Name("text");
                w.Text(text ?? string.Empty);
                w.Field("height", height);
                w.EndObject();
                return w.Finish();
            }
        }

        private static void WritePoints(DataWriter w, IReadOnlyList<GeoPoint2> points, IReadOnlyList<double> bulges = null)
        {
            w.BeginObject();
            w.Name("points");
            w.BeginArray();

            foreach (GeoPoint2 p in points)
            {
                w.Point(p.X, p.Y);
            }

            w.EndArray();

            if (bulges != null)
            {
                w.Numbers("bulges", bulges);
            }

            w.EndObject();
        }

        private static void WritePoints(DataWriter w, IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> bulges = null, IReadOnlyList<GeoVector3> normals = null)
        {
            w.BeginObject();
            w.Name("points");
            w.BeginArray();

            foreach (GeoPoint3 p in points)
            {
                w.Point(p.X, p.Y, p.Z);
            }

            w.EndArray();

            if (bulges != null)
            {
                w.Numbers("bulges", bulges);
            }

            if (normals != null)
            {
                w.Name("normals");
                w.BeginArray();

                foreach (GeoVector3 n in normals)
                {
                    w.Point(n.X, n.Y, n.Z);
                }

                w.EndArray();
            }

            w.EndObject();
        }

        private static void WriteLoop(DataWriter w, IReadOnlyList<GeoPoint2> points)
        {
            w.BeginArray();

            foreach (GeoPoint2 p in points)
            {
                w.Point(p.X, p.Y);
            }

            w.EndArray();
        }

        private static void WriteLoop(DataWriter w, IReadOnlyList<GeoPoint3> points)
        {
            w.BeginArray();

            foreach (GeoPoint3 p in points)
            {
                w.Point(p.X, p.Y, p.Z);
            }

            w.EndArray();
        }

        private static void WriteFace(DataWriter w, GeoFace3 face)
        {
            w.BeginObject();
            w.Name("outer");
            WriteLoop(w, face.Boundary.Vertices);
            w.Name("holes");
            w.BeginArray();

            foreach (GeoPolygon3 hole in face.Holes)
            {
                WriteLoop(w, hole.Vertices);
            }

            w.EndArray();
            w.EndObject();
        }

        private static void WriteSolid(DataWriter w, GeoSolid3 solid)
        {
            w.BeginObject();
            w.Name("faces");
            w.BeginArray();

            foreach (GeoFace3 face in solid.Faces)
            {
                WriteFace(w, face);
            }

            w.EndArray();
            w.Name("openings");
            w.BeginArray();

            foreach (GeoSolid3 opening in solid.Openings)
            {
                WriteSolid(w, opening);
            }

            w.EndArray();
            w.EndObject();
        }

        private static void WriteFaces(DataWriter w, int count, Func<int, int[]> indexesOf)
        {
            w.Name("faces");
            w.BeginArray();

            for (int i = 0; i < count; i++)
            {
                w.BeginArray();

                foreach (int index in indexesOf(i))
                {
                    w.Integer(index);
                }

                w.EndArray();
            }

            w.EndArray();
        }
    }
}
