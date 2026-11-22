using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>grdraw</c>, <c>grvecs</c> and <c>grclear</c>: vectors drawn on the screen for a moment, not part of the drawing. The
    /// points are in the current UCS, in drawing units (millimetres); the session keeps the vectors
    /// (<see cref="CadCommandSession.TemporaryVectors"/>) and the window draws them over the canvas until REDRAW, REGEN or
    /// <c>(redraw)</c> takes them off; <c>grclear</c> leaves them, as AutoCAD 2021 does. A session with no window keeps them and draws nothing. All three give
    /// nil, as in AutoCAD 2021's console (probe g1).
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterGraphics(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("GRDRAW", 3, 4, a => GrDraw(lisp, session, a));
            lisp.Define("GRVECS", 1, 2, a => GrVecs(lisp, session, a));

            // AutoCAD 2021 calls grclear obsolete and its window clears nothing (same pixel counts of a grdraw before and after it):
            // the vectors stay until REDRAW, REGEN or (redraw).
            lisp.Define("GRCLEAR", 0, 0, a => null);
        }

        /// <summary>
        /// <c>(grdraw from to color [highlight])</c>: one vector. The color is a whole number (a negative one is AutoCAD's XOR
        /// colour, 0 and 256 and beyond are taken as it is); the highlight is a whole number too, and where it is not 0 the
        /// vector is drawn dashed (AutoCAD 2021, probe g1: a real, <c>T</c> or nil for either is <c>bad argument type: fixnump</c>).
        /// </summary>
        private static object GrDraw(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            GeoPoint3 from = PointOf(a[0]) ?? throw LispInterpreter.TypeError("2D/3D point", a[0], colon: true);
            GeoPoint3 to = PointOf(a[1]) ?? throw LispInterpreter.TypeError("2D/3D point", a[1], colon: true);
            int colour = LispInterpreter.Int(a[2]);
            bool highlight = a.Length > 3 && LispInterpreter.Int(a[3]) != 0;

            lisp.OnOwner(() =>
            {
                session.AddTemporaryVectors(
                    new[]
                    {
                        new CadTemporaryVector(UcsToWorld(session, from), UcsToWorld(session, to), colour, highlight),
                    }
                );
                return null;
            });
            return null;
        }

        /// <summary>
        /// <c>(grvecs list [matrix])</c>: a list of vectors, each two points, an optional whole number before a vector giving the
        /// colour of it and of those after it until the next one (white where none is given). A point left with no end is
        /// <c>grvecs missing endpoint</c>, a thing that is neither a whole number nor a point <c>bad argument type: 2D/3D point</c>.
        /// The matrix is four rows of four numbers; each point is moved by it (its last column is the translation) before it is
        /// taken as a point of the current UCS. A matrix of another shape is <c>bad argument type: matrix</c> (probe g1).
        /// </summary>
        private static object GrVecs(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            if (!(a[0] is LispCons))
            {
                throw LispInterpreter.TypeError("consp", a[0]);
            }

            double[][] matrix = a.Length > 1 ? MatrixOf(a[1]) : null;
            var vectors = new List<CadTemporaryVector>();
            int colour = 7;
            GeoPoint3? pending = null;

            foreach (object item in LispInterpreter.ItemsOf(a[0]))
            {
                if (item is int whole)
                {
                    if (pending.HasValue)
                    {
                        throw new LispException("grvecs missing endpoint");
                    }

                    colour = whole;
                    continue;
                }

                GeoPoint3 point = PointOf(item) ?? throw LispInterpreter.TypeError("2D/3D point", item, colon: true);

                if (matrix != null)
                {
                    point = Moved(matrix, point);
                }

                if (!pending.HasValue)
                {
                    pending = point;
                    continue;
                }

                vectors.Add(new CadTemporaryVector(pending.Value, point, colour, false));
                pending = null;
            }

            if (pending.HasValue)
            {
                throw new LispException("grvecs missing endpoint");
            }

            lisp.OnOwner(() =>
            {
                var world = new List<CadTemporaryVector>(vectors.Count);

                foreach (CadTemporaryVector vector in vectors)
                {
                    world.Add(
                        new CadTemporaryVector(
                            UcsToWorld(session, vector.From),
                            UcsToWorld(session, vector.To),
                            vector.Colour,
                            false
                        )
                    );
                }

                session.AddTemporaryVectors(world);
                return null;
            });
            return null;
        }

        /// <summary>A point of the current UCS in the world.</summary>
        private static GeoPoint3 UcsToWorld(CadCommandSession session, GeoPoint3 point)
        {
            double[] world = FrameOf(session, 1).ToWorld(new[] { point.X, point.Y, point.Z }, false);
            return new GeoPoint3(world[0], world[1], world[2]);
        }

        /// <summary>The four rows of four numbers of a transformation matrix; <c>bad argument type: matrix</c> for anything else, a matrix of three rows and nil included.</summary>
        private static double[][] MatrixOf(object value)
        {
            var rows = new List<double[]>();
            object cell = value;

            while (cell is LispCons row && TryRow(row.Car, out double[] numbers))
            {
                rows.Add(numbers);
                cell = row.Cdr;
            }

            if (cell != null || rows.Count != 4)
            {
                throw LispInterpreter.TypeError("matrix", value, colon: true);
            }

            return rows.ToArray();
        }

        /// <summary>The four numbers of a row of a matrix; false for anything else.</summary>
        private static bool TryRow(object row, out double[] numbers)
        {
            var found = new List<double>(4);
            object cell = row;

            while (cell is LispCons item)
            {
                if (item.Car is int whole)
                {
                    found.Add(whole);
                }
                else if (item.Car is double real)
                {
                    found.Add(real);
                }
                else
                {
                    break;
                }

                cell = item.Cdr;
            }

            numbers = found.ToArray();
            return cell == null && found.Count == 4;
        }

        /// <summary>A point moved by a 4 by 4 matrix: each coordinate is its row's dot product with the point, plus the row's last number.</summary>
        private static GeoPoint3 Moved(double[][] m, GeoPoint3 p) =>
            new GeoPoint3(
                m[0][0] * p.X + m[0][1] * p.Y + m[0][2] * p.Z + m[0][3],
                m[1][0] * p.X + m[1][1] * p.Y + m[1][2] * p.Z + m[1][3],
                m[2][0] * p.X + m[2][1] * p.Y + m[2][2] * p.Z + m[2][3]
            );
    }
}
