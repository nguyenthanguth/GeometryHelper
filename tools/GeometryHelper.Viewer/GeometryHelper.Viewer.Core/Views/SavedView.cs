using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A named look at the scene: where the eye stands, what it looks at, which way is up, whether there is perspective, and how wide the view is.
    /// It is kept in the scene file, so that a repro opens at the spot it is about, and it is the same in every view whatever the shape of its window.
    /// </summary>
    /// <remarks>
    /// The camera of the viewer never rolls: +Z is up the screen at every angle but straight up or straight down (within a twentieth of a degree of it), where <see cref="Up"/>
    /// says which way the top of the screen points. Anywhere else <see cref="Up"/> is kept in the file and not read.
    /// </remarks>
    public sealed class SavedView
    {
        /// <summary>
        /// The sideways part of the line of sight, as a share of its length, below which a view counts as straight down or up: a thousandth, 0.057 degrees.
        /// </summary>
        private const double SteepLimit = 1E-3;

        /// <summary>
        /// Initializes a saved view.
        /// </summary>
        /// <param name="name">The name; not empty.</param>
        /// <param name="eye">Where the eye stands.</param>
        /// <param name="target">What it looks at, which is the middle of the view and the point it turns about.</param>
        /// <param name="up">The direction of the top of the screen; it need not be square to the line of sight.</param>
        /// <param name="orthographic">Whether the view has no perspective.</param>
        /// <param name="width">How wide the view is on the plane through the target, in millimetres, for a view with no perspective; nought takes the distance from the eye to the target for the scale instead. A view with perspective is as wide as its distance makes it, and the width is not read.</param>
        /// <exception cref="ArgumentException">Thrown when the name is empty, a number is not finite, the eye is on the target, or the width is negative.</exception>
        public SavedView(string name, Vec3 eye, Vec3 target, Vec3 up, bool orthographic, double width)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A saved view needs a name.", nameof(name));
            }

            if (!eye.IsFinite || !target.IsFinite || !up.IsFinite)
            {
                throw new ArgumentException("The eye, the target and the up of a view must be finite numbers.");
            }

            if (!((target - eye).Length > 0.0))
            {
                throw new ArgumentException("The eye is on the target; the view has no direction.", nameof(eye));
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width < 0.0)
            {
                throw new ArgumentException("The width of a view must be a finite number not below nought.", nameof(width));
            }

            Name = name;
            Eye = eye;
            Target = target;
            Up = up;
            Orthographic = orthographic;
            Width = width;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets where the eye stands.
        /// </summary>
        public Vec3 Eye { get; }

        /// <summary>
        /// Gets what the eye looks at.
        /// </summary>
        public Vec3 Target { get; }

        /// <summary>
        /// Gets the direction of the top of the screen.
        /// </summary>
        public Vec3 Up { get; }

        /// <summary>
        /// Gets whether the view has no perspective.
        /// </summary>
        public bool Orthographic { get; }

        /// <summary>
        /// Gets how wide the view is on the plane through the target, for a view with no perspective; nought where the scale is the distance from the eye to the target.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Takes the look a camera has.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="camera">The camera.</param>
        /// <param name="aspect">The width of the view the camera is in over its height, which sets how wide it is.</param>
        /// <returns>The view; a view with no perspective is as wide as the view it was taken from.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the camera is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty.</exception>
        public static SavedView FromCamera(string name, ViewCamera camera, double aspect)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            double width = 2.0 * camera.Distance * camera.HalfHeightRatio * Math.Max(aspect, 1E-6);
            return new SavedView(name, camera.Target - camera.Forward * camera.Distance, camera.Target, camera.Up, camera.Orthographic, camera.Orthographic ? width : 0.0);
        }

        /// <summary>
        /// Gets the camera that looks as the view does.
        /// </summary>
        /// <param name="aspect">The width of the view the camera is to be in over its height: a view with no perspective is made as wide as the saved view is, whatever the height it has.</param>
        public ViewCamera ToCamera(double aspect)
        {
            Vec3 look = Target - Eye;
            Vec3 forward = look.Normalized();
            double horizontal = Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y);
            var camera = new ViewCamera { Target = Target, Orthographic = Orthographic };

            camera.Elevation = Math.Atan2(-forward.Z, horizontal);

            // The eye and the target are points, each carried to a unit in the last place of its largest coordinate; a sideways part of the line of sight
            // no larger than that is rounding, and the camera is taken to look straight down or up.
            double scale = Math.Max(
                Math.Max(Math.Max(Math.Abs(Eye.X), Math.Abs(Eye.Y)), Math.Abs(Eye.Z)),
                Math.Max(Math.Max(Math.Abs(Target.X), Math.Abs(Target.Y)), Math.Abs(Target.Z)));
            var flat = new Vec3(Up.X, Up.Y, 0.0).Normalized();
            bool steep = horizontal <= Math.Max(SteepLimit, 1E-15 * scale / look.Length);

            // Where the line of sight is within a few hundredths of a degree of vertical its sideways part is rounding more than direction, and the way
            // the camera turns about it is better told by the top of the screen, which then points along the ground: straight down the top of the screen is
            // the way the camera came from, and straight up it is the way the camera is going. A view that gives no such direction goes by the line of sight.
            if (steep && !flat.Equals(default(Vec3)))
            {
                camera.Azimuth = forward.Z < 0.0 ? Math.Atan2(-flat.Y, -flat.X) : Math.Atan2(flat.Y, flat.X);
            }
            else if (horizontal > Math.Max(1E-9, 1E-15 * scale / look.Length))
            {
                camera.Azimuth = Math.Atan2(-forward.Y, -forward.X);
            }

            camera.Distance = Orthographic && Width > 0.0
                ? Width / (2.0 * camera.HalfHeightRatio * Math.Max(aspect, 1E-6))
                : look.Length;
            return camera;
        }
    }
}
