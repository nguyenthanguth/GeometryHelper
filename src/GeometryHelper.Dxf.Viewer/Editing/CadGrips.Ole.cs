using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    public static partial class CadGrips
    {
        /// <summary>
        /// The corner grips of an OLE frame, as AutoCAD 2021 gives them: its lower left, upper left, upper right and lower right
        /// corners in turn; none for a frame of no width or height.
        /// </summary>
        private static void OleGrips(DxfOle2Frame frame, Action<double, double> add)
        {
            if (!(frame.WcsWidth > 0.0) || !(frame.WcsHeight > 0.0))
            {
                return;
            }

            DxfRectangle3d corners = frame.Position3d;
            add(corners.LowerLeft.X, corners.LowerLeft.Y);
            add(corners.UpperLeft.X, corners.UpperLeft.Y);
            add(corners.UpperRight.X, corners.UpperRight.Y);
            add(corners.LowerRight.X, corners.LowerRight.Y);
        }

        /// <summary>
        /// A corner of an OLE frame dragged: the frame scaled about the corner across from it, each side by the point's distance
        /// from that corner over the grip's, the larger of the two for both where the aspect is locked, the frame kept on its
        /// side of the corner. A frame that would have no width or no height is refused.
        /// </summary>
        private static CadEditResult ResizeOle(
            CadDocument document,
            string handle,
            string property,
            string was,
            string went,
            IReadOnlyList<CadGrip> grips,
            int index,
            GeoPoint2 to
        )
        {
            var frame = (DxfOle2Frame)document.FindEntity(handle);
            GeoPoint2 kept = grips[(index + 2) % 4].Point;
            GeoPoint2 grip = grips[index].Point;
            double width = Math.Abs(grip.X - kept.X);
            double height = Math.Abs(grip.Y - kept.Y);
            double least = Tolerance.Default.EqualPoint;
            double across = Math.Abs(to.X - kept.X) / width;
            double up = Math.Abs(to.Y - kept.Y) / height;

            if (frame.LockAspect)
            {
                across = Math.Max(across, up);
                up = across;
            }

            if (!(across * width > least) || !(up * height > least))
            {
                return CadEditResult.Refuse("An OLE object needs a width and a height above nought.");
            }

            double x = kept.X + (grip.X >= kept.X ? 1.0 : -1.0) * across * width;
            double y = kept.Y + (grip.Y >= kept.Y ? 1.0 : -1.0) * up * height;
            double left = Math.Min(kept.X, x);
            double right = Math.Max(kept.X, x);
            double bottom = Math.Min(kept.Y, y);
            double top = Math.Max(kept.Y, y);
            double z = frame.Position3d.UpperLeft.Z;
            var resized = new DxfRectangle3d(
                new GeoPoint3(left, top, z),
                new GeoPoint3(right, top, z),
                new GeoPoint3(left, bottom, z),
                new GeoPoint3(right, bottom, z)
            );
            return Reshape(document, handle, property, was, went, () =>
            {
                ((DxfOle2Frame)document.FindEntity(handle)).Position3d = resized;
                return true;
            });
        }
    }
}
