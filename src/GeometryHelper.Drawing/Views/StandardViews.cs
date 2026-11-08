using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The views along the axes of a drawing's frame, the isometric view, and where the views along the axes go round the
    /// front view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The drawing's frame is the main object's: X along its length, Y up in its cross-section, Z towards the one who
    /// looks at its front. Each view is set in it, so a beam lying along any axis of the model gives the same views.
    /// </para>
    /// <para>
    /// Every view is the object as seen from its side, never a mirror image. In a drawing frame at the origin, a box 30
    /// by 20 by 10 from the origin has its corner (30, 20, 10) at (30, 20) in the front view, at (30, -10) in the top
    /// view, whose bottom edge is the box's front, and at (-10, 20) in the right end view, whose left edge is the box's
    /// front; in each of them the face turned to the viewer runs round the way it does seen from outside.
    /// </para>
    /// </remarks>
    public static class StandardViews
    {
        /// <summary>
        /// Gets the frame of a view along the axes of a drawing's frame, or of its isometric view, its origin at the
        /// drawing frame's, seeing everything in front and behind.
        /// </summary>
        /// <param name="drawingFrame">The drawing's frame, as <see cref="DrawingSource.Frame"/> gives it.</param>
        /// <param name="kind">The view: front, top, bottom, left, right, back or isometric.</param>
        /// <returns>The frame of the view.</returns>
        /// <exception cref="ArgumentException">Thrown when the drawing frame is not a frame of three unit axes square to
        /// each other, or the kind is a section, a detail or a free view, which have no frame of their own.</exception>
        /// <remarks>
        /// <list type="table">
        /// <listheader><term>View</term><description>Looks from; across the view, up it</description></listheader>
        /// <item><term>Front</term><description>+Z; X, Y</description></item>
        /// <item><term>Top</term><description>+Y; X, -Z</description></item>
        /// <item><term>Bottom</term><description>-Y; X, Z</description></item>
        /// <item><term>Left</term><description>-X; Z, Y</description></item>
        /// <item><term>Right</term><description>+X; -Z, Y</description></item>
        /// <item><term>Back</term><description>-Z; -X, Y</description></item>
        /// <item><term>Isometric</term><description>+X, +Y and +Z alike; Y up</description></item>
        /// </list>
        /// </remarks>
        public static ViewFrame3 GetFrame(GeoCoordinateSystem3 drawingFrame, ViewKind kind)
        {
            if (!drawingFrame.IsValid)
            {
                throw new ArgumentException("The frame has to have three unit axes square to each other.", nameof(drawingFrame));
            }

            GeoPoint3 origin = drawingFrame.Origin;
            GeoVector3 x = drawingFrame.XAxis;
            GeoVector3 y = drawingFrame.YAxis;
            GeoVector3 z = drawingFrame.ZAxis;

            switch (kind)
            {
                case ViewKind.Front:
                    return ViewFrame3.Along(origin, z, y);
                case ViewKind.Top:
                    return ViewFrame3.Along(origin, y, z.Negate());
                case ViewKind.Bottom:
                    return ViewFrame3.Along(origin, y.Negate(), z);
                case ViewKind.Left:
                    return ViewFrame3.Along(origin, x.Negate(), y);
                case ViewKind.Right:
                    return ViewFrame3.Along(origin, x, y);
                case ViewKind.Back:
                    return ViewFrame3.Along(origin, z.Negate(), y);
                case ViewKind.Isometric:
                    return ViewFrame3.Along(origin, x.Add(y).Add(z).Normalize(), y);
                default:
                    throw new ArgumentException("A " + kind + " view has no frame of its own; make one with its direction.", nameof(kind));
            }
        }

        /// <summary>
        /// Gets where a view along the axes goes round the front view, in steps of one view across and up.
        /// </summary>
        /// <param name="kind">The view: front, top, bottom, left, right or back.</param>
        /// <param name="method">How the views are laid out; third-angle unless set otherwise.</param>
        /// <param name="column">How many views to the right of the front view it goes; negative to the left.</param>
        /// <param name="row">How many views above the front view it goes; negative below.</param>
        /// <exception cref="ArgumentException">Thrown when the kind is not a view along the axes, or the method is not
        /// one of <see cref="ProjectionMethod"/>.</exception>
        /// <remarks>
        /// In third-angle projection the top view goes above the front view, the bottom view below, the right end view on
        /// its right and the left end view on its left; in first-angle projection each goes on the other side. The back
        /// view goes two steps to the right either way, beside the end view on the right.
        /// </remarks>
        public static void GetSlot(ViewKind kind, ProjectionMethod method, out int column, out int row)
        {
            int side;

            switch (method)
            {
                case ProjectionMethod.ThirdAngle:
                    side = 1;
                    break;
                case ProjectionMethod.FirstAngle:
                    side = -1;
                    break;
                default:
                    throw new ArgumentException("The method has to be one of ProjectionMethod.", nameof(method));
            }

            switch (kind)
            {
                case ViewKind.Front:
                    column = 0;
                    row = 0;
                    return;
                case ViewKind.Top:
                    column = 0;
                    row = side;
                    return;
                case ViewKind.Bottom:
                    column = 0;
                    row = -side;
                    return;
                case ViewKind.Left:
                    column = -side;
                    row = 0;
                    return;
                case ViewKind.Right:
                    column = side;
                    row = 0;
                    return;
                case ViewKind.Back:
                    column = 2;
                    row = 0;
                    return;
                default:
                    throw new ArgumentException("A " + kind + " view has no place of its own round the front view.", nameof(kind));
            }
        }
    }
}
