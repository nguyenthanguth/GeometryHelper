using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A transformation of the plane, <c>x' = A x + C y + E</c> and <c>y' = B x + D y + F</c>: how the entities of a block
    /// are placed in the drawing when an INSERT is drawn, kept as one matrix whatever the depth of the blocks.
    /// </summary>
    public readonly struct Affine2
    {
        /// <summary>Gets the identity: nothing moves.</summary>
        public static Affine2 Identity => new Affine2(1.0, 0.0, 0.0, 1.0, 0.0, 0.0);

        /// <summary>Makes a transformation from its six numbers.</summary>
        /// <param name="a">The X part of the image of the X axis.</param>
        /// <param name="b">The Y part of the image of the X axis.</param>
        /// <param name="c">The X part of the image of the Y axis.</param>
        /// <param name="d">The Y part of the image of the Y axis.</param>
        /// <param name="e">The X shift.</param>
        /// <param name="f">The Y shift.</param>
        public Affine2(double a, double b, double c, double d, double e, double f)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            E = e;
            F = f;
        }

        /// <summary>Gets the X part of the image of the X axis.</summary>
        public double A { get; }

        /// <summary>Gets the Y part of the image of the X axis.</summary>
        public double B { get; }

        /// <summary>Gets the X part of the image of the Y axis.</summary>
        public double C { get; }

        /// <summary>Gets the Y part of the image of the Y axis.</summary>
        public double D { get; }

        /// <summary>Gets the X shift.</summary>
        public double E { get; }

        /// <summary>Gets the Y shift.</summary>
        public double F { get; }

        /// <summary>Gets the determinant: negative when the transformation mirrors.</summary>
        public double Determinant => A * D - B * C;

        /// <summary>Gets whether the transformation is the identity.</summary>
        public bool IsIdentity => A == 1.0 && B == 0.0 && C == 0.0 && D == 1.0 && E == 0.0 && F == 0.0;

        /// <summary>
        /// Gets whether the transformation turns, mirrors and scales alike in both directions, so that a circle stays a
        /// circle and an arc stays an arc.
        /// </summary>
        public bool IsSimilarity
        {
            get
            {
                double sx = Math.Sqrt(A * A + B * B);
                double sy = Math.Sqrt(C * C + D * D);
                double dot = A * C + B * D;
                return Math.Abs(sx - sy) <= 1E-9 * Math.Max(sx, sy) && Math.Abs(dot) <= 1E-9 * sx * sy;
            }
        }

        /// <summary>Gets the factor by which lengths change, for a similarity.</summary>
        public double Scale => Math.Sqrt(Math.Abs(Determinant));

        /// <summary>Gets the angle the X axis turns to, in radians.</summary>
        public double Rotation => Math.Atan2(B, A);

        /// <summary>Gets the X of a point after the transformation.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>The new X.</returns>
        public double X(double x, double y) => A * x + C * y + E;

        /// <summary>Gets the Y of a point after the transformation.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <returns>The new Y.</returns>
        public double Y(double x, double y) => B * x + D * y + F;

        /// <summary>Gets the transformation that does this one first and then another.</summary>
        /// <param name="then">The transformation done after this one.</param>
        /// <returns>The combined transformation.</returns>
        public Affine2 Then(Affine2 then) =>
            new Affine2(
                then.A * A + then.C * B,
                then.B * A + then.D * B,
                then.A * C + then.C * D,
                then.B * C + then.D * D,
                then.A * E + then.C * F + then.E,
                then.B * E + then.D * F + then.F
            );

        /// <summary>Makes a translation.</summary>
        /// <param name="dx">The shift in X.</param>
        /// <param name="dy">The shift in Y.</param>
        /// <returns>The translation.</returns>
        public static Affine2 Translation(double dx, double dy) => new Affine2(1.0, 0.0, 0.0, 1.0, dx, dy);

        /// <summary>Makes the scales of the axes followed by a turn about the origin.</summary>
        /// <param name="sx">The scale of X.</param>
        /// <param name="sy">The scale of Y.</param>
        /// <param name="rotation">The turn in radians.</param>
        /// <returns>The transformation.</returns>
        public static Affine2 ScaleRotate(double sx, double sy, double rotation)
        {
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            return new Affine2(cos * sx, sin * sx, -sin * sy, cos * sy, 0.0, 0.0);
        }
    }
}
