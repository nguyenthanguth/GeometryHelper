using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
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
        /// -STYLE: "Enter name of text style or [?]", then the font ("Specify font name or font filename (for SHX)", as AutoCAD 2021's
        /// console words it for TrueType fonts too), height, width factor (shown as 1.0000), obliquing angle, backwards, upside down
        /// and, for an SHX font, "Vertical?"; an existing style with a TrueType font is said so, "Existing style. Full font name =
        /// Arial". The style made or changed is current. The whole run is one undo step. A new style starts from the defaults (txt, 0
        /// high, 1 wide), not from the current style; the height prompt offers Annotative, which asks "Create annotative text style" and
        /// for Yes "Match text orientation to layout?" and then the height again.
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
            string typeface = exists ? c.Database.TextStyleTable[name].Font.TypeFace : string.Empty;
            c.Write(
                exists
                    ? (typeface.Length > 0 ? "Existing style. Full font name = " + typeface : "Existing style.")
                    : "New style."
            );
            // A new style starts from the defaults, txt 0 high and 1 wide, and not from the current style, as AutoCAD's does.
            CadTextStyleSettings settings = exists
                ? CadTextStyleSettings.Of(c.Database.TextStyleTable[name])
                : new CadTextStyleSettings();
            string[] yesNo = { "Yes", "No" };
            string font = settings.Font.TypeFace.Length > 0 ? settings.Font.TypeFace : settings.FileName;
            yield return CadPrompt.ForString("Specify font name or font filename (for SHX)", font);

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
            {
                Font(c, settings, c.Input.Text.Trim());
            }

            // Annotative asks whether the style is, and whether its text matches the orientation of the layout (not kept: the library
            // holds no such flag), and the height is asked again.
            while (true)
            {
                yield return CadPrompt.ForDistance(
                    "Specify height of text",
                    null,
                    new[] { "Annotative" },
                    Length(settings.Height)
                );

                if (!c.Input.Is("Annotative"))
                {
                    break;
                }

                yield return CadPrompt.ForKeyword("Create annotative text style", yesNo, "Yes");
                settings.Annotative = !c.Input.Is("No");

                if (settings.Annotative)
                {
                    yield return CadPrompt.ForKeyword("Match text orientation to layout?", yesNo, "No");
                }
            }

            if (c.Input.Kind == CadInputKind.Number)
            {
                if (c.Input.Number < 0.0)
                {
                    c.Write("Value must be positive or zero.");
                    yield break;
                }

                settings.Height = c.Input.Number;
            }

            yield return CadPrompt.ForDistance(
                "Specify width factor",
                null,
                null,
                settings.WidthFactor.ToString("0.0000", CultureInfo.InvariantCulture)
            );

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

            yield return CadPrompt.ForKeyword("Display text backwards?", yesNo, settings.Backwards ? "Yes" : "No");
            settings.Backwards = c.Input.Is("Yes") || (settings.Backwards && !c.Input.Is("No"));
            yield return CadPrompt.ForKeyword("Display text upside-down?", yesNo, settings.UpsideDown ? "Yes" : "No");
            settings.UpsideDown = c.Input.Is("Yes") || (settings.UpsideDown && !c.Input.Is("No"));

            if (settings.Font.TypeFace.Length == 0)
            {
                yield return CadPrompt.ForKeyword("Vertical?", yesNo, settings.Vertical ? "Yes" : "No");
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

        // The SHX files AutoCAD 2021 installs, which a font typed with no extension names before any TrueType family does (the
        // TrueType files of the same names, romans__.ttf and txt____.ttf, are another thing): named here, no file of them is read.
        private static readonly string[] AutoCadShxFonts =
        {
            "txt",
            "monotxt",
            "simplex",
            "complex",
            "italic",
            "italicc",
            "italict",
            "romans",
            "romanc",
            "romand",
            "romant",
            "scripts",
            "scriptc",
            "greeks",
            "greekc",
            "gothice",
            "gothicg",
            "gothici",
            "symap",
            "symath",
            "symeteo",
            "symusic",
            "syastro",
            "iso",
            "isocp",
            "isocp2",
            "isocp3",
            "isoct",
            "isoct2",
            "isoct3",
            "amgdt",
            "whgtxt",
            "whgdtxt",
            "whtgtxt",
            "whtmtxt",
            "ltypeshp",
            "bigfont",
            "extfont",
            "extfont2",
        };

        /// <summary>
        /// Takes a font typed, as AutoCAD's -STYLE does: an SHX where it is named .shx, is one of AutoCAD's own SHX files (romans,
        /// txt), is the presenter's own (ghsimplex, written ghsimplex.shx) or is found by the fonts of the drawing; a TrueType
        /// family where one of that exact name is installed (Arial) or the file is named .ttf, .ttc or .otf; else an SHX file by
        /// the name typed (nosuchfont.shx), which the presenter draws in its substitute.
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
                        || Array.Exists(AutoCadShxFonts, n =>
                            string.Equals(n, typed, StringComparison.OrdinalIgnoreCase)
                        )
                        || c.Document.Fonts.Locate(typed + ".shx", c.Document.FontFolder) != null
                        || !CadTrueTypeFonts.Families.Any(f =>
                            string.Equals(f, typed, StringComparison.OrdinalIgnoreCase)
                        )
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
