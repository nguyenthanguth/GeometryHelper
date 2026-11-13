using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The OLE frames of a drawing, drawn as AutoCAD 2021 shows them: the picture a frame holds, then its frame.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// An OLE frame: the picture it holds where it is a bitmap (<see cref="DisplayImage2"/>, the bytes of the file in place
        /// of a path), then its frame as the document's OLEFRAME says (0 none, 1 shown and plotted, 2 shown and left off a
        /// plot). A frame whose picture cannot be drawn, an object of another application or a metafile, is drawn as its frame
        /// alone with its kind, as <see cref="DxfOle2Frame.UserType"/> names it, written in its corner, however OLEFRAME is set;
        /// such a frame is left off a plot unless OLEFRAME is 1. The document's OLEHIDE leaves out the frames of one space or
        /// both: 1 those of model space, 2 those of paper space, 3 all.
        /// </summary>
        private void DrawOle(
            DxfOle2Frame frame,
            Frame placement,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            int hide = _document?.OleHide ?? 0;
            string block = frame.BlockName ?? string.Empty;
            bool model = string.Equals(block, "*Model_Space", StringComparison.OrdinalIgnoreCase);
            bool paper = block.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase);

            if (hide == 3 || (hide == 1 && model) || (hide == 2 && paper))
            {
                return;
            }

            int shown = _document?.OleFrame ?? 2;
            Affine2 m = placement.Matrix;
            DxfRectangle3d corners = frame.Position3d;
            double x = m.X(corners.LowerLeft.X, corners.LowerLeft.Y);
            double y = m.Y(corners.LowerLeft.X, corners.LowerLeft.Y);
            double ux = m.X(corners.LowerRight.X, corners.LowerRight.Y) - x;
            double uy = m.Y(corners.LowerRight.X, corners.LowerRight.Y) - y;
            double vx = m.X(corners.UpperLeft.X, corners.UpperLeft.Y) - x;
            double vy = m.Y(corners.UpperLeft.X, corners.UpperLeft.Y) - y;
            byte[] bitmap = BitmapOf(frame);

            if (bitmap != null)
            {
                list.Add(
                    new DisplayImage2(x, y, ux, uy, vx, vy, null, false, null, string.Empty, style, handle, layerName)
                    {
                        Picture = bitmap,
                        PictureKey = PictureKeyOf(bitmap),
                        IsMonochrome = frame.OutputQuality == 0,
                        IsPickedInside = true,
                    }
                );
            }

            if (bitmap != null && shown == 0)
            {
                return;
            }

            int before = list.Count;

            foreach (object shape in frame.GetGeometry())
            {
                AddShape(shape, m, list, style, handle, layerName, false, false);
            }

            if (bitmap == null)
            {
                AddKind(frame, ux, uy, vx, vy, x, y, list, style, handle, layerName);
            }

            if (shown != 1)
            {
                for (int i = before; i < list.Count; i++)
                {
                    list.Items[i].IsNotPlotted = true;
                }
            }
        }

        /// <summary>The bytes of the bitmap file a frame holds, null where it holds none a picture can be made of: another kind of object, a metafile, a damaged file.</summary>
        private static byte[] BitmapOf(DxfOle2Frame frame)
        {
            try
            {
                byte[] data = frame.GetPictureData(out DxfOlePictureFormat format);
                return format == DxfOlePictureFormat.Dib ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A name for the bytes of a picture: their length and a 64-bit hash of them, so that equal pictures share one decoded image.</summary>
        private static string PictureKeyOf(byte[] bytes)
        {
            ulong hash = 14695981039346656037UL;

            foreach (byte b in bytes)
            {
                hash = (hash ^ b) * 1099511628211UL;
            }

            return "ole:"
                + bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The kind of an object that cannot be drawn, written in the corner of its frame a tenth of its shorter side high, as the name of a picture not found is.</summary>
        private void AddKind(
            DxfOle2Frame frame,
            double ux,
            double uy,
            double vx,
            double vy,
            double x,
            double y,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            string kind;

            try
            {
                kind = frame.UserType;
            }
            catch (Exception)
            {
                kind = null;
            }

            double width = Math.Sqrt(ux * ux + uy * uy);
            double height = Math.Sqrt(vx * vx + vy * vy);
            double size = Math.Min(width, height) / 10.0;

            if (string.IsNullOrEmpty(kind) || !(size > 0.0))
            {
                return;
            }

            double ax = ux / width;
            double ay = uy / width;
            double bx = vx / height;
            double by = vy / height;
            list.Add(
                Text(
                    FaceOf(null),
                    kind,
                    x + (ax + bx) * size / 2.0,
                    y + (ay + by) * size / 2.0,
                    size,
                    Math.Atan2(uy, ux),
                    1.0,
                    0,
                    0,
                    style,
                    handle,
                    layerName,
                    0.0,
                    false
                )
            );
        }
    }
}
