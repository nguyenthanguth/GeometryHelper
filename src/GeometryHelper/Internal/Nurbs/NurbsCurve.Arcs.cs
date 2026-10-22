using System;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// An arc of a circle in the plane, as a piece of a curve or a partner is one: the centre and the radius, where it starts and
    /// which way and how far it turns.
    /// </summary>
    internal struct PieceArc
    {
        /// <summary>The centre.</summary>
        internal double Cx, Cy;

        /// <summary>The radius, above nought.</summary>
        internal double Radius;

        /// <summary>The angle the arc starts at, in radians, about the centre.</summary>
        internal double Start;

        /// <summary>The angle it turns through, signed, counter-clockwise when positive; ignored when whole.</summary>
        internal double Sweep;

        /// <summary>Whether it is the whole circle.</summary>
        internal bool Whole;

        /// <summary>
        /// How far the centre and the radius can stand off those of the circle the arc is, from the rounding of the numbers they were
        /// read from: nought for an arc given by its centre and radius.
        /// </summary>
        internal double Error;

        /// <summary>
        /// Says whether a point of the circle, or one beside it, lies over the arc: its angle about the centre within the turn, or
        /// within a slack of either end, measured along the circle.
        /// </summary>
        internal bool Holds(double x, double y, double slack)
        {
            if (Whole)
            {
                return true;
            }

            double angle = Math.Atan2(y - Cy, x - Cx);
            double along = Sweep >= 0.0 ? angle - Start : Start - angle;
            along -= 2.0 * Math.PI * Math.Floor(along / (2.0 * Math.PI));
            double room = slack / Radius;

            return along <= Math.Abs(Sweep) + room || along >= 2.0 * Math.PI - room;
        }
    }

    internal sealed partial class NurbsCurve
    {
        private PieceArc[] _arcs;
        private volatile bool[] _isArc;

        /// <summary>
        /// Says whether a Bézier piece of a curve in the plane is an arc of a circle, to rounding, and gives the circle: a rational
        /// quadratic piece whose two outer control points are as far from the middle one, and whose middle weight, against the outer two,
        /// is the cosine of half the turn, the half chord over that distance, as every piece of <see cref="Geometry.GeoSpline2.FromArc"/>
        /// and <see cref="Geometry.GeoSpline2.FromCircle"/> is made. Worked out once for the whole curve.
        /// </summary>
        /// <param name="piece">Which piece.</param>
        /// <param name="arc">The arc, when the method returns true.</param>
        /// <remarks>
        /// To rounding is to a millionth of a millionth of the size of the numbers: an arc of radius 100 000 has its control points to
        /// some 1E-11, and a piece that bows off a circle by more than 1E-12 of its numbers is no arc. The centre is the meeting of the
        /// lines square to the piece at its ends, worked out from the middle control point and the middle of the chord, and the rounding
        /// of the control points, taken at the size of the radius as the arc was made from its centre, reaches it times four times the
        /// square of the radius over the chord (<see cref="PieceArc.Error"/>): a quarter turn of radius 100 000 has its centre to 1E-10,
        /// an arc of radius 1 000 000 and chord 300 to no better than a fiftieth, 2.3E-4 when it was measured, and one of radius
        /// 10 000 000 and chord 1 000 a hundred thousand out to no better than two, 0.6 when it was measured.
        /// </remarks>
        internal bool TryGetArc(int piece, out PieceArc arc)
        {
            bool[] isArc = _isArc;

            if (isArc == null)
            {
                FindArcs();
                isArc = _isArc;
            }

            arc = _arcs[piece];
            return isArc[piece];
        }

        private void FindArcs()
        {
            BezierPieces pieces = Pieces;
            var arcs = new PieceArc[pieces.Count];
            var isArc = new bool[pieces.Count];

            if (Dim == 2 && Degree == 2 && Rational)
            {
                for (int i = 0; i < pieces.Count; i++)
                {
                    isArc[i] = TryReadArc(pieces, i, out arcs[i]);
                }
            }

            _arcs = arcs;
            _isArc = isArc;
        }

        private static bool TryReadArc(BezierPieces pieces, int piece, out PieceArc arc)
        {
            arc = default(PieceArc);
            int at = pieces.PointOffset(piece);
            int weight = pieces.WeightOffset(piece);
            double x0 = pieces.Points[at];
            double y0 = pieces.Points[at + 1];
            double x1 = pieces.Points[at + 2];
            double y1 = pieces.Points[at + 3];
            double x2 = pieces.Points[at + 4];
            double y2 = pieces.Points[at + 5];
            double w0 = pieces.Weights[weight];
            double w1 = pieces.Weights[weight + 1];
            double w2 = pieces.Weights[weight + 2];

            if (!(w0 > 0.0 && w1 > 0.0 && w2 > 0.0))
            {
                return false;
            }

            double first = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            double second = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            double chord = Math.Sqrt((x2 - x0) * (x2 - x0) + (y2 - y0) * (y2 - y0));
            double side = 0.5 * (first + second);
            double middle = w1 / Math.Sqrt(w0 * w2);
            double size = Math.Max(Math.Max(Math.Max(Math.Abs(x0), Math.Abs(y0)), Math.Max(Math.Abs(x1), Math.Abs(y1))), Math.Max(Math.Abs(x2), Math.Abs(y2)));
            double rounding = 1E-12 * (size + side);

            // Isosceles, turning by less than half a turn, and weighted as the arc of that turn is.
            if (!(chord > rounding) || !(middle < 1.0) || !(Math.Abs(first - second) <= rounding) || !(Math.Abs(2.0 * side * middle - chord) <= rounding))
            {
                return false;
            }

            // The centre lies on the line from the middle control point through the middle of the chord, as far from the middle control
            // point as the side squared over that distance.
            double mx = 0.5 * (x0 + x2);
            double my = 0.5 * (y0 + y2);
            double dx = mx - x1;
            double dy = my - y1;
            double toMiddle = dx * dx + dy * dy;

            if (!(toMiddle > 0.0))
            {
                return false;
            }

            double reach = side * side / toMiddle;
            double cx = x1 + dx * reach;
            double cy = y1 + dy * reach;
            double radius = 0.5 * (Math.Sqrt((x0 - cx) * (x0 - cx) + (y0 - cy) * (y0 - cy)) + Math.Sqrt((x2 - cx) * (x2 - cx) + (y2 - cy) * (y2 - cy)));
            double sx = x0 - cx;
            double sy = y0 - cy;
            double ex = x2 - cx;
            double ey = y2 - cy;

            // The rounding of the control points, the larger of a few units of the last place of the numbers or of the radius, since an
            // arc is made from its centre, and how far the piece is seen to miss being isosceles and weighted as an arc, carried to the
            // centre by the square of the radius over the chord: the distance from the middle control point to the chord, which the centre
            // is read from, is that small beside the radius.
            double off = Math.Max(4.4E-16 * Math.Max(size, radius), Math.Max(Math.Abs(first - second), Math.Abs(2.0 * side * middle - chord)));

            arc.Cx = cx;
            arc.Cy = cy;
            arc.Radius = radius;
            arc.Start = Math.Atan2(sy, sx);
            arc.Sweep = Math.Atan2(sx * ey - sy * ex, sx * ex + sy * ey);
            arc.Error = off * (1.0 + 4.0 * radius * radius / (chord * chord));
            return radius > 0.0;
        }
    }
}
