using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
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
    /// A layout is read; its name is set, the entry of the layout dictionary renamed with it. One made in code is added as
    /// AutoCAD's is, <c>var plan = new DxfLayout { LayoutName = "Plan 1-50" }; plan.AddToLayoutDictionary(db, null);
    /// plan.Initialize();</c>: its LAYOUT, its block and its entry by <see cref="AddToLayoutDictionary"/>, the viewport that
    /// shows its sheet by <see cref="Initialize"/>. The plot settings are read, not set.
    /// </para>
    /// </remarks>
    public sealed class DxfLayout : DxfPlotSettings
    {
        private const string Marker = "AcDbLayout";

        // The groups of AcDbPlotSettings and AcDbLayout in the order AutoCAD writes them, a 100 where each marker stands.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[]
        {
            100, 1, 2, 4, 6, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 140, 141, 142, 143, 70, 72, 73, 74, 7, 75, 76, 77, 78, 147, 148, 149, 333,
            100, 1, 70, 71, 10, 20, 11, 21, 12, 22, 32, 14, 24, 34, 15, 25, 35, 146, 13, 23, 33, 16, 26, 36, 17, 27, 37, 76, 330, 331, 345, 346,
        });

        // The groups of a BLOCK_RECORD in the order AutoCAD writes them, after its marker AcDbSymbolTableRecord.
        private static readonly IReadOnlyList<int> BlockRecordOrder = Array.AsReadOnly(new[] { 100, 100, 2, 340, 70, 280, 281, 310 });

        private string _name;

        /// <summary>
        /// Initializes a layout made in code, as AutoCAD's <c>Layout()</c>: give it its <see cref="LayoutName"/>, then add it to a
        /// drawing by <see cref="AddToLayoutDictionary"/> and give it the viewport of its sheet by <see cref="Initialize"/>.
        /// </summary>
        public DxfLayout()
        {
        }

        internal DxfLayout(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets or sets the name of the layout (group 1 of AcDbLayout), as AutoCAD's <c>LayoutName</c>: <c>Model</c>,
        /// <c>Layout1</c>; empty for a layout made in code and not named yet.
        /// </summary>
        /// <value>Set on a layout made in code, before it is added; on a layout of a drawing, the record's name and the entry of the layout dictionary renamed. The name it has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take, or another layout has it,
        /// letters of either case the same.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layout is Model, which AutoCAD does not rename, or is erased.</exception>
        public string LayoutName
        {
            get => Item == null ? _name ?? string.Empty : Text(Marker, 1);

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

                if (Item == null)
                {
                    DxfSymbolTable.CheckName(value, nameof(value));
                    _name = value;
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
        /// <c>*Paper_Space</c>. A drawing keeps no other selection of tabs; a layout made in code and not added is not selected.
        /// </summary>
        public bool TabSelected
        {
            get
            {
                if (Item == null)
                {
                    return false;
                }

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
        /// <c>*Paper_Space0</c> and on for the others; null where the drawing has no block of the handle, and for a layout made in
        /// code and not added.
        /// </summary>
        public DxfBlockTableRecord BlockTableRecord
        {
            get
            {
                string handle = PartValue(Marker, 330)?.Trim();
                DxfDatabase database = Item?.File.Database;

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

        /// <summary>
        /// Adds a layout made in code to a drawing, as AutoCAD's <c>AddToLayoutDictionary</c>: a LAYOUT at the end of the OBJECTS
        /// section, named in the layout dictionary (ACAD_LAYOUT) by its <see cref="LayoutName"/>, on the tab after the last, with
        /// the plot settings of a sheet of A3 on its side and no plotter; and the block that holds its entities, whose
        /// BLOCK_RECORD names the layout (group 340), made where none is given. The layout has no viewport until
        /// <see cref="Initialize"/> gives it one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A drawing ezdxf made with Model, Layout1 and Sheet A on tab 3, its block <c>*Paper_Space0</c>, is given
        /// <c>new DxfLayout { LayoutName = "Plan 1-50" }</c> on tab 4 with the block <c>*Paper_Space1</c>, the first of
        /// <c>*Paper_Space0</c>, <c>*Paper_Space1</c> and on the drawing has not, as AutoCAD and ezdxf name it.
        /// </para>
        /// <para>
        /// The plot settings are ezdxf's for a new layout, named as AutoCAD names no plotter and its sheet of ISO A3: no page
        /// setup, the device <c>none_device</c>, the sheet <c>ISO_A3_(420.00_x_297.00_MM)</c> 420 by 297 in millimetres,
        /// margins 7.5 at the sides and 20 above and below, plotted as a layout at 1:1 (flags 688: viewports first,
        /// lineweights, plot styles, the standard scale), shaded as displayed at the normal level of 300 dots an inch. The
        /// limits are the sheet about the corner of its plotted area, (-7.5, -20) to (412.5, 277), its extents empty, its user
        /// coordinate system the world's, and its flags 1 (PSLTSCALE), as ezdxf's layouts have them.
        /// </para>
        /// <para>
        /// Each layout added costs a little more the more the drawing has, its dictionary and block table written anew: 200
        /// layouts added and initialized in one drawing take 0.14 to 0.25 s once the code is compiled, and 0.35 to 0.56 s as
        /// the first work of a process; AutoCAD allows 255.
        /// </para>
        /// </remarks>
        /// <param name="toWhichDatabase">The drawing, of R2000 or later.</param>
        /// <param name="blockTableRecord">The block of the layout: one of the drawing's named <c>*Paper_Space</c> and a number,
        /// as AutoCAD names those of its layouts, that no layout has, its BLOCK_RECORD naming none (340 of 0, or no 340); null
        /// to have one made.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has a layout of the name already, letters of either case
        /// the same, or the block given is not one of the drawing's named <c>*Paper_Space</c> and a number, or is a layout's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the layout is in a drawing already, or has no name, or the
        /// drawing has no layout dictionary, as one older than R2000 (AC1015) has none.</exception>
        public void AddToLayoutDictionary(DxfDatabase toWhichDatabase, DxfBlockTableRecord blockTableRecord)
        {
            if (toWhichDatabase == null)
            {
                throw new ArgumentNullException(nameof(toWhichDatabase));
            }

            if (Item != null)
            {
                throw new InvalidOperationException("The layout " + LayoutName + " is in a drawing already, as the LAYOUT " + (Item.Handle ?? string.Empty) + ".");
            }

            if (string.IsNullOrEmpty(_name))
            {
                throw new InvalidOperationException("A layout is given its LayoutName before it is added to a drawing.");
            }

            DxfFile file = toWhichDatabase.File;
            DxfDBDictionary dictionary = toWhichDatabase.LayoutDictionary;

            if (dictionary == null || dictionary.Handle == null || !file.Supports(1015))
            {
                throw new InvalidOperationException("A DXF " + (toWhichDatabase.Version ?? "with no version") + " has no layout dictionary to add a layout to: R2000 (AC1015) brought them.");
            }

            if (dictionary.Contains(_name))
            {
                throw new ArgumentException("The drawing has a layout named " + _name + " already, letters of either case the same.", nameof(toWhichDatabase));
            }

            // The layouts of the dictionary, in no order: the last tab and the blocks taken are what is asked of them.
            var layouts = new List<DxfLayout>();

            foreach (DxfDBDictionaryEntry entry in dictionary)
            {
                if (entry.Value is DxfLayout layout)
                {
                    layouts.Add(layout);
                }
            }

            DxfItem record;

            if (blockTableRecord != null)
            {
                record = blockTableRecord.Item;
                string name = blockTableRecord.Name ?? string.Empty;
                string linked = record == null || blockTableRecord.IsErased || blockTableRecord.File != file || record.Type != "BLOCK_RECORD" ? null : file.ValueOf(record, 340)?.Trim() ?? string.Empty;

                if (linked == null || (linked.Length > 0 && linked != "0") || !IsNumberedPaperSpace(name) || layouts.Any(l => ReferenceEquals(l.BlockTableRecord, blockTableRecord)))
                {
                    throw new ArgumentException("A layout is added with a block of the drawing named *Paper_Space and a number that no layout has, or with none to have one made: " + name + " is not one.", nameof(blockTableRecord));
                }
            }
            else
            {
                // The names of the blocks read once, letters of either case the same, so that a drawing of many layouts is not
                // walked once for each number tried.
                var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfBlockTableRecord existing in toWhichDatabase.BlockTable)
                {
                    taken.Add(existing.Name ?? string.Empty);
                }

                string name = null;

                for (int n = 0; name == null; n++)
                {
                    string candidate = DxfBlockTableRecord.PaperSpace + n.ToString(CultureInfo.InvariantCulture);
                    name = taken.Contains(candidate) ? null : candidate;
                }

                DxfFileBlock block = file.AddBlock(name, default(GeoPoint3), 0, true, false, string.Empty, out record);
                toWhichDatabase.BlockTable.Adopted(block, record);
            }

            int tab = 0;

            foreach (DxfLayout layout in layouts)
            {
                tab = Math.Max(tab, layout.TabOrder);
            }

            string owner = record.Handle;
            string layoutName = _name;
            Bind(dictionary.Add(layoutName, "LAYOUT", b =>
            {
                b.Group(100, "AcDbPlotSettings");
                b.Group(1, string.Empty);
                b.Group(2, "none_device");
                b.Group(4, "ISO_A3_(420.00_x_297.00_MM)");
                b.Group(6, string.Empty);
                b.Group(40, "7.5");
                b.Group(41, "20.0");
                b.Group(42, "7.5");
                b.Group(43, "20.0");
                b.Group(44, "420.0");
                b.Group(45, "297.0");

                foreach (int code in new[] { 46, 47, 48, 49, 140, 141 })
                {
                    b.Group(code, "0.0");
                }

                b.Group(142, "1.0");
                b.Group(143, "1.0");
                b.Group(70, "688");
                b.Group(72, "1");
                b.Group(73, "0");
                b.Group(74, "5");
                b.Group(7, string.Empty);
                b.Group(75, "16");
                b.Group(76, "0");
                b.Group(77, "2");
                b.Group(78, "300");
                b.Group(147, "1.0");
                b.Group(148, "0.0");
                b.Group(149, "0.0");
                b.Group(100, Marker);
                b.Group(1, layoutName);
                b.Group(70, "1");
                b.Group(71, (tab + 1).ToString(CultureInfo.InvariantCulture));
                b.Group(10, "-7.5");
                b.Group(20, "-20.0");
                b.Group(11, "412.5");
                b.Group(21, "277.0");

                foreach (int code in new[] { 12, 22, 32 })
                {
                    b.Group(code, "0.0");
                }

                foreach (int code in new[] { 14, 24, 34 })
                {
                    b.Group(code, "1E+20");
                }

                foreach (int code in new[] { 15, 25, 35 })
                {
                    b.Group(code, "-1E+20");
                }

                b.Group(146, "0.0");

                foreach (KeyValuePair<int, string> group in new[]
                {
                    Pair(13, "0.0"), Pair(23, "0.0"), Pair(33, "0.0"), Pair(16, "1.0"), Pair(26, "0.0"), Pair(36, "0.0"),
                    Pair(17, "0.0"), Pair(27, "1.0"), Pair(37, "0.0"), Pair(76, "1"),
                })
                {
                    b.Group(group.Key, group.Value);
                }

                if (owner != null)
                {
                    b.Group(330, owner);
                }
            }));

            file.SetValues(record, new[] { Pair(340, Handle) }, BlockRecordOrder, false);
            GeometryHelperLog.Info("DxfLayout: the layout " + layoutName + " was added on tab " + (tab + 1).ToString(CultureInfo.InvariantCulture) + ".");
        }

        /// <summary>
        /// Gives a layout of paper space the viewport that shows its sheet, as AutoCAD's <c>Initialize</c>: a VIEWPORT of
        /// <see cref="DxfViewport.Number"/> 1 in its block ahead of the viewports it has, on and stacked first (68 of 1), about
        /// the middle of its <see cref="Limits"/> and 1.1 times their size, showing the sheet at 1:1, as ezdxf makes the main
        /// viewport of a layout; the layout's last active viewport (group 331) where it names none. A layout that has its
        /// viewport 1 is given nothing.
        /// </summary>
        /// <remarks>
        /// A layout added as <see cref="AddToLayoutDictionary"/> adds one, of limits (-7.5, -20) to (412.5, 277), is given a
        /// viewport about (202.5, 128.5) 462 by 326.7, its view about the same point 326.7 high. Limits of no area are taken as
        /// the sheet from (0, 0) to its <see cref="DxfPlotSettings.PlotPaperSize"/>. The first VIEWPORT of a layout is the one
        /// AutoCAD and BricsCAD take for its sheet: a layout that has viewports and no viewport 1, as Layout1 given one 50 by 40
        /// turned on, has them moved after the new one, their handles kept, and those stacked on (68 of 1 or more) moved up one,
        /// the 50 by 40 from 1 to 2.
        /// </remarks>
        /// <returns>The viewport 1 of the layout, as AutoCAD gives its id: the one made, or the one it had.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the layout is in no drawing, is erased, or is Model, whose
        /// viewports are records of the VPORT table, or has no block.</exception>
        public DxfViewport Initialize()
        {
            DxfFile file = InFile("be initialized");
            DxfBlockTableRecord block = BlockTableRecord;

            if (ModelType || block == null || DxfBlockTableRecord.IsModelName(block.Name ?? string.Empty))
            {
                throw new InvalidOperationException("The layout " + LayoutName + " is Model, whose viewports are records of the VPORT table, or has no block: it is given no viewport 1.");
            }

            IReadOnlyList<DxfViewport> others = GetViewports();

            foreach (DxfViewport existing in others)
            {
                if (existing.Id == 1)
                {
                    return existing;
                }
            }

            DxfExtents2d limits = Limits;
            GeoPoint2 min = limits.MinPoint;
            GeoPoint2 max = limits.MaxPoint;

            if (!(max.X - min.X > 0.0) || !(max.Y - min.Y > 0.0))
            {
                min = new GeoPoint2(0.0, 0.0);
                max = new GeoPoint2(PlotPaperSize.X, PlotPaperSize.Y);
            }

            var center = new GeoPoint3((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, 0.0);
            double height = (max.Y - min.Y) * 11.0 / 10.0;
            var viewport = new DxfViewport { CenterPoint = center, Width = (max.X - min.X) * 11.0 / 10.0, Height = height, ViewHeight = height };
            viewport.ViewCenter = new GeoPoint2(center.X, center.Y);
            block.AppendEntity(viewport);
            viewport.MakeSheetViewport();

            // The first VIEWPORT of a layout is the one AutoCAD and BricsCAD take for its sheet: those the layout has are moved
            // after the viewport 1, in their order and with their handles, and each stacked on (68 of 1 or more) moves up one.
            foreach (DxfViewport other in others)
            {
                other.MoveUpInStack();
                other.Bind(file.Move(other.Item, viewport.Item.Space));
            }

            if (PartIndex(Marker, 331) < 0 && viewport.Handle != null)
            {
                file.SetValues(Item, new[] { Pair(331, viewport.Handle) }, Order, false);
            }

            return viewport;
        }

        /// <summary>
        /// Takes the layout's last active viewport (group 331 of AcDbLayout) off the viewport of a handle, erased, so that no
        /// pointer is left at it: to the layout's viewport 1, which shows the sheet itself, as AutoCAD 2021 points it there when
        /// the last active viewport is erased; out where the layout has no other viewport 1. Nothing where it names another, or
        /// none.
        /// </summary>
        internal void ForgetViewport(string handle)
        {
            int at = PartIndex(Marker, 331);

            if (at >= 0 && !IsErased && string.Equals(PartValue(Marker, 331)?.Trim(), handle, StringComparison.OrdinalIgnoreCase))
            {
                DxfViewport sheet = GetViewports().FirstOrDefault(v => v.Id == 1 && !v.IsErased && v.Handle != null && !string.Equals(v.Handle, handle, StringComparison.OrdinalIgnoreCase));

                if (sheet != null)
                {
                    Item.File.SetValueAt(Item, at, sheet.Handle);
                }
                else
                {
                    Item.File.RemoveGroupAt(Item, at);
                }
            }
        }

        /// <summary>Gives the layout as a line for debugging: its type, handle, name and tab.</summary>
        /// <returns>The line, such as <c>LAYOUT 1E Layout1, tab 1</c>.</returns>
        public override string ToString() => "LAYOUT " + (Handle ?? string.Empty) + " " + LayoutName + ", tab " + TabOrder.ToString(CultureInfo.InvariantCulture);

        private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>Whether a name is that of the block of a layout other than the one last shown, as AutoCAD names it: <c>*Paper_Space</c> and a number.</summary>
        private static bool IsNumberedPaperSpace(string name)
        {
            if (name.Length <= DxfBlockTableRecord.PaperSpace.Length || !name.StartsWith(DxfBlockTableRecord.PaperSpace, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = DxfBlockTableRecord.PaperSpace.Length; i < name.Length; i++)
            {
                if (name[i] < '0' || name[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
