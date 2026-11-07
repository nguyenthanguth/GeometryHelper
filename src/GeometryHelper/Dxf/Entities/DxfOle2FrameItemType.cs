namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The kind of OLE object an OLE2FRAME holds, as AutoCAD's <c>Ole2Frame.ItemType</c> numbers it: what MFC's
    /// <c>COleClientItem::GetType</c> gives, and AutoCAD writes as group 71.
    /// </summary>
    public enum DxfOle2FrameItemType
    {
        /// <summary>A link to a document outside the drawing, 1: the drawing holds where it is, and its last picture.</summary>
        Link = 1,

        /// <summary>An object embedded, 2: the document of its application held in the drawing, as a sheet of Excel or a file packed.</summary>
        Embedded = 2,

        /// <summary>A static picture, 3: a bitmap or a metafile pasted, of no application to edit it in.</summary>
        Static = 3,
    }
}
