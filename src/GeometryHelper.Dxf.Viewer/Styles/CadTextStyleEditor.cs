using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What AutoCAD's Text Style dialog sets of a style: its font (an SHX file with its big font, or a TrueType typeface with
    /// its file), its fixed height, its width and slant, and backwards, upside down and vertical.
    /// </summary>
    /// <remarks>
    /// The style romans.shx 2.5 high, three quarters wide is <c>FileName</c> romans.shx, <see cref="Height"/> 2.5 and
    /// <see cref="WidthFactor"/> 0.75; the style of Arial bold is <c>FileName</c> arial.ttf and a <see cref="Font"/> of the
    /// typeface Arial, bold.
    /// </remarks>
    public sealed class CadTextStyleSettings
    {
        /// <summary>Gets or sets the file of the font (group 3): romans.shx, arial.ttf.</summary>
        public string FileName { get; set; } = "txt";

        /// <summary>Gets or sets the big font (group 4); empty for none.</summary>
        public string BigFontFileName { get; set; } = string.Empty;

        /// <summary>Gets or sets the TrueType font, written as the style's extended data of ACAD; of no typeface for an SHX font.</summary>
        public DxfFontDescriptor Font { get; set; }

        /// <summary>Gets or sets the fixed height of the texts (group 40): nought where each text gives its own, as TEXT then asks.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets how wide the letters are for their height (group 41): 1 as drawn.</summary>
        public double WidthFactor { get; set; } = 1.0;

        /// <summary>Gets or sets the slant of the letters in radians (group 50, written in degrees), from -85 to 85 degrees.</summary>
        public double ObliqueAngle { get; set; }

        /// <summary>Gets or sets whether the texts run backwards (flag 2 of group 71).</summary>
        public bool Backwards { get; set; }

        /// <summary>Gets or sets whether the texts stand upside down (flag 4 of group 71).</summary>
        public bool UpsideDown { get; set; }

        /// <summary>Gets or sets whether the texts run down (flag 4 of group 70), which only an SHX font of two orientations draws.</summary>
        public bool Vertical { get; set; }

        /// <summary>Reads the settings of a style.</summary>
        /// <param name="style">The style.</param>
        /// <returns>Its settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        public static CadTextStyleSettings Of(DxfTextStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            return new CadTextStyleSettings
            {
                FileName = style.FileName,
                BigFontFileName = style.BigFontFileName,
                Font = style.Font,
                Height = style.TextSize,
                WidthFactor = style.XScale,
                ObliqueAngle = style.ObliquingAngle,
                Backwards = (style.FlagBits & 2) != 0,
                UpsideDown = (style.FlagBits & 4) != 0,
                Vertical = style.IsVertical,
            };
        }

        /// <summary>Gets whether two settings write the same style.</summary>
        /// <param name="other">The other.</param>
        /// <returns>True where every value is the same, the font file and big font letters of either case the same.</returns>
        public bool SameAs(CadTextStyleSettings other)
            => other != null && string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(BigFontFileName ?? string.Empty, other.BigFontFileName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && Font == other.Font && Height.Equals(other.Height) && WidthFactor.Equals(other.WidthFactor) && ObliqueAngle.Equals(other.ObliqueAngle)
                && Backwards == other.Backwards && UpsideDown == other.UpsideDown && Vertical == other.Vertical;
    }

    /// <summary>
    /// The edits of the text style table, as AutoCAD's Text Style dialog (STYLE) and -STYLE make them: each one undo step
    /// through <see cref="CadDocument.ApplyEdit"/>, refused where AutoCAD refuses it.
    /// </summary>
    public static class CadTextStyleEditor
    {
        /// <summary>
        /// Gets how many texts are written in a style: the TEXTs, MTEXTs and attribute definitions of model space, the layouts
        /// and the blocks, as AutoCAD counts what keeps a style from being deleted.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The number of texts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static int CountUses(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            int count = 0;

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    string style = entity is DxfDBText text ? text.TextStyleName : entity is DxfMText mtext ? mtext.TextStyleName : null;
                    count += style != null && string.Equals(style, name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                }
            }

            return count;
        }

        /// <summary>Gets the name of the current text style (<c>$TEXTSTYLE</c>), as the drawing's table spells it; Standard where the header names none it has.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The name.</returns>
        public static string Current(DxfDatabase database)
        {
            string name;

            try
            {
                name = database.Textstyle;
            }
            catch (Exception)
            {
                name = null;
            }

            return !string.IsNullOrEmpty(name) && database.TextStyleTable.Has(name) ? database.TextStyleTable[name].Name : "Standard";
        }

        /// <summary>Makes a style current (<c>$TEXTSTYLE</c>), as Set Current and the style box do.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetCurrent(CadDocument document, string name)
        {
            DxfTextStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            if (style.IsShapeFile)
            {
                return CadEditResult.Refuse("The style " + style.Name + " is of shapes, and cannot be current.");
            }

            string set = style.Name;
            string old = Current(document.Database);
            return document.ApplyEdit("STYLE", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$TEXTSTYLE", old, set) }, s => s.Database.Textstyle = set);
        }

        /// <summary>
        /// Adds a style, as New of the Text Style dialog: a copy of the settings of another (the style selected), under a name
        /// no style of the drawing has, letters of either case the same.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The new name, as style1.</param>
        /// <param name="from">The style whose settings it takes; Standard where null.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Add(CadDocument document, string name, string from)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return CadEditResult.Refuse("A text style needs a name.");
            }

            if (document.Database.TextStyleTable.Has(trimmed))
            {
                return CadEditResult.Refuse("Text style \"" + trimmed + "\" already exists.");
            }

            DxfTextStyleTableRecord source = Find(document, from ?? "Standard", out _);
            CadTextStyleSettings settings = source == null ? new CadTextStyleSettings() : CadTextStyleSettings.Of(source);
            return document.ApplyEdit("STYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New text style", null, trimmed) }, s =>
            {
                s.Database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = trimmed });
                Write(s.Database.TextStyleTable[trimmed], settings);
            });
        }

        /// <summary>
        /// Writes the settings into a style, as Apply of the Text Style dialog: every text written in it drawn again in them.
        /// Nothing is written, and nothing refused, where they are the style's already.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>What came of it; refused for a width not above nought, a height below nought or a slant past 85 degrees.</returns>
        public static CadEditResult Apply(CadDocument document, string name, CadTextStyleSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DxfTextStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            if (!(settings.WidthFactor > 0.0) || double.IsInfinity(settings.WidthFactor))
            {
                return CadEditResult.Refuse("Width factor must be a number above nought.");
            }

            if (!(settings.Height >= 0.0) || double.IsInfinity(settings.Height))
            {
                return CadEditResult.Refuse("Height must be a number of nought or more.");
            }

            if (!(Math.Abs(settings.ObliqueAngle) <= 85.0 * Math.PI / 180.0 + 1E-12))
            {
                return CadEditResult.Refuse("Oblique angle must be from -85 to 85 degrees.");
            }

            if (CadTextStyleSettings.Of(style).SameAs(settings))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            string set = style.Name;
            return document.ApplyEdit("STYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Text style " + set, null, settings.FileName) }, s => Write(s.Database.TextStyleTable[set], settings));
        }

        /// <summary>
        /// Deletes a style, as Delete of the Text Style dialog: refused for Standard, the current style, a style texts are
        /// written in (saying how many) and one a dimension style or a table style points at.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            DxfTextStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string set = style.Name;

            if (string.Equals(set, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("The Standard style cannot be deleted.");
            }

            if (string.Equals(set, Current(document.Database), StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("The current style cannot be deleted: make another current first.");
            }

            int uses = CountUses(document.Database, set);

            if (uses > 0)
            {
                return CadEditResult.Refuse("Style \"" + set + "\" is in use by " + uses + (uses == 1 ? " text" : " texts") + ", and cannot be deleted.");
            }

            return document.ApplyEdit("STYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete text style", set, null) }, s => s.Database.TextStyleTable[set].Erase());
        }

        /// <summary>Renames a style with every text written in it, as the Text Style dialog's Rename; Standard keeps its name, as in AutoCAD.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Rename(CadDocument document, string name, string newName)
        {
            DxfTextStyleTableRecord style = Find(document, name, out CadEditResult refused);

            if (style == null)
            {
                return refused;
            }

            string set = style.Name;
            string trimmed = (newName ?? string.Empty).Trim();

            if (string.Equals(set, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return CadEditResult.Refuse("The Standard style cannot be renamed.");
            }

            if (trimmed.Length == 0)
            {
                return CadEditResult.Refuse("A text style needs a name.");
            }

            if (!string.Equals(trimmed, set, StringComparison.OrdinalIgnoreCase) && document.Database.TextStyleTable.Has(trimmed))
            {
                return CadEditResult.Refuse("Text style \"" + trimmed + "\" already exists.");
            }

            return document.ApplyEdit("STYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename text style", set, trimmed) }, s => s.Database.TextStyleTable[set].Name = trimmed);
        }

        /// <summary>Gets the styles of a drawing that are of letters, not shapes, in the order of the table.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The styles.</returns>
        public static IReadOnlyList<DxfTextStyleTableRecord> Styles(DxfDatabase database)
        {
            var styles = new List<DxfTextStyleTableRecord>();

            foreach (DxfTextStyleTableRecord style in database.TextStyleTable)
            {
                if (!style.IsShapeFile && !string.IsNullOrEmpty(style.Name))
                {
                    styles.Add(style);
                }
            }

            return styles;
        }

        /// <summary>Writes settings into a style of a drawing, the font's extended data last, as AutoCAD writes it after the record.</summary>
        private static void Write(DxfTextStyleTableRecord style, CadTextStyleSettings settings)
        {
            style.FileName = settings.FileName ?? string.Empty;
            style.BigFontFileName = settings.BigFontFileName ?? string.Empty;
            style.TextSize = settings.Height;
            style.XScale = settings.WidthFactor;
            style.ObliquingAngle = settings.ObliqueAngle;
            style.FlagBits = (byte)((settings.Backwards ? 2 : 0) | (settings.UpsideDown ? 4 : 0));
            style.IsVertical = settings.Vertical;
            style.Font = settings.Font;
        }

        private static DxfTextStyleTableRecord Find(CadDocument document, string name, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            refused = null;

            if (string.IsNullOrEmpty(name) || !document.Database.TextStyleTable.Has(name))
            {
                refused = CadEditResult.Refuse("Text style \"" + name + "\" not found.");
                return null;
            }

            return document.Database.TextStyleTable[name];
        }
    }
}
