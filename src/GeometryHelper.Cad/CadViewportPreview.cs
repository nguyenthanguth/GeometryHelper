using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A viewport of a layout as MOVE, COPY, ROTATE, SCALE and MIRROR will leave it, for the ghost that follows the cursor:
    /// the frame and the view worked out from the transformation the cursor gives, by the rules the edit itself follows
    /// (<see cref="CadViewportClips.TurnPlain"/>, <see cref="CadViewportClips.Refit"/>).
    /// </summary>
    internal static class CadViewportPreview
    {
        private const double Alike = 1E-9;
        private const double QuarterTurn = 1E-6;

        /// <summary>
        /// The viewport once a transformation has been carried out on it, as the edit leaves it: a move takes the frame and the
        /// model with it; a scale scales the frame about the base point and leaves the model standing still on the sheet, the
        /// view's scale kept; a mirror puts the frame's centre at its mirror image, the frame's size and the model's orientation
        /// kept; a turn turns the frame and the view's twist by the angle, and puts at the middle of the frame the point of model
        /// space the edit does (the old centre of the view read as a point of model space, which is where it was with no twist
        /// and the target at the origin). A turn by other than a quarter makes a plain viewport clipped to the frame's corners
        /// turned. A viewport 200 by 100 about (110, 60) moved by (30, 20) is about (140, 80) still showing the same point of
        /// model space at its middle; scaled by 2 about (10, 10) it is 400 by 200 about (210, 110), the point of model space
        /// that was at (110, 60) on the sheet still there.
        /// </summary>
        /// <param name="v">The viewport as it is drawn now.</param>
        /// <param name="m">What the edit does to the sheet: a translation, a turn or a mirror about a point, a scale about a point.</param>
        /// <param name="target">The target of the viewport's view (<see cref="DxfViewport.ViewTarget"/>), which a turn reads.</param>
        /// <returns>The viewport as it will be drawn.</returns>
        internal static DisplayViewport2 After(DisplayViewport2 v, Affine2 m, GeoPoint2 target)
        {
            double centerX = m.X(v.CenterX, v.CenterY);
            double centerY = m.Y(v.CenterX, v.CenterY);
            double width = v.Width;
            double height = v.Height;
            DisplayClip2 clip = v.Clip?.Transformed(m);
            double twist = v.Twist;
            double s = m.Scale;
            Affine2 content;

            if (Math.Abs(s - 1.0) > Alike)
            {
                // The frame scales and the model stays where it is on the sheet.
                content = Affine2.Identity;
                width *= s;
                height *= s;
            }
            else if (m.Determinant < 0.0)
            {
                // The frame's centre goes to its mirror image, the model with it, but not itself mirrored.
                content = Affine2.Translation(centerX - v.CenterX, centerY - v.CenterY);
            }
            else
            {
                double angle = m.Rotation;
                double quarters = Math.Round(angle / (Math.PI / 2.0));
                bool square = Math.Abs(angle - quarters * Math.PI / 2.0) <= QuarterTurn;

                if (square && quarters == 0.0)
                {
                    content = m;
                }
                else
                {
                    if (square)
                    {
                        angle = quarters * Math.PI / 2.0;

                        if (Math.Abs(quarters % 2.0) == 1.0)
                        {
                            width = v.Height;
                            height = v.Width;
                        }
                    }
                    else
                    {
                        DisplayBox box = TurnedBox(v, m, out DisplayClip2 turned);
                        width = box.Width;
                        height = box.Height;
                        clip = clip ?? turned;
                    }

                    twist = v.Twist + angle;

                    // The view's centre as the viewport keeps it, in the view's coordinates; the edit reads it as a point of model space.
                    double cos = Math.Cos(v.Twist);
                    double sin = Math.Sin(v.Twist);
                    double x = v.ViewCenterX - target.X;
                    double y = v.ViewCenterY - target.Y;
                    double viewX = cos * x - sin * y;
                    double viewY = sin * x + cos * y;
                    bool clipped = v.Clip != null;

                    return Made(
                        v,
                        centerX,
                        centerY,
                        width,
                        height,
                        clipped ? 2.0 * viewX - centerX : viewX,
                        clipped ? 2.0 * viewY - centerY : viewY,
                        twist,
                        clip
                    );
                }
            }

            // The model carried by what the edit does to the sheet: the point of it at the middle of the frame is where that puts it.
            Affine2 now = v.ModelToPaper.Then(content);
            double c = Math.Cos(twist);
            double sn = Math.Sin(twist);
            double dx = centerX - now.E;
            double dy = centerY - now.F;
            return Made(
                v,
                centerX,
                centerY,
                width,
                height,
                (c * dx + sn * dy) / v.Scale,
                (-sn * dx + c * dy) / v.Scale,
                twist,
                clip
            );
        }

        private static DisplayViewport2 Made(
            DisplayViewport2 v,
            double centerX,
            double centerY,
            double width,
            double height,
            double viewX,
            double viewY,
            double twist,
            DisplayClip2 clip
        ) =>
            new DisplayViewport2(
                centerX,
                centerY,
                width,
                height,
                viewX,
                viewY,
                v.Scale,
                twist,
                v.Model,
                v.FrozenLayers,
                v.Style,
                v.Handle,
                v.Layer
            )
            {
                Clip = clip,
            };

        /// <summary>The box of a viewport's frame turned by a transformation, and the frame's four corners so turned as an outline.</summary>
        private static DisplayBox TurnedBox(DisplayViewport2 v, Affine2 m, out DisplayClip2 turned)
        {
            DisplayBox b = v.Bounds;
            var corners = new[] { b.MinX, b.MinY, b.MaxX, b.MinY, b.MaxX, b.MaxY, b.MinX, b.MaxY };
            var xy = new double[8];
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            for (int i = 0; i < 4; i++)
            {
                xy[2 * i] = m.X(corners[2 * i], corners[2 * i + 1]);
                xy[2 * i + 1] = m.Y(corners[2 * i], corners[2 * i + 1]);
                minX = Math.Min(minX, xy[2 * i]);
                maxX = Math.Max(maxX, xy[2 * i]);
                minY = Math.Min(minY, xy[2 * i + 1]);
                maxY = Math.Max(maxY, xy[2 * i + 1]);
            }

            turned = new DisplayClip2(xy, new double[4]);
            return new DisplayBox(minX, minY, maxX, maxY);
        }
    }
}
