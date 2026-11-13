using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>Multiline styles made in code, as AutoCAD's <c>MlineStyle</c> is added to the dictionary of multiline styles.</summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Adds a style of multilines to the dictionary of multiline styles (<see cref="MLStyleDictionary"/>) under a name, as
        /// AutoCAD adds one made by <c>new MlineStyle()</c> with its elements set: an MLINESTYLE written as AutoCAD writes one,
        /// <c>100 AcDbMlineStyle</c>, its name in capitals (2: AutoCAD keeps <c>ghLow</c> as <c>GHLOW</c>), flags (70), description (3), fill (62), angles (51, 52 in degrees), count of
        /// elements (71) and each element's offset (49), colour (62) and linetype (6), the elements from the greatest offset to
        /// the least. A drawing with no dictionary of multiline styles is given one, ACAD_MLINESTYLE of its named object
        /// dictionary.
        /// </summary>
        /// <remarks>
        /// A style of three elements at 0.5, 0 and -0.5 in colours 1, 2 and 5, the first two by layer and the last by block,
        /// the start squared by a line and the end rounded by an arc, filled in colour 3 and with a line across each joint, has
        /// the flags 1 + 2 + 16 + 1024, 1043.
        /// </remarks>
        /// <param name="name">The name, letters of either case the same, with none of the characters AutoCAD refuses in a name.</param>
        /// <param name="settings">What the style is made of.</param>
        /// <returns>The style, in the dictionary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is not one AutoCAD takes, the dictionary has a style of it
        /// already, the settings are not ones AutoCAD takes, or an element is drawn with a linetype the drawing has not.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no named object dictionary, as one of R12.</exception>
        public DxfMlineStyle AddMlineStyle(string name, DxfMlineStyleSettings settings)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DxfSymbolTable.CheckName(name, nameof(name));
            settings.Check();
            DxfDBDictionary root = NamedObjectsDictionary
                ?? throw new InvalidOperationException(
                    "A DXF "
                        + (Version ?? "with no version")
                        + " has no named object dictionary to hold multiline styles: R13 (AC1012) brought it."
                );
            DxfDBDictionary styles = MLStyleDictionary;

            if (styles != null && styles.Contains(name))
            {
                throw new ArgumentException(
                    "The drawing has a multiline style named " + name + " already, letters of either case the same.",
                    nameof(name)
                );
            }

            CheckElementLinetypes(settings, name);

            if (styles == null)
            {
                root.SetAt("ACAD_MLINESTYLE", new DxfDBDictionary());
                styles = MLStyleDictionary;
            }

            List<KeyValuePair<int, string>> groups = settings.Groups();
            styles.Add(name, "MLINESTYLE", b =>
            {
                b.Group(100, "AcDbMlineStyle");
                b.Group(2, name.ToUpperInvariant());

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });

            GeometryHelperLog.Info("DxfDatabase: the multiline style " + name + " was added.");
            return (DxfMlineStyle)styles.GetAt(name);
        }

        /// <summary>
        /// Renames a style of multilines, as the Rename of AutoCAD's Multiline Style dialog does: its entry in the dictionary of
        /// multiline styles takes the name as given and its record (2) the name in capitals, and every multiline drawn by it, in
        /// any block, names it so (its handle, 340, is kept); the current style (<see cref="CmlstyleID"/>) stays current. A style
        /// WALLS renamed to Partitions is written PARTITIONS.
        /// </summary>
        /// <param name="oldName">The name of the style, letters of either case the same.</param>
        /// <param name="newName">The new name, one AutoCAD takes in a name.</param>
        /// <exception cref="ArgumentNullException">Thrown when either name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no style of the old name, the style is STANDARD,
        /// which AutoCAD does not rename, or the new name is not one AutoCAD takes or is another style's.</exception>
        public void RenameMlineStyle(string oldName, string newName)
        {
            if (oldName == null)
            {
                throw new ArgumentNullException(nameof(oldName));
            }

            if (newName == null)
            {
                throw new ArgumentNullException(nameof(newName));
            }

            DxfSymbolTable.CheckName(newName, nameof(newName));
            DxfDBDictionary styles = MLStyleDictionary;

            if (styles == null || !styles.Contains(oldName))
            {
                throw new ArgumentException(
                    "The drawing has no multiline style named " + oldName + ".",
                    nameof(oldName)
                );
            }

            if (IsOneOf(oldName, "Standard"))
            {
                throw new ArgumentException(
                    "The multiline style STANDARD is not renamed, as AutoCAD's MLSTYLE keeps it.",
                    nameof(oldName)
                );
            }

            if (styles.Contains(newName) && !string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The drawing has a multiline style named " + newName + " already, letters of either case the same.",
                    nameof(newName)
                );
            }

            var style = (DxfMlineStyle)styles.GetAt(oldName);
            string was = style.Name;
            DxfDBObject current = CmlstyleID;
            bool isCurrent =
                current != null && string.Equals(current.Handle, style.Handle, StringComparison.OrdinalIgnoreCase);
            styles.SetName(styles.NameAt(style), newName);
            style.Rename(newName.ToUpperInvariant());

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfUnknownEntity mline && mline.DxfName == "MLINE")
                    {
                        DxfMline record = DxfMline.Read(mline.MlineGroups());

                        if (
                            string.Equals(record.StyleHandle, style.Handle, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(record.StyleName, was, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            mline.SetMlineStyleName(newName.ToUpperInvariant());
                        }
                    }
                }
            }

            if (isCurrent)
            {
                CmlstyleID = style;
            }

            GeometryHelperLog.Info("DxfDatabase: the multiline style " + was + " was renamed " + newName + ".");
        }

        /// <summary>
        /// Saves a style of multilines to a file of them, as the Save of AutoCAD's Multiline Style dialog writes acad.mln: the
        /// style opened by <c>MLSTYLE</c>, then its name (2) and its groups (70, 3, 62, 51, 52, 71, and each element's 49, 62 and
        /// 6), a code on one line and its value on the next after a space, the angles and offsets with fourteen decimals, and
        /// a group 0 after it; the styles the file has already are kept, one of the same name written over. A file that is not
        /// there is made. <see cref="LoadMlineStyleFile"/> reads it back.
        /// </summary>
        /// <param name="mlineStyleName">The name of the style, letters of either case the same.</param>
        /// <param name="fileName">The path of the file of multiline styles.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no style of the name.</exception>
        /// <exception cref="FormatException">Thrown when a file there already does not give a group code where it should.</exception>
        /// <exception cref="System.IO.IOException">Thrown when the file cannot be read or written.</exception>
        public void SaveMlineStyleFile(string mlineStyleName, string fileName)
        {
            if (mlineStyleName == null)
            {
                throw new ArgumentNullException(nameof(mlineStyleName));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            DxfDBDictionary styles = MLStyleDictionary;

            if (styles == null || !styles.Contains(mlineStyleName))
            {
                throw new ArgumentException(
                    "The drawing has no multiline style named " + mlineStyleName + ".",
                    nameof(mlineStyleName)
                );
            }

            var style = (DxfMlineStyle)styles.GetAt(mlineStyleName);
            var groups = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(2, " " + style.Name) };

            foreach (KeyValuePair<int, string> group in style.GetSettings().Groups())
            {
                groups.Add(new KeyValuePair<int, string>(group.Key, " " + MlnValue(group.Key, group.Value)));
            }

            var written = new List<List<KeyValuePair<int, string>>>();
            bool replaced = false;

            if (System.IO.File.Exists(fileName))
            {
                foreach (
                    List<DxfTypedValue> each in DxfSupportFiles.ReadMlineStyles(DxfSupportFiles.ReadText(fileName))
                )
                {
                    DxfTypedValue name = each.Find(g => g.TypeCode == 2);

                    if (
                        name.TypeCode == 2
                        && name.Value != null
                        && string.Equals(name.Value.Trim(), style.Name, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        written.Add(groups);
                        replaced = true;
                        continue;
                    }

                    // Each value is written after a space, as it was read.
                    written.Add(each.ConvertAll(g => new KeyValuePair<int, string>(g.TypeCode, " " + g.Value)));
                }
            }

            if (!replaced)
            {
                written.Add(groups);
            }

            var text = new System.Text.StringBuilder();

            foreach (List<KeyValuePair<int, string>> each in written)
            {
                text.Append("MLSTYLE\r\n");

                foreach (KeyValuePair<int, string> group in each)
                {
                    text.Append(group.Key.ToString(CultureInfo.InvariantCulture))
                        .Append("\r\n")
                        .Append(group.Value)
                        .Append("\r\n");
                }

                text.Append("0\r\n");
            }

            System.IO.File.WriteAllText(fileName, text.ToString(), new System.Text.UTF8Encoding(false));
            GeometryHelperLog.Info(
                "DxfDatabase: the multiline style "
                    + style.Name
                    + " was saved to "
                    + System.IO.Path.GetFileName(fileName)
                    + "."
            );
        }

        /// <summary>A value as acad.mln writes it: an angle or an offset with fourteen decimals, the rest as the record has it.</summary>
        private static string MlnValue(int code, string value) =>
            (code == 51 || code == 52 || code == 49)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? number.ToString("F14", CultureInfo.InvariantCulture)
                : value;

        /// <summary>Refuses an element drawn with a linetype the drawing has not.</summary>
        internal void CheckElementLinetypes(DxfMlineStyleSettings settings, string styleName)
        {
            foreach (DxfMlineStyleElement element in settings.Elements)
            {
                if (!IsOneOf(element.Linetype, "BYLAYER", "BYBLOCK") && !LinetypeTable.Has(element.Linetype))
                {
                    throw new ArgumentException(
                        "The multiline style "
                            + styleName
                            + " draws an element with the linetype "
                            + element.Linetype
                            + ", which the drawing has not: load it first.",
                        nameof(settings)
                    );
                }
            }
        }
    }
}
