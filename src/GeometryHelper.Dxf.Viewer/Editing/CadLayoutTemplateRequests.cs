using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What LAYOUT Template gives its Insert Layout(s) dialog once the template file is chosen: the file and the names of its
    /// layouts in the order of its tabs. The dialog fills <see cref="Picked"/>; on OK each layout picked comes in after the
    /// drawing's last tab, as the typed Template brings it in, one undo step. A template of A1 Sheet, Details and Cover lists
    /// the three, the first picked to start with.
    /// </summary>
    public sealed class CadInsertLayoutRequest
    {
        internal CadInsertLayoutRequest(string fileName, IReadOnlyList<string> layoutNames)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            LayoutNames = layoutNames ?? throw new ArgumentNullException(nameof(layoutNames));
        }

        /// <summary>Gets the template file the layouts are read from.</summary>
        public string FileName { get; }

        /// <summary>Gets the names of the template's layouts, in the order of its tabs.</summary>
        public IReadOnlyList<string> LayoutNames { get; }

        /// <summary>Gets the names of the layouts the dialog picked.</summary>
        public IList<string> Picked { get; } = new List<string>();

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }

    /// <summary>
    /// What LAYOUT SAveas gives its Template Options dialog before a .dwt is written: the file, the description (empty to
    /// start) and the measurement, metric where the drawing's MEASUREMENT is 1. On OK the template is written with the
    /// description in its DWG's template section and MEASUREMENT set as chosen; Cancel writes nothing.
    /// </summary>
    public sealed class CadTemplateOptionsRequest
    {
        internal CadTemplateOptionsRequest(string fileName, bool isMetric)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            IsMetric = isMetric;
        }

        /// <summary>Gets the template file about to be written.</summary>
        public string FileName { get; }

        /// <summary>Gets or sets the description written into the template; empty for none.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the template's MEASUREMENT is metric (1) rather than English (0).</summary>
        public bool IsMetric { get; set; }

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }
}
