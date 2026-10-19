using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A view frozen at one moment: where the camera stands and how big the picture is, to take a point of the scene to a place
    /// in the picture and a place in the picture back to a ray through the scene. Places in the picture are in pixels, from the
    /// top left corner, x to the right and y down, as the window gives the mouse. It works in double precision from the exact
    /// scene, so the snaps, the picking and the box select do not depend on what the graphics card was given.
    /// </summary>
    public sealed class ScreenMap
    {
        private readonly ViewCamera _camera;
        private readonly Bounds3 _scene;
        private readonly Vec3 _eye;
        private readonly Vec3 _forward;
        private readonly Vec3 _right;
        private readonly Vec3 _up;
        private readonly double _half;
        private readonly double _aspect;

        /// <summary>
        /// Initializes a map.
        /// </summary>
        /// <param name="camera">The camera; it is copied, so moving it afterwards does not move the map.</param>
        /// <param name="scene">The box round the scene, which sets where an orthographic eye stands and the near plane.</param>
        /// <param name="widthPixels">The width of the picture in pixels; at least one.</param>
        /// <param name="heightPixels">The height of the picture in pixels; at least one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the camera is null.</exception>
        public ScreenMap(ViewCamera camera, Bounds3 scene, double widthPixels, double heightPixels)
        {
            _camera = (camera ?? throw new ArgumentNullException(nameof(camera))).Clone();
            _scene = scene;
            Width = Math.Max(1.0, widthPixels);
            Height = Math.Max(1.0, heightPixels);
            _aspect = Width / Height;
            _eye = _camera.EyePosition(scene);
            _forward = _camera.Forward;
            _right = _camera.Right;
            _up = _camera.Up;
            _half = _camera.Distance * _camera.HalfHeightRatio;
            _camera.ClipPlanes(scene, out double near, out double far);
            Near = near;
            Far = far;
        }

        /// <summary>
        /// Gets the width of the picture in pixels.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the height of the picture in pixels.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets whether the view has no perspective.
        /// </summary>
        public bool Orthographic => _camera.Orthographic;

        /// <summary>
        /// Gets the distance from the eye to the near plane: a perspective view shows nothing nearer.
        /// </summary>
        public double Near { get; }

        /// <summary>
        /// Gets where the eye stands.
        /// </summary>
        public Vec3 Eye => _eye;

        /// <summary>
        /// Gets the distance from the eye to the far plane: a perspective view shows nothing further, so nothing further can be picked, snapped to
        /// or selected.
        /// </summary>
        public double Far { get; }

        /// <summary>
        /// Gets whether a point is between the near and far planes of a perspective view, as everything in front of an orthographic eye is.
        /// </summary>
        /// <param name="point">The point.</param>
        public bool IsInDepth(Vec3 point)
        {
            if (_camera.Orthographic)
            {
                return true;
            }

            double depth = DepthOf(point);
            return depth >= Near * (1.0 - DepthSlack) && depth <= Far * (1.0 + DepthSlack);
        }

        /// <summary>
        /// The share of the near plane's distance a point may be short of it and still be in the picture: a billionth, so that the end of a
        /// segment cut at the near plane, which rounding leaves a hair either side of it, is not refused.
        /// </summary>
        public const double DepthSlack = 1E-9;

        /// <summary>
        /// Gets the distance from the eye a segment is cut at: the near plane and a billionth of it more, so that what is cut is inside the picture.
        /// </summary>
        public double ClipDepth => Near * (1.0 + DepthSlack);

        /// <summary>
        /// Gets the unit vector the camera looks along.
        /// </summary>
        public Vec3 Forward => _forward;

        /// <summary>
        /// Gets how far a point is from the eye along the view direction.
        /// </summary>
        /// <param name="point">The point.</param>
        public double DepthOf(Vec3 point) => Vec3.Dot(point - _eye, _forward);

        /// <summary>
        /// Gets the world length of one pixel on a plane square to the view through a point: the same everywhere in an orthographic
        /// view, and in proportion to the distance from the eye in a perspective one.
        /// </summary>
        /// <param name="point">The point.</param>
        public double PixelSizeAt(Vec3 point)
            => 2.0 * (_camera.Orthographic ? _camera.Distance : Math.Max(DepthOf(point), Near)) * _camera.HalfHeightRatio / Height;

        /// <summary>
        /// Takes a point of the scene to the picture.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="x">Where it is across the picture, in pixels from the left.</param>
        /// <param name="y">Where it is down the picture, in pixels from the top.</param>
        /// <param name="depth">How far it is from the eye along the view direction.</param>
        /// <returns>false when the point is not in front of the near plane of a perspective view, and so not in the picture at all.</returns>
        public bool TryProject(Vec3 point, out double x, out double y, out double depth)
        {
            Vec3 d = point - _eye;
            depth = Vec3.Dot(d, _forward);
            double across;
            double up;

            if (_camera.Orthographic)
            {
                Vec3 fromTarget = point - _camera.Target;
                across = Vec3.Dot(fromTarget, _right) / (_half * _aspect);
                up = Vec3.Dot(fromTarget, _up) / _half;
            }
            else
            {
                if (!(depth >= Near * (1.0 - DepthSlack) && depth <= Far * (1.0 + DepthSlack)))
                {
                    x = double.NaN;
                    y = double.NaN;
                    return false;
                }

                double scale = depth * _camera.HalfHeightRatio;
                across = Vec3.Dot(d, _right) / (scale * _aspect);
                up = Vec3.Dot(d, _up) / scale;
            }

            x = (across + 1.0) * 0.5 * Width;
            y = (1.0 - up) * 0.5 * Height;
            return !double.IsNaN(x) && !double.IsNaN(y);
        }

        /// <summary>
        /// Gets the ray through a place in the picture.
        /// </summary>
        /// <param name="x">Where it is across the picture, in pixels from the left.</param>
        /// <param name="y">Where it is down the picture, in pixels from the top.</param>
        /// <param name="origin">The eye for a perspective view; for an orthographic one the point on the plane through the target, whose ray runs both ways.</param>
        /// <param name="direction">The unit direction of the ray.</param>
        public void GetRay(double x, double y, out Vec3 origin, out Vec3 direction)
            => _camera.GetRay(2.0 * x / Width - 1.0, 1.0 - 2.0 * y / Height, _aspect, _scene, out origin, out direction);

        /// <summary>
        /// Gets the point of the plane through the target, square to the view, that shows at a place in the picture.
        /// </summary>
        /// <param name="x">Where it is across the picture, in pixels from the left.</param>
        /// <param name="y">Where it is down the picture, in pixels from the top.</param>
        public Vec3 PointOnTargetPlane(double x, double y)
            => _camera.PointOnTargetPlane(2.0 * x / Width - 1.0, 1.0 - 2.0 * y / Height, _aspect);

        /// <summary>
        /// Cuts a segment to the part between the near and far planes, which is all of it in an orthographic view.
        /// </summary>
        /// <param name="a">One end.</param>
        /// <param name="b">The other end.</param>
        /// <param name="from">Where the part that shows starts.</param>
        /// <param name="to">Where it ends.</param>
        /// <returns>false when none of the segment shows.</returns>
        public bool ClipSegment(Vec3 a, Vec3 b, out Vec3 from, out Vec3 to)
        {
            from = a;
            to = b;

            if (_camera.Orthographic)
            {
                return true;
            }

            // Cut by the near plane, then by the far plane: the part between them.
            double da = DepthOf(a) - ClipDepth;
            double db = DepthOf(b) - ClipDepth;

            if (da < 0.0 && db < 0.0)
            {
                return false;
            }

            if (da < 0.0)
            {
                from = a + (b - a) * (da / (da - db));
            }
            else if (db < 0.0)
            {
                to = b + (a - b) * (db / (db - da));
            }

            double clipFar = Far * (1.0 - DepthSlack);
            double fa = clipFar - DepthOf(from);
            double fb = clipFar - DepthOf(to);

            if (fa < 0.0 && fb < 0.0)
            {
                return false;
            }

            if (fa < 0.0)
            {
                from = from + (to - from) * (fa / (fa - fb));
            }
            else if (fb < 0.0)
            {
                to = to + (from - to) * (fb / (fb - fa));
            }
            return true;
        }
    }
}
