using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What was left of the acad.pgp gap list in round 3, item R3.2, that the drawing alone can do: IMAGEADJUST (and
    /// -IMAGEADJUST), which sets the brightness, contrast and fade of images, ATTEXT (and -ATTEXT, in
    /// CadCommands.Attext.cs), and CTABLESTYLE, the table style new tables take. OPEN, DRAWINGRECOVERY and the QV* views
    /// are the window's, in MainViewModel.PgpR32.cs.
    /// </summary>
    internal static partial class CadCommands
    {
        // The options IMAGEADJUST offers, in the order AutoCAD 2021 writes them.
        private static readonly string[] ImageAdjustChoices = { "Contrast", "Fade", "Brightness" };

        private static void AddPgpR32To(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("IMAGEADJUST", ImageAdjust));
            registry.Add(new CadCommand("-IMAGEADJUST", ImageAdjust));
            registry.Add(new CadCommand("ATTEXT", AttributeExtract));
            registry.Add(new CadCommand("-ATTEXT", AttributeExtract));
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CTABLESTYLE",
                    true,
                    s => CadTableStyleEditor.Current(s.Document.Database),
                    CurrentTableStyle
                )
            );
        }

        /// <summary>
        /// CTABLESTYLE: a table style of the drawing made current, letters of either case alike, as Set Current makes it, one
        /// undo step; a name the drawing has no style of is refused as AutoCAD 2021 refuses it: "Cannot set CTABLESTYLE to
        /// that value."
        /// </summary>
        private static string CurrentTableStyle(CadCommandContext c, string typed)
        {
            if (CadTableStyleEditor.Find(c.Database, typed) == null)
            {
                return "Cannot set CTABLESTYLE to that value.";
            }

            c.OpenUndoStep();
            CadEditResult result = CadTableStyleEditor.SetCurrent(c.Document, typed);
            return result.Succeeded ? null : result.Message;
        }

        /// <summary>
        /// IMAGEADJUST and -IMAGEADJUST (no dialog is built, so both ask on the command line, as AutoCAD 2021's -IMAGEADJUST
        /// does): "Select image(s):", then "Enter image option [Contrast/Fade/Brightness] &lt;Brightness&gt;:" and the value of
        /// the option, "Enter brightness value (0-100) &lt;50&gt;:", a whole number from 0 to 100, asked again with "Value
        /// must be an integer between 0 and 100." for any other; Enter takes the value offered, which is the image's own where
        /// one is selected and the factory's (50 for brightness and contrast, 0 for fade) where several are. Objects that are no
        /// image are not counted; an image on a locked layer is left as it is, "The image is on a locked layer." The change is
        /// one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ImageAdjust(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select image(s)");
            var handles = new List<string>();

            foreach (string handle in c.Input.Handles ?? Array.Empty<string>())
            {
                if (c.Document.FindEntity(handle) is DxfRasterImage image && !(image is DxfWipeout))
                {
                    handles.Add(handle);
                }
            }

            if (handles.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter image option", ImageAdjustChoices, "Brightness");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            int factory = option == "Fade" ? 0 : 50;
            int offered =
                handles.Count == 1 ? ImageLevel((DxfRasterImage)c.Document.FindEntity(handles[0]), option) : factory;
            int value = offered;

            while (true)
            {
                yield return CadPrompt.ForString(
                    "Enter " + option.ToLowerInvariant() + " value (0-100)",
                    offered.ToString(CultureInfo.InvariantCulture)
                );

                if (c.Input.IsNone)
                {
                    break;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                if (
                    int.TryParse(c.Input.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                    && value >= 0
                    && value <= 100
                )
                {
                    break;
                }

                c.Write("Value must be an integer between 0 and 100.");
            }

            var changed = new List<string>();
            var planned = new List<CadEditRecord>();
            int locked = 0;

            foreach (string handle in handles)
            {
                var image = (DxfRasterImage)c.Document.FindEntity(handle);

                if (CadLayerEditor.IsLocked(c.Database, image.Layer))
                {
                    locked++;
                    continue;
                }

                int was = ImageLevel(image, option);

                if (was != value)
                {
                    changed.Add(handle);
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            handle,
                            option,
                            was.ToString(CultureInfo.InvariantCulture),
                            value.ToString(CultureInfo.InvariantCulture)
                        )
                    );
                }
            }

            if (locked > 0)
            {
                c.Write("The image is on a locked layer.");
            }

            if (changed.Count == 0)
            {
                yield break;
            }

            c.Apply(planned, s =>
            {
                foreach (string handle in changed)
                {
                    SetImageLevel((DxfRasterImage)s.Document.FindEntity(handle), option, (byte)value);
                }
            });
        }

        private static int ImageLevel(DxfRasterImage image, string option) =>
            option == "Contrast" ? image.Contrast
            : option == "Fade" ? image.Fade
            : image.Brightness;

        private static void SetImageLevel(DxfRasterImage image, string option, byte value)
        {
            if (option == "Contrast")
            {
                image.Contrast = value;
            }
            else if (option == "Fade")
            {
                image.Fade = value;
            }
            else
            {
                image.Brightness = value;
            }
        }
    }
}
