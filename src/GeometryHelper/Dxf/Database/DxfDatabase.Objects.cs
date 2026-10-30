using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The objects of the OBJECTS section AutoCAD's <c>Database</c> reaches by their ids: the named object dictionary and the
    /// dictionaries named in it, the layouts, and the properties of the drawing (DWGPROPS).
    /// </summary>
    public sealed partial class DxfDatabase
    {
        private Dictionary<string, DxfItem> _entities;
        private int _entitiesAt = -1;

        // The records of the tables and the tables (their objects), and the records that follow an entity (their records), by
        // handle, as at an edit count.
        private Dictionary<string, object> _others;
        private int _othersAt = -1;

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
        /// Gets the viewports of the layout the drawing shows, as AutoCAD's <c>GetViewports</c> gives their ids: where it shows
        /// paper space (<see cref="TileMode"/> false), the VIEWPORTs of <see cref="PaperSpace"/> in the order of the file, the one
        /// of <see cref="DxfViewport.Number"/> 1 that shows the sheet itself among them, as its layout's
        /// <see cref="DxfLayout.GetViewports"/> gives them. A drawing ezdxf made, shown in paper space with a viewport 1 and one
        /// 200 by 150 on its sheet, gives the two.
        /// </summary>
        /// <remarks>
        /// Where the drawing shows model space, or the viewports of paper space are not asked for, none: the viewports AutoCAD gives
        /// then are the records of the VPORT table that tile model space, which the library has no class for.
        /// </remarks>
        /// <param name="getPaperspaceVports">Whether the viewports of paper space are asked for, as AutoCAD's
        /// <c>bGetPaperspaceVports</c>.</param>
        /// <returns>The viewports, a new list.</returns>
        public IReadOnlyList<DxfViewport> GetViewports(bool getPaperspaceVports)
        {
            var viewports = new List<DxfViewport>();

            if (getPaperspaceVports && !TileMode)
            {
                foreach (DxfEntity entity in PaperSpace)
                {
                    if (entity is DxfViewport viewport)
                    {
                        viewports.Add(viewport);
                    }
                }
            }

            return viewports.AsReadOnly();
        }

        /// <summary>
        /// Gets or sets the properties of the drawing, as AutoCAD's <c>SummaryInfo</c>: those of its DWGPROPS, the XRECORD of
        /// the named object dictionary AutoCAD keeps them in, its title (group 2), subject (3), author (4), comments (6),
        /// keywords (7), who saved it last (8), its revision (9), the base of its hyperlinks (the group 1 after its dates) and its
        /// custom properties: each <c>name=value</c> in a group 300 to 309, then those of a list after the group 90 that follows
        /// the base of the hyperlinks, where one follows it, each a name and a value in two groups 1. The base of the hyperlinks
        /// is the header's <c>$HYPERLINKBASE</c> where that is not empty, as AutoCAD 2021 reads it there. A drawing with no
        /// DWGPROPS, as AutoCAD saves one of R2004 or later, gives the texts of its header, <c>$TITLE</c>, <c>$SUBJECT</c>,
        /// <c>$AUTHOR</c>, <c>$COMMENTS</c>, <c>$KEYWORDS</c> and <c>$REVISIONNUMBER</c>, and its custom properties, each the
        /// value of a <c>$CUSTOMPROPERTYTAG</c> and of the <c>$CUSTOMPROPERTY</c> after it, in their order: AutoCAD 2021 saved a
        /// drawing of R2018 titled Plate B-12, of the custom properties Project and Checked by, with <c>$TITLE</c>,
        /// <c>$AUTHOR</c> and two such pairs after <c>$DWGCODEPAGE</c>, and no DWGPROPS. Empty where the drawing has none.
        /// </summary>
        /// <value>
        /// <para>
        /// Set in a drawing of R2004 (AC1018) or later, written in its header as AutoCAD 2021 writes a drawing of R2018: each
        /// text not empty in its variable, <c>$TITLE</c>, <c>$SUBJECT</c>, <c>$AUTHOR</c>, <c>$KEYWORDS</c>, <c>$COMMENTS</c>
        /// and <c>$REVISIONNUMBER</c>, then a <c>$CUSTOMPROPERTYTAG</c> and a <c>$CUSTOMPROPERTY</c> for each custom property, as
        /// many as there are, after <c>$DWGCODEPAGE</c>, the header's <c>$LASTSAVEDBY</c> moved among them before
        /// <c>$REVISIONNUMBER</c>, its value kept, as AutoCAD 2021 wrote the header of R2004, R2010 and R2018; an empty text has
        /// no variable, and the base of the hyperlinks goes to <c>$HYPERLINKBASE</c>. A DWGPROPS the drawing has is taken out,
        /// as AutoCAD 2021 saved one of R2018 read with a DWGPROPS without it; who saved it last is not written but kept.
        /// </para>
        /// <para>
        /// Set in a drawing of R13 to R2000, the DWGPROPS written anew, as AutoCAD 2021 writes one of R2000 and reads it back:
        /// its marker <c>DWGPROPS COOKIE</c> (1), the texts, the custom properties in the ten groups 300 to 309 (<c>=</c> where
        /// there is none), the time spent on it and the dates it was made and saved (40, 41, 42, those of a DWGPROPS kept, else
        /// the header's <c>$TDINDWG</c>, <c>$TDCREATE</c> and <c>$TDUPDATE</c>), the base of its hyperlinks and the count of the
        /// custom properties (90); the groups after those kept as they were. A list of them after the group 90, which AutoCAD
        /// 2021 does not read, is written no more. The base of the hyperlinks is written to the header's
        /// <c>$HYPERLINKBASE</c> too, in a drawing of R2000, and each text the header holds already is set there as well, so
        /// that the two agree. A drawing with none is given one, owned by the named object dictionary. Properties the same as
        /// the drawing has change nothing, in a drawing of any version.
        /// </para>
        /// <para>
        /// <c>db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-12" }.ToDatabaseSummaryInfo()</c>
        /// gives the drawing its title, its other properties kept.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the properties set are null.</exception>
        /// <exception cref="ArgumentException">Thrown when set in a drawing older than R2004 with more than ten custom
        /// properties, or one whose name has an equals sign in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file of R12, or one older than R2004 with no named
        /// object dictionary.</exception>
        public DxfDatabaseSummaryInfo SummaryInfo
        {
            get
            {
                DxfXrecord properties = DwgProps();
                string hyperlinkBase = HyperlinkBase;

                if (properties == null)
                {
                    string[] texts = HeaderTexts.Select(name => name == null || File.HeaderOf(name) == null ? null : TextOf(name, 1, string.Empty)).ToArray();
                    List<KeyValuePair<string, string>> custom = HeaderCustomProperties();
                    return texts.All(t => t == null) && hyperlinkBase.Length == 0 && custom.Count == 0
                        ? DxfDatabaseSummaryInfo.Empty
                        : new DxfDatabaseSummaryInfo(texts, hyperlinkBase, custom);
                }

                DxfDatabaseSummaryInfo read = DxfDatabaseSummaryInfo.Read(properties.Data);
                return hyperlinkBase.Length == 0 ? read : read.WithHyperlinkBase(hyperlinkBase);
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

                if (File.Supports(1018))
                {
                    WriteSummaryToHeader(value);
                    return;
                }

                DxfDBDictionary root = NamedObjectsDictionary ?? throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no named object dictionary to hold DWGPROPS: R13 (AC1012) brought it.");
                DxfXrecord properties = DwgProps();

                if (value.CustomProperties.Count > 10)
                {
                    throw new ArgumentException("A DWGPROPS of the groups 300 to 309 holds ten custom properties, and " + value.CustomProperties.Count.ToString(CultureInfo.InvariantCulture) + " are given.", nameof(value));
                }

                foreach (KeyValuePair<string, string> property in value.CustomProperties)
                {
                    if (property.Key.IndexOf('=') >= 0)
                    {
                        throw new ArgumentException("The name of a custom property has no equals sign in it, which ends it in DWGPROPS: '" + property.Key + "' has one.", nameof(value));
                    }
                }

                List<DxfTypedValue> data = value.Written(properties?.Data, Header("$TDINDWG"), Header("$TDCREATE"), Header("$TDUPDATE"));

                if (properties != null)
                {
                    properties.Data = data;
                }
                else
                {
                    root.SetAt("DWGPROPS", new DxfXrecord { Data = data });
                }

                // The header's base of the hyperlinks, which AutoCAD reads, and each text a drawing AutoCAD saved holds there.
                if (File.Supports(1015))
                {
                    HyperlinkBase = value.HyperlinkBase;
                }

                string[] now = value.Texts;

                for (int i = 0; i < HeaderTexts.Length; i++)
                {
                    if (HeaderTexts[i] != null && File.HeaderOf(HeaderTexts[i]) != null)
                    {
                        PutText(HeaderTexts[i], 1, now[i], TextOf(HeaderTexts[i], 1, string.Empty), 1018);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the object of a handle, as AutoCAD's <c>GetObjectId(false, handle, 0)</c> gives its id: an entity of model space,
        /// paper space or a block (a VERTEX of a POLYLINE and an ATTRIB of an INSERT among them), an object of the OBJECTS section,
        /// a record of the layer, linetype, text style, dimension style or block table, or one of those five tables; letters of
        /// either case the same. <c>db.GetObject("2F")</c> of a drawing whose layer steel has the handle 2F gives that
        /// <see cref="DxfLayerTableRecord"/>.
        /// </summary>
        /// <remarks>
        /// The library has no class for the other records that carry a handle, and finds none of them: the tables of
        /// applications (APPID), viewports (VPORT), views (VIEW) and coordinate systems (UCS) and their records, and the BLOCK
        /// and ENDBLK that open and close a block. The objects are found by handle in one walk of the drawing, kept until the
        /// drawing next changes, so that many questions between two changes cost one walk.
        /// </remarks>
        /// <param name="handle">The handle, as the file writes it, hexadecimal.</param>
        /// <returns>The object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the drawing has no object of the handle, or it is erased, as AutoCAD
        /// answers eUnknownHandle.</exception>
        public DxfDBObject GetObject(string handle)
        {
            if (!TryGetObject(handle, out DxfDBObject found))
            {
                throw new KeyNotFoundException("The drawing has no object of the handle " + handle + ".");
            }

            return found;
        }

        /// <summary>
        /// Gets the object of a handle, as <see cref="GetObject"/> finds it; false, with null, where the drawing has none of the
        /// handle, the handle is not hexadecimal, or the object is erased.
        /// </summary>
        /// <param name="handle">The handle, hexadecimal.</param>
        /// <param name="value">The object; null where there is none.</param>
        /// <returns>Whether there is one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handle is null.</exception>
        public bool TryGetObject(string handle, out DxfDBObject value)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            value = null;
            string key = handle.Trim();

            if (!ulong.TryParse(key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong number) || number == 0)
            {
                return false;
            }

            value = (DxfDBObject)EntityByHandle(key) ?? (File.HoldsObjects && File.ObjectItem(key) is DxfItem item ? DxfDBObject.OfObject(item) : null)
                ?? OtherByHandle(key);

            if (value != null && value.IsErased)
            {
                value = null;
            }

            return value != null;
        }

        /// <summary>
        /// Gets how many objects the drawing holds, as AutoCAD's <c>ApproxNumObjects</c>: those <see cref="GetObject"/> finds by
        /// handle, the entities of every space and block with their vertices and attributes, the objects of the OBJECTS section,
        /// and the five tables it reads with their records; those erased left out. AutoCAD counts the records of the tables the
        /// library has no class for as well (APPID, VPORT, VIEW, UCS) and the BLOCK and ENDBLK of each block, so its number is
        /// somewhat higher. A drawing of R12 that writes no handles counts none. The drawing is walked once for each question.
        /// </summary>
        public int ApproxNumObjects
        {
            get
            {
                IndexEntities();

                if (_others == null || _othersAt != File.EditCount)
                {
                    IndexOthers();
                }

                int count = _others.Count + (File.HoldsObjects ? File.ObjectCount() : 0);

                foreach (DxfItem entity in _entities.Values)
                {
                    if (!entity.IsRemoved)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// A record of a table of the drawing, or the table, or a record that follows an entity (a VERTEX or an ATTRIB), of a
        /// handle; null where there is none. They are found by handle in one walk, made again once the records change.
        /// </summary>
        private DxfDBObject OtherByHandle(string handle)
        {
            if (_others == null || _othersAt != File.EditCount)
            {
                IndexOthers();
            }

            return !_others.TryGetValue(handle, out object found) ? null : found as DxfDBObject ?? DxfEntity.Of((DxfItem)found);
        }

        /// <summary>
        /// Finds the records of the tables, the tables and the records that follow an entity by their handles, in one walk: the
        /// tables in the order layer, linetype, text style, dimension style and block, each before its records, then the
        /// followers; the first of a handle kept.
        /// </summary>
        private void IndexOthers()
        {
            var others = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            void Keep(string own, object found)
            {
                if (own != null && !others.ContainsKey(own))
                {
                    others.Add(own, found);
                }
            }

            foreach (DxfSymbolTable table in new DxfSymbolTable[] { LayerTable, LinetypeTable, TextStyleTable, DimStyleTable, BlockTable })
            {
                Keep(table.Handle, table);

                foreach (object record in table)
                {
                    if (record is DxfDBObject found)
                    {
                        Keep(found.Handle, found);
                    }
                }
            }

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                foreach (DxfEntity entity in block.AllEntities())
                {
                    int count = File.RecordCount(entity.Item);

                    for (int r = 1; r < count; r++)
                    {
                        DxfItem follower = File.FollowerOf(entity.Item, r);
                        Keep(follower.Handle, follower);
                    }
                }
            }

            _others = others;
            _othersAt = File.EditCount;
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

        /// <summary>
        /// The variables of the header AutoCAD writes the texts of the drawing's properties in from R2004 on, in the order of the
        /// texts of <see cref="DxfDatabaseSummaryInfo"/>: title, subject, author, comments, keywords, revision. Who saved it last
        /// (<c>$LASTSAVEDBY</c>) is none of them here: ezdxf writes its own name there in every drawing, and AutoCAD 2021 does
        /// not carry that into the properties it saves.
        /// </summary>
        private static readonly string[] HeaderTexts = { "$TITLE", "$SUBJECT", "$AUTHOR", "$COMMENTS", "$KEYWORDS", null, "$REVISIONNUMBER" };

        /// <summary>
        /// The texts of <see cref="HeaderTexts"/> in the order the header of R2004 or later is given them, by their place among
        /// the texts of <see cref="DxfDatabaseSummaryInfo"/>: title, subject, author, keywords, comments, revision, the order
        /// of the properties in a drawing of R2004.
        /// </summary>
        private static readonly int[] HeaderTextOrder = { 0, 1, 2, 4, 3, 6 };

        /// <summary>The variables of the header the properties of a drawing of R2004 or later are written in, each taken out before they are.</summary>
        private static readonly string[] SummaryVariables = { "$TITLE", "$SUBJECT", "$AUTHOR", "$KEYWORDS", "$COMMENTS", "$REVISIONNUMBER", "$CUSTOMPROPERTYTAG", "$CUSTOMPROPERTY" };

        /// <summary>
        /// The custom properties of the header, each the value of a <c>$CUSTOMPROPERTYTAG</c> and of the
        /// <c>$CUSTOMPROPERTY</c> after it, in the order of the file; a tag with no value after it has an empty one.
        /// </summary>
        private List<KeyValuePair<string, string>> HeaderCustomProperties()
        {
            var custom = new List<KeyValuePair<string, string>>();
            string tag = null;

            foreach (KeyValuePair<string, IReadOnlyList<DxfTypedValue>> variable in File.HeaderInOrder())
            {
                string text = variable.Value.Count > 0 ? DxfReader.Decode(variable.Value[0].Value ?? string.Empty) : string.Empty;

                if (variable.Key == "$CUSTOMPROPERTYTAG")
                {
                    if (tag != null)
                    {
                        custom.Add(new KeyValuePair<string, string>(tag, string.Empty));
                    }

                    tag = text;
                }
                else if (variable.Key == "$CUSTOMPROPERTY" && tag != null)
                {
                    custom.Add(new KeyValuePair<string, string>(tag, text));
                    tag = null;
                }
            }

            if (tag != null)
            {
                custom.Add(new KeyValuePair<string, string>(tag, string.Empty));
            }

            return custom;
        }

        /// <summary>
        /// Writes the properties of a drawing of R2004 or later in its header, as <see cref="SummaryInfo"/> says, and takes out
        /// the DWGPROPS it has.
        /// </summary>
        private void WriteSummaryToHeader(DxfDatabaseSummaryInfo value)
        {
            string[] texts = value.Texts;
            var run = new List<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>>();
            IReadOnlyList<DxfTypedValue> saved = File.HeaderOf("$LASTSAVEDBY");

            foreach (int i in HeaderTextOrder)
            {
                // Who saved it last stands before the revision, as AutoCAD 2021 writes the header, its value kept.
                if (i == 6 && saved != null)
                {
                    run.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>("$LASTSAVEDBY", saved));
                }

                if (texts[i].Length > 0)
                {
                    run.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>(HeaderTexts[i], new[] { new DxfTypedValue(1, texts[i]) }));
                }
            }

            foreach (KeyValuePair<string, string> property in value.CustomProperties)
            {
                run.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>("$CUSTOMPROPERTYTAG", new[] { new DxfTypedValue(1, property.Key) }));
                run.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>("$CUSTOMPROPERTY", new[] { new DxfTypedValue(1, property.Value) }));
            }

            File.SetHeaderRun(saved != null ? SummaryVariables.Concat(new[] { "$LASTSAVEDBY" }).ToArray() : SummaryVariables, run, new[] { "$DWGCODEPAGE", "$LASTSAVEDBY" });
            HyperlinkBase = value.HyperlinkBase;
            DxfDBDictionary root = NamedObjectsDictionary;

            if (root != null && root.Contains("DWGPROPS"))
            {
                root.Remove("DWGPROPS");
            }
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
