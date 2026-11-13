using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the presenter does with OLE frames beyond what the library does: a frame kept upright by an edit that would turn it,
    /// as AutoCAD 2021's GUI keeps it, and a new frame made of a picture and placed at the upper left of the view, as INSERTOBJ
    /// and PASTECLIP place one.
    /// </summary>
    internal static class CadOleFrames
    {
        /// <summary>
        /// Moves a frame by a transformation, as MOVE, ROTATE, SCALE and MIRROR move it. The library takes what keeps a frame
        /// along x and y (a move, a scale by one factor, a mirror about a line along x or y); a turn or a mirror about another
        /// line it refuses, and AutoCAD 2021's GUI then only moves the picture: the frame stays upright and as large, its centre
        /// where the transformation puts it. A frame 40 by 20 centred at (30, 30) turned 90 degrees about the origin stands 40
        /// by 20 centred at (-30, 30).
        /// </summary>
        /// <param name="frame">The frame.</param>
        /// <param name="transformation">The transformation.</param>
        internal static void Transform(DxfOle2Frame frame, GeoTransform3 transformation)
        {
            try
            {
                frame.TransformBy(transformation);
            }
            catch (ArgumentException)
            {
                frame.TransformBy(CentreMove(frame, transformation));
            }
        }

        /// <summary>A copy of a frame moved by a transformation as <see cref="Transform"/> moves a frame, in no drawing.</summary>
        /// <param name="frame">The frame.</param>
        /// <param name="transformation">The transformation.</param>
        /// <returns>The copy.</returns>
        internal static DxfEntity TransformedCopy(DxfOle2Frame frame, GeoTransform3 transformation)
        {
            try
            {
                return frame.GetTransformedCopy(transformation);
            }
            catch (ArgumentException)
            {
                return frame.GetTransformedCopy(CentreMove(frame, transformation));
            }
        }

        /// <summary>The move that takes the centre of a frame where a transformation puts it.</summary>
        private static GeoTransform3 CentreMove(DxfOle2Frame frame, GeoTransform3 transformation)
        {
            GeoAabb3 box = frame.GeometricExtents;
            var centre = new GeoPoint3(
                (box.Min.X + box.Max.X) / 2.0,
                (box.Min.Y + box.Max.Y) / 2.0,
                (box.Min.Z + box.Max.Z) / 2.0
            );
            GeoPoint3 to = transformation.Transform(centre);
            return GeoTransform3.Translation(new GeoVector3(to.X - centre.X, to.Y - centre.Y, 0.0));
        }

        /// <summary>
        /// Makes a static picture of the bytes of a bitmap file or a PNG and appends it to the space worked in, one undo step: at
        /// its natural size (96 dots an inch where the picture gives none), its upper left corner at the upper left of the view
        /// (of the space's extents where there is no view, its lower left corner at the origin where there are none), on the
        /// current layer, its plot quality that of OLEQUALITY where that is 0, 1 or 2.
        /// </summary>
        /// <param name="c">The command inserting it.</param>
        /// <param name="picture">The bytes.</param>
        /// <returns>Whether a frame was made; where not, the reason is written to the command line.</returns>
        internal static bool TryInsert(CadCommandContext c, byte[] picture)
        {
            DxfOle2Frame frame;

            try
            {
                frame = new DxfOle2Frame(picture);
            }
            catch (ArgumentException exception)
            {
                string why = exception.Message;
                int line = why.IndexOf('\n');
                c.Write(
                    "The file is no picture an OLE object can be made of: "
                        + (line < 0 ? why : why.Substring(0, line)).Trim()
                );
                return false;
            }

            CadSpace space = c.Space;
            CadView view = c.Session.WorkView;
            DisplayBox seen = view != null ? view.VisibleBox : c.Document.GetDisplay(space).Bounds;
            int quality = c.Document.OleQuality;
            c.Apply(null, s =>
            {
                var placed = (DxfOle2Frame)s.Append(space, CadCommands.Current(c, frame));

                if (!seen.IsEmpty)
                {
                    placed.TransformBy(
                        GeoTransform3.Translation(new GeoVector3(seen.MinX, seen.MaxY - placed.WcsHeight, 0.0))
                    );
                }

                if (quality >= 0 && quality <= 2)
                {
                    placed.OutputQuality = (byte)quality;
                }
            });
            return true;
        }
    }
}
