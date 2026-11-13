using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        private const string DimensionContextMarker = "AcDbDimensionObjectContextData";

        // The highest number of an entry *A of ACDB_ANNOTATIONSCALES each object has used: AutoCAD does not give a number again
        // within a session, so the entry added after *A2 was deleted is *A3. Kept by the dictionary, not saved, as AutoCAD's.
        private static readonly ConditionalWeakTable<DxfItem, StrongBox<int>> ContextCounters =
            new ConditionalWeakTable<DxfItem, StrongBox<int>>();

        /// <summary>
        /// How AutoCAD 2021 writes the context of a dimension of a kind (probed on a DIMLINEAR, DIMALIGNED, DIMRADIUS,
        /// DIMDIAMETER, DIMANGULAR of two lines and DIMORDINATE): the class of the record, the subclass that holds the points,
        /// and which group of the dimension's own record gives each point of it.
        /// </summary>
        private sealed class DimensionContextKind
        {
            internal DimensionContextKind(string type, string marker, params int[] records)
                : this(type, null, null, marker, records) { }

            internal DimensionContextKind(string type, string before, int[] beforeRecords, string marker, int[] records)
            {
                Type = type;
                Before = before;
                BeforeRecords = beforeRecords ?? new int[0];
                Marker = marker;
                Records = records;
            }

            internal string Type { get; }

            /// <summary>The subclass that comes before <see cref="Marker"/> with its own points (a jogged radius's chord point); null where there is none.</summary>
            internal string Before { get; }

            /// <summary>The group of the record each point of the context's subclass <see cref="Before"/> is taken from.</summary>
            internal int[] BeforeRecords { get; }

            /// <summary>The subclass named by the CLASSES section (the last one of the record).</summary>
            internal string Marker { get; }

            /// <summary>The group of the record each point of the context (11, then 12) is taken from.</summary>
            internal int[] Records { get; }
        }

        private static readonly DimensionContextKind AlignedKind = new DimensionContextKind(
            "ACDB_ALDIMOBJECTCONTEXTDATA_CLASS",
            "AcDbAlignedDimensionObjectContextData",
            10
        );

        private static readonly DimensionContextKind RadialKind = new DimensionContextKind(
            "ACDB_RADIMOBJECTCONTEXTDATA_CLASS",
            "AcDbRadialDimensionObjectContextData",
            15
        );

        private static readonly DimensionContextKind DiametricKind = new DimensionContextKind(
            "ACDB_DMDIMOBJECTCONTEXTDATA_CLASS",
            "AcDbDiametricDimensionObjectContextData",
            15,
            10
        );

        private static readonly DimensionContextKind AngularKind = new DimensionContextKind(
            "ACDB_ANGDIMOBJECTCONTEXTDATA_CLASS",
            "AcDbAngularDimensionObjectContextData",
            16
        );

        private static readonly DimensionContextKind OrdinateKind = new DimensionContextKind(
            "ACDB_ORDDIMOBJECTCONTEXTDATA_CLASS",
            "AcDbOrdinateDimensionObjectContextData",
            10,
            14
        );

        // An arc length dimension's context is an angular one whose point is the record's 10 (a point of the dimension arc), as
        // AutoCAD 2021 writes it (probe GHA3: a DIMARC at 1:50 and 1:100).
        private static readonly DimensionContextKind ArcLengthKind = new DimensionContextKind(
            "ACDB_ANGDIMOBJECTCONTEXTDATA_CLASS",
            "AcDbAngularDimensionObjectContextData",
            10
        );

        // A jogged radius's context holds the chord point (the record's 15) in the radial subclass and the point the radius
        // is measured from (13) and the jog's (14) in its own (probe GHA3: a DIMJOGGED at 1:50 and 1:100).
        private static readonly DimensionContextKind JoggedKind = new DimensionContextKind(
            "ACDB_RADIMLRGOBJECTCONTEXTDATA_CLASS",
            "AcDbRadialDimensionObjectContextData",
            new[] { 15 },
            "AcDbRadialDimensionLargeObjectContextData",
            new[] { 13, 14 }
        );

        /// <summary>The context kind of a dimension; null for a kind of dimension the library does not write contexts of yet.</summary>
        private DimensionContextKind DimensionKindOf()
        {
            switch (this)
            {
                case DxfRotatedDimension _:
                case DxfAlignedDimension _:
                    return AlignedKind;
                case DxfRadialDimension _:
                    return RadialKind;
                case DxfDiametricDimension _:
                    return DiametricKind;
                case DxfPoint3AngularDimension _:
                case DxfLineAngularDimension2 _:
                    return AngularKind;
                case DxfOrdinateDimension _:
                    return OrdinateKind;
                case DxfArcDimension _:
                    return ArcLengthKind;
                case DxfRadialDimensionLarge _:
                    return JoggedKind;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Whether the library writes contexts for the dimension: a linear, aligned, radius, jogged radius, diameter, angular (of
        /// two lines or three points), arc length or ordinate one, its words where they are put or turned or not, as AutoCAD
        /// 2021 was probed; its picture is laid out at each scale by the caller, which holds the builders.
        /// </summary>
        internal bool CanHoldDimensionContexts() => DimensionKindOf() != null && this is DxfDimension;

        /// <summary>
        /// Makes a dimension annotative at a scale as AutoCAD 2021 writes it: its extended data <c>AcadAnnotative</c>, an
        /// extension dictionary holding AcDbContextDataManager and ACDB_ANNOTATIONSCALES, and in it the default context, which
        /// names the dimension's picture block (<see cref="DxfDimension.DimBlock"/>) and holds the middle of its words (group 11
        /// of the record) and the points of its kind. The dimension is not drawn again: the caller drew its picture at the
        /// scale.
        /// </summary>
        internal void MakeDimensionAnnotative(DxfAnnotationScale scale)
        {
            DxfFile file = InFile("be made annotative");
            DimensionContextKind kind = DimensionKindOf();
            var dimension = this as DxfDimension;

            if (kind == null || dimension?.DimBlock == null)
            {
                throw new NotSupportedException(
                    "A "
                        + KindName()
                        + " is not made annotative here: its kind or its picture is not one the library writes contexts of."
                );
            }

            if (!file.Supports(1021))
            {
                throw new InvalidOperationException(
                    "A DXF older than R2007 (AC1021) has no annotative objects: AutoCAD 2008, which saves R2007, brought them."
                );
            }

            file.AddClass(kind.Type, kind.Marker, 1153);
            XData = AnnotativeOn();
            WriteContext(
                EnsureContextDictionary(_item),
                kind.Type,
                scale,
                true,
                DimensionGroups(kind, dimension.DimBlock.Name, Rec(11, 0.0), Rec(21, 0.0))
            );
        }

        /// <summary>
        /// Shows an annotative dimension at another scale, as AutoCAD 2021's OBJECTSCALE does: a context named <c>*A</c> and one
        /// more than the highest number used, not the default, that names the picture block laid out at the scale and the middle
        /// of its words there. The record keeps the picture of the default scale.
        /// </summary>
        internal void AddDimensionContext(DxfAnnotationScale scale, DxfBlockTableRecord picture, GeoPoint3 words)
        {
            DxfFile file = InFile("be shown at a scale");
            DimensionContextKind kind = DimensionKindOf();

            if (kind == null || Annotative != DxfAnnotativeStates.True)
            {
                throw new InvalidOperationException(
                    "A "
                        + KindName()
                        + " that is not annotative, or of a kind the library writes no contexts of, is not shown at a scale."
                );
            }

            file.AddClass(kind.Type, kind.Marker, 1153);
            WriteContext(
                EnsureContextDictionary(_item),
                kind.Type,
                scale,
                false,
                DimensionGroups(kind, picture.Name, words.X, words.Y)
            );
        }

        /// <summary>The groups of a dimension's context after the scale, as AutoCAD 2021 writes them for the kinds probed.</summary>
        private List<KeyValuePair<int, string>> DimensionGroups(
            DimensionContextKind kind,
            string block,
            double wordsX,
            double wordsY
        )
        {
            // 294 is 1 where the words stand at their default place and 140 is their angle in radians, as the record's flag 128
            // and its 53 (degrees) say (probe GHA3: a DIMLINEAR whose words DIMTEDIT moved holds 294 0, one turned 30 degrees 140 0.5236).
            var dimension = (DxfDimension)this;
            var groups = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(100, DimensionContextMarker),
                new KeyValuePair<int, string>(2, block),
                Pair(293, 0),
                Pair(10, wordsX),
                Pair(20, wordsY),
                Pair(294, dimension.UsingDefaultTextPosition ? 1 : 0),
                Pair(140, dimension.TextRotation),
                Pair(298, 0),
                Pair(291, 0),
                Pair(70, 0),
                Pair(292, 0),
                Pair(71, 0),
                Pair(280, 0),
                Pair(295, 0),
                Pair(296, 0),
                Pair(297, 0),
            };

            // The points of each subclass, numbered on from 11: a jogged radius's 11 is in the radial subclass, its 12 and 13 in
            // the large one.
            int n = 0;

            if (kind.Before != null)
            {
                groups.Add(new KeyValuePair<int, string>(100, kind.Before));
                n = AddPoints(groups, kind.BeforeRecords, n);
            }

            groups.Add(new KeyValuePair<int, string>(100, kind.Marker));
            AddPoints(groups, kind.Records, n);
            return groups;
        }

        /// <summary>Adds the points of a context's subclass, each taken from a group of the record, from the number <paramref name="n"/> on (11 + n); the next number.</summary>
        private int AddPoints(List<KeyValuePair<int, string>> groups, int[] records, int n)
        {
            foreach (int code in records)
            {
                groups.Add(Pair(11 + n, Rec(code, 0.0)));
                groups.Add(Pair(21 + n, Rec(code + 10, 0.0)));
                groups.Add(Pair(31 + n, Rec(code + 20, 0.0)));
                n++;
            }

            return n;
        }

        /// <summary>
        /// The picture block a dimension is drawn with at a scale: the block its context of the scale names where it is annotative
        /// and shown at the scale, else its own block (<see cref="DxfDimension.DimBlock"/>) where
        /// <paramref name="allVisible"/> is true or it is not annotative; null where it is annotative and not shown at the scale
        /// and ANNOALLVISIBLE is off.
        /// </summary>
        internal bool TryPictureAt(DxfAnnotationScale at, bool allVisible, out DxfBlockTableRecord picture)
        {
            var dimension = this as DxfDimension;
            picture = dimension?.DimBlock;

            if (
                dimension == null
                || at == null
                || (_item.CopiedContexts == null && _item.File.ExtensionDictionaryOf(_item) == null)
                || Annotative != DxfAnnotativeStates.True
            )
            {
                return true;
            }

            foreach (ScaleContext held in ScaleContexts())
            {
                if (!string.Equals(held.Scale, at.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DxfItem context = FindContext(held.Key);
                DxfDatabase database = (_item.CopiedContexts != null ? _item.File.Origin : _item.File).Database;
                string name = ContextBlockName(context);

                if (name != null && database != null && database.BlockTable.Has(name))
                {
                    picture = database.BlockTable[name];
                }

                return true;
            }

            return allVisible;
        }

        /// <summary>The picture of one scale of an annotative dimension: the scale, the block its context names and its entry's name.</summary>
        internal readonly struct DimensionPicture
        {
            internal DimensionPicture(string key, DxfAnnotationScale scale, DxfBlockTableRecord block, bool isDefault)
            {
                Key = key;
                Scale = scale;
                Block = block;
                IsDefault = isDefault;
            }

            internal string Key { get; }

            internal DxfAnnotationScale Scale { get; }

            internal DxfBlockTableRecord Block { get; }

            internal bool IsDefault { get; }
        }

        /// <summary>The pictures of an annotative dimension, one for each context that names a block of the drawing, in the order of the contexts.</summary>
        internal List<DimensionPicture> DimensionPictures()
        {
            var pictures = new List<DimensionPicture>();

            if (!(this is DxfDimension) || _item == null || _item.CopiedContexts != null)
            {
                return pictures;
            }

            DxfDatabase database = _item.File.Database;

            foreach (ScaleContext held in ScaleContexts())
            {
                string name = ContextBlockName(FindContext(held.Key));
                DxfDatabase own = _item.File.Database;
                DxfAnnotationScale scale = null;

                if (own != null)
                {
                    foreach (DxfAnnotationScale each in DxfObjectContextCollection.Scales(own))
                    {
                        if (string.Equals(each.Handle, held.Scale, StringComparison.OrdinalIgnoreCase))
                        {
                            scale = each;
                            break;
                        }
                    }
                }

                if (name != null && database != null && database.BlockTable.Has(name) && scale != null)
                {
                    pictures.Add(new DimensionPicture(held.Key, scale, database.BlockTable[name], held.IsDefault));
                }
            }

            return pictures;
        }

        /// <summary>
        /// Erases the picture blocks of the contexts of a dimension that are not the block of its record: those of the scales it
        /// is taken out of when it is made not annotative. A block something still names is kept.
        /// </summary>
        private void ErasePictures(bool defaultToo)
        {
            DxfDimension dimension = (DxfDimension)this;
            DxfBlockTableRecord own = dimension.DimBlock;

            foreach (DimensionPicture picture in DimensionPictures())
            {
                if (picture.Block == own && !defaultToo)
                {
                    continue;
                }

                try
                {
                    picture.Block.Erase();
                }
                catch (InvalidOperationException)
                {
                    // A block something still names stays.
                }
            }
        }

        /// <summary>Names a block as the picture of the context of an entry (group 2), as a copy of a dimension gives each context a block of its own.</summary>
        internal void SetContextBlock(string key, DxfBlockTableRecord block)
        {
            DxfItem context = FindContext(key);

            if (context == null)
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> groups = context.File.GroupsOf(context);
            bool inside = false;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 100)
                {
                    inside = groups[i].Value.Trim() == DimensionContextMarker;
                }
                else if (inside && groups[i].TypeCode == 2)
                {
                    context.File.SetValueAt(context, i, block.Name);
                    return;
                }
            }
        }

        /// <summary>
        /// Moves the points of every context of a dimension by a transformation, as the dimension's own points are moved: the middle
        /// of the words (10, 20) and the points of its kind (11, 12). The picture blocks are moved by the caller.
        /// </summary>
        internal void TransformDimensionContexts(GeoTransform3 transform)
        {
            double z = Rec(31, 0.0);

            foreach (ScaleContext held in ScaleContexts())
            {
                DxfItem context = FindContext(held.Key);

                if (context == null)
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> groups = context.File.GroupsOf(context);
                string marker = null;

                foreach (int first in new[] { 10, 11, 12, 13 })
                {
                    marker = null;
                    double[] at = { double.NaN, double.NaN, z };
                    int[] index = { -1, -1, -1 };

                    for (int i = 0; i < groups.Count; i++)
                    {
                        int code = groups[i].TypeCode;

                        if (code == 100)
                        {
                            marker = groups[i].Value.Trim();
                        }
                        else if (
                            (
                                first == 10
                                    ? marker == DimensionContextMarker
                                    : marker != DimensionContextMarker
                                        && marker != ContextDataMarker
                                        && marker != ScaleContextMarker
                            )
                            && code >= first
                            && code <= first + 20
                            && (code - first) % 10 == 0
                            && Numbers.TryRead(groups[i].Value, out double value)
                        )
                        {
                            at[(code - first) / 10] = value;
                            index[(code - first) / 10] = i;
                        }
                    }

                    if (index[0] < 0 || index[1] < 0)
                    {
                        continue;
                    }

                    GeoPoint3 moved = transform.Transform(new GeoPoint3(at[0], at[1], at[2]));
                    context.File.SetValueAt(context, index[0], Numbers.Exact(moved.X + 0.0));
                    context.File.SetValueAt(context, index[1], Numbers.Exact(moved.Y + 0.0));

                    if (index[2] >= 0)
                    {
                        context.File.SetValueAt(context, index[2], Numbers.Exact(moved.Z + 0.0));
                    }
                }
            }
        }

        /// <summary>Writes the middle of the words (10, 20) of the context of an entry, as it is after its picture was laid out again.</summary>
        internal void SetDimensionWords(string key, GeoPoint3 words)
        {
            DxfItem context = FindContext(key);

            if (context == null)
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> groups = context.File.GroupsOf(context);
            bool inside = false;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 100)
                {
                    inside = groups[i].Value.Trim() == DimensionContextMarker;
                }
                else if (inside && (groups[i].TypeCode == 10 || groups[i].TypeCode == 20))
                {
                    context.File.SetValueAt(
                        context,
                        i,
                        Numbers.Exact((groups[i].TypeCode == 10 ? words.X : words.Y) + 0.0)
                    );
                }
            }
        }

        /// <summary>
        /// Writes the points of its kind (11, 12 and 13 of each context: the record's 10, 15, 13 or 14 by the kind) and the words'
        /// flags (294 and 140) of every context of a dimension as the record holds them now, as they are after the dimension's
        /// definition points or its words were edited and its pictures drawn again.
        /// </summary>
        internal void SetDimensionPoints(string key)
        {
            DimensionContextKind kind = DimensionKindOf();
            DxfItem context = FindContext(key);

            if (kind == null || context == null)
            {
                return;
            }

            var dimension = (DxfDimension)this;
            var records = new List<int>(kind.BeforeRecords);
            records.AddRange(kind.Records);
            IReadOnlyList<DxfTypedValue> groups = context.File.GroupsOf(context);
            string marker = null;

            for (int i = 0; i < groups.Count; i++)
            {
                int code = groups[i].TypeCode;

                if (code == 100)
                {
                    marker = groups[i].Value.Trim();
                }
                else if (marker == DimensionContextMarker && code == 294)
                {
                    context.File.SetValueAt(context, i, dimension.UsingDefaultTextPosition ? "1" : "0");
                }
                else if (marker == DimensionContextMarker && code == 140)
                {
                    context.File.SetValueAt(context, i, Numbers.Exact(dimension.TextRotation + 0.0));
                }
                else if (
                    marker != DimensionContextMarker
                    && marker != ContextDataMarker
                    && marker != ScaleContextMarker
                    && code >= 11
                    && code <= 33
                    && code % 10 >= 1
                    && code % 10 <= records.Count
                )
                {
                    int record = records[code % 10 - 1] + (code / 10 - 1) * 10;
                    context.File.SetValueAt(context, i, Numbers.Exact(Rec(record, 0.0) + 0.0));
                }
            }
        }

        /// <summary>The name of the picture block a dimension's context holds (group 2 of AcDbDimensionObjectContextData); null where it names none.</summary>
        private static string ContextBlockName(DxfItem context)
        {
            string marker = null;

            foreach (DxfTypedValue group in context.File.ObjectGroupsOf(context))
            {
                if (group.TypeCode == 100)
                {
                    marker = group.Value.Trim();
                }
                else if (group.TypeCode == 2 && marker == DimensionContextMarker)
                {
                    return group.Value.Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The number the next entry of ACDB_ANNOTATIONSCALES is named by: one more than the highest the dictionary holds or has
        /// held, as AutoCAD 2021 counts them within a session (after <c>*A2</c> is deleted the next is <c>*A3</c>).
        /// </summary>
        private static int NextContextNumber(DxfDBDictionary scales, int top)
        {
            DxfItem owner = scales.Item;

            if (owner != null && ContextCounters.TryGetValue(owner, out StrongBox<int> held) && held.Value > top)
            {
                top = held.Value;
            }

            return top + 1;
        }

        /// <summary>Remembers that the entries of a dictionary have been numbered as far as an entry's name says.</summary>
        private static void NoteContextNumber(DxfDBDictionary scales, string key)
        {
            DxfItem owner = scales.Item;

            if (
                owner == null
                || key.Length <= 2
                || !key.StartsWith("*A", StringComparison.Ordinal)
                || !int.TryParse(key.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
            )
            {
                return;
            }

            StrongBox<int> held = ContextCounters.GetOrCreateValue(owner);

            if (n > held.Value)
            {
                held.Value = n;
            }
        }

        /// <summary>
        /// Takes the context of a scale out of the object, as <see cref="RemoveContext"/> does for the one it is not the last of:
        /// its entry erased and, for a dimension, its picture block. Where the context is the default the next one in the order
        /// of the dictionary becomes it, and the record takes that context's data (the groups the library writes into the
        /// context), as AutoCAD 2021 rewrites it: the height of a TEXT, an attribute definition or an MTEXT is multiplied by the
        /// old default's scale over the new one's (125 at 1:50 is 250 at 1:100), its place, angle and the rest taken from the
        /// context; a HATCH's pattern lines, an INSERT's factors and place, a dimension's block and the middle of its words, and
        /// a multileader's context data likewise.
        /// </summary>
        private void RemoveHeldContext(List<ScaleContext> held, ScaleContext gone)
        {
            DxfFile file = _item.File;
            DxfDBDictionary scales = ContextDictionary();
            DxfItem old = FindContext(gone.Key);

            if (gone.IsDefault)
            {
                ScaleContext next = held.Find(c => c.Key != gone.Key);
                DxfItem now = FindContext(next.Key);
                double factor = ScaleRatioOf(file, gone.Scale) / ScaleRatioOf(file, next.Scale);
                MarkDefault(now);
                TakeRecordFrom(now, old, factor);
            }

            if (this is DxfDimension && old != null)
            {
                string name = ContextBlockName(old);
                DxfDatabase database = file.Database;

                if (name != null && database != null && database.BlockTable.Has(name))
                {
                    try
                    {
                        database.BlockTable[name].Erase();
                    }
                    catch (InvalidOperationException)
                    {
                        // A block a dimension still names is kept.
                    }
                }
            }

            NoteContextNumber(scales, gone.Key);
            scales.Remove(gone.Key);
        }

        /// <summary>Makes a context the default (the 290 of 1 of its AcDbObjectContextData).</summary>
        private static void MarkDefault(DxfItem context)
        {
            IReadOnlyList<DxfTypedValue> groups = context.File.GroupsOf(context);
            bool inside = false;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 100)
                {
                    inside = groups[i].Value.Trim() == ContextDataMarker;
                }
                else if (inside && groups[i].TypeCode == 290)
                {
                    context.File.SetValueAt(context, i, "1");
                    return;
                }
            }
        }

        /// <summary>The number of the first group of a code in a context after its marker; the fallback where it holds none.</summary>
        private static double ContextNumber(DxfItem context, string marker, int code, double fallback)
        {
            string at = null;

            foreach (DxfTypedValue group in context.File.ObjectGroupsOf(context))
            {
                if (group.TypeCode == 100)
                {
                    at = group.Value.Trim();
                }
                else if (at == marker && group.TypeCode == code)
                {
                    return Numbers.TryRead(group.Value, out double value) && Guard.IsFinite(value) ? value : fallback;
                }
            }

            return fallback;
        }

        /// <summary>Sets the first group of a code in the object's record, where it holds one.</summary>
        private void SetRecord(int code, double value)
        {
            IReadOnlyList<DxfTypedValue> groups = _item.File.GroupsOf(_item);

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == code)
                {
                    _item.File.SetValueAt(_item, i, Numbers.Exact(value + 0.0));
                    return;
                }
            }
        }

        /// <summary>Gives the record of the object the data of the context that became its default, as AutoCAD 2021 rewrites it.</summary>
        private void TakeRecordFrom(DxfItem now, DxfItem old, double factor)
        {
            switch (_item.Type)
            {
                case "TEXT":
                case "ATTDEF":
                case "ATTRIB":
                    SetRecord(40, Rec(40, 0.0) * factor);
                    SetRecord(10, ContextNumber(now, ScaleContextMarker, 10, Rec(10, 0.0)));
                    SetRecord(20, ContextNumber(now, ScaleContextMarker, 20, Rec(20, 0.0)));
                    SetRecord(50, ContextNumber(now, ScaleContextMarker, 50, Rec(50, 0.0)));
                    SetRecord(72, ContextNumber(now, ScaleContextMarker, 70, Rec(72, 0.0)));

                    if (Rec(72, 0.0) != 0.0 || Rec(73, 0.0) != 0.0)
                    {
                        SetRecord(11, ContextNumber(now, ScaleContextMarker, 11, Rec(11, 0.0)));
                        SetRecord(21, ContextNumber(now, ScaleContextMarker, 21, Rec(21, 0.0)));
                    }

                    break;
                case "MTEXT":
                    SetRecord(40, Rec(40, 0.0) * factor);
                    SetRecord(41, ContextNumber(now, ScaleContextMarker, 40, Rec(41, 0.0)));
                    SetRecord(10, ContextNumber(now, ScaleContextMarker, 11, Rec(10, 0.0)));
                    SetRecord(20, ContextNumber(now, ScaleContextMarker, 21, Rec(20, 0.0)));
                    SetRecord(30, ContextNumber(now, ScaleContextMarker, 31, Rec(30, 0.0)));
                    SetRecord(11, ContextNumber(now, ScaleContextMarker, 10, Rec(11, 1.0)));
                    SetRecord(21, ContextNumber(now, ScaleContextMarker, 20, Rec(21, 0.0)));
                    SetRecord(31, ContextNumber(now, ScaleContextMarker, 30, Rec(31, 0.0)));
                    SetRecord(71, ContextNumber(now, ScaleContextMarker, 70, Rec(71, 1.0)));
                    break;
                case "HATCH":
                    TakeHatchLines(now);
                    break;
                case "INSERT":
                    foreach (int code in new[] { 10, 20, 30, 41, 42, 43, 50 })
                    {
                        SetRecord(code, ContextNumber(now, ScaleContextMarker, code, Rec(code, 0.0)));
                    }

                    break;
                case "MULTILEADER":
                    ((DxfMLeader)this).SetContextData(ContextData(now));
                    break;
                default:
                    if (this is DxfDimension)
                    {
                        string name = ContextBlockName(now);
                        DxfDatabase database = _item.File.Database;

                        if (name != null && database != null && database.BlockTable.Has(name))
                        {
                            ((DxfDimension)this).DimBlock = database.BlockTable[name];
                        }

                        SetRecord(11, ContextNumber(now, DimensionContextMarker, 10, Rec(11, 0.0)));
                        SetRecord(21, ContextNumber(now, DimensionContextMarker, 20, Rec(21, 0.0)));
                    }

                    break;
            }
        }

        /// <summary>The groups of a hatch context's pattern lines (43, 44, 45, 46 and 49 after its 78) replace those of the record, in order.</summary>
        private void TakeHatchLines(DxfItem now)
        {
            var lines = new List<string>();
            string marker = null;
            bool inLines = false;

            foreach (DxfTypedValue group in now.File.ObjectGroupsOf(now))
            {
                int code = group.TypeCode;

                if (code == 100)
                {
                    marker = group.Value.Trim();
                }
                else if (marker == HatchContextMarker && code == 78)
                {
                    inLines = true;
                }
                else if (marker == HatchContextMarker && inLines && code == 40)
                {
                    break;
                }
                else if (inLines && (code == 43 || code == 44 || code == 45 || code == 46 || code == 49))
                {
                    lines.Add(group.Value.Trim());
                }
            }

            IReadOnlyList<DxfTypedValue> record = _item.File.GroupsOf(_item);
            bool inRecord = false;
            int at = 0;

            for (int i = 0; i < record.Count && at < lines.Count; i++)
            {
                int code = record[i].TypeCode;

                if (code == 78)
                {
                    inRecord = true;
                }
                else if (inRecord && (code == 98 || code == 47 || code >= 1000 || code == 450 || code == 100))
                {
                    break;
                }
                else if (inRecord && (code == 43 || code == 44 || code == 45 || code == 46 || code == 49))
                {
                    _item.File.SetValueAt(_item, i, lines[at++]);
                }
            }
        }

        /// <summary>The groups of a multileader's context after its scale (340), in order, as its record holds its context data.</summary>
        private static List<KeyValuePair<int, string>> ContextData(DxfItem context)
        {
            var groups = new List<KeyValuePair<int, string>>();
            bool after = false;

            foreach (DxfTypedValue group in context.File.ObjectGroupsOf(context))
            {
                if (after)
                {
                    groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
                else if (group.TypeCode == 340)
                {
                    after = true;
                }
            }

            return groups;
        }
    }
}
