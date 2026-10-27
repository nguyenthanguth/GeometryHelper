using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a data link is kept, as AutoCAD's <c>DataLinkOption</c> flags it (group 90 of a DATALINK).
    /// </summary>
    [Flags]
    public enum DxfDataLinkOption
    {
        /// <summary>Nothing of the two, 0.</summary>
        None = 0,

        /// <summary>The link has no name of its own to be shown by, 1.</summary>
        Anonymous = 1,

        /// <summary>The data last read from the source is kept with the drawing, 2.</summary>
        PersistCache = 2,
    }
}
