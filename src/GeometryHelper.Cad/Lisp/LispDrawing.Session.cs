using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>trans</c>, <c>osnap</c>, <c>alert</c>, <c>grtext</c> and <c>redraw</c>: what a routine asks of the session. A point is
    /// carried between the coordinate systems AutoLISP names (0 world, 1 the current UCS, 2 the display, 3 the paper's display,
    /// the extrusion of an entity or an extrusion vector), the nearest object snap of a point is found as a snap typed at a prompt
    /// is, and a message is handed to the window.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterSession(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("TRANS", 3, 4, a => Trans(lisp, session, a));
            lisp.Define("OSNAP", 2, 2, a => Osnap(lisp, session, a));
            lisp.Define("ALERT", 1, 1, a => Alert(lisp, session, LispInterpreter.Str(a[0])));
            lisp.Define("GRTEXT", 0, 3, a => GrText(a));
            lisp.Define("REDRAW", 0, 2, a => Redraw(a));
        }

        #region trans

        /// <summary>A coordinate system as the three axes it has in the world and where its origin is.</summary>
        private sealed class Frame
        {
            internal Frame(double[] origin, double[] x, double[] y, double[] z)
            {
                Origin = origin;
                X = x;
                Y = y;
                Z = z;
            }

            private double[] Origin { get; }

            private double[] X { get; }

            private double[] Y { get; }

            private double[] Z { get; }

            /// <summary>A point of the system in the world; for a displacement the origin is left out.</summary>
            internal double[] ToWorld(double[] point, bool displacement)
            {
                var world = new double[3];

                for (int i = 0; i < 3; i++)
                {
                    world[i] = X[i] * point[0] + Y[i] * point[1] + Z[i] * point[2] + (displacement ? 0.0 : Origin[i]);
                }

                return world;
            }

            /// <summary>A point of the world in the system.</summary>
            internal double[] FromWorld(double[] point, bool displacement)
            {
                double[] moved = displacement
                    ? point
                    : new[] { point[0] - Origin[0], point[1] - Origin[1], point[2] - Origin[2] };
                return new[] { Dot(moved, X), Dot(moved, Y), Dot(moved, Z) };
            }

            private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        }

        private static readonly Frame World = new Frame(
            new[] { 0.0, 0.0, 0.0 },
            new[] { 1.0, 0.0, 0.0 },
            new[] { 0.0, 1.0, 0.0 },
            new[] { 0.0, 0.0, 1.0 }
        );

        private static object Trans(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            GeoPoint3 point = PointOf(a[0]) ?? throw LispInterpreter.TypeError("2D/3D point", a[0], colon: true);
            bool displacement = a.Length > 3 && a[3] != null;
            object from = a[1];
            object to = a[2];

            return lisp.OnOwner(() =>
            {
                Frame source = FrameOf(session, from);
                Frame target = FrameOf(session, to);

                if (source == null || target == null)
                {
                    return null;
                }

                double[] world = source.ToWorld(new[] { point.X, point.Y, point.Z }, displacement);
                double[] result = target.FromWorld(world, displacement);
                return LispInterpreter.ListOf(new object[] { result[0], result[1], result[2] });
            });
        }

        /// <summary>The system a <c>trans</c> argument names: 0 world, 1 UCS, 2 and 3 the display (the world, as every view of the presenter is a plan), an entity (its extrusion) or an extrusion vector.</summary>
        private static Frame FrameOf(CadCommandSession session, object system)
        {
            switch (system)
            {
                case int code when code == 0 || code == 2 || code == 3:
                    return World;
                case int code when code == 1:
                    CadUcs ucs = session.Ucs;
                    return new Frame(
                        new[] { ucs.Origin.X, ucs.Origin.Y, ucs.Origin.Z },
                        new[] { ucs.XAxis.X, ucs.XAxis.Y, ucs.XAxis.Z },
                        new[] { ucs.YAxis.X, ucs.YAxis.Y, ucs.YAxis.Z },
                        new[] { ucs.ZAxis.X, ucs.ZAxis.Y, ucs.ZAxis.Z }
                    );
                case int _:
                    return null;
                case LispEntityName name:
                    DxfItem item = FindItem(session.Document, name.Handle);

                    if (item == null)
                    {
                        return null;
                    }

                    double[] normal = { 0.0, 0.0, 1.0 };

                    foreach (KeyValuePair<int, object> pair in LispEntityData.Read(item.Groups, item.Type))
                    {
                        if (pair.Key == 210 && pair.Value is double[] extrusion && extrusion.Length == 3)
                        {
                            normal = extrusion;
                            break;
                        }
                    }

                    return Extrusion(normal);
                default:
                    GeoPoint3? vector = PointOf(system);

                    if (!vector.HasValue || LispInterpreter.ItemsOf(system).Count != 3)
                    {
                        throw LispInterpreter.TypeError("fixnump", system, colon: true);
                    }

                    return Extrusion(new[] { vector.Value.X, vector.Value.Y, vector.Value.Z });
            }
        }

        /// <summary>The system of an extrusion direction, by AutoCAD's arbitrary axis algorithm: the x axis is the world y crossed with it, or the world z where it is within 1/64 of the world z.</summary>
        private static Frame Extrusion(double[] normal)
        {
            double length = Math.Sqrt(normal[0] * normal[0] + normal[1] * normal[1] + normal[2] * normal[2]);

            if (length < 1E-12)
            {
                return World;
            }

            double nx = normal[0] / length;
            double ny = normal[1] / length;
            double nz = normal[2] / length;
            double[] x =
                Math.Abs(nx) < 1.0 / 64.0 && Math.Abs(ny) < 1.0 / 64.0
                    ? new[] { nz, 0.0, -nx } // the world y crossed with the normal
                    : new[] { -ny, nx, 0.0 }; // the world z crossed with the normal
            x = Unit(x);
            double[] y = Unit(new[] { ny * x[2] - nz * x[1], nz * x[0] - nx * x[2], nx * x[1] - ny * x[0] });
            return new Frame(new[] { 0.0, 0.0, 0.0 }, x, y, new[] { nx, ny, nz });
        }

        private static double[] Unit(double[] v)
        {
            double length = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
            return length < 1E-12 ? v : new[] { v[0] / length, v[1] / length, v[2] / length };
        }

        #endregion

        #region osnap

        private static object Osnap(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            GeoPoint3 point = PointOf(a[0]) ?? throw LispInterpreter.TypeError("2D/3D point", a[0], colon: true);
            string modes = LispInterpreter.Str(a[1]);

            return lisp.OnOwner(() => NearestSnap(session, point, modes));
        }

        /// <summary>
        /// The nearest snap of the modes to a point within the aperture of a snap typed at a prompt (<see cref="CadCommandSession.SnapAperture"/>),
        /// as a list of three reals in the UCS; nil where no object is there or no mode is known. The modes are the three
        /// letters AutoCAD takes (<c>end</c>, <c>mid</c>, <c>cen</c>, <c>nod</c>, <c>qua</c>, <c>int</c>, <c>ins</c>, <c>per</c>,
        /// <c>tan</c>, <c>nea</c>, <c>app</c>), each with or without a leading <c>_</c>, separated by commas; <c>non</c> gives the
        /// point as it is.
        /// </summary>
        private static object NearestSnap(CadCommandSession session, GeoPoint3 point, string modes)
        {
            var kinds = new List<CadSnapKind>();

            foreach (string mode in modes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = mode.Trim().TrimStart('_').ToLowerInvariant();

                if (name.StartsWith("non", StringComparison.Ordinal))
                {
                    return LispInterpreter.ListOf(new object[] { point.X, point.Y, point.Z });
                }

                CadSnapKind? kind = KindOfName(name);

                if (kind.HasValue)
                {
                    kinds.Add(kind.Value);
                }
            }

            if (kinds.Count == 0)
            {
                return null;
            }

            GeoPoint2 at = session.Ucs.ToWorld(new GeoPoint2(point.X, point.Y));
            IReadOnlyList<string> near = session
                .Document.GetPickIndex(session.Document.WorkingSpace)
                .Pick(at.X, at.Y, Math.Max(session.SnapAperture, 1E-9));
            GeoPoint2? best = null;
            double nearest = double.MaxValue;
            string[] objects = new List<string>(near).GetRange(0, Math.Min(near.Count, 8)).ToArray();

            void Consider(GeoPoint2? found)
            {
                if (found.HasValue && (found.Value - at).Length < nearest)
                {
                    best = found;
                    nearest = (found.Value - at).Length;
                }
            }

            foreach (CadSnapKind kind in kinds)
            {
                if (kind == CadSnapKind.Intersection || kind == CadSnapKind.ApparentIntersection)
                {
                    for (int i = 0; i < objects.Length; i++)
                    {
                        for (int j = i + 1; j < objects.Length; j++)
                        {
                            Consider(CadCalcSnap.Between(session, kind, objects[i], objects[j], at));
                        }
                    }

                    continue;
                }

                GeoPoint2? from =
                    kind == CadSnapKind.Perpendicular || kind == CadSnapKind.Tangent
                        ? session.LastPoint
                        : (GeoPoint2?)null;

                foreach (string handle in near)
                {
                    Consider(CadCalcSnap.Snap(session, kind, handle, at, from));
                }
            }

            if (!best.HasValue)
            {
                return null;
            }

            GeoPoint2 ucs = session.Ucs.ToUcs(best.Value);
            return LispInterpreter.ListOf(new object[] { ucs.X, ucs.Y, 0.0 });
        }

        private static CadSnapKind? KindOfName(string name)
        {
            switch (name)
            {
                case "end":
                case "endp":
                case "endpoint":
                    return CadSnapKind.Endpoint;
                case "mid":
                case "midp":
                case "midpoint":
                    return CadSnapKind.Midpoint;
                case "cen":
                case "center":
                    return CadSnapKind.Center;
                case "nod":
                case "node":
                    return CadSnapKind.Node;
                case "qua":
                case "quadrant":
                    return CadSnapKind.Quadrant;
                case "int":
                case "intersection":
                    return CadSnapKind.Intersection;
                case "ins":
                case "insert":
                case "insertion":
                    return CadSnapKind.Insertion;
                case "per":
                case "perpendicular":
                    return CadSnapKind.Perpendicular;
                case "tan":
                case "tangent":
                    return CadSnapKind.Tangent;
                case "nea":
                case "nearest":
                    return CadSnapKind.Nearest;
                case "app":
                case "appint":
                    return CadSnapKind.ApparentIntersection;
                default:
                    return null;
            }
        }

        #endregion

        #region alert, grtext, redraw

        /// <summary>
        /// Shows a message: the session asks the window for an <c>ALERT</c>, the text as its payload; where nobody answers (a
        /// session with no window) the lines of the text are written as the command line. Gives nil.
        /// </summary>
        private static object Alert(LispInterpreter lisp, CadCommandSession session, string text)
        {
            lisp.OnOwner(() =>
            {
                session.ShowAlert(text);
                return null;
            });
            return null;
        }

        /// <summary><c>(grtext [box text [highlight]])</c>: the status line is not drawn in L2; gives the text, nil for no arguments.</summary>
        private static object GrText(object[] a)
        {
            if (a.Length == 0)
            {
                return null;
            }

            LispInterpreter.Int(a[0]);
            string text = a.Length > 1 ? LispInterpreter.Str(a[1]) : null;

            if (a.Length > 2)
            {
                LispInterpreter.Int(a[2]);
            }

            return text;
        }

        /// <summary><c>(redraw [ename [mode]])</c>: the drawing is shown as it is; an entity name that is no entity is an error, as in AutoCAD 2021 (q6). Gives nil.</summary>
        private static object Redraw(object[] a)
        {
            if (a.Length > 0)
            {
                NameOf(a[0]);
            }

            if (a.Length > 1)
            {
                LispInterpreter.Int(a[1]);
            }

            return null;
        }

        #endregion
    }
}
