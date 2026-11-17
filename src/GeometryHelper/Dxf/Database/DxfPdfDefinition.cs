using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A PDFDEFINITION of a DXF file, as AutoCAD's <c>PdfDefinition</c>: the page of a PDF file the PDFUNDERLAYs of a drawing
    /// show, under its name in the dictionary of PDF underlays (<see cref="GetPdfDictionary"/>, ACAD_PDFUNDERLAYS).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library reads no PDF: the definition keeps the path as the file writes it (group 1) and the page's name (group 2),
    /// which for a PDF is the page's number as text, <c>1</c> for the first. AutoCAD 2021 writes the page 1 of
    /// <c>D:\plans\sample.pdf</c> as the groups 1 <c>D:\plans\sample.pdf</c> and 2 <c>1</c>, under the name <c>sample - Page_1</c>.
    /// </para>
    /// <para>
    /// Made in code, a definition is added to a drawing's dictionary under a name, and a <see cref="DxfPdfUnderlay"/> is then
    /// made of it: <c>DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(db);
    /// underlays.SetAt(DxfPdfDefinition.SuggestName(underlays, path, "1"), new DxfPdfDefinition(path, "1"));</c>
    /// </para>
    /// </remarks>
    public sealed class DxfPdfDefinition : DxfDBObject
    {
        // The groups of AcDbUnderlayDefinition in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 1, 2 });

        // The groups a definition made in code is written with after its marker, until it is added to a drawing.
        private readonly List<KeyValuePair<int, string>> _made;

        /// <summary>
        /// Initializes a definition made in code of a page of a PDF file, as AutoCAD's <c>new PdfDefinition()</c> given its
        /// <c>SourceFileName</c> and <c>ItemName</c>. Added to a drawing by <see cref="DxfDBDictionary.SetAt"/> of its dictionary
        /// of PDF underlays (<see cref="CreatePdfDictionary"/>).
        /// </summary>
        /// <param name="sourceFileName">The path of the PDF, as it is to be written: <c>C:\plans\sample.pdf</c>. The library does not look for the file.</param>
        /// <param name="itemName">The name of the page, its number as text for a PDF: <c>1</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path or the page's name is null.</exception>
        public DxfPdfDefinition(string sourceFileName, string itemName)
        {
            if (sourceFileName == null)
            {
                throw new ArgumentNullException(nameof(sourceFileName));
            }

            if (itemName == null)
            {
                throw new ArgumentNullException(nameof(itemName));
            }

            _made = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(1, sourceFileName),
                new KeyValuePair<int, string>(2, itemName),
            };
        }

        internal DxfPdfDefinition(DxfItem item)
            : base(item) { }

        /// <summary>
        /// Gets or sets the path of the PDF file (group 1), as AutoCAD's <c>SourceFileName</c>: as the file writes it, a path of the
        /// machine it was drawn on or one relative to the drawing.
        /// </summary>
        /// <value>Set, the path as it is to be written; the underlays that show the definition show that file once it is found.</value>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the definition is erased.</exception>
        public string SourceFileName
        {
            get => Text(1) ?? string.Empty;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, SourceFileName, StringComparison.Ordinal))
                {
                    Set(new KeyValuePair<int, string>(1, value));
                }
            }
        }

        /// <summary>
        /// Gets the name of the page (group 2), as AutoCAD's <c>ItemName</c>: the number of the page as text for a PDF, <c>1</c> for
        /// the first; empty where the record gives none.
        /// </summary>
        public string ItemName => Text(2) ?? string.Empty;

        /// <summary>
        /// Gets how many underlays of the drawing show the definition: those whose handle (340) it is, an underlay in a block as
        /// one in model space; an erased underlay is not counted.
        /// </summary>
        /// <returns>The count; nought for a definition in no file.</returns>
        public int GetEntityCount()
        {
            if (Item == null || !IsResident || IsErased)
            {
                return 0;
            }

            DxfDatabase database = Item.File.Database;
            int count = 0;

            if (database == null)
            {
                return 0;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (
                        entity is DxfPdfUnderlay underlay
                        && !entity.IsErased
                        && string.Equals(underlay.DefinitionHandle, Handle, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Erases the definition, as AutoCAD 2021's <c>Erase</c> of a <c>PdfDefinition</c>: its entry of the dictionary of PDF
        /// underlays taken out, and with it every underlay of the drawing that shows it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the definition is in no file, or is erased.</exception>
        public override void Erase()
        {
            DxfFile file = InFile("be erased");
            DxfDatabase database = file.Database;

            if (database != null)
            {
                var showing = new List<DxfEntity>();

                foreach (DxfBlockTableRecord block in database.BlockTable)
                {
                    foreach (DxfEntity entity in block)
                    {
                        if (
                            entity is DxfPdfUnderlay underlay
                            && string.Equals(underlay.DefinitionHandle, Handle, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            showing.Add(entity);
                        }
                    }
                }

                foreach (DxfEntity entity in showing)
                {
                    entity.Erase();
                }
            }

            DxfDBDictionary.EraseFromOwner(this, file);
        }

        /// <summary>
        /// Gets the dictionary of the PDF underlays of a drawing: ACAD_PDFUNDERLAYS of the named object dictionary, each
        /// <see cref="DxfPdfDefinition"/> under its name; null where the drawing has none.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The dictionary, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static DxfDBDictionary GetPdfDictionary(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfDBDictionary root = database.NamedObjectsDictionary;
            return root != null && root.Contains(DictionaryName) ? root.GetAt(DictionaryName) as DxfDBDictionary : null;
        }

        /// <summary>
        /// Gets the dictionary of the PDF underlays of a drawing, made where it has none: ACAD_PDFUNDERLAYS of the named object
        /// dictionary, owned by it, of entries kept (281 of 1), as AutoCAD 2021 writes it.
        /// </summary>
        /// <param name="database">The drawing, of R2010 (AC1024) or later.</param>
        /// <returns>The dictionary.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing is older than R2010, which has no PDF underlays,
        /// or has no named object dictionary.</exception>
        public static DxfDBDictionary CreatePdfDictionary(DxfDatabase database)
        {
            DxfDBDictionary found = GetPdfDictionary(database);

            if (found != null)
            {
                return found;
            }

            database.File.Requires(1024, "PDF underlays", "R2010 (AC1024)");
            DxfDBDictionary root = database.NamedObjectsDictionary;

            if (root == null)
            {
                throw new InvalidOperationException(
                    "The drawing has no named object dictionary to hold its dictionary of PDF underlays."
                );
            }

            var underlays = new DxfDBDictionary();
            root.SetAt(DictionaryName, underlays);
            return underlays;
        }

        /// <summary>
        /// Gives a name for a definition of a page in a dictionary of PDF underlays, as the External References palette names
        /// one: the file's name without its folder or extension, then <c> - Page_</c> and the page, <c>sample - Page_1</c> for
        /// page <c>1</c> of <c>C:\plans\sample.pdf</c>; where the dictionary has that name, letters of either case the same,
        /// the name and <c>_1</c>, or <c>_2</c> and on, the first it has not.
        /// </summary>
        /// <param name="pdfDictionary">The dictionary of PDF underlays.</param>
        /// <param name="path">The path of the PDF.</param>
        /// <param name="itemName">The name of the page.</param>
        /// <returns>The name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the dictionary, the path or the page's name is null.</exception>
        public static string SuggestName(DxfDBDictionary pdfDictionary, string path, string itemName)
        {
            if (pdfDictionary == null)
            {
                throw new ArgumentNullException(nameof(pdfDictionary));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (itemName == null)
            {
                throw new ArgumentNullException(nameof(itemName));
            }

            string name = path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            int dot = name.LastIndexOf('.');
            name = (dot > 0 ? name.Substring(0, dot) : name) + " - Page_" + itemName;

            if (!pdfDictionary.Contains(name))
            {
                return name;
            }

            for (long n = 1; ; n++)
            {
                string next = name + "_" + n.ToString(CultureInfo.InvariantCulture);

                if (!pdfDictionary.Contains(next))
                {
                    return next;
                }
            }
        }

        /// <summary>Gives the definition as a line for debugging: its type, its handle, its path and its page.</summary>
        /// <returns>The line, such as <c>PDFDEFINITION 79 C:\plans\sample.pdf 1</c>.</returns>
        public override string ToString() =>
            "PDFDEFINITION " + (Handle ?? string.Empty) + " " + SourceFileName + " " + ItemName;

        /// <summary>The name of the dictionary of PDF underlays in the named object dictionary.</summary>
        internal const string DictionaryName = "ACAD_PDFUNDERLAYS";

        /// <summary>The groups a definition made in code is written with, after its subclass marker, in AutoCAD's order.</summary>
        internal IReadOnlyList<KeyValuePair<int, string>> MadeGroups => _made.AsReadOnly();

        /// <summary>The first group of a code of the definition's own part, as made in code or as the file writes it; null where it has none.</summary>
        private string Text(int code)
        {
            if (Item == null)
            {
                return _made.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();
            }

            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>Sets groups of the definition, each in its place, as made in code or in its record.</summary>
        private void Set(params KeyValuePair<int, string>[] values)
        {
            if (Item != null)
            {
                InFile("be changed").SetValues(Item, values, Order, false);
                return;
            }

            foreach (KeyValuePair<int, string> value in values)
            {
                int at = _made.FindIndex(g => g.Key == value.Key);
                _made[at] = value;
            }
        }
    }
}
