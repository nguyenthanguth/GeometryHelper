using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        // The extended data that marks an object annotative, and that which keeps it the way up of the sheet, as AutoCAD's
        // drawings register them; the dictionary of the extension dictionary that holds the contexts of its scales, and the
        // subclasses of a context whose groups are read.
        private const string AnnotativeApplication = "AcadAnnotative";
        private const string AnnotativeDataName = "AnnotativeData";
        private const string PaperOrientationApplication = "AcadAnnoPO";
        private const string ContextManagerName = "AcDbContextDataManager";
        private const string ContextDataMarker = "AcDbObjectContextData";
        private const string ScaleContextMarker = "AcDbAnnotScaleObjectContextData";

        // The entities that can be annotative and keep their own flag, as AutoCAD's annotative protocol takes them; an INSERT
        // is annotative as its block is.
        private static readonly HashSet<string> AnnotativeEntities = new HashSet<string>(StringComparer.Ordinal)
        {
            "TEXT", "MTEXT", "ATTRIB", "ATTDEF", "DIMENSION", "ARC_DIMENSION", "LARGE_RADIAL_DIMENSION", "LEADER", "MULTILEADER", "TOLERANCE", "HATCH",
        };

        // The entities that keep the way up of the sheet of their own, as AutoCAD's protocol of paper orientation takes them.
        private static readonly HashSet<string> OrientedEntities = new HashSet<string>(StringComparer.Ordinal) { "TEXT", "MTEXT", "ATTRIB", "ATTDEF" };

        /// <summary>
        /// Gets or sets whether the object is annotative, as AutoCAD's <c>Annotative</c>: drawn at the size its scale gives it on
        /// the sheet, each scale it is shown at a context of its own (<see cref="HasContext"/>).
        /// </summary>
        /// <value>
        /// <para>
        /// Get, as AutoCAD keeps it: for a TEXT, an MTEXT, an attribute, a dimension, a leader, a multileader, a tolerance, a
        /// hatch, a text style, a dimension style and a block, the flag of its extended data <c>AcadAnnotative</c>, the second
        /// 1070 of <c>AnnotativeData</c>, <see cref="DxfAnnotativeStates.False"/> where it has none; a multileader is annotative
        /// by its own group 293 (<see cref="DxfMLeader.EnableAnnotationScale"/>) as well; an INSERT by its block, that of a
        /// dynamic block by the dynamic block where its anonymous block is not; an ATTRIB by its INSERT, whatever its own data
        /// says, as AutoCAD 2021 reads it: the attribute ATT_DVSUP of AutoCAD's sample, whose AcadAnnotative says 0, is
        /// annotative in its annotative valve, and one of contexts is not in a block that is not (check D35b); in a block that is
        /// not annotative, an ATTRIB whose own data says 1 and that holds a context is, as AutoCAD 2021 read the attribute it made
        /// of an annotative definition in a block that was not, and one that says 1 and holds none is not (check D35c); a
        /// multileader style by its group 296. <see cref="DxfAnnotativeStates.NotApplicable"/> for every other object, as a line.
        /// </para>
        /// <para>
        /// Set <see cref="DxfAnnotativeStates.True"/>, a text style or a dimension style is given the extended data
        /// <c>AcadAnnotative</c> (1000 <c>AnnotativeData</c>, then 1070 1 and 1070 1 in braces), which is all AutoCAD keeps of
        /// it. Set <see cref="DxfAnnotativeStates.False"/>, the extended data is taken out, with <c>AcadAnnoPO</c>, a
        /// multileader's 293 is made 0, and an entity's contexts go with the dictionary AcDbContextDataManager of its extension
        /// dictionary, of any kind of entity: its own groups, those of the scale it was last shown at, stay. A value the object
        /// has changes nothing.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the value set is <see cref="DxfAnnotativeStates.NotApplicable"/>, or not one of the enumeration.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set on an object that is never annotative, as AutoCAD answers
        /// eNotApplicable; on an object in no file or erased; or true on a style in a file older than R2007 (AC1021).</exception>
        /// <exception cref="NotSupportedException">Thrown when set true on an entity, which AutoCAD shows at the current scale by
        /// a context the library does not write until AutoCAD's record of it is checked on a drawing AutoCAD saved; or set on an
        /// INSERT (annotative as its block is), a block or a multileader style.</exception>
        public DxfAnnotativeStates Annotative
        {
            get
            {
                switch (AnnotativeKind())
                {
                    case AnnotationKind.None:
                        return DxfAnnotativeStates.NotApplicable;
                    case AnnotationKind.Reference:
                        // The anonymous block of a dynamic block's INSERT is annotative as the dynamic block is (check D35b).
                        var reference = (DxfBlockReference)this;
                        DxfBlockTableRecord block = reference.BlockTableRecord;
                        bool annotative = block != null && (block.Annotative == DxfAnnotativeStates.True || reference.DynamicBlockTableRecord?.Annotative == DxfAnnotativeStates.True);
                        return annotative ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
                    case AnnotationKind.MLeaderStyle:
                        return _item != null && _item.File.ValueOf(_item, 296)?.Trim() == "1" ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
                    case AnnotationKind.Entity when this is DxfAttributeReference attribute && attribute.Owner is DxfBlockReference insert:
                        // An ATTRIB is annotative as its INSERT is, whatever its own data says (check D35b); in a block that is
                        // not, where its own data says so and it holds a context (check D35c).
                        return insert.Annotative == DxfAnnotativeStates.True || (AnnotativeFlag() && ScaleContexts().Count > 0) ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
                    default:
                        return AnnotativeFlag() || (this is DxfMLeader mleader && mleader.EnableAnnotationScale) ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
                }
            }

            set
            {
                if (value != DxfAnnotativeStates.True && value != DxfAnnotativeStates.False)
                {
                    throw new ArgumentException("An object is made annotative or not: " + value + " is not set.", nameof(value));
                }

                AnnotationKind kind = AnnotativeKind();

                if (kind == AnnotationKind.None)
                {
                    throw new InvalidOperationException("A " + KindName() + " is never annotative, as AutoCAD answers eNotApplicable.");
                }

                if (value == Annotative)
                {
                    return;
                }

                if (kind == AnnotationKind.Reference || kind == AnnotationKind.Block || kind == AnnotationKind.MLeaderStyle)
                {
                    throw new NotSupportedException("A " + KindName() + " is not made annotative here: an INSERT is annotative as its block is, and a block's and a multileader style's flag are not set yet.");
                }

                if (value == DxfAnnotativeStates.True && kind == AnnotationKind.Entity)
                {
                    throw new NotSupportedException("A " + KindName() + " is not made annotative yet: AutoCAD shows it at the current scale by a context the library writes only once AutoCAD's record of it is checked on a drawing AutoCAD saved.");
                }

                DxfFile file = InFile(value == DxfAnnotativeStates.True ? "be made annotative" : "be made not annotative");

                if (value == DxfAnnotativeStates.True)
                {
                    if (!file.Supports(1021))
                    {
                        throw new InvalidOperationException("A DXF older than R2007 (AC1021) has no annotative styles: AutoCAD 2008, which saves R2007, brought them.");
                    }

                    XData = AnnotativeOn();
                    return;
                }

                DxfDBDictionary extension = ExtensionDictionary;

                if (extension != null && extension.Contains(ContextManagerName))
                {
                    extension.Remove(ContextManagerName);
                }

                var taken = new List<DxfTypedValue>();

                foreach (string application in new[] { AnnotativeApplication, PaperOrientationApplication })
                {
                    if (GetXDataForApplication(application) != null)
                    {
                        taken.Add(new DxfTypedValue(1001, application));
                    }
                }

                if (taken.Count > 0)
                {
                    XData = taken;
                }

                if (this is DxfMLeader mleader)
                {
                    mleader.EnableAnnotationScale = false;
                }
            }
        }

        /// <summary>
        /// Gets whether the object is shown at a scale, as AutoCAD's <c>HasContext</c>: whether a context under
        /// ACDB_ANNOTATIONSCALES of AcDbContextDataManager of its extension dictionary points at the scale's SCALE, by the first
        /// 340 of its subclass AcDbAnnotScaleObjectContextData, of any kind of object. Those two groups, with the default flag
        /// (290 of AcDbObjectContextData), are all the library reads of a context, but for the placing of the context of a TEXT,
        /// an attribute or a definition, an MTEXT and an INSERT, which <see cref="DxfDBText.Position"/>,
        /// <see cref="DxfMText.Location"/> and <see cref="DxfBlockReference.Position"/> read and write; the rest of AutoCAD's
        /// record of it is kept as it is. A copy in no file (<see cref="DxfEntity.Clone"/>) carries the contexts of its original,
        /// and is shown at the scales of its original's drawing.
        /// </summary>
        /// <param name="context">The scale, of the object's drawing's list.</param>
        /// <returns>Whether it is; false for a scale of another drawing or in none, and for an object with no contexts, as one not
        /// annotative, but for an annotative INSERT, or an ATTRIB of one, that holds none, which is shown at the scale named 1:1,
        /// whatever CANNOSCALE says, as AutoCAD 2021 read one (check D35b).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the scale is null.</exception>
        public bool HasContext(DxfObjectContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // A copy that carries its original's contexts is shown at the scales of its original's drawing.
            DxfFile own = _item?.CopiedContexts != null ? _item.File.Origin : _item?.File;

            if (!(context is DxfAnnotationScale scale) || scale.Item == null || _item == null || scale.Item.File != own)
            {
                return false;
            }

            List<ScaleContext> contexts = ScaleContexts();

            foreach (ScaleContext held in contexts)
            {
                if (string.Equals(held.Scale, scale.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // An INSERT of an annotative block, or an attribute of one, that holds no context is shown at 1:1 (check D35b).
            return contexts.Count == 0 && scale.Name == "1:1" && (this is DxfBlockReference || this is DxfAttributeReference) && Annotative == DxfAnnotativeStates.True;
        }

        /// <summary>
        /// Stops showing an annotative object at a scale, as AutoCAD's <c>RemoveContext</c>: its context of the scale erased with
        /// its entry of ACDB_ANNOTATIONSCALES, of any kind of object, every other context kept as it is. A scale the object is not
        /// shown at changes nothing.
        /// </summary>
        /// <param name="context">The scale.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scale is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scale is the last the object is shown at, as AutoCAD keeps
        /// one (make it not annotative by <see cref="Annotative"/> instead); when its context is the default (the 290 of 1 of its
        /// subclass AcDbObjectContextData), which no other is made in its place; when the object is erased; or when it is a copy
        /// in no file that carries its original's contexts, until it is added to a drawing.</exception>
        public void RemoveContext(DxfObjectContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!HasContext(context))
            {
                return;
            }

            InFile("stop being shown at a scale");
            string handle = ((DxfAnnotationScale)context).Handle;
            List<ScaleContext> held = ScaleContexts();

            // One shown at 1:1 by its block, holding no context, is shown at that scale alone.
            if (held.Count <= 1)
            {
                throw new InvalidOperationException("The scale " + context.Name + " is the last the " + KindName() + " is shown at, which AutoCAD keeps: make it not annotative instead.");
            }

            ScaleContext gone = held.Find(c => string.Equals(c.Scale, handle, StringComparison.OrdinalIgnoreCase));

            if (gone.IsDefault)
            {
                throw new InvalidOperationException("The context of the scale " + context.Name + " is the default of the " + KindName() + ", and the library makes no other the default in its place.");
            }

            ContextDictionary().Remove(gone.Key);
        }

        /// <summary>
        /// Gets whether the object keeps the way up of the sheet, as AutoCAD's <c>PaperOrientation</c>, its "match orientation to
        /// layout": for an annotative TEXT, MTEXT, attribute, text style or block, the first 1070 of its extended data
        /// <c>AcadAnnoPO</c>, <see cref="DxfPaperOrientationStates.False"/> where it has none or is not annotative; for an INSERT,
        /// that of its block; <see cref="DxfPaperOrientationStates.NotApplicable"/> for every other object, as a line or a
        /// dimension.
        /// </summary>
        /// <remarks>
        /// The data <c>AcadAnnoPO</c> is the library's reading of AutoCAD's, whose drawings register that application: neither
        /// the DXF reference nor ezdxf gives it, and it is not yet checked on a drawing AutoCAD saved.
        /// </remarks>
        public DxfPaperOrientationStates PaperOrientation
        {
            get
            {
                if (!Orientable())
                {
                    return DxfPaperOrientationStates.NotApplicable;
                }

                if (AnnotativeKind() == AnnotationKind.Reference)
                {
                    DxfBlockTableRecord block = ((DxfBlockReference)this).BlockTableRecord;
                    return block != null && block.PaperOrientation == DxfPaperOrientationStates.True ? DxfPaperOrientationStates.True : DxfPaperOrientationStates.False;
                }

                if (Annotative != DxfAnnotativeStates.True)
                {
                    return DxfPaperOrientationStates.False;
                }

                IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(PaperOrientationApplication);

                if (data != null)
                {
                    foreach (DxfTypedValue group in data)
                    {
                        if (group.TypeCode == 1070)
                        {
                            return group.Value.Trim() == "0" ? DxfPaperOrientationStates.False : DxfPaperOrientationStates.True;
                        }
                    }
                }

                return DxfPaperOrientationStates.False;
            }
        }

        /// <summary>The kinds of object as the annotative members take them.</summary>
        private enum AnnotationKind
        {
            /// <summary>Never annotative.</summary>
            None,

            /// <summary>An entity with its own flag and contexts.</summary>
            Entity,

            /// <summary>An INSERT, annotative as its block is.</summary>
            Reference,

            /// <summary>A text style or a dimension style, whose flag is all AutoCAD keeps.</summary>
            Style,

            /// <summary>A block, whose flag its INSERTs take.</summary>
            Block,

            /// <summary>A multileader style, whose flag is its group 296.</summary>
            MLeaderStyle,
        }

        /// <summary>The kind of the object as the annotative members take it.</summary>
        private AnnotationKind AnnotativeKind()
        {
            switch (this)
            {
                case DxfTextStyleTableRecord _:
                case DxfDimStyleTableRecord _:
                    return AnnotationKind.Style;
                case DxfBlockTableRecord _:
                    return AnnotationKind.Block;
                case DxfMLeaderStyle _:
                    return AnnotationKind.MLeaderStyle;
                case DxfBlockReference _:
                    return AnnotationKind.Reference;
                case DxfEntity _:
                    return _item != null && AnnotativeEntities.Contains(_item.Type) ? AnnotationKind.Entity : AnnotationKind.None;
                default:
                    return AnnotationKind.None;
            }
        }

        /// <summary>Whether the object can keep the way up of the sheet: a TEXT, an MTEXT, an attribute, an INSERT, a text style or a block.</summary>
        private bool Orientable()
            => this is DxfTextStyleTableRecord || this is DxfBlockTableRecord || this is DxfBlockReference || (this is DxfEntity && _item != null && OrientedEntities.Contains(_item.Type));

        /// <summary>The name of the object's kind for a message: its record's type, or its class.</summary>
        private string KindName() => _item?.Type ?? GetType().Name;

        /// <summary>Whether the extended data <c>AcadAnnotative</c> marks the object annotative: the second 1070 after <c>AnnotativeData</c>, not nought.</summary>
        private bool AnnotativeFlag()
        {
            IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(AnnotativeApplication);

            if (data == null)
            {
                return false;
            }

            bool named = false;
            int whole = 0;

            foreach (DxfTypedValue group in data)
            {
                if (group.TypeCode == 1000)
                {
                    named = string.Equals(group.Value.Trim(), AnnotativeDataName, StringComparison.OrdinalIgnoreCase);
                    whole = 0;
                }
                else if (named && group.TypeCode == 1070 && ++whole == 2)
                {
                    return group.Value.Trim() != "0";
                }
            }

            return false;
        }

        /// <summary>The extended data <c>AcadAnnotative</c> of an annotative style, as AutoCAD writes it.</summary>
        private static DxfTypedValue[] AnnotativeOn() => new[]
        {
            new DxfTypedValue(1001, AnnotativeApplication), new DxfTypedValue(1000, AnnotativeDataName), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        /// <summary>The dictionary ACDB_ANNOTATIONSCALES of AcDbContextDataManager of the extension dictionary; null where there is none.</summary>
        private DxfDBDictionary ContextDictionary()
            => ChildDictionary(ChildDictionary(ExtensionDictionary, ContextManagerName), DxfObjectContextManager.AnnotationScales);

        private static DxfDBDictionary ChildDictionary(DxfDBDictionary owner, string name)
            => owner != null && owner.Contains(name) ? owner.GetAt(name) as DxfDBDictionary : null;

        /// <summary>
        /// The records of the contexts of the object's scales, each with the name of its entry, in the order of their dictionary:
        /// those a copy in no file carries of its original (<see cref="DxfItem.CopiedContexts"/>), else those of
        /// ACDB_ANNOTATIONSCALES; none where it has none.
        /// </summary>
        private List<KeyValuePair<string, DxfItem>> ContextEntries() => _item == null ? new List<KeyValuePair<string, DxfItem>>() : ContextEntries(_item);

        /// <summary>The records of the contexts of the scales of an object's record, as <see cref="ContextEntries()"/> gives them.</summary>
        private static List<KeyValuePair<string, DxfItem>> ContextEntries(DxfItem item)
        {
            if (item.CopiedContexts != null)
            {
                return item.CopiedContexts;
            }

            var entries = new List<KeyValuePair<string, DxfItem>>();
            string handle = item.File.ExtensionDictionaryOf(item);
            DxfItem extension = handle == null ? null : item.File.ObjectItem(handle);
            DxfDBDictionary contexts = ChildDictionary(ChildDictionary(extension == null ? null : OfObject(extension) as DxfDBDictionary, ContextManagerName), DxfObjectContextManager.AnnotationScales);

            if (contexts == null)
            {
                return entries;
            }

            foreach (DxfDBDictionaryEntry entry in contexts)
            {
                if (entry.Value?.Item != null)
                {
                    entries.Add(new KeyValuePair<string, DxfItem>(entry.Key, entry.Value.Item));
                }
            }

            return entries;
        }

        /// <summary>The contexts of the object's scales, in the order of their dictionary; none where it has none.</summary>
        private List<ScaleContext> ScaleContexts()
        {
            var held = new List<ScaleContext>();

            foreach (KeyValuePair<string, DxfItem> entry in ContextEntries())
            {
                string marker = null;
                string scale = null;
                bool isDefault = false;

                foreach (DxfTypedValue group in entry.Value.File.ObjectGroupsOf(entry.Value))
                {
                    if (group.TypeCode == 100)
                    {
                        marker = group.Value.Trim();
                    }
                    else if (group.TypeCode == 290 && marker == ContextDataMarker)
                    {
                        isDefault = group.Value.Trim() == "1";
                    }
                    else if (group.TypeCode == 340 && marker == ScaleContextMarker && scale == null)
                    {
                        scale = group.Value.Trim();
                    }
                }

                if (scale != null)
                {
                    held.Add(new ScaleContext(entry.Key, scale, isDefault));
                }
            }

            return held;
        }

        /// <summary>
        /// The contexts of the object of the record types given, as AutoCAD 2021 reads an annotative text, attribute or
        /// definition once it has read its drawing (check D35): all of them, in the order of their dictionary, and the one it is
        /// shown at, that of the scale named 1:1, else the default (290 of 1), whatever CANNOSCALE says; null where it has none
        /// of those types, or none of them is shown.
        /// </summary>
        private protected ShownContexts ContextsOfType(string type, string otherType)
        {
            // Most objects have no extension dictionary: one look at the record says so. A copy carries its original's contexts.
            if (_item == null || (_item.CopiedContexts == null && _item.File.ExtensionDictionaryOf(_item) == null))
            {
                return null;
            }

            List<KeyValuePair<string, DxfItem>> contexts = ContextEntries();

            if (contexts.Count == 0)
            {
                return null;
            }

            var all = new List<DxfItem>();
            var ratios = new List<double>();
            DxfItem unit = null;
            DxfItem fallback = null;
            string unitScale = null;
            string fallbackScale = null;
            bool unitIsDefault = false;
            double unitRatio = 1.0;
            double fallbackRatio = 1.0;

            foreach (KeyValuePair<string, DxfItem> entry in contexts)
            {
                DxfItem item = entry.Value;

                if (item.Type != type && item.Type != otherType)
                {
                    continue;
                }

                string marker = null;
                string scale = null;
                bool isDefault = false;

                foreach (DxfTypedValue group in item.File.ObjectGroupsOf(item))
                {
                    if (group.TypeCode == 100)
                    {
                        marker = group.Value.Trim();
                    }
                    else if (group.TypeCode == 290 && marker == ContextDataMarker)
                    {
                        isDefault = group.Value.Trim() == "1";
                    }
                    else if (group.TypeCode == 340 && marker == ScaleContextMarker && scale == null)
                    {
                        scale = group.Value.Trim();
                    }
                }

                all.Add(item);
                string name = null;
                double each = 1.0;
                bool known = scale != null && ScaleOf(item.File, scale, out name, out each);
                ratios.Add(known ? each : 1.0);

                if (unit == null && known && name == "1:1")
                {
                    unit = item;
                    unitScale = scale;
                    unitIsDefault = isDefault;
                    unitRatio = each;
                }

                if (fallback == null && isDefault)
                {
                    fallback = item;
                    fallbackScale = scale;
                    fallbackRatio = known ? each : 1.0;
                }
            }

            if (unit == null && fallback == null)
            {
                return null;
            }

            if (unit == null || unitIsDefault || fallback == null)
            {
                return new ShownContexts(all, ratios, unit ?? fallback, unit == null || unitIsDefault, 1.0, unit != null ? unitRatio : fallbackRatio, unit != null ? unitRatio : fallbackRatio);
            }

            // The record holds the height at the default's scale: at 1:1 it is that times the default's scale.
            double factor = fallbackScale != null && ScaleOf(_item.File, fallbackScale, out _, out double ratio) && ScaleOf(_item.File, unitScale, out _, out double shown) ? ratio / shown : 1.0;
            return new ShownContexts(all, ratios, unit, false, Guard.IsFinite(factor) && factor > 0.0 ? factor : 1.0, unitRatio, fallbackRatio);
        }

        /// <summary>
        /// The name of a SCALE (300) and its units of the paper over those of the drawing (140 over 141), of the file given, or
        /// of the drawing a file of copies holds them for; false where the handle names none.
        /// </summary>
        private static bool ScaleOf(DxfFile file, string handle, out string name, out double ratio)
        {
            name = null;
            ratio = 1.0;
            file = file.IsDetached && file.Origin != null ? file.Origin : file;
            DxfItem scale = file.ObjectItem(handle);

            if (scale == null || scale.Type != "SCALE")
            {
                return false;
            }

            double paper = double.NaN;
            double drawing = double.NaN;

            foreach (DxfTypedValue group in file.ObjectGroupsOf(scale))
            {
                if (group.TypeCode == 300 && name == null)
                {
                    name = group.Value.Trim();
                }
                else if (group.TypeCode == 140 && Numbers.TryRead(group.Value, out double p))
                {
                    paper = p;
                }
                else if (group.TypeCode == 141 && Numbers.TryRead(group.Value, out double d))
                {
                    drawing = d;
                }
            }

            double r = paper / drawing;
            ratio = Guard.IsFinite(r) && r > 0.0 ? r : 1.0;
            return true;
        }

        /// <summary>
        /// The contexts of an annotative object of a kind: every one of them, the one AutoCAD shows it at, whether that is the
        /// default, whose data the object's own groups hold, and what the height of the record is multiplied by at the shown
        /// scale, the default's scale over the shown one's.
        /// </summary>
        private protected sealed class ShownContexts
        {
            internal ShownContexts(List<DxfItem> all, List<double> ratios, DxfItem shown, bool shownIsDefault, double heightFactor, double shownRatio, double defaultRatio)
            {
                All = all;
                Ratios = ratios;
                Shown = shown;
                ShownIsDefault = shownIsDefault;
                HeightFactor = heightFactor;
                ShownRatio = shownRatio;
                DefaultRatio = defaultRatio;
            }

            /// <summary>Every context of the kind, in the order of the dictionary.</summary>
            internal IReadOnlyList<DxfItem> All { get; }

            /// <summary>The scale of each context of <see cref="All"/>, its units of the paper over those of the drawing; 1 where it names no SCALE.</summary>
            internal IReadOnlyList<double> Ratios { get; }

            /// <summary>The scale of the context shown, as <see cref="Ratios"/> gives one.</summary>
            internal double ShownRatio { get; }

            /// <summary>The scale of the default context, as <see cref="Ratios"/> gives one; that of the context shown where it is the default or there is none.</summary>
            internal double DefaultRatio { get; }

            /// <summary>The context shown: of the scale 1:1, else the default.</summary>
            internal DxfItem Shown { get; }

            /// <summary>Whether the context shown is the default, whose data the object's own groups hold.</summary>
            internal bool ShownIsDefault { get; }

            /// <summary>What the record's height is multiplied by at the shown scale: exactly 1 where it is the default.</summary>
            internal double HeightFactor { get; }
        }

        /// <summary>
        /// The groups of a context that the library reads and writes, as AutoCAD 2021 writes them after the marker
        /// AcDbAnnotScaleObjectContextData and its scale (340): the first of each code given, before the 290 that says whether an
        /// attribute's MTEXT follows; or, for that MTEXT, those after the marker's second turn; where each stands among the
        /// record's groups, to be written in its place.
        /// </summary>
        private protected sealed class ContextGroups
        {
            private readonly DxfItem _item;
            private readonly int[] _codes;
            private readonly int[] _places;
            private readonly double[] _numbers;

            private ContextGroups(DxfItem item, int[] codes)
            {
                _item = item;
                _codes = codes;
                _places = new int[codes.Length];
                _numbers = new double[codes.Length];

                for (int i = 0; i < _places.Length; i++)
                {
                    _places[i] = -1;
                }
            }

            /// <summary>The groups of the codes given, each a slot in their order, of a context as its record holds them now; those of an attribute's MTEXT where embedded is true.</summary>
            internal static ContextGroups Read(DxfItem item, int[] codes, bool embedded = false)
            {
                var context = new ContextGroups(item, codes);
                IReadOnlyList<DxfTypedValue> groups = item.File.GroupsOf(item);
                int turn = embedded ? 2 : 1;
                int seen = 0;
                bool own = false;

                for (int i = 0; i < groups.Count; i++)
                {
                    int code = groups[i].TypeCode;

                    if (code == 100)
                    {
                        own = groups[i].Value.Trim() == ScaleContextMarker && ++seen == turn;
                        continue;
                    }

                    if (!own)
                    {
                        continue;
                    }

                    if (!embedded && (code == 290 || code == 101))
                    {
                        break;
                    }

                    int slot = Array.IndexOf(codes, code);

                    if (slot >= 0 && context._places[slot] < 0 && Numbers.TryRead(groups[i].Value, out double value) && Guard.IsFinite(value))
                    {
                        context._places[slot] = i;
                        context._numbers[slot] = value;
                    }
                }

                return context;
            }

            /// <summary>The context's record.</summary>
            internal DxfItem Item => _item;

            /// <summary>Whether the context holds the group of a slot.</summary>
            internal bool Has(int slot) => _places[slot] >= 0;

            /// <summary>The number of a slot; the fallback where the context holds none.</summary>
            internal double Value(int slot, double fallback) => Has(slot) ? _numbers[slot] : fallback;

            /// <summary>A copy of the numbers of every slot.</summary>
            internal double[] Copy() => (double[])_numbers.Clone();

            /// <summary>Writes a number into a slot the context holds, where it differs to the bit; a whole number for a code of one (70).</summary>
            internal void Write(int slot, double value) => Put(slot, value);

            /// <summary>Writes two numbers, as <see cref="Write(int, double)"/> writes one.</summary>
            internal void Write(int slot, double value, int other, double otherValue)
            {
                Put(slot, value);
                Put(other, otherValue);
            }

            /// <summary>Writes every number given, slot by slot, as <see cref="Write(int, double)"/> writes one.</summary>
            internal void Write(double[] numbers)
            {
                for (int slot = 0; slot < numbers.Length; slot++)
                {
                    Put(slot, numbers[slot]);
                }
            }

            private void Put(int slot, double value)
            {
                if (Has(slot) && BitConverter.DoubleToInt64Bits(value + 0.0) != BitConverter.DoubleToInt64Bits(_numbers[slot] + 0.0))
                {
                    bool whole = _codes[slot] >= 60 && _codes[slot] < 100;
                    _item.File.SetValueAt(_item, _places[slot], whole ? ((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture) : Numbers.Exact(value + 0.0));
                    _numbers[slot] = value;
                }
            }
        }

        /// <summary>
        /// The groups of a context of an MTEXT, and of the MTEXT of a context of an attribute of several lines, that the library
        /// reads and writes, in their slots, as AutoCAD 2021 writes them (check D35b): the attachment (70), the direction (10,
        /// 20, 30), the location (11, 21, 31), the width (40) and the height of the box (41).
        /// </summary>
        private protected static class MTextContext
        {
            internal const int Attached = 0;
            internal const int AlongX = 1;
            internal const int AlongY = 2;
            internal const int AlongZ = 3;
            internal const int AtX = 4;
            internal const int AtY = 5;
            internal const int AtZ = 6;
            internal const int Wide = 7;
            internal const int High = 8;

            internal static readonly int[] Codes = { 70, 10, 20, 30, 11, 21, 31, 40, 41 };

            /// <summary>
            /// The contexts given as a transformation carries them, each with its new numbers, as AutoCAD 2021 carries them: its
            /// location carried, its direction carried and made of unit length, its width stretched as its direction is and the
            /// height of its box as its upright in the plane of the normal given; those of an attribute's MTEXT where embedded is
            /// true. Null for none.
            /// </summary>
            internal static List<KeyValuePair<ContextGroups, double[]>> Moved(IReadOnlyList<DxfItem> contexts, bool embedded, GeoTransform3 transform, GeoVector3 normal)
            {
                if (contexts == null)
                {
                    return null;
                }

                var moved = new List<KeyValuePair<ContextGroups, double[]>>(contexts.Count);

                foreach (DxfItem item in contexts)
                {
                    ContextGroups context = ContextGroups.Read(item, Codes, embedded);
                    double[] numbers = context.Copy();

                    if (context.Has(AtX) && context.Has(AtY))
                    {
                        GeoPoint3 at = transform.Transform(new GeoPoint3(numbers[AtX], numbers[AtY], numbers[AtZ]));
                        numbers[AtX] = at.X;
                        numbers[AtY] = at.Y;
                        numbers[AtZ] = at.Z;
                    }

                    if (context.Has(AlongX) && context.Has(AlongY))
                    {
                        var along = new GeoVector3(numbers[AlongX], numbers[AlongY], numbers[AlongZ]);
                        GeoVector3 turned = transform.Transform(along);
                        GeoVector3 upright = DxfOcsTransform.Cross(normal, along);

                        if (turned.Length > 0.0 && along.Length > 0.0 && upright.Length > 0.0)
                        {
                            GeoVector3 unit = DxfOcsTransform.Normalized(turned);
                            numbers[AlongX] = unit.X;
                            numbers[AlongY] = unit.Y;
                            numbers[AlongZ] = unit.Z;
                            numbers[Wide] *= turned.Length / along.Length;
                            numbers[High] *= transform.Transform(upright.Divide(upright.Length)).Length;
                        }
                    }

                    moved.Add(new KeyValuePair<ContextGroups, double[]>(context, numbers));
                }

                return moved;
            }
        }

        /// <summary>Writes the numbers worked out for contexts into them, slot by slot; nothing for none.</summary>
        private protected static void WriteMoved(List<KeyValuePair<ContextGroups, double[]>> contexts)
        {
            if (contexts == null)
            {
                return;
            }

            foreach (KeyValuePair<ContextGroups, double[]> context in contexts)
            {
                context.Key.Write(context.Value);
            }
        }

        /// <summary>
        /// Gives a copy in no file, just made of the object, copies held in no file of the records of the contexts the object
        /// holds, its own and those of each ATTRIB of an INSERT, as AutoCAD 2021's <c>Clone</c> keeps them (check D35c); nothing
        /// for an object of none.
        /// </summary>
        private protected void CarryContextsTo(DxfDBObject copy)
        {
            List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> held = HeldByRecords(_item, this is DxfBlockReference);

            foreach (KeyValuePair<int, List<KeyValuePair<string, DxfItem>>> record in held ?? new List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>>())
            {
                (record.Key == 0 ? copy._item : copy._item.File.FollowerOf(copy._item, record.Key)).CopiedContexts = record.Value;
            }
        }

        /// <summary>
        /// Copies held in no file of the records of the contexts an entity's record holds, and where <paramref name="attributes"/>
        /// is true each of its ATTRIBs', by the index of the record (0 the entity's own), in the order of the records; null where
        /// none holds any.
        /// </summary>
        internal static List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> HeldByRecords(DxfItem item, bool attributes)
        {
            List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> held = null;
            List<KeyValuePair<string, DxfItem>> own = HeldContexts(item);

            if (own != null)
            {
                held = new List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> { new KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>(0, own) };
            }

            if (!attributes)
            {
                return held;
            }

            DxfFile file = item.File;
            int count = file.RecordCount(item);

            for (int r = 1; r < count; r++)
            {
                List<KeyValuePair<string, DxfItem>> theirs = file.RecordType(item, r) == "ATTRIB" ? HeldContexts(file.FollowerOf(item, r)) : null;

                if (theirs != null)
                {
                    (held = held ?? new List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>>()).Add(new KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>(r, theirs));
                }
            }

            return held;
        }

        /// <summary>Copies held in no file of the records of the contexts of an object's record, each with the name of its entry; null for one of none.</summary>
        private static List<KeyValuePair<string, DxfItem>> HeldContexts(DxfItem item)
        {
            if (item.CopiedContexts == null && !item.File.OpensWithExtensionDictionary(item))
            {
                return null;
            }

            List<KeyValuePair<string, DxfItem>> entries = ContextEntries(item);

            if (entries.Count == 0)
            {
                return null;
            }

            var held = new List<KeyValuePair<string, DxfItem>>(entries.Count);

            foreach (KeyValuePair<string, DxfItem> entry in entries)
            {
                held.Add(new KeyValuePair<string, DxfItem>(entry.Key, entry.Value.File.CopyOf(entry.Value)));
            }

            return held;
        }

        /// <summary>
        /// Writes the contexts a copy carried into the drawing it was just added to, as AutoCAD 2021 writes those of its copy
        /// (check D35c): for each record that carried some, the entity's own (0) or an ATTRIB's, an extension dictionary owning
        /// AcDbContextDataManager, that one ACDB_ANNOTATIONSCALES, and that one a record of each context under its name, as it
        /// was carried; nothing for none. Where the drawing is not the copy's <paramref name="origin"/>, each context points at
        /// the scale <see cref="ScalesFor"/> gives it there, and one whose scale its origin has not is left out.
        /// </summary>
        internal static void WriteCopiedContexts(DxfItem item, List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> carried, DxfFile origin)
        {
            if (carried == null)
            {
                return;
            }

            Dictionary<string, string> scaleOf = origin == null || origin == item.File ? null : ScalesFor(origin, item.File, carried);

            foreach (KeyValuePair<int, List<KeyValuePair<string, DxfItem>>> record in carried)
            {
                DxfItem owner = record.Key == 0 ? item : item.File.FollowerOf(item, record.Key);
                owner.CopiedContexts = null;
                DxfFile file = owner.File;
                DxfItem extension = file.AddObject("DICTIONARY", owner.Handle, b =>
                {
                    b.Group(100, "AcDbDictionary");
                    b.Group(280, "1");
                    b.Group(281, "1");
                });

                file.SetExtensionDictionary(owner, extension.Handle);
                DxfItem manager = ((DxfDBDictionary)OfObject(extension)).Add(ContextManagerName, "DICTIONARY", OwnedDictionary);
                var scales = (DxfDBDictionary)OfObject(((DxfDBDictionary)OfObject(manager)).Add(DxfObjectContextManager.AnnotationScales, "DICTIONARY", OwnedDictionary));

                foreach (KeyValuePair<string, DxfItem> context in record.Value)
                {
                    IReadOnlyList<DxfTypedValue> groups = context.Value.File.ObjectGroupsOf(context.Value);

                    if (scaleOf != null && !scaleOf.ContainsKey(ScaleHandleOf(groups) ?? string.Empty))
                    {
                        continue;
                    }

                    scales.Add(context.Key, context.Value.Type, b =>
                    {
                        foreach (DxfTypedValue group in groups)
                        {
                            // The scales of the drawing it came from named as this drawing's, an embedded MTEXT's among them.
                            b.Group(group.TypeCode, scaleOf != null && group.TypeCode == 340 && scaleOf.TryGetValue(group.Value.Trim(), out string mapped) ? mapped : group.Value);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Whether a drawing holds a list of scales a copy's contexts can stand on: one of R2007 (AC1021) or later, of an OBJECTS
        /// section, whose ACAD_SCALELIST is a dictionary or is not there yet.
        /// </summary>
        internal static bool HoldsScales(DxfFile file)
        {
            DxfDBDictionary root = file.Database?.NamedObjectsDictionary;
            return root != null && file.Supports(1021) && (!root.Contains("ACAD_SCALELIST") || root.GetAt("ACAD_SCALELIST") is DxfDBDictionary);
        }

        /// <summary>
        /// The scales of a drawing (<paramref name="target"/>) the contexts copied from another (<paramref name="origin"/>) stand
        /// on, by the handle of the other's SCALE, as AutoCAD 2021's <c>WblockCloneObjects</c> and <c>Insert</c> find them (check
        /// D35d): the scale of the name, letters of either case the same, where its units give the same scale, to the twelfth
        /// figure; else one added to the list as <see cref="DxfObjectContextCollection.AddContext"/> adds it, of the other's units,
        /// under its name where the list has none of it, else under the name and <c>_1</c>, <c>_2</c> and on, the first the
        /// list has not. A context whose scale the other drawing has not is given none.
        /// </summary>
        private static Dictionary<string, string> ScalesFor(DxfFile origin, DxfFile target, List<KeyValuePair<int, List<KeyValuePair<string, DxfItem>>>> carried)
        {
            var scaleOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            DxfObjectContextCollection list = target.Database.ObjectContextManager.GetContextCollection(DxfObjectContextManager.AnnotationScales);

            foreach (KeyValuePair<int, List<KeyValuePair<string, DxfItem>>> record in carried)
            {
                foreach (KeyValuePair<string, DxfItem> context in record.Value)
                {
                    string handle = ScaleHandleOf(context.Value.File.ObjectGroupsOf(context.Value));
                    DxfItem item = handle == null || scaleOf.ContainsKey(handle) ? null : origin.ObjectItem(handle);

                    if (item == null || item.Type != "SCALE" || item.IsRemoved)
                    {
                        continue;
                    }

                    var theirs = new DxfAnnotationScale(item);
                    string name = theirs.Name;
                    DxfAnnotationScale ours = DxfObjectContextCollection.Find(target.Database, name);

                    for (int n = 1; ours != null && !SameScale(ours, theirs); n++)
                    {
                        name = theirs.Name + "_" + n.ToString(CultureInfo.InvariantCulture);
                        ours = DxfObjectContextCollection.Find(target.Database, name);
                    }

                    if (ours == null)
                    {
                        ours = new DxfAnnotationScale { Name = name, PaperUnits = theirs.PaperUnits, DrawingUnits = theirs.DrawingUnits };
                        list.AddContext(ours);
                    }

                    scaleOf[handle] = ours.Handle;
                }
            }

            return scaleOf;
        }

        /// <summary>Whether two scales give the same scale, their units of the paper over those of the drawing, to the twelfth figure.</summary>
        private static bool SameScale(DxfAnnotationScale a, DxfAnnotationScale b)
        {
            double x = a.Scale;
            double y = b.Scale;
            return Math.Abs(x - y) <= 1E-12 * Math.Max(Math.Abs(x), Math.Abs(y));
        }

        /// <summary>The handle of the SCALE a context's record points at, the first 340 of its AcDbAnnotScaleObjectContextData; null where it has none.</summary>
        private static string ScaleHandleOf(IReadOnlyList<DxfTypedValue> groups)
        {
            string marker = null;

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 100)
                {
                    marker = group.Value?.Trim();
                }
                else if (group.TypeCode == 340 && marker == "AcDbAnnotScaleObjectContextData")
                {
                    return group.Value?.Trim();
                }
            }

            return null;
        }

        /// <summary>The groups of a dictionary of an object's contexts after its type, owner and reactors, as AutoCAD writes them.</summary>
        private static void OwnedDictionary(DxfFile.Builder b)
        {
            b.Group(100, "AcDbDictionary");
            b.Group(281, "1");
        }

        /// <summary>A context of a scale an object holds: its entry's name, the handle of its SCALE and whether it is the default.</summary>
        private readonly struct ScaleContext
        {
            internal ScaleContext(string key, string scale, bool isDefault)
            {
                Key = key;
                Scale = scale;
                IsDefault = isDefault;
            }

            internal string Key { get; }

            internal string Scale { get; }

            internal bool IsDefault { get; }
        }
    }
}
