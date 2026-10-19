using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A point as the graphics card holds it: three floats, measured from the render origin.
    /// </summary>
    public readonly struct Vec3F
    {
        /// <summary>
        /// Initializes a point.
        /// </summary>
        public Vec3F(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the X coordinate.
        /// </summary>
        public float X { get; }

        /// <summary>
        /// Gets the Y coordinate.
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets the Z coordinate.
        /// </summary>
        public float Z { get; }
    }

    /// <summary>
    /// How the double precision scene is handed to a graphics card that works in floats. Everything stays double on the
    /// processor; what goes to the card is the point less the render origin, and the origin is moved to the camera's target
    /// whenever the target has strayed so far from it that a float could no longer tell a pixel's thousandth.
    /// </summary>
    /// <remarks>
    /// A float keeps 24 bits, so a coordinate r from the origin is off by up to r * 2^-24. With the origin at the middle of a
    /// scene 60 000 long, a needle 0.0005 wide 30 000 from it is drawn 0 or 0.001 wide; with the origin on the needle it is drawn
    /// 0.0005 wide to 1E-7.
    /// </remarks>
    public static class RenderSpace
    {
        /// <summary>
        /// The share of a pixel the error of a float may reach: a thousandth.
        /// </summary>
        public const double PixelShare = 1E-3;

        private const double HalfUlp = 1.0 / 16777216.0;

        /// <summary>
        /// Gets a point relative to the origin, as the graphics card holds it.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="origin">The render origin.</param>
        public static Vec3F Relative(Vec3 point, Vec3 origin)
            => new Vec3F((float)(point.X - origin.X), (float)(point.Y - origin.Y), (float)(point.Z - origin.Z));

        /// <summary>
        /// Gets how far from the render origin the target of the camera may be before the origin has to move: the distance at
        /// which a float is off by <see cref="PixelShare"/> of a pixel, the error being counted as two half units in the last
        /// place, one for the coordinate and one for the matrix it is carried through.
        /// </summary>
        /// <param name="camera">The camera; its distance gives the size of a pixel.</param>
        /// <param name="viewHeightPixels">The height of the view in pixels.</param>
        public static double MaxOffset(ViewCamera camera, double viewHeightPixels)
        {
            double pixel = 2.0 * camera.Distance * camera.HalfHeightRatio / Math.Max(1.0, viewHeightPixels);
            return pixel * PixelShare / (2.0 * HalfUlp);
        }

        /// <summary>
        /// Gets how many decimals a coordinate is shown with so that a hundredth of a pixel can be told: a pixel of 0.1 mm gets
        /// three, one of 0.00001 mm seven, from none up to nine.
        /// </summary>
        /// <param name="pixelSize">The size of a pixel on the plane shown, in millimetres.</param>
        public static int DecimalsFor(double pixelSize)
            => pixelSize > 0.0 && !double.IsInfinity(pixelSize) ? (int)Math.Max(0.0, Math.Min(9.0, Math.Ceiling(Math.Log10(100.0 / pixelSize)))) : 3;

        /// <summary>
        /// Gets whether the origin has to move to the target of the camera.
        /// </summary>
        /// <param name="origin">The render origin now.</param>
        /// <param name="camera">The camera.</param>
        /// <param name="viewHeightPixels">The height of the view in pixels.</param>
        public static bool NeedsRebase(Vec3 origin, ViewCamera camera, double viewHeightPixels)
            => (camera.Target - origin).Length > MaxOffset(camera, viewHeightPixels);
    }
}
