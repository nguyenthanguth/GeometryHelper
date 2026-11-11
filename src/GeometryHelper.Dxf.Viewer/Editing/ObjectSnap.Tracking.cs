using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The snaps that work from more than the cursor: Extension, Parallel and object snap tracking from points acquired by
    /// resting the cursor on them, and Apparent Intersection from two objects under the cursor or one clicked first.
    /// </summary>
    public sealed partial class ObjectSnap
    {
        /// <summary>AutoCAD keeps seven acquired points at most.</summary>
        private const int MostAcquired = 7;

        /// <summary>The most tracking lines one acquired point gives: a polar increment of half a degree.</summary>
        private const int MostTrackingLines = 720;

        private readonly List<CadAcquiredPoint> _acquired = new List<CadAcquiredPoint>();

        // The object clicked first for an extended apparent intersection; null until one is.
        private Curve _firstObject;

        /// <summary>
        /// The cursor has rested, as AutoCAD acquires a point when the cursor pauses on it: a point acquired before and within
        /// the aperture is let go; else, with object snap tracking on, the snap point under the cursor is acquired, and with
        /// Extension the end of a line or an arc there; else, with Parallel and a base point, the line under the cursor. A
        /// point acquired past the seventh lets the oldest go. A line 0,0 to 100,0 rested on at its end (99.6, 0.3) with Extension
        /// gives the end (100, 0) running on along +X.
        /// </summary>
        /// <param name="index">The index of the space under the cursor.</param>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <param name="aperture">How near a point or object must come, in drawing units.</param>
        /// <param name="basePoint">The point the rubber band is drawn from, which Parallel needs; null for none.</param>
        /// <returns>Whether a point was acquired or let go.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the index is null.</exception>
        public bool Acquire(PickIndex index, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint = null)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            _active = Override ?? (IsOn ? Modes : CadSnapModes.None);
            bool extension = Has(CadSnapModes.Extension);
            bool parallel = Has(CadSnapModes.Parallel) && basePoint.HasValue;

            if (_active == CadSnapModes.None || aperture <= 0.0 || !(Tracking || extension || parallel))
            {
                return false;
            }

            // A point acquired before, rested on again, is let go: the nearest, the oldest of equals.
            int release = -1;
            double nearestRelease = double.MaxValue;

            for (int i = 0; i < _acquired.Count; i++)
            {
                double reach = (_acquired[i].Point - cursor).Length;

                if (reach <= aperture && reach < nearestRelease)
                {
                    release = i;
                    nearestRelease = reach;
                }
            }

            if (release >= 0)
            {
                _acquired.RemoveAt(release);
                return true;
            }

            var found = new Found { Ends = extension ? new List<End>() : null };
            Gather(index, cursor, aperture, basePoint, found);
            Candidate point = Tracking ? Best(found.Near) : null;
            End end = BestEnd(found.Ends);

            if (point != null || end != null)
            {
                CadAcquiredPoint acquired =
                    point != null && (end == null || point.Reach <= end.Reach)
                        ? new CadAcquiredPoint(point.Kind, point.Point, point.Handle)
                        : new CadAcquiredPoint(CadSnapKind.Extension, end.Point, end.Handle);

                // The end at the point acquired gives its run to Extension.
                if (end != null && (end.Point - acquired.Point).Length <= Tolerance.EqualPoint)
                {
                    acquired.Along = end.Along;
                    acquired.Arc = end.Arc;
                    acquired.AtArcStart = end.AtArcStart;
                }

                Add(acquired);
                return true;
            }

            if (!parallel)
            {
                return false;
            }

            // The line under the cursor, for Parallel: the nearest straight piece not drawn for a curve.
            Curve line = null;

            foreach (Curve curve in found.Curves)
            {
                if (
                    curve.Radius < 0.0
                    && !curve.Sampled
                    && curve.Run.Length > Tolerance.EqualPoint
                    && (line == null || CompareLines(curve, line) < 0)
                )
                {
                    line = curve;
                }
            }

            if (line == null)
            {
                return false;
            }

            GeoVector2 along = line.Run;
            Add(
                new CadAcquiredPoint(CadSnapKind.Parallel, line.Foot, line.Handle) { Direction = along / along.Length }
            );
            return true;
        }

        /// <summary>
        /// Acquires a point given rather than rested on, as AutoCAD's temporary track point (TT): while OTRACK is on, tracking
        /// lines run from it as from any point acquired, so that TT at (2 000, 0) holds the cursor near (2 000.3, 700) at
        /// (2 000, 700). It is drawn as a small cross and counts among the seven, the oldest let go first.
        /// </summary>
        /// <param name="point">The point, in drawing units.</param>
        public void AcquireTrackPoint(GeoPoint2 point) => Add(new CadAcquiredPoint(CadSnapKind.Tracking, point, null));

        /// <summary>Lets every acquired point go, and the object clicked first for an apparent intersection: the window calls it once a point is taken or the prompt goes.</summary>
        public void ClearAcquired()
        {
            _acquired.Clear();
            _firstObject = null;
        }

        /// <summary>
        /// Picks the object an <see cref="CadSnapKind.ExtendedApparentIntersection"/> offers as the first of two, as a click on
        /// it does in AutoCAD: the point is not taken, and the object under the cursor after gives where the two would cross,
        /// run on. Two lines 0,0 to 10,0 and 20,5 to 20,10, the first picked, give (20, 0) from anywhere on the second.
        /// </summary>
        /// <param name="result">The snap the click was given.</param>
        /// <returns>Whether the object was picked: false for any other snap.</returns>
        public bool TakeFirstObject(CadSnapResult result)
        {
            if (
                result == null
                || result.Kind != CadSnapKind.ExtendedApparentIntersection
                || !(result.Source is Curve curve)
            )
            {
                return false;
            }

            _firstObject = curve;
            return true;
        }

        private void Add(CadAcquiredPoint acquired)
        {
            if (_acquired.Count == MostAcquired)
            {
                _acquired.RemoveAt(0);
            }

            _acquired.Add(acquired);
        }

        private static int CompareLines(Curve a, Curve b)
        {
            int c = a.Reach.CompareTo(b.Reach);

            if (c == 0)
            {
                c = string.CompareOrdinal(a.Handle, b.Handle);
            }

            if (c == 0)
            {
                c = a.A.X.CompareTo(b.A.X);
            }

            if (c == 0)
            {
                c = a.A.Y.CompareTo(b.A.Y);
            }

            if (c == 0)
            {
                c = a.B.X.CompareTo(b.B.X);
            }

            return c != 0 ? c : a.B.Y.CompareTo(b.B.Y);
        }

        /// <summary>The end of a segment at <paramref name="at"/>, its run going on away from <paramref name="from"/>, where it lies within the aperture.</summary>
        private void AddEnd(
            List<End> ends,
            GeoPoint2 at,
            GeoPoint2 from,
            string handle,
            GeoPoint2 cursor,
            double aperture,
            double onIt
        )
        {
            GeoVector2 along = at - from;
            double length = along.Length;
            double reach = (at - cursor).Length;

            if (reach <= aperture && length > Tolerance.EqualPoint)
            {
                ends.Add(
                    new End
                    {
                        Point = at,
                        Handle = handle,
                        Reach = reach,
                        OnIt = onIt,
                        Along = along / length,
                    }
                );
            }
        }

        /// <summary>An end of an arc, its run going on round the circle away from the sweep, where it lies within the aperture.</summary>
        private static void AddEnd(
            List<End> ends,
            DisplayArc2 arc,
            bool atStart,
            GeoPoint2 at,
            GeoPoint2 cursor,
            double aperture,
            double onIt
        )
        {
            double reach = (at - cursor).Length;

            if (reach <= aperture)
            {
                ends.Add(
                    new End
                    {
                        Point = at,
                        Handle = arc.Handle,
                        Reach = reach,
                        OnIt = onIt,
                        Arc = arc,
                        AtArcStart = atStart,
                    }
                );
            }
        }

        /// <summary>The end nearest the cursor, then of the piece nearest it, then by handle, X, Y and the run.</summary>
        private static End BestEnd(List<End> ends)
        {
            End best = null;

            if (ends == null)
            {
                return null;
            }

            foreach (End end in ends)
            {
                if (best == null || end.CompareTo(best) < 0)
                {
                    best = end;
                }
            }

            return best;
        }

        /// <summary>
        /// Apparent intersections: where two curves under the aperture would cross run on, near the cursor; where the object
        /// clicked first and one under the cursor would; and, for a click, the object under the cursor offered as the first.
        /// Curves drawn as chains (splines, ellipses) have no run and are left out.
        /// </summary>
        private void AddApparent(
            List<Curve> curves,
            GeoPoint2 cursor,
            double aperture,
            List<Candidate> near,
            List<Candidate> onObject,
            List<Candidate> offered
        )
        {
            Curve offer = null;

            for (int i = 0; i < curves.Count; i++)
            {
                Curve a = curves[i];

                if (a.Sampled)
                {
                    continue;
                }

                for (int j = i + 1; j < curves.Count; j++)
                {
                    Curve b = curves[j];

                    if (b.Sampled)
                    {
                        continue;
                    }

                    foreach (GeoPoint2 point in Cross(a, b, true))
                    {
                        string handle = string.CompareOrdinal(a.Handle, b.Handle) <= 0 ? a.Handle : b.Handle;
                        Near(near, CadSnapKind.ApparentIntersection, point, handle, cursor, aperture);
                    }
                }

                if (_firstObject != null)
                {
                    // The crossing nearer the cursor of the two a circle may give is taken first.
                    foreach (GeoPoint2 point in Cross(_firstObject, a, true))
                    {
                        onObject.Add(
                            new Candidate(
                                CadSnapKind.ApparentIntersection,
                                point,
                                a.Handle,
                                a.Reach + (point - cursor).Length * 1E-9
                            )
                        );
                    }
                }

                if (offer == null || CompareOffers(a, offer) < 0)
                {
                    offer = a;
                }
            }

            if (offer != null)
            {
                offered.Add(
                    new Candidate(
                        CadSnapKind.ExtendedApparentIntersection,
                        offer.NearestTo(cursor),
                        offer.Handle,
                        offer.Reach,
                        source: offer
                    )
                );
            }
        }

        private static int CompareOffers(Curve a, Curve b)
        {
            int c = a.Reach.CompareTo(b.Reach);

            if (c == 0)
            {
                c = string.CompareOrdinal(a.Handle, b.Handle);
            }

            if (c == 0)
            {
                c = a.Radius.CompareTo(b.Radius);
            }

            if (c == 0)
            {
                c = (a.Radius < 0.0 ? a.A.X : a.Center.X).CompareTo(b.Radius < 0.0 ? b.A.X : b.Center.X);
            }

            return c != 0 ? c : (a.Radius < 0.0 ? a.A.Y : a.Center.Y).CompareTo(b.Radius < 0.0 ? b.A.Y : b.Center.Y);
        }

        /// <summary>
        /// The points the acquired points give: on an extension, a parallel or a tracking line within the aperture of the
        /// cursor (<paramref name="tracked"/>), and where two of these lines cross within it (<paramref name="near"/>), the
        /// polar lines from the base point among them while POLAR is on.
        /// </summary>
        private void AddTracking(
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            List<Candidate> near,
            List<Candidate> tracked
        )
        {
            if (_acquired.Count == 0)
            {
                return;
            }

            var paths = new List<TrackPath>();
            bool polar = Polar && PolarAngle > Tolerance.EqualAngleRad;
            double increment = polar ? PolarAngle : Math.PI / 2.0;
            int lines = Math.Min(MostTrackingLines, (int)Math.Floor((2.0 * Math.PI - 1E-9) / increment) + 1);

            // The lines from the points acquired run on the polar angles too only where POLARMODE says so (2), else level and upright.
            double trackIncrement = polar && TracksPolarAngles ? increment : Math.PI / 2.0;
            int trackLines = Math.Min(MostTrackingLines, (int)Math.Floor((2.0 * Math.PI - 1E-9) / trackIncrement) + 1);

            // The additional angles (POLARMODE 4) run with the polar ones; the polar lines from the base point are counted from
            // the last segment (POLARMODE 1), those from the points acquired from the X axis.
            List<double> trackRays = Rays(trackLines, trackIncrement, 0.0, polar && TracksPolarAngles);

            foreach (CadAcquiredPoint acquired in _acquired)
            {
                if (acquired.Direction.HasValue)
                {
                    // A line acquired for Parallel: the endless line through the base point along it, as two rays.
                    if (basePoint.HasValue && Has(CadSnapModes.Parallel))
                    {
                        paths.Add(
                            new TrackPath(
                                basePoint.Value,
                                acquired.Direction.Value,
                                CadSnapKind.Parallel,
                                acquired.Handle,
                                acquired
                            )
                        );
                        paths.Add(
                            new TrackPath(
                                basePoint.Value,
                                -acquired.Direction.Value,
                                CadSnapKind.Parallel,
                                acquired.Handle,
                                acquired
                            )
                        );
                    }

                    continue;
                }

                if (Has(CadSnapModes.Extension) && acquired.Along.HasValue)
                {
                    paths.Add(
                        new TrackPath(
                            acquired.Point,
                            acquired.Along.Value,
                            CadSnapKind.Extension,
                            acquired.Handle,
                            acquired
                        )
                    );
                }

                if (Has(CadSnapModes.Extension) && acquired.Arc != null)
                {
                    AddArcExtension(acquired, cursor, aperture, tracked);
                }

                if (Tracking)
                {
                    foreach (double angle in trackRays)
                    {
                        GeoVector2 direction = _ucs.ToWorld(new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                        paths.Add(
                            new TrackPath(acquired.Point, direction, CadSnapKind.Tracking, acquired.Handle, acquired)
                        );
                    }
                }
            }

            // The polar lines from the base point cross the others, as AutoCAD's polar and tracking lines do.
            if (polar && basePoint.HasValue && Tracking)
            {
                foreach (double angle in Rays(lines, increment, RelativeTurn() ?? 0.0, true))
                {
                    GeoVector2 direction = _ucs.ToWorld(new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                    paths.Add(new TrackPath(basePoint.Value, direction, CadSnapKind.Polar, null, null));
                }
            }

            // Only lines passing within the aperture of the cursor can give a point near it.
            var close = new List<TrackPath>();

            foreach (TrackPath path in paths)
            {
                double along =
                    (cursor.X - path.From.X) * path.Direction.X + (cursor.Y - path.From.Y) * path.Direction.Y;

                if (along < -aperture)
                {
                    continue;
                }

                GeoPoint2 foot = path.From + path.Direction * Math.Max(0.0, along);
                double reach = (foot - cursor).Length;

                if (reach > aperture)
                {
                    continue;
                }

                close.Add(path);

                if (path.Kind != CadSnapKind.Polar)
                {
                    tracked.Add(
                        new Candidate(path.Kind, foot, path.Handle, reach, new[] { new[] { path.From, foot } })
                    );
                }
            }

            for (int i = 0; i < close.Count; i++)
            {
                for (int j = i + 1; j < close.Count; j++)
                {
                    TrackPath a = close[i];
                    TrackPath b = close[j];

                    // Two lines from one acquired point, or from the base point, meet only there.
                    if (
                        (a.Owner != null && a.Owner == b.Owner)
                        || (a.Kind == CadSnapKind.Polar && b.Kind == CadSnapKind.Polar)
                        || (a.From - b.From).Length <= Tolerance.EqualPoint
                    )
                    {
                        continue;
                    }

                    double denominator = a.Direction.X * b.Direction.Y - a.Direction.Y * b.Direction.X;

                    if (Math.Abs(denominator) <= Tolerance.EqualVector)
                    {
                        continue;
                    }

                    GeoVector2 q = b.From - a.From;
                    double t = (q.X * b.Direction.Y - q.Y * b.Direction.X) / denominator;
                    double u = (q.X * a.Direction.Y - q.Y * a.Direction.X) / denominator;

                    if (t < -Tolerance.EqualPoint || u < -Tolerance.EqualPoint)
                    {
                        continue;
                    }

                    GeoPoint2 point = a.From + a.Direction * t;
                    double reach = (point - cursor).Length;

                    if (reach > aperture)
                    {
                        continue;
                    }

                    bool tracking =
                        a.Kind == CadSnapKind.Tracking
                        || b.Kind == CadSnapKind.Tracking
                        || a.Kind == CadSnapKind.Polar
                        || b.Kind == CadSnapKind.Polar;
                    string handle =
                        b.Handle == null || (a.Handle != null && string.CompareOrdinal(a.Handle, b.Handle) <= 0)
                            ? a.Handle
                            : b.Handle;
                    near.Add(
                        new Candidate(
                            tracking ? CadSnapKind.Tracking : CadSnapKind.Intersection,
                            point,
                            handle,
                            reach,
                            new[] { new[] { a.From, point }, new[] { b.From, point } }
                        )
                    );
                }
            }
        }

        /// <summary>The run of an acquired end of an arc: the point of its circle at the cursor's angle, where that lies off the arc and the cursor near the circle.</summary>
        private void AddArcExtension(
            CadAcquiredPoint acquired,
            GeoPoint2 cursor,
            double aperture,
            List<Candidate> tracked
        )
        {
            DisplayArc2 arc = acquired.Arc;
            double fromCenter = Math.Sqrt(
                (cursor.X - arc.Cx) * (cursor.X - arc.Cx) + (cursor.Y - arc.Cy) * (cursor.Y - arc.Cy)
            );
            double reach = Math.Abs(fromCenter - arc.Radius);

            if (reach > aperture || fromCenter <= Tolerance.EqualPoint)
            {
                return;
            }

            // The run leaves the end away from the sweep: forward from the end angle, back from the start angle.
            double sign = (arc.Sweep >= 0.0 ? 1.0 : -1.0) * (acquired.AtArcStart ? -1.0 : 1.0);
            double from = acquired.AtArcStart ? arc.Start : arc.Start + arc.Sweep;
            double angle = Math.Atan2(cursor.Y - arc.Cy, cursor.X - arc.Cx);
            double gone = sign * (angle - from);
            gone -= 2.0 * Math.PI * Math.Floor(gone / (2.0 * Math.PI));
            double room = 2.0 * Math.PI - Math.Abs(arc.Sweep);

            if (gone * arc.Radius <= Tolerance.EqualPoint || gone >= room)
            {
                return;
            }

            int steps = Math.Max(1, (int)Math.Ceiling(gone / (Math.PI / 36.0)));
            var track = new GeoPoint2[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                arc.PointAt(from + sign * gone * i / steps, out double x, out double y);
                track[i] = new GeoPoint2(x, y);
            }

            tracked.Add(new Candidate(CadSnapKind.Extension, track[steps], acquired.Handle, reach, new[] { track }));
        }

        /// <summary>The end of a line or an arc near the cursor, whose run Extension may follow.</summary>
        private sealed class End
        {
            public GeoPoint2 Point { get; set; }

            public string Handle { get; set; }

            /// <summary>How far the end lies from the cursor.</summary>
            public double Reach { get; set; }

            /// <summary>How far the piece it ends lies from the cursor: of two pieces meeting at a corner, the one under the cursor wins.</summary>
            public double OnIt { get; set; }

            public GeoVector2? Along { get; set; }

            public DisplayArc2 Arc { get; set; }

            public bool AtArcStart { get; set; }

            public int CompareTo(End other)
            {
                int c = Reach.CompareTo(other.Reach);

                if (c == 0)
                {
                    c = OnIt.CompareTo(other.OnIt);
                }

                if (c == 0)
                {
                    c = string.CompareOrdinal(Handle, other.Handle);
                }

                if (c == 0)
                {
                    c = Point.X.CompareTo(other.Point.X);
                }

                if (c == 0)
                {
                    c = Point.Y.CompareTo(other.Point.Y);
                }

                if (c == 0)
                {
                    c = (Along?.X ?? 2.0).CompareTo(other.Along?.X ?? 2.0);
                }

                if (c == 0)
                {
                    c = (Along?.Y ?? 2.0).CompareTo(other.Along?.Y ?? 2.0);
                }

                return c != 0 ? c : AtArcStart.CompareTo(other.AtArcStart);
            }
        }

        /// <summary>A ray a tracked point may lie on: from an acquired point, an end, or the base point, along a unit direction.</summary>
        private sealed class TrackPath
        {
            public TrackPath(
                GeoPoint2 from,
                GeoVector2 direction,
                CadSnapKind kind,
                string handle,
                CadAcquiredPoint owner
            )
            {
                From = from;
                Direction = direction;
                Kind = kind;
                Handle = handle;
                Owner = owner;
            }

            public GeoPoint2 From { get; }

            public GeoVector2 Direction { get; }

            public CadSnapKind Kind { get; }

            public string Handle { get; }

            /// <summary>The acquired point it runs from; null for a polar line from the base point.</summary>
            public CadAcquiredPoint Owner { get; }
        }
    }
}
