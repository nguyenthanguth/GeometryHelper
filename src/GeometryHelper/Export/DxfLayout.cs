using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A layout of a DXF file, as AutoCAD's <c>Layout</c>: a tab of the drawing, Model or a sheet of paper space, with its name,
    /// its place among the tabs, its limits and extents, the block that holds its entities and its viewports, and the settings
    /// it is plotted with (<see cref="DxfPlotSettings"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layouts are <see cref="DxfDatabase.Layouts"/>, in the order of their tabs, and the entries of
    /// <see cref="DxfDatabase.LayoutDictionary"/>, by name. A drawing ezdxf made has Model and Layout1: Layout1's
    /// <see cref="BlockTableRecord"/> is <c>*Paper_Space</c>, its limits (0, 0) to (420, 297), a sheet of A3 on its side.
    /// </para>
    /// <para>
    /// A layout is read; its name is set, the entry of the layout dictionary renamed with it. Making layouts, and the plot
    /// settings, are AutoCAD's layout manager's.
    /// </para>
    /// </remarks>
    public sealed class DxfLayout : DxfPlotSettings
    {
        private const string Marker = "AcDbLayout";

        internal DxfLayout(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets or sets the name of the layout (group 1 of AcDbLayout), as AutoCAD's <c>LayoutName</c>: <c>Model</c>, <c>Layout1</c>.</summary>
        /// <value>Set, the record's name and the entry of the layout dictionary renamed; the name it has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take, or another layout has it,
        /// letters of either case the same.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layout is Model, which AutoCAD does not rename, or is erased.</exception>
        public string LayoutName
        {
            get => Text(Marker, 1);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, LayoutName, StringComparison.Ordinal))
                {
                    return;
                }

                DxfFile file = InFile("be renamed");

                if (ModelType || string.Equals(LayoutName, "Model", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The layout Model keeps its name, as AutoCAD keeps it.");
                }

                DxfSymbolTable.CheckName(value, nameof(value));
                DxfItem owner = file.ObjectItem(OwnerHandle);
                var dictionary = owner == null ? null : OfObject(owner) as DxfDBDictionary;
                string entry = dictionary?.NameOf(this);

                if (entry != null && !string.Equals(entry, value, StringComparison.OrdinalIgnoreCase) && dictionary.Contains(value))
                {
                    throw new ArgumentException("The drawing has a layout named " + value + " already, letters of either case the same.", nameof(value));
                }

                int at = PartIndex(Marker, 1);

                if (at < 0)
                {
                    throw new InvalidOperationException("The LAYOUT " + Handle + " has no name of its own to set.");
                }

                if (entry != null)
                {
                    dictionary.SetName(entry, value);
                }

                file.SetValueAt(Item, at, value);
            }
        }

        /// <summary>Gets the place of the layout among the tabs (group 71), as AutoCAD's <c>TabOrder</c>: 0 for Model, 1 for the first sheet.</summary>
        public int TabOrder => Whole(Marker, 71, 0);

        /// <summary>
        /// Gets whether the layout is the one shown when the drawing is opened, as AutoCAD's <c>TabSelected</c> says of the tab
        /// selected: Model where the header's <c>$TILEMODE</c> is 1 or not given, else the layout whose block is
        /// <c>*Paper_Space</c>. A drawing keeps no other selection of tabs.
        /// </summary>
        public bool TabSelected
        {
            get
            {
                DxfDatabase database = Item.File.Database;
                IReadOnlyList<DxfTypedValue> tile = database?.GetHeader("$TILEMODE");
                bool model = tile == null || tile.Count == 0 || tile[0].Value.Trim() != "0";
                DxfBlockTableRecord block = BlockTableRecord;

                if (model)
                {
                    return ModelType || (block != null && ReferenceEquals(block, database?.ModelSpace));
                }

                return block != null && ReferenceEquals(block, database?.PaperSpace);
            }
        }

        /// <summary>
        /// Gets the limits of the layout (groups 10, 20 and 11, 21 of AcDbLayout), as AutoCAD's <c>Limits</c>: the sheet a layout
        /// of paper space is drawn on, (0, 0) to (420, 297) for A3 on its side.
        /// </summary>
        public DxfExtents2d Limits => new DxfExtents2d(new GeoPoint2(Real(Marker, 10, 0.0), Real(Marker, 20, 0.0)), new GeoPoint2(Real(Marker, 11, 0.0), Real(Marker, 21, 0.0)));

        /// <summary>
        /// Gets the box the entities of the layout fill as last worked out (groups 14, 24, 34 and 15, 25, 35), as AutoCAD's
        /// <c>Extents</c>; empty where the layout has none, as a new layout's least corner (1E+20, 1E+20, 1E+20) above its
        /// greatest says.
        /// </summary>
        public GeoAabb3 Extents
        {
            get
            {
                var min = new GeoPoint3(Real(Marker, 14, 1E+20), Real(Marker, 24, 1E+20), Real(Marker, 34, 1E+20));
                var max = new GeoPoint3(Real(Marker, 15, -1E+20), Real(Marker, 25, -1E+20), Real(Marker, 35, -1E+20));
                return min.X > max.X || min.Y > max.Y || min.Z > max.Z ? default(GeoAabb3) : new GeoAabb3(min, max);
            }
        }

        /// <summary>
        /// Gets the block that holds the layout's entities (group 330 of AcDbLayout), as AutoCAD's <c>BlockTableRecordId</c> names
        /// it: <see cref="DxfDatabase.ModelSpace"/> for Model, <see cref="DxfDatabase.PaperSpace"/> for the layout last shown,
        /// <c>*Paper_Space0</c> and on for the others; null where the drawing has no block of the handle.
        /// </summary>
        public DxfBlockTableRecord BlockTableRecord
        {
            get
            {
                string handle = PartValue(Marker, 330)?.Trim();
                DxfDatabase database = Item.File.Database;

                if (handle == null || database == null)
                {
                    return null;
                }

                foreach (DxfBlockTableRecord block in database.BlockTable)
                {
                    if (string.Equals(block.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return block;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the viewports of the layout, as AutoCAD's <c>GetViewports</c> gives their ids: the VIEWPORTs of its block in the
        /// order of the file, the one of <see cref="DxfViewport.Number"/> 1 that shows the sheet itself among them; none for Model.
        /// </summary>
        /// <returns>The viewports, a new list.</returns>
        public IReadOnlyList<DxfViewport> GetViewports()
        {
            DxfBlockTableRecord block = BlockTableRecord;
            return block == null ? new List<DxfViewport>().AsReadOnly() : block.OfType<DxfViewport>().ToList().AsReadOnly();
        }

        /// <summary>Gives the layout as a line for debugging: its type, handle, name and tab.</summary>
        /// <returns>The line, such as <c>LAYOUT 1E Layout1, tab 1</c>.</returns>
        public override string ToString() => "LAYOUT " + (Handle ?? string.Empty) + " " + LayoutName + ", tab " + TabOrder.ToString(CultureInfo.InvariantCulture);
    }
}
