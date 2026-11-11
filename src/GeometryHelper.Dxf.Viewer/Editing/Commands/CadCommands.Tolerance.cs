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

        /// <summary>
        /// The parts of a frame of words at a point, as the frame made there draws them, for a preview: by the current
        /// dimension variables, DIMSCALE as a leader takes it.
        /// </summary>
        private static IReadOnlyList<DxfEntity> FrameParts(
            CadCommandContext c,
            string text,
            GeoPoint3 at,
            GeoVector3 along
        )
        {
            DxfTolerance look = Current(c, new DxfTolerance());
            DxfDimStyleTableRecord drawn = c.Database.GetDimstyleData();
            drawn.Keep(DxfDimVariable.Named("Dimscale"), LeaderScale(c));
            return CadToleranceFrame
                .Of(text, at, along, new GeoVector3(0.0, 0.0, 1.0), drawn, look, c.Document.LayoutOf)
                .Parts;
        }

        /// <summary>
        /// Adds a frame of words at a point along a direction, in the current layer and dimension style (else Standard), each
        /// current dimension variable other than the style's written as an override, as dimensions and leaders write them: a
        /// frame made in ISO-25 with DIMTXT 5 typed holds <c>1070 140, 1040 5</c> and has rows 10 high.
        /// </summary>
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
            DxfDimStyleTableRecord style = NamedDimStyle(c);

            if (style != null)
            {
                frame.DimensionStyleName = style.Name;
            }

            OverrideFrame(frame, style, c.Database, LeaderScale(c));
            return frame;
        }

        /// <summary>
        /// Writes every current dimension variable a new frame is drawn by where it is not its style's, as AutoCAD 2021 writes
        /// those of a dimension or a leader, in the order of their groups: DIMSCALE (40) as the scale it is drawn by, DIMTXT
        /// (140), DIMCLRD (176), DIMCLRT (178) and each other. A variable the drawing's version cannot hold is left out.
        /// </summary>
        private static void OverrideFrame(
            DxfTolerance frame,
            DxfDimStyleTableRecord style,
            DxfDatabase db,
            double scale
        )
        {
            DxfDimStyleTableRecord current = db.GetDimstyleData();

            foreach (DxfDimVariable variable in VariablesByGroup)
            {
                object wanted = variable.Code == 40 ? scale : current.Get(variable);
                object own =
                    style != null ? style.Get(variable)
                    : variable.IsReference ? null
                    : variable.Read(null);

                if (wanted == null && variable.Kind == DxfDimKind.TextStyle || variable.SameValue(wanted, own))
                {
                    continue;
                }

                // One variable at a time, so that each is written last, in the order of the groups.
                DxfDimStyleTableRecord one = frame.GetDimstyleData();
                one.Keep(variable, wanted);

                try
                {
                    frame.SetDimstyleData(one);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    GeometryHelperLog.Debug(
                        "TOLERANCE: the override of " + variable.Name.ToUpperInvariant() + " is left out: " + e.Message
                    );
                }
            }
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
        /// A frame of the drawing laid out as the canvas lays it out: its <see cref="CadToleranceFrame.Width"/>, that of its
        /// widest row, is what a leader on its left sets it back by, as AutoCAD 2021 sets back a frame copied to a leader drawn
        /// to the left (<c>{\Fgdt;j}%%v0.1</c> at DIMTXT 0.18 in Arial, 0.92296; probe GHTQ2, a frame whose second row is the
        /// wider, that row's 1.27105).
        /// </summary>
        private static CadToleranceFrame FrameLaidOut(CadDocument document, DxfTolerance frame) =>
            CadToleranceFrame.Of(frame, document.LayoutOf);
    }
}
