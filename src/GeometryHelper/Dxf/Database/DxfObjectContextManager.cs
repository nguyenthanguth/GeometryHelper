using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The contexts of a drawing, as AutoCAD's <c>ObjectContextManager</c>: reached by <see cref="DxfDatabase.ObjectContextManager"/>,
    /// it gives the collection of the drawing's scales, <c>ACDB_ANNOTATIONSCALES</c>, the one collection a drawing holds.
    /// </summary>
    /// <remarks>
    /// AutoCAD's <c>RegisterContextCollection</c> and <c>UnregisterContextCollection</c> register a collection of an
    /// application's own while AutoCAD runs; a DXF file holds none such, and they are not here.
    /// </remarks>
    public sealed class DxfObjectContextManager
    {
        /// <summary>The name of the collection of scales, as AutoCAD names it.</summary>
        internal const string AnnotationScales = "ACDB_ANNOTATIONSCALES";

        private readonly DxfDatabase _database;

        internal DxfObjectContextManager(DxfDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Gets a collection of contexts by its name, as AutoCAD's <c>GetContextCollection</c>: <c>ACDB_ANNOTATIONSCALES</c>, letters
        /// of either case the same, the drawing's list of scales. <c>db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext("1:50")</c>
        /// gives the scale 1:50.
        /// </summary>
        /// <param name="collectionName">The name of the collection.</param>
        /// <returns>The collection; null for any other name, as AutoCAD gives none for a collection not registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public DxfObjectContextCollection GetContextCollection(string collectionName)
        {
            if (collectionName == null)
            {
                throw new ArgumentNullException(nameof(collectionName));
            }

            return string.Equals(collectionName.Trim(), AnnotationScales, StringComparison.OrdinalIgnoreCase) ? new DxfObjectContextCollection(_database) : null;
        }
    }
}
