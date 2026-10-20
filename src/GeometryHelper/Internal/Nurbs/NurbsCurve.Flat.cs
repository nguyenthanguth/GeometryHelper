using System;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// A plane of space two curves lie in: two unit directions along it and the unit normal, square to each other, and how far along the
    /// normal the plane is from the origin of the drawing. A point of the plane is its two numbers along the two directions, measured from
    /// the origin of the drawing, so that a plane square to an axis keeps the coordinates as they are.
    /// </summary>
    internal sealed class FlatFrame
    {
        internal double Ux, Uy, Uz, Vx, Vy, Vz, Nx, Ny, Nz;

        /// <summary>The axis the plane is square to, 0, 1 or 2 for X, Y or Z, whose two others are its directions in order; -1 for none.</summary>
        internal int Square = -1;

        /// <summary>How far along the normal the plane lies from the origin of the drawing.</summary>
        internal double Height;

        /// <summary>
        /// Gets the point of space at two numbers of the plane.
        /// </summary>
        internal void ToSpace(double x, double y, out double px, out double py, out double pz)
        {
            px = x * Ux + y * Vx + Height * Nx;
            py = x * Uy + y * Vy + Height * Ny;
            pz = x * Uz + y * Vz + Height * Nz;
        }

        /// <summary>
        /// Gets the two numbers in the plane of a point of space, or of a direction: the same numbers a curve laid flat takes for its
        /// control points (<see cref="NurbsCurve.TryLayFlat"/>).
        /// </summary>
        internal void ToPlane(double x, double y, double z, out double px, out double py)
        {
            if (Square == 2)
            {
                px = x;
                py = y;
            }
            else if (Square == 0)
            {
                px = y;
                py = z;
            }
            else if (Square == 1)
            {
                px = x;
                py = z;
            }
            else
            {
                px = Ux * x + Uy * y + Uz * z;
                py = Vx * x + Vy * y + Vz * z;
            }
        }

        /// <summary>
        /// Says which way round a turn about a direction of space runs in the plane: 1 where the direction is along the plane's first
        /// direction crossed with its second, so that counter-clockwise about it is counter-clockwise in the plane, and -1 where it is the
        /// other way.
        /// </summary>
        internal double Handedness(double x, double y, double z)
        {
            double cx = Uy * Vz - Uz * Vy;
            double cy = Uz * Vx - Ux * Vz;
            double cz = Ux * Vy - Uy * Vx;

            return cx * x + cy * y + cz * z >= 0.0 ? 1.0 : -1.0;
        }
    }

    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// Lays two curves in space that lie in one plane into the plane: the same degrees, knots and weights, and each control point as
        /// its two numbers in the plane. Two curves in one plane are then asked about each other as two curves of the plane are, the
        /// offset of the one from the other having a sign there that it has nowhere in space, and the answers are those of the plane.
        /// </summary>
        /// <param name="a">The first curve, in space.</param>
        /// <param name="b">The second curve, in space.</param>
        /// <param name="frame">The plane, when the method returns true.</param>
        /// <param name="flatA">The first curve laid in the plane.</param>
        /// <param name="flatB">The second curve laid in the plane.</param>
        /// <returns>
        /// true when every control point of both lies within a snap (1E-13 of the size of the numbers) of one plane, so that the curves,
        /// which lie in the hulls of their control points, do too; false otherwise.
        /// </returns>
        /// <remarks>
        /// The plane is through the first control point of the first curve, the control point furthest from it, and the control point
        /// furthest from the line through those two; two curves on one line lie in a plane square to an axis where the line allows it.
        /// A plane square to an axis has the other two axes as its directions, in order, so that two curves in the floor keep their numbers
        /// to the bit. The curves laid in the plane keep the size of the numbers of space as the size their rounding goes by: a point a
        /// million from the origin carries the rounding of a million, whatever its numbers in the plane are.
        /// </remarks>
        internal static bool TryLayFlat(NurbsCurve a, NurbsCurve b, out FlatFrame frame, out NurbsCurve flatA, out NurbsCurve flatB)
        {
            flatA = null;
            flatB = null;

            if (!InOnePlane(a, b, out frame))
            {
                return false;
            }

            double scale = Math.Max(a.Scale, b.Scale);
            flatA = a.LaidFlat(frame, scale);
            flatB = b.LaidFlat(frame, scale);
            return true;
        }

        /// <summary>
        /// Says whether two curves in space lie in one plane, to a snap of the size of their numbers, and gives the plane
        /// (<see cref="TryLayFlat"/>).
        /// </summary>
        internal static bool InOnePlane(NurbsCurve a, NurbsCurve b, out FlatFrame frame) => InOnePlane(a.Points, b.Points, Math.Max(a.Scale, b.Scale), out frame);

        /// <summary>
        /// Says whether two sets of points of space, three numbers each, lie in one plane, to a snap of a size of the numbers, and gives
        /// the plane, as for two curves whose control points they are (<see cref="TryLayFlat"/>).
        /// </summary>
        internal static bool InOnePlane(double[] first, double[] second, double scale, out FlatFrame frame)
        {
            frame = null;
            double snap = Hunt.SnapShare * scale;
            double ox = first[0];
            double oy = first[1];
            double oz = first[2];

            // The control point furthest from the first.
            double fx = 0.0, fy = 0.0, fz = 0.0, furthest = 0.0;

            for (int curve = 0; curve < 2; curve++)
            {
                double[] points = curve == 0 ? first : second;

                for (int i = 0; i < points.Length; i += 3)
                {
                    double dx = points[i] - ox;
                    double dy = points[i + 1] - oy;
                    double dz = points[i + 2] - oz;
                    double square = dx * dx + dy * dy + dz * dz;

                    if (square > furthest)
                    {
                        furthest = square;
                        fx = dx;
                        fy = dy;
                        fz = dz;
                    }
                }
            }

            double nx, ny, nz;

            if (!(furthest > 0.0))
            {
                // Every control point is one point.
                nx = 0.0;
                ny = 0.0;
                nz = 1.0;
            }
            else
            {
                double length = Math.Sqrt(furthest);
                double ex = fx / length;
                double ey = fy / length;
                double ez = fz / length;

                // The control point furthest from the line through those two, by the cross product with its direction.
                double cx = 0.0, cy = 0.0, cz = 0.0, across = 0.0;

                for (int curve = 0; curve < 2; curve++)
                {
                    double[] points = curve == 0 ? first : second;

                    for (int i = 0; i < points.Length; i += 3)
                    {
                        double dx = points[i] - ox;
                        double dy = points[i + 1] - oy;
                        double dz = points[i + 2] - oz;
                        double px = ey * dz - ez * dy;
                        double py = ez * dx - ex * dz;
                        double pz = ex * dy - ey * dx;
                        double square = px * px + py * py + pz * pz;

                        if (square > across)
                        {
                            across = square;
                            cx = px;
                            cy = py;
                            cz = pz;
                        }
                    }
                }

                if (!(Math.Sqrt(across) > snap))
                {
                    // On one line: a plane through it, square to an axis where the line runs square to that axis.
                    if (ez == 0.0)
                    {
                        nx = 0.0;
                        ny = 0.0;
                        nz = 1.0;
                    }
                    else if (ex == 0.0)
                    {
                        nx = 1.0;
                        ny = 0.0;
                        nz = 0.0;
                    }
                    else if (ey == 0.0)
                    {
                        nx = 0.0;
                        ny = 1.0;
                        nz = 0.0;
                    }
                    else
                    {
                        Slab.Perpendiculars(ex, ey, ez, out nx, out ny, out nz, out _, out _, out _);
                    }
                }
                else
                {
                    double normal = Math.Sqrt(across);
                    nx = cx / normal;
                    ny = cy / normal;
                    nz = cz / normal;
                }
            }

            // Every control point within a snap of the plane through the first.
            for (int curve = 0; curve < 2; curve++)
            {
                double[] points = curve == 0 ? first : second;

                for (int i = 0; i < points.Length; i += 3)
                {
                    if (!(Math.Abs(nx * (points[i] - ox) + ny * (points[i + 1] - oy) + nz * (points[i + 2] - oz)) <= snap))
                    {
                        return false;
                    }
                }
            }

            frame = new FlatFrame();

            if (nx == 0.0 && ny == 0.0)
            {
                frame.Ux = 1.0;
                frame.Vy = 1.0;
                frame.Nz = 1.0;
                frame.Height = oz;
                frame.Square = 2;
            }
            else if (ny == 0.0 && nz == 0.0)
            {
                frame.Uy = 1.0;
                frame.Vz = 1.0;
                frame.Nx = 1.0;
                frame.Height = ox;
                frame.Square = 0;
            }
            else if (nx == 0.0 && nz == 0.0)
            {
                frame.Ux = 1.0;
                frame.Vz = 1.0;
                frame.Ny = 1.0;
                frame.Height = oy;
                frame.Square = 1;
            }
            else
            {
                Slab.Perpendiculars(nx, ny, nz, out frame.Ux, out frame.Uy, out frame.Uz, out frame.Vx, out frame.Vy, out frame.Vz);
                frame.Nx = nx;
                frame.Ny = ny;
                frame.Nz = nz;
                frame.Height = nx * ox + ny * oy + nz * oz;
            }

            return true;
        }

        /// <summary>
        /// Gets the curve laid in a plane: each control point as its two numbers along the plane's directions, the size of its numbers
        /// held to a size given.
        /// </summary>
        internal NurbsCurve LaidFlat(FlatFrame frame, double scale)
        {
            var points = new double[2 * Count];

            for (int i = 0; i < Count; i++)
            {
                double x = Points[3 * i];
                double y = Points[3 * i + 1];
                double z = Points[3 * i + 2];

                if (frame.Square == 2)
                {
                    points[2 * i] = x;
                    points[2 * i + 1] = y;
                }
                else if (frame.Square == 0)
                {
                    points[2 * i] = y;
                    points[2 * i + 1] = z;
                }
                else if (frame.Square == 1)
                {
                    points[2 * i] = x;
                    points[2 * i + 1] = z;
                }
                else
                {
                    points[2 * i] = frame.Ux * x + frame.Uy * y + frame.Uz * z;
                    points[2 * i + 1] = frame.Vx * x + frame.Vy * y + frame.Vz * z;
                }
            }

            var flat = new NurbsCurve(2, Degree, points, Knots, Rational ? Weights : null);
            flat._scale = Math.Max(flat.Scale, scale);

            return flat;
        }
    }
}
