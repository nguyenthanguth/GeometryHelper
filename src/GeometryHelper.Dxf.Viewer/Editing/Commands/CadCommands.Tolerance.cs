using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>TOLERANCE: a feature control frame from the Geometric Tolerance dialog, put where it is picked.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// TOLERANCE (TOL), as AutoCAD 2021 makes one: the Geometric Tolerance dialog, then "Enter tolerance location:", the
        /// frame following the cursor by its insertion point until the point is picked; it runs along the X axis of the UCS,
        /// in the current layer and dimension style. Cancel, or OK with every box empty, ends the command with no frame; with
        /// no window, a line saying so.
        /// </summary>
        private static IEnumerable<CadPrompt> FeatureControlFrame(CadCommandContext c)
        {
            if (!ToleranceDialog(c, out string text) || text == null)
            {
                yield break;
            }

            GeoVector3 along = ToleranceDirection(c);
            yield return CadPrompt
                .ForPoint("Enter tolerance location")
                .Previewing(CadPreview.Making(c, input => FrameParts(c, text, P3(input.Point), along)));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint3 at = P3(c.Input.Point);
            CadSpace space = c.Space;
            c.Apply(null, s => AddFeatureFrame(c, s, space, text, at, along));
        }

        /// <summary>
        /// The Geometric Tolerance dialog over every box empty, as AutoCAD 2021 opens it: the words of the frame on OK, null on
        /// Cancel and for a dialog left empty. False, with a line saying so, where there is no window to show it.
        /// </summary>
        private static bool ToleranceDialog(CadCommandContext c, out string text)
        {
            var request = new CadToleranceRequest(c.Document);
            text = null;

            if (!c.Session.Request("TOLERANCE", c.Document, request))
            {
                c.Write("Geometric Tolerance is shown by the window: no frame is made without it.");
                return false;
            }

            string written = request.Action == CadDialogAction.Ok ? request.Text : string.Empty;
            text = written.Length > 0 ? written : null;
            return true;
        }

        /// <summary>The way a frame runs, as AutoCAD 2021 puts one: along the X axis of the UCS.</summary>
        private static GeoVector3 ToleranceDirection(CadCommandContext c)
        {
            GeoVector2 x = c.Session.Ucs.ToWorld(new GeoVector2(1.0, 0.0));
            return new GeoVector3(x.X, x.Y, 0.0);
        }

        /// <summary>The parts of a frame of words at a point, as the frame made there draws them, for a preview.</summary>
        private static IReadOnlyList<DxfEntity> FrameParts(
            CadCommandContext c,
            string text,
            GeoPoint3 at,
            GeoVector3 along
        )
        {
            DxfTolerance look = Current(c, new DxfTolerance());
            string name = c.Database.Dimstyle;
            DxfDimStyleTableRecord style = c.Database.DimStyleTable.Has(name)
                ? c.Database.DimStyleTable[name]
                : c.Database.GetDimstyleData();
            return CadToleranceFrame
                .Of(text, at, along, new GeoVector3(0.0, 0.0, 1.0), style, look, c.Document.LayoutOf)
                .Parts;
        }

        /// <summary>Adds a frame of words at a point along a direction, in the current layer and dimension style.</summary>
        private static DxfTolerance AddFeatureFrame(
            CadCommandContext c,
            CadEditScope s,
            CadSpace space,
            string text,
            GeoPoint3 at,
            GeoVector3 along
        )
        {
            var frame = new DxfTolerance
            {
                Text = text,
                Location = at,
                Direction = along,
            };
            s.Append(space, Current(c, frame));
            string name = c.Database.Dimstyle;

            if (s.Database.DimStyleTable.Has(name))
            {
                frame.DimensionStyleName = s.Database.DimStyleTable[name].Name;
            }

            return frame;
        }

        /// <summary>
        /// Adds a copy of a TOLERANCE as the words of a leader, as AutoCAD 2021's Copy and Reuse Current copy one: its words,
        /// dimension style and overrides kept, its insertion point at the leader's end, along the X axis of the UCS in the
        /// drawing's plane (probe GHTOL4: the frame at (200, 200) copied to a leader ending at (110, 105) stands at (110, 105)).
        /// </summary>
        private static DxfTolerance FrameCopyAtEnd(
            CadEditScope s,
            CadSpace space,
            DxfTolerance source,
            GeoPoint3 end,
            GeoVector2 xAxis
        )
        {
            DxfEntity copy = source.GetTransformedCopy(GeoTransform3.Translation(source.Location.GetVectorTo(end)));
            CadOwnerTag.Remove(copy);
            var frame = (DxfTolerance)s.Append(space, copy);
            frame.Normal = new GeoVector3(0.0, 0.0, 1.0);
            frame.Direction = new GeoVector3(xAxis.X, xAxis.Y, 0.0);
            frame.Location = end;
            return frame;
        }

        /// <summary>
        /// The width of the first row of a frame of the drawing, as the canvas lays it out: what a leader on its left sets it
        /// back by, as AutoCAD 2021 sets back a frame copied to a leader drawn to the left (<c>{\Fgdt;j}%%v0.1</c> at DIMTXT
        /// 0.18 in Arial, 0.92296).
        /// </summary>
        private static double FrameWidth(CadDocument document, DxfTolerance frame) =>
            CadToleranceFrame.Of(frame, document.LayoutOf).FirstWidth;
    }
}
