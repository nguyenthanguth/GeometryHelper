using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The style commands: STYLE (the Text Style dialog, else -STYLE's prompts), -STYLE typed in AutoCAD's words, and DIMSTYLE
    /// (the Dimension Style Manager).
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> StyleDialog(CadCommandContext c)
        {
            if (c.Session.Request("STYLE"))
            {
                yield break;
            }

            foreach (CadPrompt prompt in StyleTyped(c))
            {
                yield return prompt;
            }
        }

        private static IEnumerable<CadPrompt> DimStyleDialog(CadCommandContext c)
        {
            if (!c.Session.Request("DIMSTYLE"))
            {
                c.Write(
                    "Current dimension style:  "
                        + (CadDimStyleEditor.Current(c.Database) ?? "none")
                        + ". The Dimension Style Manager is shown by the window."
                );
            }

            yield break;
        }

        /// <summary>
        /// -STYLE: "Enter name of text style or [?]", then the font, height, width factor, obliquing angle, backwards, upside
        /// down and, for an SHX font, vertically; the style made or changed is current. The whole run is one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> StyleTyped(CadCommandContext c)
        {
            string current = CadTextStyleEditor.Current(c.Database);
            yield return CadPrompt.ForString("Enter name of text style or [?]", current);
            string name =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : current;

            if (name == "?")
            {
                yield return CadPrompt.ForString("Enter text style(s) to list", "*");
                ListStyles(c);
                yield break;
            }

            bool exists = c.Database.TextStyleTable.Has(name);
            c.Write(exists ? "Existing style." : "New style.");
            CadTextStyleSettings settings = exists
                ? CadTextStyleSettings.Of(c.Database.TextStyleTable[name])
                : CadTextStyleSettings.Of(c.Database.TextStyleTable[current]);
            string font = settings.Font.TypeFace.Length > 0 ? settings.Font.TypeFace : settings.FileName;
            yield return CadPrompt.ForString("Specify full font name or font filename (TTF or SHX)", font);

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
            {
                Font(c, settings, c.Input.Text.Trim());
            }

            yield return CadPrompt.ForDistance("Specify height of text", null, null, Length(settings.Height));

            if (c.Input.Kind == CadInputKind.Number)
            {
                if (c.Input.Number < 0.0)
                {
                    c.Write("Value must be positive or zero.");
                    yield break;
                }

                settings.Height = c.Input.Number;
            }

            yield return CadPrompt.ForDistance("Specify width factor", null, null, Number(settings.WidthFactor));

            if (c.Input.Kind == CadInputKind.Number)
            {
                settings.WidthFactor = c.Input.Number;
            }

            yield return CadPrompt
                .ForAngle("Specify obliquing angle", null, null, Number(settings.ObliqueAngle * 180.0 / Math.PI))
                .Plain();

            if (c.Input.Kind == CadInputKind.Number)
            {
                settings.ObliqueAngle = c.Input.Number;
            }

            string[] yesNo = { "Yes", "No" };
            yield return CadPrompt.ForKeyword("Display text backwards?", yesNo, settings.Backwards ? "Yes" : "No");
            settings.Backwards = c.Input.Is("Yes") || (settings.Backwards && !c.Input.Is("No"));
            yield return CadPrompt.ForKeyword("Display text upside-down?", yesNo, settings.UpsideDown ? "Yes" : "No");
            settings.UpsideDown = c.Input.Is("Yes") || (settings.UpsideDown && !c.Input.Is("No"));

            if (settings.Font.TypeFace.Length == 0)
            {
                yield return CadPrompt.ForKeyword("Vertically?", yesNo, settings.Vertical ? "Yes" : "No");
                settings.Vertical = c.Input.Is("Yes") || (settings.Vertical && !c.Input.Is("No"));
            }

            c.OpenUndoStep();
            CadEditResult result = exists
                ? CadEditResult.Done(Array.Empty<CadEditRecord>())
                : CadTextStyleEditor.Add(c.Document, name, current);
            result = result.Succeeded ? CadTextStyleEditor.Apply(c.Document, name, settings) : result;
            result = result.Succeeded ? CadTextStyleEditor.SetCurrent(c.Document, name) : result;
            c.Write(
                result.Succeeded
                    ? "\"" + c.Database.TextStyleTable[name].Name + "\" is now the current text style."
                    : result.Message
            );
        }

        /// <summary>
        /// Takes a font typed: an SHX by its file (romans, romans.shx) where the fonts find it, it is named .shx or it is the
        /// presenter's own (ghsimplex, written ghsimplex.shx), else a TrueType family or file.
        /// </summary>
        private static void Font(CadCommandContext c, CadTextStyleSettings settings, string typed)
        {
            string extension = Path.GetExtension(typed).ToLowerInvariant();
            bool shx =
                extension == ".shx"
                || (
                    extension.Length == 0
                    && (
                        CadFonts.IsBuiltIn(typed)
                        || c.Document.Fonts.Locate(typed + ".shx", c.Document.FontFolder) != null
                    )
                );

            if (shx)
            {
                settings.FileName = extension.Length == 0 ? typed + ".shx" : typed;
                settings.Font = default;
                return;
            }

            bool file = extension == ".ttf" || extension == ".ttc" || extension == ".otf";
            string family = file ? CadTrueTypeFonts.FamilyOf(typed) ?? Path.GetFileNameWithoutExtension(typed) : typed;
            settings.FileName = file ? typed : CadTrueTypeFonts.FileOf(typed);
            settings.BigFontFileName = string.Empty;
            settings.Font = new DxfFontDescriptor(family, false, false, 0, 34);
            settings.Vertical = false;
        }

        /// <summary>STYLE ?: each style as AutoCAD lists it, its font files, height, width factor, obliquing angle and generation.</summary>
        private static void ListStyles(CadCommandContext c)
        {
            foreach (DxfTextStyleTableRecord style in CadTextStyleEditor.Styles(c.Database))
            {
                string files =
                    style.Font.TypeFace.Length > 0
                        ? style.Font.TypeFace
                        : style.FileName
                            + (style.BigFontFileName.Length > 0 ? ", " + style.BigFontFileName : string.Empty);
                var generation = new List<string>();

                if ((style.FlagBits & 2) != 0)
                {
                    generation.Add("Backwards");
                }

                if ((style.FlagBits & 4) != 0)
                {
                    generation.Add("Upside-down");
                }

                if (style.IsVertical)
                {
                    generation.Add("Vertical");
                }

                c.Write("Style name: \"" + style.Name + "\"  Font files: " + files);
                c.Write(
                    "   Height: "
                        + style.TextSize.ToString("0.0000", CultureInfo.InvariantCulture)
                        + "  Width factor: "
                        + style.XScale.ToString("0.0000", CultureInfo.InvariantCulture)
                        + "  Obliquing angle: "
                        + Number(style.ObliquingAngle * 180.0 / Math.PI)
                );
                c.Write("   Generation: " + (generation.Count == 0 ? "Normal" : string.Join(", ", generation)));
            }

            c.Write("Current text style:  \"" + CadTextStyleEditor.Current(c.Database) + "\"");
        }
    }
}
