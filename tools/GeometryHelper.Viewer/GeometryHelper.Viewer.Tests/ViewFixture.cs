using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A scene built, drawn and looked at, for the tests of the snaps, the picking, the box select and the measuring: the primitives, the
    /// exact curves and a view of them, with the place on the screen of a point of the scene to hand to them.
    /// </summary>
    internal sealed class ViewFixture
    {
        internal const double Width = 1000.0;
        internal const double Height = 800.0;

        private ViewFixture(SceneWriter writer, StandardView view)
        {
            Scene = writer.Scene;
            Primitives = Primitives.Build(Scene);
            Geometry = ExactGeometry.Build(Scene, Primitives);
            Camera = new ViewCamera();
            Camera.SetView(view);
            Camera.Fit(Primitives.Bounds, Width / Height);
            Map = new ScreenMap(Camera, Primitives.Bounds, Width, Height);
        }

        internal Scene Scene { get; }

        internal Primitives Primitives { get; }

        internal ExactGeometry Geometry { get; }

        internal ViewCamera Camera { get; }

        internal ScreenMap Map { get; }

        /// <summary>
        /// Looks at the scene straight down, in the plan.
        /// </summary>
        internal static ViewFixture Top(SceneWriter writer) => new ViewFixture(writer, StandardView.Top);

        /// <summary>
        /// Looks at the scene from a corner above it, in perspective.
        /// </summary>
        internal static ViewFixture Iso(SceneWriter writer) => new ViewFixture(writer, StandardView.Iso);

        /// <summary>
        /// Gets where a point of the scene shows on the screen.
        /// </summary>
        internal (double X, double Y) Pixel(Vec3 point)
        {
            if (!Map.TryProject(point, out double x, out double y, out _))
            {
                throw new InvalidOperationException("The point " + point + " is not in the picture.");
            }

            return (x, y);
        }

        internal static void AssertNear(Vec3 expected, Vec3 actual, double tolerance = 1E-12)
        {
            double gap = (expected - actual).Length;

            if (!(gap <= tolerance))
            {
                throw new Xunit.Sdk.XunitException(expected + " expected, " + actual + " found, " + gap + " apart");
            }
        }
    }
}
