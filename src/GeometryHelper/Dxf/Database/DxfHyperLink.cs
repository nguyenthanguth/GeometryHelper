using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A hyperlink of an entity or a block, as AutoCAD's <c>HyperLink</c>: the address it opens, the text it is shown by, and
    /// the place in the target it opens at, kept in the extended data of the application <c>PE_URL</c>.
    /// </summary>
    /// <remarks>
    /// <c>new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" }</c> added to an entity's
    /// <see cref="DxfEntity.Hyperlinks"/> is written as AutoCAD writes it: the address as a 1000, then between braces (1002)
    /// the description as a 1000 and the sub-location as another where there is one.
    /// </remarks>
    public sealed class DxfHyperLink
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _subLocation = string.Empty;

        /// <summary>Gets or sets the address the hyperlink opens, as AutoCAD's <c>Name</c>: a web address or a file; empty for one made with none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the address set is null.</exception>
        public string Name
        {
            get => _name;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the text the hyperlink is shown by, as AutoCAD's <c>Description</c>; empty where it has none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        public string Description
        {
            get => _description;
            set => _description = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets or sets the place in the target the hyperlink opens at, as AutoCAD's <c>SubLocation</c>: a named view, a layout; empty where it has none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the place set is null.</exception>
        public string SubLocation
        {
            get => _subLocation;
            set => _subLocation = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets what AutoCAD shows for the hyperlink, as its <c>DisplayString</c>: the description, or the address where there is none.</summary>
        public string DisplayString => _description.Length > 0 ? _description : _name;

        /// <summary>Gets how deep in blocks the hyperlink's owner is, as AutoCAD's <c>NestedLevel</c>: nought, the hyperlink of the object itself.</summary>
        public int NestedLevel => 0;

        /// <summary>Gets whether the hyperlink is of the outermost object, as AutoCAD's <c>IsOutermostContainer</c>: true, the hyperlink of the object itself.</summary>
        public bool IsOutermostContainer => true;

        /// <summary>Gives the hyperlink as a line for debugging: what is shown for it and its address.</summary>
        /// <returns>The line, such as <c>Plate B-12 (https://example.com/B-12.pdf)</c>.</returns>
        public override string ToString() => DisplayString + (DisplayString == _name ? string.Empty : " (" + _name + ")");

        /// <summary>Whether two hyperlinks have the same address, description and sub-location, letter for letter.</summary>
        internal static bool Same(DxfHyperLink a, DxfHyperLink b)
            => a != null && b != null && string.Equals(a._name, b._name, StringComparison.Ordinal) && string.Equals(a._description, b._description, StringComparison.Ordinal)
                && string.Equals(a._subLocation, b._subLocation, StringComparison.Ordinal);
    }
}
