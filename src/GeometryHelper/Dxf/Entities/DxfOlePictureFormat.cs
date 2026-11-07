namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The kind of picture <see cref="DxfOle2Frame.GetPictureData"/> gives of a static OLE object, by the form of a file it is
    /// given in. The library's own: AutoCAD gives no picture data.
    /// </summary>
    public enum DxfOlePictureFormat
    {
        /// <summary>No picture: the object is embedded or linked, or its data is damaged.</summary>
        None = 0,

        /// <summary>A bitmap, a <c>Picture (Device Independent Bitmap)</c>, given as the bytes of a .bmp file.</summary>
        Dib = 1,

        /// <summary>A Windows metafile, a <c>Picture (Metafile)</c>, given as the bytes of a placeable .wmf file.</summary>
        Metafile = 2,

        /// <summary>An enhanced metafile, a <c>Picture (Enhanced Metafile)</c>, given as the bytes of an .emf file.</summary>
        EnhancedMetafile = 3,
    }
}
