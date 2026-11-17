using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// ARRAYEDIT as AutoCAD 2021 asks it on the command line (probed through its console): the array is picked ("Select
    /// array:", and "Object selected is not an associative array." for any other object), then the options of its kind are
    /// offered until eXit or Enter, each change made at once to the array's parameters and the items made again. A
    /// rectangular array of 3 columns 20 apart edited to 5 columns 40 apart has 5 columns at 0, 40, 80, 120 and 160.
    /// Source (editing the source objects in place) and the edit of single items are not built.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] ArrayEditRectangular =
        {
            "Source",
            "REPlace",
            "Base point",
            "Rows",
            "Columns",
            "Levels",
            "RESet",
            "eXit",
        };

        private static readonly string[] ArrayEditPolar =
        {
            "Source",
            "REPlace",
            "Base point",
            "Items",
            "Angle between",
            "Fill angle",
            "Rows",
            "Levels",
            "ROTate items",
            "RESet",
            "eXit",
        };

        private static readonly string[] ArrayEditPath =
        {
            "Source",
            "REPlace",
            "Method",
            "Base point",
            "Items",
            "Rows",
            "Levels",
            "Align items",
            "Z direction",
            "RESet",
            "eXit",
        };

        /// <summary>The centre of the box of the objects selected, the base point an array takes where none is given.</summary>
        private static GeoPoint3 CentreOf(CadCommandContext c)
        {
            GeoAabb3 box = Extents(c, c.Selected);
            return box.IsEmpty
                ? GeoPoint3.Origin
                : new GeoPoint3(
                    (box.Min.X + box.Max.X) / 2.0,
                    (box.Min.Y + box.Max.Y) / 2.0,
                    (box.Min.Z + box.Max.Z) / 2.0
                );
        }

        /// <summary>
        /// Makes the array the objects selected become, one undo step, as one associative array in the space of the first of
        /// them: its INSERT of an anonymous block, the objects taken into it. True where it was made; false where the
        /// objects give no array to make (they are not in a space).
        /// </summary>
        /// <param name="c">The command.</param>
        /// <param name="build">Gives the parameters; called once.</param>
        private static bool MakeAssociative(CadCommandContext c, Func<CadAssocArray> build)
        {
            IReadOnlyList<string> handles = c.Selected;

            if (handles.Count == 0 || c.Document.FindSpace(handles[0]) == null)
            {
                return false;
            }

            CadAssocArray array = build();
            var planned = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, handle));
            }

            c.Apply(planned, s =>
            {
                CadSpace space = s.Document.FindSpace(handles[0]);
                var sources = new List<DxfEntity>();

                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (entity != null)
                    {
                        sources.Add(entity);
                    }
                }

                CadArrays.Make(s, space, array, sources, insert => Current(c, insert));

                foreach (DxfEntity entity in sources)
                {
                    if (!s.IsHandled(entity.Handle))
                    {
                        entity.Erase();
                    }
                }
            });
            return true;
        }

        private static IEnumerable<CadPrompt> ArrayEdit(CadCommandContext c)
        {
            DxfBlockReference insert;
            CadArrays.Record record;
            CadAssocArray array;
            List<CadArrayItem> items;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select array");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                insert = c.Document.FindEntity(c.Input.Handles[0]) as DxfBlockReference;
                record = insert == null ? null : CadArrays.Find(c.Database, insert);
                array = record == null ? null : CadArrays.ReadArray(c.Document, record, out items);

                if (array != null)
                {
                    break;
                }

                c.Write("Object selected is not an associative array.");
                c.Write("1 was filtered out");
            }

            string handle = insert.Handle;
            double turnBefore = array.SourceTurn();
            var count = new Ref<int?>();
            var distance = new Ref<double?>();
            string[] options =
                array.Kind == CadArrayKind.Rectangular ? ArrayEditRectangular
                : array.Kind == CadArrayKind.Polar ? ArrayEditPolar
                : ArrayEditPath;

            // Writes the array as it stands now over the drawing's: the items made again, the objects of the network written anew.
            bool Commit(Action<DxfBlockReference> beforehand = null)
            {
                bool done = false;
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "ARRAYEDIT") }, s =>
                {
                    var now = s.Document.FindEntity(handle) as DxfBlockReference;
                    CadArrays.Record old = now == null ? null : CadArrays.Find(s.Database, now);

                    if (old == null)
                    {
                        return;
                    }

                    beforehand?.Invoke(now);
                    record = CadArrays.Rebuild(s.Document, old, array, now, turnBefore);
                    turnBefore = array.SourceTurn();
                    done = true;
                });

                if (!done)
                {
                    // The change was refused (too many items, say) and taken back: the parameters are those of the drawing again.
                    CadArrays.Record kept = CadArrays.Find(c.Database, c.Document.FindEntity(handle));
                    CadAssocArray back = kept == null ? null : CadArrays.ReadArray(c.Document, kept, out _);

                    if (back != null)
                    {
                        array = back;
                        record = kept;
                        turnBefore = array.SourceTurn();
                    }
                }

                return done;
            }

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", options, "eXit");

                if (c.Input.IsNone || c.Input.Is("eXit") || c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("Source"))
                {
                    c.Write("Editing the source objects in place is not built: use REPlace and Source objects.");
                    continue;
                }

                if (c.Input.Is("RESet"))
                {
                    Commit();
                    continue;
                }

                if (c.Input.Is("REPlace"))
                {
                    foreach (CadPrompt prompt in ArrayReplace(c, handle, () => record, () => array, () => turnBefore))
                    {
                        yield return prompt;
                    }

                    CadArrays.Record again = CadArrays.Find(c.Database, c.Document.FindEntity(handle));

                    if (again != null)
                    {
                        record = again;
                        array = CadArrays.ReadArray(c.Document, again, out _) ?? array;
                        turnBefore = array.SourceTurn();
                    }

                    continue;
                }

                if (c.Input.Is("Base point"))
                {
                    yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Key point" });

                    if (c.Input.IsPoint)
                    {
                        GeoPoint3 at = P3(c.Input.Point);
                        Commit(now => RebaseArray(now, array, at, turnBefore));
                    }

                    continue;
                }

                if (c.Input.Is("Rows"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of rows", array.Rows, count))
                    {
                        yield return prompt;
                    }

                    array.Rows = count.Value ?? array.Rows;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between rows",
                            "Enter total distance between start and end rows",
                            array.RowSpacing,
                            array.Rows,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    array.RowSpacing = distance.Value ?? array.RowSpacing;
                    yield return CadPrompt.ForDistance(
                        "Specify the incrementing elevation between rows",
                        null,
                        new[] { "Expresson" },
                        Length(array.RowElevation)
                    );
                    array.RowElevation = c.Input.Kind == CadInputKind.Number ? c.Input.Number : array.RowElevation;
                    Commit();
                    continue;
                }

                if (c.Input.Is("Levels"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of levels", array.Levels, count))
                    {
                        yield return prompt;
                    }

                    array.Levels = count.Value ?? array.Levels;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between levels",
                            "Enter total distance between start and end levels",
                            array.LevelSpacing,
                            array.Levels,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    array.LevelSpacing = distance.Value ?? array.LevelSpacing;
                    Commit();
                    continue;
                }

                if (c.Input.Is("Columns"))
                {
                    foreach (CadPrompt prompt in Count(c, "Enter the number of columns", array.Columns, count))
                    {
                        yield return prompt;
                    }

                    array.Columns = count.Value ?? array.Columns;

                    foreach (
                        CadPrompt prompt in Spacing(
                            c,
                            "Specify the distance between columns",
                            "Enter total distance between start and end columns",
                            array.ColumnSpacing,
                            array.Columns,
                            distance
                        )
                    )
                    {
                        yield return prompt;
                    }

                    array.ColumnSpacing = distance.Value ?? array.ColumnSpacing;
                    Commit();
                    continue;
                }

                if (c.Input.Is("Items") && array.Kind == CadArrayKind.Polar)
                {
                    double fill = array.Fill;

                    foreach (CadPrompt prompt in Count(c, "Enter number of items in array", array.Items, count))
                    {
                        yield return prompt;
                    }

                    array.Items = count.Value ?? array.Items;
                    array.Between = Math.Abs(StepOf(array.Items, fill));
                    Commit();
                    continue;
                }

                if (c.Input.Is("Angle between"))
                {
                    yield return CadPrompt
                        .ForAngle(
                            "Specify angle between items",
                            null,
                            new[] { "EXpression" },
                            Number(array.Between * 180.0 / Math.PI)
                        )
                        .Turning();

                    if (
                        c.Input.Kind == CadInputKind.Number
                        && Math.Abs(c.Input.Number) > Tolerance.Default.EqualAngleRad
                    )
                    {
                        array.Between = Math.Abs(c.Input.Number);
                        array.Clockwise = c.Input.Number < 0.0;
                        Commit();
                    }

                    continue;
                }

                if (c.Input.Is("Fill angle"))
                {
                    yield return CadPrompt
                        .ForAngle(
                            "Specify the angle to fill " + FillWays(c),
                            null,
                            new[] { "EXpression" },
                            Number(array.Fill * 180.0 / Math.PI)
                        )
                        .Turning();

                    if (
                        c.Input.Kind == CadInputKind.Number
                        && Math.Abs(c.Input.Number) > Tolerance.Default.EqualAngleRad
                    )
                    {
                        double step = StepOf(array.Items, c.Input.Number);
                        array.Between = Math.Abs(step);
                        array.Clockwise = step < 0.0;
                        Commit();
                    }

                    continue;
                }

                if (c.Input.Is("ROTate items"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Rotate arrayed items?",
                        new[] { "Yes", "No" },
                        array.Rotate ? "Yes" : "No"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        array.Rotate = c.Input.Is("Yes");
                        Commit();
                    }

                    continue;
                }

                if (c.Input.Is("Method"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter path method",
                        new[] { "Divide", "Measure" },
                        array.Divide ? "Divide" : "Measure"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        array.Divide = c.Input.Is("Divide");
                        array.Items = array.MostAlongPath(array.Spacing);
                        Commit();
                    }

                    continue;
                }

                if (c.Input.Is("Items"))
                {
                    if (array.Divide)
                    {
                        foreach (CadPrompt prompt in Count(c, "Enter number of items along path", array.Items, count))
                        {
                            yield return prompt;
                        }

                        array.Items = count.Value ?? array.Items;
                        Commit();
                        continue;
                    }

                    yield return CadPrompt.ForDistance(
                        "Specify the distance between items along path",
                        null,
                        new[] { "Expression" },
                        Length(array.Spacing)
                    );

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > c.Session.Tolerance.EqualPoint)
                    {
                        array.Spacing = c.Input.Number;
                    }

                    int most = array.MostAlongPath(array.Spacing);
                    c.Write("Maximum items = " + most.ToString(CultureInfo.InvariantCulture));
                    yield return CadPrompt
                        .ForInteger(
                            "Specify number of items",
                            new[] { "Fill entire path", "Expression" },
                            most.ToString(CultureInfo.InvariantCulture)
                        )
                        .Refusing("Requires a positive nonzero integer or option keyword.");
                    array.Items =
                        c.Input.Kind == CadInputKind.Number && c.Input.Number >= 1.0
                            ? (int)Math.Min(c.Input.Number, most)
                            : most;
                    Commit();
                    continue;
                }

                if (c.Input.Is("Align items"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Align arrayed items to path?",
                        new[] { "Yes", "No" },
                        array.Align ? "Yes" : "No"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        array.Align = c.Input.Is("Yes");
                        Commit();
                    }

                    continue;
                }

                if (c.Input.Is("Z direction"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Maintain Z direction for all items in the array?",
                        new[] { "Yes", "No" },
                        array.MaintainZ ? "Yes" : "No"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        array.MaintainZ = c.Input.Is("Yes");
                        Commit();
                    }

                    continue;
                }
            }
        }

        /// <summary>
        /// Re-anchors the array on a new base point, the sources held from it: a rectangular array's INSERT is moved to the
        /// point and the sources kept where they stand (the items do not move), a polar array's radius and start angle are
        /// those of the point and the sources keep their place from it, and a path array's sources are held from the point,
        /// which the first item then puts on the path.
        /// </summary>
        private static void RebaseArray(DxfBlockReference insert, CadAssocArray array, GeoPoint3 at, double turnBefore)
        {
            GeoTransform3 world = insert.BlockTransform;
            GeoPoint3 local = array.Kind == CadArrayKind.Path ? at : world.Inverse().Transform(at);
            List<DxfEntity> sources = CadArrays.SourcesOf(insert);

            switch (array.Kind)
            {
                case CadArrayKind.Rectangular:
                    foreach (DxfEntity entity in sources)
                    {
                        entity.TransformBy(GeoTransform3.Translation(new GeoVector3(-local.X, -local.Y, -local.Z)));
                    }

                    insert.Position = at;
                    break;
                case CadArrayKind.Polar:
                    GeoPoint3 old = array.BasePoint;

                    foreach (DxfEntity entity in sources)
                    {
                        entity.TransformBy(
                            GeoTransform3.Translation(new GeoVector3(old.X - local.X, old.Y - local.Y, old.Z - local.Z))
                        );
                    }

                    array.BasePoint = local;
                    break;
                default:
                {
                    List<CadArrayItem> items = array.MakeItems();
                    GeoTransform3 first = items.Count > 0 ? items[0].Matrix : GeoTransform3.Identity;
                    GeoTransform3 to =
                        GeoTransform3.RotationZ(turnBefore)
                        * GeoTransform3.Translation(new GeoVector3(-at.X, -at.Y, -at.Z))
                        * first;

                    foreach (DxfEntity entity in sources)
                    {
                        entity.TransformBy(to);
                    }

                    array.BasePoint = at;
                    break;
                }
            }
        }

        /// <summary>
        /// The REPlace option: replacement objects picked, their base point, then "Source objects" puts them in the place of the
        /// source objects of every item, held from the array's base point as the sources were; picking one item to replace
        /// is not built.
        /// </summary>
        private static IEnumerable<CadPrompt> ArrayReplace(
            CadCommandContext c,
            string handle,
            Func<CadArrays.Record> record,
            Func<CadAssocArray> array,
            Func<double> turnBefore
        )
        {
            foreach (
                CadPrompt prompt in c.SelectObjectsOf("Select replacement objects", e =>
                    e != null && !string.Equals(e.Handle, handle, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                yield return prompt;
            }

            List<string> chosen = c
                .Selected.Where(h => !string.Equals(h, handle, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (chosen.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint(
                "Select base point of replacement objects",
                null,
                new[] { "Key Point" },
                "centroid"
            );

            GeoPoint3 base3 = c.Input.IsPoint ? P3(c.Input.Point) : CentreOfHandles(c, chosen);

            while (true)
            {
                yield return CadPrompt.ForEntity("Select an item in the array to replace", new[] { "Source objects" });

                if (c.Input.Is("Source objects"))
                {
                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                c.Write("Replacing a single item is not built: use Source objects.");
            }

            CadAssocArray model = array();
            double before = turnBefore();
            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(CadEditRecordKind.Reshaped, handle, "ARRAYEDIT"),
            };

            foreach (string h in chosen)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, h));
            }

            c.Apply(planned, s =>
            {
                var now = s.Document.FindEntity(handle) as DxfBlockReference;
                CadArrays.Record old = now == null ? null : CadArrays.Find(s.Database, now);

                if (old == null)
                {
                    return;
                }

                DxfBlockTableRecord source = CadArrays.SourceBlock(now);
                GeoTransform3 world = now.BlockTransform;
                GeoTransform3 into =
                    GeoTransform3.RotationZ(model.SourceTurn())
                    * GeoTransform3.Translation(new GeoVector3(-base3.X, -base3.Y, -base3.Z));

                if (model.Kind != CadArrayKind.Path)
                {
                    // Held in the frame of the INSERT: the replacement objects' base point lands on the array's.
                    GeoPoint3 there = world.Transform(
                        model.Kind == CadArrayKind.Polar ? model.BasePoint : GeoPoint3.Origin
                    );
                    into =
                        world.Inverse()
                        * GeoTransform3.Translation(
                            new GeoVector3(there.X - base3.X, there.Y - base3.Y, there.Z - base3.Z)
                        );
                }

                foreach (DxfEntity entity in source.Where(e => !e.IsErased).ToList())
                {
                    entity.Erase();
                }

                foreach (string h in chosen)
                {
                    DxfEntity entity = s.Document.FindEntity(h);

                    if (entity == null)
                    {
                        continue;
                    }

                    DxfEntity copy = entity.GetTransformedCopy(into);
                    CadOwnerTag.Remove(copy);
                    source.AppendEntity(copy);
                    entity.Erase();
                }

                CadArrays.Rebuild(s.Document, old, model, now, before);
            });
        }

        private static GeoPoint3 CentreOfHandles(CadCommandContext c, IReadOnlyList<string> handles)
        {
            GeoAabb3 box = Extents(c, handles);
            return box.IsEmpty
                ? GeoPoint3.Origin
                : new GeoPoint3(
                    (box.Min.X + box.Max.X) / 2.0,
                    (box.Min.Y + box.Max.Y) / 2.0,
                    (box.Min.Z + box.Max.Z) / 2.0
                );
        }
    }
}
