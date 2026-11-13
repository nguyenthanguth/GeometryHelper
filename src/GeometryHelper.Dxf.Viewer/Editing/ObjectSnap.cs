using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The object snaps that may be taken, with AutoCAD's OSMODE values: Endpoint 1, Midpoint 2, Center 4, Node 8, Quadrant 16,
    /// Intersection 32, Insertion 64, Perpendicular 128, Tangent 256, Nearest 512, Geometric Center 1024, Apparent
    /// Intersection 2048, Extension 4096 and Parallel 8192, so that an OSMODE of 4133 reads as Endpoint, Center,
    /// Intersection and Extension.
    /// </summary>
    [Flags]
    public enum CadSnapModes
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>The ends of lines, arcs and polyline segments, the corners of polylines and fills, and the base point of a RAY.</summary>
        Endpoint = 1,

        /// <summary>The middle of a line, an arc or a polyline segment, and the root point of an XLINE, as AutoCAD takes it.</summary>
        Midpoint = 2,

        /// <summary>The centre of an arc or a circle.</summary>
        Center = 4,

        /// <summary>A POINT object.</summary>
        Node = 8,

        /// <summary>The points of a circle or an arc at 0, 90, 180 and 270 degrees.</summary>
        Quadrant = 16,

        /// <summary>Where two objects cross.</summary>
        Intersection = 32,

        /// <summary>
        /// The insertion point of a block, a text, an MTEXT or an attribute: taken near the point, or with the cursor anywhere on
        /// the object, as AutoCAD takes the base point of a block from any of its lines.
        /// </summary>
        Insertion = 64,

        /// <summary>
        /// The foot of the perpendicular from the base point to an object: on a line or a straight segment of a polyline run on
        /// past its ends, as AutoCAD gives it; on an arc only where the arc runs.
        /// </summary>
        Perpendicular = 128,

        /// <summary>Where a line from the base point touches a circle or an arc.</summary>
        Tangent = 256,

        /// <summary>The point of an object nearest the cursor.</summary>
        Nearest = 512,

        /// <summary>The centroid of the area a closed polyline holds, taken with the cursor on the polyline or near the centroid.</summary>
        GeometricCenter = 1024,

        /// <summary>
        /// Where two objects would cross if both ran on, lines as endless lines and arcs as whole circles: two under the cursor,
        /// or one clicked first (Extended Apparent Intersection) and one under the cursor after.
        /// </summary>
        ApparentIntersection = 2048,

        /// <summary>A point on the run of a line or an arc beyond an end the cursor rested on.</summary>
        Extension = 4096,

        /// <summary>A point on the line through the base point parallel to a line the cursor rested on.</summary>
        Parallel = 8192,

        /// <summary>
        /// The eight snaps the presenter had first, Endpoint to Nearest; <see cref="Node"/>, <see cref="GeometricCenter"/> and
        /// the snaps after them are not in it, so that a set kept as "every snap but Nearest" takes the same points as before
        /// they came.
        /// </summary>
        All = Endpoint | Midpoint | Center | Quadrant | Intersection | Perpendicular | Tangent | Nearest,
    }

    /// <summary>Which snap gave a point.</summary>
    public enum CadSnapKind
    {
        /// <summary>No snap: the cursor itself.</summary>
        None,

        /// <summary>An endpoint.</summary>
        Endpoint,

        /// <summary>A midpoint.</summary>
        Midpoint,

        /// <summary>A centre.</summary>
        Center,

        /// <summary>A quadrant.</summary>
        Quadrant,

        /// <summary>An intersection.</summary>
        Intersection,

        /// <summary>A perpendicular foot.</summary>
        Perpendicular,

        /// <summary>A tangent point.</summary>
        Tangent,

        /// <summary>The nearest point of an object.</summary>
        Nearest,

        /// <summary>A point of the snap grid.</summary>
        Grid,

        /// <summary>The cursor held level or upright from the base point (ORTHO).</summary>
        Ortho,

        /// <summary>The cursor held on a polar angle from the base point (POLAR).</summary>
        Polar,

        /// <summary>A POINT object (NODe).</summary>
        Node,

        /// <summary>The centroid of a closed polyline (GCEn).</summary>
        GeometricCenter,

        /// <summary>The insertion point of a block, a text or an attribute (INSertion).</summary>
        Insertion,

        /// <summary>A point on the run of a line or an arc beyond an acquired end (EXTension).</summary>
        Extension,

        /// <summary>Where two objects would cross if both ran on (APParent intersection).</summary>
        ApparentIntersection,

        /// <summary>A point on the line through the base point parallel to an acquired line (PARallel).</summary>
        Parallel,

        /// <summary>A point on a tracking line from an acquired point, or where two tracking lines cross (OTRACK).</summary>
        Tracking,

        /// <summary>
        /// The object under the cursor offered as the first of an apparent intersection: a click picks it, as AutoCAD's
        /// Extended Apparent Intersection does, and takes no point (<see cref="ObjectSnap.TakeFirstObject"/>).
        /// </summary>
        ExtendedApparentIntersection,
    }

    /// <summary>
    /// The isometric plane of an isometric snap, as AutoCAD's SNAPISOPAIR: Left 0 on the 90 and 150 degree axes, Top 1 on the
    /// 30 and 150 degree axes, Right 2 on the 30 and 90 degree axes. Ortho holds the cursor on the two axes of the plane, and
    /// the crosshair and the grid are drawn along them.
    /// </summary>
    public enum CadIsoplane
    {
        /// <summary>The left plane, on the 90 and 150 degree axes.</summary>
        Left = 0,

        /// <summary>The top plane, on the 30 and 150 degree axes.</summary>
        Top = 1,

        /// <summary>The right plane, on the 30 and 90 degree axes.</summary>
        Right = 2,
    }

    /// <summary>The point a snap gave, which snap gave it and, for an object snap, the object.</summary>
    public sealed class CadSnapResult
    {
        private static readonly IReadOnlyList<IReadOnlyList<GeoPoint2>> NoTracks = Array.Empty<
            IReadOnlyList<GeoPoint2>
        >();

        internal CadSnapResult(
            CadSnapKind kind,
            GeoPoint2 point,
            string handle,
            IReadOnlyList<IReadOnlyList<GeoPoint2>> tracks = null,
            object source = null
        )
        {
            Kind = kind;
            Point = point;
            Handle = handle;
            Tracks = tracks ?? NoTracks;
            Source = source;
        }

        /// <summary>Gets which snap gave the point; <see cref="CadSnapKind.None"/> for the cursor itself.</summary>
        public CadSnapKind Kind { get; }

        /// <summary>Gets the point, in drawing units.</summary>
        public GeoPoint2 Point { get; }

        /// <summary>Gets the handle of the object snapped to, or null for a grid, ortho, polar or no snap.</summary>
        public string Handle { get; }

        /// <summary>
        /// Gets the dotted paths drawn to the point, each a chain of points in drawing units that ends at the point: from the
        /// acquired end along an extension, from the base point along a parallel, from each acquired point along its tracking
        /// line; none for any other snap. The extension of an arc runs round its circle in steps of 5 degrees at most.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<GeoPoint2>> Tracks { get; }

        /// <summary>The object an Extended Apparent Intersection offers, kept for <see cref="ObjectSnap.TakeFirstObject"/>.</summary>
        internal object Source { get; }

        /// <inheritdoc/>
        public override string ToString() => Kind + " " + Point;
    }

    /// <summary>
    /// A point acquired by resting the cursor on it, as AutoCAD acquires one: a snap point tracking lines run from (OTRACK), the
    /// end of a line or an arc whose run Extension follows, or a line Parallel runs alongside. It is drawn as a small cross, a
    /// line's as the marker of Parallel.
    /// </summary>
    public sealed class CadAcquiredPoint
    {
        internal CadAcquiredPoint(CadSnapKind kind, GeoPoint2 point, string handle)
        {
            Kind = kind;
            Point = point;
            Handle = handle;
        }

        /// <summary>Gets the snap that found it: an endpoint, a midpoint, <see cref="CadSnapKind.Extension"/> for the end of a run, <see cref="CadSnapKind.Parallel"/> for a line.</summary>
        public CadSnapKind Kind { get; }

        /// <summary>Gets the point, in drawing units: on a line acquired for Parallel, the point the cursor rested on.</summary>
        public GeoPoint2 Point { get; }

        /// <summary>Gets the handle of the object it lies on.</summary>
        public string Handle { get; }

        /// <summary>The unit direction a line's end runs on in, outward; null where the point is no end of a line.</summary>
        internal GeoVector2? Along { get; set; }

        /// <summary>The arc whose end the point is; null where it is none.</summary>
        internal DisplayArc2 Arc { get; set; }

        /// <summary>Whether the point is the end of <see cref="Arc"/> at its start angle, the run then going back against the sweep.</summary>
        internal bool AtArcStart { get; set; }

        /// <summary>The unit direction of a line acquired for Parallel; null for a point.</summary>
        internal GeoVector2? Direction { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Kind + " " + Point;
    }

    /// <summary>
    /// Object snaps, ortho, polar and grid snap, as AutoCAD's: given the cursor, the size of the aperture and the point a
    /// rubber band is drawn from, the point a click takes. Snaps are worked out from the display items under the aperture,
    /// through a space's <see cref="PickIndex"/>, so an INSERT's lines and arcs are snapped to where it places them.
    /// </summary>
    /// <remarks>
    /// AutoCAD's order is kept: a snap to a point near the cursor (endpoint, midpoint, centre, node, quadrant, intersection,
    /// apparent intersection, insertion, geometric centre, the crossing of two tracking lines) wins, the nearest of them
    /// first; then a snap the object under the cursor gives wherever its point lies (perpendicular, tangent, the centre of a
    /// circle whose rim is under the cursor, the centroid of a closed polyline under it, the insertion point of a block or a
    /// text under it, the apparent intersection with an object clicked first); then a point on an extension, a parallel or a
    /// tracking line near the cursor; then the object offered as the first of an extended apparent intersection; then
    /// nearest. A POINT is a node, not an endpoint, as in AutoCAD. An XLINE and a RAY are snapped along their whole length,
    /// as lines without end: nearest, perpendicular and their crossings however far from their base points; the base point
    /// of a RAY is an endpoint and the root point of an XLINE a midpoint, as AutoCAD gives them. Where no object snap is
    /// found, the cursor is put on the grid, then held level or upright (ortho) or on a polar angle from the base point.
    /// <para>
    /// Extension, Parallel and object snap tracking work from points acquired by resting the cursor (<see cref="Acquire"/>):
    /// seven at most, the oldest let go first, each let go when the cursor rests on it again, and all of them by
    /// <see cref="ClearAcquired"/>, which the window calls once a point is taken.
    /// </para>
    /// </remarks>
    public sealed partial class ObjectSnap
    {
        // The snaps of the point being worked out: the override, else the modes while snaps are on.
        private CadSnapModes _active;

        private IReadOnlyList<double> _additionalPolarAngles = Array.Empty<double>();

        /// <summary>Makes the snaps of AutoCAD's default: endpoint, midpoint, centre, quadrant and intersection, on.</summary>
        /// <param name="tolerance">The tolerance of the comparisons: whether lines are parallel, whether a point lies on an arc's sweep.</param>
        public ObjectSnap(Tolerance tolerance)
        {
            Tolerance = tolerance;
            Modes =
                CadSnapModes.Endpoint
                | CadSnapModes.Midpoint
                | CadSnapModes.Center
                | CadSnapModes.Quadrant
                | CadSnapModes.Intersection;
            IsOn = true;
            PolarAngle = Math.PI / 2.0;
            GridX = 10.0;
            GridY = 10.0;
        }

        /// <summary>Gets the tolerance of the comparisons.</summary>
        public Tolerance Tolerance { get; }

        /// <summary>Gets or sets the object snaps that may be taken.</summary>
        public CadSnapModes Modes { get; set; }

        /// <summary>Gets or sets whether object snaps are on (OSNAP, F3).</summary>
        public bool IsOn { get; set; }

        /// <summary>
        /// Gets or sets the object snaps of the next point alone, as AutoCAD's override typed at a point prompt (END, MID) or
        /// chosen from the Shift+right-click menu: taken in place of <see cref="Modes"/>, whether <see cref="IsOn"/> is on or
        /// off; <see cref="CadSnapModes.None"/> takes no object snap for that point (NONe). Null, as it is until set, for no
        /// override. The window clears it once the point is taken.
        /// </summary>
        public CadSnapModes? Override { get; set; }

        /// <summary>
        /// Gets or sets whether object snap tracking is on (OTRACK, F11): from each point acquired, lines run level and upright,
        /// or on every polar angle while <see cref="Polar"/> and <see cref="TracksPolarAngles"/> are on, and the cursor is held on one near it or where two cross.
        /// </summary>
        public bool Tracking { get; set; }

        /// <summary>Gets the points acquired, the oldest first; seven at most.</summary>
        public IReadOnlyList<CadAcquiredPoint> Acquired => _acquired;

        /// <summary>Gets or sets whether the cursor is held level or upright from the base point (ORTHO, F8).</summary>
        public bool Ortho { get; set; }

        /// <summary>Gets or sets whether the cursor is held on a polar angle from the base point when it comes near one (POLAR, F10).</summary>
        public bool Polar { get; set; }

        /// <summary>
        /// Gets or sets the direction an angle override typed at the prompt holds the cursor to, in radians counter-clockwise
        /// from the world's X axis, as <see cref="CadCommandSession.AngleOverride"/> gives it; null, as until set, for none.
        /// The cursor is held on the line through the base point at that angle, either side of it, over the grid, ortho and
        /// polar, and an object snap still wins, as AutoCAD 2021's help has it. With 30 degrees from the origin, the cursor at
        /// (100, 0) is held at (75, 43.3).
        /// </summary>
        public double? AngleOverride { get; set; }

        /// <summary>Gets or sets the polar increment in radians; a quarter turn until set.</summary>
        public double PolarAngle { get; set; }

        /// <summary>
        /// Gets or sets whether object snap tracking runs its lines from the points acquired on every polar angle while
        /// <see cref="Polar"/> is on, as AutoCAD's POLARMODE 2 (Track using all polar angle settings): off until set, when they
        /// run level and upright alone, as POLARMODE 0 (Track orthogonally only). With a polar angle of 45 and a point acquired
        /// at (100, 0), the cursor at (130, 30.4) is held on the line at 45 degrees only while it is on.
        /// </summary>
        public bool TracksPolarAngles { get; set; }

        /// <summary>
        /// Gets or sets whether the polar angles are counted from the last segment drawn (<see cref="LastSegment"/>), as
        /// AutoCAD's POLARMODE 1 (Relative to last segment): off until set, when they are counted from the X axis of the UCS
        /// turned by <see cref="SnapAngle"/>, as they are where no segment has been drawn. With a polar angle of 90 and the
        /// last line drawn at 30 degrees, the cursor is held at 30, 120, 210 and 300 degrees from the base point.
        /// </summary>
        public bool PolarFromLastSegment { get; set; }

        /// <summary>
        /// Gets or sets the direction of the last segment drawn, the way a line or the end of an arc ran, which the polar angles
        /// are counted from while <see cref="PolarFromLastSegment"/> is on; null, as until set, for none.
        /// </summary>
        public GeoVector2? LastSegment { get; set; }

        /// <summary>
        /// Gets or sets the additional polar angles, in radians from the X axis of the UCS, as AutoCAD's POLARADDANG while
        /// POLARMODE 4 is set: each held as it is beside the multiples of <see cref="PolarAngle"/>, but not its own multiples,
        /// and counted from the last segment too while <see cref="PolarFromLastSegment"/> is on; none until set. With a polar
        /// angle of 90 and 33 added, the cursor is held at 33 degrees from the base point, but not at 66.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public IReadOnlyList<double> AdditionalPolarAngles
        {
            get => _additionalPolarAngles;
            set => _additionalPolarAngles = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets whether a point is acquired for tracking only while Shift is held as the cursor rests on it, as AutoCAD's
        /// POLARMODE 8; off until set, when resting on it is enough. The window's canvas honours it.
        /// </summary>
        public bool AcquiresWithShift { get; set; }

        /// <summary>Gets or sets whether the marker of a snap is drawn at its point, as AutoCAD's AUTOSNAP 1; on until set.</summary>
        public bool ShowsMarker { get; set; } = true;

        /// <summary>Gets or sets whether the name of a snap is shown by the cursor, as AutoCAD's AUTOSNAP 2 (the AutoSnap tooltip); on until set.</summary>
        public bool ShowsSnapTip { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the cursor is drawn locked on a snap's point, the rubber band running to it, as AutoCAD's
        /// AUTOSNAP 4 (the magnet); on until set. Off, the rubber band follows the cursor, and a click still takes the snap.
        /// </summary>
        public bool Magnet { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the tip by the cursor, "Ortho" with the distance and the angle from the base point, is shown while
        /// the cursor is held by ortho, polar or a tracking line, as AutoCAD's AUTOSNAP 32 (the tooltips of polar tracking, object snap tracking and
        /// Ortho mode); on until set.
        /// </summary>
        public bool ShowsTrackingTip { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the cursor is put on the snap grid (SNAP, F9), or, while <see cref="PolarSnap"/> is on, on
        /// steps along the polar angles.
        /// </summary>
        public bool GridSnap { get; set; }

        /// <summary>
        /// Gets or sets whether SNAP steps along the polar angles in place of the grid, as AutoCAD's SNAPTYPE 1 (PolarSnap):
        /// off until set, as SNAPTYPE 0. With SNAP and POLAR on, the cursor held on a polar angle from the base point is put
        /// at a whole number of <see cref="PolarDistance"/> from it, so that with a distance of 25 a cursor 37 along the
        /// level line from (0, 0) is put at (25, 0); off a polar angle, or with POLAR off, the cursor goes free.
        /// </summary>
        public bool PolarSnap { get; set; }

        /// <summary>
        /// Gets or sets the step of <see cref="PolarSnap"/>, as AutoCAD's POLARDIST: nought, as until set, or below steps by
        /// <see cref="GridX"/>, the snap X spacing. It steps along the lines of object snap tracking from the points
        /// acquired too.
        /// </summary>
        public double PolarDistance { get; set; }

        /// <summary>Gets or sets the spacing of the snap grid along X; 10 until set.</summary>
        public double GridX { get; set; }

        /// <summary>Gets or sets the spacing of the snap grid along Y; 10 until set.</summary>
        public double GridY { get; set; }

        /// <summary>Gets or sets the point the snap grid runs through, in the coordinates of <see cref="Ucs"/>, as AutoCAD's SNAPBASE; the origin until set.</summary>
        public GeoPoint2 GridOrigin { get; set; } = GeoPoint2.Origin;

        /// <summary>
        /// Gets or sets the angle the snap grid is turned by about <see cref="GridOrigin"/>, in radians from the X axis of
        /// <see cref="Ucs"/>, as AutoCAD's SNAPANG; nought until set. Ortho holds the cursor along the turned axes and the
        /// polar angles are counted from it, as AutoCAD's are. With a snap of 10 turned by 30 degrees, the cursor at (9, 4)
        /// is put on (8.6603, 5), the point 10 along the turned X axis.
        /// </summary>
        public double SnapAngle { get; set; }

        /// <summary>
        /// Gets or sets the plane of an isometric snap, as AutoCAD's SNAPSTYL 1 with SNAPISOPAIR; null, as until set, for the
        /// rectangular snap of SNAPSTYL 0. The snap points then lie where the 30, 90 and 150 degree lines cross, twice
        /// <see cref="GridY"/> apart along each, as AutoCAD keeps half the vertical spacing in the drawing: SNAP Style
        /// Isometric 5 keeps a spacing of 8.6603 by 2.5, and the points lie 5 apart. Ortho holds the cursor on the two axes of
        /// the plane, whether the snap grid is on or not.
        /// </summary>
        public CadIsoplane? Isoplane { get; set; }

        /// <summary>
        /// Gets or sets the user coordinate system the grid, ortho and polar tracking follow, as AutoCAD's do: the grid runs
        /// along its axes, ortho holds the cursor along them, and polar angles are measured from its X axis. Object snaps give
        /// world points whatever it is. The world until set; null sets the world.
        /// </summary>
        public CadUcs Ucs
        {
            get => _ucs;
            set => _ucs = value ?? CadUcs.World;
        }

        private CadUcs _ucs = CadUcs.World;

        /// <summary>Gets the point a click at the cursor takes.</summary>
        /// <param name="index">The index of the space under the cursor.</param>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <param name="aperture">How near a snap point or object must come, in drawing units: the aperture in pixels divided by the view's pixels per unit.</param>
        /// <param name="basePoint">The point the rubber band is drawn from, which perpendicular, tangent, ortho and polar work from; null for none.</param>
        /// <param name="drawnFrom">The point perpendicular and tangent are drawn from where there is no base point, as a dimension's first origin draws them from the last point (<see cref="CadPrompt.SnapFrom"/>); null for none.</param>
        /// <returns>The point and the snap that gave it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the index is null.</exception>
        public CadSnapResult Snap(
            PickIndex index,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint = null,
            GeoPoint2? drawnFrom = null
        )
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            _active = Override ?? (IsOn ? Modes : CadSnapModes.None);

            if (_active != CadSnapModes.None && aperture > 0.0)
            {
                CadSnapResult found = ObjectSnapAt(index, cursor, aperture, basePoint, drawnFrom);

                if (found != null)
                {
                    return found;
                }
            }

            // An angle override holds the cursor on its line through the base point, over the grid, ortho and polar.
            if (AngleOverride.HasValue && basePoint.HasValue)
            {
                var along = new GeoVector2(Math.Cos(AngleOverride.Value), Math.Sin(AngleOverride.Value));
                return new CadSnapResult(
                    CadSnapKind.None,
                    basePoint.Value + along * (cursor - basePoint.Value).DotProduct(along),
                    null
                );
            }

            // The grid, ortho and polar work in the user coordinate system, the point taken back to the world's at the end.
            CadUcs ucs = _ucs;
            GeoPoint2 point = ucs.ToUcs(cursor);
            basePoint = basePoint.HasValue ? ucs.ToUcs(basePoint.Value) : basePoint;
            CadSnapKind kind = CadSnapKind.None;
            bool turned = IsTurned();

            if (GridSnap && !PolarSnap && GridX > 0.0 && GridY > 0.0)
            {
                point =
                    Isoplane.HasValue ? OnIsometricGrid(point)
                    : turned ? OnTurnedGrid(point)
                    : new GeoPoint2(
                        GridOrigin.X
                            + Math.Round((point.X - GridOrigin.X) / GridX, MidpointRounding.AwayFromZero) * GridX,
                        GridOrigin.Y
                            + Math.Round((point.Y - GridOrigin.Y) / GridY, MidpointRounding.AwayFromZero) * GridY
                    );
                kind = CadSnapKind.Grid;
            }

            if (basePoint.HasValue)
            {
                GeoPoint2 from = basePoint.Value;
                double dx = point.X - from.X;
                double dy = point.Y - from.Y;

                if (Ortho)
                {
                    point =
                        turned || Isoplane.HasValue ? OnOrthoAxis(from, point)
                        : Math.Abs(dx) >= Math.Abs(dy) ? new GeoPoint2(point.X, from.Y)
                        : new GeoPoint2(from.X, point.Y);
                    kind = CadSnapKind.Ortho;
                }
                else if (Polar && PolarAngle > Tolerance.EqualAngleRad)
                {
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    double angle = Math.Atan2(dy, dx);
                    double held = Math.Round(angle / PolarAngle, MidpointRounding.AwayFromZero) * PolarAngle;
                    double along = length * Math.Cos(angle - held);
                    double off = Math.Abs(length * Math.Sin(angle - held));

                    // POLARMODE 1 and 4: the angles counted from the last segment, and the additional angles, the nearest
                    // kept; SNAPANG: the angles counted from the snap's.
                    if (turned || RelativeTurn().HasValue || AdditionalPolarAngles.Count > 0)
                    {
                        NearestPolar(angle, length, ref held, ref along, ref off);
                    }

                    // The cursor is held on the polar ray only when it comes within the aperture of it, as AutoCAD tracks it.
                    if (length > 0.0 && off <= aperture)
                    {
                        // PolarSnap: the distance along the ray taken to a whole number of steps, as AutoCAD's SNAPTYPE 1.
                        double step = PolarDistance > 0.0 ? PolarDistance : GridX;

                        if (GridSnap && PolarSnap && step > 0.0)
                        {
                            along = Math.Round(along / step, MidpointRounding.AwayFromZero) * step;
                        }

                        point = new GeoPoint2(from.X + along * Math.Cos(held), from.Y + along * Math.Sin(held));
                        kind = CadSnapKind.Polar;
                    }
                }
            }

            return new CadSnapResult(kind, ucs.ToWorld(point), null);
        }

        /// <summary>
        /// The angle, in the UCS, the polar angles are counted from while <see cref="PolarFromLastSegment"/> is on: that of the
        /// last segment drawn; null where it is off or no segment has been drawn, when they are counted from
        /// <see cref="SnapAngle"/>.
        /// </summary>
        private double? RelativeTurn()
        {
            if (!PolarFromLastSegment || !LastSegment.HasValue)
            {
                return null;
            }

            GeoVector2 way = _ucs.ToUcs(LastSegment.Value);
            return way.Length > Tolerance.EqualVector ? Math.Atan2(way.Y, way.X) : (double?)null;
        }

        /// <summary>Whether the snap is turned by <see cref="SnapAngle"/>: by more than the tolerance of angles either way of a whole turn.</summary>
        private bool IsTurned() => Math.Abs(Math.IEEERemainder(SnapAngle, 2.0 * Math.PI)) > Tolerance.EqualAngleRad;

        /// <summary>A point of the UCS on the snap grid turned by <see cref="SnapAngle"/> about <see cref="GridOrigin"/>: the nearest corner.</summary>
        private GeoPoint2 OnTurnedGrid(GeoPoint2 point)
        {
            double cos = Math.Cos(SnapAngle);
            double sin = Math.Sin(SnapAngle);
            double dx = point.X - GridOrigin.X;
            double dy = point.Y - GridOrigin.Y;
            double u = Math.Round((dx * cos + dy * sin) / GridX, MidpointRounding.AwayFromZero) * GridX;
            double v = Math.Round((dy * cos - dx * sin) / GridY, MidpointRounding.AwayFromZero) * GridY;
            return new GeoPoint2(GridOrigin.X + u * cos - v * sin, GridOrigin.Y + u * sin + v * cos);
        }

        /// <summary>
        /// A point of the UCS on the isometric snap grid: the nearest of the points where the 30, 90 and 150 degree lines,
        /// turned by <see cref="SnapAngle"/> about <see cref="GridOrigin"/>, cross, a side of twice <see cref="GridY"/> apart.
        /// The point lies in a triangle of three of them, so the nearest is a corner of the cell of the 30 and 150 degree
        /// steps it falls in; the first of the four, in the order tried, where two lie as near.
        /// </summary>
        private GeoPoint2 OnIsometricGrid(GeoPoint2 point)
        {
            double side = 2.0 * GridY;
            double half = Math.Sqrt(3.0) / 2.0;
            double cos = Math.Cos(SnapAngle);
            double sin = Math.Sin(SnapAngle);
            double dx = point.X - GridOrigin.X;
            double dy = point.Y - GridOrigin.Y;
            double x = dx * cos + dy * sin;
            double y = dy * cos - dx * sin;

            // The steps along 30 degrees (s) and 150 degrees (t): x = side * cos30 * (s - t), y = side / 2 * (s + t).
            double across = x / (side * half);
            double up = 2.0 * y / side;
            double s0 = Math.Floor((up + across) / 2.0);
            double t0 = Math.Floor((up - across) / 2.0);
            double bestX = 0.0;
            double bestY = 0.0;
            double best = double.PositiveInfinity;

            for (int k = 0; k < 4; k++)
            {
                double s = s0 + (k & 1);
                double t = t0 + (k >> 1);
                double cx = side * half * (s - t);
                double cy = side / 2.0 * (s + t);
                double far = (cx - x) * (cx - x) + (cy - y) * (cy - y);

                if (far < best)
                {
                    best = far;
                    bestX = cx;
                    bestY = cy;
                }
            }

            return new GeoPoint2(GridOrigin.X + bestX * cos - bestY * sin, GridOrigin.Y + bestX * sin + bestY * cos);
        }

        /// <summary>
        /// The cursor held by ortho on the axis through the base point nearest its way, the point taken square onto it: the
        /// axes of the snap turned by <see cref="SnapAngle"/>, or of the isometric plane; the first where both lie as near.
        /// </summary>
        private GeoPoint2 OnOrthoAxis(GeoPoint2 from, GeoPoint2 point)
        {
            OrthoAxes(out double first, out double second);
            double dx = point.X - from.X;
            double dy = point.Y - from.Y;
            double ux = Math.Cos(SnapAngle + first);
            double uy = Math.Sin(SnapAngle + first);
            double vx = Math.Cos(SnapAngle + second);
            double vy = Math.Sin(SnapAngle + second);
            double onU = dx * ux + dy * uy;
            double onV = dx * vx + dy * vy;
            return Math.Abs(onU) >= Math.Abs(onV)
                ? new GeoPoint2(from.X + onU * ux, from.Y + onU * uy)
                : new GeoPoint2(from.X + onV * vx, from.Y + onV * vy);
        }

        /// <summary>
        /// The two axes ortho holds the cursor on, in radians from the snap's X axis: across and up for the rectangular snap,
        /// those of the isometric plane for an isometric one (90 and 150 degrees on the left, 30 and 150 on the top, 30 and 90
        /// on the right).
        /// </summary>
        private void OrthoAxes(out double first, out double second)
        {
            switch (Isoplane)
            {
                case CadIsoplane.Left:
                    first = Math.PI / 2.0;
                    second = 5.0 * Math.PI / 6.0;
                    break;
                case CadIsoplane.Top:
                    first = Math.PI / 6.0;
                    second = 5.0 * Math.PI / 6.0;
                    break;
                case CadIsoplane.Right:
                    first = Math.PI / 6.0;
                    second = Math.PI / 2.0;
                    break;
                default:
                    first = 0.0;
                    second = Math.PI / 2.0;
                    break;
            }
        }

        /// <summary>
        /// The polar ray nearest the cursor's angle from the base point, among the multiples of the increment counted from the
        /// last segment (<see cref="RelativeTurn"/>) and the additional angles (<see cref="AdditionalPolarAngles"/>): the angle
        /// held, how far along it the cursor lies and how far off it. An additional angle is taken only where the cursor lies
        /// ahead along it and nearer it than to any multiple; the first in the list where two lie as near.
        /// </summary>
        private void NearestPolar(double angle, double length, ref double held, ref double along, ref double off)
        {
            double turn = RelativeTurn() ?? (IsTurned() ? SnapAngle : 0.0);
            held = turn + Math.Round((angle - turn) / PolarAngle, MidpointRounding.AwayFromZero) * PolarAngle;
            along = length * Math.Cos(angle - held);
            off = Math.Abs(length * Math.Sin(angle - held));

            foreach (double added in _additionalPolarAngles)
            {
                double ray = turn + added;
                double ahead = length * Math.Cos(angle - ray);
                double aside = Math.Abs(length * Math.Sin(angle - ray));

                if (ahead > 0.0 && aside < off)
                {
                    held = ray;
                    along = ahead;
                    off = aside;
                }
            }
        }

        /// <summary>
        /// The directions, in the UCS, of tracking or polar rays: the multiples of the increment counted from a turn, then each
        /// additional angle (<see cref="AdditionalPolarAngles"/>) where they are asked for.
        /// </summary>
        private List<double> Rays(int lines, double increment, double turn, bool added)
        {
            var rays = new List<double>(lines + (added ? _additionalPolarAngles.Count : 0));

            for (int k = 0; k < lines; k++)
            {
                rays.Add(turn + k * increment);
            }

            if (added)
            {
                foreach (double angle in _additionalPolarAngles)
                {
                    rays.Add(turn + angle);
                }
            }

            return rays;
        }

        private CadSnapResult ObjectSnapAt(
            PickIndex index,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            GeoPoint2? drawnFrom
        )
        {
            var found = new Found();
            Gather(index, cursor, aperture, basePoint ?? drawnFrom, found);
            List<Candidate> near = found.Near;
            var tracked = new List<Candidate>();
            var offered = new List<Candidate>();

            if (Has(CadSnapModes.ApparentIntersection))
            {
                AddApparent(found.Curves, cursor, aperture, near, found.OnObject, offered);
            }

            AddTracking(cursor, aperture, basePoint, near, tracked);
            Candidate best = Best(near)
                ?? Best(found.OnObject)
                ?? Best(tracked)
                ?? Best(offered)
                ?? Best(found.Nearest);
            return best == null
                ? null
                : new CadSnapResult(best.Kind, best.Point, best.Handle, best.Tracks, best.Source);
        }

        /// <summary>The snaps of the items under the aperture: the points near the cursor, those the objects under it give, the nearest points, and the curves under it.</summary>
        private void Gather(PickIndex index, GeoPoint2 cursor, double aperture, GeoPoint2? basePoint, Found found)
        {
            var box = new DisplayBox(
                cursor.X - aperture,
                cursor.Y - aperture,
                cursor.X + aperture,
                cursor.Y + aperture
            );
            IReadOnlyList<DisplayItem2> items = index.ItemsNear(box);
            List<Candidate> near = found.Near;
            List<Candidate> onObject = found.OnObject;
            List<Candidate> nearest = found.Nearest;
            List<Curve> curves = found.Curves;

            foreach (DisplayItem2 item in items)
            {
                switch (item)
                {
                    case DisplayPolyline2 chain:
                        AddChain(
                            chain.Xy,
                            chain.Count,
                            chain.Closed,
                            chain.IsSampled,
                            item,
                            cursor,
                            aperture,
                            basePoint,
                            near,
                            onObject,
                            nearest,
                            curves,
                            found.Ends
                        );
                        break;
                    case DisplayFill2 fill:
                        foreach (double[] ring in fill.Rings)
                        {
                            AddChain(
                                ring,
                                ring.Length / 2,
                                true,
                                false,
                                item,
                                cursor,
                                aperture,
                                basePoint,
                                near,
                                onObject,
                                nearest,
                                curves,
                                found.Ends
                            );
                        }

                        break;
                    case DisplayArc2 arc:
                        AddArc(arc, cursor, aperture, basePoint, near, onObject, nearest, curves, found.Ends);
                        break;
                    case DisplayRay2 ray:
                        AddRay(ray, cursor, aperture, basePoint, near, onObject, nearest, curves, found.Ends);
                        break;
                }

                if (Has(CadSnapModes.Insertion))
                {
                    AddInsertion(index.List, item, cursor, aperture, near, onObject);
                }
            }

            if (Has(CadSnapModes.Intersection))
            {
                AddIntersections(curves, cursor, aperture, near);
            }
        }

        /// <summary>
        /// The insertion points of the block or text an item is drawn for: near the cursor, or wherever they lie with the cursor
        /// on the item, as AutoCAD takes a block's base point from any of its lines.
        /// </summary>
        private static void AddInsertion(
            DisplayList2 list,
            DisplayItem2 item,
            GeoPoint2 cursor,
            double aperture,
            List<Candidate> near,
            List<Candidate> onObject
        )
        {
            IReadOnlyList<GeoPoint2> points = list.InsertionPointsOf(item.Handle);

            if (points.Count == 0)
            {
                return;
            }

            double reach = item.DistanceTo(cursor.X, cursor.Y);

            foreach (GeoPoint2 point in points)
            {
                Near(near, CadSnapKind.Insertion, point, item.Handle, cursor, aperture);

                if (reach <= aperture)
                {
                    onObject.Add(new Candidate(CadSnapKind.Insertion, point, item.Handle, reach));
                }
            }
        }

        private bool Has(CadSnapModes mode) => (_active & mode) != 0;

        private void AddChain(
            IReadOnlyList<double> xy,
            int count,
            bool closed,
            bool sampled,
            DisplayItem2 item,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            List<Candidate> near,
            List<Candidate> onObject,
            List<Candidate> nearest,
            List<Curve> curves,
            List<End> ends
        )
        {
            if (count == 0)
            {
                return;
            }

            if (count == 1)
            {
                // A chain of one point is a POINT: a node, as in AutoCAD, not an endpoint.
                if (Has(CadSnapModes.Node))
                {
                    Near(near, CadSnapKind.Node, new GeoPoint2(xy[0], xy[1]), item.Handle, cursor, aperture);
                }

                return;
            }

            // The walls, symbols and words of a feature control frame give no End, Mid, Intersection or Extension: its rows do, as
            // AutoCAD 2021 gives them (a line through a frame meets the sides of its rows, no wall; a wall meets no side).
            bool inside = item.IsFrameInside;

            if (!inside && Has(CadSnapModes.Endpoint))
            {
                for (int i = 0; i < count; i++)
                {
                    if (sampled && (closed || (i > 0 && i < count - 1)))
                    {
                        continue;
                    }

                    Near(
                        near,
                        CadSnapKind.Endpoint,
                        new GeoPoint2(xy[2 * i], xy[2 * i + 1]),
                        item.Handle,
                        cursor,
                        aperture
                    );
                }
            }

            int segments = closed && count > 2 ? count : count - 1;
            bool under = false;

            for (int i = 0; i < segments; i++)
            {
                var a = new GeoPoint2(xy[2 * i], xy[2 * i + 1]);
                int j = (i + 1) % count;
                var b = new GeoPoint2(xy[2 * j], xy[2 * j + 1]);

                if (!sampled && !inside && Has(CadSnapModes.Midpoint))
                {
                    Near(
                        near,
                        CadSnapKind.Midpoint,
                        new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0),
                        item.Handle,
                        cursor,
                        aperture
                    );
                }

                GeoPoint2 foot = Foot(cursor, a, b, out _);
                double reach = (foot - cursor).Length;

                if (ends != null && !sampled && !inside)
                {
                    AddEnd(ends, a, b, item.Handle, cursor, aperture, reach);
                    AddEnd(ends, b, a, item.Handle, cursor, aperture, reach);
                }

                if (reach > aperture)
                {
                    continue;
                }

                under = true;

                if (!inside)
                {
                    curves.Add(Curve.Segment(a, b, item.Handle, reach, foot, sampled));
                }

                // Nor Nearest or Perpendicular: AutoCAD 2021 gives them on the sides of a frame's rows alone (probe GHTH2).
                if (!inside && Has(CadSnapModes.Nearest))
                {
                    nearest.Add(new Candidate(CadSnapKind.Nearest, foot, item.Handle, reach));
                }

                if (!inside && basePoint.HasValue && Has(CadSnapModes.Perpendicular))
                {
                    GeoPoint2 perpendicular = Foot(basePoint.Value, a, b, out double t);
                    double slack = Tolerance.EqualPoint / Math.Max((b - a).Length, Tolerance.EqualPoint);

                    // A line or a straight segment of a polyline gives the foot on its line run on past either end, as
                    // AutoCAD 2021 gives it (a parameter of 1.75 from (20, 120) to (100, -20)-(100, 60)); a curve drawn in
                    // pieces and the edge of a fill only between their ends.
                    bool past = t < -slack || t > 1.0 + slack;

                    if (!past || (item is DisplayPolyline2 && !sampled && (b - a).Length > Tolerance.EqualPoint))
                    {
                        onObject.Add(
                            new Candidate(
                                CadSnapKind.Perpendicular,
                                past ? a + (b - a) * t : perpendicular,
                                item.Handle,
                                reach
                            )
                        );
                    }
                }
            }

            // The centroid of a closed polyline: near the cursor, or with the cursor on the polyline, as the centre of a circle is.
            if (
                Has(CadSnapModes.GeometricCenter)
                && !inside
                && closed
                && count > 2
                && item is DisplayPolyline2
                && Centroid(xy, count, out GeoPoint2 centroid)
            )
            {
                Near(near, CadSnapKind.GeometricCenter, centroid, item.Handle, cursor, aperture);

                if (under)
                {
                    onObject.Add(
                        new Candidate(CadSnapKind.GeometricCenter, centroid, item.Handle, (centroid - cursor).Length)
                    );
                }
            }
        }

        /// <summary>The centroid of the area a ring holds, worked out from its first point; false where the ring holds no area.</summary>
        private bool Centroid(IReadOnlyList<double> xy, int count, out GeoPoint2 centroid)
        {
            double x0 = xy[0];
            double y0 = xy[1];
            double twice = 0.0;
            double cx = 0.0;
            double cy = 0.0;
            double perimeter = 0.0;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                double ax = xy[2 * i] - x0;
                double ay = xy[2 * i + 1] - y0;
                double bx = xy[2 * j] - x0;
                double by = xy[2 * j + 1] - y0;
                double cross = ax * by - bx * ay;
                twice += cross;
                cx += (ax + bx) * cross;
                cy += (ay + by) * cross;
                perimeter += Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            }

            // A ring whose area is no more than a strip of the point tolerance along its outline holds none: a sliver has no centroid.
            if (Math.Abs(twice) / 2.0 <= Tolerance.EqualPoint * perimeter)
            {
                centroid = default;
                return false;
            }

            centroid = new GeoPoint2(x0 + cx / (3.0 * twice), y0 + cy / (3.0 * twice));
            return true;
        }

        private void AddArc(
            DisplayArc2 arc,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            List<Candidate> near,
            List<Candidate> onObject,
            List<Candidate> nearest,
            List<Curve> curves,
            List<End> ends
        )
        {
            var center = new GeoPoint2(arc.Cx, arc.Cy);
            double reach = arc.DistanceTo(cursor.X, cursor.Y);

            if (!arc.IsCircle)
            {
                arc.PointAt(arc.Start, out double sx, out double sy);
                arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
                arc.PointAt(arc.Start + arc.Sweep / 2.0, out double mx, out double my);

                if (ends != null)
                {
                    AddEnd(ends, arc, true, new GeoPoint2(sx, sy), cursor, aperture, reach);
                    AddEnd(ends, arc, false, new GeoPoint2(ex, ey), cursor, aperture, reach);
                }

                if (Has(CadSnapModes.Endpoint))
                {
                    Near(near, CadSnapKind.Endpoint, new GeoPoint2(sx, sy), arc.Handle, cursor, aperture);
                    Near(near, CadSnapKind.Endpoint, new GeoPoint2(ex, ey), arc.Handle, cursor, aperture);
                }

                if (Has(CadSnapModes.Midpoint))
                {
                    Near(near, CadSnapKind.Midpoint, new GeoPoint2(mx, my), arc.Handle, cursor, aperture);
                }
            }

            if (Has(CadSnapModes.Center))
            {
                Near(near, CadSnapKind.Center, center, arc.Handle, cursor, aperture);

                if (reach <= aperture)
                {
                    onObject.Add(new Candidate(CadSnapKind.Center, center, arc.Handle, reach));
                }
            }

            if (Has(CadSnapModes.Quadrant))
            {
                for (int q = 0; q < 4; q++)
                {
                    double angle = q * Math.PI / 2.0;
                    arc.PointAt(angle, out double qx, out double qy);

                    if (Covers(arc, angle))
                    {
                        Near(near, CadSnapKind.Quadrant, new GeoPoint2(qx, qy), arc.Handle, cursor, aperture);
                    }
                }
            }

            if (reach > aperture)
            {
                return;
            }

            curves.Add(Curve.Arc(arc, reach));

            if (Has(CadSnapModes.Nearest))
            {
                double angle = Math.Atan2(cursor.Y - arc.Cy, cursor.X - arc.Cx);
                GeoPoint2 on;

                if (Covers(arc, angle))
                {
                    arc.PointAt(angle, out double nx, out double ny);
                    on = new GeoPoint2(nx, ny);
                }
                else
                {
                    arc.PointAt(arc.Start, out double sx, out double sy);
                    arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
                    var s = new GeoPoint2(sx, sy);
                    var e = new GeoPoint2(ex, ey);
                    on = (s - cursor).Length <= (e - cursor).Length ? s : e;
                }

                nearest.Add(new Candidate(CadSnapKind.Nearest, on, arc.Handle, reach));
            }

            if (!basePoint.HasValue)
            {
                return;
            }

            GeoVector2 fromCenter = basePoint.Value - center;
            double d = fromCenter.Length;

            if (Has(CadSnapModes.Perpendicular) && d > Tolerance.EqualPoint)
            {
                double angle = Math.Atan2(fromCenter.Y, fromCenter.X);

                // Both points where the line through the centre meets the rim are perpendicular to it; the one nearer the cursor is taken.
                foreach (double a in new[] { angle, angle + Math.PI })
                {
                    if (Covers(arc, a))
                    {
                        arc.PointAt(a, out double px, out double py);
                        var foot = new GeoPoint2(px, py);
                        onObject.Add(
                            new Candidate(
                                CadSnapKind.Perpendicular,
                                foot,
                                arc.Handle,
                                reach + (foot - cursor).Length * 1E-9
                            )
                        );
                    }
                }
            }

            if (Has(CadSnapModes.Tangent) && d > arc.Radius + Tolerance.EqualPoint)
            {
                double angle = Math.Atan2(fromCenter.Y, fromCenter.X);
                double spread = Math.Acos(arc.Radius / d);

                foreach (double a in new[] { angle + spread, angle - spread })
                {
                    if (Covers(arc, a))
                    {
                        arc.PointAt(a, out double tx, out double ty);
                        var touch = new GeoPoint2(tx, ty);
                        onObject.Add(
                            new Candidate(
                                CadSnapKind.Tangent,
                                touch,
                                arc.Handle,
                                reach + (touch - cursor).Length * 1E-9
                            )
                        );
                    }
                }
            }
        }

        /// <summary>
        /// An XLINE or a RAY, as AutoCAD snaps to them: the base point of a ray is an endpoint, whose run Extension follows back
        /// behind it, and the root point of an xline a midpoint; along them, nearest, perpendicular and the crossings with what
        /// else lies under the cursor, however far from the base point. A ray along +X from (100, 0) gives the perpendicular
        /// from (500, 80) at (500, 0), and none from (0, 80), whose foot lies behind it.
        /// </summary>
        private void AddRay(
            DisplayRay2 ray,
            GeoPoint2 cursor,
            double aperture,
            GeoPoint2? basePoint,
            List<Candidate> near,
            List<Candidate> onObject,
            List<Candidate> nearest,
            List<Curve> curves,
            List<End> ends
        )
        {
            var origin = new GeoPoint2(ray.X, ray.Y);
            var way = new GeoVector2(ray.Dx, ray.Dy);
            double least = ray.IsBothWays ? double.NegativeInfinity : 0.0;
            GeoPoint2 foot = Foot(cursor, origin, way, least, out _);
            double reach = (foot - cursor).Length;

            if (ray.IsBothWays)
            {
                if (Has(CadSnapModes.Midpoint))
                {
                    Near(near, CadSnapKind.Midpoint, origin, ray.Handle, cursor, aperture);
                }
            }
            else
            {
                if (Has(CadSnapModes.Endpoint))
                {
                    Near(near, CadSnapKind.Endpoint, origin, ray.Handle, cursor, aperture);
                }

                double toBase = (origin - cursor).Length;

                if (ends != null && toBase <= aperture)
                {
                    ends.Add(
                        new End
                        {
                            Point = origin,
                            Handle = ray.Handle,
                            Reach = toBase,
                            OnIt = reach,
                            Along = -way,
                        }
                    );
                }
            }

            if (reach > aperture)
            {
                return;
            }

            curves.Add(Curve.Ray(origin, way, least, ray.Handle, reach, foot));

            if (Has(CadSnapModes.Nearest))
            {
                nearest.Add(new Candidate(CadSnapKind.Nearest, foot, ray.Handle, reach));
            }

            if (basePoint.HasValue && Has(CadSnapModes.Perpendicular))
            {
                // The way is of unit length, so the distance along it is in drawing units.
                GeoPoint2 perpendicular = Foot(basePoint.Value, origin, way, least, out double t);

                if (t >= least - Tolerance.EqualPoint)
                {
                    onObject.Add(new Candidate(CadSnapKind.Perpendicular, perpendicular, ray.Handle, reach));
                }
            }
        }

        private void AddIntersections(List<Curve> curves, GeoPoint2 cursor, double aperture, List<Candidate> near)
        {
            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    foreach (GeoPoint2 point in Cross(curves[i], curves[j]))
                    {
                        string handle =
                            string.CompareOrdinal(curves[i].Handle, curves[j].Handle) <= 0
                                ? curves[i].Handle
                                : curves[j].Handle;
                        Near(near, CadSnapKind.Intersection, point, handle, cursor, aperture);
                    }
                }
            }
        }

        /// <summary>Where two curves cross; <paramref name="extended"/>, where they would cross run on, segments as endless lines and arcs as whole circles.</summary>
        private IEnumerable<GeoPoint2> Cross(Curve a, Curve b, bool extended = false)
        {
            if (a.Radius < 0.0 && b.Radius < 0.0)
            {
                GeoVector2 r = a.Run;
                GeoVector2 s = b.Run;
                double denominator = r.X * s.Y - r.Y * s.X;
                double lengths = r.Length * s.Length;

                if (lengths <= 0.0 || Math.Abs(denominator) <= Tolerance.EqualVector * lengths)
                {
                    yield break;
                }

                GeoVector2 q = b.A - a.A;
                double t = (q.X * s.Y - q.Y * s.X) / denominator;
                double u = (q.X * r.Y - q.Y * r.X) / denominator;
                double slackT = Tolerance.EqualPoint / r.Length;
                double slackU = Tolerance.EqualPoint / s.Length;

                if (
                    extended
                    || (t >= a.Least - slackT && t <= a.Most + slackT && u >= b.Least - slackU && u <= b.Most + slackU)
                )
                {
                    yield return a.A + r * t;
                }

                yield break;
            }

            if (a.Radius >= 0.0 && b.Radius >= 0.0)
            {
                foreach (GeoPoint2 point in CircleCircle(a, b, extended))
                {
                    yield return point;
                }

                yield break;
            }

            Curve line = a.Radius < 0.0 ? a : b;
            Curve circle = a.Radius < 0.0 ? b : a;

            foreach (GeoPoint2 point in LineCircle(line, circle, extended))
            {
                yield return point;
            }
        }

        private IEnumerable<GeoPoint2> LineCircle(Curve line, Curve circle, bool extended)
        {
            GeoVector2 d = line.Run;
            double length = d.Length;

            if (length <= 0.0)
            {
                yield break;
            }

            GeoVector2 f = line.A - circle.Center;
            double a = d.X * d.X + d.Y * d.Y;
            double b = 2.0 * (f.X * d.X + f.Y * d.Y);
            double c = f.X * f.X + f.Y * f.Y - circle.Radius * circle.Radius;
            double discriminant = b * b - 4.0 * a * c;
            double slack = Tolerance.EqualPoint / length;

            if (discriminant < 0.0)
            {
                // A line within the tolerance of touching the circle touches it at the foot from the centre.
                GeoPoint2 foot = line.IsEndless
                    ? Foot(circle.Center, line.A, d, line.Least, out double tf)
                    : Foot(circle.Center, line.A, line.B, out tf);

                if (extended)
                {
                    // Run on, the line is endless: the foot is taken wherever it lies, the circle whole.
                    foot = line.A + d * (((circle.Center.X - line.A.X) * d.X + (circle.Center.Y - line.A.Y) * d.Y) / a);

                    if (Math.Abs((foot - circle.Center).Length - circle.Radius) <= Tolerance.EqualPoint)
                    {
                        yield return foot;
                    }

                    yield break;
                }

                if (
                    Math.Abs((foot - circle.Center).Length - circle.Radius) <= Tolerance.EqualPoint
                    && tf >= line.Least - slack
                    && tf <= line.Most + slack
                    && circle.Covers(foot, Tolerance)
                )
                {
                    yield return foot;
                }

                yield break;
            }

            double root = Math.Sqrt(discriminant);

            foreach (double t in new[] { (-b - root) / (2.0 * a), (-b + root) / (2.0 * a) })
            {
                GeoPoint2 point = line.A + d * t;

                if (extended || (t >= line.Least - slack && t <= line.Most + slack && circle.Covers(point, Tolerance)))
                {
                    yield return point;
                }
            }
        }

        private IEnumerable<GeoPoint2> CircleCircle(Curve a, Curve b, bool extended)
        {
            GeoVector2 between = b.Center - a.Center;
            double d = between.Length;

            if (
                d <= Tolerance.EqualPoint
                || d > a.Radius + b.Radius + Tolerance.EqualPoint
                || d < Math.Abs(a.Radius - b.Radius) - Tolerance.EqualPoint
            )
            {
                yield break;
            }

            double along = (a.Radius * a.Radius - b.Radius * b.Radius + d * d) / (2.0 * d);
            double h = Math.Sqrt(Math.Max(0.0, a.Radius * a.Radius - along * along));
            GeoVector2 unit = between / d;
            GeoPoint2 middle = a.Center + unit * along;
            var normal = new GeoVector2(-unit.Y, unit.X);
            var points =
                h <= Tolerance.EqualPoint ? new[] { middle } : new[] { middle + normal * h, middle - normal * h };

            foreach (GeoPoint2 point in points)
            {
                if (extended || (a.Covers(point, Tolerance) && b.Covers(point, Tolerance)))
                {
                    yield return point;
                }
            }
        }

        private bool Covers(DisplayArc2 arc, double angle)
        {
            if (arc.IsCircle)
            {
                return true;
            }

            arc.PointAt(angle, out double x, out double y);

            // Within the sweep, or within the point tolerance of either end of it.
            if (arc.CoversDirection(x, y))
            {
                return true;
            }

            arc.PointAt(arc.Start, out double sx, out double sy);
            arc.PointAt(arc.Start + arc.Sweep, out double ex, out double ey);
            double toStart = Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy));
            double toEnd = Math.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
            return Math.Min(toStart, toEnd) <= Tolerance.EqualPoint;
        }

        private static void Near(
            List<Candidate> list,
            CadSnapKind kind,
            GeoPoint2 point,
            string handle,
            GeoPoint2 cursor,
            double aperture
        )
        {
            double reach = (point - cursor).Length;

            if (reach <= aperture)
            {
                list.Add(new Candidate(kind, point, handle, reach));
            }
        }

        private static GeoPoint2 Foot(GeoPoint2 p, GeoPoint2 a, GeoPoint2 b, out double t)
        {
            GeoVector2 ab = b - a;
            double length2 = ab.X * ab.X + ab.Y * ab.Y;
            t = length2 <= 0.0 ? 0.0 : ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / length2;
            double clamped = Math.Max(0.0, Math.Min(1.0, t));
            return a + ab * clamped;
        }

        /// <summary>The foot of a point on a line without end from <paramref name="a"/> along a unit way, held at <paramref name="least"/> along it at the back: nought for a ray, minus infinity for an xline.</summary>
        private static GeoPoint2 Foot(GeoPoint2 p, GeoPoint2 a, GeoVector2 way, double least, out double t)
        {
            t = (p.X - a.X) * way.X + (p.Y - a.Y) * way.Y;
            return a + way * Math.Max(least, t);
        }

        /// <summary>
        /// The best of the candidates: the nearest, then by kind, handle, X and Y; of candidates equal in all of these, the
        /// first found, so that the choice never rests on how a sort treats a tie.
        /// </summary>
        private static Candidate Best(List<Candidate> list)
        {
            Candidate best = null;

            foreach (Candidate candidate in list)
            {
                if (best == null || Compare(candidate, best) < 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static int Compare(Candidate a, Candidate b)
        {
            int c = a.Reach.CompareTo(b.Reach);

            if (c == 0)
            {
                c = a.Kind.CompareTo(b.Kind);
            }

            if (c == 0)
            {
                c = string.CompareOrdinal(a.Handle, b.Handle);
            }

            if (c == 0)
            {
                c = a.Point.X.CompareTo(b.Point.X);
            }

            return c != 0 ? c : a.Point.Y.CompareTo(b.Point.Y);
        }

        /// <summary>What <see cref="Gather"/> finds under the aperture.</summary>
        private sealed class Found
        {
            public List<Candidate> Near { get; } = new List<Candidate>();

            public List<Candidate> OnObject { get; } = new List<Candidate>();

            public List<Candidate> Nearest { get; } = new List<Candidate>();

            public List<Curve> Curves { get; } = new List<Curve>();

            /// <summary>The ends of lines and arcs near the cursor, gathered only while a point is acquired; null otherwise.</summary>
            public List<End> Ends { get; set; }
        }

        private sealed class Candidate
        {
            public Candidate(
                CadSnapKind kind,
                GeoPoint2 point,
                string handle,
                double reach,
                IReadOnlyList<IReadOnlyList<GeoPoint2>> tracks = null,
                object source = null
            )
            {
                Kind = kind;
                Point = point;
                Handle = handle;
                Reach = reach;
                Tracks = tracks;
                Source = source;
            }

            public CadSnapKind Kind { get; }

            public GeoPoint2 Point { get; }

            public string Handle { get; }

            public double Reach { get; }

            public IReadOnlyList<IReadOnlyList<GeoPoint2>> Tracks { get; }

            public object Source { get; }
        }

        /// <summary>A straight segment (a radius below nought) or an arc, crossed with others for intersections.</summary>
        private sealed class Curve
        {
            private DisplayArc2 _arc;

            public GeoPoint2 A { get; private set; }

            public GeoPoint2 B { get; private set; }

            /// <summary>The way from <see cref="A"/> to <see cref="B"/>; of a line without end, its unit way exactly.</summary>
            public GeoVector2 Run { get; private set; }

            /// <summary>How far back along <see cref="Run"/> from <see cref="A"/> a straight curve reaches: nought for a segment and a ray, minus infinity for an xline.</summary>
            public double Least { get; private set; }

            /// <summary>How far on along <see cref="Run"/> from <see cref="A"/> a straight curve reaches: one for a segment, infinity for a ray and an xline.</summary>
            public double Most { get; private set; } = 1.0;

            /// <summary>Whether it is an XLINE or a RAY, whose <see cref="B"/> lies one unit along it from its base point.</summary>
            public bool IsEndless { get; private set; }

            public GeoPoint2 Center { get; private set; }

            public double Radius { get; private set; } = -1.0;

            public string Handle { get; private set; }

            /// <summary>How far the cursor lies from it.</summary>
            public double Reach { get; private set; }

            /// <summary>The point of a segment nearest the cursor; for an arc, see <see cref="NearestTo"/>.</summary>
            public GeoPoint2 Foot { get; private set; }

            /// <summary>Whether it is a piece of a curve drawn as a chain (a spline, an ellipse), whose run means nothing.</summary>
            public bool Sampled { get; private set; }

            public static Curve Segment(
                GeoPoint2 a,
                GeoPoint2 b,
                string handle,
                double reach,
                GeoPoint2 foot,
                bool sampled
            ) =>
                new Curve
                {
                    A = a,
                    B = b,
                    Run = b - a,
                    Handle = handle,
                    Reach = reach,
                    Foot = foot,
                    Sampled = sampled,
                };

            public static Curve Ray(
                GeoPoint2 origin,
                GeoVector2 way,
                double least,
                string handle,
                double reach,
                GeoPoint2 foot
            ) =>
                new Curve
                {
                    A = origin,
                    B = origin + way,
                    Run = way,
                    Least = least,
                    Most = double.PositiveInfinity,
                    IsEndless = true,
                    Handle = handle,
                    Reach = reach,
                    Foot = foot,
                };

            public static Curve Arc(DisplayArc2 arc, double reach) =>
                new Curve
                {
                    _arc = arc,
                    Center = new GeoPoint2(arc.Cx, arc.Cy),
                    Radius = arc.Radius,
                    Handle = arc.Handle,
                    Reach = reach,
                };

            /// <summary>The point of the curve nearest a point: the foot on a segment, on an arc the point at its angle, else the nearer end.</summary>
            public GeoPoint2 NearestTo(GeoPoint2 cursor)
            {
                if (_arc == null)
                {
                    return Foot;
                }

                double angle = Math.Atan2(cursor.Y - _arc.Cy, cursor.X - _arc.Cx);
                _arc.PointAt(angle, out double x, out double y);

                if (_arc.IsCircle || _arc.CoversDirection(x, y))
                {
                    return new GeoPoint2(x, y);
                }

                _arc.PointAt(_arc.Start, out double sx, out double sy);
                _arc.PointAt(_arc.Start + _arc.Sweep, out double ex, out double ey);
                var s = new GeoPoint2(sx, sy);
                var e = new GeoPoint2(ex, ey);
                return (s - cursor).Length <= (e - cursor).Length ? s : e;
            }

            public bool Covers(GeoPoint2 point, Tolerance tolerance)
            {
                if (_arc == null || _arc.IsCircle || _arc.CoversDirection(point.X, point.Y))
                {
                    return true;
                }

                _arc.PointAt(_arc.Start, out double sx, out double sy);
                _arc.PointAt(_arc.Start + _arc.Sweep, out double ex, out double ey);
                return (point - new GeoPoint2(sx, sy)).Length <= tolerance.EqualPoint
                    || (point - new GeoPoint2(ex, ey)).Length <= tolerance.EqualPoint;
            }
        }
    }
}
