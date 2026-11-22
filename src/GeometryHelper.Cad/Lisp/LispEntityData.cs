using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The groups of an entity as AutoLISP's <c>entget</c> gives them and <c>entmod</c> and <c>entmake</c> take them: the
    /// text of a DXF group read as the value its code says (a string, a whole number, a real, a point as a list of its
    /// coordinates, an entity name for a pointer), in the order AutoCAD's lists have, with the groups AutoCAD's list holds
    /// where the presenter's record gives none (the extrusion 210, the defaults of a text, the widths of a polyline's
    /// vertices), and the way back to the text of a record. A group is a code and a value: a <see cref="string"/>, an
    /// <see cref="int"/>, a <see cref="double"/>, a <c>double[]</c> of two or three coordinates or a <see cref="LispEntityName"/>.
    /// </summary>
    internal static class LispEntityData
    {
        private static readonly double[] Up = { 0.0, 0.0, 1.0 };
        private static readonly double[] Zero = { 0.0, 0.0, 0.0 };

        /// <summary>One group of a list: the code and its value.</summary>
        internal static KeyValuePair<int, object> Pair(int code, object value) =>
            new KeyValuePair<int, object>(code, value);

        #region What a code holds

        private enum Kind
        {
            Text,
            Whole,
            Real,
            Pointer,
        }

        private static Kind KindOf(int code)
        {
            if (code <= 9)
            {
                return Kind.Text;
            }

            if (code <= 59)
            {
                return Kind.Real;
            }

            if (code <= 99)
            {
                return Kind.Whole;
            }

            if (code <= 109)
            {
                return Kind.Text;
            }

            if (code <= 159)
            {
                return Kind.Real;
            }

            if (code <= 179)
            {
                return Kind.Whole;
            }

            if (code >= 210 && code <= 239)
            {
                return Kind.Real;
            }

            if (code >= 270 && code <= 299)
            {
                return Kind.Whole;
            }

            if (code >= 300 && code <= 319)
            {
                return Kind.Text;
            }

            if (code >= 320 && code <= 369)
            {
                return Kind.Pointer;
            }

            if (code >= 370 && code <= 389)
            {
                return Kind.Whole;
            }

            if (code >= 390 && code <= 399)
            {
                return Kind.Pointer;
            }

            if (code >= 400 && code <= 409)
            {
                return Kind.Whole;
            }

            if (code >= 410 && code <= 419)
            {
                return Kind.Text;
            }

            if (code >= 420 && code <= 429)
            {
                return Kind.Whole;
            }

            if (code >= 430 && code <= 439)
            {
                return Kind.Text;
            }

            if (code >= 440 && code <= 459)
            {
                return Kind.Whole;
            }

            if (code >= 460 && code <= 469)
            {
                return Kind.Real;
            }

            if (code >= 470 && code <= 479)
            {
                return Kind.Text;
            }

            if (code == 480 || code == 481)
            {
                return Kind.Pointer;
            }

            if (code >= 1010 && code <= 1059)
            {
                return Kind.Real;
            }

            return code >= 1060 && code <= 1071 ? Kind.Whole : Kind.Text;
        }

        /// <summary>Whether a code is the first coordinate of a point: 10 to 18, 110 to 112, 210 and 1010 to 1013.</summary>
        internal static bool IsPointStart(int code) =>
            (code >= 10 && code <= 18) || (code >= 110 && code <= 112) || code == 210 || (code >= 1010 && code <= 1013);

        /// <summary>
        /// Whether the angles of a type, its groups 50 to 58, are written in degrees by the file and given in radians by
        /// <c>entget</c>: ARC, TEXT, ATTRIB, ATTDEF, INSERT, DIMENSION and HATCH (its pattern angle 52, the angle of each pattern
        /// line 53 and the angles 50 and 51 of the arcs of its boundary; AutoCAD 2021, console, l2p pc). An MTEXT's rotation is in
        /// radians in the file too.
        /// </summary>
        internal static bool HasAngles(string type)
        {
            switch (type)
            {
                case "ARC":
                case "HATCH":
                case "TEXT":
                case "ATTRIB":
                case "ATTDEF":
                case "INSERT":
                case "DIMENSION":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAngle(int code) => code >= 50 && code <= 58;

        #endregion

        #region From the file

        /// <summary>
        /// Reads the groups of a record, from the one after its group 0 on, until the extended data or another record starts, as
        /// pairs of a code and a value; the three groups of a point are one pair.
        /// </summary>
        /// <param name="groups">The groups as the file holds them.</param>
        /// <param name="type">The type of the record, which says whether its angles are given in radians.</param>
        /// <returns>The pairs.</returns>
        internal static List<KeyValuePair<int, object>> Read(IReadOnlyList<DxfTypedValue> groups, string type)
        {
            bool radians = HasAngles(type);
            var pairs = new List<KeyValuePair<int, object>>(groups.Count);

            for (int i = 0; i < groups.Count; i++)
            {
                int code = groups[i].TypeCode;
                string text = groups[i].Value;

                if (code == 0 || code == 1001)
                {
                    break;
                }

                if (code == 999)
                {
                    continue;
                }

                if (IsPointStart(code) && i + 1 < groups.Count && groups[i + 1].TypeCode == code + 10)
                {
                    bool three = i + 2 < groups.Count && groups[i + 2].TypeCode == code + 20;
                    double x = Real(text);
                    double y = Real(groups[i + 1].Value);

                    // The points of a HATCH's edges and seeds are three-dimensional in AutoCAD's list, the file holds two coordinates.
                    bool padded = !three && type == "HATCH" && (code == 10 || code == 11);
                    pairs.Add(
                        Pair(
                            code,
                            three ? new[] { x, y, Real(groups[i + 2].Value) }
                                : padded ? new[] { x, y, 0.0 }
                                : new[] { x, y }
                        )
                    );
                    i += three ? 2 : 1;
                    continue;
                }

                pairs.Add(Pair(code, ValueOf(code, text, radians && IsAngle(code))));
            }

            return pairs;
        }

        /// <summary>The extended data of a record: the application names and, for each, its groups, as the file holds them from group 1001 on.</summary>
        internal static List<KeyValuePair<string, List<KeyValuePair<int, object>>>> ReadExtended(
            IReadOnlyList<DxfTypedValue> groups
        )
        {
            var applications = new List<KeyValuePair<string, List<KeyValuePair<int, object>>>>();
            int start = 0;

            while (start < groups.Count && groups[start].TypeCode != 1001)
            {
                start++;
            }

            for (int i = start; i < groups.Count; i++)
            {
                int code = groups[i].TypeCode;

                if (code == 0)
                {
                    break;
                }

                if (code == 1001)
                {
                    applications.Add(
                        new KeyValuePair<string, List<KeyValuePair<int, object>>>(
                            groups[i].Value.Trim(),
                            new List<KeyValuePair<int, object>>()
                        )
                    );
                    continue;
                }

                List<KeyValuePair<int, object>> current = applications[applications.Count - 1].Value;

                if (IsPointStart(code) && i + 2 < groups.Count && groups[i + 1].TypeCode == code + 10)
                {
                    current.Add(
                        Pair(
                            code,
                            new[] { Real(groups[i].Value), Real(groups[i + 1].Value), Real(groups[i + 2].Value) }
                        )
                    );
                    i += 2;
                    continue;
                }

                current.Add(Pair(code, ValueOf(code, groups[i].Value, false)));
            }

            return applications;
        }

        /// <summary>The value of a group of a given code read from the text a file prints for it.</summary>
        internal static object ValueOf(int code, string text, bool radians)
        {
            switch (KindOf(code))
            {
                case Kind.Whole:
                    return int.TryParse(
                        text,
                        NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out int whole
                    )
                        ? whole
                        : (object)Real(text);
                case Kind.Real:
                    double real = Real(text);
                    return radians ? real * Math.PI / 180.0 : real;
                case Kind.Pointer:
                    return new LispEntityName(text.Trim());
                default:
                    return text;
            }
        }

        private static double Real(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;

        #endregion

        #region AutoCAD's order and defaults

        /// <summary>One place of AutoCAD's list: a group with the value it has where the record gives none (null for none), or a subclass marker.</summary>
        private sealed class Slot
        {
            internal Slot(int code, object value, bool many = false)
            {
                Code = code;
                Value = value;
                Many = many;
            }

            internal int Code { get; }

            internal object Value { get; }

            /// <summary>Whether the slot takes every group of its code the record has, one after another (the chunks of an MTEXT's text).</summary>
            internal bool Many { get; }
        }

        private static Slot Mark(string name) => new Slot(100, name);

        private static Slot Optional(int code) => new Slot(code, null);

        private static Slot Has(int code, object value) => new Slot(code, value);

        private static Slot Each(int code) => new Slot(code, null, true);

        // The order of the groups of each type in AutoCAD 2021's lists and what a group is where the record gives none.
        // LINE, CIRCLE, TEXT, LWPOLYLINE and DIMENSION are as measured (q5, q8); ARC, POINT and INSERT follow AutoCAD's
        // documentation ("reference"): their defaults are not measured.
        private static readonly Slot[] LineSlots =
        {
            Mark("AcDbLine"),
            Optional(39),
            Optional(10),
            Optional(11),
            Has(210, Up),
        };

        private static readonly Slot[] CircleSlots =
        {
            Mark("AcDbCircle"),
            Optional(39),
            Optional(10),
            Optional(40),
            Has(210, Up),
        };

        private static readonly Slot[] ArcSlots =
        {
            Mark("AcDbCircle"),
            Optional(39),
            Optional(10),
            Optional(40),
            Has(210, Up),
            Mark("AcDbArc"),
            Optional(50),
            Optional(51),
        };

        private static readonly Slot[] PointSlots =
        {
            Mark("AcDbPoint"),
            Optional(39),
            Optional(10),
            Has(210, Up),
            Has(50, 0.0),
        };

        private static readonly Slot[] TextSlots =
        {
            Mark("AcDbText"),
            Optional(39),
            Optional(10),
            Optional(40),
            Optional(1),
            Has(50, 0.0),
            Has(41, 1.0),
            Has(51, 0.0),
            Has(7, "Standard"),
            Has(71, 0),
            Has(72, 0),
            Has(11, Zero),
            Has(210, Up),
            Mark("AcDbText"),
            Has(73, 0),
        };

        private static readonly Slot[] InsertSlots =
        {
            Mark("AcDbBlockReference"),
            Optional(66),
            Optional(2),
            Optional(10),
            Has(41, 1.0),
            Has(42, 1.0),
            Has(43, 1.0),
            Has(50, 0.0),
            Has(70, 0),
            Has(71, 0),
            Has(44, 0.0),
            Has(45, 0.0),
            Has(210, Up),
        };

        private static readonly Slot[] DimensionSlots =
        {
            Mark("AcDbDimension"),
            Has(280, 0),
            Optional(2),
            Optional(10),
            Optional(11),
            Has(12, Zero),
            Optional(70),
            Has(1, string.Empty),
            Has(71, 5),
            Has(72, 1),
            Has(41, 1.0),
            Optional(42),
            Has(73, 0),
            Has(74, 0),
            Has(75, 0),
            Has(52, 0.0),
            Has(53, 0.0),
            Has(54, 0.0),
            Has(51, 0.0),
            Has(210, Up),
            Has(3, "Standard"),
        };

        private static readonly Slot[] AlignedSlots =
        {
            Mark("AcDbAlignedDimension"),
            Optional(13),
            Optional(14),
            Has(15, Zero),
            Has(16, Zero),
            Has(40, 0.0),
            Has(50, 0.0),
            Mark("AcDbRotatedDimension"),
        };

        // The slots <c>entget</c> gives these types, as AutoCAD 2021 listed them (console, l2p pa and pd). Only the listing uses them:
        // entmake and entmod keep the slots of SlotsOf as they were. An ATTRIB's and an ATTDEF's two groups 280 (the version after
        // the marker, the lock of the position last) are put in place by Attribute, not by a slot.
        private static readonly Slot[] AttributeTextSlots =
        {
            Mark("AcDbText"),
            Optional(39),
            Optional(10),
            Optional(40),
            Optional(1),
            Has(50, 0.0),
            Has(41, 1.0),
            Has(51, 0.0),
            Has(7, "Standard"),
            Has(71, 0),
            Has(72, 0),
            Has(11, Zero),
            Has(210, Up),
        };

        private static readonly Slot[] AttribTailSlots =
        {
            Mark("AcDbAttribute"),
            Optional(2),
            Optional(70),
            Has(73, 0),
            Has(74, 0),
        };

        private static readonly Slot[] AttdefTailSlots =
        {
            Mark("AcDbAttributeDefinition"),
            Optional(3),
            Optional(2),
            Optional(70),
            Has(73, 0),
            Has(74, 0),
        };

        private static readonly Slot[] MTextSlots =
        {
            Mark("AcDbMText"),
            Optional(10),
            Optional(40),
            Has(41, 0.0),
            Has(46, 0.0),
            Optional(71),
            Has(72, 5),
            Each(3),
            Optional(1),
            Has(7, "Standard"),
            Has(210, Up),
            Has(11, new[] { 1.0, 0.0, 0.0 }),
            Optional(42),
            Optional(43),
            Has(50, 0.0),
            Has(73, 1),
            Has(44, 1.0),
        };

        private static Slot[] PolylineSlots(string marker) =>
            new[]
            {
                Mark(marker),
                Has(66, 1),
                Has(10, Zero),
                Optional(39),
                Has(70, 0),
                Has(40, 0.0),
                Has(41, 0.0),
                Has(210, Up),
                Has(71, 0),
                Has(72, 0),
                Has(73, 0),
                Has(74, 0),
                Has(75, 0),
            };

        private static readonly Slot[] Vertex2dSlots =
        {
            Mark("AcDbVertex"),
            Mark("AcDb2dVertex"),
            Optional(10),
            Has(40, 0.0),
            Has(41, 0.0),
            Has(42, 0.0),
            Has(91, 0),
            Has(70, 0),
            Has(50, 0.0),
        };

        private static readonly Slot[] Vertex3dSlots =
        {
            Mark("AcDbVertex"),
            Mark("AcDb3dPolylineVertex"),
            Optional(10),
            Has(40, 0.0),
            Has(41, 0.0),
            Has(42, 0.0),
            Has(70, 32),
            Has(50, 0.0),
            Has(71, 0),
            Has(72, 0),
            Has(73, 0),
            Has(74, 0),
        };

        /// <summary>The flags (group 70) a record gives, 0 where it gives none.</summary>
        private static int FlagsOf(IReadOnlyList<KeyValuePair<int, object>> body)
        {
            foreach (KeyValuePair<int, object> pair in body)
            {
                if (pair.Key == 70 && pair.Value is int flags)
                {
                    return flags;
                }
            }

            return 0;
        }

        /// <summary>
        /// The slots of a type for the listing of <c>entget</c>: an MTEXT, an ATTRIB, an ATTDEF, a POLYLINE and a VERTEX, which the
        /// other uses of <see cref="SlotsOf"/> leave as they were; the types of <see cref="SlotsOf"/> are the same.
        /// </summary>
        private static List<Slot> ListingSlotsOf(string type, IReadOnlyList<KeyValuePair<int, object>> body)
        {
            var slots = new List<Slot>();

            switch (type)
            {
                case "ATTRIB":
                    slots.AddRange(AttributeTextSlots);
                    slots.AddRange(AttribTailSlots);
                    return slots;
                case "ATTDEF":
                    slots.AddRange(AttributeTextSlots);
                    slots.AddRange(AttdefTailSlots);
                    return slots;
                case "MTEXT":
                    slots.AddRange(MTextSlots);
                    return slots;
                case "POLYLINE":
                    int flags = FlagsOf(body);
                    slots.AddRange(
                        PolylineSlots(
                            (flags & 8) != 0 ? "AcDb3dPolyline"
                            : (flags & 16) != 0 ? "AcDbPolygonMesh"
                            : (flags & 64) != 0 ? "AcDbPolyFaceMesh"
                            : "AcDb2dPolyline"
                        )
                    );
                    return slots;
                case "VERTEX":
                    int kind = FlagsOf(body);
                    if ((kind & 192) == 128)
                    {
                        slots.Add(Mark("AcDbFaceRecord"));
                    }
                    else if ((kind & 32) != 0)
                    {
                        slots.AddRange(Vertex3dSlots);
                    }
                    else if ((kind & 128) != 0)
                    {
                        slots.Add(Mark("AcDbVertex"));
                        slots.Add(Mark("AcDbPolyFaceMeshVertex"));
                    }
                    else if ((kind & 64) != 0)
                    {
                        slots.Add(Mark("AcDbVertex"));
                        slots.Add(Mark("AcDbPolygonMeshVertex"));
                    }
                    else
                    {
                        slots.AddRange(Vertex2dSlots);
                    }

                    return slots;
                default:
                    return SlotsOf(type, body);
            }
        }

        /// <summary>
        /// The groups of a record in AutoCAD's order: <c>(330 . owner)</c> and <c>(5 . handle)</c> first, then those the record has
        /// before its first subclass marker with <c>67</c> and <c>410</c> and the layer where it gives none, then the groups of
        /// its subclasses in the order of AutoCAD's list with the defaults it gives where the record has none.
        /// </summary>
        /// <param name="type">The type of the entity.</param>
        /// <param name="pairs">The groups as <see cref="Read"/> gave them; not changed.</param>
        /// <param name="owner">The handle of the owner, used where the record gives none.</param>
        /// <param name="layout">The name of the space the entity is in, <c>Model</c> or a layout's.</param>
        /// <param name="paper">Whether the space is paper space.</param>
        /// <param name="layer">The layer of the entity, used where the record gives none.</param>
        /// <returns>The groups in AutoCAD's order.</returns>
        internal static List<KeyValuePair<int, object>> Complete(
            string type,
            IReadOnlyList<KeyValuePair<int, object>> pairs,
            string owner,
            string layout,
            bool paper,
            string layer
        )
        {
            int bodyStart = BodyStart(pairs);
            var result = new List<KeyValuePair<int, object>>(pairs.Count + 8);
            object handle = null;
            object ownerName = null;
            var head = new List<KeyValuePair<int, object>>();
            int depth = 0;

            for (int i = 0; i < bodyStart; i++)
            {
                if (pairs[i].Key == 102)
                {
                    depth += IsOpening(pairs[i]) ? 1 : -1;
                }

                // The owner is the 330 outside the lists of reactors and of the extension dictionary.
                if (pairs[i].Key == 5 && depth == 0)
                {
                    handle = pairs[i].Value;
                }
                else if (pairs[i].Key == 330 && ownerName == null && depth == 0)
                {
                    ownerName = pairs[i].Value;
                }
                else
                {
                    head.Add(pairs[i]);
                }
            }

            KeyValuePair<int, object> ownerPair = Pair(330, ownerName ?? new LispEntityName(owner ?? "0"));
            int lists = 0;

            while (lists < head.Count && head[lists].Key == 102 && IsOpening(head[lists]))
            {
                while (lists < head.Count && !(head[lists].Key == 102 && !IsOpening(head[lists])))
                {
                    lists++;
                }

                lists++;
            }

            lists = Math.Min(lists, head.Count);

            // An entity with an extension dictionary lists its handle, the groups of its lists, then the owner (AutoCAD 2021,
            // console, C4b probe l2); one without lists the owner and the handle.
            if (handle != null && head.GetRange(0, lists).Exists(p => p.Key == 360))
            {
                result.Add(Pair(5, handle));
                result.AddRange(head.GetRange(0, lists));
                head.RemoveRange(0, lists);
                result.Add(ownerPair);
            }
            else
            {
                result.Add(ownerPair);

                if (handle != null)
                {
                    result.Add(Pair(5, handle));
                }
            }

            int marker = head.FindIndex(p => p.Key == 100 && Same(p.Value, "AcDbEntity"));
            int put = marker < 0 ? 0 : marker + 1;

            if (marker < 0)
            {
                head.Insert(0, Pair(100, "AcDbEntity"));
                put = 1;
            }

            if (!head.Exists(p => p.Key == 67))
            {
                head.Insert(put++, Pair(67, paper ? 1 : 0));
            }
            else
            {
                put = head.FindIndex(p => p.Key == 67) + 1;
            }

            if (layout == null)
            {
                // An entity of a block definition has no 410 (AutoCAD 2021, console, l2p pa).
                put = head.FindIndex(p => p.Key == 67) + 1;
            }
            else if (!head.Exists(p => p.Key == 410))
            {
                head.Insert(put++, Pair(410, layout));
            }
            else
            {
                put = head.FindIndex(p => p.Key == 410) + 1;
            }

            if (!head.Exists(p => p.Key == 8))
            {
                head.Insert(put, Pair(8, layer ?? "0"));
            }

            // AutoCAD lists the colour before the linetype where an entity has both (console, l2p2 pf and ph), the file the other way.
            int linetypeAt = head.FindIndex(p => p.Key == 6);
            int colourAt = head.FindIndex(p => p.Key == 62);

            if (linetypeAt >= 0 && colourAt > linetypeAt)
            {
                KeyValuePair<int, object> colour = head[colourAt];
                head.RemoveAt(colourAt);
                head.Insert(linetypeAt, colour);
            }

            result.AddRange(head);

            var body = new List<KeyValuePair<int, object>>();

            for (int i = bodyStart; i < pairs.Count; i++)
            {
                body.Add(pairs[i]);
            }

            result.AddRange(type == "LWPOLYLINE" ? Polyline(body) : Merge(type, body, true, true));
            return result;
        }

        /// <summary>Where the body of a record starts: after the groups before its first subclass marker that is not AcDbEntity's, the lists of reactors (102) among them.</summary>
        private static int BodyStart(IReadOnlyList<KeyValuePair<int, object>> pairs)
        {
            int at = 0;

            while (at < pairs.Count)
            {
                KeyValuePair<int, object> pair = pairs[at];

                if (pair.Key == 100 && !Same(pair.Value, "AcDbEntity"))
                {
                    break;
                }

                if (pair.Key == 102 && IsOpening(pair))
                {
                    do
                    {
                        at++;
                    } while (at < pairs.Count && !(pairs[at].Key == 102 && !IsOpening(pairs[at])));

                    at++;
                    continue;
                }

                if (!InHead(pair.Key))
                {
                    break;
                }

                at++;
            }

            return Math.Min(at, pairs.Count);
        }

        private static bool IsOpening(KeyValuePair<int, object> pair) =>
            pair.Value is string text && text.StartsWith("{", StringComparison.Ordinal);

        private static bool InHead(int code)
        {
            switch (code)
            {
                case 5:
                case 6:
                case 8:
                case 48:
                case 60:
                case 62:
                case 67:
                case 284:
                case 330:
                case 347:
                case 370:
                case 410:
                case 420:
                case 430:
                case 440:
                case 100:
                    return true;
                default:
                    return false;
            }
        }

        private static bool Same(object value, string text) =>
            value is string s && string.Equals(s, text, StringComparison.Ordinal);

        private static readonly Slot[] BareSlots = new Slot[0];

        /// <summary>The slots of a type, in AutoCAD's order; one subclass marker for the types whose defaults are not known; none for the rest.</summary>
        private static List<Slot> SlotsOf(string type, IReadOnlyList<KeyValuePair<int, object>> body)
        {
            var slots = new List<Slot>();

            switch (type)
            {
                case "LINE":
                    slots.AddRange(LineSlots);
                    break;
                case "CIRCLE":
                    slots.AddRange(CircleSlots);
                    break;
                case "ARC":
                    slots.AddRange(ArcSlots);
                    break;
                case "POINT":
                    slots.AddRange(PointSlots);
                    break;
                case "TEXT":
                    slots.AddRange(TextSlots);
                    break;
                case "INSERT":
                    slots.AddRange(InsertSlots);
                    break;
                case "DIMENSION":
                    slots.AddRange(DimensionSlots);

                    if (HasMarker(body, "AcDbAlignedDimension"))
                    {
                        slots.AddRange(AlignedSlots);
                    }

                    break;
                case "SOLID":
                case "TRACE":
                    slots.Add(Mark("AcDbTrace"));
                    break;
                case "3DFACE":
                    slots.Add(Mark("AcDbFace"));
                    break;
                case "RAY":
                    slots.Add(Mark("AcDbRay"));
                    break;
                case "XLINE":
                    slots.Add(Mark("AcDbXline"));
                    break;
                case "SPLINE":
                    slots.Add(Mark("AcDbSpline"));
                    break;
                case "MTEXT":
                    slots.Add(Mark("AcDbMText"));
                    break;
                case "ELLIPSE":
                    slots.Add(Mark("AcDbEllipse"));
                    break;
                default:
                    slots.AddRange(BareSlots);
                    break;
            }

            return slots;
        }

        private static bool HasMarker(IReadOnlyList<KeyValuePair<int, object>> body, string name)
        {
            foreach (KeyValuePair<int, object> pair in body)
            {
                if (pair.Key == 100 && Same(pair.Value, name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The body of a record walked through the slots of its type: what the record holds in the slots' order, the slots it lacks
        /// filled with their defaults where <paramref name="defaults"/> says (the subclass markers always), then the groups no slot
        /// names in the record's order.
        /// </summary>
        private static List<KeyValuePair<int, object>> Merge(
            string type,
            List<KeyValuePair<int, object>> body,
            bool defaults = true,
            bool listing = false
        )
        {
            List<Slot> slots = listing ? ListingSlotsOf(type, body) : SlotsOf(type, body);

            if (slots.Count == 0)
            {
                return body;
            }

            bool attribute = listing && (type == "ATTRIB" || type == "ATTDEF");
            object version = 0;
            object locked = 0;
            var rest = new List<KeyValuePair<int, object>>(body);

            if (attribute)
            {
                // The 280 before the tag is the version of the attribute, the one after it the lock of its position.
                int tag = rest.FindIndex(p => p.Key == 2);

                for (int i = rest.Count - 1; i >= 0; i--)
                {
                    if (rest[i].Key == 280)
                    {
                        if (tag >= 0 && i > tag)
                        {
                            locked = rest[i].Value;
                        }
                        else
                        {
                            version = rest[i].Value;
                        }

                        rest.RemoveAt(i);
                    }
                }
            }

            var merged = new List<KeyValuePair<int, object>>(body.Count + 8);

            foreach (Slot slot in slots)
            {
                int at = Find(rest, slot);

                if (at >= 0)
                {
                    merged.Add(rest[at]);
                    rest.RemoveAt(at);

                    while (slot.Many && (at = Find(rest, slot)) >= 0)
                    {
                        merged.Add(rest[at]);
                        rest.RemoveAt(at);
                    }
                }
                else if (slot.Value != null && (defaults || slot.Code == 100))
                {
                    merged.Add(Pair(slot.Code, slot.Value));
                }
            }

            if (attribute)
            {
                int marker = merged.FindIndex(p =>
                    p.Key == 100 && Same(p.Value, (string)slots[AttributeTextSlots.Length].Value)
                );
                merged.Insert(marker + 1, Pair(280, version));
                merged.Insert(merged.FindIndex(marker, p => p.Key == 74) + 1, Pair(280, locked));
            }

            merged.AddRange(rest);
            return merged;
        }

        private static int Find(List<KeyValuePair<int, object>> pairs, Slot slot) =>
            slot.Code == 100
                ? pairs.FindIndex(p => p.Key == 100 && Same(p.Value, (string)slot.Value))
                : pairs.FindIndex(p => p.Key == slot.Code);

        /// <summary>
        /// The groups of a list for <c>entmake</c>: the subclass markers of its type put in their places, no default filled in.
        /// An LWPOLYLINE is given the count of its vertices where the list has none.
        /// </summary>
        internal static List<KeyValuePair<int, object>> Marked(string type, List<KeyValuePair<int, object>> groups)
        {
            if (type == "LWPOLYLINE")
            {
                var result = new List<KeyValuePair<int, object>> { Pair(100, "AcDbPolyline") };

                if (!groups.Exists(p => p.Key == 90))
                {
                    result.Add(Pair(90, groups.FindAll(p => p.Key == 10).Count));
                }

                result.AddRange(groups);
                return result;
            }

            return Merge(type, groups, false);
        }

        /// <summary>
        /// Puts the groups of a list into the groups of a record, as <c>entmod</c> does: a group replaces the first group of its
        /// code the record has, a group the record lacks is put where AutoCAD's list has it (a header group such as the colour
        /// after the layer, a group of the body before the first group the order of its type puts after it). A code the list gives
        /// twice has the last value, as AutoCAD takes it (<c>(append (entget e) '((62 . 5) (62 . 6)))</c> makes the colour 6),
        /// except a code the entity has several of (the vertices of a polyline, the knots of a spline). A colour 256 or a linetype
        /// <c>ByLayer</c> is no group: it takes the entity's own out (AutoCAD 2021, feed dcd). An LWPOLYLINE that is given
        /// vertices has all its vertices replaced by them, its count set to match.
        /// </summary>
        internal static void Apply(
            string type,
            List<KeyValuePair<int, object>> record,
            List<KeyValuePair<int, object>> changes
        )
        {
            changes = Settled(type, record, changes);
            var taken = new List<int>();

            if (type == "LWPOLYLINE" && changes.Exists(p => p.Key == 10))
            {
                int first = record.FindIndex(p => p.Key == 10);
                int where = first < 0 ? record.Count : first;
                record.RemoveAll(p => p.Key == 10 || p.Key == 40 || p.Key == 41 || p.Key == 42 || p.Key == 91);
                where = Math.Min(where, record.Count);
                var vertices = new List<KeyValuePair<int, object>>();
                int count = 0;

                foreach (KeyValuePair<int, object> pair in changes)
                {
                    if (pair.Key == 10 || pair.Key == 40 || pair.Key == 41 || pair.Key == 42 || pair.Key == 91)
                    {
                        vertices.Add(pair);
                        count += pair.Key == 10 ? 1 : 0;
                    }
                }

                record.InsertRange(where, vertices);
                changes = changes.FindAll(p =>
                    !(p.Key == 10 || p.Key == 40 || p.Key == 41 || p.Key == 42 || p.Key == 91 || p.Key == 90)
                );
                int counted = record.FindIndex(p => p.Key == 90);

                if (counted >= 0)
                {
                    record[counted] = Pair(90, count);
                }
                else
                {
                    record.Insert(BodyStart(record) + 1, Pair(90, count));
                }
            }

            foreach (KeyValuePair<int, object> change in changes)
            {
                int at = -1;

                for (int i = 0; i < record.Count && at < 0; i++)
                {
                    if (record[i].Key == change.Key && !taken.Contains(i))
                    {
                        at = i;
                    }
                }

                if (at >= 0)
                {
                    record[at] = change;
                    taken.Add(at);
                    continue;
                }

                int placed = Place(type, record, change);

                for (int i = 0; i < taken.Count; i++)
                {
                    if (taken[i] >= placed)
                    {
                        taken[i]++;
                    }
                }

                taken.Add(placed);
            }
        }

        /// <summary>
        /// The groups of a list <see cref="Apply"/> puts in: a code given twice only by its last group, unless the entity has
        /// several of that code (two or more in its record, or the chunks of an MTEXT, the points and knots of a SPLINE, the
        /// vertices of an LWPOLYLINE); a colour 256 or a linetype ByLayer not at all, the entity's own group of that code taken out
        /// of the record instead.
        /// </summary>
        private static List<KeyValuePair<int, object>> Settled(
            string type,
            List<KeyValuePair<int, object>> record,
            List<KeyValuePair<int, object>> changes
        )
        {
            var held = new Dictionary<int, int>();

            foreach (KeyValuePair<int, object> pair in record)
            {
                held[pair.Key] = held.TryGetValue(pair.Key, out int count) ? count + 1 : 1;
            }

            // From the end, so that the last group of a code is the one kept; the groups come back in their order.
            var kept = new List<KeyValuePair<int, object>>(changes.Count);
            var seen = new HashSet<int>();

            for (int i = changes.Count - 1; i >= 0; i--)
            {
                int code = changes[i].Key;

                if (!seen.Add(code) && !Repeats(type, code, held))
                {
                    continue;
                }

                kept.Add(changes[i]);
            }

            kept.Reverse();
            var result = new List<KeyValuePair<int, object>>(kept.Count);

            foreach (KeyValuePair<int, object> pair in kept)
            {
                bool byLayer =
                    (pair.Key == 62 && pair.Value is int color && color == 256)
                    || (
                        pair.Key == 6
                        && pair.Value is string linetype
                        && string.Equals(linetype, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    );

                if (byLayer)
                {
                    record.RemoveAll(p => p.Key == pair.Key);
                }
                else
                {
                    result.Add(pair);
                }
            }

            return result;
        }

        /// <summary>Whether an entity of a type has several groups of a code: its record gives two or more, or the type is one whose list repeats it.</summary>
        private static bool Repeats(string type, int code, Dictionary<int, int> held)
        {
            if (held.TryGetValue(code, out int count) && count > 1)
            {
                return true;
            }

            switch (type)
            {
                case "MTEXT":
                    return code == 3;
                case "SPLINE":
                    return code == 10 || code == 11 || code == 40 || code == 41;
                case "LWPOLYLINE":
                    return code == 10 || code == 40 || code == 41 || code == 42 || code == 91;
                default:
                    return false;
            }
        }

        /// <summary>Puts a group the record lacks where its type's order has it; gives where it went.</summary>
        private static int Place(string type, List<KeyValuePair<int, object>> record, KeyValuePair<int, object> pair)
        {
            int start = BodyStart(record);

            if (pair.Key != 100 && IsHeadCode(pair.Key))
            {
                record.Insert(start, pair);
                return start;
            }

            var body = record.GetRange(start, record.Count - start);
            List<Slot> slots = SlotsOf(type, body);
            int slotOf = slots.FindIndex(s => s.Code == pair.Key);

            if (slotOf < 0)
            {
                record.Add(pair);
                return record.Count - 1;
            }

            // Which slot each group of the body fills, the groups taken in the order of the slots as Merge takes them.
            var ranks = new int[body.Count];

            for (int i = 0; i < ranks.Length; i++)
            {
                ranks[i] = int.MaxValue;
            }

            var left = new List<int>();

            for (int i = 0; i < body.Count; i++)
            {
                left.Add(i);
            }

            for (int s = 0; s < slots.Count; s++)
            {
                for (int k = 0; k < left.Count; k++)
                {
                    KeyValuePair<int, object> candidate = body[left[k]];

                    if (
                        slots[s].Code == candidate.Key
                        && (slots[s].Code != 100 || Same(candidate.Value, (string)slots[s].Value))
                    )
                    {
                        ranks[left[k]] = s;
                        left.RemoveAt(k);
                        break;
                    }
                }
            }

            for (int i = 0; i < body.Count; i++)
            {
                if (ranks[i] != int.MaxValue && ranks[i] > slotOf)
                {
                    record.Insert(start + i, pair);
                    return start + i;
                }
            }

            record.Add(pair);
            return record.Count - 1;
        }

        internal static bool IsHeadCode(int code)
        {
            switch (code)
            {
                case 6:
                case 8:
                case 48:
                case 60:
                case 62:
                case 284:
                case 347:
                case 370:
                case 420:
                case 430:
                case 440:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// An LWPOLYLINE's body: the count, the flags, the constant width, elevation and thickness, then each vertex as its
        /// point and its start width, end width, bulge and identifier (0 where it has none), then the extrusion.
        /// </summary>
        private static List<KeyValuePair<int, object>> Polyline(List<KeyValuePair<int, object>> body)
        {
            var result = new List<KeyValuePair<int, object>>(body.Count * 2);
            var vertices = new List<List<KeyValuePair<int, object>>>();
            var rest = new List<KeyValuePair<int, object>>();
            object count = null;
            object flags = null;
            object constant = null;
            object elevation = null;
            object thickness = null;
            object extrusion = null;
            bool marker = false;
            List<KeyValuePair<int, object>> vertex = null;

            foreach (KeyValuePair<int, object> pair in body)
            {
                switch (pair.Key)
                {
                    case 100 when !marker && Same(pair.Value, "AcDbPolyline"):
                        marker = true;
                        break;
                    case 90 when count == null:
                        count = pair.Value;
                        break;
                    case 70 when flags == null:
                        flags = pair.Value;
                        break;
                    case 43 when constant == null:
                        constant = pair.Value;
                        break;
                    case 38 when elevation == null:
                        elevation = pair.Value;
                        break;
                    case 39 when thickness == null:
                        thickness = pair.Value;
                        break;
                    case 210:
                        extrusion = pair.Value;
                        break;
                    case 10:
                        vertex = new List<KeyValuePair<int, object>> { pair };
                        vertices.Add(vertex);
                        break;
                    case 40:
                    case 41:
                    case 42:
                    case 91:
                        if (vertex != null)
                        {
                            vertex.Add(pair);
                        }
                        else
                        {
                            rest.Add(pair);
                        }

                        break;
                    default:
                        rest.Add(pair);
                        break;
                }
            }

            result.Add(Pair(100, "AcDbPolyline"));
            result.Add(Pair(90, count ?? vertices.Count));
            result.Add(Pair(70, flags ?? 0));
            result.Add(Pair(43, constant ?? 0.0));
            result.Add(Pair(38, elevation ?? 0.0));
            result.Add(Pair(39, thickness ?? 0.0));

            foreach (List<KeyValuePair<int, object>> v in vertices)
            {
                result.Add(v[0]);

                foreach (int code in new[] { 40, 41, 42, 91 })
                {
                    int at = v.FindIndex(p => p.Key == code);
                    result.Add(at >= 0 ? v[at] : Pair(code, code == 91 ? (object)0 : 0.0));
                }
            }

            result.AddRange(rest);
            result.Add(Pair(210, extrusion ?? Up));
            return result;
        }

        #endregion

        #region To LISP

        /// <summary>The list <c>entget</c> gives: <c>(-1 . ename)</c>, <c>(0 . type)</c> and the groups, each a pair, a point's coordinates as the rest of its list.</summary>
        internal static object ToList(
            LispEntityName name,
            string type,
            IReadOnlyList<KeyValuePair<int, object>> pairs,
            IReadOnlyList<KeyValuePair<string, List<KeyValuePair<int, object>>>> extended = null
        )
        {
            var items = new List<object>(pairs.Count + 3) { new LispCons(-1, name), new LispCons(0, type) };

            foreach (KeyValuePair<int, object> pair in pairs)
            {
                items.Add(ToItem(pair));
            }

            if (extended != null && extended.Count > 0)
            {
                var applications = new List<object>();

                foreach (KeyValuePair<string, List<KeyValuePair<int, object>>> application in extended)
                {
                    var entries = new List<object> { application.Key };

                    foreach (KeyValuePair<int, object> pair in application.Value)
                    {
                        entries.Add(ToItem(pair));
                    }

                    applications.Add(LispInterpreter.ListOf(entries));
                }

                items.Add(new LispCons(-3, LispInterpreter.ListOf(applications)));
            }

            return LispInterpreter.ListOf(items);
        }

        /// <summary>One group as a list item: <c>(code . value)</c>, or <c>(code x y z)</c> for a point.</summary>
        internal static object ToItem(KeyValuePair<int, object> pair)
        {
            if (pair.Value is double[] point)
            {
                var coordinates = new List<object>(point.Length + 1) { pair.Key };

                foreach (double coordinate in point)
                {
                    coordinates.Add(coordinate);
                }

                return LispInterpreter.ListOf(coordinates);
            }

            return new LispCons(pair.Key, pair.Value);
        }

        #endregion

        #region From LISP

        /// <summary>
        /// Takes the groups of a list as <c>entmod</c> and <c>entmake</c> are given them, each <c>(code . value)</c> or
        /// <c>(code x y z)</c>; false for a list that is no list of groups, a code that is no whole number, or a value of the wrong
        /// kind for its code (a string for a real, a whole number for a text, a point of one coordinate). A whole number given for
        /// a real is taken as one. The extended data of group -3 is returned as it was given.
        /// </summary>
        internal static bool TryRead(object list, out List<KeyValuePair<int, object>> pairs)
        {
            pairs = new List<KeyValuePair<int, object>>();
            object cell = list;

            while (cell is LispCons item)
            {
                if (!(item.Car is LispCons group) || !(group.Car is int code))
                {
                    return false;
                }

                object value = group.Cdr;

                if (code == -3)
                {
                    pairs.Add(Pair(code, value));
                }
                else if (value is LispCons)
                {
                    if (!TryPoint(value, out double[] point))
                    {
                        return false;
                    }

                    pairs.Add(Pair(code, point));
                }
                else
                {
                    pairs.Add(Pair(code, value));
                }

                cell = item.Cdr;
            }

            return cell == null && pairs.Count > 0 && pairs.TrueForAll(p => Valid(p));
        }

        private static bool TryPoint(object list, out double[] point)
        {
            var values = new List<double>(3);
            object cell = list;
            point = null;

            while (cell is LispCons item)
            {
                if (item.Car is int whole)
                {
                    values.Add(whole);
                }
                else if (item.Car is double real)
                {
                    values.Add(real);
                }
                else
                {
                    return false;
                }

                cell = item.Cdr;
            }

            if (cell != null || values.Count < 2 || values.Count > 3)
            {
                return false;
            }

            point = values.ToArray();
            return true;
        }

        /// <summary>Whether the value of a group is of the kind its code holds.</summary>
        private static bool Valid(KeyValuePair<int, object> pair)
        {
            int code = pair.Key;
            object value = pair.Value;

            if (code < 0)
            {
                return true;
            }

            if (IsPointStart(code))
            {
                return value is double[];
            }

            switch (KindOf(code))
            {
                case Kind.Whole:
                    return value is int;
                case Kind.Real:
                    return value is double || value is int;
                case Kind.Pointer:
                    return value is LispEntityName || value is string;
                default:
                    return value is string;
            }
        }

        /// <summary>
        /// The groups of a list written as a record holds them: <paramref name="pairs"/> as codes and the text of their values, a
        /// point as its coordinates in groups of their own, an angle in degrees for a type that has them, an entity name as its handle.
        /// </summary>
        internal static List<KeyValuePair<int, string>> ToGroups(
            string type,
            IEnumerable<KeyValuePair<int, object>> pairs
        )
        {
            bool degrees = HasAngles(type);
            var groups = new List<KeyValuePair<int, string>>();
            bool elevated = false;

            foreach (KeyValuePair<int, object> pair in pairs)
            {
                int code = pair.Key;

                switch (pair.Value)
                {
                    case double[] point:
                        // A HATCH's points but its first, the elevation, go back to the two coordinates its file holds.
                        int length =
                            type == "HATCH" && (code == 11 || (code == 10 && elevated))
                                ? Math.Min(2, point.Length)
                                : point.Length;
                        elevated |= code == 10;

                        for (int i = 0; i < length; i++)
                        {
                            groups.Add(new KeyValuePair<int, string>(code + 10 * i, Number(point[i])));
                        }

                        break;
                    case double real:
                        groups.Add(
                            new KeyValuePair<int, string>(
                                code,
                                Number(degrees && IsAngle(code) ? real * 180.0 / Math.PI : real)
                            )
                        );
                        break;
                    case int whole:
                        groups.Add(
                            new KeyValuePair<int, string>(
                                code,
                                KindOf(code) == Kind.Real
                                    ? Number(degrees && IsAngle(code) ? whole * 180.0 / Math.PI : whole)
                                    : whole.ToString(CultureInfo.InvariantCulture)
                            )
                        );
                        break;
                    case LispEntityName name:
                        groups.Add(new KeyValuePair<int, string>(code, name.Handle));
                        break;
                    default:
                        groups.Add(new KeyValuePair<int, string>(code, (string)pair.Value));
                        break;
                }
            }

            return groups;
        }

        /// <summary>A real as the text a file holds, with all the digits it has.</summary>
        internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        #endregion
    }
}
