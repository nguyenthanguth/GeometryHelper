using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        // The class of the contexts of a HATCH and a MULTILEADER, as AutoCAD 2021 lists them in CLASSES; those of the other
        // kinds are named where the entity is (DxfDBText, DxfMText, DxfBlockReference).
        private const string HatchContextType = "ACDB_HATCHSCALECONTEXTDATA_CLASS";
        private const string MLeaderContextType = "ACDB_MLEADEROBJECTCONTEXTDATA_CLASS";
        private const string HatchContextMarker = "AcDbHatchObjectContextData";

        // The entities the library makes annotative, each by the context AutoCAD 2021 gives it.
        private static readonly HashSet<string> MadeAnnotative = new HashSet<string>(StringComparer.Ordinal)
        {
            "TEXT",
            "MTEXT",
            "ATTDEF",
            "HATCH",
            "MULTILEADER",
        };

        /// <summary>
        /// Gets the scales the object is shown at, as AutoCAD's <c>ObjectContexts.GetContexts</c>: the scale each context under
        /// ACDB_ANNOTATIONSCALES points at, in the order of its dictionary (the default first, as AutoCAD writes it), each a
        /// <see cref="DxfAnnotationScale"/> of the drawing's list. An INSERT of an annotative block, or an attribute of one, that
        /// holds no context is shown at the scale named 1:1 (<see cref="HasContext"/>), which this gives where the drawing's list
        /// has it. A text of two scales, 1:1 and 1:50, gives those two; an object that is not annotative gives none.
        /// </summary>
        /// <returns>The scales; none for an object that is never annotative or is not annotative now, and for one in no file.</returns>
        public IReadOnlyList<DxfObjectContext> GetContexts()
        {
            var scales = new List<DxfObjectContext>();

            if (_item == null || AnnotativeKind() == AnnotationKind.None)
            {
                return scales;
            }

            DxfFile own = _item.CopiedContexts != null ? _item.File.Origin : _item.File;

            foreach (ScaleContext held in ScaleContexts())
            {
                DxfItem scale = own.ObjectItem(held.Scale);

                if (scale != null && scale.Type == "SCALE" && !scale.IsRemoved)
                {
                    scales.Add(new DxfAnnotationScale(scale));
                }
            }

            if (scales.Count == 0 && own.Database != null)
            {
                DxfAnnotationScale unit = DxfObjectContextCollection.Find(own.Database, "1:1");

                if (unit != null && HasContext(unit))
                {
                    scales.Add(unit);
                }
            }

            return scales;
        }

        /// <summary>
        /// Makes the object shown at another scale, as AutoCAD's <c>ObjectContexts.AddContext</c> (the command OBJECTSCALE, Add): a
        /// context of the scale is written next to the others under ACDB_ANNOTATIONSCALES of its extension dictionary, named
        /// <c>*A</c> and one more than the highest number the object holds, not the default (290 of 0), and made from the default
        /// context as AutoCAD 2021 makes it (checked on a TEXT, an MTEXT, an ATTDEF, a HATCH and an INSERT of an annotative block
        /// drawn at 1:1 and 1:50): a TEXT and an attribute definition keep their point, angle and mode; an MTEXT keeps its place
        /// and direction and has its width, the height of its box and the width and height of its words (40, 41, 42, 43)
        /// multiplied by the default's scale over the new one's (an MTEXT 40 wide at 1:1 is 2000 wide at 1:50); a HATCH has the
        /// lines of its pattern (43, 44, 45, 46, 49) multiplied so, and 1 over the scale in its group 40 (50 at 1:50); an INSERT
        /// has its three factors multiplied so (50 at 1:50 for a 1 at 1:1) and each attribute of it that holds contexts is
        /// given one as well, its point moved out from the INSERT's by the same factor: the attribute at (300, 1003) of an
        /// INSERT at (300, 1000) is at (300, 1150) at 1:50. An INSERT that holds no context gets the scale as its one context,
        /// the default, its record's factors and those of the context multiplied by 1 over the scale, as AutoCAD 2021 gave
        /// it. A MULTILEADER holding text is laid out again: its scale (40) is 1 over the new scale's, its sizes (the
        /// text height, the arrowhead, the landing gap, the dogleg) multiplied by the default's scale over the new one's, the landing
        /// and its lines kept, and the content moved out from the landing by the new dogleg: text 0.2 high at 1:50 is 0.4 at 1:100,
        /// a dogleg of 0.36 is 0.72, and the words stand 9.72 right of the landing (check GHAN4). The record of the other kinds is
        /// kept: their size at the other scale is worked out from it.
        /// </summary>
        /// <param name="context">The scale, of the object's drawing's list.</param>
        /// <exception cref="ArgumentNullException">Thrown when the scale is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the scale is not one of this drawing's list.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is not annotative (<see cref="Annotative"/>), as AutoCAD
        /// answers eNotApplicable; is in no file, is erased, or is a copy in no file that carries its original's contexts.</exception>
        /// <exception cref="NotSupportedException">Thrown for a dimension, whose picture the library does not lay out (the
        /// presenter does: a context per scale each with its own block), for a multileader that holds neither words nor a block, and
        /// for the other annotative kinds whose contexts the library does not make for a second scale yet.</exception>
        public void AddContext(DxfObjectContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            DxfFile file = InFile("be shown at a scale");

            if (_item.CopiedContexts != null)
            {
                throw new InvalidOperationException(
                    "The "
                        + KindName()
                        + " is a copy in no file that carries its original's contexts: add it to a drawing first."
                );
            }

            DxfAnnotationScale scale = DxfObjectContextCollection.Listed(file.Database, context, nameof(context));

            if (Annotative != DxfAnnotativeStates.True)
            {
                throw new InvalidOperationException(
                    "A "
                        + KindName()
                        + " that is not annotative is not shown at a scale, as AutoCAD answers eNotApplicable."
                );
            }

            if (HasContext(scale))
            {
                return;
            }

            string kind = _item.Type;

            if (
                kind != "TEXT"
                && kind != "MTEXT"
                && kind != "ATTDEF"
                && kind != "HATCH"
                && kind != "INSERT"
                && kind != "MULTILEADER"
            )
            {
                throw new NotSupportedException(
                    "A "
                        + KindName()
                        + " is not shown at a second scale here yet: AutoCAD lays it out again at each scale, which the library does not do."
                );
            }

            List<ScaleContext> held = ScaleContexts();

            if (held.Count == 0 && kind == "INSERT")
            {
                ((DxfBlockReference)this).AddFirstContext(scale);
                return;
            }

            if (held.Count == 0)
            {
                throw new InvalidOperationException(
                    "The " + KindName() + " holds no context to be shown at another scale from."
                );
            }

            ScaleContext first = held.Find(c => c.IsDefault);
            DxfItem source = FindContext(string.IsNullOrEmpty(first.Key) ? held[0].Key : first.Key);
            double factor = ScaleRatioOf(file, ScaleHandleOf(source.File.ObjectGroupsOf(source))) / scale.Scale;

            if (!Guard.IsFinite(factor) || factor <= 0.0)
            {
                throw new InvalidOperationException(
                    "The scale of the default context of the " + KindName() + " is not one the library can work from."
                );
            }

            if (kind == "MULTILEADER")
            {
                WriteContext(
                    EnsureContextDictionary(_item),
                    MLeaderContextType,
                    scale,
                    false,
                    MLeaderLaidOut(ContextData(source), scale, factor)
                );
                return;
            }

            AddDerived(_item, source, scale, factor, null);

            if (kind == "INSERT")
            {
                AddToAttributes(_item, scale, factor, InsertPointOf(source));
            }
        }

        /// <summary>
        /// Makes an entity annotative as AutoCAD 2021 writes it, at the scale of its place: its extended data
        /// <c>AcadAnnotative</c> (a multileader its group 293 instead), an extension dictionary holding
        /// AcDbContextDataManager and ACDB_ANNOTATIONSCALES, and in it the default context of the scale CANNOSCALE names, or 1:1
        /// in paper space. A HATCH has the lines of its pattern multiplied by 1 over the scale, as AutoCAD 2021 multiplies them.
        /// </summary>
        private void MakeEntityAnnotative(DxfFile file)
        {
            string kind = _item.Type;

            if (!MadeAnnotative.Contains(kind))
            {
                throw new NotSupportedException(
                    "A "
                        + KindName()
                        + " is not made annotative here yet: AutoCAD lays out one picture of it at each scale, which the library does not write."
                );
            }

            if (!file.Supports(1021))
            {
                throw new InvalidOperationException(
                    "A DXF older than R2007 (AC1021) has no annotative objects: AutoCAD 2008, which saves R2007, brought them."
                );
            }

            DxfDatabase database = file.Database;
            string place = (this as DxfEntity)?.BlockName;
            string name =
                place != null && place.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase)
                    ? "1:1"
                    : database?.Cannoscale;
            DxfAnnotationScale scale = database == null ? null : DxfObjectContextCollection.Find(database, name);

            if (scale == null)
            {
                throw new InvalidOperationException(
                    "The drawing's list of scales has no scale named "
                        + (name ?? "(none)")
                        + ", which the object is first shown at: add it with DxfObjectContextCollection.AddContext."
                );
            }

            double ratio = scale.Scale;
            var groups = new List<KeyValuePair<int, string>>();

            switch (kind)
            {
                case "TEXT":
                case "ATTDEF":
                    TextContext(groups, kind == "ATTDEF");
                    break;
                case "MTEXT":
                    MTextContextGroups(groups);
                    break;
                case "HATCH":
                    HatchContextGroups(groups, 1.0 / ratio);
                    break;
                default:
                    MLeaderContextGroups(groups);
                    break;
            }

            string type = ContextTypeOf(kind);
            file.AddClass(type, ClassOf(type), 1153);

            if (kind == "MULTILEADER")
            {
                ((DxfMLeader)this).EnableAnnotationScale = true;
            }
            else
            {
                XData = AnnotativeOn();
            }

            DxfDBDictionary scales = EnsureContextDictionary(_item);
            WriteContext(scales, type, scale, true, groups);
        }

        /// <summary>
        /// Gives an attribute of an INSERT of an annotative block, which holds no context, the one AutoCAD 2021's -INSERT gives it
        /// (probe GHA3, a block of a circle and an attribute definition at 1:50): a context of the scale, the default, of the class
        /// of an attribute's (ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS) holding the attribute's angle and point as its record
        /// has them; no extended data <c>AcadAnnotative</c> comes with it. One that holds contexts already is left.
        /// </summary>
        internal void AddFirstAttributeContext(DxfAnnotationScale scale)
        {
            DxfFile file = InFile("be shown at a scale");

            if (_item.Type != "ATTRIB")
            {
                throw new InvalidOperationException("Only an attribute of an INSERT is given a first context here.");
            }

            if (ContextEntries(_item).Count > 0)
            {
                return;
            }

            var groups = new List<KeyValuePair<int, string>>();
            TextContext(groups, true);
            string type = DxfDBText.AttributeContextType;
            file.AddClass(type, ClassOf(type), 1153);
            WriteContext(EnsureContextDictionary(_item), type, scale, true, groups);
        }

        /// <summary>The class of the contexts of an entity's kind.</summary>
        private static string ContextTypeOf(string kind)
        {
            switch (kind)
            {
                case "TEXT":
                    return DxfDBText.TextContextType;
                case "ATTDEF":
                    return DxfDBText.AttributeContextType;
                case "MTEXT":
                    return DxfMText.ContextType;
                case "HATCH":
                    return HatchContextType;
                default:
                    return MLeaderContextType;
            }
        }

        /// <summary>Writes a context into ACDB_ANNOTATIONSCALES: the groups every context begins with, then those of its kind.</summary>
        private static void WriteContext(
            DxfDBDictionary scales,
            string type,
            DxfAnnotationScale scale,
            bool isDefault,
            List<KeyValuePair<int, string>> groups
        )
        {
            scales.Add(NextContextKey(scales), type, b =>
            {
                b.Group(100, ContextDataMarker);
                b.Group(70, "4");
                b.Group(290, isDefault ? "1" : "0");
                b.Group(100, ScaleContextMarker);
                b.Group(340, scale.Handle);

                foreach (KeyValuePair<int, string> group in groups)
                {
                    b.Group(group.Key, group.Value);
                }
            });
        }

        /// <summary>The number of a group of the object's record; the fallback where it holds none.</summary>
        private double Rec(int code, double fallback) => RecordNumber(_item, code, fallback);

        private static KeyValuePair<int, string> Pair(int code, double value) =>
            new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0));

        private static KeyValuePair<int, string> Pair(int code, int value) =>
            new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// The groups of the context of a TEXT or an attribute definition, as AutoCAD 2021 writes them (checked on a TEXT, an
        /// ATTDEF at 1:1 and 1:50): the horizontal mode (70), the angle in degrees (50), the point (10, 20), the second point
        /// (11, 21) where the text is not aligned left on its baseline, else (0, 0), and for a definition the 290 of 0 that says
        /// no MTEXT follows.
        /// </summary>
        private void TextContext(List<KeyValuePair<int, string>> groups, bool definition)
        {
            bool aligned = Rec(72, 0.0) != 0.0 || Rec(73, 0.0) != 0.0;
            groups.Add(Pair(70, (int)Rec(72, 0.0)));
            groups.Add(Pair(50, Rec(50, 0.0)));
            groups.Add(Pair(10, Rec(10, 0.0)));
            groups.Add(Pair(20, Rec(20, 0.0)));
            groups.Add(Pair(11, aligned ? Rec(11, 0.0) : 0.0));
            groups.Add(Pair(21, aligned ? Rec(21, 0.0) : 0.0));

            if (definition)
            {
                groups.Add(Pair(290, 0));
            }
        }

        /// <summary>
        /// The groups of the context of an MTEXT, as AutoCAD 2021 writes them (checked at 1:1 and 1:50): the attachment (70), the
        /// direction (10, 20, 30), the location (11, 21, 31), the width (40), the height of its box (41), the width and height of
        /// its words (42, 43) and the 71 of 0.
        /// </summary>
        private void MTextContextGroups(List<KeyValuePair<int, string>> groups)
        {
            var mtext = (DxfMText)this;
            GeoVector3 along = mtext.Direction;
            groups.Add(Pair(70, (int)Rec(71, 1.0)));
            groups.Add(Pair(10, along.X));
            groups.Add(Pair(20, along.Y));
            groups.Add(Pair(30, along.Z));
            groups.Add(Pair(11, Rec(10, 0.0)));
            groups.Add(Pair(21, Rec(20, 0.0)));
            groups.Add(Pair(31, Rec(30, 0.0)));
            groups.Add(Pair(40, Rec(41, 0.0)));
            groups.Add(Pair(41, 0.0));
            groups.Add(Pair(42, mtext.ActualWidth));
            groups.Add(Pair(43, mtext.ActualHeight));
            groups.Add(Pair(71, 0));
        }

        /// <summary>
        /// The groups of the context of a HATCH, as AutoCAD 2021 writes them (checked at 1:1 and 1:50, on one loop and on two):
        /// the subclass AcDbHatchObjectContextData, the lines of the pattern as the record holds them (78, then 53, 43, 44, 45,
        /// 46, 79 and the dashes 49 of each), the scale (40), 1 over the scale of the context, the direction of the pattern (10,
        /// 20, 30), the cosine and sine of its angle, the number of loops (90) and for each loop its type (90) and a 290 of 1.
        /// The lines of the record are multiplied by <paramref name="factor"/> first, as AutoCAD 2021 multiplies them when it
        /// makes a hatch annotative at a scale.
        /// </summary>
        private void HatchContextGroups(List<KeyValuePair<int, string>> groups, double factor)
        {
            DxfFile file = _item.File;
            IReadOnlyList<DxfTypedValue> record = file.GroupsOf(_item);
            int lines = 0;
            int at = -1;

            for (int i = 0; i < record.Count; i++)
            {
                if (record[i].TypeCode == 78 && at < 0)
                {
                    at = i;
                    Numbers.TryRead(record[i].Value, out double count);
                    lines = (int)count;
                }
            }

            groups.Add(new KeyValuePair<int, string>(100, HatchContextMarker));
            groups.Add(Pair(78, lines));

            if (at >= 0)
            {
                // The lines of the record, scaled in place, and given to the context as they stand then.
                for (int i = at + 1; i < record.Count; i++)
                {
                    int code = record[i].TypeCode;

                    if (code == 98 || code == 47 || code >= 1000 || code == 450 || code == 100)
                    {
                        break;
                    }

                    if (code == 43 || code == 44 || code == 45 || code == 46 || code == 49)
                    {
                        if (Numbers.TryRead(record[i].Value, out double number))
                        {
                            string scaled = record[i].Value.Trim();

                            if (factor != 1.0)
                            {
                                scaled = Numbers.Exact(number * factor + 0.0);
                                file.SetValueAt(_item, i, scaled);
                            }

                            groups.Add(new KeyValuePair<int, string>(code, scaled));
                        }
                    }
                    else if (code == 53 || code == 79)
                    {
                        groups.Add(new KeyValuePair<int, string>(code, record[i].Value.Trim()));
                    }
                }
            }

            double angle = Rec(52, 0.0) * (Math.PI / 180.0);
            groups.Add(Pair(40, factor));
            groups.Add(Pair(10, Math.Cos(angle)));
            groups.Add(Pair(20, Math.Sin(angle)));
            groups.Add(Pair(30, 0.0));

            // The loops: the count (91) and the type of each (92) in the order the record lists them.
            var types = new List<string>();
            bool inLoops = false;

            foreach (DxfTypedValue group in record)
            {
                if (group.TypeCode == 91)
                {
                    inLoops = true;
                }
                else if (inLoops && group.TypeCode == 92)
                {
                    types.Add(group.Value.Trim());
                }
                else if (group.TypeCode == 75)
                {
                    break;
                }
            }

            groups.Add(Pair(90, types.Count));

            foreach (string type in types)
            {
                groups.Add(new KeyValuePair<int, string>(90, type));
                groups.Add(Pair(290, 1));
            }
        }

        /// <summary>
        /// The groups of the context of a MULTILEADER: those of its own context data (from its 300 <c>CONTEXT_DATA{</c> to its
        /// 301 <c>}</c>, the braces left out), as AutoCAD 2021 writes the context of the scale it is made annotative at.
        /// </summary>
        private void MLeaderContextGroups(List<KeyValuePair<int, string>> groups)
        {
            bool inside = false;

            foreach (DxfTypedValue group in _item.File.GroupsOf(_item))
            {
                if (group.TypeCode == 300 && group.Value.Trim() == "CONTEXT_DATA{")
                {
                    inside = true;
                }
                else if (group.TypeCode == 301 && inside)
                {
                    return;
                }
                else if (inside)
                {
                    groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
                }
            }
        }

        /// <summary>The point of an INSERT's context (10, 20); the world's origin where it holds none.</summary>
        private static GeoPoint3 InsertPointOf(DxfItem context)
        {
            double x = 0.0;
            double y = 0.0;
            string marker = null;
            bool gotX = false;
            bool gotY = false;

            foreach (DxfTypedValue group in context.File.ObjectGroupsOf(context))
            {
                if (group.TypeCode == 100)
                {
                    marker = group.Value.Trim();
                }
                else if (marker == ScaleContextMarker && group.TypeCode == 10 && !gotX)
                {
                    gotX = Numbers.TryRead(group.Value, out x);
                }
                else if (marker == ScaleContextMarker && group.TypeCode == 20 && !gotY)
                {
                    gotY = Numbers.TryRead(group.Value, out y);
                }
            }

            return new GeoPoint3(x, y, 0.0);
        }

        /// <summary>
        /// How an annotative TEXT, attribute, MTEXT or HATCH is drawn at a scale, the presenter's reading of AutoCAD's: the number
        /// its sizes read at the context the library reads it at (<see cref="HasContext"/>'s 1:1, else the default) are multiplied
        /// by to be the sizes at <paramref name="at"/> (the paper size over the scale), or, for a hatch, the number the lines of
        /// its pattern, which the record holds at the default's scale, are multiplied by. 1 for an object not annotative or of
        /// no context of a kind it knows. An object not shown at the scale is drawn as it is read where
        /// <paramref name="allVisible"/> is true (ANNOALLVISIBLE), and not at all where it is false.
        /// </summary>
        internal bool TryFactorAt(DxfAnnotationScale at, bool allVisible, out double factor)
        {
            factor = 1.0;
            string type = null;
            string other = null;
            bool hatch = false;

            switch (_item?.Type)
            {
                case "TEXT":
                case "ATTDEF":
                case "ATTRIB":
                    type = DxfDBText.TextContextType;
                    other = DxfDBText.AttributeContextType;
                    break;
                case "MTEXT":
                    type = DxfMText.ContextType;
                    other = type;
                    break;
                case "HATCH":
                    type = HatchContextType;
                    other = type;
                    hatch = true;
                    break;
                default:
                    return true;
            }

            // An annotative object holds an extension dictionary: one look at the record says it has none.
            if (
                at == null
                || (_item.CopiedContexts == null && _item.File.ExtensionDictionaryOf(_item) == null)
                || Annotative != DxfAnnotativeStates.True
            )
            {
                return true;
            }

            ShownContexts contexts = ContextsOfType(type, other);

            if (contexts == null)
            {
                return true;
            }

            if (!HasContext(at))
            {
                return allVisible;
            }

            factor = (hatch ? contexts.DefaultRatio : contexts.ShownRatio) / at.Scale;

            if (!Guard.IsFinite(factor) || factor <= 0.0)
            {
                factor = 1.0;
            }

            return true;
        }

        /// <summary>
        /// How an INSERT of an annotative block that holds contexts is drawn at a scale, the presenter's reading of AutoCAD's:
        /// false where it is not shown at the scale and <paramref name="allVisible"/> is false; otherwise true, with
        /// <paramref name="factor"/> the number its record's factors, those of its default context's scale, are multiplied by to
        /// be those at the scale (the default's scale over the scale's: 2 for an INSERT at 1:50 drawn at 1:100). 1 for one that
        /// is not annotative or holds no context.
        /// </summary>
        internal bool TryInsertFactorAt(DxfAnnotationScale at, bool allVisible, out double factor)
        {
            factor = 1.0;

            if (
                !(this is DxfBlockReference)
                || at == null
                || (_item.CopiedContexts == null && _item.File.ExtensionDictionaryOf(_item) == null)
                || Annotative != DxfAnnotativeStates.True
            )
            {
                return true;
            }

            List<ScaleContext> held = ScaleContexts();

            if (held.Count == 0)
            {
                return true;
            }

            if (!HasContext(at))
            {
                return allVisible;
            }

            ScaleContext first = held.Find(c => c.IsDefault);
            string scale = string.IsNullOrEmpty(first.Scale) ? held[0].Scale : first.Scale;
            DxfFile own = _item.CopiedContexts != null ? _item.File.Origin : _item.File;
            factor = ScaleRatioOf(own, scale) / at.Scale;

            if (!Guard.IsFinite(factor) || factor <= 0.0)
            {
                factor = 1.0;
            }

            return true;
        }

        /// <summary>
        /// Multiplies the lines of a hatch's pattern, its record's 43, 44, 45, 46 and 49, by a factor, as the presenter draws
        /// an annotative hatch at another scale on a copy.
        /// </summary>
        internal static void ScaleHatchLines(DxfItem hatch, double factor)
        {
            IReadOnlyList<DxfTypedValue> record = hatch.File.GroupsOf(hatch);
            bool lines = false;

            for (int i = 0; i < record.Count; i++)
            {
                int code = record[i].TypeCode;

                if (code == 78)
                {
                    lines = true;
                }
                else if (code == 98 || code == 47 || code >= 1000 || code == 450 || code == 100)
                {
                    if (lines)
                    {
                        break;
                    }
                }
                else if (
                    lines
                    && (code == 43 || code == 44 || code == 45 || code == 46 || code == 49)
                    && Numbers.TryRead(record[i].Value, out double number)
                )
                {
                    hatch.File.SetValueAt(hatch, i, Numbers.Exact(number * factor + 0.0));
                }
            }
        }

        /// <summary>The context of a name under ACDB_ANNOTATIONSCALES of the object's extension dictionary; null where it has none.</summary>
        private DxfItem FindContext(string key)
        {
            foreach (KeyValuePair<string, DxfItem> entry in ContextEntries())
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }

            return null;
        }

        /// <summary>The units of the paper over those of the drawing of the SCALE a handle names; 1 where it names none.</summary>
        private static double ScaleRatioOf(DxfFile file, string handle) =>
            ScaleOf(file, handle, out _, out double ratio) ? ratio : 1.0;

        /// <summary>The name of a new entry of ACDB_ANNOTATIONSCALES: <c>*A</c> and one more than the highest number of such a name it holds.</summary>
        private static string NextContextKey(DxfDBDictionary scales)
        {
            int top = 0;

            foreach (DxfDBDictionaryEntry entry in scales)
            {
                string key = entry.Key;

                if (
                    key.Length > 2
                    && key.StartsWith("*A", StringComparison.Ordinal)
                    && int.TryParse(key.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int n)
                    && n > top
                )
                {
                    top = n;
                }
            }

            return "*A" + NextContextNumber(scales, top).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The dictionary ACDB_ANNOTATIONSCALES of the record's extension dictionary, the extension dictionary and
        /// AcDbContextDataManager made where it has none, as AutoCAD writes them.
        /// </summary>
        private static DxfDBDictionary EnsureContextDictionary(DxfItem owner)
        {
            DxfFile file = owner.File;
            string handle = file.ExtensionDictionaryOf(owner);
            DxfItem extension;

            if (handle == null)
            {
                extension = file.AddObject("DICTIONARY", owner.Handle, b =>
                {
                    b.Group(100, "AcDbDictionary");
                    b.Group(280, "1");
                    b.Group(281, "1");
                });
                file.SetExtensionDictionary(owner, extension.Handle);
            }
            else
            {
                extension = file.ObjectItem(handle);
            }

            var root = (DxfDBDictionary)OfObject(extension);
            DxfDBDictionary manager = ChildDictionary(root, ContextManagerName);

            if (manager == null)
            {
                manager = (DxfDBDictionary)OfObject(root.Add(ContextManagerName, "DICTIONARY", OwnedDictionary));
            }

            DxfDBDictionary scales = ChildDictionary(manager, DxfObjectContextManager.AnnotationScales);

            if (scales == null)
            {
                scales = (DxfDBDictionary)OfObject(
                    manager.Add(DxfObjectContextManager.AnnotationScales, "DICTIONARY", OwnedDictionary)
                );
            }

            return scales;
        }

        /// <summary>
        /// Writes a context of the scale made from a context the object holds, not the default, its numbers multiplied as
        /// <see cref="AddContext"/> says. For the attribute of an INSERT <paramref name="insert"/> is the point of the INSERT's
        /// context, which the attribute's moves out from.
        /// </summary>
        private static void AddDerived(
            DxfItem owner,
            DxfItem source,
            DxfAnnotationScale scale,
            double factor,
            GeoPoint3? insert
        )
        {
            DxfDBDictionary scales = EnsureContextDictionary(owner);
            IReadOnlyList<DxfTypedValue> groups = source.File.ObjectGroupsOf(source);
            string type = source.Type;
            owner.File.AddClass(type, ClassOf(type), 1153);

            scales.Add(NextContextKey(scales), type, b =>
            {
                string marker = null;
                bool flagged = false;
                bool scaled = false;

                foreach (DxfTypedValue group in groups)
                {
                    int code = group.TypeCode;
                    string value = group.Value;

                    if (code == 100)
                    {
                        marker = value.Trim();
                    }
                    else if (code == 290 && marker == ContextDataMarker && !flagged)
                    {
                        flagged = true;
                        value = "0";
                    }
                    else if (code == 340 && marker == ScaleContextMarker && !scaled)
                    {
                        scaled = true;
                        value = scale.Handle;
                    }
                    else if (marker == HatchContextMarker && code == 40)
                    {
                        value = Numbers.Exact(1.0 / scale.Scale);
                    }
                    else if (insert != null && marker == ScaleContextMarker && (code == 10 || code == 20))
                    {
                        // An attribute's point moves out from its INSERT's.
                        double from = code == 10 ? insert.Value.X : insert.Value.Y;

                        if (Numbers.TryRead(value, out double at))
                        {
                            value = Numbers.Exact(from + (at - from) * factor + 0.0);
                        }
                    }
                    else if (ScaledByFactor(type, marker, code) && Numbers.TryRead(value, out double number))
                    {
                        value = Numbers.Exact(number * factor + 0.0);
                    }

                    b.Group(code, value);
                }
            });
        }

        /// <summary>Whether a number of a context is multiplied by the factor between two scales: the sizes of the kinds the library derives.</summary>
        private static bool ScaledByFactor(string type, string marker, int code)
        {
            if (marker == null || marker == ContextDataMarker)
            {
                return false;
            }

            switch (type)
            {
                case DxfMText.ContextType:
                    return code == 40 || code == 41 || code == 42 || code == 43;
                case HatchContextType:
                    return marker == HatchContextMarker
                        && (code == 43 || code == 44 || code == 45 || code == 46 || code == 49);
                case DxfBlockReference.ContextType:
                    return code == 41 || code == 42 || code == 43;
                default:
                    return false;
            }
        }

        /// <summary>The CLASSES entry's C++ name of a context's record type, as AutoCAD 2021 lists it.</summary>
        private static string ClassOf(string type)
        {
            switch (type)
            {
                case DxfDBText.TextContextType:
                    return "AcDbTextObjectContextData";
                case DxfDBText.AttributeContextType:
                    return "AcDbMTextAttributeObjectContextData";
                case DxfMText.ContextType:
                    return "AcDbMTextObjectContextData";
                case HatchContextType:
                    return "AcDbHatchScaleContextData";
                case MLeaderContextType:
                    return "AcDbMLeaderObjectContextData";
                case DxfBlockReference.ContextType:
                    return "AcDbBlkRefObjectContextData";
                default:
                    return type;
            }
        }

        /// <summary>
        /// The first context of an INSERT of an annotative block that holds none: the scale as the default, the point and the
        /// angle of the record and the factors given, as AutoCAD 2021 wrote it.
        /// </summary>
        private protected static void WriteFirstReferenceContext(
            DxfItem insert,
            DxfAnnotationScale scale,
            GeoVector3 factors
        )
        {
            DxfFile file = insert.File;
            DxfDBDictionary scales = EnsureContextDictionary(insert);
            file.AddClass(DxfBlockReference.ContextType, "AcDbBlkRefObjectContextData", 1153);
            var groups = new List<KeyValuePair<int, string>>
            {
                Pair(50, RecordNumber(insert, 50, 0.0)),
                Pair(10, RecordNumber(insert, 10, 0.0)),
                Pair(20, RecordNumber(insert, 20, 0.0)),
                Pair(30, RecordNumber(insert, 30, 0.0)),
                Pair(41, factors.X),
                Pair(42, factors.Y),
                Pair(43, factors.Z),
            };
            WriteContext(scales, DxfBlockReference.ContextType, scale, true, groups);
        }

        /// <summary>
        /// Gives each attribute of an INSERT that holds contexts a context of the scale, from its default, its point moved out
        /// from the INSERT's point by the factor; one that is shown at the scale already is left.
        /// </summary>
        private protected static void AddToAttributes(
            DxfItem insert,
            DxfAnnotationScale scale,
            double factor,
            GeoPoint3 at
        )
        {
            DxfFile file = insert.File;
            int count = file.RecordCount(insert);

            for (int r = 1; r < count; r++)
            {
                if (file.RecordType(insert, r) != "ATTRIB")
                {
                    continue;
                }

                DxfItem attribute = file.FollowerOf(insert, r);
                List<KeyValuePair<string, DxfItem>> entries = ContextEntries(attribute);
                DxfItem source = null;
                bool shown = false;

                foreach (KeyValuePair<string, DxfItem> entry in entries)
                {
                    IReadOnlyList<DxfTypedValue> groups = entry.Value.File.ObjectGroupsOf(entry.Value);
                    shown |= string.Equals(ScaleHandleOf(groups), scale.Handle, StringComparison.OrdinalIgnoreCase);

                    if (source == null || IsDefaultContext(groups))
                    {
                        source = entry.Value;
                    }
                }

                if (source != null && !shown)
                {
                    double own = ScaleRatioOf(file, ScaleHandleOf(source.File.ObjectGroupsOf(source)));
                    AddDerived(attribute, source, scale, own / scale.Scale, at);
                }
            }
        }

        /// <summary>Whether a context's groups say it is the default (the 290 of 1 of AcDbObjectContextData).</summary>
        private static bool IsDefaultContext(IReadOnlyList<DxfTypedValue> groups)
        {
            string marker = null;

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 100)
                {
                    marker = group.Value.Trim();
                }
                else if (group.TypeCode == 290 && marker == ContextDataMarker)
                {
                    return group.Value.Trim() == "1";
                }
            }

            return false;
        }

        /// <summary>The number of a group of an object's record; the fallback where it holds none.</summary>
        private protected static double RecordNumber(DxfItem item, int code, double fallback)
        {
            string text = item.File.ValueOf(item, code);
            return text != null && Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : fallback;
        }
    }
}
