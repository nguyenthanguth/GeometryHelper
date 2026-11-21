using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>The handles a selection set of the object model holds, and whether <c>Delete</c> took it out of the collection.</summary>
    internal sealed class LispAxSet
    {
        /// <summary>Gets or sets the name; empty for the active and the pick-first sets, which are no member of the collection.</summary>
        internal string Name { get; set; }

        /// <summary>Gets the handles in the order they were added.</summary>
        internal List<string> Handles { get; } = new List<string>();

        /// <summary>Gets or sets whether <c>Delete</c> was called.</summary>
        internal bool Deleted { get; set; }
    }

    /// <summary>The color an AcCmColor object holds: a copy of the color of an entity or a layer, changed through its own members.</summary>
    internal sealed class LispAxColor
    {
        /// <summary>Makes a holder.</summary>
        /// <param name="color">The color it starts as.</param>
        internal LispAxColor(DxfColor color)
        {
            Color = color;
        }

        /// <summary>Gets or sets the color.</summary>
        internal DxfColor Color { get; set; }
    }

    /// <summary>
    /// The application, the document, the collections (blocks, layers, text styles, linetypes, dimension styles, selection
    /// sets), the blocks and table records, the selection sets, the AcCmColor and the undo marks of the object model
    /// (D219 to D222, D225).
    /// </summary>
    internal sealed partial class LispActiveX
    {
        private static readonly string[][] Collections =
        {
            new[] { "Blocks", "IAcadBlocks" },
            new[] { "Layers", "IAcadLayers" },
            new[] { "TextStyles", "IAcadTextStyles" },
            new[] { "Linetypes", "IAcadLineTypes" },
            new[] { "DimStyles", "IAcadDimStyles" },
            new[] { "SelectionSets", "IAcadSelectionSets" },
        };

        private readonly List<LispAxSet> _sets = new List<LispAxSet>();
        private DxfDatabase _indexed;
        private Dictionary<string, DxfSymbolTableRecord> _records;

        #region Identity

        /// <summary>
        /// A new value for an object, at every read: <c>eq</c> of two values of one object is nil and <c>equal</c> T (AutoCAD 2021,
        /// GUI g2).
        /// </summary>
        internal LispVlaObject Make(LispVlaKind kind, string interfaceName, string key) =>
            new LispVlaObject(kind, interfaceName, key);

        /// <summary>Lets a value go (<c>vlax-release-object</c>): it is marked released, prints as zeros and cannot be used again.</summary>
        internal void Release(LispVlaObject target) => target.IsReleased = true;

        /// <summary>Gets the application: one per interpreter.</summary>
        internal LispVlaObject Application => Make(LispVlaKind.Application, "IAcadApplication", "");

        /// <summary>Gets the document of the session.</summary>
        internal LispVlaObject DocumentObject => Make(LispVlaKind.Document, "IAcadDocument", "");

        /// <summary>Gets a collection of the document by its name: <c>Blocks</c>, <c>Layers</c>, ...</summary>
        internal LispVlaObject Collection(string name)
        {
            foreach (string[] known in Collections)
            {
                if (string.Equals(known[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Make(LispVlaKind.Collection, known[1], known[0]);
                }
            }

            throw UnknownName(name);
        }

        /// <summary>
        /// Gives an error where a value is of an object that is released (<c>null interface pointer: #&lt;VLA-OBJECT 000...&gt;</c>,
        /// AutoCAD 2021, GUI g2), erased (<c>Automation Error. Object was erased</c>) or a selection set taken out of its collection.
        /// </summary>
        internal void EnsureLive(LispVlaObject target)
        {
            if (target.IsReleased)
            {
                throw Released();
            }

            switch (target.Kind)
            {
                case LispVlaKind.Entity:
                    ItemOf(target);
                    break;
                case LispVlaKind.Block:
                case LispVlaKind.Record:
                    RecordOf(target);
                    break;
                case LispVlaKind.SelectionSet:
                    if (SetOf(target).Deleted)
                    {
                        throw Automation("Selection Set was deleted");
                    }

                    break;
            }
        }

        /// <summary>Whether the object of a value is erased: an entity or a record that is not in the drawing, or a selection set that was deleted.</summary>
        internal bool IsErased(LispVlaObject target)
        {
            switch (target.Kind)
            {
                case LispVlaKind.Entity:
                    return FindItemOf(target) == null;
                case LispVlaKind.Block:
                case LispVlaKind.Record:
                    return FindRecordOf(target) == null;
                case LispVlaKind.SelectionSet:
                    return SetOf(target).Deleted;
                default:
                    return false;
            }
        }

        #endregion

        #region Records and blocks

        /// <summary>The record of a handle in one of the tables: null where none holds it, or it is erased.</summary>
        internal DxfSymbolTableRecord FindRecord(string handle)
        {
            DxfDatabase database = Document.Database;

            for (int round = 0; round < 2; round++)
            {
                if (round == 1 || _records == null || !ReferenceEquals(_indexed, database))
                {
                    _records = new Dictionary<string, DxfSymbolTableRecord>(StringComparer.OrdinalIgnoreCase);
                    _indexed = database;

                    foreach (DxfSymbolTableRecord record in database.LayerTable)
                    {
                        Index(record);
                    }

                    foreach (DxfSymbolTableRecord record in database.TextStyleTable)
                    {
                        Index(record);
                    }

                    foreach (DxfSymbolTableRecord record in database.LinetypeTable)
                    {
                        Index(record);
                    }

                    foreach (DxfSymbolTableRecord record in database.DimStyleTable)
                    {
                        Index(record);
                    }

                    foreach (DxfSymbolTableRecord record in database.BlockTable)
                    {
                        Index(record);
                    }
                }

                if (_records.TryGetValue(handle, out DxfSymbolTableRecord found) && !found.IsErased)
                {
                    return found;
                }
            }

            return null;
        }

        private void Index(DxfSymbolTableRecord record)
        {
            if (!string.IsNullOrEmpty(record.Handle))
            {
                _records[record.Handle] = record;
            }
        }

        private DxfSymbolTableRecord FindRecordOf(LispVlaObject target) => FindRecord(target.Key);

        /// <summary>The record a value stands for; <c>Automation Error. Object was erased</c> where it is gone.</summary>
        internal DxfSymbolTableRecord RecordOf(LispVlaObject target) =>
            FindRecordOf(target) ?? throw Automation("Object was erased");

        /// <summary>The block a value stands for.</summary>
        internal DxfBlockTableRecord BlockOf(LispVlaObject target) =>
            RecordOf(target) as DxfBlockTableRecord ?? throw Automation("Object was erased");

        /// <summary>The value of a record of a table or a block: its interface follows its kind (model space, a layout's block, a block).</summary>
        internal LispVlaObject WrapRecord(DxfSymbolTableRecord record)
        {
            string handle = record.Handle ?? "";

            switch (record)
            {
                case DxfBlockTableRecord block:
                    string name = block.Name ?? "";
                    string face =
                        string.Equals(name, DxfBlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)
                            ? "IAcadModelSpace"
                        : block.IsLayout ? "IAcadPaperSpace"
                        : "IAcadBlock";
                    return Make(LispVlaKind.Block, face, handle);
                case DxfLayerTableRecord _:
                    return Make(LispVlaKind.Record, "IAcadLayer", handle);
                case DxfTextStyleTableRecord _:
                    return Make(LispVlaKind.Record, "IAcadTextStyle", handle);
                case DxfLinetypeTableRecord _:
                    return Make(LispVlaKind.Record, "IAcadLineType", handle);
                case DxfDimStyleTableRecord _:
                    return Make(LispVlaKind.Record, "IAcadDimStyle", handle);
                default:
                    return Make(LispVlaKind.Record, "IAcadObject", handle);
            }
        }

        private static string ObjectNameOfRecord(DxfSymbolTableRecord record)
        {
            switch (record)
            {
                case DxfBlockTableRecord _:
                    return "AcDbBlockTableRecord";
                case DxfLayerTableRecord _:
                    return "AcDbLayerTableRecord";
                case DxfTextStyleTableRecord _:
                    return "AcDbTextStyleTableRecord";
                case DxfLinetypeTableRecord _:
                    return "AcDbLinetypeTableRecord";
                case DxfDimStyleTableRecord _:
                    return "AcDbDimStyleTableRecord";
                default:
                    return "AcDbSymbolTableRecord";
            }
        }

        #endregion

        #region Collections

        /// <summary>The records of the table a collection stands for, in the table's order.</summary>
        private List<DxfSymbolTableRecord> RecordsOf(string collection)
        {
            DxfDatabase database = Document.Database;
            var records = new List<DxfSymbolTableRecord>();

            switch (collection)
            {
                case "Blocks":
                    foreach (DxfSymbolTableRecord record in database.BlockTable)
                    {
                        records.Add(record);
                    }

                    break;
                case "Layers":
                    foreach (DxfSymbolTableRecord record in database.LayerTable)
                    {
                        records.Add(record);
                    }

                    break;
                case "TextStyles":
                    foreach (DxfSymbolTableRecord record in database.TextStyleTable)
                    {
                        records.Add(record);
                    }

                    break;
                case "Linetypes":
                    foreach (DxfSymbolTableRecord record in database.LinetypeTable)
                    {
                        records.Add(record);
                    }

                    break;
                case "DimStyles":
                    foreach (DxfSymbolTableRecord record in database.DimStyleTable)
                    {
                        records.Add(record);
                    }

                    break;
            }

            return records;
        }

        /// <summary>The members of a collection as values, in <c>Item</c> order (D220); what <c>vlax-for</c> and <c>vlax-map-collection</c> walk.</summary>
        internal List<LispVlaObject> ItemsOfCollection(LispVlaObject target)
        {
            var items = new List<LispVlaObject>();

            switch (target.Kind)
            {
                case LispVlaKind.Collection when target.Key == "SelectionSets":
                    foreach (LispAxSet set in _sets)
                    {
                        if (!set.Deleted)
                        {
                            items.Add(SetValue(set));
                        }
                    }

                    break;
                case LispVlaKind.Collection:
                    foreach (DxfSymbolTableRecord record in RecordsOf(target.Key))
                    {
                        items.Add(WrapRecord(record));
                    }

                    break;
                case LispVlaKind.Block:
                    foreach (DxfItem item in LispDrawing.Items(BlockOf(target)))
                    {
                        items.Add(WrapEntity(item));
                    }

                    break;
                case LispVlaKind.SelectionSet:
                    foreach (string handle in LiveHandles(SetOf(target)))
                    {
                        LispVlaObject wrapped = Wrap(handle);

                        if (wrapped != null)
                        {
                            items.Add(wrapped);
                        }
                    }

                    break;
                default:
                    throw LispInterpreter.TypeError("VLA-object collection:", target);
            }

            return items;
        }

        private int CountOf(LispVlaObject target)
        {
            switch (target.Kind)
            {
                case LispVlaKind.Collection when target.Key == "SelectionSets":
                    return ItemsOfCollection(target).Count;
                case LispVlaKind.Collection:
                    return RecordsOf(target.Key).Count;
                case LispVlaKind.Block:
                    int count = 0;

                    foreach (DxfItem _ in LispDrawing.Items(BlockOf(target)))
                    {
                        count++;
                    }

                    return count;
                default:
                    return LiveHandles(SetOf(target)).Count;
            }
        }

        /// <summary>
        /// <c>Item</c> of a collection, a block or a selection set: an integer, a real or a variant index from 0, or, for a
        /// collection, a name (any case for the tables, as written for the selection sets). Past the end (AutoCAD 2021, GUI g2,
        /// g3): <c>Automation Error. Description was not provided.</c>, for the selection sets <c>Automation Error. Invalid
        /// argument index in Item</c>; a name that is not there <c>Automation Error. Key not found</c>.
        /// </summary>
        private object CollectionItem(LispVlaObject target, object index)
        {
            object held = index is LispVariant variant ? variant.Value : index;

            if (held is string name)
            {
                if (target.Kind != LispVlaKind.Collection)
                {
                    throw Automation("Key not found");
                }

                if (target.Key == "SelectionSets")
                {
                    foreach (LispVlaObject set in ItemsOfCollection(target))
                    {
                        if (string.Equals(SetOf(set).Name, name, StringComparison.Ordinal))
                        {
                            return set;
                        }
                    }

                    throw Automation("Key not found");
                }

                foreach (DxfSymbolTableRecord record in RecordsOf(target.Key))
                {
                    if (string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return WrapRecord(record);
                    }
                }

                throw Automation("Key not found");
            }

            int at = IntOf(held);
            bool sets = target.Kind == LispVlaKind.Collection && target.Key == "SelectionSets";
            LispException past = Automation(sets ? "Invalid argument index in Item" : "Description was not provided.");

            if (at < 0)
            {
                throw past;
            }

            if (target.Kind == LispVlaKind.Collection && !sets)
            {
                List<DxfSymbolTableRecord> records = RecordsOf(target.Key);
                return at < records.Count ? WrapRecord(records[at]) : throw past;
            }

            List<LispVlaObject> items = ItemsOfCollection(target);
            return at < items.Count ? items[at] : throw past;
        }

        #endregion

        #region Selection sets

        /// <summary>The state of a selection set value.</summary>
        internal static LispAxSet SetOf(LispVlaObject target) => (LispAxSet)target.State;

        /// <summary>The handles of a set that are still entities of the drawing, by the handle they have now, each once, in the order the set has them.</summary>
        internal List<string> LiveHandles(LispAxSet set)
        {
            var live = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in set.Handles)
            {
                string now = State.Resolve(handle);

                if (Document.FindEntity(now) != null && seen.Add(now))
                {
                    live.Add(now);
                }
            }

            return live;
        }

        /// <summary>A new value of a selection set: every read gives one, all of the same set.</summary>
        private static LispVlaObject SetValue(LispAxSet set) =>
            new LispVlaObject(LispVlaKind.SelectionSet, "IAcadSelectionSet", set.Name) { State = set };

        /// <summary>
        /// A selection set of the collection, made empty, kept by the interpreter under its name; names are case-sensitive
        /// (AutoCAD 2021, GUI g2): <c>Automation Error. The named selection set exists</c> for one that is there.
        /// </summary>
        private LispVlaObject AddSet(string name)
        {
            foreach (LispAxSet set in _sets)
            {
                if (!set.Deleted && string.Equals(set.Name, name, StringComparison.Ordinal))
                {
                    throw Automation("The named selection set exists");
                }
            }

            var made = new LispAxSet { Name = name };
            _sets.Add(made);
            return SetValue(made);
        }

        /// <summary>
        /// The set the application keeps under a name of its own, <c>CURRENT</c> for the last selection and <c>PICKFIRST</c> for
        /// the pick-first one: made at the first read and added to the collection, its objects set at each read (GUI g2).
        /// </summary>
        private LispVlaObject NamedSet(string name, IEnumerable<string> handles)
        {
            LispAxSet found = null;

            foreach (LispAxSet set in _sets)
            {
                if (!set.Deleted && string.Equals(set.Name, name, StringComparison.Ordinal))
                {
                    found = set;
                    break;
                }
            }

            if (found == null)
            {
                found = new LispAxSet { Name = name };
                _sets.Add(found);
            }

            found.Handles.Clear();
            found.Handles.AddRange(handles);
            return SetValue(found);
        }

        /// <summary>The handles of the entities an argument holds: a variant or a safearray of objects, or, for the old functions, a list.</summary>
        private List<string> HandlesOfObjects(object argument)
        {
            object held = argument is LispVariant variant ? variant.Value : argument;
            var objects = new List<object>();

            if (held is LispSafeArray array)
            {
                objects.AddRange(array.Items);
            }
            else if (held is LispCons)
            {
                objects.AddRange(LispInterpreter.ItemsOf(held));
            }
            else if (held is LispVlaObject)
            {
                objects.Add(held);
            }
            else
            {
                throw Mismatch();
            }

            var handles = new List<string>();

            foreach (object item in objects)
            {
                object one = item is LispVariant inner ? inner.Value : item;

                if (!(one is LispVlaObject entity) || entity.Kind != LispVlaKind.Entity)
                {
                    throw Mismatch();
                }

                EnsureLive(entity);
                handles.Add(ItemOf(entity).Handle);
            }

            return handles;
        }

        /// <summary>
        /// <c>Select</c> (D222): mode 0 window, 1 crossing, 3 previous, 4 last, 5 all (of the current space), the corners in the
        /// WCS, with an ssget-style filter from two arrays; the objects found are added to the set.
        /// </summary>
        private void Select(LispAxSet set, object[] a)
        {
            int mode = (int)a[0];
            var request = new LispDrawing.Request();
            Tolerance tolerance = Session.Tolerance;
            LispSelectionFilter filter = FilterOf(a, 3, tolerance);
            request.Filter = filter;
            List<string> found;

            switch (mode)
            {
                case 0:
                case 1:
                    if (a.Length < 3 || a[1] == null || a[2] == null)
                    {
                        throw new LispException("too few arguments");
                    }

                    request.Mode = mode == 0 ? "W" : "C";
                    request.Point = UcsPoint(a[1]);
                    request.Corner = UcsPoint(a[2]);
                    found = LispDrawing.Find(Session, request);
                    break;
                case 3:
                    request.Mode = "P";
                    found = LispDrawing.Find(Session, request);
                    break;
                case 4:
                    request.Mode = "L";
                    found = LispDrawing.Find(Session, request);
                    break;
                case 5:
                    found = new List<string>();

                    foreach (DxfItem item in LispDrawing.Items(Document.WorkingSpace.Block))
                    {
                        if (filter == null || LispDrawing.Accepted(Session, filter, item.Handle))
                        {
                            found.Add(item.Handle);
                        }
                    }

                    break;
                default:
                    throw Automation("Invalid argument");
            }

            AddHandles(set, found);
        }

        /// <summary>The filter of two arrays, the codes and the values, given as the arguments from <paramref name="at"/>; null for none.</summary>
        private LispSelectionFilter FilterOf(object[] a, int at, Tolerance tolerance)
        {
            if (a.Length <= at || a[at] == null)
            {
                return null;
            }

            if (a.Length < at + 2 || !(a[at] is LispVariant || a[at] is LispSafeArray))
            {
                throw Mismatch();
            }

            object codes = PlainValue(a[at]);
            object values = PlainValue(a[at + 1]);
            List<object> codeItems = LispInterpreter.ItemsOf(codes);
            List<object> valueItems = LispInterpreter.ItemsOf(values);

            if (codeItems.Count != valueItems.Count)
            {
                throw Automation("Invalid argument");
            }

            var pairs = new List<object>();

            for (int i = 0; i < codeItems.Count; i++)
            {
                pairs.Add(new LispCons(codeItems[i], valueItems[i]));
            }

            if (!LispSelectionFilter.TryParse(LispInterpreter.ListOf(pairs), tolerance, out LispSelectionFilter filter))
            {
                throw Automation("Invalid argument");
            }

            return filter;
        }

        /// <summary>A point of the WCS, as the object model gives it, in the current UCS, where the selection finds it.</summary>
        private GeoPoint2 UcsPoint(object value)
        {
            var point = (double[])value;
            return Session.Ucs.ToUcs(new GeoPoint2(point[0], point[1]));
        }

        private static void AddHandles(LispAxSet set, IEnumerable<string> handles)
        {
            foreach (string handle in handles)
            {
                if (!set.Handles.Exists(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase)))
                {
                    set.Handles.Add(handle);
                }
            }
        }

        /// <summary><c>SelectAtPoint</c> (D222): the object a pick at the point finds in the current space, as <c>nentselp</c> picks, with a filter; added to the set.</summary>
        private void SelectAtPoint(LispAxSet set, object[] a)
        {
            var request = new LispDrawing.Request
            {
                Mode = "PICK",
                Point = UcsPoint(a[0]),
                Filter = FilterOf(a, 1, Session.Tolerance),
            };
            AddHandles(set, LispDrawing.Find(Session, request));
        }

        #endregion

        #region Registration

        private static readonly LispAxType[] NoParameters = Array.Empty<LispAxType>();

        private void RegisterDocumentMembers()
        {
            Func<LispVlaObject, bool> app = OfKinds(LispVlaKind.Application);
            Func<LispVlaObject, bool> doc = OfKinds(LispVlaKind.Document);
            Func<LispVlaObject, bool> everything = OfKinds(
                LispVlaKind.Application,
                LispVlaKind.Document,
                LispVlaKind.Collection,
                LispVlaKind.Block,
                LispVlaKind.Record,
                LispVlaKind.Entity,
                LispVlaKind.SelectionSet
            );

            Property("Application", everything, o => Application);
            Property("ActiveDocument", app, o => DocumentObject);

            Property("ActiveSpace", doc, o => Document.WorkingSpace.IsModel ? 1 : 0);
            Property(
                "ActiveLayer",
                doc,
                o => WrapRecord(Document.Database.LayerTable[Document.Database.Clayer]),
                LispAxType.Object,
                (o, value) => SetActiveLayer((LispVlaObject)value)
            );
            Property("ModelSpace", doc, o => WrapRecord(Document.ModelSpace.Block));
            Property("PaperSpace", doc, o => WrapRecord(PaperSpaceBlock()));
            Property("Blocks", doc, o => Collection("Blocks"));
            Property("Layers", doc, o => Collection("Layers"));
            Property("TextStyles", doc, o => Collection("TextStyles"));
            Property("Linetypes", doc, o => Collection("Linetypes"));
            Property("DimStyles", doc, o => Collection("DimStyles"));
            Property("SelectionSets", doc, o => Collection("SelectionSets"));
            Property("ActiveSelectionSet", doc, o => NamedSet("CURRENT", LivePrevious()));
            Property("PickfirstSelectionSet", doc, o => NamedSet("PICKFIRST", PickfirstHandles()));
            Property("Name", doc, o =>
                Path.GetFileName(Document.FileName ?? "") is string n && n.Length > 0 ? n : "Drawing1.dwg"
            );
            Property("FullName", doc, o => Document.FileName ?? "");
            Property("Path", doc, o => Path.GetDirectoryName(Document.FileName ?? "") ?? "");
            Method("StartUndoMark", doc, 0, NoParameters, (o, a) =>
            {
                Session.ReleaseLispHold();
                Document.BeginUndoGroup();
                return null;
            });
            Method("EndUndoMark", doc, 0, NoParameters, (o, a) =>
            {
                Session.ReleaseLispHold();
                Document.EndUndoGroup();
                return null;
            });

            Func<LispVlaObject, bool> holder = OfKinds(
                LispVlaKind.Collection,
                LispVlaKind.Block,
                LispVlaKind.SelectionSet
            );
            Property("Count", holder, o => CountOf(o));
            Method("Item", holder, 1, new[] { LispAxType.Any }, (o, a) => CollectionItem(o, a[0]));
            Method("Add", OfInterfaces("IAcadSelectionSets"), 1, new[] { LispAxType.String }, (o, a) =>
                AddSet((string)a[0])
            );

            // A table has an ObjectName and a Handle; a document and the selection sets have none (AutoCAD 2021, GUI g2).
            Func<LispVlaObject, bool> table = OfInterfaces(
                "IAcadBlocks",
                "IAcadLayers",
                "IAcadTextStyles",
                "IAcadLineTypes",
                "IAcadDimStyles"
            );
            Property("ObjectName", table, o => ObjectNameOfTable(o));
            Property("Handle", table, o => TableOf(o).Handle ?? "");

            Property("Name", OfKinds(LispVlaKind.Block, LispVlaKind.Record), o => RecordOf(o).Name);
            Property("Name", OfKinds(LispVlaKind.SelectionSet), o => SetOf(o).Name);

            RegisterBlockMembers();
            RegisterRecordMembers();
            RegisterSetMembers();
            RegisterColorMembers();
        }

        private DxfSymbolTable TableOf(LispVlaObject target)
        {
            DxfDatabase database = Document.Database;

            switch (target.Key)
            {
                case "Blocks":
                    return database.BlockTable;
                case "Layers":
                    return database.LayerTable;
                case "TextStyles":
                    return database.TextStyleTable;
                case "Linetypes":
                    return database.LinetypeTable;
                default:
                    return database.DimStyleTable;
            }
        }

        private static string ObjectNameOfTable(LispVlaObject target)
        {
            switch (target.Key)
            {
                case "Blocks":
                    return "AcDbBlockTable";
                case "Layers":
                    return "AcDbLayerTable";
                case "TextStyles":
                    return "AcDbTextStyleTable";
                case "Linetypes":
                    return "AcDbLinetypeTable";
                default:
                    return "AcDbDimStyleTable";
            }
        }

        private void RegisterBlockMembers()
        {
            Func<LispVlaObject, bool> block = OfKinds(LispVlaKind.Block);
            Property("IsLayout", block, o => BlockOf(o).IsLayout);
            Property("IsXRef", block, o => BlockOf(o).IsFromExternalReference);
            Property("IsDynamicBlock", block, o => BlockOf(o).IsDynamicBlock);
            Property("Origin", block, o =>
            {
                GeoPoint3 origin = BlockOf(o).Origin;
                return LispVariant.Point(origin.X, origin.Y, origin.Z);
            });
        }

        private void RegisterRecordMembers()
        {
            Func<LispVlaObject, bool> layer = OfInterfaces("IAcadLayer");
            DxfLayerTableRecord Layer(LispVlaObject o) => (DxfLayerTableRecord)RecordOf(o);

            LispException Refused(string name) =>
                new LispException(
                    "vla-put-" + name + ": changing a table record is not supported by GeometryHelper.Cad yet"
                );

            Property("Color", layer, o => Layer(o).ColorIndex, LispAxType.Int, (o, v) => throw Refused("Color"));
            Property(
                "TrueColor",
                layer,
                o => ColorObject(Layer(o).Color),
                LispAxType.Object,
                (o, v) => throw Refused("TrueColor")
            );
            Property(
                "Linetype",
                layer,
                o => Layer(o).LinetypeName,
                LispAxType.String,
                (o, v) => throw Refused("Linetype")
            );
            Property(
                "Lineweight",
                layer,
                o => (int)Layer(o).LineWeight,
                LispAxType.Int,
                (o, v) => throw Refused("Lineweight")
            );
            Property("LayerOn", layer, o => !Layer(o).IsOff);
            Property("Freeze", layer, o => Layer(o).IsFrozen);
            Property("Lock", layer, o => Layer(o).IsLocked);
            Property("Plottable", layer, o => Layer(o).IsPlottable);
            Property("Description", layer, o => Layer(o).Description ?? "");

            Func<LispVlaObject, bool> style = OfInterfaces("IAcadTextStyle");
            DxfTextStyleTableRecord Style(LispVlaObject o) => (DxfTextStyleTableRecord)RecordOf(o);
            Property("FontFile", style, o => Style(o).FileName ?? "");
            Property("Height", style, o => Style(o).TextSize);
            Property("Width", style, o => Style(o).XScale);
            Property("ObliqueAngle", style, o => Style(o).ObliquingAngle);

            Property("Description", OfInterfaces("IAcadLineType"), o =>
                ((DxfLinetypeTableRecord)RecordOf(o)).AsciiDescription ?? ""
            );
        }

        private void RegisterSetMembers()
        {
            Func<LispVlaObject, bool> set = OfKinds(LispVlaKind.SelectionSet);
            Method("Clear", set, 0, NoParameters, (o, a) =>
            {
                SetOf(o).Handles.Clear();
                return null;
            });
            Method("Delete", set, 0, NoParameters, (o, a) =>
            {
                LispAxSet state = SetOf(o);
                state.Deleted = true;
                state.Handles.Clear();
                return null;
            });
            Method("AddItems", set, 1, new[] { LispAxType.Any }, (o, a) =>
            {
                AddHandles(SetOf(o), HandlesOfObjects(a[0]));
                return null;
            });
            Method("RemoveItems", set, 1, new[] { LispAxType.Any }, (o, a) =>
            {
                LispAxSet state = SetOf(o);

                foreach (string handle in HandlesOfObjects(a[0]))
                {
                    state.Handles.RemoveAll(h =>
                        string.Equals(State.Resolve(h), handle, StringComparison.OrdinalIgnoreCase)
                    );
                }

                return null;
            });
            Method(
                "Select",
                set,
                1,
                new[]
                {
                    LispAxType.Int,
                    LispAxType.OptionalPoint,
                    LispAxType.OptionalPoint,
                    LispAxType.Any,
                    LispAxType.Any,
                },
                (o, a) =>
                {
                    Select(SetOf(o), a);
                    return null;
                }
            );
            Method("SelectAtPoint", set, 1, new[] { LispAxType.Point, LispAxType.Any, LispAxType.Any }, (o, a) =>
            {
                SelectAtPoint(SetOf(o), a);
                return null;
            });
            Method("Highlight", set, 1, new[] { LispAxType.Bool }, (o, a) => null);
        }

        private void RegisterColorMembers()
        {
            Func<LispVlaObject, bool> color = OfKinds(LispVlaKind.Color);
            LispAxColor Held(LispVlaObject o) => (LispAxColor)o.State;

            Property(
                "ColorIndex",
                color,
                o => (int)Held(o).Color.ColorIndex,
                LispAxType.Int,
                (o, v) => Held(o).Color = ColorOfIndex((int)v)
            );
            Property(
                "ColorMethod",
                color,
                o => (int)Held(o).Color.ColorMethod,
                LispAxType.Int,
                (o, v) => Held(o).Color = ColorOfMethod(Held(o).Color, (int)v)
            );
            Property("Red", color, o => Held(o).Color.IsByColor ? (int)Held(o).Color.Red : 0);
            Property("Green", color, o => Held(o).Color.IsByColor ? (int)Held(o).Color.Green : 0);
            Property("Blue", color, o => Held(o).Color.IsByColor ? (int)Held(o).Color.Blue : 0);
            Property(
                "EntityColor",
                color,
                o => EntityColorOf(Held(o).Color),
                LispAxType.Int,
                (o, v) => Held(o).Color = ColorOfEntityColor((int)v)
            );
            Method("SetRGB", color, 3, new[] { LispAxType.Int, LispAxType.Int, LispAxType.Int }, (o, a) =>
            {
                int red = (int)a[0];
                int green = (int)a[1];
                int blue = (int)a[2];

                if (red < 0 || red > 255 || green < 0 || green > 255 || blue < 0 || blue > 255)
                {
                    throw Automation("Invalid argument");
                }

                Held(o).Color = DxfColor.FromRgb((byte)red, (byte)green, (byte)blue);
                return null;
            });
        }

        #endregion

        #region Colors

        /// <summary>An AcCmColor holding a copy of a color: a new value at each read, as COM gives a copy (D211).</summary>
        internal static LispVlaObject ColorObject(DxfColor color) =>
            new LispVlaObject(LispVlaKind.Color, "IAcadAcCmColor", "") { State = new LispAxColor(color) };

        private static DxfColor ColorOfIndex(int index)
        {
            if (index < 0 || index > 256)
            {
                throw Automation("Invalid argument");
            }

            return DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);
        }

        private static DxfColor ColorOfMethod(DxfColor current, int method)
        {
            switch ((DxfColorMethod)method)
            {
                case DxfColorMethod.ByLayer:
                    return DxfColor.ByLayer;
                case DxfColorMethod.ByBlock:
                    return DxfColor.ByBlock;
                case DxfColorMethod.ByAci:
                    return DxfColor.FromColorIndex(DxfColorMethod.ByAci, current.ColorIndex);
                case DxfColorMethod.ByColor:
                    return DxfColor.FromRgb(current.Red, current.Green, current.Blue);
                default:
                    throw Automation("Invalid argument");
            }
        }

        /// <summary>The EntityColor of a color: the method in the top byte (0xC0 by layer, 0xC1 by block, 0xC2 true color, 0xC3 index) and the index or the red, green and blue below it.</summary>
        private static int EntityColorOf(DxfColor color)
        {
            switch (color.ColorMethod)
            {
                case DxfColorMethod.ByLayer:
                    return unchecked((int)0xC0000000);
                case DxfColorMethod.ByBlock:
                    return unchecked((int)0xC1000000);
                case DxfColorMethod.ByColor:
                    return unchecked((int)(0xC2000000 | (uint)color.ColorValue));
                default:
                    return unchecked((int)(0xC3000000 | (uint)(color.ColorIndex & 0xFF)));
            }
        }

        private static DxfColor ColorOfEntityColor(int value)
        {
            uint bits = unchecked((uint)value);
            int low = (int)(bits & 0xFFFFFF);

            switch (bits >> 24)
            {
                case 0xC0:
                    return DxfColor.ByLayer;
                case 0xC1:
                    return DxfColor.ByBlock;
                case 0xC2:
                    return DxfColor.FromRgb((byte)(low >> 16), (byte)(low >> 8), (byte)low);
                case 0xC3:
                    return ColorOfIndex(low & 0xFF);
                default:
                    throw Automation("Invalid argument");
            }
        }

        #endregion

        #region Document

        /// <summary>The block of the current layout; the last layout's while model space is current (D219).</summary>
        private DxfBlockTableRecord PaperSpaceBlock()
        {
            CadSpace current = Document.CurrentSpace;

            if (current != null && !current.IsModel)
            {
                return current.Block;
            }

            for (int i = Document.Spaces.Count - 1; i >= 0; i--)
            {
                if (!Document.Spaces[i].IsModel)
                {
                    return Document.Spaces[i].Block;
                }
            }

            return Document.Database.PaperSpace;
        }

        private void SetActiveLayer(LispVlaObject layer)
        {
            if (layer.Kind != LispVlaKind.Record || layer.InterfaceName != "IAcadLayer")
            {
                throw Mismatch();
            }

            string name = RecordOf(layer).Name;
            CadSystemVariable variable = LispDrawing.Named(Session, "CLAYER");

            if (variable == null)
            {
                throw Automation("Invalid argument");
            }

            Session.OpenLispStep();
            Session.HoldLispStep();

            if (!variable.SetFromLisp(Session, name))
            {
                throw Automation("Invalid argument");
            }
        }

        private List<string> LivePrevious()
        {
            var live = new List<string>();

            foreach (string handle in Session.PreviousSelection)
            {
                if (Document.FindEntity(handle) != null)
                {
                    live.Add(handle);
                }
            }

            return live;
        }

        /// <summary>The handles of the pick-first selection, in the order of their numbers, as <c>ssgetfirst</c> gives them.</summary>
        private List<string> PickfirstHandles()
        {
            var numbered = new List<KeyValuePair<ulong, string>>();

            foreach (string handle in Document.Selection.Handles)
            {
                if (Document.FindEntity(handle) == null)
                {
                    continue;
                }

                ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value);
                numbered.Add(new KeyValuePair<ulong, string>(value, handle));
            }

            numbered.Sort(
                (x, y) =>
                {
                    int order = x.Key.CompareTo(y.Key);
                    return order != 0 ? order : string.CompareOrdinal(x.Value, y.Value);
                }
            );

            var handles = new List<string>(numbered.Count);

            foreach (KeyValuePair<ulong, string> pair in numbered)
            {
                handles.Add(pair.Value);
            }

            return handles;
        }

        #endregion
    }
}
