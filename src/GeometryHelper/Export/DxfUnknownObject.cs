namespace GeometryHelper.Export
{
    /// <summary>
    /// An object of the OBJECTS section of a type the library has no class for yet, as an XRECORD or a MATERIAL: kept as the
    /// file has it, with its handle, owner, extended data and extension dictionary, as every <see cref="DxfDBObject"/> has.
    /// </summary>
    internal sealed class DxfUnknownObject : DxfDBObject
    {
        internal DxfUnknownObject(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gives the object as a line for debugging: its type and its handle.</summary>
        /// <returns>The line, such as <c>XRECORD 2F</c>.</returns>
        public override string ToString() => Item.Type + " " + (Handle ?? string.Empty);
    }
}
