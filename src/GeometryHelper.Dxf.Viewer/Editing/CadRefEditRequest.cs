using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// AutoCAD 2021's Reference Edit dialog of REFEDIT, as data the command and the window share: the references the point
    /// picked reaches (<see cref="Levels"/>, each a block or an external reference with the path of its file), the one chosen,
    /// whether the objects to edit are all taken or asked for by point, and the three settings of its Settings tab: display of
    /// attribute definitions, unique layer and symbol names, and the lock on the objects outside the working set. The window
    /// fills it and sets <see cref="IsAccepted"/> on OK; the command then writes the settings to the document and opens the
    /// edit. With no window to show it, REFEDIT asks the same things on the command line, as -REFEDIT does.
    /// </summary>
    public sealed class CadRefEditRequest
    {
        /// <summary>Makes the request of a document: the settings are those the document holds, the first reference chosen, every object taken.</summary>
        /// <param name="document">The document.</param>
        /// <param name="levels">The references the dialog lists, the outermost first, as <see cref="CadDocument.FindRefEditLevels"/> gives them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the levels are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no level.</exception>
        public CadRefEditRequest(CadDocument document, IReadOnlyList<CadRefEditLevel> levels)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Levels = levels ?? throw new ArgumentNullException(nameof(levels));

            if (levels.Count == 0)
            {
                throw new ArgumentException("The dialog lists at least one reference.", nameof(levels));
            }

            DisplayAttributeDefinitions = document.RefEditAttributeDefinitions;
            UniqueNames = document.RefEditUniqueNames;
            LockObjects = document.RefEditLocksObjects;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the references the dialog lists, the outermost first.</summary>
        public IReadOnlyList<CadRefEditLevel> Levels { get; }

        /// <summary>Gets or sets which of <see cref="Levels"/> is chosen; the first, the reference picked, until set.</summary>
        public int SelectedIndex { get; set; }

        /// <summary>Gets the reference chosen.</summary>
        public CadRefEditLevel Selected => Levels[Math.Max(0, Math.Min(Levels.Count - 1, SelectedIndex))];

        /// <summary>Gets or sets whether the objects to edit are asked for by point (Prompt to select nested objects); false takes every object (Automatically select all nested objects).</summary>
        public bool PromptForObjects { get; set; }

        /// <summary>Gets or sets whether the attribute definitions of the block are in the working set (Display attribute definitions for editing).</summary>
        public bool DisplayAttributeDefinitions { get; set; }

        /// <summary>Gets or sets whether the layers of an external reference's objects take names of their own while they are edited (Enable unique layer and symbol names).</summary>
        public bool UniqueNames { get; set; }

        /// <summary>Gets or sets whether the objects outside the working set are locked (Lock objects not in working set).</summary>
        public bool LockObjects { get; set; }

        /// <summary>Gets or sets whether OK was pressed; false for Cancel, Esc and the close button.</summary>
        public bool IsAccepted { get; set; }

        /// <summary>Gets whether a reference of the list has attribute definitions, which the setting to display them is for.</summary>
        /// <param name="index">The place of the reference in <see cref="Levels"/>.</param>
        /// <returns>True for a block that holds attribute definitions; false for any other, and for an index outside the list.</returns>
        public bool HasAttributeDefinitionsAt(int index) =>
            index >= 0 && index < Levels.Count && Document.RefEditBlockHasAttributeDefinitions(Levels[index].Chain);

        /// <summary>Writes the three settings to the document, as OK does; the edit then opens with them.</summary>
        public void Apply()
        {
            Document.RefEditAttributeDefinitions = DisplayAttributeDefinitions;
            Document.RefEditUniqueNames = UniqueNames;
            Document.RefEditLocksObjects = LockObjects;
        }
    }
}
