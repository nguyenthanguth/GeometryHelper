using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>entmake</c> of what is made by several calls (AutoCAD 2021, probe pa of l2p): a POLYLINE with its VERTEXes and
    /// SEQEND, a block reference with its ATTRIBs and SEQEND, and a BLOCK with its entities and ENDBLK. What has been given
    /// waits in the document until the end comes; nothing is in the drawing before it.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>A polyline or a block reference with attributes that has been begun: its head, and the vertices or attributes given since.</summary>
        private sealed class PendingComplex
        {
            internal PendingComplex(string type, string layer, List<KeyValuePair<int, object>> head)
            {
                Type = type;
                Layer = layer;
                Head = head;
            }

            internal string Type { get; }

            internal string Layer { get; }

            internal List<KeyValuePair<int, object>> Head { get; }

            internal List<List<KeyValuePair<int, object>>> Parts { get; } = new List<List<KeyValuePair<int, object>>>();
        }

        /// <summary>A block that has been begun: what its BLOCK gave, and the entities made since, to be put into it by ENDBLK.</summary>
        private sealed class PendingBlock
        {
            internal PendingBlock(string name, int? flags, double[] origin)
            {
                Name = name;
                Flags = flags;
                Origin = origin;
            }

            internal string Name { get; }

            internal int? Flags { get; }

            internal double[] Origin { get; }

            internal List<Placement> Entries { get; } = new List<Placement>();
        }

        /// <summary>What a document has been given by <c>entmake</c> and not finished: a complex entity, and a block (a complex entity may be inside the block).</summary>
        private sealed class Pending
        {
            internal PendingComplex Complex { get; set; }

            internal PendingBlock Block { get; set; }
        }

        private static readonly ConditionalWeakTable<CadDocument, Pending> Pendings =
            new ConditionalWeakTable<CadDocument, Pending>();

        /// <summary>What a document waits for, made once.</summary>
        private static Pending PendingOf(CadDocument document) => Pendings.GetValue(document, _ => new Pending());

        /// <summary>
        /// Takes the types <c>entmake</c> does not make in one call: the parts and ends of a complex entity, BLOCK and ENDBLK, the
        /// records of the tables, and any other entity while a complex one is pending, which AutoCAD refuses. False for a type the
        /// plain path makes.
        /// </summary>
        private static bool TryMakeSpecial(
            LispInterpreter lisp,
            CadCommandSession session,
            string type,
            List<KeyValuePair<int, object>> given,
            object list,
            out object result
        )
        {
            Pending pending = PendingOf(session.Document);
            result = null;

            switch (type)
            {
                case "VERTEX":
                case "ATTRIB":
                case "SEQEND":
                    result = Continue(lisp, session, pending, type, given, list);
                    return true;
                case "POLYLINE":
                    result =
                        pending.Complex != null
                            ? Refuse(lisp, pending)
                            : BeginComplex(session, pending, type, given, list);
                    return true;
                case "INSERT" when given.Exists(p => p.Key == 66 && p.Value is int attributes && attributes != 0):
                    result =
                        pending.Complex != null
                            ? Refuse(lisp, pending)
                            : BeginComplex(session, pending, type, given, list);
                    return true;
                case "BLOCK":
                    result = pending.Complex != null ? Refuse(lisp, pending) : BeginBlock(pending, given, list);
                    return true;
                case "ENDBLK":
                    result = pending.Complex != null ? Refuse(lisp, pending) : EndBlock(lisp, session, pending);
                    return true;
            }

            if (IsRecordType(type))
            {
                result = pending.Complex != null ? Refuse(lisp, pending) : MakeRecord(lisp, session, type, given, list);
                return true;
            }

            // Any other entity while a complex one is pending: refused with no line break after the words, as AutoCAD writes them.
            if (pending.Complex != null)
            {
                result = Refuse(lisp, pending);
                return true;
            }

            return false;
        }

        /// <summary>The refusal of an entity that comes while a complex one is pending: the words, the pending entity dropped, nil.</summary>
        private static object Refuse(LispInterpreter lisp, Pending pending)
        {
            lisp.Write("Invalid complex object.");
            pending.Complex = null;
            return null;
        }

        /// <summary>
        /// Begins a POLYLINE, or a block reference that says it has attributes (group 66 not 0): the head is kept, nothing is made. A
        /// polyline needs no group 66 and no point (it is at 0,0,0); a block reference needs its block and its point.
        /// </summary>
        private static object BeginComplex(
            CadCommandSession session,
            Pending pending,
            string type,
            List<KeyValuePair<int, object>> given,
            object list
        )
        {
            CadDocument document = session.Document;
            List<KeyValuePair<int, object>> groups = ChangesOf(given);
            CompletePoints(type, groups);
            TakeLinetypeAndColour(document.Database, groups);

            foreach (int required in RequiredOf(type))
            {
                if (!groups.Exists(p => p.Key == required))
                {
                    return null;
                }
            }

            if (!Fits(document, type, groups))
            {
                return null;
            }

            if (type == "INSERT" && !document.Database.BlockTable.Has((string)groups.Find(p => p.Key == 2).Value))
            {
                return null;
            }

            int layerAt = groups.FindIndex(p => p.Key == 8);
            string layer = layerAt >= 0 ? (string)groups[layerAt].Value : document.Database.Clayer;
            groups.RemoveAll(p => p.Key == 8);
            pending.Complex = new PendingComplex(type, layer, groups);
            return list;
        }

        /// <summary>
        /// A VERTEX or an ATTRIB (that must fit what is pending: a polyline takes vertices, a block reference attributes) or the
        /// SEQEND that ends them. Nil where nothing is pending; a part that cannot be made gives nil and drops what was pending.
        /// </summary>
        private static object Continue(
            LispInterpreter lisp,
            CadCommandSession session,
            Pending pending,
            string type,
            List<KeyValuePair<int, object>> given,
            object list
        )
        {
            PendingComplex complex = pending.Complex;

            if (complex == null)
            {
                return null;
            }

            if (type == "SEQEND")
            {
                return End(lisp, session, pending, list);
            }

            if ((type == "VERTEX") != (complex.Type == "POLYLINE"))
            {
                return Refuse(lisp, pending);
            }

            List<KeyValuePair<int, object>> part =
                type == "VERTEX" ? CheckedVertex(given) : CheckedAttribute(session.Document, given);

            if (part == null)
            {
                pending.Complex = null;
                return null;
            }

            complex.Parts.Add(part);
            return list;
        }

        /// <summary>The groups of a vertex a polyline can hold; null where it has no point.</summary>
        private static List<KeyValuePair<int, object>> CheckedVertex(List<KeyValuePair<int, object>> given)
        {
            List<KeyValuePair<int, object>> groups = ChangesOf(given);
            CompletePoints("VERTEX", groups);

            if (!(groups.Find(p => p.Key == 10).Value is double[]))
            {
                return null;
            }

            return groups.FindAll(p =>
                p.Key == 10
                || p.Key == 40
                || p.Key == 41
                || p.Key == 42
                || p.Key == 50
                || (p.Key >= 70 && p.Key <= 74)
                || p.Key == 91
            );
        }

        /// <summary>The groups of an attribute; null where one it needs is missing or the attribute cannot be made of them.</summary>
        private static List<KeyValuePair<int, object>> CheckedAttribute(
            CadDocument document,
            List<KeyValuePair<int, object>> given
        )
        {
            List<KeyValuePair<int, object>> groups = ChangesOf(given);
            CompletePoints("ATTRIB", groups);
            TakeLinetypeAndColour(document.Database, groups);

            foreach (int required in RequiredOf("ATTRIB"))
            {
                if (!groups.Exists(p => p.Key == required))
                {
                    return null;
                }
            }

            try
            {
                BuildAttribute(document.Database, groups);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return null;
            }

            return groups;
        }

        /// <summary>
        /// An attribute of the groups of a list, in no block reference yet: its tag, value, point, height, turn, width and slant,
        /// whether it is not drawn, how it is held and the text style it is in; the layer and the look are put by
        /// <see cref="NewAttribute"/>. A text style the drawing lacks is refused.
        /// </summary>
        private static DxfAttributeReference BuildAttribute(
            DxfDatabase database,
            List<KeyValuePair<int, object>> groups
        )
        {
            double[] at = (double[])groups.Find(p => p.Key == 10).Value;
            string value = ((string)groups.Find(p => p.Key == 1).Value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace('\n', ' ');
            string tag = (string)groups.Find(p => p.Key == 2).Value;
            string styleName = groups.Find(p => p.Key == 7).Value as string;

            if (styleName != null && !database.TextStyleTable.Has(styleName))
            {
                throw new ArgumentException("The text style " + styleName + " is not in the drawing.");
            }

            double Real(int code, double otherwise)
            {
                object found = groups.Find(p => p.Key == code).Value;
                return found is double d ? d
                    : found is int i ? i
                    : otherwise;
            }

            int Whole(int code) => groups.Find(p => p.Key == code).Value is int i ? i : 0;

            var attribute = new DxfAttributeReference(
                new GeoPoint3(at[0], at[1], at.Length > 2 ? at[2] : 0.0),
                value,
                tag,
                styleName == null ? null : database.TextStyleTable[styleName]
            )
            {
                Height = Real(40, 2.5),
                Rotation = Real(50, 0.0),
                WidthFactor = Real(41, 1.0),
                Oblique = Real(51, 0.0),
                Invisible = (Whole(70) & 1) != 0,
            };

            if (Whole(72) != 0 || Whole(74) != 0)
            {
                attribute.HorizontalMode = (DxfTextHorizontalMode)Whole(72);
                attribute.VerticalMode = (DxfTextVerticalMode)Whole(74);

                if (groups.Find(p => p.Key == 11).Value is double[] held)
                {
                    attribute.AlignmentPoint = new GeoPoint3(held[0], held[1], held.Length > 2 ? held[2] : 0.0);
                }
            }

            return attribute;
        }

        /// <summary>An attribute of a block reference being made: <see cref="BuildAttribute"/> on its layer (the current one where the list gives none), made where it is not there, and in its colour and linetype.</summary>
        private static DxfAttributeReference NewAttribute(CadDocument document, List<KeyValuePair<int, object>> groups)
        {
            DxfDatabase database = document.Database;
            DxfAttributeReference attribute = BuildAttribute(database, groups);
            string layer = groups.Find(p => p.Key == 8).Value as string ?? database.Clayer;
            EnsureLayer(document, layer);
            attribute.Layer = layer;

            if (
                groups.Find(p => p.Key == 6).Value is string linetype
                && !string.Equals(linetype, "ByLayer", StringComparison.OrdinalIgnoreCase)
                && (
                    string.Equals(linetype, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    || database.LinetypeTable.Has(linetype)
                )
            )
            {
                attribute.Linetype = linetype;
            }

            if (groups.Find(p => p.Key == 62).Value is int colour && colour >= 0 && colour <= 255)
            {
                attribute.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)colour);
            }

            return attribute;
        }

        /// <summary>
        /// The SEQEND: the polyline or the block reference with its attributes is made, in the current space or, while a block is
        /// pending, in its definition. A polyline with no vertex writes <c>Invalid POLYLINE object.</c>. Gives the list of the end,
        /// or nil where nothing was made; what was pending is gone either way.
        /// </summary>
        private static object End(LispInterpreter lisp, CadCommandSession session, Pending pending, object list)
        {
            PendingComplex complex = pending.Complex;
            pending.Complex = null;
            Placement place;

            if (complex.Type == "POLYLINE")
            {
                if (complex.Parts.Count == 0)
                {
                    lisp.Write("Invalid POLYLINE object.\n");
                    return null;
                }

                place = PlacePolyline(complex);
            }
            else
            {
                place = PrepareInsert(session, complex.Layer, complex.Head, complex.Parts);
            }

            if (place == null)
            {
                return null;
            }

            if (pending.Block != null)
            {
                pending.Block.Entries.Add(place);
                return list;
            }

            session.OpenLispStep();
            session.HoldLispStep();
            return Put(session, session.Document.WorkingSpace, place) ? list : null;
        }

        #region The polyline

        /// <summary>The placement of a polyline: its records written as a small DXF and brought in as a paste brings them, the vertices and the end with it.</summary>
        private static Placement PlacePolyline(PendingComplex complex)
        {
            DxfDatabase fragment;

            try
            {
                fragment = DxfDatabase.Parse(Wrap(PolylineRecords(complex)));
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("LispDrawing: entmake could not read the polyline it was given.", exception);
                return null;
            }

            return PlaceFragment(fragment);
        }

        private static void WriteGroup(StringBuilder text, int code, string value) =>
            text.Append(code.ToString(CultureInfo.InvariantCulture))
                .Append('\n')
                .Append(value.Replace("\r", string.Empty).Replace('\n', ' '))
                .Append('\n');

        /// <summary>The text of a DXF whose only entity is made of the records given.</summary>
        private static string Wrap(string records)
        {
            var text = new StringBuilder();
            WriteGroup(text, 0, "SECTION");
            WriteGroup(text, 2, "HEADER");
            WriteGroup(text, 9, "$ACADVER");
            WriteGroup(text, 1, "AC1027");
            WriteGroup(text, 0, "ENDSEC");
            WriteGroup(text, 0, "SECTION");
            WriteGroup(text, 2, "ENTITIES");
            text.Append(records);
            WriteGroup(text, 0, "ENDSEC");
            WriteGroup(text, 0, "EOF");
            return text.ToString();
        }

        /// <summary>
        /// The POLYLINE, its VERTEXes and its SEQEND as a DXF has them. The flags choose the kind (8 a 3D polyline, 16 a mesh, 64 a
        /// polyface mesh, otherwise a 2D one), which the markers of the records say; a vertex of a 3D polyline that gives no flags
        /// has 32. Every record is on the polyline's layer, with its look.
        /// </summary>
        private static string PolylineRecords(PendingComplex complex)
        {
            var text = new StringBuilder();
            List<KeyValuePair<int, object>> head = complex.Head;
            int flags = head.Find(p => p.Key == 70).Value is int given ? given : 0;
            bool mesh = (flags & 64) != 0;
            bool net = !mesh && (flags & 16) != 0;
            bool space = !mesh && !net && (flags & 8) != 0;
            string marker =
                mesh ? "AcDbPolyFaceMesh"
                : net ? "AcDbPolygonMesh"
                : space ? "AcDb3dPolyline"
                : "AcDb2dPolyline";
            List<KeyValuePair<int, object>> look = head.FindAll(p => LispEntityData.IsHeadCode(p.Key));
            double[] at = head.Find(p => p.Key == 10).Value as double[] ?? new[] { 0.0, 0.0, 0.0 };

            void Common(string type, string subclass)
            {
                WriteGroup(text, 0, type);
                WriteGroup(text, 100, "AcDbEntity");
                WriteGroup(text, 8, complex.Layer);

                foreach (KeyValuePair<int, string> group in LispEntityData.ToGroups(type, look))
                {
                    WriteGroup(text, group.Key, group.Value);
                }

                if (subclass != null)
                {
                    WriteGroup(text, 100, subclass);
                }
            }

            Common("POLYLINE", marker);
            WriteGroup(text, 66, "1");

            foreach (
                KeyValuePair<int, string> group in LispEntityData.ToGroups(
                    "POLYLINE",
                    new[] { LispEntityData.Pair(10, at) }
                )
            )
            {
                WriteGroup(text, group.Key, group.Value);
            }

            WriteGroup(text, 70, flags.ToString(CultureInfo.InvariantCulture));

            foreach (
                KeyValuePair<int, string> group in LispEntityData.ToGroups(
                    "POLYLINE",
                    head.FindAll(p => !LispEntityData.IsHeadCode(p.Key) && p.Key != 66 && p.Key != 10 && p.Key != 70)
                )
            )
            {
                WriteGroup(text, group.Key, group.Value);
            }

            foreach (List<KeyValuePair<int, object>> vertex in complex.Parts)
            {
                int vertexFlags = vertex.Find(p => p.Key == 70).Value is int own ? own : 0;
                string kind =
                    mesh ? ((vertexFlags & 128) != 0 ? "AcDbFaceRecord" : "AcDbPolyFaceMeshVertex")
                    : net ? "AcDbPolygonMeshVertex"
                    : space ? "AcDb3dPolylineVertex"
                    : "AcDb2dVertex";
                Common("VERTEX", "AcDbVertex");
                WriteGroup(text, 100, kind);
                var body = new List<KeyValuePair<int, object>>(vertex);

                if (space && !body.Exists(p => p.Key == 70))
                {
                    body.Add(LispEntityData.Pair(70, 32));
                }

                foreach (KeyValuePair<int, string> group in LispEntityData.ToGroups("VERTEX", body))
                {
                    WriteGroup(text, group.Key, group.Value);
                }
            }

            Common("SEQEND", null);
            return text.ToString();
        }

        #endregion

        #region The attributes

        // The groups of the text a text and an attribute are, before the groups of the attribute.
        private static readonly HashSet<int> TextCodes = new HashSet<int>
        {
            39,
            10,
            40,
            1,
            50,
            41,
            51,
            7,
            71,
            72,
            11,
            210,
        };

        /// <summary>
        /// The groups of an ATTDEF with the subclass markers of AutoCAD's record: those of the text first, then the marker of the
        /// definition and the tag, prompt and flags. Nothing is added to what the list gave.
        /// </summary>
        private static List<KeyValuePair<int, object>> AttributeMarked(
            string type,
            List<KeyValuePair<int, object>> body
        )
        {
            var result = new List<KeyValuePair<int, object>> { LispEntityData.Pair(100, "AcDbText") };
            result.AddRange(body.FindAll(p => TextCodes.Contains(p.Key)));
            result.Add(LispEntityData.Pair(100, type == "ATTDEF" ? "AcDbAttributeDefinition" : "AcDbAttribute"));
            result.AddRange(body.FindAll(p => !TextCodes.Contains(p.Key)));
            return result;
        }

        #endregion

        #region BLOCK and ENDBLK

        /// <summary>
        /// BLOCK: a name and a base point are needed (nil at once without them); the flags may be missing, which ENDBLK says. The
        /// entities made after it are kept for the definition. A BLOCK while another is pending cannot be made and drops it.
        /// </summary>
        private static object BeginBlock(Pending pending, List<KeyValuePair<int, object>> given, object list)
        {
            if (pending.Block != null)
            {
                pending.Block = null;
                return null;
            }

            string name = (given.Find(p => p.Key == 2).Value as string)?.Trim();

            if (string.IsNullOrEmpty(name) || !(given.Find(p => p.Key == 10).Value is double[] origin))
            {
                return null;
            }

            int? flags = given.Find(p => p.Key == 70).Value is int own ? own : (int?)null;
            pending.Block = new PendingBlock(name, flags, origin);
            return list;
        }

        /// <summary>
        /// ENDBLK: the block is made of the entities kept, as -BLOCK makes one (a block of the name that is there is made again
        /// with them, which is written first: <c>Redefining block "GHE1"</c>). Gives the block's name, <c>*U1</c> for a block named
        /// <c>*U</c>; nil where no block is pending, where its flags were not given (<c>Block flags missing</c>, no line break
        /// after it) or its name is not one a block can have.
        /// </summary>
        private static object EndBlock(LispInterpreter lisp, CadCommandSession session, Pending pending)
        {
            PendingBlock block = pending.Block;

            if (block == null)
            {
                return null;
            }

            pending.Block = null;

            if (block.Flags == null)
            {
                lisp.Write("Block flags missing");
                return null;
            }

            DxfDatabase database = session.Document.Database;
            string name = block.Name;
            bool anonymous = name.Length == 2 && name[0] == '*' && "UuDdTt".IndexOf(name[1]) >= 0;

            if (!anonymous)
            {
                try
                {
                    DxfSymbolTable.CheckName(name, nameof(name));
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            bool exists = !anonymous && database.BlockTable.Has(name);
            session.OpenLispStep();
            session.HoldLispStep();

            if (exists)
            {
                lisp.Write("Redefining block \"" + name + "\"\n");
            }

            string made = null;
            var planned = new[]
            {
                new CadEditRecord(
                    CadEditRecordKind.TableChanged,
                    null,
                    exists ? "Redefine block" : "New block",
                    null,
                    name
                ),
            };
            CadEditResult result = session.Apply("ENTMAKE", planned, s =>
            {
                DxfBlockTableRecord record;
                var origin = new GeoPoint3(
                    block.Origin[0],
                    block.Origin[1],
                    block.Origin.Length > 2 ? block.Origin[2] : 0.0
                );

                if (exists)
                {
                    record = s.Database.BlockTable[name];

                    foreach (DxfEntity old in record.ToList())
                    {
                        old.Erase();
                    }

                    record.Origin = origin;
                }
                else
                {
                    // The BLOCK entity of a block made by entmake lists the name it was given as its group 1 (AutoCAD 2021, console,
                    // l2p2 pf: GHF1 and, for <c>*U</c>, "*U" though the block is *U5), where a block of -BLOCK lists an empty one.
                    record = new DxfBlockTableRecord
                    {
                        Name = name,
                        Origin = origin,
                        MadePath = block.Name,
                    };
                    s.Database.BlockTable.Add(record);
                }

                foreach (Placement place in block.Entries)
                {
                    place(s, record, null);
                }

                made = record.Name;
            });

            return result.Succeeded ? made : null;
        }

        #endregion
    }
}
