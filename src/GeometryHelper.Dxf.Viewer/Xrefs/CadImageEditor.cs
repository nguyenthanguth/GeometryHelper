using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the Image tab does to the images selected: a value changed in it is one undo step named after the command that
    /// changes the same thing (IMAGEADJUST, TRANSPARENCY, IMAGECLIP, or Show image), written as that command writes it, so that
    /// the tab and the commands leave the same image. An image on a locked layer is left as it is, as IMAGEADJUST leaves it.
    /// </summary>
    internal static class CadImageEditor
    {
        /// <summary>
        /// Changes images with an action, one undo step: each image found again in the drawing as the edit begins, those on a
        /// locked layer passed over.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the images.</param>
        /// <param name="step">The name of the undo step.</param>
        /// <param name="what">What changes, for the record of the edit.</param>
        /// <param name="value">The new value, as the record writes it.</param>
        /// <param name="change">Changes an image it is given.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult Edit(
            CadDocument document,
            IReadOnlyList<string> handles,
            string step,
            string what,
            string value,
            Action<DxfRasterImage> change
        )
        {
            List<string> open = handles
                .Where(h =>
                    document.FindEntity(h) is DxfRasterImage image
                    && !(image is DxfWipeout)
                    && !CadLayerEditor.IsLocked(document.Database, image.Layer)
                )
                .ToList();
            var planned = open.Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, what, null, value))
                .ToList();
            return document.ApplyEdit(step, planned, s =>
            {
                foreach (string handle in open)
                {
                    if (s.Document.FindEntity(handle) is DxfRasterImage image)
                    {
                        change(image);
                    }
                }
            });
        }

        /// <summary>Sets the brightness, contrast or fade of images, 0 to 100, as IMAGEADJUST does.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the images.</param>
        /// <param name="option">Brightness, Contrast or Fade.</param>
        /// <param name="value">The level; kept between 0 and 100.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult SetLevel(
            CadDocument document,
            IReadOnlyList<string> handles,
            string option,
            int value
        )
        {
            int level = Math.Max(0, Math.Min(100, value));
            return Edit(document, handles, "IMAGEADJUST", option, level.ToString(CultureInfo.InvariantCulture), image =>
            {
                if (option == "Contrast")
                {
                    image.Contrast = (byte)level;
                }
                else if (option == "Fade")
                {
                    image.Fade = (byte)level;
                }
                else
                {
                    image.Brightness = (byte)level;
                }
            });
        }

        /// <summary>Shows or hides the pictures of images, their frames staying (flag 1 of group 70).</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the images.</param>
        /// <param name="shown">Whether the pictures are shown.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult SetShown(CadDocument document, IReadOnlyList<string> handles, bool shown) =>
            Edit(document, handles, "Show image", "Show image", shown ? "On" : "Off", image => image.ShowImage = shown);

        /// <summary>Makes the background of bitonal images transparent or opaque, as TRANSPARENCY does.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the images.</param>
        /// <param name="on">Whether the background is transparent.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult SetTransparent(CadDocument document, IReadOnlyList<string> handles, bool on) =>
            Edit(document, handles, "TRANSPARENCY", "Transparency", on ? "On" : "Off", image =>
                image.ImageTransparency = on
            );

        /// <summary>Takes the clipping boundary off images, as IMAGECLIP Delete does: each shows its whole picture again.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the images.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult RemoveClipping(CadDocument document, IReadOnlyList<string> handles) =>
            Edit(document, handles, "IMAGECLIP", "Clip boundary", "Whole image", image =>
                image.SetClipBoundaryToWholeImage()
            );

        /// <summary>Whether an image is clipped by a boundary of its own rather than by its whole picture.</summary>
        /// <param name="image">The image.</param>
        /// <returns>True where it shows part of its picture.</returns>
        internal static bool IsClippedByBoundary(DxfRasterImage image) => CadCommands.Clipped(image);
    }
}
