using System;
using System.Collections.Generic;

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
