using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A coordinate system the user works in: an origin and three perpendicular unit axes. The grid, the readout of the cursor, the point a
    /// click lands on where it hits nothing, and the offsets of a measured distance are all taken on it. The global one is the plane z = 0
    /// with the axes of the scene. It is kept in double precision and holds no screen type.
    /// </summary>
    /// <remarks>
    /// The axes are made perpendicular and of length one when the plane is made, so that a point taken to the plane and back comes
    /// back to within a few units in the last place of its largest coordinate: 1E-12 on coordinates of a few thousand.
    /// </remarks>
    public sealed class WorkPlane
    {
        /// <summary>
        /// How many grid lines cross the scene, about, whatever its size: twenty.
        /// </summary>
        public const int GridLinesAcross = 20;

        private static readonly WorkPlane _global = new WorkPlane(default(Vec3), new Vec3(1.0, 0.0, 0.0), new Vec3(0.0, 1.0, 0.0), new Vec3(0.0, 0.0, 1.0), "global");

        private WorkPlane(Vec3 origin, Vec3 x, Vec3 y, Vec3 z, string description)
        {
            Origin = origin;
            XAxis = x;
            YAxis = y;
            ZAxis = z;
            Description = description;
            IsGlobal = origin.Equals(default(Vec3)) && x.Equals(new Vec3(1.0, 0.0, 0.0)) && y.Equals(new Vec3(0.0, 1.0, 0.0)) && z.Equals(new Vec3(0.0, 0.0, 1.0));
        }

        /// <summary>
        /// Gets the global plane: origin at nought, the axes of the scene, z = 0 the plane.
        /// </summary>
        public static WorkPlane Global => _global;

        /// <summary>
        /// Gets the origin, in world coordinates.
        /// </summary>
        public Vec3 Origin { get; }

        /// <summary>
        /// Gets the unit vector along the x axis of the plane.
        /// </summary>
        public Vec3 XAxis { get; }

        /// <summary>
        /// Gets the unit vector along the y axis of the plane.
        /// </summary>
        public Vec3 YAxis { get; }

        /// <summary>
        /// Gets the unit vector square to the plane, the z axis: the x axis turned towards the y axis by the right-hand rule.
        /// </summary>
        public Vec3 ZAxis { get; }

        /// <summary>
        /// Gets how the plane was made: <c>global</c>, <c>3 points</c>, <c>face</c> or <c>view</c>.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets whether this is the global plane, with the origin at nought and the axes of the scene.
        /// </summary>
        public bool IsGlobal { get; }

        /// <summary>
        /// Makes a plane from three picked points: the origin, a point along the x axis, and a point in the xy plane on the side the y axis points to.
        /// </summary>
        /// <param name="origin">The origin.</param>
        /// <param name="onX">A point on the x axis; the axis runs from the origin to it.</param>
        /// <param name="inPlane">A point of the plane off the x axis; the y axis is the part of the way to it square to the x axis.</param>
        /// <returns>The plane.</returns>
        /// <exception cref="ArgumentException">Thrown when the first two points are the same, or the three are on a line (the plane is then not told).</exception>
        public static WorkPlane FromThreePoints(Vec3 origin, Vec3 onX, Vec3 inPlane)
        {
            CheckFinite(origin, nameof(origin));
            CheckFinite(onX, nameof(onX));
            CheckFinite(inPlane, nameof(inPlane));
            Vec3 x = (onX - origin).Normalized();

            if (x.Equals(default(Vec3)))
            {
                throw new ArgumentException("The point on the x axis is the origin; the axis has no direction.", nameof(onX));
            }

            Vec3 toThird = inPlane - origin;
            Vec3 across = toThird - x * Vec3.Dot(toThird, x);

            // A point whose distance from the x axis is a billionth of its distance from the origin is on the axis.
            if (!(across.Length > 1E-9 * toThird.Length) || toThird.Length == 0.0)
            {
                throw new ArgumentException("The three points are on a line; they do not tell a plane.", nameof(inPlane));
            }

            return Orthonormal(origin, x, across, "3 points");
        }

        /// <summary>
        /// Makes a plane lying on a face: the origin on it, the z axis its normal, and the x axis the direction given as near as the plane allows.
        /// </summary>
        /// <param name="origin">A point of the face; the origin of the plane.</param>
        /// <param name="normal">The normal of the face.</param>
        /// <param name="xHint">The direction the x axis should be as near to as it can in the plane, such as the right of the view; where it is square to the plane, or none, the axes of the scene are tried.</param>
        /// <returns>The plane.</returns>
        /// <exception cref="ArgumentException">Thrown when the normal has no length.</exception>
        public static WorkPlane FromNormal(Vec3 origin, Vec3 normal, Vec3 xHint)
        {
            CheckFinite(origin, nameof(origin));
            CheckFinite(normal, nameof(normal));
            Vec3 z = normal.Normalized();

            if (z.Equals(default(Vec3)))
            {
                throw new ArgumentException("The normal has no length.", nameof(normal));
            }

            foreach (Vec3 hint in new[] { xHint, new Vec3(1.0, 0.0, 0.0), new Vec3(0.0, 1.0, 0.0) })
            {
                Vec3 x = hint - z * Vec3.Dot(hint, z);

                if (hint.IsFinite && x.Length > 1E-6 * Math.Max(hint.Length, 1E-300))
                {
                    return Orthonormal(origin, x, Vec3.Cross(z, x), "face");
                }
            }

            throw new ArgumentException("No direction in the plane could be found.", nameof(xHint));
        }

        /// <summary>
        /// Makes a plane lying on the view: the x axis to the right of the screen and the y axis up it, so that its z axis comes towards the eye.
        /// </summary>
        /// <param name="origin">The origin, such as the target of the view.</param>
        /// <param name="right">The direction to the right of the screen.</param>
        /// <param name="up">The direction up the screen.</param>
        /// <returns>The plane.</returns>
        /// <exception cref="ArgumentException">Thrown when the two directions are along one line or one has no length.</exception>
        public static WorkPlane FromView(Vec3 origin, Vec3 right, Vec3 up)
        {
            CheckFinite(origin, nameof(origin));
            Vec3 x = right.Normalized();
            Vec3 across = up - x * Vec3.Dot(up, x);

            if (x.Equals(default(Vec3)) || !(across.Length > 1E-9 * up.Length) || up.Length == 0.0)
            {
                throw new ArgumentException("The right and up of a view must be two directions across each other.");
            }

            return Orthonormal(origin, x, across, "view");
        }

        /// <summary>
        /// Takes a point of the scene to the plane: its offsets along the three axes from the origin. The z is the distance from the plane, positive on the side the z axis points to.
        /// </summary>
        /// <param name="world">The point, in world coordinates.</param>
        public Vec3 ToLocal(Vec3 world)
        {
            Vec3 d = world - Origin;
            return new Vec3(Vec3.Dot(d, XAxis), Vec3.Dot(d, YAxis), Vec3.Dot(d, ZAxis));
        }

        /// <summary>
        /// Takes a point given on the plane to the scene.
        /// </summary>
        /// <param name="local">The offsets along the x, y and z axes from the origin.</param>
        public Vec3 ToWorld(Vec3 local) => Origin + XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

        /// <summary>
        /// Takes a direction or an offset of the scene to the plane: the same turn as <see cref="ToLocal"/> without the shift to the origin.
        /// </summary>
        /// <param name="world">The vector, in world coordinates.</param>
        public Vec3 DirectionToLocal(Vec3 world) => new Vec3(Vec3.Dot(world, XAxis), Vec3.Dot(world, YAxis), Vec3.Dot(world, ZAxis));

        /// <summary>
        /// Takes a direction given along the axes of the plane to the scene.
        /// </summary>
        /// <param name="local">The components along the x, y and z axes.</param>
        public Vec3 DirectionToWorld(Vec3 local) => XAxis * local.X + YAxis * local.Y + ZAxis * local.Z;

        /// <summary>
        /// Gets how far a point is from the plane, positive on the side the z axis points to.
        /// </summary>
        /// <param name="world">The point.</param>
        public double SignedDistance(Vec3 world) => Vec3.Dot(world - Origin, ZAxis);

        /// <summary>
        /// Finds where a ray meets the plane.
        /// </summary>
        /// <param name="rayOrigin">Where the ray starts.</param>
        /// <param name="direction">The direction of the ray, of length one.</param>
        /// <param name="bothWays">Whether the ray runs backwards too, as an orthographic one does.</param>
        /// <param name="point">The point of the plane the ray meets.</param>
        /// <returns>false when the ray runs along the plane (within a billionth of it) or, going forwards only, away from it.</returns>
        public bool TryIntersect(Vec3 rayOrigin, Vec3 direction, bool bothWays, out Vec3 point)
        {
            double slope = Vec3.Dot(direction, ZAxis);
            point = default(Vec3);

            if (Math.Abs(slope) < 1E-9)
            {
                return false;
            }

            double along = Vec3.Dot(Origin - rayOrigin, ZAxis) / slope;

            if (!bothWays && !(along > 0.0))
            {
                return false;
            }

            point = rayOrigin + direction * along;
            return true;
        }

        /// <summary>
        /// Gets the lines of the grid that lies on the plane over a scene: lines along the x and the y axes, a round step apart (1, 2 or 5 times a
        /// power of ten) so that about <see cref="GridLinesAcross"/> of them cross the scene, reaching one step past it on each side.
        /// The global plane gives the lines of the plane z = 0 over the box.
        /// </summary>
        /// <param name="scene">The box round the scene.</param>
        /// <returns>One two-point line each, in world coordinates; none where the box is empty.</returns>
        public List<DrawPolyline> GridLines(Bounds3 scene)
        {
            var lines = new List<DrawPolyline>();

            if (scene.IsEmpty)
            {
                return lines;
            }

            double lowX = double.MaxValue;
            double highX = double.MinValue;
            double lowY = double.MaxValue;
            double highY = double.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vec3 local = ToLocal(new Vec3((i & 1) == 0 ? scene.Min.X : scene.Max.X, (i & 2) == 0 ? scene.Min.Y : scene.Max.Y, (i & 4) == 0 ? scene.Min.Z : scene.Max.Z));
                lowX = Math.Min(lowX, local.X);
                highX = Math.Max(highX, local.X);
                lowY = Math.Min(lowY, local.Y);
                highY = Math.Max(highY, local.Y);
            }

            double span = Math.Max(highX - lowX, highY - lowY);
            double step = NiceStep(Math.Max(span, 1.0) / GridLinesAcross);
            double minX = Math.Floor(lowX / step - 1.0) * step;
            double maxX = Math.Ceiling(highX / step + 1.0) * step;
            double minY = Math.Floor(lowY / step - 1.0) * step;
            double maxY = Math.Ceiling(highY / step + 1.0) * step;

            for (double x = minX; x <= maxX + step * 0.5; x += step)
            {
                lines.Add(new DrawPolyline(new[] { ToWorld(new Vec3(x, minY, 0.0)), ToWorld(new Vec3(x, maxY, 0.0)) }, false));
            }

            for (double y = minY; y <= maxY + step * 0.5; y += step)
            {
                lines.Add(new DrawPolyline(new[] { ToWorld(new Vec3(minX, y, 0.0)), ToWorld(new Vec3(maxX, y, 0.0)) }, false));
            }

            return lines;
        }

        /// <summary>
        /// Gets the round step at or above a size: 1, 2 or 5 times a power of ten.
        /// </summary>
        /// <param name="raw">The size, above nought.</param>
        public static double NiceStep(double raw)
        {
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double scaled = raw / power;
            return (scaled <= 1.0 ? 1.0 : scaled <= 2.0 ? 2.0 : scaled <= 5.0 ? 5.0 : 10.0) * power;
        }

        /// <summary>
        /// Describes the plane for the status bar and the output: how it was made, and its origin and axes.
        /// </summary>
        public override string ToString()
            => IsGlobal
                ? "global (z = 0)"
                : Description + ": origin " + Origin + ", x " + Format(XAxis) + ", y " + Format(YAxis) + ", z " + Format(ZAxis);

        private static string Format(Vec3 v) => "(" + Short(v.X) + ", " + Short(v.Y) + ", " + Short(v.Z) + ")";

        private static string Short(double value)
        {
            string text = value.ToString("0.#####", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        private static WorkPlane Orthonormal(Vec3 origin, Vec3 x, Vec3 yHint, string description)
        {
            // Two passes of Gram-Schmidt, so that the axes are perpendicular to a unit in the last place however skew the hint was.
            Vec3 ux = x.Normalized();
            Vec3 y = yHint - ux * Vec3.Dot(yHint, ux);
            y = (y - ux * Vec3.Dot(y, ux)).Normalized();
            Vec3 z = Vec3.Cross(ux, y).Normalized();
            y = Vec3.Cross(z, ux).Normalized();
            return new WorkPlane(origin, ux, y, z, description);
        }

        private static void CheckFinite(Vec3 point, string name)
        {
            if (!point.IsFinite)
            {
                throw new ArgumentException("The point is not a finite number.", name);
            }
        }
    }
}
