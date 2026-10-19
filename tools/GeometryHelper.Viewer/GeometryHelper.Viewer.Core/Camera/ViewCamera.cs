using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The standard views.
    /// </summary>
    public enum StandardView
    {
        /// <summary>
        /// From a corner above the scene, in perspective.
        /// </summary>
        Iso,

        /// <summary>
        /// Straight down, the plan, with +Y up the screen and no perspective.
        /// </summary>
        Top,

        /// <summary>
        /// From the front, looking along +Y with +Z up the screen, no perspective.
        /// </summary>
        Front,

        /// <summary>
        /// From the right, looking along -X with +Z up the screen, no perspective.
        /// </summary>
        Right,
    }

    /// <summary>
    /// The camera of the viewer, in double precision world coordinates, with the maths that moves it: pan, orbit, zoom about
    /// the cursor, fit to a box, and the planes that clip the scene. It holds no screen type, so the net48 harnesses and the
    /// tests can use it.
    /// </summary>
    /// <remarks>
    /// The camera looks at <see cref="Target"/> from <see cref="Distance"/> away, along a direction given by two angles:
    /// <see cref="Azimuth"/>, counter-clockwise seen from above from +X, and <see cref="Elevation"/> above the ground plane.
    /// +Z is up the screen at any elevation, and straight above or below the target the top of the screen is the way the
    /// camera came from, so there is no angle at which the view flips. In both projections one pixel is the same distance
    /// on the target plane: <c>2 * Distance * tan(FieldOfView / 2) / height</c> of the view in pixels.
    /// </remarks>
    public sealed class ViewCamera
    {
        /// <summary>
        /// The smallest distance the camera is brought to the target.
        /// </summary>
        public const double MinDistance = 1E-6;

        /// <summary>
        /// The largest distance the camera is taken from the target.
        /// </summary>
        public const double MaxDistance = 1E12;

        private double _distance = 1000.0;

        /// <summary>
        /// Gets or sets the point the camera looks at and turns about.
        /// </summary>
        public Vec3 Target { get; set; }

        /// <summary>
        /// Gets or sets the distance from the camera to the target, which also sets how much of the scene is in view in both
        /// projections. It is kept between <see cref="MinDistance"/> and <see cref="MaxDistance"/>.
        /// </summary>
        public double Distance
        {
            get => _distance;
            set => _distance = Math.Max(MinDistance, Math.Min(MaxDistance, value));
        }

        /// <summary>
        /// Gets or sets the angle of the camera round the target, in radians, counter-clockwise seen from above, from +X.
        /// </summary>
        public double Azimuth { get; set; } = -Math.PI / 4.0;

        /// <summary>
        /// Gets or sets how high the camera is above the ground plane through the target, in radians, kept within a quarter
        /// turn either way.
        /// </summary>
        public double Elevation
        {
            get => _elevation;
            set => _elevation = Math.Max(-Math.PI / 2.0, Math.Min(Math.PI / 2.0, value));
        }

        private double _elevation = Math.PI / 6.0;

        /// <summary>
        /// Gets or sets whether the view has no perspective.
        /// </summary>
        public bool Orthographic { get; set; }

        /// <summary>
        /// Gets the vertical angle of view, in radians: 45 degrees.
        /// </summary>
        public double FieldOfView => Math.PI / 4.0;

        /// <summary>
        /// Gets the unit vector from the target towards the camera along the ground plane.
        /// </summary>
        private Vec3 Ground => new Vec3(Math.Cos(Azimuth), Math.Sin(Azimuth), 0.0);

        /// <summary>
        /// Gets the unit vector the camera looks along.
        /// </summary>
        public Vec3 Forward => -(Ground * Math.Cos(Elevation) + new Vec3(0.0, 0.0, Math.Sin(Elevation)));

        /// <summary>
        /// Gets the unit vector up the screen.
        /// </summary>
        public Vec3 Up => Ground * -Math.Sin(Elevation) + new Vec3(0.0, 0.0, Math.Cos(Elevation));

        /// <summary>
        /// Gets the unit vector to the right of the screen.
        /// </summary>
        public Vec3 Right => Vec3.Cross(Forward, Up).Normalized();

        /// <summary>
        /// Gets where a perspective camera stands: <see cref="Distance"/> back from the target.
        /// </summary>
        public Vec3 Position => Target - Forward * Distance;

        /// <summary>
        /// Gets half the height of the view on the target plane over its distance, the tangent of half the field of view.
        /// </summary>
        public double HalfHeightRatio => Math.Tan(FieldOfView * 0.5);

        /// <summary>
        /// Makes an independent copy of the camera.
        /// </summary>
        public ViewCamera Clone()
            => new ViewCamera { Target = Target, Distance = Distance, Azimuth = Azimuth, Elevation = Elevation, Orthographic = Orthographic };

        /// <summary>
        /// Gets how far the eye stands from the target. A perspective camera stands <see cref="Distance"/> away. An orthographic
        /// one stands just outside the scene along the view direction, a twentieth of the scene's depth and a hundredth of the
        /// distance in front of its nearest point, since its distance only sets the scale: so the depth the graphics card has
        /// to tell apart is the depth of the scene and no more.
        /// </summary>
        /// <param name="scene">The box round the scene.</param>
        public double EyeDistance(Bounds3 scene)
        {
            if (!Orthographic)
            {
                return Distance;
            }

            DepthRange(scene, out double nearest, out double farthest, out double margin);
            return -nearest + margin;
        }

        /// <summary>
        /// Gets where the eye stands; see <see cref="EyeDistance"/>.
        /// </summary>
        /// <param name="scene">The box round the scene.</param>
        public Vec3 EyePosition(Bounds3 scene) => Target - Forward * EyeDistance(scene);

        /// <summary>
        /// Gets the depth of the scene along the view direction, measured from the target: negative nearer the camera.
        /// </summary>
        private void DepthRange(Bounds3 scene, out double nearest, out double farthest, out double margin)
        {
            nearest = -Distance;
            farthest = Distance;

            if (!scene.IsEmpty)
            {
                Vec3 forward = Forward;
                nearest = double.MaxValue;
                farthest = double.MinValue;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vec3((i & 1) == 0 ? scene.Min.X : scene.Max.X, (i & 2) == 0 ? scene.Min.Y : scene.Max.Y, (i & 4) == 0 ? scene.Min.Z : scene.Max.Z);
                    double depth = Vec3.Dot(corner - Target, forward);
                    nearest = Math.Min(nearest, depth);
                    farthest = Math.Max(farthest, depth);
                }
            }

            margin = 0.05 * (farthest - nearest) + 0.01 * Distance;
        }

        /// <summary>
        /// The most the far plane of a perspective view may lie beyond the near plane, as a multiple of it: a million. The
        /// graphics card tells depths apart in 24 bits, and the step it can tell at a distance z is about z squared over the near
        /// plane over 2^24, so at the far plane the step is
        /// about 600 times the distance to the target (z = 10 000 target distances, the near plane a hundredth of one), and with no bound it would grow
        /// without limit as the scene is made longer.
        /// </summary>
        public const double MaxDepthRatio = 1E6;

        /// <summary>
        /// Gets the planes that clip the scene: all of it in front of the camera between them, and no further apart than it takes.
        /// A perspective camera clips at a hundredth of its distance from the target, so that the target and what is near it
        /// are never cut however far the rest of the scene reaches, and at the farthest corner of the scene's box in front of the
        /// camera, but never further than <see cref="MaxDepthRatio"/> times the near plane: a part of the scene more than ten
        /// thousand target distances away is cut: the far plane of a camera 2 mm from its target is 20 m away and cuts a box
        /// 60 m long; an orthographic camera clips just outside the depth of the scene.
        /// </summary>
        /// <param name="scene">The box round the scene.</param>
        /// <param name="near">The distance from the eye to the near plane, above nought.</param>
        /// <param name="far">The distance from the eye to the far plane.</param>
        public void ClipPlanes(Bounds3 scene, out double near, out double far)
        {
            if (Orthographic)
            {
                DepthRange(scene, out double nearest, out double farthest, out double margin);
                near = margin * 0.5;
                far = farthest - nearest + 2.0 * margin;
                return;
            }

            double reach = Distance * 2.0;

            if (!scene.IsEmpty)
            {
                Vec3 eye = Position;
                Vec3 forward = Forward;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vec3((i & 1) == 0 ? scene.Min.X : scene.Max.X, (i & 2) == 0 ? scene.Min.Y : scene.Max.Y, (i & 4) == 0 ? scene.Min.Z : scene.Max.Z);
                    reach = Math.Max(reach, Vec3.Dot(corner - eye, forward));
                }
            }

            near = Distance * 0.01;
            far = Math.Min(reach * 1.05, near * MaxDepthRatio);
        }

        /// <summary>
        /// Gets the point of the plane through the target, square to the view, that appears at a place on the screen.
        /// </summary>
        /// <param name="ndcX">The place across the view, from -1 at the left edge to 1 at the right.</param>
        /// <param name="ndcY">The place up the view, from -1 at the bottom edge to 1 at the top.</param>
        /// <param name="aspect">The width of the view over its height.</param>
        public Vec3 PointOnTargetPlane(double ndcX, double ndcY, double aspect)
        {
            double half = Distance * HalfHeightRatio;
            return Target + Right * (ndcX * half * aspect) + Up * (ndcY * half);
        }

        /// <summary>
        /// Gets the ray through a place on the screen.
        /// </summary>
        /// <param name="ndcX">The place across the view, from -1 at the left edge to 1 at the right.</param>
        /// <param name="ndcY">The place up the view, from -1 at the bottom edge to 1 at the top.</param>
        /// <param name="aspect">The width of the view over its height.</param>
        /// <param name="scene">The box round the scene, which sets where an orthographic eye stands.</param>
        /// <param name="origin">The point the ray starts at: the eye for a perspective view, the point under the cursor on the target plane for an orthographic one, whose ray runs both ways.</param>
        /// <param name="direction">The unit direction of the ray.</param>
        public void GetRay(double ndcX, double ndcY, double aspect, Bounds3 scene, out Vec3 origin, out Vec3 direction)
        {
            Vec3 onPlane = PointOnTargetPlane(ndcX, ndcY, aspect);

            if (Orthographic)
            {
                direction = Forward;
                origin = onPlane;
            }
            else
            {
                origin = Position;
                direction = (onPlane - origin).Normalized();
            }
        }

        /// <summary>
        /// Moves the view with the mouse so that the scene follows it: dragging right moves the scene right.
        /// </summary>
        /// <param name="dxPixels">The mouse movement to the right.</param>
        /// <param name="dyPixels">The mouse movement down.</param>
        /// <param name="viewHeightPixels">The height of the view in pixels.</param>
        public void Pan(double dxPixels, double dyPixels, double viewHeightPixels)
        {
            double perPixel = 2.0 * Distance * HalfHeightRatio / Math.Max(1.0, viewHeightPixels);
            Target = Target - Right * (dxPixels * perPixel) + Up * (dyPixels * perPixel);
        }

        /// <summary>
        /// Moves the view by a share of its size: a positive share to the right and up.
        /// </summary>
        /// <param name="rightShare">The share of the width of the view.</param>
        /// <param name="upShare">The share of the height of the view.</param>
        /// <param name="aspect">The width of the view over its height.</param>
        public void PanBy(double rightShare, double upShare, double aspect)
        {
            double height = 2.0 * Distance * HalfHeightRatio;
            Target = Target + Right * (rightShare * height * aspect) + Up * (upShare * height);
        }

        /// <summary>
        /// Turns the view about the target with the mouse so that the scene follows it: dragging right turns the near side of
        /// the scene to the right.
        /// </summary>
        /// <param name="dxPixels">The mouse movement to the right.</param>
        /// <param name="dyPixels">The mouse movement down.</param>
        public void Orbit(double dxPixels, double dyPixels)
        {
            const double RadiansPerPixel = 0.4 * Math.PI / 180.0;
            Azimuth -= dxPixels * RadiansPerPixel;
            Elevation += dyPixels * RadiansPerPixel;
        }

        /// <summary>
        /// Turns the view about the target by angles.
        /// </summary>
        /// <param name="azimuthRadians">The turn of the camera round the target, counter-clockwise seen from above.</param>
        /// <param name="elevationRadians">The rise of the camera.</param>
        public void Rotate(double azimuthRadians, double elevationRadians)
        {
            Azimuth += azimuthRadians;
            Elevation += elevationRadians;
        }

        /// <summary>
        /// Turns the view about a point of the scene with the mouse, as <see cref="Orbit"/> does about the target, and moves the
        /// target so that the point stays where it is on the screen.
        /// </summary>
        /// <param name="center">The point to turn about.</param>
        /// <param name="dxPixels">The mouse movement to the right.</param>
        /// <param name="dyPixels">The mouse movement down.</param>
        public void OrbitAbout(Vec3 center, double dxPixels, double dyPixels)
        {
            const double RadiansPerPixel = 0.4 * Math.PI / 180.0;
            RotateAbout(center, -dxPixels * RadiansPerPixel, dyPixels * RadiansPerPixel);
        }

        /// <summary>
        /// Turns the view by angles about a point of the scene, and moves the target so that the point stays where it is on the
        /// screen, at the same depth: the point's offset from the target along the right, up and forward axes of the view is
        /// kept, and the target is put where that offset, along the new axes, ends on the point.
        /// </summary>
        /// <param name="center">The point to turn about.</param>
        /// <param name="azimuthRadians">The turn of the camera round the point, counter-clockwise seen from above.</param>
        /// <param name="elevationRadians">The rise of the camera.</param>
        public void RotateAbout(Vec3 center, double azimuthRadians, double elevationRadians)
        {
            Vec3 offset = center - Target;
            double x = Vec3.Dot(offset, Right);
            double y = Vec3.Dot(offset, Up);
            double z = Vec3.Dot(offset, Forward);

            Rotate(azimuthRadians, elevationRadians);
            Target = center - (Right * x + Up * y + Forward * z);
        }

        /// <summary>
        /// Zooms about the middle of the view.
        /// </summary>
        /// <param name="factor">How many times larger the scene gets; 2 brings it twice as near.</param>
        public void Zoom(double factor)
        {
            if (factor > 0.0 && !double.IsInfinity(factor))
            {
                Distance /= factor;
            }
        }

        /// <summary>
        /// Zooms about a place on the screen, which keeps the point of the scene under it where it is.
        /// </summary>
        /// <param name="factor">How many times larger the scene gets; 2 brings it twice as near.</param>
        /// <param name="ndcX">The place across the view, from -1 at the left edge to 1 at the right.</param>
        /// <param name="ndcY">The place up the view, from -1 at the bottom edge to 1 at the top.</param>
        /// <param name="aspect">The width of the view over its height.</param>
        public void ZoomAt(double factor, double ndcX, double ndcY, double aspect)
        {
            if (!(factor > 0.0) || double.IsInfinity(factor))
            {
                return;
            }

            // The scene is scaled about the point under the cursor on the target plane: the target moves towards it by the
            // same share the camera comes nearer, so the point stays where it was on the screen.
            Vec3 p = PointOnTargetPlane(ndcX, ndcY, aspect);
            double before = Distance;
            Distance = before / factor;
            double actual = before / Distance;
            Target = p + (Target - p) * (1.0 / actual);
        }

        /// <summary>
        /// Looks at a box from the direction the camera already has, near enough to take in all of it.
        /// </summary>
        /// <param name="box">The box; an empty one leaves the camera where it is.</param>
        /// <param name="aspect">The width of the view over its height.</param>
        public void Fit(Bounds3 box, double aspect)
        {
            if (box.IsEmpty)
            {
                return;
            }

            Target = box.Center;
            Vec3 half = box.Size * 0.5;

            if (half.Length <= 0.0)
            {
                Distance = 10.0;
                return;
            }

            Vec3 right = Right;
            Vec3 up = Up;
            Vec3 forward = Forward;
            double tanV = HalfHeightRatio;
            double tanH = tanV * Math.Max(aspect, 1E-6);
            double needed = 0.0;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vec3((i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z);
                double x = Math.Abs(Vec3.Dot(corner, right));
                double y = Math.Abs(Vec3.Dot(corner, up));
                double depth = Vec3.Dot(corner, forward);
                double wanted = Math.Max(x / tanH, y / tanV);

                // A perspective corner farther from the camera than the target is smaller on the screen, and one nearer is larger.
                needed = Math.Max(needed, Orthographic ? wanted : wanted - depth);
            }

            Distance = Math.Max(needed, box.Radius * 1.1) * 1.05;
        }

        /// <summary>
        /// Looks at the scene from a standard direction. The target, and the distance, stay.
        /// </summary>
        /// <param name="view">The view.</param>
        public void SetView(StandardView view)
        {
            switch (view)
            {
                case StandardView.Top:
                    Azimuth = -Math.PI / 2.0;
                    Elevation = Math.PI / 2.0;
                    Orthographic = true;
                    break;

                case StandardView.Front:
                    Azimuth = -Math.PI / 2.0;
                    Elevation = 0.0;
                    Orthographic = true;
                    break;

                case StandardView.Right:
                    Azimuth = 0.0;
                    Elevation = 0.0;
                    Orthographic = true;
                    break;

                default:
                    Azimuth = -Math.PI / 4.0;
                    Elevation = Math.PI / 6.0;
                    Orthographic = false;
                    break;
            }
        }
    }
}
