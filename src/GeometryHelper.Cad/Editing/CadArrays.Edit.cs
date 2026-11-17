using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    internal static partial class CadArrays
    {
        /// <summary>Whether the drawing can hold an associative array: an OBJECTS section, handles, and the file version that brought associations (R2010, AC1024).</summary>
        internal static bool CanTie(DxfDatabase database) => CadCenterMarks.CanTie(database);

        /// <summary>
        /// Makes an associative array of source objects: a block of the sources as they lie from the base point (turned by
        /// <see cref="CadAssocArray.SourceTurn"/>), a block of the items, the INSERT of that in the space of the first source,
        /// and the objects of the network. The sources are taken out of the space, as AutoCAD's command takes them into the
        /// array. Returns the INSERT.
        /// </summary>
        /// <param name="s">The edit that makes it.</param>
        /// <param name="space">The space.</param>
        /// <param name="array">The parameters.</param>
        /// <param name="sources">The objects arrayed, in the space.</param>
        /// <param name="look">Gives the INSERT the layer and properties new objects take.</param>
        internal static DxfBlockReference Make(
            CadEditScope s,
            CadSpace space,
            CadAssocArray array,
            IReadOnlyList<DxfEntity> sources,
            Action<DxfBlockReference> look
        )
        {
            CheckSize(array);
            DxfDatabase database = s.Database;
            GeoPoint3 baseAt = array.BasePoint;
            GeoTransform3 toSource =
                GeoTransform3.RotationZ(array.SourceTurn())
                * GeoTransform3.Translation(new GeoVector3(-baseAt.X, -baseAt.Y, -baseAt.Z));
            var source = new DxfBlockTableRecord { Name = "*U", MadeFlags = 1 };
            database.BlockTable.AddAnonymous(source);

            foreach (DxfEntity entity in sources)
            {
                DxfEntity copy = entity.GetTransformedCopy(toSource);
                CadOwnerTag.Remove(copy);
                source.AppendEntity(copy);
            }

            var block = new DxfBlockTableRecord
            {
                Name = "*U",
                MadeFlags = 1,
                BlockScaling = DxfBlockScaling.Uniform,
            };
            database.BlockTable.AddAnonymous(block);
            List<CadArrayItem> items = array.MakeItems();
            AddItems(block, source, items);

            var insert = new DxfBlockReference(
                array.Kind == CadArrayKind.Path ? GeoPoint3.Origin : array.Origin,
                block
            );
            look?.Invoke(insert);
            s.Append(space, insert);
            WriteObjects(database, space, array, insert, source, block, items);
            return insert;
        }

        /// <summary>Refuses an array of more items than AutoCAD 2021 makes.</summary>
        private static void CheckSize(CadAssocArray array)
        {
            if (array.TotalItems > CadAssocArray.MostItems)
            {
                throw new InvalidOperationException(
                    "An array of more than " + CadAssocArray.MostItems + " items is too large."
                );
            }
        }

        /// <summary>Puts an INSERT of the source block in the block of items for each item, ByBlock in everything, the matrix of the item applied.</summary>
        private static void AddItems(
            DxfBlockTableRecord block,
            DxfBlockTableRecord source,
            IReadOnlyList<CadArrayItem> items
        )
        {
            foreach (CadArrayItem item in items)
            {
                var one = new DxfBlockReference(GeoPoint3.Origin, source)
                {
                    Color = DxfColor.ByBlock,
                    Linetype = "ByBlock",
                    LineWeight = DxfLineWeight.ByBlock,
                    Transparency = DxfTransparency.ByBlock,
                };
                one.TransformBy(item.Matrix);
                block.AppendEntity(one);
                item.Handle = one.Handle;
            }
        }

        /// <summary>
        /// Gives the copy of an array's INSERT blocks of its own, as AutoCAD's COPY makes a new array of an array: a copy of the
        /// source objects, a block of items, and for a rectangular or polar array the objects of the network too, so that the
        /// copy is an associative array and a change of the one leaves the other as it is. A path array's copy keeps its picture
        /// in blocks of its own and is no longer tied to the path.
        /// </summary>
        /// <param name="s">The edit that makes the copy.</param>
        /// <param name="space">The space the copy is in.</param>
        /// <param name="original">The INSERT copied.</param>
        /// <param name="copy">Its copy, which still places the blocks of the original.</param>
        internal static void Reassociate(
            CadEditScope s,
            CadSpace space,
            DxfBlockReference original,
            DxfBlockReference copy
        )
        {
            DxfDatabase database = s.Database;
            Record record = Find(database, original);
            CadAssocArray array = record == null ? null : ReadArray(s.Document, record, out _);

            if (array == null)
            {
                return;
            }

            ReadArray(s.Document, record, out List<CadArrayItem> items);
            var source = new DxfBlockTableRecord { Name = "*U", MadeFlags = 1 };
            database.BlockTable.AddAnonymous(source);

            foreach (DxfEntity entity in SourcesOf(original))
            {
                source.AppendEntity(entity.Clone());
            }

            var block = new DxfBlockTableRecord
            {
                Name = "*U",
                MadeFlags = 1,
                BlockScaling = DxfBlockScaling.Uniform,
            };
            database.BlockTable.AddAnonymous(block);
            AddItems(block, source, items);
            copy.BlockTableRecord = block;

            if (array.Kind != CadArrayKind.Path)
            {
                WriteObjects(database, space, array, copy, source, block, items);
            }
        }

        /// <summary>The associative array an INSERT of a handle is, read from the drawing; null where the entity is none.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handle">The INSERT's handle.</param>
        internal static CadAssocArray TryRead(CadDocument document, string handle)
        {
            DxfEntity entity = document.FindEntity(handle);
            Record record = entity == null ? null : Find(document.Database, entity);
            return record == null ? null : ReadArray(document, record, out _);
        }

        /// <summary>
        /// Changes the parameters of the array of an INSERT and writes it again, one undo step named ARRAYEDIT: what the Array
        /// tab and ARRAYEDIT do. Refused where the INSERT is no associative array.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handle">The INSERT's handle.</param>
        /// <param name="change">Changes the parameters it is given.</param>
        internal static CadEditResult Edit(CadDocument document, string handle, Action<CadAssocArray> change)
        {
            DxfEntity entity = document.FindEntity(handle);
            Record record = entity == null ? null : Find(document.Database, entity);
            CadAssocArray array = record == null ? null : ReadArray(document, record, out _);

            if (array == null)
            {
                return CadEditResult.Refuse("The object is not an associative array.");
            }

            double turn = array.SourceTurn();
            change(array);
            return document.ApplyEdit(
                "ARRAYEDIT",
                new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "ARRAYEDIT") },
                s =>
                {
                    var now = (DxfBlockReference)s.Document.FindEntity(handle);
                    Rebuild(s.Document, Find(s.Database, now), array, now, turn);
                }
            );
        }

        /// <summary>The block that holds the source objects of an array: the block its item INSERTs name.</summary>
        internal static DxfBlockTableRecord SourceBlock(DxfBlockReference insert)
        {
            foreach (DxfEntity entity in insert.BlockTableRecord)
            {
                if (entity is DxfBlockReference one && !one.IsErased)
                {
                    return one.BlockTableRecord;
                }
            }

            return null;
        }

        /// <summary>The source objects of an array.</summary>
        internal static List<DxfEntity> SourcesOf(DxfBlockReference insert)
        {
            DxfBlockTableRecord source = SourceBlock(insert);
            return source == null ? new List<DxfEntity>() : source.Where(e => !e.IsErased).ToList();
        }

        /// <summary>
        /// The array an INSERT is, read back from the drawing: its kind, parameters and items as the action and its body hold
        /// them. Null where the INSERT is no array, or its parameters are not ones this reads (a kind it does not know).
        /// </summary>
        internal static CadAssocArray ReadArray(CadDocument document, Record record, out List<CadArrayItem> items)
        {
            items = new List<CadArrayItem>();
            DxfFile file = document.Database.File;
            IReadOnlyList<DxfTypedValue> groups = record.Body.Groups;
            int at = CadCenterMarks.Start(groups, "AcDbAssocArrayActionBody");

            if (at < 0 || at + 4 >= groups.Count || groups[at + 1].TypeCode != 1)
            {
                return null;
            }

            string name = groups[at + 1].Value.Trim();
            var array = new CadAssocArray();

            switch (name)
            {
                case "AcDbAssocArrayRectangularParameters":
                    array.Kind = CadArrayKind.Rectangular;
                    break;
                case "AcDbAssocArrayPolarParameters":
                    array.Kind = CadArrayKind.Polar;
                    break;
                case "AcDbAssocArrayPathParameters":
                    array.Kind = CadArrayKind.Path;
                    break;
                default:
                    return null;
            }

            Dictionary<string, double> values = ValuesOf(record.Action);
            double Number(string key, double fallback) => values.TryGetValue(key, out double v) ? v : fallback;
            array.Rows = (int)Math.Round(Number("Rows", 1.0));
            array.RowSpacing = Number("RowSpacing", 1.0);
            array.RowElevation = Number("RowElevation", 0.0);
            array.Levels = (int)Math.Round(Number("Levels", 1.0));
            array.LevelSpacing = Number("LevelSpacing", 1.0);

            switch (array.Kind)
            {
                case CadArrayKind.Rectangular:
                    array.Columns = (int)Math.Round(Number("Items", 1.0));
                    array.ColumnSpacing = Number("ItemSpacing", 1.0);
                    array.AxesAngle = Number("AxesAngle", Math.PI / 2.0);
                    array.RowAngle = ProfileAngle(record);
                    break;
                case CadArrayKind.Polar:
                    array.Items = (int)Math.Round(Number("Items", 1.0));
                    array.Between = Math.Abs(Number("ItemSpacing", 0.0));
                    array.Clockwise = Number("Direction", 1.0) < 0.5;
                    array.Rotate = Number("AlignItems", 1.0) > 0.5;
                    double radius = Number("Radius", 0.0);
                    double start = Number("StartAngle", 0.0);
                    array.BasePoint = new GeoPoint3(radius * Math.Cos(start), radius * Math.Sin(start), 0.0);
                    break;
                default:
                    array.Items = (int)Math.Round(Number("Items", 1.0));
                    array.Spacing = Number("ItemSpacing", 1.0);
                    array.Align = Number("AlignItems", 1.0) > 0.5;
                    array.MaintainZ = Number("MaintainZ", 1.0) > 0.5;
                    array.Tangent = Number("Orientation", 1.0) > 1.5 ? CadPathTangent.Normal : CadPathTangent.Path;
                    array.TangentAngle = Number("TangentOrientationAngle", 0.0);

                    if (array.Tangent == CadPathTangent.Path && Math.Abs(array.TangentAngle) > 1E-9)
                    {
                        array.Tangent = CadPathTangent.Vector;
                    }

                    array.Divide = Number("Method", 1.0) < 0.5;
                    array.FromEnd = Number("PathDirection", 1.0) < 0.5;

                    if (!(document.FindEntity(record.PathHandle) is DxfCurve path))
                    {
                        return null;
                    }

                    array.Path = path;
                    array.PathLength = CurveLength(path);
                    array.PathClosed =
                        path is DxfCircle
                        || path.Closed
                        || path.StartPoint.DistanceTo(path.EndPoint) <= 1E-10 * Math.Max(1.0, array.PathLength);
                    array.BasePoint = array.FirstPoint();
                    break;
            }

            // The items, with their handles and matrices as the body keeps them.
            for (int i = at + 5; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 100 || groups[i].Value.Trim() != "AcDbAssocArrayItem")
                {
                    continue;
                }

                if (i + 5 >= groups.Count)
                {
                    break;
                }

                var item = new CadArrayItem
                {
                    Index = WholeAt(groups, i + 2),
                    Row = WholeAt(groups, i + 3),
                    Level = WholeAt(groups, i + 4),
                };
                int flags = WholeAt(groups, i + 5);

                if (flags != 8 && flags != 12)
                {
                    // An item AutoCAD changed by itself (erased, replaced or moved on its own) is kept in a way not read here.
                    return null;
                }

                int k = i + 6;
                var m = new double[4, 4];
                m[0, 0] = m[1, 1] = m[2, 2] = m[3, 3] = 1.0;

                if (flags == 8)
                {
                    m[0, 3] = RealAt(groups, k);
                    m[1, 3] = RealAt(groups, k + 1);
                    m[2, 3] = RealAt(groups, k + 2);
                    k += 3;
                }
                else
                {
                    for (int n = 0; n < 16; n++)
                    {
                        m[n / 4, n % 4] = RealAt(groups, k + n);
                    }

                    k += 16;
                }

                item.Matrix = new GeoTransform3(m);
                item.Handle = k < groups.Count && groups[k].TypeCode == 330 ? groups[k].Value.Trim() : null;
                items.Add(item);
            }

            return array;
        }

        private static int WholeAt(IReadOnlyList<DxfTypedValue> groups, int at) =>
            at < groups.Count
            && int.TryParse(groups[at].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : 0;

        private static double RealAt(IReadOnlyList<DxfTypedValue> groups, int at) =>
            at < groups.Count
            && double.TryParse(groups[at].Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : 0.0;

        /// <summary>The values of an action by name: a whole number or a real, whichever its value group is.</summary>
        private static Dictionary<string, double> ValuesOf(DxfItem action)
        {
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            IReadOnlyList<DxfTypedValue> groups = action.Groups;

            for (int i = 0; i + 3 < groups.Count; i++)
            {
                if (groups[i].TypeCode == 1 && groups[i + 1].TypeCode == 90 && groups[i + 2].TypeCode == 90)
                {
                    values[groups[i].Value] = RealAt(groups, i + 3);
                }
            }

            return values;
        }

        /// <summary>The angle of the row profile of a rectangular array: the way the line from the origin points; nought where it has none.</summary>
        private static double ProfileAngle(Record record)
        {
            if (record.ProfileEdge == null)
            {
                return 0.0;
            }

            IReadOnlyList<DxfTypedValue> groups = record.ProfileEdge.Groups;
            int at = CadCenterMarks.Start(groups, "AcDbAssocEdgeActionParam");

            // 90 0, 330, 290 1, 90 kind, then the two points.
            if (at < 0 || at + 9 >= groups.Count)
            {
                return 0.0;
            }

            double x = RealAt(groups, at + 7);
            double y = RealAt(groups, at + 8);
            return Math.Sqrt(x * x + y * y) > 1E-12 ? Math.Atan2(y, x) : 0.0;
        }

        private static double CurveLength(DxfCurve curve)
        {
            try
            {
                return curve.GetDistanceAtParameter(curve.EndParam);
            }
            catch (Exception e)
                when (e is InvalidOperationException || e is ArgumentException || e is NotSupportedException)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Takes the objects of an array out of the network, as AutoCAD does where its INSERT is erased or the array is made
        /// plain: the reactors they set on the INSERT, the blocks and the path taken off, the dependencies linked out of the
        /// chains on those objects, the action taken out of its network and its objects erased, and the network and the
        /// dictionaries round it where it was the last. The INSERT and the blocks stay.
        /// </summary>
        internal static void Release(CadDocument document, Record record)
        {
            DxfFile file = document.Database.File;

            foreach (DxfItem dependency in record.Dependencies)
            {
                if (dependency.IsRemoved)
                {
                    continue;
                }

                string target = ObjectOf(dependency);
                int at = CadCenterMarks.Start(dependency.Groups, "AcDbAssocDependency");

                if (at >= 0 && at + 10 < dependency.Groups.Count)
                {
                    string previous = dependency.Groups[at + 9].Value.Trim();
                    string next = dependency.Groups[at + 10].Value.Trim();
                    CadCenterMarks.Relink(file, previous, 10, next);
                    CadCenterMarks.Relink(file, next, 9, previous);
                }

                if (target != null && file.ObjectItem(target) is DxfItem on && !on.IsRemoved)
                {
                    file.SetReactor(on, dependency.Handle, false);
                }
            }

            DxfItem network = file.ObjectItem(file.OwnerOf(record.Action) ?? string.Empty);

            if (network != null && !network.IsRemoved)
            {
                CadCenterMarks.Unlist(file, network, record.Action.Handle);
            }

            var doomed = new List<DxfItem>(record.Dependencies);
            doomed.AddRange(record.Parameters);
            doomed.Add(record.Body);

            foreach (DxfItem item in doomed)
            {
                if (item != null && !item.IsRemoved)
                {
                    file.RemoveRecord(item);
                }
            }

            if (!record.Action.IsRemoved)
            {
                file.RemoveRecord(record.Action);
            }

            if (network != null && !network.IsRemoved && CadCenterMarks.ActionsOf(network).Count == 0)
            {
                CadCenterMarks.ReleaseNetwork(document, file, network);
            }
        }

        /// <summary>
        /// Writes the array again after its parameters were changed: the sources turned where the way they are held changed
        /// (the path array's Align items or tangent), the block of items made again, and the objects of the network written
        /// anew over the same INSERT and blocks, so that the INSERT keeps its handle, place and properties. Returns the new record.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="record">The array as it was.</param>
        /// <param name="array">The array as it is to be.</param>
        /// <param name="insert">The INSERT.</param>
        /// <param name="turnBefore">The turn the sources were held by (<see cref="CadAssocArray.SourceTurn"/> of the old parameters).</param>
        internal static Record Rebuild(
            CadDocument document,
            Record record,
            CadAssocArray array,
            DxfBlockReference insert,
            double turnBefore
        )
        {
            CheckSize(array);
            DxfDatabase database = document.Database;
            DxfBlockTableRecord block = insert.BlockTableRecord;
            DxfBlockTableRecord source = SourceBlock(insert);
            CadSpace space = document.FindSpace(insert.Handle) ?? document.ModelSpace;
            double turn = array.SourceTurn() - turnBefore;

            if (source != null && Math.Abs(turn) > 1E-12)
            {
                GeoTransform3 by = GeoTransform3.RotationZ(turn);

                foreach (DxfEntity entity in source.Where(e => !e.IsErased).ToList())
                {
                    entity.TransformBy(by);
                }
            }

            Release(document, record);

            foreach (DxfEntity old in block.Where(e => !e.IsErased).ToList())
            {
                old.Erase();
            }

            List<CadArrayItem> items = array.MakeItems();
            AddItems(block, source, items);
            return WriteObjects(database, space, array, insert, source, block, items);
        }

        /// <summary>
        /// The pieces an array explodes into: a copy of each source object for each item, in the order of the items, put where
        /// the item stands in the drawing. A rectangular array of 3 columns and 2 rows of one circle gives six circles.
        /// </summary>
        internal static List<DxfEntity> Explode(CadDocument document, Record record, DxfBlockReference insert)
        {
            var pieces = new List<DxfEntity>();
            CadAssocArray array = ReadArray(document, record, out List<CadArrayItem> items);

            if (array == null)
            {
                return pieces;
            }

            List<DxfEntity> sources = SourcesOf(insert);
            GeoTransform3 world = insert.BlockTransform;

            foreach (CadArrayItem item in items)
            {
                GeoTransform3 move = world * item.Matrix;

                foreach (DxfEntity entity in sources)
                {
                    pieces.Add(entity.GetTransformedCopy(move));
                }
            }

            return pieces;
        }

        /// <summary>
        /// Follows an edit: an array whose INSERT was moved, turned or scaled has its transformation kept in the body (where
        /// a path array is disassociated, as its items stand on the path and not on the INSERT); one whose INSERT or path
        /// curve was erased is taken out of the network; one whose path curve was changed is written again from the
        /// curve as it is now. Returns the records of the INSERTs it changed, for the edit's step.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();

            if (!Holds(document.Database))
            {
                return followed;
            }

            DxfFile file = document.Database.File;
            var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var erased = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null || record.Property == "ARRAYEDIT")
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                            moved.Add(record.Handle);
                            changed.Add(record.Handle);
                            break;
                        case CadEditRecordKind.Erased:
                            erased.Add(record.Handle);
                            break;
                    }
                }
            }

            var records2 = new SortedDictionary<string, Record>(StringComparer.Ordinal);

            foreach (string handle in changed.Concat(erased).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DxfEntity entity = document.FindKept(handle);

                if (entity == null)
                {
                    continue;
                }

                foreach (string reactor in entity.GetPersistentReactorIds())
                {
                    DxfItem dependency = file.ObjectItem(reactor);
                    DxfItem action =
                        dependency == null || dependency.IsRemoved
                            ? null
                            : file.ObjectItem(file.OwnerOf(dependency) ?? string.Empty);

                    if (action == null || action.IsRemoved || action.Type != "ACDBASSOCACTION")
                    {
                        continue;
                    }

                    Record found = Read(file, action);

                    if (found != null)
                    {
                        records2[action.Handle] = found;
                    }
                }
            }

            foreach (Record record in records2.Values)
            {
                string space = document.FindSpace(record.InsertHandle)?.Name;

                if (
                    erased.Contains(record.InsertHandle)
                    || (record.PathHandle != null && erased.Contains(record.PathHandle))
                    || (record.PathHandle != null && moved.Contains(record.InsertHandle))
                )
                {
                    Release(document, record);
                    continue;
                }

                if (!(document.FindEntity(record.InsertHandle) is DxfBlockReference insert))
                {
                    continue;
                }

                if (record.PathHandle != null && moved.Contains(record.PathHandle))
                {
                    CadAssocArray array = ReadArray(document, record, out _);

                    if (array != null)
                    {
                        double turn = array.SourceTurn();
                        Rebuild(document, record, array, insert, turn);
                        followed.Add(new CadEditRecord(CadEditRecordKind.Reshaped, insert.Handle) { Space = space });
                    }

                    continue;
                }

                if (moved.Contains(record.InsertHandle))
                {
                    KeepTransform(file, record, insert);
                }
            }

            return followed;
        }

        /// <summary>The transformation of the INSERT as the body of the array keeps it, its last 16 numbers by rows; the identity where it has none.</summary>
        internal static GeoTransform3 StoredTransform(Record record)
        {
            IReadOnlyList<DxfTypedValue> groups = record.Body.Groups;
            int end = groups.Count;

            while (end > 0 && groups[end - 1].TypeCode != 40)
            {
                end--;
            }

            var m = new double[4, 4];

            if (end - 16 < 0)
            {
                return GeoTransform3.Identity;
            }

            for (int n = 0; n < 16; n++)
            {
                m[n / 4, n % 4] = RealAt(groups, end - 16 + n);
            }

            return new GeoTransform3(m);
        }

        /// <summary>Writes the transformation of the INSERT over the 16 numbers that end the body, as AutoCAD keeps it when its array is moved.</summary>
        private static void KeepTransform(DxfFile file, Record record, DxfBlockReference insert)
        {
            IReadOnlyList<DxfTypedValue> groups = record.Body.Groups;
            int end = groups.Count;

            while (end > 0 && groups[end - 1].TypeCode != 40)
            {
                end--;
            }

            int start = end - 16;

            if (start < 0)
            {
                return;
            }

            GeoTransform3 matrix = insert.BlockTransform;

            for (int n = 0; n < 16; n++)
            {
                file.SetValueAt(record.Body, start + n, Numbers.Exact(matrix[n / 4, n % 4] + 0.0));
            }
        }
    }
}
