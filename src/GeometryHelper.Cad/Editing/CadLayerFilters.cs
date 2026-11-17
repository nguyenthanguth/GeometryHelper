using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>What kind of filter a layer filter of the drawing is.</summary>
    public enum CadLayerFilterKind
    {
        /// <summary>A property filter: the layers whose properties pass its <see cref="CadLayerFilter.Expression"/>.</summary>
        Property,

        /// <summary>A group filter: the layers it names.</summary>
        Group,
    }

    /// <summary>
    /// What a layer filter is tested against: the viewport VP Freeze looks at, and the plot styles and the layers in use of the
    /// drawing. The layers in use are worked out by the first rule that asks for them, once, and kept until the drawing changes.
    /// </summary>
    public sealed class CadLayerFilterContext
    {
        private readonly DxfDatabase _database;

        /// <summary>Makes a context.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="viewport">The viewport VPFROZEN is read in; null for none, where it reads False.</param>
        public CadLayerFilterContext(DxfDatabase database, DxfViewport viewport = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            Viewport = viewport;
        }

        /// <summary>Gets the viewport VPFROZEN is read in; null for none.</summary>
        public DxfViewport Viewport { get; }

        /// <summary>
        /// The plot style a layer has: in a drawing plotted by colour Color_ and its colour's index, as AutoCAD names the plot
        /// style of a layer there (Color_30); else its entry of the plot style names, Normal where it names none.
        /// </summary>
        internal string PlotStyleOf(DxfLayerTableRecord layer)
        {
            if (_database.PlotStyleMode)
            {
                return "Color_" + Math.Abs(layer.ColorIndex).ToString(CultureInfo.InvariantCulture);
            }

            string name = layer.PlotStyleName;
            return name.Length == 0 ? "Normal" : name;
        }

        internal bool IsUsed(DxfLayerTableRecord layer) => layer.IsUsed;
    }

    /// <summary>
    /// A layer filter the drawing keeps, as AutoCAD 2021 keeps it in ACLYDICTIONARY of the layer table's extension dictionary:
    /// an XRECORD of its name (group 300) and its expression (301) for a property filter, or its layers (a 330 each) for a group
    /// filter, the filter nested in it in an ACLYDICTIONARY of its own XRECORD's extension dictionary.
    /// </summary>
    public sealed class CadLayerFilter
    {
        private readonly List<CadLayerFilter> _children = new List<CadLayerFilter>();
        private readonly List<string> _layerHandles = new List<string>();
        private readonly List<string> _layerNames = new List<string>();

        internal CadLayerFilter(string name, CadLayerFilterKind kind, CadLayerFilter parent)
        {
            Name = name;
            Kind = kind;
            Parent = parent;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is a property filter or a group filter.</summary>
        public CadLayerFilterKind Kind { get; }

        /// <summary>Gets the filter it is nested in; null for one at the top, under All.</summary>
        public CadLayerFilter Parent { get; }

        /// <summary>Gets the filters nested in it, by name.</summary>
        public IReadOnlyList<CadLayerFilter> Children => _children;

        /// <summary>Gets the expression of a property filter as the drawing keeps it; empty for a group filter.</summary>
        public string Expression { get; internal set; } = string.Empty;

        /// <summary>Gets the names of the layers of a group filter, by name; empty for a property filter.</summary>
        public IReadOnlyList<string> LayerNames => _layerNames;

        /// <summary>Gets whether it is the filter AutoCAD marks as the current one (group 290), the one the layer list shows.</summary>
        public bool IsCurrent { get; internal set; }

        /// <summary>Gets whether only ACAD_LAYERFILTERS, the dictionary of AutoCAD 2004 and earlier, keeps it.</summary>
        public bool IsLegacy { get; internal set; }

        /// <summary>Gets the names from the top to this filter, as <c>Steel</c>, <c>Beams</c>.</summary>
        public IReadOnlyList<string> Path
        {
            get
            {
                var path = new List<string>();

                for (CadLayerFilter filter = this; filter != null; filter = filter.Parent)
                {
                    path.Insert(0, filter.Name);
                }

                return path;
            }
        }

        /// <summary>Gives the filter as a line for debugging: its path.</summary>
        /// <returns>The path joined by a slash, as <c>Steel/Beams</c>.</returns>
        public override string ToString() => string.Join("/", Path);

        internal List<string> Handles => _layerHandles;

        internal List<string> NamesMutable => _layerNames;

        internal List<CadLayerFilter> ChildrenMutable => _children;
    }

    /// <summary>
    /// The layer filters of a drawing, read and edited as AutoCAD 2021 keeps them: property filters and group filters nested
    /// under All, the three AutoCAD makes (All Used Layers, Unreconciled New Layers, Viewport Overrides) not kept in the drawing. A
    /// filter is added, changed, renamed, deleted, made current and given layers, each one undo step; a name that is a
    /// sibling's already is refused.
    /// </summary>
    public static class CadLayerFilters
    {
        private const string Dictionary = "ACLYDICTIONARY";
        private const string Legacy = "ACAD_LAYERFILTERS";
        private const string PropertyMark = "AcLyLayerFilter";
        private const string GroupMark = "AcLyLayerGroup";

        /// <summary>The filters AutoCAD makes, listed after those the drawing keeps; each tests one property.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Dynamic = new[]
        {
            new KeyValuePair<string, string>("All Used Layers", "USED==\"True\""),
            new KeyValuePair<string, string>("Unreconciled New Layers", "UNRECONCILED==\"True\""),
            new KeyValuePair<string, string>("Viewport Overrides", "VPOVERRIDES==\"True\""),
        };

        /// <summary>
        /// Reads the filters of the drawing: those of ACLYDICTIONARY with the filters nested in them, then those only
        /// ACAD_LAYERFILTERS keeps (marked <see cref="CadLayerFilter.IsLegacy"/>, their expression the one of the record's extended
        /// data), each level in the order of the names, letters of either case alike.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The filters under All.</returns>
        public static IReadOnlyList<CadLayerFilter> Read(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var top = new List<CadLayerFilter>();
            DxfDBDictionary kept = KeptIn(database.LayerTable, false);

            if (kept != null)
            {
                ReadLevel(database, kept, null, top);
            }

            DxfDBDictionary extension = database.LayerTable.ExtensionDictionary;

            if (extension != null && extension.Contains(Legacy) && extension.GetAt(Legacy) is DxfDBDictionary old)
            {
                foreach (DxfDBDictionaryEntry entry in old)
                {
                    if (top.Any(f => Same(f.Name, entry.Key)) || entry.Value == null)
                    {
                        continue;
                    }

                    IReadOnlyList<DxfTypedValue> data = entry.Value.GetXDataForApplication("ACAD");
                    string expression = data?.Where(g => g.TypeCode == 1000).Select(g => g.Value).FirstOrDefault();

                    if (expression != null && CadLayerExpression.TryParse(expression, out _))
                    {
                        top.Add(
                            new CadLayerFilter(entry.Key, CadLayerFilterKind.Property, null)
                            {
                                Expression = expression,
                                IsLegacy = true,
                            }
                        );
                    }
                }
            }

            Sort(top);
            return top;
        }

        /// <summary>Finds a filter by its names from the top, letters of either case the same.</summary>
        /// <param name="filters">The filters under All.</param>
        /// <param name="path">The names, as <c>Steel</c>, <c>Beams</c>.</param>
        /// <returns>The filter; null where there is none.</returns>
        public static CadLayerFilter Find(IReadOnlyList<CadLayerFilter> filters, IReadOnlyList<string> path)
        {
            CadLayerFilter found = null;
            IReadOnlyList<CadLayerFilter> level = filters;

            foreach (string name in path ?? Array.Empty<string>())
            {
                found = level?.FirstOrDefault(f => Same(f.Name, name));

                if (found == null)
                {
                    return null;
                }

                level = found.Children;
            }

            return found;
        }

        /// <summary>
        /// Whether a layer passes a filter: the layer has to pass every filter above it and the filter itself, so that a filter nested
        /// in Steel by COLOR=="1" shows the red layers of the steel ones. A property filter reads its expression, a group filter its
        /// layers.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <param name="layer">The layer.</param>
        /// <param name="context">What the layer is read against.</param>
        /// <returns>True when it passes.</returns>
        public static bool Passes(CadLayerFilter filter, DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            for (CadLayerFilter each = filter; each != null; each = each.Parent)
            {
                if (!PassesOne(each, layer, context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a layer passes one of the filters AutoCAD makes, by its name: All Used Layers, Unreconciled New Layers (none, as
        /// the library does not reconcile) and Viewport Overrides.
        /// </summary>
        /// <param name="name">The name of the filter.</param>
        /// <param name="layer">The layer.</param>
        /// <param name="context">What the layer is read against.</param>
        /// <returns>True when it passes.</returns>
        public static bool PassesDynamic(string name, DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            foreach (KeyValuePair<string, string> made in Dynamic)
            {
                if (Same(made.Key, name))
                {
                    CadLayerExpression.TryParse(made.Value, out CadLayerExpression expression);
                    return expression.Matches(layer, context);
                }
            }

            return true;
        }

        /// <summary>
        /// The names of the layers a filter lists: those that pass it (those that do not where <paramref name="invert"/>), the filter
        /// found by its names from the top, or by the name of one AutoCAD makes. A path that names no filter, and an empty one,
        /// is All: every layer.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="path">The names of the filter; none for All.</param>
        /// <param name="invert">Whether the layers that fail the filter are listed instead.</param>
        /// <param name="viewport">The viewport VPFROZEN is read in; null for none.</param>
        /// <returns>The names, letters of either case alike.</returns>
        public static HashSet<string> Visible(
            DxfDatabase database,
            IReadOnlyList<string> path,
            bool invert,
            DxfViewport viewport
        )
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var context = new CadLayerFilterContext(database, viewport);
            string made =
                path != null && path.Count == 1
                    ? Dynamic.Select(d => d.Key).FirstOrDefault(k => Same(k, path[0]))
                    : null;
            CadLayerFilter filter = made == null && path != null && path.Count > 0 ? Find(Read(database), path) : null;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                bool passes =
                    made != null ? PassesDynamic(made, layer, context)
                    : filter != null ? Passes(filter, layer, context)
                    : true;

                if (passes != invert)
                {
                    names.Add(layer.Name);
                }
            }

            return names;
        }

        /// <summary>Adds a property filter, nested in a filter or at the top, with an expression of AutoCAD's form.</summary>
        /// <param name="document">The document.</param>
        /// <param name="parentPath">The names of the filter it is nested in; none for the top.</param>
        /// <param name="name">The name.</param>
        /// <param name="expression">The expression, as <c>NAME=="1_Arch*" AND OFF=="False"</c>; refused where it is not one.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult AddProperty(
            CadDocument document,
            IReadOnlyList<string> parentPath,
            string name,
            string expression
        )
        {
            if (!CadLayerExpression.TryParse(expression, out _))
            {
                return CadEditResult.Refuse("The filter definition is not valid: " + expression);
            }

            return Add(document, parentPath, name, PropertyData(name, expression));
        }

        /// <summary>Adds a group filter, nested in a filter or at the top, holding the layers named (those the drawing has).</summary>
        /// <param name="document">The document.</param>
        /// <param name="parentPath">The names of the filter it is nested in; none for the top.</param>
        /// <param name="name">The name.</param>
        /// <param name="layerNames">The layers; none for an empty group, as AutoCAD's New Group Filter makes one.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult AddGroup(
            CadDocument document,
            IReadOnlyList<string> parentPath,
            string name,
            IEnumerable<string> layerNames
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return Add(document, parentPath, name, GroupData(document.Database, name, layerNames ?? new string[0]));
        }

        /// <summary>Changes the expression of a property filter.</summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter.</param>
        /// <param name="expression">The expression; refused where it is not one of AutoCAD's form.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetExpression(CadDocument document, IReadOnlyList<string> path, string expression)
        {
            if (!CadLayerExpression.TryParse(expression, out _))
            {
                return CadEditResult.Refuse("The filter definition is not valid: " + expression);
            }

            return Edit(
                document,
                path,
                "Definition of the layer filter",
                filter =>
                    filter.Kind == CadLayerFilterKind.Property
                        ? null
                        : "\"" + filter.Name + "\" is a group filter: it has layers, not a definition.",
                (database, filter, record) => record.Data = Replace(record.Data, 301, expression)
            );
        }

        /// <summary>Renames a filter; a name a sibling has already, letters of either case the same, is refused.</summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter.</param>
        /// <param name="newName">The new name, the spaces round it taken off.</param>
        /// <returns>What came of it; the name it has already is done with nothing written.</returns>
        public static CadEditResult Rename(CadDocument document, IReadOnlyList<string> path, string newName)
        {
            string wanted = (newName ?? string.Empty).Trim();
            string bad = BadName(wanted);

            if (bad != null)
            {
                return CadEditResult.Refuse(bad);
            }

            CadLayerFilter found = Find(Read(document.Database), path);

            if (found == null)
            {
                return Missing(path);
            }

            if (string.Equals(found.Name, wanted, StringComparison.Ordinal))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            IEnumerable<CadLayerFilter> siblings =
                found.Parent == null ? Read(document.Database) : found.Parent.Children;

            if (
                !Same(found.Name, wanted)
                && (siblings.Any(f => Same(f.Name, wanted)) || Dynamic.Any(d => Same(d.Key, wanted)))
            )
            {
                return CadEditResult.Refuse("A layer filter named \"" + wanted + "\" already exists.");
            }

            return Edit(
                document,
                path,
                "Rename of the layer filter",
                filter => null,
                (database, filter, record) => record.Data = Replace(record.Data, 300, wanted)
            );
        }

        /// <summary>Deletes a filter with the filters nested in it; the layers stay.</summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, IReadOnlyList<string> path)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            CadLayerFilter found = Find(Read(document.Database), path);

            if (found == null)
            {
                return Missing(path);
            }

            string old = found.ToString();
            return document.ApplyEdit(
                "LAYER",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete layer filter", old, null) },
                s =>
                {
                    DxfDatabase database = s.Database;
                    CadLayerFilter now = Find(Read(database), path);
                    DxfDBDictionary owner = KeptIn(
                        now.Parent == null ? (DxfDBObject)database.LayerTable : Record(database, now.Parent.Path),
                        false
                    );

                    if (owner != null && ContainsFilter(owner, now.Name, out string key))
                    {
                        owner.Remove(key);
                    }

                    DropLegacy(database, now.Name);
                }
            );
        }

        /// <summary>
        /// Gives a group filter layers, as dragging layers onto it or Select Layers does: added to those it has, or in place of
        /// them. A layer the drawing does not have is left out.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter.</param>
        /// <param name="layerNames">The layers.</param>
        /// <param name="replace">Whether they replace the layers the group has.</param>
        /// <returns>What came of it; refused for a property filter.</returns>
        public static CadEditResult SetLayers(
            CadDocument document,
            IReadOnlyList<string> path,
            IEnumerable<string> layerNames,
            bool replace
        )
        {
            var names = (layerNames ?? new string[0]).ToList();
            return Edit(
                document,
                path,
                "Layers of the layer filter",
                filter =>
                    filter.Kind == CadLayerFilterKind.Group
                        ? null
                        : "\"" + filter.Name + "\" is a property filter: layers are added to a group filter.",
                (database, filter, record) =>
                {
                    var handles = replace ? new List<string>() : new List<string>(filter.Handles);

                    foreach (string name in names)
                    {
                        if (database.LayerTable.Has(name))
                        {
                            string handle = database.LayerTable[name].Handle;

                            if (handle != null && !handles.Contains(handle, StringComparer.OrdinalIgnoreCase))
                            {
                                handles.Add(handle);
                            }
                        }
                    }

                    record.Data = GroupRecord(record.Data, handles);
                }
            );
        }

        /// <summary>
        /// Turns a property filter into a group filter of the layers it shows now, as AutoCAD's Convert to Group Filter does; the
        /// filters nested in it stay in it.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter.</param>
        /// <returns>What came of it; refused for a group filter.</returns>
        public static CadEditResult ConvertToGroup(CadDocument document, IReadOnlyList<string> path)
        {
            return Edit(
                document,
                path,
                "Convert to group layer filter",
                filter =>
                    filter.Kind == CadLayerFilterKind.Property
                        ? null
                        : "\"" + filter.Name + "\" is a group filter already.",
                (database, filter, record) =>
                {
                    var context = new CadLayerFilterContext(database, null);
                    var handles = new List<string>();

                    foreach (DxfLayerTableRecord layer in database.LayerTable)
                    {
                        if (layer.Handle != null && Passes(filter, layer, context))
                        {
                            handles.Add(layer.Handle);
                        }
                    }

                    var data = new List<DxfTypedValue>();

                    if (filter.IsCurrent)
                    {
                        data.Add(new DxfTypedValue(290, "1"));
                    }

                    data.Add(new DxfTypedValue(1, GroupMark));
                    data.Add(new DxfTypedValue(90, "1"));
                    data.Add(new DxfTypedValue(300, filter.Name));
                    handles.ForEach(h => data.Add(new DxfTypedValue(330, h)));
                    record.Data = data;
                }
            );
        }

        /// <summary>
        /// Marks the filter the layer list shows as the current one, as AutoCAD does when one is chosen in its tree (group 290 of its
        /// XRECORD); none marks All.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="path">The names of the filter; none for All.</param>
        /// <returns>What came of it; a filter AutoCAD makes, or one only ACAD_LAYERFILTERS keeps, is not marked and is not an error.</returns>
        public static CadEditResult SetCurrent(CadDocument document, IReadOnlyList<string> path)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            IReadOnlyList<CadLayerFilter> filters = Read(document.Database);
            CadLayerFilter wanted = path == null || path.Count == 0 ? null : Find(filters, path);

            if (path != null && path.Count > 0 && wanted == null)
            {
                return Missing(path);
            }

            CadLayerFilter now = All(filters).FirstOrDefault(f => f.IsCurrent);

            if (now == wanted || (wanted != null && wanted.IsLegacy))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return document.ApplyEdit(
                "LAYER",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Current layer filter",
                        now?.ToString(),
                        wanted?.ToString()
                    ),
                },
                s =>
                {
                    DxfDatabase database = s.Database;

                    if (now != null)
                    {
                        DxfXrecord old = Record(database, now.Path);
                        old.Data = old.Data.Where(g => g.TypeCode != 290).ToList();
                    }

                    if (wanted != null)
                    {
                        DxfXrecord record = Record(database, wanted.Path);
                        var data = new List<DxfTypedValue> { new DxfTypedValue(290, "1") };
                        data.AddRange(record.Data.Where(g => g.TypeCode != 290));
                        record.Data = data;
                    }
                }
            );
        }

        /// <summary>The filters and every filter nested in them, depth first, in the order they are listed.</summary>
        /// <param name="filters">The filters under All.</param>
        /// <returns>All of them.</returns>
        public static IEnumerable<CadLayerFilter> All(IEnumerable<CadLayerFilter> filters)
        {
            foreach (CadLayerFilter filter in filters)
            {
                yield return filter;

                foreach (CadLayerFilter nested in All(filter.Children))
                {
                    yield return nested;
                }
            }
        }

        /// <summary>The names AutoCAD refuses for a filter: empty, or one that holds a character it takes out of a name.</summary>
        private static string BadName(string name)
        {
            if (name.Length == 0)
            {
                return "A layer filter needs a name.";
            }

            if (name.Length > 255 || name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
            {
                return "\"" + name + "\" is an invalid layer filter name.";
            }

            return null;
        }

        private static CadEditResult Missing(IReadOnlyList<string> path) =>
            CadEditResult.Refuse(
                "The drawing has no layer filter \"" + string.Join("/", path ?? new string[0]) + "\"."
            );

        private static bool PassesOne(CadLayerFilter filter, DxfLayerTableRecord layer, CadLayerFilterContext context)
        {
            if (filter.Kind == CadLayerFilterKind.Group)
            {
                return layer.Handle != null && filter.Handles.Contains(layer.Handle, StringComparer.OrdinalIgnoreCase);
            }

            return CadLayerExpression.TryParse(filter.Expression, out CadLayerExpression expression)
                && expression.Matches(layer, context);
        }

        private static CadEditResult Add(
            CadDocument document,
            IReadOnlyList<string> parentPath,
            string name,
            List<DxfTypedValue> data
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string wanted = (name ?? string.Empty).Trim();
            string bad = BadName(wanted);

            if (bad != null)
            {
                return CadEditResult.Refuse(bad);
            }

            if (string.CompareOrdinal(document.Database.Version, "AC1015") < 0)
            {
                return CadEditResult.Refuse("A drawing older than AutoCAD 2000 keeps no layer filters.");
            }

            IReadOnlyList<CadLayerFilter> filters = Read(document.Database);
            CadLayerFilter parent = parentPath == null || parentPath.Count == 0 ? null : Find(filters, parentPath);

            if (parentPath != null && parentPath.Count > 0 && parent == null)
            {
                return Missing(parentPath);
            }

            IEnumerable<CadLayerFilter> siblings = parent == null ? filters : parent.Children;

            if (siblings.Any(f => Same(f.Name, wanted)) || (parent == null && Dynamic.Any(d => Same(d.Key, wanted))))
            {
                return CadEditResult.Refuse("A layer filter named \"" + wanted + "\" already exists.");
            }

            // The name written is the one trimmed.
            data = data.Select(g => g.TypeCode == 300 ? new DxfTypedValue(300, wanted) : g).ToList();
            return document.ApplyEdit(
                "LAYER",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "New layer filter",
                        null,
                        parent == null ? wanted : parent + "/" + wanted
                    ),
                },
                s =>
                {
                    DxfDatabase database = s.Database;
                    DxfDBObject owner =
                        parent == null ? (DxfDBObject)database.LayerTable : Record(database, parent.Path);
                    DxfDBDictionary into = KeptIn(owner, true);
                    into.SetAt(FreeKey(into), new DxfXrecord { Data = data });
                }
            );
        }

        /// <summary>
        /// Changes the record of a filter in one undo step: a filter only ACAD_LAYERFILTERS keeps is made one of ACLYDICTIONARY
        /// first, as AutoCAD 2021 keeps it there on saving, and the record ACAD_LAYERFILTERS keeps of the name is dropped.
        /// </summary>
        private static CadEditResult Edit(
            CadDocument document,
            IReadOnlyList<string> path,
            string what,
            Func<CadLayerFilter, string> refuse,
            Action<DxfDatabase, CadLayerFilter, DxfXrecord> change
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            CadLayerFilter found = Find(Read(document.Database), path);

            if (found == null)
            {
                return Missing(path);
            }

            string refusal = refuse(found);

            if (refusal != null)
            {
                return CadEditResult.Refuse(refusal);
            }

            return document.ApplyEdit(
                "LAYER",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, what, found.ToString(), found.ToString()),
                },
                s =>
                {
                    DxfDatabase database = s.Database;
                    CadLayerFilter now = Find(Read(database), path);
                    DxfXrecord record = now.IsLegacy ? Promote(database, now) : Record(database, now.Path);
                    change(database, now, record);
                    DropLegacy(database, now.Name);
                }
            );
        }

        /// <summary>Makes a filter only ACAD_LAYERFILTERS keeps a record of ACLYDICTIONARY, with its expression.</summary>
        private static DxfXrecord Promote(DxfDatabase database, CadLayerFilter filter)
        {
            DxfDBDictionary into = KeptIn(database.LayerTable, true);
            string key = FreeKey(into);
            into.SetAt(key, new DxfXrecord { Data = PropertyData(filter.Name, filter.Expression) });
            return (DxfXrecord)into.GetAt(key);
        }

        /// <summary>The first name <c>*A1</c>, <c>*A2</c> the dictionary has no entry of, as AutoCAD names the filters it keeps.</summary>
        private static string FreeKey(DxfDBDictionary into)
        {
            int n = 1;

            while (into.Contains("*A" + n.ToString(CultureInfo.InvariantCulture)))
            {
                n++;
            }

            return "*A" + n.ToString(CultureInfo.InvariantCulture);
        }

        private static List<DxfTypedValue> PropertyData(string name, string expression) =>
            new List<DxfTypedValue>
            {
                new DxfTypedValue(1, PropertyMark),
                new DxfTypedValue(90, "1"),
                new DxfTypedValue(300, name),
                new DxfTypedValue(301, expression),
            };

        private static List<DxfTypedValue> GroupData(DxfDatabase database, string name, IEnumerable<string> layerNames)
        {
            var handles = new List<string>();

            foreach (string layer in layerNames)
            {
                string handle =
                    layer != null && database.LayerTable.Has(layer) ? database.LayerTable[layer].Handle : null;

                if (handle != null && !handles.Contains(handle, StringComparer.OrdinalIgnoreCase))
                {
                    handles.Add(handle);
                }
            }

            return GroupRecord(
                new[] { new DxfTypedValue(1, GroupMark), new DxfTypedValue(90, "1"), new DxfTypedValue(300, name) },
                handles
            );
        }

        /// <summary>A group's data with the layers given: what comes before its first 330 kept, a 330 for each handle after it.</summary>
        private static List<DxfTypedValue> GroupRecord(IEnumerable<DxfTypedValue> data, IEnumerable<string> handles)
        {
            var result = data.Where(g => g.TypeCode != 330).ToList();

            foreach (string handle in handles)
            {
                result.Add(new DxfTypedValue(330, handle));
            }

            return result;
        }

        private static List<DxfTypedValue> Replace(IEnumerable<DxfTypedValue> data, int code, string value) =>
            data.Select(g => g.TypeCode == code ? new DxfTypedValue(code, value) : g).ToList();

        /// <summary>Takes out the record ACAD_LAYERFILTERS keeps of a filter of the name, which AutoCAD 2021 would list again beside the one changed.</summary>
        private static void DropLegacy(DxfDatabase database, string name)
        {
            DxfDBDictionary extension = database.LayerTable.ExtensionDictionary;

            if (
                extension != null
                && extension.Contains(Legacy)
                && extension.GetAt(Legacy) is DxfDBDictionary old
                && old.Any(e => Same(e.Key, name))
            )
            {
                old.Remove(old.First(e => Same(e.Key, name)).Key);
            }
        }

        private static DxfXrecord Record(DxfDatabase database, IReadOnlyList<string> path)
        {
            DxfDBObject owner = database.LayerTable;
            DxfXrecord record = null;

            foreach (string name in path)
            {
                DxfDBDictionary level = KeptIn(owner, false);

                if (level == null)
                {
                    return null;
                }

                record = null;

                foreach (DxfDBDictionaryEntry entry in level)
                {
                    if (entry.Value is DxfXrecord candidate && Same(NameOf(candidate.Data), name))
                    {
                        record = candidate;
                        break;
                    }
                }

                if (record == null)
                {
                    return null;
                }

                owner = record;
            }

            return record;
        }

        private static bool ContainsFilter(DxfDBDictionary level, string name, out string key)
        {
            foreach (DxfDBDictionaryEntry entry in level)
            {
                if (entry.Value is DxfXrecord record && Same(NameOf(record.Data), name))
                {
                    key = entry.Key;
                    return true;
                }
            }

            key = null;
            return false;
        }

        /// <summary>The ACLYDICTIONARY of an object's extension dictionary; made where <paramref name="create"/>, null where it has none.</summary>
        private static DxfDBDictionary KeptIn(DxfDBObject owner, bool create)
        {
            DxfDBDictionary extension = owner.ExtensionDictionary;

            if (extension == null)
            {
                if (!create)
                {
                    return null;
                }

                owner.CreateExtensionDictionary();
                extension = owner.ExtensionDictionary;
            }

            if (!extension.Contains(Dictionary))
            {
                if (!create)
                {
                    return null;
                }

                extension.SetAt(Dictionary, new DxfDBDictionary());
            }

            return extension.GetAt(Dictionary) as DxfDBDictionary;
        }

        private static void ReadLevel(
            DxfDatabase database,
            DxfDBDictionary level,
            CadLayerFilter parent,
            List<CadLayerFilter> into
        )
        {
            foreach (DxfDBDictionaryEntry entry in level)
            {
                if (!(entry.Value is DxfXrecord record))
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> data = record.Data;
                string mark = data.Where(g => g.TypeCode == 1).Select(g => g.Value).FirstOrDefault();
                string name = NameOf(data);

                if (string.IsNullOrEmpty(name) || (mark != PropertyMark && mark != GroupMark))
                {
                    continue;
                }

                var filter = new CadLayerFilter(
                    name,
                    mark == GroupMark ? CadLayerFilterKind.Group : CadLayerFilterKind.Property,
                    parent
                )
                {
                    Expression = data.Where(g => g.TypeCode == 301).Select(g => g.Value).FirstOrDefault()
                        ?? string.Empty,
                    IsCurrent = data.Any(g => g.TypeCode == 290 && g.Value.Trim() != "0"),
                };

                foreach (DxfTypedValue group in data.Where(g => g.TypeCode == 330))
                {
                    filter.Handles.Add(group.Value);

                    if (
                        database.TryGetObject(group.Value ?? string.Empty, out DxfDBObject found)
                        && found is DxfLayerTableRecord layer
                    )
                    {
                        filter.NamesMutable.Add(layer.Name);
                    }
                }

                if (into.Any(f => Same(f.Name, name)))
                {
                    continue;
                }

                DxfDBDictionary nested = KeptIn(record, false);

                if (nested != null)
                {
                    ReadLevel(database, nested, filter, filter.ChildrenMutable);
                    Sort(filter.ChildrenMutable);
                }

                into.Add(filter);
            }
        }

        private static string NameOf(IEnumerable<DxfTypedValue> data) =>
            data.Where(g => g.TypeCode == 300).Select(g => g.Value).FirstOrDefault();

        private static void Sort(List<CadLayerFilter> filters) =>
            filters.Sort(
                (a, b) =>
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
                    return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
                }
            );

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
