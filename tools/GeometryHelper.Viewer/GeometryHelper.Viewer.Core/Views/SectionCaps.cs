using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Fills the closed lines in which a clip plane cuts a body: the cap that shows what is inside. The lines are put in the plane, the ones inside
    /// another counted as its holes, and each outline with its holes is cut into triangles by the library, so that a block with a bore through it has a cap with
    /// a round hole in it and not a disc.
    /// </summary>
    /// <remarks>
    /// A line inside one line is a hole, a line inside that a piece of cap again, and so on inwards, which is the even-odd rule; a line is inside another
    /// when most of its corners are. Lines that do not close (a plane that cuts an open body) have no cap, and a group the library refuses to cut, or that throws, is left unfilled and
    /// counted. A block 100 by 60 by 40 with a bore of 20 across through it, cut at half its height across the bore, has a cap of 6000 less the bore's area.
    /// </remarks>
    public static class SectionCaps
    {
        /// <summary>
        /// The most lines one plane's section is capped with: a section with more is capped as far as it goes.
        /// </summary>
        public const int MaxLoops = 2000;

        /// <summary>
        /// The most corners an outline and its holes may have for the library to cut them, unless the outline is a convex ring that is cut in time that grows with its corners:
        /// five thousand, since the library cuts a loop by taking its corners off one at a time and the time grows with the square of them (a loop of 16 000 takes 1.4 s, one of 128 000 more
        /// than ten minutes). An outline with more is left unfilled and counted in <c>refused</c>.
        /// </summary>
        public const int MaxLibraryCorners = 5000;

        /// <summary>
        /// Gets the triangles that fill the closed lines of a section.
        /// </summary>
        /// <param name="chains">The lines of the section, as <see cref="SectionBuilder.Chains"/> gives them.</param>
        /// <param name="plane">The plane that cut them; the triangles lie in it and face away from the side that it keeps, towards the eye that cut the body away.</param>
        /// <param name="tolerance">The library tolerance the outlines are cut at.</param>
        /// <param name="refused">Gets how many outlines the library refused to cut, or that were left out as too many.</param>
        /// <param name="cancel">Stops the work between outlines.</param>
        /// <returns>The triangles; none where no line closes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the lines or the plane are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static List<DrawTriangle> Build(IReadOnlyList<DrawPolyline> chains, ClipPlane plane, Tolerance tolerance, out int refused, CancellationToken cancel = default(CancellationToken))
        {
            if (chains == null)
            {
                throw new ArgumentNullException(nameof(chains));
            }

            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            refused = 0;
            var triangles = new List<DrawTriangle>();
            List<DrawPolyline> closed = chains.Where(c => c.Closed && c.Points.Length >= 3).ToList();

            if (closed.Count == 0)
            {
                return triangles;
            }

            if (closed.Count > MaxLoops)
            {
                refused += closed.Count - MaxLoops;
                closed = closed.Take(MaxLoops).ToList();
            }

            ArcTessellation.Axes(plane.Normal, out Vec3 u, out Vec3 v);
            Vec3 origin = plane.Point;
            var loops = new Loop[closed.Count];

            for (int i = 0; i < loops.Length; i++)
            {
                loops[i] = new Loop(closed[i].Points, origin, u, v);
            }

            // The smallest loop that holds each one, and so how deep it is: a hole is odd, a piece of cap even.
            var parent = new int[loops.Length];
            var depth = new int[loops.Length];

            for (int i = 0; i < loops.Length; i++)
            {
                parent[i] = -1;

                for (int j = 0; j < loops.Length; j++)
                {
                    if (j != i && loops[j].Area > loops[i].Area && loops[j].Holds(loops[i]) && (parent[i] < 0 || loops[j].Area < loops[parent[i]].Area))
                    {
                        parent[i] = j;
                    }
                }
            }

            for (int i = 0; i < loops.Length; i++)
            {
                for (int j = parent[i]; j >= 0; j = parent[j])
                {
                    depth[i]++;
                }
            }

            Vec3 facing = -plane.Normal;

            using (Tolerance.Use(tolerance))
            {
                for (int outer = 0; outer < loops.Length; outer++)
                {
                    if ((depth[outer] & 1) != 0)
                    {
                        continue;
                    }

                    cancel.ThrowIfCancellationRequested();
                    var holes = new List<GeoPolygon3>();

                    try
                    {
                        for (int hole = 0; hole < loops.Length; hole++)
                        {
                            if (parent[hole] == outer && (depth[hole] & 1) != 0)
                            {
                                holes.Add(Polygon(closed[hole].Points));
                            }
                        }

                        var face = new GeoFace3(Polygon(closed[outer].Points), holes);

                        if (!FaceCutter.TryCutBounded(face, MaxLibraryCorners, out GeoTriangle3[] cut))
                        {
                            refused++;
                            continue;
                        }

                        foreach (GeoTriangle3 t in cut)
                        {
                            triangles.Add(new DrawTriangle(new Vec3(t.A.X, t.A.Y, t.A.Z), new Vec3(t.B.X, t.B.Y, t.B.Z), new Vec3(t.C.X, t.C.Y, t.C.Z), facing));
                        }
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException) && !(e is OperationCanceledException))
                    {
                        refused++;
                    }
                }
            }

            return triangles;
        }

        private static GeoPolygon3 Polygon(Vec3[] points) => new GeoPolygon3(points.Select(p => new GeoPoint3(p.X, p.Y, p.Z)));

        /// <summary>
        /// A closed line laid in the plane: its corners across and up the plane, and its area.
        /// </summary>
        private sealed class Loop
        {
            private readonly double[] _x;
            private readonly double[] _y;
            private readonly double _minX;
            private readonly double _maxX;
            private readonly double _minY;
            private readonly double _maxY;

            internal Loop(Vec3[] points, Vec3 origin, Vec3 u, Vec3 v)
            {
                _x = new double[points.Length];
                _y = new double[points.Length];
                _minX = double.PositiveInfinity;
                _minY = double.PositiveInfinity;
                _maxX = double.NegativeInfinity;
                _maxY = double.NegativeInfinity;
                double twice = 0.0;

                for (int i = 0; i < points.Length; i++)
                {
                    _x[i] = Vec3.Dot(points[i] - origin, u);
                    _y[i] = Vec3.Dot(points[i] - origin, v);
                    _minX = Math.Min(_minX, _x[i]);
                    _maxX = Math.Max(_maxX, _x[i]);
                    _minY = Math.Min(_minY, _y[i]);
                    _maxY = Math.Max(_maxY, _y[i]);
                }

                for (int i = 0; i < points.Length; i++)
                {
                    int k = (i + 1) % points.Length;
                    twice += (_x[i] - _x[0]) * (_y[k] - _y[0]) - (_x[k] - _x[0]) * (_y[i] - _y[0]);
                }

                Area = Math.Abs(twice) * 0.5;
            }

            internal double Area { get; }

            /// <summary>
            /// Gets whether most of the corners of another loop are inside this one.
            /// </summary>
            internal bool Holds(Loop other)
            {
                if (other._minX < _minX || other._maxX > _maxX || other._minY < _minY || other._maxY > _maxY)
                {
                    // The box of a loop inside another is inside its box, but for the rounding of the corners they share.
                    double slack = 1E-9 * Math.Max(_maxX - _minX, _maxY - _minY);

                    if (other._minX < _minX - slack || other._maxX > _maxX + slack || other._minY < _minY - slack || other._maxY > _maxY + slack)
                    {
                        return false;
                    }
                }

                int inside = 0;
                int count = other._x.Length;

                for (int i = 0; i < count; i++)
                {
                    if (Contains(other._x[i], other._y[i]))
                    {
                        inside++;
                    }
                }

                return inside * 2 > count;
            }

            private bool Contains(double x, double y)
            {
                bool inside = false;

                for (int i = 0, k = _x.Length - 1; i < _x.Length; k = i++)
                {
                    if ((_y[i] > y) != (_y[k] > y) && x < (_x[k] - _x[i]) * (y - _y[i]) / (_y[k] - _y[i]) + _x[i])
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
        }
    }
}
