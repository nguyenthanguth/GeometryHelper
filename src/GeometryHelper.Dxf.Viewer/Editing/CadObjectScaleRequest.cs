using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What OBJECTSCALE gives its Annotation Object Scale dialog: the drawing's scale list in its order, and the scales each
    /// selected annotative object is shown at. The dialog fills <see cref="ToAdd"/> and <see cref="ToDelete"/>; on OK every
    /// object is given each scale to add it is not shown at, and loses each scale to delete it is shown at but for its last,
    /// in one undo step. Three texts, at 1:1 and 1:50, at 1:50, and at 1:50 and 1:100, list 1:1, 1:50 and 1:100, or 1:50
    /// alone when only the scales common to all are listed.
    /// </summary>
    public sealed class CadObjectScaleRequest
    {
        internal CadObjectScaleRequest(
            IReadOnlyList<CadScaleListRow> drawingScales,
            IReadOnlyList<IReadOnlyList<string>> objectScales
        )
        {
            DrawingScales = drawingScales ?? throw new ArgumentNullException(nameof(drawingScales));
            ObjectScales = objectScales ?? throw new ArgumentNullException(nameof(objectScales));
        }

        /// <summary>Gets the drawing's scales, in the order of its list (SCALELISTEDIT).</summary>
        public IReadOnlyList<CadScaleListRow> DrawingScales { get; }

        /// <summary>Gets, for each selected annotative object, the names of the scales it is shown at.</summary>
        public IReadOnlyList<IReadOnlyList<string>> ObjectScales { get; }

        /// <summary>Gets the names of the scales the dialog adds to every selected object.</summary>
        public IList<string> ToAdd { get; } = new List<string>();

        /// <summary>Gets the names of the scales the dialog takes from the selected objects.</summary>
        public IList<string> ToDelete { get; } = new List<string>();

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }
}
