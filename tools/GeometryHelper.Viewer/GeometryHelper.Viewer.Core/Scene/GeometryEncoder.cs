using System;
using System.Collections.Generic;
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
            string name = geometry?.GetType().Name;
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

        private static void Write(DataWriter w, object geometry)
        {
            switch (geometry)
            {
                case GeoPoint2 p:
                    w.Point(p.X, p.Y);
                    break;

                case GeoPoint3 p:
                    w.Point(p.X, p.Y, p.Z);
                    break;

                case GeoLine2 l:
                    w.BeginObject();
                    w.Field("a", l.StartPoint.X, l.StartPoint.Y);
                    w.Field("b", l.EndPoint.X, l.EndPoint.Y);
                    w.EndObject();
                    break;

                case GeoLine3 l:
                    w.BeginObject();
                    w.Field("a", l.StartPoint.X, l.StartPoint.Y, l.StartPoint.Z);
                    w.Field("b", l.EndPoint.X, l.EndPoint.Y, l.EndPoint.Z);
                    w.EndObject();
                    break;

                case GeoRay2 r:
                    w.BeginObject();
                    w.Field("a", r.Origin.X, r.Origin.Y);
                    w.Field("b", r.Direction.X, r.Direction.Y);
                    w.EndObject();
                    break;

                case GeoRay3 r:
                    w.BeginObject();
                    w.Field("a", r.Origin.X, r.Origin.Y, r.Origin.Z);
                    w.Field("b", r.Direction.X, r.Direction.Y, r.Direction.Z);
                    w.EndObject();
                    break;

                case GeoPolyline2 p:
                    WritePoints(w, p.Vertices);
                    break;

                case GeoPolygon2 p:
                    WritePoints(w, p.Vertices);
                    break;

                case GeoPolyline3 p:
                    WritePoints(w, p.Vertices);
                    break;

                case GeoPolygon3 p:
                    WritePoints(w, p.Vertices);
                    break;

                case GeoPolylineArc2 p:
                    WritePoints(w, p.Vertices, p.Bulges);
                    break;

                case GeoPolygonArc2 p:
                    WritePoints(w, p.Vertices, p.Bulges);
                    break;

                case GeoPolylineArc3 p:
                    WritePoints(w, p.Vertices, p.Bulges, p.Normals);
                    break;

                case GeoPolygonArc3 p:
                    WritePoints(w, p.Vertices, p.Bulges, p.Normals);
                    break;

                case GeoArc2 a:
                    w.BeginObject();
                    w.Field("center", a.Center.X, a.Center.Y);
                    w.Field("radius", a.Radius);
                    w.Field("start", a.StartAngle);
                    w.Field("sweep", a.SweptAngle);
                    w.EndObject();
                    break;

                case GeoArc3 a:
                    w.BeginObject();
                    w.Field("center", a.Center.X, a.Center.Y, a.Center.Z);
                    w.Field("normal", a.Normal.X, a.Normal.Y, a.Normal.Z);
                    w.Field("radius", a.Radius);
                    w.Field("start", a.StartAngle);
                    w.Field("sweep", a.SweptAngle);
                    w.EndObject();
                    break;

                case GeoCircle2 c:
                    w.BeginObject();
                    w.Field("center", c.Center.X, c.Center.Y);
                    w.Field("radius", c.Radius);
                    w.EndObject();
                    break;

                case GeoCircle3 c:
                    w.BeginObject();
                    w.Field("center", c.Center.X, c.Center.Y, c.Center.Z);
                    w.Field("normal", c.Normal.X, c.Normal.Y, c.Normal.Z);
                    w.Field("radius", c.Radius);
                    w.EndObject();
                    break;

                case GeoEllipse2 e:
                    w.BeginObject();
                    w.Field("center", e.Center.X, e.Center.Y);
                    w.Field("majorAxis", e.MajorAxis.X, e.MajorAxis.Y);
                    w.Field("majorRadius", e.MajorRadius);
                    w.Field("minorRadius", e.MinorRadius);
                    w.EndObject();
                    break;

                case GeoEllipse3 e:
                    w.BeginObject();
                    w.Field("center", e.Center.X, e.Center.Y, e.Center.Z);
                    w.Field("normal", e.Normal.X, e.Normal.Y, e.Normal.Z);
                    w.Field("majorAxis", e.MajorAxis.X, e.MajorAxis.Y, e.MajorAxis.Z);
                    w.Field("majorRadius", e.MajorRadius);
                    w.Field("minorRadius", e.MinorRadius);
                    w.EndObject();
                    break;

                case GeoRectangle2 r:
                    w.BeginObject();
                    w.Field("center", r.Center.X, r.Center.Y);
                    w.Field("width", r.Width);
                    w.Field("height", r.Height);
                    w.Field("angle", r.AngleRad);
                    w.EndObject();
                    break;

                case GeoTriangle2 t:
                    w.BeginObject();
                    w.Name("points");
                    w.BeginArray();
                    w.Point(t.A.X, t.A.Y);
                    w.Point(t.B.X, t.B.Y);
                    w.Point(t.C.X, t.C.Y);
                    w.EndArray();
                    w.EndObject();
                    break;

                case GeoTriangle3 t:
                    w.BeginObject();
                    w.Name("points");
                    w.BeginArray();
                    w.Point(t.A.X, t.A.Y, t.A.Z);
                    w.Point(t.B.X, t.B.Y, t.B.Z);
                    w.Point(t.C.X, t.C.Y, t.C.Z);
                    w.EndArray();
                    w.EndObject();
                    break;

                case GeoFace2 f:
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
                    break;

                case GeoFace3 f:
                    WriteFace(w, f);
                    break;

                case GeoSolid3 s:
                    WriteSolid(w, s);
                    break;

                case GeoMesh2 m:
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
                    break;

                case GeoMesh3 m:
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
                    break;

                case GeoAabb3 b:
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
                    break;

                case GeoObb3 b:
                    w.BeginObject();
                    w.Field("center", b.Center.X, b.Center.Y, b.Center.Z);
                    w.Field("sizeX", b.SizeX);
                    w.Field("sizeY", b.SizeY);
                    w.Field("sizeZ", b.SizeZ);
                    w.Field("axisX", b.AxisX.X, b.AxisX.Y, b.AxisX.Z);
                    w.Field("axisY", b.AxisY.X, b.AxisY.Y, b.AxisY.Z);
                    w.EndObject();
                    break;

                case GeoPlane3 p:
                    w.BeginObject();
                    w.Field("origin", p.Origin.X, p.Origin.Y, p.Origin.Z);
                    w.Field("normal", p.Normal.X, p.Normal.Y, p.Normal.Z);
                    w.EndObject();
                    break;

                default:
                    throw new ArgumentException("The viewer cannot write a " + geometry.GetType().FullName + ".", nameof(geometry));
            }
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
