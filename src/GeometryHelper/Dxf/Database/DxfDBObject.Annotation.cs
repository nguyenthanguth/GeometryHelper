using System;
using System.Collections.Generic;
using GeometryHelper.Export;

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
        /// by its own group 293 (<see cref="DxfMLeader.EnableAnnotationScale"/>) as well; an INSERT by its block; a multileader
        /// style by its group 296. <see cref="DxfAnnotativeStates.NotApplicable"/> for every other object, as a line.
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
                        DxfBlockTableRecord block = ((DxfBlockReference)this).BlockTableRecord;
                        return block != null && block.Annotative == DxfAnnotativeStates.True ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
                    case AnnotationKind.MLeaderStyle:
                        return _item != null && _item.File.ValueOf(_item, 296)?.Trim() == "1" ? DxfAnnotativeStates.True : DxfAnnotativeStates.False;
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
        /// (290 of AcDbObjectContextData), are all the library reads of a context, but for the angle and two points of the
        /// context of a TEXT, an attribute or a definition, which <see cref="DxfDBText.Position"/> reads and writes; the rest
        /// of AutoCAD's record of it is kept as it is.
        /// </summary>
        /// <param name="context">The scale, of the object's drawing's list.</param>
        /// <returns>Whether it is; false for a scale of another drawing or in none, and for an object with no contexts, as one not
        /// annotative.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the scale is null.</exception>
        public bool HasContext(DxfObjectContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!(context is DxfAnnotationScale scale) || scale.Item == null || _item == null || scale.Item.File != _item.File)
            {
                return false;
            }

            foreach (ScaleContext held in ScaleContexts())
            {
                if (string.Equals(held.Scale, scale.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
        /// subclass AcDbObjectContextData), which no other is made in its place; or when the object is erased.</exception>
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

            if (held.Count == 1)
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

        /// <summary>The contexts of the object's scales, in the order of their dictionary; none where it has none.</summary>
        private List<ScaleContext> ScaleContexts()
        {
            var held = new List<ScaleContext>();
            DxfDBDictionary contexts = ContextDictionary();

            if (contexts == null)
            {
                return held;
            }

            foreach (DxfDBDictionaryEntry entry in contexts)
            {
                if (entry.Value?.Item == null)
                {
                    continue;
                }

                string marker = null;
                string scale = null;
                bool isDefault = false;

                foreach (DxfTypedValue group in entry.Value.Item.File.ObjectGroupsOf(entry.Value.Item))
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
            // Most objects have no extension dictionary: one look at the record says so.
            if (_item == null || _item.File.ExtensionDictionaryOf(_item) == null)
            {
                return null;
            }

            DxfDBDictionary contexts = ContextDictionary();

            if (contexts == null)
            {
                return null;
            }

            var all = new List<DxfItem>();
            DxfItem unit = null;
            DxfItem fallback = null;
            string unitScale = null;
            string fallbackScale = null;
            bool unitIsDefault = false;

            foreach (DxfDBDictionaryEntry entry in contexts)
            {
                DxfItem item = entry.Value?.Item;

                if (item == null || (item.Type != type && item.Type != otherType))
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

                if (unit == null && scale != null && ScaleOf(item.File, scale, out string name, out _) && name == "1:1")
                {
                    unit = item;
                    unitScale = scale;
                    unitIsDefault = isDefault;
                }

                if (fallback == null && isDefault)
                {
                    fallback = item;
                    fallbackScale = scale;
                }
            }

            if (unit == null && fallback == null)
            {
                return null;
            }

            if (unit == null || unitIsDefault || fallback == null)
            {
                return new ShownContexts(all, unit ?? fallback, unit == null || unitIsDefault, 1.0);
            }

            // The record holds the height at the default's scale: at 1:1 it is that times the default's scale.
            double factor = fallbackScale != null && ScaleOf(_item.File, fallbackScale, out _, out double ratio) && ScaleOf(_item.File, unitScale, out _, out double shown) ? ratio / shown : 1.0;
            return new ShownContexts(all, unit, false, Guard.IsFinite(factor) && factor > 0.0 ? factor : 1.0);
        }

        /// <summary>The name of a SCALE (300) and its units of the paper over those of the drawing (140 over 141); false where the handle names none.</summary>
        private static bool ScaleOf(DxfFile file, string handle, out string name, out double ratio)
        {
            name = null;
            ratio = 1.0;
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
            internal ShownContexts(List<DxfItem> all, DxfItem shown, bool shownIsDefault, double heightFactor)
            {
                All = all;
                Shown = shown;
                ShownIsDefault = shownIsDefault;
                HeightFactor = heightFactor;
            }

            /// <summary>Every context of the kind, in the order of the dictionary.</summary>
            internal IReadOnlyList<DxfItem> All { get; }

            /// <summary>The context shown: of the scale 1:1, else the default.</summary>
            internal DxfItem Shown { get; }

            /// <summary>Whether the context shown is the default, whose data the object's own groups hold.</summary>
            internal bool ShownIsDefault { get; }

            /// <summary>What the record's height is multiplied by at the shown scale: exactly 1 where it is the default.</summary>
            internal double HeightFactor { get; }
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
