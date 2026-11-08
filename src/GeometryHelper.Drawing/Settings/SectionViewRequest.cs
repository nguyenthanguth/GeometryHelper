using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A section a drawing is asked for: a cut square to the direction one of the views along the axes looks in, at a share of
    /// the depth of the objects, seen from that view's side with what lies in front of the cut taken away; or a cut along a
    /// line drawn in one of those views, square to it.
    /// </summary>
    /// <remarks>
    /// The share is counted from the side away from the viewer: a beam 3 000 long along the drawing frame's X, cut as the right
    /// end view sees it (from +X) at 0.5, is cut at 1 500 and shows its cross-section with the half from 0 to 1 500 behind
    /// it, as deep as <see cref="ViewSettings.SectionDepth"/> lets the view see. The section is named "SECTION A-A" for the
    /// name "A".
    /// </remarks>
    public sealed class SectionViewRequest
    {
        /// <summary>Makes the request for a section.</summary>
        /// <param name="name">The letter or name of the section, "A" for SECTION A-A.</param>
        /// <param name="lookingAs">The view along the axes whose direction the section looks in: front, top, bottom, left,
        /// right or back.</param>
        /// <param name="share">Where the cut is, as a share of the depth of the objects from the side away from the viewer:
        /// nought to one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, or the view is not one along the axes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the share is not a number from nought to one.</exception>
        public SectionViewRequest(string name, ViewKind lookingAs, double share)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("A section needs a name.", nameof(name));
            }

            if (lookingAs != ViewKind.Front && lookingAs != ViewKind.Top && lookingAs != ViewKind.Bottom
                && lookingAs != ViewKind.Left && lookingAs != ViewKind.Right && lookingAs != ViewKind.Back)
            {
                throw new ArgumentException("A section looks as one of the views along the axes does.", nameof(lookingAs));
            }

            if (!(share >= 0.0) || !(share <= 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(share), share, "The share has to be a number from nought to one.");
            }

            Name = name;
            LookingAs = lookingAs;
            Share = share;
        }

        /// <summary>Gets the letter or name of the section.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the view along the axes whose direction the section looks in; for a section cut along a line, the view the
        /// line is drawn in.
        /// </summary>
        public ViewKind LookingAs { get; }

        /// <summary>
        /// Gets where the cut is, as a share of the depth of the objects from the side away from the viewer; nought for a
        /// section cut along a line.
        /// </summary>
        public double Share { get; }

        /// <summary>
        /// Gets the line the section is cut along, in the millimetres of the view <see cref="LookingAs"/>; null for a section
        /// cut at a share of the depth.
        /// </summary>
        public GeoLine2? CutLine { get; }

        /// <summary>
        /// Gets how far behind its cut a section cut along a line sees, in model millimetres; NaN for a section cut at a share,
        /// which sees as far as <see cref="ViewSettings.SectionDepth"/>.
        /// </summary>
        public double Depth { get; } = double.NaN;

        /// <summary>
        /// Makes the request for a section cut along a line drawn in a view along the axes, square to that view, looking to
        /// the right of the line from its first point to its second, as far as a depth behind the cut.
        /// </summary>
        /// <param name="name">The letter or name of the section, "A" for SECTION A-A.</param>
        /// <param name="inView">The view along the axes the line is drawn in: front, top, bottom, left, right or back.</param>
        /// <param name="cutLine">The line, in the view's millimetres at 1:1.</param>
        /// <param name="depth">How far behind the cut the section sees, in model millimetres; infinity to see all.</param>
        /// <remarks>
        /// The line from (1500, -200) to (1500, 200) across the front view of a beam 3 000 long along the view's X cuts it
        /// at 1 500 and looks towards its far end, the right of the line going up; from (1500, 200) to (1500, -200) it looks
        /// back towards the beam's start.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the view is not one along the axes, or the line has
        /// no length or is not of finite numbers.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the depth is negative or not a number.</exception>
        public SectionViewRequest(string name, ViewKind inView, GeoLine2 cutLine, double depth)
            : this(name, inView, 0.0)
        {
            double length = cutLine.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A cut line has to have a length of finite numbers.", nameof(cutLine));
            }

            if (!(depth >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "A depth has to be nought or more.");
            }

            CutLine = cutLine;
            Depth = depth;
        }

        /// <summary>
        /// The way a section cut along a line looks, in the view the line is drawn in: the unit vector to the right of the
        /// line from its first point to its second.
        /// </summary>
        internal GeoVector2 LookInView()
        {
            GeoLine2 line = CutLine.Value;
            double dx = line.EndPoint.X - line.StartPoint.X, dy = line.EndPoint.Y - line.StartPoint.Y, length = Math.Sqrt(dx * dx + dy * dy);
            return new GeoVector2(dy / length, -dx / length);
        }
    }
}
