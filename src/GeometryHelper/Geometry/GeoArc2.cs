using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a circular arc in the plane: part of a circle, running from one angle to another.
    /// <para>
    /// An arc is a curve, so it encloses nothing and has no area. It is held as a centre, a radius, the
    /// angle it starts at and the angle it sweeps through, signed: positive sweeps counter-clockwise and
    /// negative clockwise. A sweep of a whole turn is allowed and draws the full circle; a sweep of
    /// nothing is not, because an arc that goes nowhere has no direction and no length.
    /// </para>
    /// </summary>
    public readonly partial struct GeoArc2 : IEquatable<GeoArc2>
    {
        private const double FullTurn = Math.PI * 2.0;

        /// <summary>
        /// Gets the centre of the circle the arc lies on.
        /// </summary>
        public GeoPoint2 Center { get; }

        /// <summary>
        /// Gets the radius of the arc.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Gets the angle the arc starts at, in radians, measured counter-clockwise from the X axis and
        /// wrapped into the range from zero to a full turn.
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// Gets the angle the arc sweeps through, in radians: positive counter-clockwise, negative
        /// clockwise, never zero, and never more than a whole turn either way.
        /// </summary>
        public double SweptAngle { get; }

        /// <summary>
        /// Initializes an arc running counter-clockwise from one angle to another.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc2(GeoPoint2 center, double radius, double startAngle, double endAngle)
            : this(center, radius, startAngle, endAngle, false)
        {
        }

        /// <summary>
        /// Initializes an arc running from one angle to another, either way round.
        /// </summary>
        /// <param name="center">The centre of the circle the arc lies on.</param>
        /// <param name="radius">The radius; it must be positive.</param>
        /// <param name="startAngle">The angle the arc starts at, in radians.</param>
        /// <param name="endAngle">The angle the arc ends at, in radians. Equal angles mean a whole turn.</param>
        /// <param name="clockwise">true to sweep clockwise from the start angle; false to sweep counter-clockwise.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, or an angle is not a number.</exception>
        public GeoArc2(GeoPoint2 center, double radius, double startAngle, double endAngle, bool clockwise)
        {
            RequirePositive(radius, nameof(radius));
            RequireNumber(startAngle, nameof(startAngle));
            RequireNumber(endAngle, nameof(endAngle));

            Center = center;
            Radius = radius;
            StartAngle = Wrap(startAngle);

            double sweep = clockwise ? Wrap(startAngle - endAngle) : Wrap(endAngle - startAngle);

            // Going nowhere is not an arc; coming back to where it started is a whole turn.
            if (sweep <= 0.0)
            {
                sweep = FullTurn;
            }

            SweptAngle = clockwise ? -sweep : sweep;
        }

        /// <summary>
        /// Initializes an arc from its start angle and the angle it sweeps through.
        /// </summary>
        private GeoArc2(GeoPoint2 center, double radius, double startAngle, double sweptAngle, bool clockwise, bool bySweep)
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweptAngle = sweptAngle;
        }

        #region Factories

        /// <summary>
        /// Creates the arc through three points, using the default tolerance.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="middle">A point the arc passes through between its ends.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <returns>The arc from the start through the middle to the end.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the three points share a line, so no circle passes through them.</exception>
        public static GeoArc2 FromThreePoints(GeoPoint2 start, GeoPoint2 middle, GeoPoint2 end)
        {
            return FromThreePoints(start, middle, end, Tolerance.Global);
        }

        /// <summary>
        /// Creates the arc through three points, within a tolerance.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="middle">A point the arc passes through between its ends.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc from the start through the middle to the end.</returns>
        /// <remarks>
        /// Which way the arc turns follows the middle point: it sweeps the way that passes through it, so
        /// swapping the start and the end reverses the arc rather than giving the other part of the circle.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the three points share a line, so no circle passes through them.</exception>
        public static GeoArc2 FromThreePoints(GeoPoint2 start, GeoPoint2 middle, GeoPoint2 end, Tolerance tolerance)
        {
            GeoVector2 toMiddle = start.GetVectorTo(middle);
            GeoVector2 toEnd = start.GetVectorTo(end);

            double twiceArea = toMiddle.CrossProduct(toEnd);

            if (Math.Abs(twiceArea) <= tolerance.EqualPoint * tolerance.EqualPoint)
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            // The centre is where the perpendicular bisectors of the two chords meet, written in terms of
            // the squared lengths so that it needs no division until the end.
            double middleSquared = toMiddle.LengthSquared;
            double endSquared = toEnd.LengthSquared;
            double factor = 0.5 / twiceArea;

            var offset = new GeoVector2(
                (toEnd.Y * middleSquared - toMiddle.Y * endSquared) * factor,
                (toMiddle.X * endSquared - toEnd.X * middleSquared) * factor);

            GeoPoint2 center = start.Add(offset);
            double radius = offset.Length;

            // The arc turns the way that carries it through the middle point, which is the way the three
            // points wind.
            bool clockwise = twiceArea < 0.0;

            return new GeoArc2(center, radius, AngleOf(center, start), AngleOf(center, end), clockwise);
        }

        /// <summary>
        /// Creates the arc between two points with a given bulge, the number AutoCAD stores per vertex.
        /// </summary>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="end">Where the arc ends.</param>
        /// <param name="bulge">The tangent of a quarter of the angle the arc sweeps: positive counter-clockwise, negative clockwise.</param>
        /// <returns>The arc from the start to the end.</returns>
        /// <remarks>
        /// A bulge of one is a half turn, and the sign says which way it goes. A bulge of zero would be a
        /// straight segment rather than an arc, which is why it is refused here: a chain stores that as an
        /// edge with no bulge instead. The centre stands off the middle of the chord by c (1 - b²) / 4b and the
        /// radius is c (1 + b²) / 4|b|, worked out so that neither cancels: the chord of 200 000 with a bulge of
        /// 1 - 1E-8 has its centre 0.001 off the chord.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bulge is zero or not a number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the two points are the same, so there is no chord to bulge.</exception>
        public static GeoArc2 FromBulge(GeoPoint2 start, GeoPoint2 end, double bulge)
        {
            if (double.IsNaN(bulge) || bulge == 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(bulge), "A bulge of zero is a straight segment, not an arc.");
            }

            GeoVector2 chord = start.GetVectorTo(end);
            double chordLength = chord.Length;

            if (chordLength <= 0.0)
            {
                throw new InvalidOperationException("Two points in the same place have no chord to bulge.");
            }

            double sweep = 4.0 * Math.Atan(bulge);
            double magnitude = Math.Abs(bulge);
            double radius;
            double apothem;

            // The centre sits off the middle of the chord, to the left of it, by c (1 - b²) / 4b: on the side the bulge leans
            // away from up to a half turn, and past it on the side it leans to. Up to a bulge of a half the root of r² - c²/4
            // gives it within a few roundings and keeps the ends on the circle to the last digit, which an arc bowing a
            // ten-millionth of its chord, of radius 2.5E8, needs to cross another where it does. Past that it cancels: a chord
            // of 200 000 with a bulge of 1 - 1E-8 put the centre on the chord, 0.001 off where it is. There the centre and the
            // radius, c (1 + b²) / 4|b|, are worked out as they stand; the radius as the chord over twice the sine of half the
            // sweep cancelled near a whole turn, where that sine is the rounding of the sweep away from nought.
            if (magnitude <= 0.5)
            {
                radius = Math.Abs(chordLength / (2.0 * Math.Sin(sweep * 0.5)));
                apothem = Math.Sqrt(Math.Max(0.0, radius * radius - chordLength * chordLength * 0.25)) * Math.Sign(bulge);
            }
            else
            {
                radius = chordLength * (1.0 / magnitude + magnitude) * 0.25;
                apothem = chordLength * (1.0 - bulge) * ((1.0 + bulge) / (4.0 * bulge));
            }

            GeoVector2 unit = chord.Multiply(1.0 / chordLength);
            GeoVector2 toCenter = new GeoVector2(-unit.Y, unit.X).Multiply(apothem);

            GeoPoint2 middleOfChord = new GeoPoint2((start.X + end.X) * 0.5, (start.Y + end.Y) * 0.5);
            GeoPoint2 center = middleOfChord.Add(toCenter);

            return new GeoArc2(center, radius, AngleOf(center, start), sweep, sweep < 0.0, true);
        }

        #endregion

        #region Measurements

        /// <summary>
        /// Gets the angle the arc ends at, in radians, wrapped into the range from zero to a full turn.
        /// </summary>
        public double EndAngle => Wrap(StartAngle + SweptAngle);

        /// <summary>
        /// Gets a value indicating whether the arc sweeps clockwise.
        /// </summary>
        public bool IsClockwise => SweptAngle < 0.0;

        /// <summary>
        /// Gets the length of the arc.
        /// </summary>
        public double Length => Math.Abs(SweptAngle) * Radius;

        /// <summary>
        /// Gets the bulge of the arc: the tangent of a quarter of the angle it sweeps, the number AutoCAD
        /// stores per vertex, signed the same way as <see cref="SweptAngle"/>.
        /// </summary>
        public double Bulge => Math.Tan(SweptAngle * 0.25);

        /// <summary>
        /// Gets the point the arc starts at.
        /// </summary>
        public GeoPoint2 StartPoint => GetPointAtAngle(StartAngle);

        /// <summary>
        /// Gets the point the arc ends at.
        /// </summary>
        public GeoPoint2 EndPoint => GetPointAtAngle(StartAngle + SweptAngle);

        /// <summary>
        /// Gets the point halfway along the arc.
        /// </summary>
        public GeoPoint2 MidPoint => GetPointAtAngle(StartAngle + SweptAngle * 0.5);

        /// <summary>
        /// Gets the chord of the arc: the straight segment from where it starts to where it ends.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the arc sweeps a whole turn, so its ends are in the same place.</exception>
        public GeoLine2 GetChord() => new GeoLine2(StartPoint, EndPoint);

        /// <summary>
        /// Gets the circle the arc lies on.
        /// </summary>
        public GeoCircle2 GetCircle() => new GeoCircle2(Center, Radius);

        /// <summary>
        /// Gets the point of the arc at an angle, whether or not the arc reaches that far.
        /// </summary>
        /// <param name="angleRad">The angle in radians, measured counter-clockwise from the X axis.</param>
        public GeoPoint2 GetPointAtAngle(double angleRad)
        {
            return new GeoPoint2(
                Center.X + Radius * Math.Cos(angleRad),
                Center.Y + Radius * Math.Sin(angleRad));
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the arc, where 0 is its start and 1 its end.
        /// </summary>
        /// <param name="parameter">The parameter. Values outside 0 to 1 run on around the circle.</param>
        public GeoPoint2 GetPointAtParameter(double parameter) => GetPointAtAngle(StartAngle + SweptAngle * parameter);

        /// <summary>
        /// Gets the point at an arc length measured from the start of the arc.
        /// </summary>
        /// <param name="distance">The length along the arc. Values outside its length run on around the circle.</param>
        public GeoPoint2 GetPointAtDistance(double distance) => GetPointAtParameter(distance / Length);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the arc.</param>
        /// <returns>The parameter, from 0 at the start of the arc to 1 at its end, clamped to that range.</returns>
        public double GetParameterAtPoint(GeoPoint2 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point of the arc nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the arc.</param>
        /// <param name="tolerance">The tolerance: a point at the centre has no direction and gives the start.</param>
        /// <returns>The parameter, from 0 at the start of the arc to 1 at its end, clamped to that range.</returns>
        /// <remarks>
        /// A point off the ends of the arc is answered with whichever end is nearer it, so the parameter
        /// never runs outside the arc.
        /// </remarks>
        public double GetParameterAtPoint(GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 fromCenter = Center.GetVectorTo(point);

            if (fromCenter.Length <= tolerance.EqualPoint)
            {
                return 0.0;
            }

            double angle = Math.Atan2(fromCenter.Y, fromCenter.X);
            double along = IsClockwise ? Wrap(StartAngle - angle) : Wrap(angle - StartAngle);
            double sweep = Math.Abs(SweptAngle);

            if (along <= sweep)
            {
                return along / sweep;
            }

            // Beyond the end: the far side of the circle belongs to whichever end of the arc is nearer.
            double past = along - sweep;
            double back = FullTurn - along;

            return past <= back ? 1.0 : 0.0;
        }

        /// <summary>
        /// Gets the arc length from the start of the arc to the point of it nearest a point.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point) => GetParameterAtPoint(point) * Length;

        /// <summary>
        /// Gets the arc length from the start of the arc to the point of it nearest a point, within a tolerance.
        /// </summary>
        public double GetDistanceAtPoint(GeoPoint2 point, Tolerance tolerance) => GetParameterAtPoint(point, tolerance) * Length;

        /// <summary>
        /// Gets the arc length from the start of the arc to a normalized parameter.
        /// </summary>
        public double GetDistanceAtParameter(double parameter) => parameter * Length;

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of the arc.
        /// </summary>
        public double GetParameterAtDistance(double distance) => distance / Length;

        #endregion

        #region Shaping

        /// <summary>
        /// Gets the arc running the other way, from this one's end to its start.
        /// </summary>
        public GeoArc2 Reverse() => new GeoArc2(Center, Radius, EndAngle, -SweptAngle, !IsClockwise, true);

        /// <summary>
        /// Creates a copy of this arc.
        /// </summary>
        /// <remarks>
        /// Arc is a readonly struct, so plain assignment already produces an independent copy and this
        /// method is not needed to avoid sharing. It exists so that every geometry type offers the same way
        /// to ask for a copy.
        /// </remarks>
        public GeoArc2 Clone() => new GeoArc2(Center, Radius, StartAngle, SweptAngle, IsClockwise, true);

        /// <summary>
        /// Moves the arc by a vector.
        /// </summary>
        public GeoArc2 Translate(GeoVector2 vector) => new GeoArc2(Center.Add(vector), Radius, StartAngle, SweptAngle, IsClockwise, true);

        /// <summary>
        /// Turns the arc about a point.
        /// </summary>
        /// <param name="angleRad">The angle in radians, counter-clockwise.</param>
        /// <param name="center">The point to turn about.</param>
        public GeoArc2 RotateBy(double angleRad, GeoPoint2 center)
        {
            GeoVector2 fromCenter = center.GetVectorTo(Center).RotateBy(angleRad);

            return new GeoArc2(center.Add(fromCenter), Radius, StartAngle + angleRad, SweptAngle, IsClockwise, true);
        }

        /// <summary>
        /// Applies a transformation to this arc.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The transformed arc.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would skew or stretch the arc into part of an ellipse, so that the arc through where
        /// its ends and its middle land would land further than the global point tolerance from where its points are put, or,
        /// for an arc of a whole turn, would skew or stretch it, as <see cref="GeoTransform2.Transform(GeoCircle2)"/> refuses a
        /// circle.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a skew, a transformation <see cref="GeoTransform2.Transform(GeoCircle2)"/> would refuse for a circle, brings
        /// the two ends of an arc past a half turn within the global point tolerance of each other, so that no arc can be told
        /// through them and its middle.
        /// </exception>
        /// <remarks>
        /// A turn, a shift, a mirror or an even scaling moves the arc whole, by its centre and its radius, so an arc of any
        /// sweep moves; one that turns shapes round, a mirror or a negative scaling, also reverses the way the arc sweeps. One
        /// whose axes are one length and square to each other only to a share of some 1E-9, the rounding of a matrix built
        /// from axes, moves it whole where it lands within the global point tolerance so, and is read as a stretch otherwise:
        /// X stretched by a share of 4E-10 and scaled by 1 000 took the arc of radius 1E5 sweeping 6 radians to one 0.04 off,
        /// and refuses it. Any other transformation, a skew or a stretch, makes part of an ellipse of the arc, and the arc
        /// through where its two ends and its middle land, as in space (<see cref="GeoArc3.TransformBy(GeoTransform3)"/>), stands
        /// for it only where it lands within the global point tolerance of where the transformation puts the rest of it,
        /// everywhere along it, and is refused otherwise: asked at every sixteenth of its sweep only, the arc of radius 87.66
        /// from 0.2 radians sweeping 1.51, under X stretched by 1 + 1.793E-6 and scaled by 84.1, landed 0.0010114 off between two
        /// of them, and was moved. The skew that sends Y to (0.000001, 1) moves the arc of radius 100 from 0.5 to 2
        /// radians 0.0000069 off, and refuses it scaled by 1 000, where it would be 0.0069 off; the skew that sends X to (1, 0)
        /// and Y to (0.6, 0.8) refuses it, the arc of radius 66.09 through its moved ends and middle standing far off the
        /// ellipse it becomes. A skew that keeps both axes one length is judged as a stretch is: a turn of 0.7529 radians of a stretch of
        /// 1 + 4.29E-9 and a shear of 9.28E-5, scaled by 55.57, took the arc of radius 9 069.6 to one 9.16 off, and refuses it.
        /// A stretch of 3E-6 along X moves the arc of radius 100 from 0.5 to 2 radians at the scale it is drawn, 0.000017 off,
        /// and refuses it scaled by 1 000, where it would be 0.017 off. Each point is read from the moved start across the arc,
        /// so an arc bowing a hair off its chord is judged as surely as a half circle. An arc of a whole turn has no ends to go
        /// through: it is the whole circle, and is read as its circle is (<see cref="GeoTransform2.Transform(GeoCircle2)"/>);
        /// an arc past a half turn whose ends the transformation brings within the global point tolerance of each other is
        /// refused under a skew too. Where the ends come that close under a transformation that carries a circle, a turn whose
        /// numbers are rounded to six places, the arc is moved whole as the whole turn is: the arc of radius 100 sweeping a whole
        /// turn less 1E-9 under such a turn moves, where it threw.
        /// </remarks>
        public GeoArc2 TransformBy(GeoTransform2 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoVector2 alongX = transform.Transform(GeoVector2.XAxis);
            GeoVector2 alongY = transform.Transform(GeoVector2.YAxis);

            // Axes of unequal length stretch the arc into part of an ellipse.
            bool stretched = !GeoTransform2.EquallyLong(alongX, alongY);

            // A whole turn is the whole circle, refused under a skew as the circle is. Any other arc is carried by its centre
            // where the axes also come out square and of one length to rounding, or where its ends come together as a whole
            // turn's do and the circle would be carried, and is otherwise the arc through where its ends and its middle land.
            if (Math.Abs(SweptAngle) >= FullTurn)
            {
                transform.Transform(GetCircle());

                return MovedWhole(transform, alongX);
            }

            bool round = GeoTransform2.KeepsRound(alongX, alongY);

            if (round || (EndsMeetUnder(transform) && GeoTransform2.KeepsCircles(alongX, alongY, Radius)))
            {
                GeoArc2 whole = MovedWhole(transform, alongX);

                // Round to the rounding of a matrix built from axes, the axes can still be a share of some 1E-9 apart, which
                // takes an arc moved to a radius of 1E8 up to 0.04 off: where its circle would not be carried, it is moved whole
                // only where it lands within the tolerance, and is otherwise read as under a stretch.
                if (!round || GeoTransform2.KeepsCircles(alongX, alongY, Radius) || LandsWholeWithin(transform, whole))
                {
                    return whole;
                }
            }

            if (stretched && EndsMeetUnder(transform))
            {
                throw new InvalidOperationException("This transformation stretches the arc into part of an ellipse.");
            }

            GeoArc2 through = ThroughMoved(transform);

            if (!LandsWithin(transform, through))
            {
                throw new InvalidOperationException("This transformation stretches the arc into part of an ellipse.");
            }

            return through;
        }

        /// <summary>
        /// Determines whether this arc, moved whole, lands within the global point tolerance of where a transformation puts its
        /// points everywhere along it, the moved arc's circle read by its own centre and radius.
        /// </summary>
        private bool LandsWholeWithin(GeoTransform2 transform, GeoArc2 moved)
        {
            GeoVector2 toCentre = transform.Transform(StartPoint).GetVectorTo(moved.Center);
            double reach = toCentre.Length;

            return LandsWithin(transform, toCentre, moved.Radius, (reach - moved.Radius) * (reach + moved.Radius));
        }

        /// <summary>
        /// Gets the arc moved whole, by its centre and its radius, as a turn, a shift, a mirror or an even scaling moves it.
        /// </summary>
        private GeoArc2 MovedWhole(GeoTransform2 transform, GeoVector2 alongX)
        {
            GeoPoint2 center = transform.Transform(Center);
            GeoPoint2 start = transform.Transform(StartPoint);
            GeoPoint2 middle = transform.Transform(MidPoint);

            bool mirrored = transform.GetDeterminant() < 0.0;
            double sweep = mirrored ? -SweptAngle : SweptAngle;

            return new GeoArc2(center, Radius * alongX.Length, AngleOf(center, start), sweep, sweep < 0.0, true)
                .WithMiddleThrough(middle);
        }

        /// <summary>
        /// Determines whether this arc, moved to another, lands within the global point tolerance of where a transformation
        /// puts its points, everywhere along it: asked at every sixteenth of its sweep, and between them by how far it can
        /// stray (<see cref="Core.ArcLanding"/>).
        /// </summary>
        /// <remarks>
        /// Each point is read from the moved start, across the arc, and its distance from the moved circle is worked out from
        /// there as well, the difference of two squares over their sum, so that an arc bowing a hair off its chord, its radius
        /// far past the reach of its digits, is read as surely as a half circle: the edge 100 long bowing by 1E-12, of radius
        /// 2.5E13, read from its centre, stood a few thousandths off where it lands under a turn rounded to six places.
        /// </remarks>
        internal bool LandsWithin(GeoTransform2 transform, GeoArc2 moved)
        {
            GeoVector2 toCentre = transform.Transform(StartPoint).GetVectorTo(moved.Center);

            return LandsWithin(transform, toCentre, toCentre.Length, 0.0);
        }

        /// <summary>
        /// Determines whether this arc lands within the global point tolerance of a circle everywhere along it under a
        /// transformation (<see cref="LandsWithin(GeoTransform2, GeoArc2)"/>).
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <param name="toCentre">The circle's centre, read from where the start lands.</param>
        /// <param name="radius">The circle's radius.</param>
        /// <param name="shift">
        /// The square of how far the centre stands from where the start lands, less the square of the radius: nought for an
        /// arc through where the start lands.
        /// </param>
        private bool LandsWithin(GeoTransform2 transform, GeoVector2 toCentre, double radius, double shift)
        {
            double tolerance = Tolerance.Global.EqualPoint;
            var gaps = new double[17];

            // Read off the circle the moved arc lies on, which an arc shorter than the tolerance has as surely as any other.
            for (int i = 1; i < 16; i++)
            {
                GeoVector2 across = transform.Transform(Across(StartAngle, SweptAngle * (i / 16.0)));
                double off = (across.LengthSquared - 2.0 * across.DotProduct(toCentre) + shift) / (across.Subtract(toCentre).Length + radius);

                if (Math.Abs(off) > tolerance)
                {
                    return false;
                }

                gaps[i] = across.LengthSquared - 2.0 * across.DotProduct(toCentre) + shift;
            }

            // The two ends, which an arc through where they land lands on, and an arc moved whole may not.
            GeoVector2 toEnd = transform.Transform(Across(StartAngle, SweptAngle));
            gaps[0] = shift;
            gaps[16] = toEnd.LengthSquared - 2.0 * toEnd.DotProduct(toCentre) + shift;

            if (Math.Abs(gaps[0] / (toCentre.Length + radius)) > tolerance
                || Math.Abs(gaps[16] / (toEnd.Subtract(toCentre).Length + radius)) > tolerance)
            {
                return false;
            }

            // The axes of the plane the radius long, carried: what the stray between the points asked is bounded by.
            GeoVector2 p = transform.Transform(GeoVector2.XAxis).Multiply(Radius);
            GeoVector2 q = transform.Transform(GeoVector2.YAxis).Multiply(Radius);
            double bend = Core.ArcLanding.Bend(p.X, p.Y, 0.0, q.X, q.Y, 0.0, StartAngle, toCentre.X, toCentre.Y, 0.0);
            var heights = new double[17];

            if (Core.ArcLanding.Plainly(heights, gaps, SweptAngle, radius, bend, 0.0, tolerance))
            {
                return true;
            }

            GeoArc2 arc = this;
            Func<double, (double Height, double Gap)> at = share =>
            {
                GeoVector2 across = transform.Transform(arc.Across(arc.StartAngle, arc.SweptAngle * share));
                return (0.0, across.LengthSquared - 2.0 * across.DotProduct(toCentre) + shift);
            };

            return Core.ArcLanding.Holds(at, heights, gaps, SweptAngle, radius, bend, 0.0, tolerance);
        }

        /// <summary>
        /// Gets the arc through where this one's ends and its middle land under a transformation, the middle read across
        /// the chord from the start, so that an arc bowing a thousandth of its chord off it is not three points on a line.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the ends land within the point tolerance of each other.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the transformation lays the arc flat on a line.</exception>
        private GeoArc2 ThroughMoved(GeoTransform2 transform)
        {
            GeoPoint2 start = transform.Transform(StartPoint);
            GeoVector2 toEnd = transform.Transform(Across(StartAngle, SweptAngle));
            GeoVector2 toMiddle = transform.Transform(Across(StartAngle, SweptAngle * 0.5));

            if (EndsMeetUnder(transform))
            {
                throw new ArgumentException("This transformation brings the ends of the arc together, so no arc runs through them and its middle.", nameof(transform));
            }

            if (!Arc2.TryBulgeThrough(toEnd, toMiddle, out double bulge))
            {
                throw new InvalidOperationException("Three points on one line have no arc through them.");
            }

            return FromBulge(start, start.Add(toEnd), bulge);
        }

        /// <summary>
        /// Determines whether the arc sweeps past a half turn and a transformation brings its ends within the global point
        /// tolerance of each other: a hair short of a whole turn, whose chord says nothing about which way round it runs.
        /// </summary>
        private bool EndsMeetUnder(GeoTransform2 transform)
            => Math.Abs(SweptAngle) > Math.PI && !(transform.Transform(Across(StartAngle, SweptAngle)).Length > Tolerance.Global.EqualPoint);

        /// <summary>
        /// Gets the chord from the point of the arc's circle at one angle to the point a turn further round.
        /// </summary>
        private GeoVector2 Across(double from, double turn)
        {
            double half = turn * 0.5;
            double toward = from + half;
            double chord = 2.0 * Radius * Math.Sin(half);

            return new GeoVector2(-chord * Math.Sin(toward), chord * Math.Cos(toward));
        }

        /// <summary>
        /// Returns this arc, or the one sweeping the other way, whichever passes nearer a point.
        /// </summary>
        private GeoArc2 WithMiddleThrough(GeoPoint2 middle)
        {
            GeoArc2 other = new GeoArc2(Center, Radius, StartAngle, -Math.Sign(SweptAngle) * (FullTurn - Math.Abs(SweptAngle)), SweptAngle > 0.0, true);

            return MidPoint.GetDistanceSquaredTo(middle) <= other.MidPoint.GetDistanceSquaredTo(middle) ? this : other;
        }

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, using the default tolerance.
        /// </summary>
        /// <param name="distance">How far to move it: outward from the centre when positive, inward when negative.</param>
        /// <param name="result">The offset arc, or this arc when the method returns false.</param>
        /// <returns>true if the offset arc has a positive radius; otherwise, false.</returns>
        public bool TryOffset(double distance, out GeoArc2 result) => TryOffset(distance, out result, Tolerance.Global);

        /// <summary>
        /// Gets the arc concentric with this one at a distance from it, within a tolerance.
        /// </summary>
        /// <param name="distance">How far to move it: outward from the centre when positive, inward when negative.</param>
        /// <param name="result">The offset arc, or this arc when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the offset arc has a positive radius; otherwise, false.</returns>
        /// <remarks>
        /// The offset of an arc is exactly an arc: same centre, same angles, a different radius. An offset
        /// inward by more than the radius would turn it inside out, which is refused rather than answered.
        /// </remarks>
        public bool TryOffset(double distance, out GeoArc2 result, Tolerance tolerance)
        {
            double radius = Radius + distance;

            if (radius <= tolerance.EqualPoint)
            {
                result = this;
                return false;
            }

            result = new GeoArc2(Center, radius, StartAngle, SweptAngle, IsClockwise, true);
            return true;
        }

        #endregion

        #region Approximating by straight pieces

        /// <summary>
        /// Approximates the arc as a chain, cut finely enough that it strays no further from the arc than
        /// the automatic share of its radius.
        /// </summary>
        /// <returns>A chain from the start of the arc to its end.</returns>
        public GeoPolyline2 ToPolyline() => ToPolylineByChordTolerance(0.0);

        /// <summary>
        /// Approximates the arc as a chain that strays no further from it than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc, in drawing units. Zero picks the automatic share of the radius.</param>
        /// <returns>A chain from the start of the arc to its end.</returns>
        /// <remarks>
        /// The promise holds however fine the tolerance and however large the radius: a quarter turn of radius 50 000 cut
        /// within 1E-4 is 12 419 pieces, and an arc of radius 83 333 sweeping 0.0012 radians is 13, not the one chord that
        /// 4 096 pieces a turn would allow. An arc that the tolerance would cut into more than 1 048 576 pieces is refused.
        /// Points closer together than the point tolerance are merged by the chain, so chords shorter than that are not kept,
        /// as for <see cref="GeoPolylineArc2.Flatten(double)"/>.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a number, or needs more than 1 048 576 pieces.</exception>
        public GeoPolyline2 ToPolylineByChordTolerance(double chordTolerance)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForArc(Radius, Math.Abs(SweptAngle), chordTolerance));
        }

        /// <summary>
        /// Approximates the arc as a chain whose points are no further apart than a spacing.
        /// </summary>
        /// <param name="spacing">The largest distance allowed between two points, measured along the arc.</param>
        /// <returns>A chain from the start of the arc to its end, its points evenly spread.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing is not a positive number.</exception>
        public GeoPolyline2 ToPolylineBySpacing(double spacing)
        {
            return ToPolyline(Internal.Tessellation.SegmentsForSpacing(Length, Math.Abs(SweptAngle), spacing));
        }

        /// <summary>
        /// Approximates the arc as a chain with a given number of pieces.
        /// </summary>
        /// <param name="segmentCount">How many pieces the chain should have; at least one.</param>
        /// <returns>A chain from the start of the arc to its end.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than one piece is asked for.</exception>
        public GeoPolyline2 ToPolyline(int segmentCount)
        {
            Internal.Tessellation.RequireSegmentCount(segmentCount, 1);

            GeoPoint2[] points = new GeoPoint2[segmentCount + 1];

            for (int i = 0; i <= segmentCount; i++)
            {
                points[i] = GetPointAtParameter((double)i / segmentCount);
            }

            return new GeoPolyline2(points);
        }

        #endregion

        #region Extending and trimming

        /// <summary>
        /// Lengthens the arc along itself at one end, keeping its centre and radius.
        /// </summary>
        /// <remarks>
        /// The distance is arc length and not chord, which is the only reading that leaves the curve where it
        /// was. A negative distance takes length away, and an extension past a whole turn is refused.
        /// </remarks>
        public GeoArc2 Extend(double distance, LineEnd end) => Lengthen2.Extend(this, distance, end);

        /// <summary>
        /// Lengthens the arc along itself at one end, within a tolerance.
        /// </summary>
        public GeoArc2 Extend(double distance, LineEnd end, Tolerance tolerance) => Lengthen2.Extend(this, distance, end, tolerance);

        /// <summary>
        /// Lengthens the arc along itself at both ends at once.
        /// </summary>
        public GeoArc2 Extend(double startDistance, double endDistance) => Lengthen2.Extend(this, startDistance, endDistance);

        /// <summary>
        /// Lengthens the arc along itself at both ends at once, within a tolerance.
        /// </summary>
        public GeoArc2 Extend(double startDistance, double endDistance, Tolerance tolerance)
            => Lengthen2.Extend(this, startDistance, endDistance, tolerance);

        /// <summary>
        /// Lengthens or shortens the arc at one end until it is a given arc length.
        /// </summary>
        public GeoArc2 ExtendToLength(double length, LineEnd end) => Lengthen2.ExtendToLength(this, length, end);

        /// <summary>
        /// Lengthens or shortens the arc at one end until it is a given arc length, within a tolerance.
        /// </summary>
        public GeoArc2 ExtendToLength(double length, LineEnd end, Tolerance tolerance) => Lengthen2.ExtendToLength(this, length, end, tolerance);

        /// <summary>
        /// Lengthens the arc at one end until it reaches a point on its own circle.
        /// </summary>
        public bool TryExtendTo(GeoPoint2 point, LineEnd end, out GeoArc2 result) => Lengthen2.TryExtendTo(this, point, end, out result);

        /// <summary>
        /// Lengthens the arc at one end until it reaches a point on its own circle, within a tolerance.
        /// </summary>
        public bool TryExtendTo(GeoPoint2 point, LineEnd end, out GeoArc2 result, Tolerance tolerance)
            => Lengthen2.TryExtendTo(this, point, end, out result, tolerance);

        /// <summary>
        /// Shortens the arc at one end back to a point on it.
        /// </summary>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoArc2 result) => Lengthen2.TryTrimTo(this, point, end, out result);

        /// <summary>
        /// Shortens the arc at one end back to a point on it, within a tolerance.
        /// </summary>
        public bool TryTrimTo(GeoPoint2 point, LineEnd end, out GeoArc2 result, Tolerance tolerance)
            => Lengthen2.TryTrimTo(this, point, end, out result, tolerance);

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another arc holds exactly the same centre, radius and angles.
        /// </summary>
        public bool Equals(GeoArc2 other)
        {
            return Center.Equals(other.Center)
                && Radius.Equals(other.Radius)
                && StartAngle.Equals(other.StartAngle)
                && SweptAngle.Equals(other.SweptAngle);
        }

        /// <summary>
        /// Determines whether the specified object is an equal arc.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoArc2 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this arc.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Center.GetHashCode();
                hash = hash * 397 ^ Radius.GetHashCode();
                hash = hash * 397 ^ StartAngle.GetHashCode();
                hash = hash * 397 ^ SweptAngle.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether another arc draws the same curve, within the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoArc2 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether another arc draws the same curve, within a tolerance.
        /// </summary>
        /// <param name="other">The arc to compare with.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arcs are the same within tolerance; otherwise, false.</returns>
        /// <remarks>
        /// An arc and the same arc reversed are not equal: which way a curve runs is part of it, as it is
        /// for a segment and for a chain.
        /// </remarks>
        public bool IsEqualTo(GeoArc2 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance)
                && Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint
                && StartPoint.IsEqualTo(other.StartPoint, tolerance)
                && EndPoint.IsEqualTo(other.EndPoint, tolerance)
                && Math.Sign(SweptAngle) == Math.Sign(other.SweptAngle);
        }

        /// <summary>
        /// Determines whether two arcs hold exactly the same values.
        /// </summary>
        public static bool operator ==(GeoArc2 left, GeoArc2 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two arcs hold different values.
        /// </summary>
        public static bool operator !=(GeoArc2 left, GeoArc2 right) => !left.Equals(right);

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: the centre is a valid point, the radius a positive number and the angles numbers.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one — an element of a new array, or what
        /// the <c>out</c> of a <c>Try</c> method holds when the method said false — may not be, and answers
        /// questions without complaint all the same. A default one has no radius.
        /// </remarks>
        public bool IsValid => Center.IsValid && Radius > 0.0 && Guard.IsFinite(Radius) && Guard.IsFinite(StartAngle) && Guard.IsFinite(SweptAngle);

        /// <summary>
        /// Describes the arc.
        /// </summary>
        public override string ToString()
        {
            return $"GeoArc2[Center:{Center}, R:{Radius:0.###}, {StartAngle * 180.0 / Math.PI:0.#}° sweep {SweptAngle * 180.0 / Math.PI:0.#}°]";
        }

        #endregion

        #region Helpers

        private static double AngleOf(GeoPoint2 center, GeoPoint2 point)
        {
            GeoVector2 fromCenter = center.GetVectorTo(point);

            return Math.Atan2(fromCenter.Y, fromCenter.X);
        }

        private static double Wrap(double angle)
        {
            double wrapped = angle % FullTurn;

            return wrapped < 0.0 ? wrapped + FullTurn : wrapped;
        }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "An arc must have a positive radius.");
            }
        }

        private static void RequireNumber(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "An angle must be a number.");
            }
        }

        #endregion

        #region Operations

        /// <summary>
        /// Determines whether a point lies on this arc.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => Arc2.IsPointOn(this, point);

        /// <summary>
        /// Determines whether a point lies on this arc, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => Arc2.IsPointOn(this, point, tolerance);

        /// <summary>
        /// Says where a point sits relative to this arc; a curve never answers Inside.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point) => Arc2.Locate(this, point);

        /// <summary>
        /// Says where a point sits relative to this arc, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint2 point, Tolerance tolerance) => Arc2.Locate(this, point, tolerance);

        /// <summary>
        /// Splits this arc at a normalized parameter.
        /// </summary>
        public bool TrySplitAt(double parameter, out GeoArc2[] pieces) => Arc2.TrySplitAt(this, parameter, out pieces);

        /// <summary>
        /// Splits this arc at a normalized parameter, within a tolerance.
        /// </summary>
        public bool TrySplitAt(double parameter, out GeoArc2[] pieces, Tolerance tolerance) => Arc2.TrySplitAt(this, parameter, out pieces, tolerance);

        /// <summary>
        /// Splits this arc at the point of it nearest a point.
        /// </summary>
        public bool TrySplitAt(GeoPoint2 point, out GeoArc2[] pieces) => Arc2.TrySplitAt(this, point, out pieces);

        /// <summary>
        /// Splits this arc at the point of it nearest a point, within a tolerance.
        /// </summary>
        public bool TrySplitAt(GeoPoint2 point, out GeoArc2[] pieces, Tolerance tolerance) => Arc2.TrySplitAt(this, point, out pieces, tolerance);

        #endregion
    }
}
