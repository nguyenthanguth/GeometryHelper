using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The objects of the OBJECTS section AutoCAD's <c>Database</c> reaches by their ids: the named object dictionary and the
    /// dictionaries named in it, the layouts, and the properties of the drawing (DWGPROPS).
    /// </summary>
    public sealed partial class DxfDatabase
    {
        private Dictionary<string, DxfItem> _entities;
        private int _entitiesAt = -1;

        /// <summary>
        /// Gets the named object dictionary, as AutoCAD's <c>NamedObjectsDictionaryId</c> names it: the first DICTIONARY of the
        /// OBJECTS section, which names every other dictionary of the drawing (ACAD_GROUP, ACAD_LAYOUT and on); null in a file
        /// of R12, or one with no OBJECTS section.
        /// </summary>
        public DxfDBDictionary NamedObjectsDictionary
        {
            get
            {
                DxfItem root = File.HoldsObjects ? File.ObjectItem(File.RootDictionaryHandle()) : null;
                return root == null ? null : DxfDBObject.OfObject(root) as DxfDBDictionary;
            }
        }

        /// <summary>Gets the dictionary of groups (ACAD_GROUP), as AutoCAD's <c>GroupDictionaryId</c> names it, each a <see cref="DxfGroup"/> under its name; null where the drawing has none.</summary>
        public DxfDBDictionary GroupDictionary => Named("ACAD_GROUP");

        /// <summary>Gets the dictionary of layouts (ACAD_LAYOUT), as AutoCAD's <c>LayoutDictionaryId</c> names it, each a <see cref="DxfLayout"/> under its name; null where the drawing has none.</summary>
        public DxfDBDictionary LayoutDictionary => Named("ACAD_LAYOUT");

        /// <summary>Gets the dictionary of multileader styles (ACAD_MLEADERSTYLE), as AutoCAD's <c>MLeaderStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary MLeaderStyleDictionary => Named("ACAD_MLEADERSTYLE");

        /// <summary>Gets the dictionary of table styles (ACAD_TABLESTYLE), as AutoCAD's <c>TableStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary TableStyleDictionary => Named("ACAD_TABLESTYLE");

        /// <summary>Gets the dictionary of multiline styles (ACAD_MLINESTYLE), as AutoCAD's <c>MLStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary MLStyleDictionary => Named("ACAD_MLINESTYLE");

        /// <summary>Gets the dictionary of materials (ACAD_MATERIAL), as AutoCAD's <c>MaterialDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary MaterialDictionary => Named("ACAD_MATERIAL");

        /// <summary>Gets the dictionary of page setups (ACAD_PLOTSETTINGS), as AutoCAD's <c>PlotSettingsDictionaryId</c> names it, each a <see cref="DxfPlotSettings"/>; null where the drawing has none.</summary>
        public DxfDBDictionary PlotSettingsDictionary => Named("ACAD_PLOTSETTINGS");

        /// <summary>Gets the dictionary of plot style names (ACAD_PLOTSTYLENAME), as AutoCAD's <c>PlotStyleNameDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary PlotStyleNameDictionary => Named("ACAD_PLOTSTYLENAME");

        /// <summary>Gets the dictionary of colours of colour books (ACAD_COLOR), as AutoCAD's <c>ColorDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary ColorDictionary => Named("ACAD_COLOR");

        /// <summary>Gets the dictionary of visual styles (ACAD_VISUALSTYLE), as AutoCAD's <c>VisualStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary VisualStyleDictionary => Named("ACAD_VISUALSTYLE");

        /// <summary>Gets the dictionary of detail view styles (ACAD_DETAILVIEWSTYLE), as AutoCAD's <c>DetailViewStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary DetailViewStyleDictionary => Named("ACAD_DETAILVIEWSTYLE");

        /// <summary>Gets the dictionary of section view styles (ACAD_SECTIONVIEWSTYLE), as AutoCAD's <c>SectionViewStyleDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary SectionViewStyleDictionary => Named("ACAD_SECTIONVIEWSTYLE");

        /// <summary>Gets the dictionary of data links (ACAD_DATALINK), as AutoCAD's <c>DataLinkDictionaryId</c> names it; null where the drawing has none.</summary>
        public DxfDBDictionary DataLinkDictionary => Named("ACAD_DATALINK");

        /// <summary>
        /// Gets the layouts of the drawing, the LAYOUTs of its layout dictionary in the order of their tabs (group 71), Model
        /// first, as AutoCAD's layout tabs stand; those of one tab number by name, then by handle. Empty for a file of R12, which
        /// has none.
        /// </summary>
        /// <remarks>AutoCAD reaches them through its <c>LayoutManager</c> and <c>LayoutDictionaryId</c>; here they are a list.</remarks>
        public IReadOnlyList<DxfLayout> Layouts
        {
            get
            {
                var layouts = new List<DxfLayout>();
                DxfDBDictionary dictionary = LayoutDictionary;

                if (dictionary != null)
                {
                    foreach (DxfDBDictionaryEntry entry in dictionary)
                    {
                        if (entry.Value is DxfLayout layout)
                        {
                            layouts.Add(layout);
                        }
                    }
                }

                layouts.Sort((a, b) =>
                {
                    int order = a.TabOrder.CompareTo(b.TabOrder);
                    order = order != 0 ? order : string.CompareOrdinal(a.LayoutName, b.LayoutName);
                    return order != 0 ? order : CompareHandles(a.Handle, b.Handle);
                });

                return layouts.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets or sets the properties of the drawing, as AutoCAD's <c>SummaryInfo</c>: those of its DWGPROPS, the XRECORD of
        /// the named object dictionary AutoCAD keeps them in, its title (group 2), subject (3), author (4), comments (6),
        /// keywords (7), who saved it last (8), its revision (9), the base of its hyperlinks (the group 1 after its dates) and its
        /// custom properties: each <c>name=value</c> in a group 300 to 309, then those of the list after the group 90 that
        /// follows the base of the hyperlinks, its count, each a name and a value in two groups 1. Empty where the drawing has none.
        /// </summary>
        /// <value>
        /// <para>
        /// Set, the DWGPROPS written anew, as AutoCAD writes it: its marker <c>DWGPROPS COOKIE</c> (1), the texts, the ten
        /// groups 300 to 309, the time spent on it and the dates it was made and saved (40, 41, 42, those of a DWGPROPS kept, else
        /// the header's <c>$TDINDWG</c>, <c>$TDCREATE</c> and <c>$TDUPDATE</c>), and the base of its hyperlinks; the groups after
        /// those kept as they were. The custom properties are written in the form the record has: in the list after a group 90,
        /// the ten groups each <c>=</c>, where it has that list, or is made new in a drawing of R2004 or later; else in the ten
        /// groups, <c>=</c> where there is none. A record never holds two sets of them. A drawing with none is given one, owned
        /// by the named object dictionary. Properties the same as the drawing has change nothing.
        /// </para>
        /// <para>
        /// <c>db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-12" }.ToDatabaseSummaryInfo()</c>
        /// gives the drawing its title, its other properties kept.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the properties set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the properties set have more than ten custom properties for a record of
        /// the ten groups, or a custom property's name has an equals sign in it there.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file of R12, or one with no named object dictionary.</exception>
        public DxfDatabaseSummaryInfo SummaryInfo
        {
            get
            {
                DxfXrecord properties = DwgProps();
                return properties == null ? DxfDatabaseSummaryInfo.Empty : DxfDatabaseSummaryInfo.Read(properties.Data);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Equals(SummaryInfo))
                {
                    return;
                }

                DxfDBDictionary root = NamedObjectsDictionary ?? throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no named object dictionary to hold DWGPROPS: R13 (AC1012) brought it.");
                DxfXrecord properties = DwgProps();
                bool list = properties != null ? DxfDatabaseSummaryInfo.ListAt(properties.Data, out _) >= 0 : File.Supports(1018);

                if (!list && value.CustomProperties.Count > 10)
                {
                    throw new ArgumentException("A DWGPROPS of the groups 300 to 309 holds ten custom properties, and " + value.CustomProperties.Count.ToString(CultureInfo.InvariantCulture) + " are given.", nameof(value));
                }

                foreach (KeyValuePair<string, string> property in list ? new List<KeyValuePair<string, string>>() : value.CustomProperties)
                {
                    if (property.Key.IndexOf('=') >= 0)
                    {
                        throw new ArgumentException("The name of a custom property has no equals sign in it, which ends it in DWGPROPS: '" + property.Key + "' has one.", nameof(value));
                    }
                }

                List<DxfTypedValue> data = value.Written(properties?.Data, Header("$TDINDWG"), Header("$TDCREATE"), Header("$TDUPDATE"), list);

                if (properties != null)
                {
                    properties.Data = data;
                    return;
                }

                root.SetAt("DWGPROPS", new DxfXrecord { Data = data });
            }
        }

        /// <summary>
        /// The entity of a handle among those of model space, paper space and every block, letters of either case the same; null
        /// where the drawing has none of that handle, or it is erased. The entities are found by handle in one walk of the
        /// drawing, and those appended since are taken in as they are asked for: an entity appended between two questions costs
        /// no walk, so that a loop that appends to a drawing and to a group stays linear. A handle found erased is looked for
        /// again by a walk, once for each change of the records, as an entity converted keeps its handle in a record of its own.
        /// </summary>
        internal DxfEntity EntityByHandle(string handle)
        {
            DxfItem item = ItemByHandle(handle);
            return item == null ? null : DxfEntity.Of(item);
        }

        /// <summary>The record of the entity of a handle, as <see cref="EntityByHandle"/> finds it; null where there is none.</summary>
        internal DxfItem ItemByHandle(string handle)
        {
            if (handle == null)
            {
                return null;
            }

            string key = handle.Trim();

            if (_entities == null)
            {
                IndexEntities();
            }
            else
            {
                foreach (DxfItem joined in File.TakeJoined())
                {
                    string own = joined.Handle;

                    if (own != null)
                    {
                        _entities[own] = joined;
                    }
                }
            }

            if (!_entities.TryGetValue(key, out DxfItem found))
            {
                return null;
            }

            if (!found.IsRemoved)
            {
                return found;
            }

            if (_entitiesAt == File.EditCount)
            {
                return null;
            }

            IndexEntities();
            return _entities.TryGetValue(key, out found) && !found.IsRemoved ? found : null;
        }

        /// <summary>Finds every entity of the drawing by its handle, in one walk; the first of a handle kept.</summary>
        private void IndexEntities()
        {
            File.TakeJoined();
            var entities = new Dictionary<string, DxfItem>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                foreach (DxfEntity entity in block.AllEntities())
                {
                    string own = entity.Handle;

                    if (own != null && !entities.ContainsKey(own))
                    {
                        entities.Add(own, entity.Item);
                    }
                }
            }

            _entities = entities;
            _entitiesAt = File.EditCount;
        }

        /// <summary>The dictionary of a name in the named object dictionary; null where there is none.</summary>
        private DxfDBDictionary Named(string name)
        {
            DxfDBDictionary root = NamedObjectsDictionary;
            return root != null && root.Contains(name) ? root.GetAt(name) as DxfDBDictionary : null;
        }

        /// <summary>The XRECORD DWGPROPS of the named object dictionary; null where there is none.</summary>
        private DxfXrecord DwgProps()
        {
            DxfDBDictionary root = NamedObjectsDictionary;
            return root != null && root.Contains("DWGPROPS") ? root.GetAt("DWGPROPS") as DxfXrecord : null;
        }

        /// <summary>The value of the first group of a variable of the header; null where it has none.</summary>
        private string Header(string name)
        {
            IReadOnlyList<DxfTypedValue> groups = File.HeaderOf(name);
            return groups == null || groups.Count == 0 ? null : groups[0].Value.Trim();
        }

        /// <summary>Two handles in the order of their numbers, one that is none or not hexadecimal last.</summary>
        internal static int CompareHandles(string a, string b)
        {
            bool hasA = ulong.TryParse(a, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong x);
            bool hasB = ulong.TryParse(b, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong y);

            if (hasA != hasB)
            {
                return hasA ? -1 : 1;
            }

            return hasA ? x.CompareTo(y) : string.CompareOrdinal(a, b);
        }
    }
}
