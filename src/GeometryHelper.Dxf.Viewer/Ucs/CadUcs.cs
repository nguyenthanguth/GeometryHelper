using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A user coordinate system, as AutoCAD's UCS: an origin and X and Y axes in world coordinates, the Z axis square to both,
    /// and a name where it is one of the drawing's named systems. Points typed at a prompt, the readout of the status bar, the
    /// grid, snap, ortho and polar tracking are in the current system, while the drawing keeps its objects in world
    /// coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A system turned 30 degrees about Z at (100, 50) takes the point it calls (10, 0) to the world point (108.6603, 55),
    /// and the world point (100, 60) to its (5, 8.6603).
    /// </para>
    /// <para>
    /// The presenter shows the drawing from above, so it works in a system whose Z axis is the world's
    /// (<see cref="IsPlanar"/>). A system of a file turned out of that plane is read and saved as it is, listed by UCS ?, and
    /// not made current here.
    /// </para>
    /// </remarks>
    public sealed class CadUcs
    {
        private readonly double _cos;
        private readonly double _sin;

        /// <summary>Makes a system of an origin and two axes, the axes made of unit length and the Y axis square to the X axis in the plane of both.</summary>
        /// <param name="origin">The origin, in world coordinates.</param>
        /// <param name="xAxis">The X axis, in world coordinates.</param>
        /// <param name="yAxis">A direction in the XY plane on the side of the positive Y axis.</param>
        /// <param name="name">The name of a named system; null for one with no name.</param>
        /// <exception cref="ArgumentException">Thrown when a number is not finite, an axis has no length, or the two axes are parallel.</exception>
        public CadUcs(GeoPoint3 origin, GeoVector3 xAxis, GeoVector3 yAxis, string name = null)
        {
            if (!origin.IsValid || !Finite(xAxis) || !Finite(yAxis))
            {
                throw new ArgumentException("A UCS has a finite origin and finite axes.", nameof(origin));
            }

            double xLength = xAxis.Length;
            GeoVector3 x = xLength > 0.0 ? xAxis / xLength : xAxis;
            GeoVector3 z = x.CrossProduct(yAxis);
            double zLength = z.Length;

            if (!(xLength > 0.0) || !(zLength > 1E-12 * yAxis.Length))
            {
                throw new ArgumentException("The axes of a UCS have a length and are not parallel: " + xAxis + " and " + yAxis + " are not.", nameof(xAxis));
            }

            Origin = origin;
            XAxis = x;
            ZAxis = z / zLength;
            YAxis = ZAxis.CrossProduct(XAxis);
            Name = string.IsNullOrEmpty(name) ? null : name;
            IsWorld = origin.X == 0.0 && origin.Y == 0.0 && origin.Z == 0.0 && XAxis.X == 1.0 && XAxis.Y == 0.0 && XAxis.Z == 0.0
                && YAxis.X == 0.0 && YAxis.Y == 1.0 && YAxis.Z == 0.0;
            IsPlanar = ZAxis.Z >= 1.0 - Tolerance.Default.EqualVector;
            Angle = Math.Atan2(XAxis.Y, XAxis.X);
            _cos = IsWorld ? 1.0 : Math.Cos(Angle);
            _sin = IsWorld ? 0.0 : Math.Sin(Angle);
        }

        /// <summary>Gets the world coordinate system, AutoCAD's WCS.</summary>
        public static CadUcs World { get; } = new CadUcs(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

        /// <summary>Gets the name of a named system, as UCS Save gave it; null for one with no name, as AutoCAD's *NO NAME*.</summary>
        public string Name { get; }

        /// <summary>Gets the origin, in world coordinates.</summary>
        public GeoPoint3 Origin { get; }

        /// <summary>Gets the X axis, of unit length, in world coordinates.</summary>
        public GeoVector3 XAxis { get; }

        /// <summary>Gets the Y axis, of unit length and square to the X axis, in world coordinates.</summary>
        public GeoVector3 YAxis { get; }

        /// <summary>Gets the Z axis, of unit length and square to the other two.</summary>
        public GeoVector3 ZAxis { get; }

        /// <summary>Gets whether this is the world's own system: the origin at nought and the axes the world's, to the bit.</summary>
        public bool IsWorld { get; }

        /// <summary>Gets whether the Z axis is the world's, to the vector tolerance of <see cref="Tolerance.Default"/>, so that the presenter, which shows the drawing from above, can work in the system.</summary>
        public bool IsPlanar { get; }

        /// <summary>Gets the angle of the X axis from the world's X axis, counter-clockwise in radians from -π to π: π/6 for a system turned 30 degrees.</summary>
        public double Angle { get; }

        /// <summary>Makes a system in the plane of the world's XY: its origin and the angle of its X axis.</summary>
        /// <param name="origin">The origin, in world coordinates.</param>
        /// <param name="angle">The angle of the X axis from the world's X axis, counter-clockwise in radians.</param>
        /// <param name="name">The name of a named system; null for one with no name.</param>
        /// <returns>The system.</returns>
        /// <exception cref="ArgumentException">Thrown when a number is not finite.</exception>
        public static CadUcs InPlane(GeoPoint2 origin, double angle, string name = null)
        {
            if (!Finite(angle))
            {
                throw new ArgumentException("An angle has to be a finite number.", nameof(angle));
            }

            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new CadUcs(new GeoPoint3(origin.X, origin.Y, 0.0), new GeoVector3(cos, sin, 0.0), new GeoVector3(-sin, cos, 0.0), name);
        }

        /// <summary>Gets a point of the world in this system: its distances along the X and Y axes from the origin.</summary>
        /// <param name="world">The point, in world coordinates.</param>
        /// <returns>The point in this system.</returns>
        public GeoPoint2 ToUcs(GeoPoint2 world)
        {
            if (IsWorld)
            {
                return world;
            }

            double dx = world.X - Origin.X;
            double dy = world.Y - Origin.Y;
            return new GeoPoint2(dx * _cos + dy * _sin, -dx * _sin + dy * _cos);
        }

        /// <summary>Gets the world point of a point given in this system.</summary>
        /// <param name="ucs">The point, in this system.</param>
        /// <returns>The point in world coordinates.</returns>
        public GeoPoint2 ToWorld(GeoPoint2 ucs) => IsWorld ? ucs : new GeoPoint2(Origin.X + ucs.X * _cos - ucs.Y * _sin, Origin.Y + ucs.X * _sin + ucs.Y * _cos);

        /// <summary>Gets a point of the world in this system, in three dimensions.</summary>
        /// <param name="world">The point, in world coordinates.</param>
        /// <returns>The point in this system.</returns>
        public GeoPoint3 ToUcs(GeoPoint3 world)
        {
            GeoVector3 d = world - Origin;
            return new GeoPoint3(d.DotProduct(XAxis), d.DotProduct(YAxis), d.DotProduct(ZAxis));
        }

        /// <summary>Gets the world point of a point given in this system, in three dimensions.</summary>
        /// <param name="ucs">The point, in this system.</param>
        /// <returns>The point in world coordinates.</returns>
        public GeoPoint3 ToWorld(GeoPoint3 ucs) => Origin + XAxis * ucs.X + YAxis * ucs.Y + ZAxis * ucs.Z;

        /// <summary>Gets the world direction of a direction given in this system: turned by <see cref="Angle"/>.</summary>
        /// <param name="ucs">The direction, in this system.</param>
        /// <returns>The direction in world coordinates.</returns>
        public GeoVector2 ToWorld(GeoVector2 ucs) => IsWorld ? ucs : new GeoVector2(ucs.X * _cos - ucs.Y * _sin, ucs.X * _sin + ucs.Y * _cos);

        /// <summary>Gets a direction of the world in this system.</summary>
        /// <param name="world">The direction, in world coordinates.</param>
        /// <returns>The direction in this system.</returns>
        public GeoVector2 ToUcs(GeoVector2 world) => IsWorld ? world : new GeoVector2(world.X * _cos + world.Y * _sin, -world.X * _sin + world.Y * _cos);

        /// <summary>Gets the world angle of an angle measured from this system's X axis: 30 degrees in a system turned 45 is 75 in the world.</summary>
        /// <param name="ucs">The angle from this system's X axis, in radians.</param>
        /// <returns>The angle from the world's X axis, in radians.</returns>
        public double AngleToWorld(double ucs) => IsWorld ? ucs : ucs + Angle;

        /// <summary>Gets the angle from this system's X axis of an angle measured from the world's.</summary>
        /// <param name="world">The angle from the world's X axis, in radians.</param>
        /// <returns>The angle from this system's X axis, in radians.</returns>
        public double AngleToUcs(double world) => IsWorld ? world : world - Angle;

        /// <summary>Gets this system under another name, or with none.</summary>
        /// <param name="name">The name; null for none.</param>
        /// <returns>The system.</returns>
        public CadUcs WithName(string name) => new CadUcs(Origin, XAxis, YAxis, name);

        /// <summary>Gets whether two systems have the same origin and axes, to a tolerance, whatever their names.</summary>
        /// <param name="other">The other system.</param>
        /// <param name="tolerance">The tolerance: points to its point tolerance, axes to its vector tolerance.</param>
        /// <returns>Whether they are the same.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other system is null.</exception>
        public bool IsSameAs(CadUcs other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return (Origin - other.Origin).Length <= tolerance.EqualPoint && (XAxis - other.XAxis).Length <= tolerance.EqualVector
                && (YAxis - other.YAxis).Length <= tolerance.EqualVector;
        }

        /// <summary>Gives the system as a line: its name, origin and angle.</summary>
        /// <returns>The line, such as <c>Slab (100, 50, 0) at 30°</c>.</returns>
        public override string ToString()
            => (Name ?? (IsWorld ? "*WORLD*" : "*NO NAME*")) + " (" + Number(Origin.X) + ", " + Number(Origin.Y) + ", " + Number(Origin.Z) + ") at "
                + Number(Angle * 180.0 / Math.PI) + "°" + (IsPlanar ? string.Empty : ", not parallel to the view");

        private static string Number(double value) => (value + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

        private static bool Finite(GeoVector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
