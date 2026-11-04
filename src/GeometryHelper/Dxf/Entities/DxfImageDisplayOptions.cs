using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How an IMAGE or a WIPEOUT is shown, as AutoCAD's <c>ImageDisplayOptions</c> flags it: group 70 of its record. AutoCAD
    /// 2021 writes a new image with 7, shown, shown when not square to the screen, and clipped by its boundary.
    /// </summary>
    [Flags]
    public enum DxfImageDisplayOptions
    {
        /// <summary>None of the flags, 0: the image is not shown.</summary>
        None = 0,

        /// <summary>The image is shown, 1.</summary>
        Show = 1,

        /// <summary>The image is shown when it is not square to the screen, 2.</summary>
        ShowUnaligned = 2,

        /// <summary>The image is clipped by its boundary, 4: <see cref="DxfRasterImage.IsClipped"/>.</summary>
        Clip = 4,

        /// <summary>The image's transparent pixels are shown as such, 8: <see cref="DxfRasterImage.ImageTransparency"/>.</summary>
        Transparent = 8,
    }
}
