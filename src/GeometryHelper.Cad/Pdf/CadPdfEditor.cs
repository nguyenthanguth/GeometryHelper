using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What the PDF Underlay tab does to the underlays selected: a value changed in it is one undo step named after the command that
    /// changes the same thing (PDFADJUST, PDFCLIP, or Show underlay), written as that command writes it, so that the tab and the
    /// commands leave the same underlay. An underlay on a locked layer is left as it is.
    /// </summary>
    internal static class CadPdfEditor
    {
        /// <summary>Changes underlays with an action, one undo step: each found again in the drawing as the edit begins, those on a locked layer passed over.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the underlays.</param>
        /// <param name="step">The name of the undo step.</param>
        /// <param name="what">What changes, for the record of the edit.</param>
        /// <param name="value">The new value, as the record writes it.</param>
        /// <param name="change">Changes an underlay it is given.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult Edit(
            CadDocument document,
            IReadOnlyList<string> handles,
            string step,
            string what,
            string value,
            Action<DxfPdfUnderlay> change
        )
        {
            List<string> open = handles
                .Where(h =>
                    document.FindEntity(h) is DxfPdfUnderlay underlay
                    && !CadLayerEditor.IsLocked(document.Database, underlay.Layer)
                )
                .ToList();
            var planned = open.Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, what, null, value))
                .ToList();
            return document.ApplyEdit(step, planned, s =>
            {
                foreach (string handle in open)
                {
                    if (s.Document.FindEntity(handle) is DxfPdfUnderlay underlay)
                    {
                        change(underlay);
                    }
                }
            });
        }

        /// <summary>Sets the fade or the contrast of underlays, 0 to 100, as PDFADJUST does.</summary>
        internal static CadEditResult SetLevel(
            CadDocument document,
            IReadOnlyList<string> handles,
            string option,
            int value
        )
        {
            int level = Math.Max(0, Math.Min(100, value));
            return Edit(
                document,
                handles,
                "PDFADJUST",
                option,
                level.ToString(CultureInfo.InvariantCulture),
                underlay =>
                {
                    if (option == "Contrast")
                    {
                        underlay.Contrast = level;
                    }
                    else
                    {
                        underlay.Fade = level;
                    }
                }
            );
        }

        /// <summary>Makes underlays monochrome or not, as PDFADJUST Monochrome does.</summary>
        internal static CadEditResult SetMonochrome(CadDocument document, IReadOnlyList<string> handles, bool on) =>
            Edit(document, handles, "PDFADJUST", "Monochrome", on ? "On" : "Off", underlay => underlay.Monochrome = on);

        /// <summary>Adjusts the colours of underlays for the background or not.</summary>
        internal static CadEditResult SetAdjustForBackground(
            CadDocument document,
            IReadOnlyList<string> handles,
            bool on
        ) =>
            Edit(document, handles, "PDFADJUST", "Adjust colors for background", on ? "On" : "Off", underlay =>
                underlay.AdjustColorForBackground = on
            );

        /// <summary>Shows or switches off underlays, their frames staying, as the Show underlay box does.</summary>
        internal static CadEditResult SetShown(CadDocument document, IReadOnlyList<string> handles, bool shown) =>
            Edit(document, handles, "Show underlay", "Show underlay", shown ? "On" : "Off", underlay =>
                underlay.IsOn = shown
            );

        /// <summary>Takes the clip boundary off underlays, as PDFCLIP Delete does: each shows its whole page again.</summary>
        internal static CadEditResult RemoveClipping(CadDocument document, IReadOnlyList<string> handles) =>
            Edit(document, handles, "PDFCLIP", "Clip boundary", "Whole page", underlay => underlay.ClearClipBoundary());
    }
}
