using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Where a view looks from and how deep it sees: a coordinate system in the model whose X runs to the right of the
    /// view, Y up it and Z towards the viewer, its XY plane the cut plane, and how far in front of that plane and behind
    /// it the view sees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A point of the model goes into the view by <see cref="ToView"/>: its X and Y across the view, in model millimetres
    /// at 1:1, and its depth, positive towards the viewer. Looking along the model's Y with Z up, a box 30 by 20 by 10 at
    /// the origin has its corner (30, 20, 10) at (30, 10) in the view, 20 behind the cut plane through the origin; the
    /// view is a right-handed frame, so nothing in it comes out mirrored.
    /// </para>
    /// <para>
    /// The depth range is what the view takes in: from <see cref="DepthInFront"/> towards the viewer to
    /// <see cref="DepthBehind"/> away from it. A view along an axis sees everything, both infinite; a section sees
    /// nothing in front of its cut, <see cref="DepthInFront"/> nought, and a depth behind it.
    /// </para>
    /// </remarks>
    public sealed class ViewFrame3
    {
        /// <summary>
        /// Makes the frame of a view looking along a direction.
        /// </summary>
        /// <param name="origin">A point of the cut plane, which becomes the origin of the view.</param>
        /// <param name="direction">The direction the view looks along, away from the viewer; of any length longer than the vector
        /// tolerance.</param>
        /// <param name="up">A direction that comes out upwards in the view; it need not be square to the direction, and is
        /// taken as its part square to it.</param>
        /// <param name="depthInFront">How far towards the viewer from the cut plane the view sees; nought or more, or
        /// infinity.</param>
        /// <param name="depthBehind">How far away from the viewer behind the cut plane the view sees; nought or more, or
        /// infinity.</param>
        /// <param name="tolerance">The tolerance the direction is read within: one of no length, or an up along it, is
        /// refused.</param>
        /// <exception cref="ArgumentException">Thrown when the origin is not a point of numbers, the direction or the up
        /// has no length, or the up lies along the direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a depth is negative or not a number.</exception>
        public ViewFrame3(GeoPoint3 origin, GeoVector3 direction, GeoVector3 up, double depthInFront, double depthBehind, Tolerance tolerance)
        {
            if (!origin.IsValid)
            {
                throw new ArgumentException("The origin has to be a point of numbers.", nameof(origin));
            }

            if (!direction.IsValid || direction.IsZeroLength(tolerance))
            {
                throw new ArgumentException("A view needs a direction to look along.", nameof(direction));
            }

            if (!up.IsValid || up.IsZeroLength(tolerance) || up.IsParallelTo(direction, tolerance))
            {
                throw new ArgumentException("The up of a view has to lie across the direction it looks along.", nameof(up));
            }

            CheckDepth(depthInFront, nameof(depthInFront));
            CheckDepth(depthBehind, nameof(depthBehind));

            CoordinateSystem = Square(origin, direction.Negate().Normalize(), up);
            DepthInFront = depthInFront;
            DepthBehind = depthBehind;
        }

        private ViewFrame3(GeoCoordinateSystem3 coordinateSystem)
        {
            CoordinateSystem = coordinateSystem;
            DepthInFront = double.PositiveInfinity;
            DepthBehind = double.PositiveInfinity;
        }

        /// <summary>
        /// Gets the coordinate system of the view in the model: its origin on the cut plane, X to the right of the view,
        /// Y up it and Z towards the viewer.
        /// </summary>
        public GeoCoordinateSystem3 CoordinateSystem { get; }

        /// <summary>Gets the direction the view looks along, away from the viewer, of unit length.</summary>
        public GeoVector3 Direction => CoordinateSystem.ZAxis.Negate();

        /// <summary>Gets the cut plane, through the origin of the view, its normal towards the viewer.</summary>
        public GeoPlane3 CutPlane => new GeoPlane3(CoordinateSystem.Origin, CoordinateSystem.ZAxis);

        /// <summary>Gets how far towards the viewer from the cut plane the view sees.</summary>
        public double DepthInFront { get; }

        /// <summary>Gets how far away from the viewer behind the cut plane the view sees.</summary>
        public double DepthBehind { get; }

        /// <summary>
        /// Takes a point of the model into the view.
        /// </summary>
        /// <param name="point">The point, in model coordinates.</param>
        /// <param name="depth">How far the point stands in front of the cut plane, towards the viewer; negative behind it.</param>
        /// <returns>The point across the view, in model millimetres at 1:1.</returns>
        public GeoPoint2 ToView(GeoPoint3 point, out double depth)
        {
            GeoPoint3 local = CoordinateSystem.ToLocal(point);
            depth = local.Z;
            return new GeoPoint2(local.X, local.Y);
        }

        /// <summary>
        /// Determines whether a depth lies within the range the view sees, its ends taken in within the point tolerance.
        /// </summary>
        /// <param name="depth">The depth, positive towards the viewer, as <see cref="ToView"/> gives it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Whether the depth is no further in front than <see cref="DepthInFront"/> and no further behind than
        /// <see cref="DepthBehind"/>.</returns>
        public bool IsWithinDepth(double depth, Tolerance tolerance)
        {
            return depth <= DepthInFront + tolerance.EqualPoint && depth >= -DepthBehind - tolerance.EqualPoint;
        }

        /// <summary>
        /// Makes the frame of a view along axes already known to be square, seeing everything in front and behind.
        /// </summary>
        internal static ViewFrame3 Along(GeoPoint3 origin, GeoVector3 towardsViewer, GeoVector3 up)
            => new ViewFrame3(Square(origin, towardsViewer, up));

        private static GeoCoordinateSystem3 Square(GeoPoint3 origin, GeoVector3 towardsViewer, GeoVector3 up)
        {
            // X is Y cross Z, so that X, Y and Z are right-handed and the view is not a mirror image. The Y passed on is
            // Z cross X, square to both, so that the coordinate system keeps the Z, the way the view looks, as it is.
            GeoVector3 right = up.CrossProduct(towardsViewer);
            return new GeoCoordinateSystem3(origin, right, towardsViewer.CrossProduct(right));
        }

        private static void CheckDepth(double depth, string name)
        {
            if (!(depth >= 0.0))
            {
                throw new ArgumentOutOfRangeException(name, depth, "A depth has to be nought or more.");
            }
        }
    }
}
