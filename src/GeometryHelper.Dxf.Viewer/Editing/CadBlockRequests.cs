using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a dialog of the block commands hands back to its command when it closes: OK, Cancel, or a pick on the canvas the
    /// command asks on the command line before it shows the dialog again, as AutoCAD hides its dialog for Pick point.
    /// </summary>
    public enum CadDialogAction
    {
        /// <summary>Cancel or the close button: nothing is made.</summary>
        Cancel,

        /// <summary>OK: the command makes what the dialog says.</summary>
        Ok,

        /// <summary>Pick point: the command asks a point, then shows the dialog again with it.</summary>
        PickPoint,

        /// <summary>Select objects: the command asks "Select objects:", then shows the dialog again with them.</summary>
        SelectObjects,

        /// <summary>The text height picked on the canvas, as a distance from the insertion point.</summary>
        PickHeight,

        /// <summary>The rotation picked on the canvas, as the angle from the insertion point.</summary>
        PickRotation,

        /// <summary>Select block: the command asks another INSERT to edit, then shows the dialog again.</summary>
        SelectBlock,

        /// <summary>Apply: the command writes the values and shows the dialog again, as the Enhanced Attribute Editor's Apply.</summary>
        Apply,
    }

    /// <summary>What the block dialogs check and list as the block commands do: the names AutoCAD takes and the blocks a user names.</summary>
    public static class CadBlocks
    {
        /// <summary>
        /// Gets the blocks of a drawing a user names, in ordinal order of their names, letters of either case alike first: no
        /// layout, no block AutoCAD names itself (*U1, *D2), no external reference.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The blocks.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<DxfBlockTableRecord> Named(DxfDatabase database) => CadCommands.NamedBlocks(database ?? throw new ArgumentNullException(nameof(database)));

        /// <summary>Gets whether AutoCAD takes a name for a block: 1 to 255 letters, none of &lt;&gt;/\":;?*|,=` and no space at either end.</summary>
        /// <param name="name">The name.</param>
        /// <returns>True where it does: GRID_BUBBLE, not GRID*.</returns>
        public static bool IsName(string name) => CadCommands.IsBlockName(name);

        /// <summary>Gets whether AutoCAD takes a tag for an attribute: one word, with no space.</summary>
        /// <param name="tag">The tag.</param>
        /// <returns>True where it does: GRID, not GRID LETTER.</returns>
        public static bool IsTag(string tag) => CadCommands.IsTag(tag);
    }

    /// <summary>What BLOCK and WBLOCK do with the objects a block is made of: AutoCAD's Retain, Convert to block and Delete.</summary>
    public enum CadBlockObjectAction
    {
        /// <summary>Retain: the objects stay as they are.</summary>
        Retain,

        /// <summary>Convert to block: the objects are erased and an INSERT of the new block placed where they were.</summary>
        ConvertToBlock,

        /// <summary>Delete: the objects are erased.</summary>
        Delete,
    }

    /// <summary>Where WBLOCK takes what it writes from: a block of the drawing, the whole drawing, or objects selected.</summary>
    public enum CadWriteBlockSource
    {
        /// <summary>A block of the drawing, its origin at the origin of the new drawing.</summary>
        Block,

        /// <summary>The whole of model space, its base point ($INSBASE) kept.</summary>
        EntireDrawing,

        /// <summary>Objects selected, the base point given at the origin of the new drawing.</summary>
        Objects,
    }

    /// <summary>
    /// The Block Definition dialog of BLOCK, as data its command and the window share: the window fills it and says what was
    /// pressed in <see cref="Action"/>; the command asks the picks and makes the block. A test fills it with no window.
    /// </summary>
    public sealed class CadBlockDefinitionRequest
    {
        /// <summary>Makes the request of a document, as BLOCK opens its dialog: Convert to block, Allow exploding, the drawing's units.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadBlockDefinitionRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Units = document.Database.Insunits == DxfUnits.Undefined ? DxfUnits.Millimeters : document.Database.Insunits;
        }

        /// <summary>Gets the document the block is made in.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets the name of the block, GRID_BUBBLE; up to 255 letters, none of &lt;&gt;/\":;?*|,=`.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the base point, in world coordinates; the origin until picked or typed.</summary>
        public GeoPoint3 BasePoint { get; set; }

        /// <summary>Gets or sets whether the base point is asked on the command line after OK (Specify On-screen).</summary>
        public bool SpecifyBasePointOnScreen { get; set; }

        /// <summary>Gets or sets the handles of the objects the block is made of, in ordinal order.</summary>
        public IReadOnlyList<string> Objects { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets whether the objects are asked on the command line after OK (Specify On-screen).</summary>
        public bool SpecifyObjectsOnScreen { get; set; }

        /// <summary>Gets or sets what becomes of the objects; Convert to block until set, as AutoCAD's dialog opens.</summary>
        public CadBlockObjectAction ObjectAction { get; set; } = CadBlockObjectAction.ConvertToBlock;

        /// <summary>Gets or sets whether the INSERTs of the block scale it alike along every axis (Scale uniformly).</summary>
        public bool ScaleUniformly { get; set; }

        /// <summary>Gets or sets whether the INSERTs of the block may be exploded (Allow exploding); true until set.</summary>
        public bool AllowExploding { get; set; } = true;

        /// <summary>Gets or sets the unit of the block, which an INSERT scales to the drawing's; the drawing's own until set.</summary>
        public DxfUnits Units { get; set; }

        /// <summary>Gets or sets the description of the block.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether a block of the name already defined is to be defined again, as the dialog's question answered Yes.</summary>
        public bool Redefine { get; set; }

        /// <summary>Gets or sets what the window's dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>
    /// The Blocks palette of INSERT, as data its command and the window share: the block chosen (of the drawing, or of
    /// another drawing's file) and the insertion options; the command asks what is to be specified on screen and places it.
    /// </summary>
    public sealed class CadInsertRequest
    {
        /// <summary>Makes the request of a document, as INSERT opens its palette: the point asked, scale 1, no rotation.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadInsertRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document the block is placed in.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets the name of the block chosen; for a file, a block of it, or empty to insert the whole file as one block.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the DXF file the block comes from (Other drawing); null for a block of the drawing.</summary>
        public string FileName { get; set; }

        /// <summary>Gets or sets whether the insertion point is asked on the command line; true until set.</summary>
        public bool SpecifyPointOnScreen { get; set; } = true;

        /// <summary>Gets or sets the insertion point where it is not asked.</summary>
        public GeoPoint3 Point { get; set; }

        /// <summary>Gets or sets whether the scale is asked on the command line.</summary>
        public bool SpecifyScaleOnScreen { get; set; }

        /// <summary>Gets or sets the scale along X, Y and Z where it is not asked; 1, 1, 1 until set.</summary>
        public GeoVector3 Scale { get; set; } = new GeoVector3(1.0, 1.0, 1.0);

        /// <summary>Gets or sets whether the rotation is asked on the command line.</summary>
        public bool SpecifyRotationOnScreen { get; set; }

        /// <summary>Gets or sets the rotation where it is not asked, in radians.</summary>
        public double Rotation { get; set; }

        /// <summary>Gets or sets whether the block is placed again and again until Enter or Esc (Repeat Placement).</summary>
        public bool Repeat { get; set; }

        /// <summary>Gets or sets whether the INSERT is exploded once placed (Explode), its attributes coming out as their definitions.</summary>
        public bool Explode { get; set; }

        /// <summary>Gets or sets what the window's palette was closed with; Cancel until set, OK when a block was chosen.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>
    /// The Write Block dialog of WBLOCK, as data its command and the window share: the source, its base point and objects,
    /// and the file written.
    /// </summary>
    public sealed class CadWriteBlockRequest
    {
        /// <summary>Makes the request of a document, as WBLOCK opens its dialog: Objects, Retain, the drawing's units.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadWriteBlockRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Units = document.Database.Insunits == DxfUnits.Undefined ? DxfUnits.Millimeters : document.Database.Insunits;
        }

        /// <summary>Gets the document written from.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets where what is written comes from; Objects until set.</summary>
        public CadWriteBlockSource Source { get; set; } = CadWriteBlockSource.Objects;

        /// <summary>Gets or sets the block written where the source is a block.</summary>
        public string BlockName { get; set; } = string.Empty;

        /// <summary>Gets or sets the base point of objects written, which comes to the origin of the new drawing.</summary>
        public GeoPoint3 BasePoint { get; set; }

        /// <summary>Gets or sets the handles of the objects written, in ordinal order.</summary>
        public IReadOnlyList<string> Objects { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets what becomes of the objects; Retain until set, as AutoCAD's dialog opens.</summary>
        public CadBlockObjectAction ObjectAction { get; set; } = CadBlockObjectAction.Retain;

        /// <summary>Gets or sets the path of the DXF file written.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Gets or sets the unit of the new drawing ($INSUNITS); the drawing's own until set.</summary>
        public DxfUnits Units { get; set; }

        /// <summary>Gets or sets what the window's dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>
    /// The Attribute Definition dialog of ATTDEF, as data its command and the window share: the modes, the attribute, where
    /// it stands and its text.
    /// </summary>
    public sealed class CadAttributeDefinitionRequest
    {
        /// <summary>Makes the request of a document, as ATTDEF opens its dialog: Lock position, the point asked, left on the baseline.</summary>
        /// <param name="document">The document.</param>
        /// <param name="style">The text style; null for Standard.</param>
        /// <param name="height">The text height offered.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadAttributeDefinitionRequest(CadDocument document, string style, double height)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            TextStyle = style;
            Height = height;
        }

        /// <summary>Gets the document the definition is made in.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets whether the attribute is not shown (Invisible).</summary>
        public bool Invisible { get; set; }

        /// <summary>Gets or sets whether its value is fixed, never asked (Constant).</summary>
        public bool Constant { get; set; }

        /// <summary>Gets or sets whether its value is asked twice (Verify).</summary>
        public bool Verify { get; set; }

        /// <summary>Gets or sets whether its default is taken without asking (Preset).</summary>
        public bool Preset { get; set; }

        /// <summary>Gets or sets whether it keeps its place in the block (Lock position); true until set, as AutoCAD's.</summary>
        public bool LockPosition { get; set; } = true;

        /// <summary>Gets or sets whether it is of several lines (Multiple lines), which needs a drawing of R2018.</summary>
        public bool MultipleLines { get; set; }

        /// <summary>Gets or sets the tag, GRID: one word, written in capitals.</summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>Gets or sets what an INSERT asks for it, Grid letter; the tag where empty.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Gets or sets the default value.</summary>
        public string Default { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the insertion point is asked on the command line after OK; true until set.</summary>
        public bool SpecifyOnScreen { get; set; } = true;

        /// <summary>Gets or sets the insertion point where it is not asked.</summary>
        public GeoPoint3 Point { get; set; }

        /// <summary>Gets or sets the justification; left on the baseline until set.</summary>
        public DxfAttachmentPoint Justification { get; set; } = DxfAttachmentPoint.BaseLeft;

        /// <summary>Gets or sets the text style; null for Standard.</summary>
        public string TextStyle { get; set; }

        /// <summary>Gets or sets the text height.</summary>
        public double Height { get; set; }

        /// <summary>Gets or sets the rotation, in radians.</summary>
        public double Rotation { get; set; }

        /// <summary>Gets or sets what the window's dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>One attribute of an INSERT as the Enhanced Attribute Editor shows it: its value, text options and properties.</summary>
    public sealed class CadAttributeRow
    {
        /// <summary>Gets or sets the handle of the ATTRIB.</summary>
        public string Handle { get; set; }

        /// <summary>Gets or sets the tag.</summary>
        public string Tag { get; set; }

        /// <summary>Gets or sets the prompt of its definition; empty where the block has none of the tag.</summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>Gets or sets the value.</summary>
        public string Value { get; set; } = string.Empty;

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

        /// <summary>Gets or sets the layer.</summary>
        public string Layer { get; set; }

        /// <summary>Gets or sets the linetype, ByLayer and the rest by name.</summary>
        public string Linetype { get; set; }

        /// <summary>Gets or sets the colour as its index: 256 ByLayer, 0 ByBlock, 1 to 255 the index colours.</summary>
        public int ColorIndex { get; set; }

        /// <summary>Gets or sets the lineweight in hundredths of a millimetre; -1 ByLayer, -2 ByBlock, -3 the default.</summary>
        public int LineWeight { get; set; }

        /// <summary>Gets a copy of the row.</summary>
        /// <returns>The copy.</returns>
        public CadAttributeRow Clone() => (CadAttributeRow)MemberwiseClone();
    }

    /// <summary>
    /// The Enhanced Attribute Editor of EATTEDIT (and ATTEDIT), as data its command and the window share: the INSERT edited
    /// and a row for each of its attributes; the command writes the rows changed on Apply and OK.
    /// </summary>
    public sealed class CadAttributeEditRequest
    {
        internal CadAttributeEditRequest(CadDocument document, string insert, string block, IReadOnlyList<CadAttributeRow> rows)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Insert = insert;
            Block = block;
            Rows = rows;
        }

        /// <summary>Makes the request over the attributes of an INSERT as the drawing has them, as EATTEDIT opens on it.</summary>
        /// <param name="document">The document.</param>
        /// <param name="insert">The handle of the INSERT.</param>
        /// <returns>The request; null where the handle is not that of an INSERT with attributes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadAttributeEditRequest Of(CadDocument document, string insert)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return document.FindEntity(insert) is DxfBlockReference reference && reference.AttributeCollection.Count > 0 ? CadCommands.Rows(document, insert) : null;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the handle of the INSERT edited.</summary>
        public string Insert { get; }

        /// <summary>Gets the name of its block.</summary>
        public string Block { get; }

        /// <summary>Gets a row for each attribute, in the order of the INSERT, as the drawing has them; the window edits these.</summary>
        public IReadOnlyList<CadAttributeRow> Rows { get; }

        /// <summary>Gets or sets what the window's dialog was closed with, or Apply; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }
    }
}
