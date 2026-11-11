using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// One attribute definition of a block as the Block Attribute Manager lists and edits it: its tag, prompt and default,
    /// its modes, its text options and its properties. The manager edits a copy; nothing reaches the drawing until Apply, OK
    /// or Sync.
    /// </summary>
    public sealed class CadAttributeDefinitionRow
    {
        /// <summary>Gets or sets the handle of the ATTDEF the row was read from.</summary>
        public string Handle { get; set; }

        /// <summary>Gets or sets the tag, GRID: one word, written in capitals.</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>Gets or sets what an INSERT asks for it, Grid letter.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Gets or sets the default value.</summary>
        public string Default { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the attribute is not shown (Invisible).</summary>
        public bool Invisible { get; set; }

        /// <summary>Gets or sets whether its value is fixed (Constant); set by ATTDEF, not by the manager.</summary>
        public bool Constant { get; set; }

        /// <summary>Gets or sets whether its value is asked twice (Verify).</summary>
        public bool Verify { get; set; }

        /// <summary>Gets or sets whether its default is taken without asking (Preset).</summary>
        public bool Preset { get; set; }

        /// <summary>Gets or sets whether it keeps its place in the block (Lock position).</summary>
        public bool LockPosition { get; set; }

        /// <summary>Gets or sets whether it is of several lines (Multiple lines); set by ATTDEF, not by the manager.</summary>
        public bool MultipleLines { get; set; }

        /// <summary>Gets or sets the text style.</summary>
        public string TextStyle { get; set; }

        /// <summary>Gets or sets the justification.</summary>
        public DxfAttachmentPoint Justification { get; set; }

        /// <summary>Gets or sets the height.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the rotation, in radians.</summary>
        public double Rotation { get; set; }

        /// <summary>Gets or sets whether the text is written backwards (mirrored in X).</summary>
        public bool Backwards { get; set; }

        /// <summary>Gets or sets whether the text is upside down (mirrored in Y).</summary>
        public bool UpsideDown { get; set; }

        /// <summary>Gets or sets the width factor.</summary>
        public double WidthFactor { get; set; }

        /// <summary>Gets or sets the oblique angle, in radians.</summary>
        public double Oblique { get; set; }

        /// <summary>Gets or sets the width of the text of an attribute of several lines; nought for one line.</summary>
        public double BoundaryWidth { get; set; }

        /// <summary>Gets or sets the layer.</summary>
        public string Layer { get; set; }

        /// <summary>Gets or sets the linetype, ByLayer and the rest by name.</summary>
        public string Linetype { get; set; }

        /// <summary>Gets or sets the colour as its index: 256 ByLayer, 0 ByBlock, 1 to 255 the index colours.</summary>
        public int ColorIndex { get; set; }

        /// <summary>Gets or sets the lineweight in hundredths of a millimetre; -1 ByLayer, -2 ByBlock, -3 the default.</summary>
        public int LineWeight { get; set; }

        /// <summary>
        /// Gets the modes as the manager's Modes column writes them, a letter for each set in AutoCAD's order: I invisible, C
        /// constant, V verify, P preset, L lock position, M multiple lines. A locked attribute of a constant value reads CL.
        /// </summary>
        public string Modes
        {
            get
            {
                var letters = new StringBuilder();
                letters
                    .Append(Invisible ? "I" : string.Empty)
                    .Append(Constant ? "C" : string.Empty)
                    .Append(Verify ? "V" : string.Empty);
                letters
                    .Append(Preset ? "P" : string.Empty)
                    .Append(LockPosition ? "L" : string.Empty)
                    .Append(MultipleLines ? "M" : string.Empty);
                return letters.ToString();
            }
        }

        /// <summary>Gets a copy of the row.</summary>
        /// <returns>The copy.</returns>
        public CadAttributeDefinitionRow Clone() => (CadAttributeDefinitionRow)MemberwiseClone();
    }

    /// <summary>
    /// What the Block Attribute Manager keeps from one BATTMAN to the next, as its Settings dialog sets it: the columns its
    /// grid shows, whether tags given twice are drawn in red, and whether Apply and OK bring the INSERTs of the block in line
    /// at once, as ATTSYNC would.
    /// </summary>
    public sealed class CadAttributeManagerSettings
    {
        /// <summary>Gets the columns the grid may show, in AutoCAD's order; Tag is always shown.</summary>
        public static IReadOnlyList<string> AllColumns { get; } =
            new[]
            {
                "Tag",
                "Prompt",
                "Default",
                "Modes",
                "Annotative",
                "Style",
                "Justification",
                "Height",
                "Rotation",
                "Width Factor",
                "Oblique Angle",
                "Boundary Width",
                "Layer",
                "Linetype",
                "Color",
                "Lineweight",
                "Plot style",
            };

        /// <summary>Gets the columns shown, in the order of <see cref="AllColumns"/>; Tag, Prompt, Default and Modes until set.</summary>
        public IList<string> Columns { get; } = new List<string> { "Tag", "Prompt", "Default", "Modes" };

        /// <summary>Gets or sets whether a tag the block defines twice is drawn in red in the grid; true until set.</summary>
        public bool EmphasizeDuplicateTags { get; set; } = true;

        /// <summary>
        /// Gets or sets whether Apply and OK bring every INSERT of the block in line with its definitions at once, as Sync
        /// does; false until set, so that the INSERTs change only on Sync.
        /// </summary>
        public bool ApplyToExistingReferences { get; set; }
    }

    /// <summary>
    /// The Block Attribute Manager of BATTMAN, as data its command and the window share: the blocks with attributes, the one
    /// shown and a row for each of its attribute definitions, in the block's order. The window moves, edits and removes rows;
    /// <see cref="Apply"/> and OK write them into the block as one undo step, <see cref="Sync"/> writes them and brings every
    /// INSERT of the block in line, as ATTSYNC does, as one undo step.
    /// </summary>
    public sealed class CadAttributeManagerRequest
    {
        internal CadAttributeManagerRequest(
            CadDocument document,
            CadAttributeManagerSettings settings,
            IReadOnlyList<string> blocks
        )
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Blocks = blocks;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the settings, which the Settings dialog changes and BATTMAN keeps for the next run.</summary>
        public CadAttributeManagerSettings Settings { get; }

        /// <summary>Gets the blocks of the drawing with attribute definitions, in ordinal order of their names.</summary>
        public IReadOnlyList<string> Blocks { get; }

        /// <summary>Gets the block shown.</summary>
        public string Block { get; internal set; }

        /// <summary>Gets the rows of the block shown, in its order; the window moves, edits and removes these.</summary>
        public IList<CadAttributeDefinitionRow> Rows { get; } = new List<CadAttributeDefinitionRow>();

        /// <summary>Gets how many INSERTs of the block the drawing holds, in its spaces, layouts and other blocks.</summary>
        public int FoundInDrawing { get; internal set; }

        /// <summary>Gets how many of them stand in model space.</summary>
        public int FoundInModelSpace { get; internal set; }

        /// <summary>Gets or sets what the window's dialog was closed with: OK, Cancel or Select block; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>What the command does on <see cref="Apply"/> and <see cref="Sync"/>, the INSERTs synchronized where true.</summary>
        internal Action<bool> Applying { get; set; }

        /// <summary>What the command does on <see cref="Show"/>.</summary>
        internal Action<string> Showing { get; set; }

        /// <summary>What the command answers to <see cref="HasChanges"/>.</summary>
        internal Func<bool> Comparing { get; set; }

        /// <summary>What the command does on <see cref="Revert"/>.</summary>
        internal Action Reverting { get; set; }

        /// <summary>
        /// Gets whether the rows differ from the block's definitions, a row moved, edited or removed and not yet written by
        /// Apply, OK or Sync; AutoCAD's manager then asks to save the changes before it shows another block.
        /// </summary>
        public bool HasChanges => Comparing?.Invoke() ?? false;

        /// <summary>The rows read again from the block, the changes made to them dropped, as No to the question of saving them.</summary>
        public void Revert() => Reverting?.Invoke();

        /// <summary>
        /// Apply, the manager staying open: the rows written into the block, as one undo step, and read again; the INSERTs of
        /// the block brought in line too where <see cref="CadAttributeManagerSettings.ApplyToExistingReferences"/> says so.
        /// Nothing is written where nothing changed.
        /// </summary>
        public void Apply() => Applying?.Invoke(Settings.ApplyToExistingReferences);

        /// <summary>
        /// Sync: the rows written into the block, then every INSERT of it brought in line with them as ATTSYNC brings it, the
        /// values typed in them kept, as one undo step; the rows read again.
        /// </summary>
        public void Sync() => Applying?.Invoke(true);

        /// <summary>Shows another block of <see cref="Blocks"/>, the changes made to the rows of this one applied first.</summary>
        /// <param name="block">The name of the block.</param>
        public void Show(string block) => Showing?.Invoke(block);
    }

    /// <summary>One attribute an INSERT is given, as the Edit Attributes dialog asks its value.</summary>
    public sealed class CadAttributeValueRow
    {
        internal CadAttributeValueRow(string tag, string prompt, string value)
        {
            Tag = tag;
            Prompt = prompt;
            Value = value;
        }

        /// <summary>Gets the tag of the definition.</summary>
        public string Tag { get; }

        /// <summary>Gets the prompt of the definition, its tag where it has none.</summary>
        public string Prompt { get; }

        /// <summary>Gets or sets the value; the default of the definition until set.</summary>
        public string Value { get; set; }
    }

    /// <summary>
    /// The Edit Attributes dialog INSERT shows with ATTDIA 1, as data its command and the window share: a row for each
    /// attribute definition of the block that is not constant, in the block's order. OK takes the values; Cancel places the
    /// INSERT with the defaults. A value to be verified is asked once here, not twice.
    /// </summary>
    public sealed class CadAttributeValuesRequest
    {
        internal CadAttributeValuesRequest(CadDocument document, string block, IReadOnlyList<CadAttributeValueRow> rows)
        {
            Document = document;
            Block = block;
            Rows = rows;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the name of the block placed.</summary>
        public string Block { get; }

        /// <summary>Gets the rows, in the order of the block.</summary>
        public IReadOnlyList<CadAttributeValueRow> Rows { get; }

        /// <summary>Gets or sets what the window's dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }
    }
}
