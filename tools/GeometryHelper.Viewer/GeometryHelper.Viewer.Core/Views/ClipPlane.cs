using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A plane that cuts the scene away: what lies on the side its normal points to stays, the rest is not drawn, picked or snapped to. It is
    /// made through an anchor, a point it was set through, and moved along its normal by an offset, so that "10 mm further" is a number that
    /// can be typed and the anchor is still where the plane began.
    /// </summary>
    public sealed class ClipPlane
    {
        /// <summary>
        /// Initializes a clip plane.
        /// </summary>
        /// <param name="anchor">A point the plane is set through.</param>
        /// <param name="normal">The direction of the normal; it is made length one. The side it points to stays.</param>
        /// <param name="offset">How far the plane lies along the normal from the anchor, in millimetres.</param>
        /// <param name="enabled">Whether the plane cuts.</param>
        /// <exception cref="ArgumentException">Thrown when the normal has no length or a number is not finite.</exception>
        public ClipPlane(Vec3 anchor, Vec3 normal, double offset = 0.0, bool enabled = true)
        {
            Vec3 unit = normal.Normalized();

            if (!anchor.IsFinite || !normal.IsFinite || unit.Equals(default(Vec3)))
            {
                throw new ArgumentException("A clip plane needs a finite anchor and a normal with a length.");
            }

            if (double.IsNaN(offset) || double.IsInfinity(offset))
            {
                throw new ArgumentException("The offset of a clip plane must be a finite number.", nameof(offset));
            }

            Anchor = anchor;
            Normal = unit;
            Offset = offset;
            Enabled = enabled;
            Point = offset == 0.0 ? anchor : anchor + unit * offset;
        }

        /// <summary>
        /// Gets the point the plane was set through.
        /// </summary>
        public Vec3 Anchor { get; }

        /// <summary>
        /// Gets the unit normal; the side it points to stays.
        /// </summary>
        public Vec3 Normal { get; }

        /// <summary>
        /// Gets how far the plane lies along the normal from the anchor.
        /// </summary>
        public double Offset { get; }

        /// <summary>
        /// Gets whether the plane cuts; a plane that is off stays in the list.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets the point of the plane the offset puts the plane through: the anchor moved along the normal by the offset.
        /// </summary>
        public Vec3 Point { get; }

        /// <summary>
        /// Gets how far a point is from the plane, positive on the side that stays.
        /// </summary>
        /// <param name="point">The point.</param>
        public double SignedDistance(Vec3 point) => Vec3.Dot(point - Point, Normal);

        /// <summary>
        /// Gets where the handle of the plane is: the point of the plane nearest the middle of the scene, which is where the plane is drawn from and
        /// where it is taken hold of to be dragged along its normal.
        /// </summary>
        /// <param name="scene">The box round the scene; with none the handle is the point of the plane the plane was set through.</param>
        public Vec3 HandleFor(Bounds3 scene)
        {
            if (scene.IsEmpty)
            {
                return Point;
            }

            Vec3 middle = scene.Center;
            return middle - Normal * SignedDistance(middle);
        }

        /// <summary>
        /// Gets the plane turned over: the other side stays, and the plane is where it was.
        /// </summary>
        public ClipPlane Flipped() => new ClipPlane(Anchor, -Normal, -Offset, Enabled);

        /// <summary>
        /// Gets the plane with another offset.
        /// </summary>
        /// <param name="offset">How far along the normal from the anchor.</param>
        public ClipPlane WithOffset(double offset) => new ClipPlane(Anchor, Normal, offset, Enabled);

        /// <summary>
        /// Gets the plane switched on or off.
        /// </summary>
        /// <param name="enabled">Whether it cuts.</param>
        public ClipPlane WithEnabled(bool enabled) => new ClipPlane(Anchor, Normal, Offset, enabled);

        /// <summary>
        /// Finds how far along a line the point lies that is nearest a ray, which is how a handle on a line is dragged: the line is the normal of a
        /// plane through its handle and the ray is the one through the mouse. Taking the difference of two answers gives how far the mouse moved it.
        /// </summary>
        /// <param name="linePoint">A point of the line.</param>
        /// <param name="lineDirection">The direction of the line, of length one.</param>
        /// <param name="rayOrigin">Where the ray starts.</param>
        /// <param name="rayDirection">The direction of the ray, of length one.</param>
        /// <returns>The distance from <paramref name="linePoint"/> along the line, or not a number where the ray is along the line (within a thousandth of a radian) and the point is not told.</returns>
        public static double ParameterNearestRay(Vec3 linePoint, Vec3 lineDirection, Vec3 rayOrigin, Vec3 rayDirection)
        {
            Vec3 w = linePoint - rayOrigin;
            double b = Vec3.Dot(lineDirection, rayDirection);
            double d = Vec3.Dot(lineDirection, w);
            double e = Vec3.Dot(rayDirection, w);
            double denominator = 1.0 - b * b;

            if (!(denominator > 1E-6))
            {
                return double.NaN;
            }

            return (b * e - d) / denominator;
        }

        /// <summary>
        /// Describes the plane: its point, normal, offset, and whether it is on.
        /// </summary>
        public override string ToString()
            => "through " + Point + ", keeping the side of " + Normal + ", offset " + Offset.ToString("G10", System.Globalization.CultureInfo.InvariantCulture) + (Enabled ? string.Empty : ", off");
    }
}
