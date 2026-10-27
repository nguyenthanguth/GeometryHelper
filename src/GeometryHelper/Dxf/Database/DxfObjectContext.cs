namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A context an object can be drawn in, as AutoCAD's <c>ObjectContext</c>: a scale of the drawing's list of scales, which
    /// is the one kind of context a drawing holds, a <see cref="DxfAnnotationScale"/>. Found in a
    /// <see cref="DxfObjectContextCollection"/>; whether an annotative object is shown at it is <see cref="DxfDBObject.HasContext"/>.
    /// </summary>
    public abstract class DxfObjectContext
    {
        /// <summary>A context of a kind of the library's own.</summary>
        private protected DxfObjectContext()
        {
        }

        /// <summary>Gets the name of the collection the context is of, as AutoCAD's <c>CollectionName</c>: <c>ACDB_ANNOTATIONSCALES</c> for a scale.</summary>
        public abstract string CollectionName { get; }

        /// <summary>Gets or sets the name of the context, as AutoCAD's <c>Name</c>: <c>1:50</c> for a scale.</summary>
        public abstract string Name { get; set; }
    }
}
