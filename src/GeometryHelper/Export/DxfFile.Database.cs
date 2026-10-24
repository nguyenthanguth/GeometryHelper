using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Export
{
    internal sealed partial class DxfFile
    {
        // The file an entity made in code stands in until it is added to one: R2018, so that any type and look can be held.
        private static readonly byte[] TemplateBytes = Encoding.ASCII.GetBytes("  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1032\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n");

        private static readonly DxfFile Template = new DxfFile(TemplateBytes, TemplateBytes.Length, null);

        private readonly bool _detached;
        private readonly DxfFile _origin;

        /// <summary>
        /// A file that holds entities not yet in a file of their own: made in code, or copied from another. It shares the bytes
        /// and style of the file it is made from, writes no handles and no owners, adds no layer, takes any linetype and grows
        /// no extents; what is put in it is never saved.
        /// </summary>
        private DxfFile(DxfFile from)
        {
            _bytes = from._bytes;
            _length = from._length;
            _starts = from._starts;
            _types = from._types;
            _sections = from._sections;
            _decoding = from._decoding;
            _header = from._header;
            _namedPage = from._namedPage;
            Version = from.Version;
            CodePage = from.CodePage;
            Warnings = from.Warnings;
            _detached = true;
            _origin = from._origin ?? from;

            _drawing = new Lazy<Drawing>(ReadDrawing);
            _blocks = new Lazy<IReadOnlyDictionary<string, DxfFileBlock>>(ReadBlocks);
            _layers = NewLayers();
            _placer = new Lazy<DxfReader.Placer>(NewPlacer);
        }

        /// <summary>Whether the file holds entities not yet in a file of their own.</summary>
        internal bool IsDetached => _detached;

        /// <summary>Whether the file writes subclass markers: from R13 on.</summary>
        internal bool HasMarkers => Style.Markers;

        #region Entities not yet in a file

        /// <summary>A shape written as an entity of its own, on layer 0 with no look, as the writer writes it in a file of R2018.</summary>
        internal static DxfItem Detached(object shape)
        {
            var file = new DxfFile(Template);
            return file.Add(file.ModelSpace, shape, null, null, null, null, null, 0.0, 0);
        }

        /// <summary>
        /// An entity of its own written from its groups: its type, the marker AcDbEntity, layer 0, then the groups given, which
        /// begin with its subclass marker.
        /// </summary>
        internal static DxfItem Detached(string type, IEnumerable<DxfTypedValue> groups)
        {
            var file = new DxfFile(Template);
            var b = new Builder(file);
            b.Group(0, type);
            b.Group(100, "AcDbEntity");
            b.Group(8, "0");

            foreach (DxfTypedValue group in groups)
            {
                b.Group(group.TypeCode, group.Value);
            }

            return file.Hold(new List<Rec> { b.Done(type) }, type);
        }

        /// <summary>
        /// A copy of an entity, not in the file: every record of it with its groups as written, but its handle, its owner, the
        /// groups of the applications (102: reactors and the extension dictionary), its space (67) and its layout (410).
        /// </summary>
        internal DxfItem CopyOf(DxfItem item)
        {
            var file = new DxfFile(this);
            var records = new List<Rec>();

            foreach (Rec record in RecordsOf(item))
            {
                var b = new Builder(file);
                List<Pair> pairs = PairsOf(record);
                b.Raw(record, pairs[0]);
                bool braced = false;

                for (int i = 1; i < pairs.Count; i++)
                {
                    Pair pair = pairs[i];

                    if (pair.Code == 102)
                    {
                        braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                        continue;
                    }

                    bool data = pair.Code >= 1000;

                    if (braced || (!data && (pair.Code == 5 || pair.Code == 105 || pair.Code == 330 || pair.Code == 360 || pair.Code == 67 || pair.Code == 410)))
                    {
                        continue;
                    }

                    b.Raw(record, pair);
                }

                records.Add(file.Recounted(b.Done(record.Type)));
            }

            return file.Hold(records, records[0].Type);
        }

        /// <summary>Records of an entity held at the end of the ENTITIES section of a file that is not saved.</summary>
        private DxfItem Hold(List<Rec> records, string type)
        {
            var piece = new Piece(records);
            return new DxfItem(this, piece, type) { Home = PutBefore(EntitiesEnd(), piece) };
        }

        /// <summary>
        /// Adds an entity not yet in a file to a space of this one, as AutoCAD appends one: its records written in the file's
        /// own style, with handles of their own above every handle of the file, the space's owner, its space (67) in paper
        /// space, no subclass markers in a file of R12, and every other group as the entity has it; its layer added to the
        /// layer table where it is not listed, and the applications of its extended data to the table of applications.
        /// </summary>
        internal DxfItem Adopt(DxfSpace space, DxfItem from)
        {
            DxfFile source = from.File;
            List<Rec> records = source.RecordsOf(from);
            Rec head = records[0];
            string type = head.Type;
            Writing style = Style;

            // A record that follows an entity is not one of a block: AutoCAD refuses a vertex appended to a block table record.
            if (type == "VERTEX" || type == "SEQEND" || type == "ATTRIB")
            {
                throw new ArgumentException("A " + type + " belongs to the entity it follows, a POLYLINE or an INSERT: add it by that entity, not to a block.", "entity");
            }

            if (!style.Markers && SinceR13.Contains(type))
            {
                throw new ArgumentException("A " + type + " cannot be added to a DXF " + VersionSaid() + ", which has no such type: R13 (AC1012) brought it.", "entity");
            }

            if (type == "LWPOLYLINE" && !style.Lightweight)
            {
                throw new ArgumentException("A LWPOLYLINE cannot be added to a DXF " + VersionSaid() + ", which has no lightweight polylines: R14 (AC1014) brought them. Add a DxfPolyline2d.", "entity");
            }

            // An entity of R12 is written in the forms of R13 on: each of its records needs AutoCAD's subclass markers. One of
            // R13 or later is written in the forms of R12 in a file of R12: each record keeps the groups R12 has, each from its
            // own part.
            bool marking = style.Markers && !source.Style.Markers;
            bool unmarking = !style.Markers && source.Style.Markers;

            foreach (Rec record in marking ? records : new List<Rec>())
            {
                if (MarkersOf(record, PairsOf(record), out _, out _) == null)
                {
                    throw new ArgumentException("A " + record.Type + " of a DXF of R12, which has no subclass markers, cannot be added to a DXF " + VersionSaid() + " yet.", "entity");
                }
            }

            foreach (Rec record in unmarking ? records : new List<Rec>())
            {
                if (!R12Groups.ContainsKey(record.Type))
                {
                    throw new ArgumentException("A " + record.Type + " of a DXF of R13 or later has no form of R12 the library writes, so it cannot be added to a DXF " + VersionSaid() + ".", "entity");
                }
            }

            string layer = null;
            string linetype = null;
            bool weighted = false;
            bool coloured = false;
            bool ownLook = false;
            var applications = new List<string>();
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);

            for (int i = 1; i < pairs.Count; i++)
            {
                string value = source.Text(head, pairs[i]);

                if (i >= data)
                {
                    if (pairs[i].Code == 1001)
                    {
                        applications.Add(value.Trim());
                    }

                    continue;
                }

                switch (pairs[i].Code)
                {
                    case 8: layer = layer ?? value; break;
                    case 6: linetype = linetype ?? value; break;
                    case 370: weighted = true; break;
                    case 420:
                    case 430:
                    case 440: coloured = true; break;
                    case 48:
                    case 60: ownLook = true; break;
                }
            }

            if (ownLook && !style.Markers)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no linetype scale or visibility of an entity's own, which came with R13 (AC1012).", "entity");
            }

            if (weighted && !style.Lineweights)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).", "entity");
            }

            if (coloured && !style.TrueColours)
            {
                throw new ArgumentException("A DXF " + VersionSaid() + " has no true colours, colour books or transparency, which came with R2004 (AC1018).", "entity");
            }

            if (linetype != null && !HasLinetype(linetype))
            {
                throw new ArgumentException("The linetype " + DxfReader.NameOf(linetype) + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", "entity");
            }

            EnsureLayer(layer == null || layer.Trim().Length == 0 ? "0" : layer.Trim());

            foreach (string application in applications)
            {
                EnsureApplication(application);
            }

            // A copy of an entity of another drawing points at objects of that drawing: each pointer is taken to the object of
            // the same name here, or left out, as AutoCAD's deep clone between databases maps or drops them.
            DxfFile origin = source.Origin;

            if (origin != null && origin != Template && origin != this)
            {
                for (int r = 0; r < records.Count; r++)
                {
                    records[r] = source.Pointed(records[r], origin, this);
                }

                head = records[0];
            }

            bool paper = space.Kind == DxfSpaceKind.Paper;
            string handle = UsesHandles() ? NewHandle() : null;
            string owner = style.Markers ? OwnerOf(space) : null;
            var written = new List<Rec> { Written(source, head, handle, owner, paper, marking, unmarking, records.Count > 1 && records[1].Type != "SEQEND") };

            for (int r = 1; r < records.Count; r++)
            {
                string own = UsesHandles() ? NewHandle() : null;
                written.Add(Written(source, records[r], own, style.Markers ? handle : null, paper, marking, unmarking, false));
            }

            var piece = new Piece(written);
            var item = new DxfItem(this, piece, type) { Home = Place(space, piece), IsPaper = paper, Block = space.Block?.Name };
            TakeFollowers(from, item);
            space.Append(item);

            if (space.Kind != DxfSpaceKind.Block)
            {
                GrowBy(item);
            }

            Edited();
            return item;
        }

        /// <summary>
        /// A record of another file written in this one's style: its type, the handle and owner given, its space in paper space,
        /// and its groups as they are, but its subclass markers in a file of R12.
        /// </summary>
        private Rec Copied(DxfFile source, Rec record, string handle, string owner, bool paper)
        {
            Writing style = Style;
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (owner != null)
            {
                b.Group(330, owner);
            }

            if (paper && !style.Markers)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);

            for (int i = 1; i < pairs.Count; i++)
            {
                string value = source.Text(record, pairs[i]);

                if (pairs[i].Code == 100 && !style.Markers)
                {
                    continue;
                }

                b.Group(pairs[i].Code, value);

                if (paper && style.Markers && pairs[i].Code == 100 && value.Trim() == "AcDbEntity")
                {
                    b.Group(67, "1");
                }
            }

            return b.Done(record.Type);
        }

        /// <summary>
        /// A record of a file of R12 written in the forms of R13 on, as AutoCAD writes it: its type, the handle and owner given,
        /// the marker AcDbEntity with its space in paper space and the groups all entities have, then each subclass marker of
        /// its type in AutoCAD's order with the groups of its own that go under it, then its extended data; every value as the
        /// record has it.
        /// </summary>
        private Rec Marked(DxfFile source, Rec record, string handle, string owner, bool paper)
        {
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (owner != null)
            {
                b.Group(330, owner);
            }

            b.Group(100, "AcDbEntity");

            if (paper)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            string[] markers = MarkersOf(record, pairs, out HashSet<int> late, out bool allLate);

            for (int i = 1; i < data; i++)
            {
                if (IsCommon(pairs[i].Code) && !Placed(pairs[i].Code))
                {
                    b.Group(pairs[i].Code, source.Text(record, pairs[i]));
                }
            }

            for (int m = 0; m < markers.Length; m++)
            {
                b.Group(100, markers[m]);
                bool last = m == markers.Length - 1;

                for (int i = 1; i < data; i++)
                {
                    int code = pairs[i].Code;

                    if (IsCommon(code) || Placed(code) || code == 100 || code == 102)
                    {
                        continue;
                    }

                    bool lateCode = allLate || (late != null && late.Contains(code));

                    if (markers.Length == 1 || lateCode == last)
                    {
                        b.Group(code, source.Text(record, pairs[i]));
                    }
                }
            }

            for (int i = data; i < pairs.Count; i++)
            {
                b.Group(pairs[i].Code, source.Text(record, pairs[i]));
            }

            return b.Done(record.Type);
        }

        /// <summary>Whether a group is one a record written anew is given afresh: its handle, owner and space.</summary>
        private static bool Placed(int code) => code == 5 || code == 105 || code == 330 || code == 360 || code == 67 || code == 410;

        /// <summary>
        /// The subclass markers AutoCAD writes a record of a type of R12 with from R13 on, in its order, as ezdxf writes them; the
        /// codes that go under the last where the type has two (an ARC's angles, a TEXT's vertical justification, an ATTRIB's
        /// tag, flags, length and vertical justification), or whether all go under the last (a VERTEX, whose AcDbVertex has
        /// none). Null for a type with no form of R13 here, as a DIMENSION.
        /// </summary>
        private string[] MarkersOf(Rec record, List<Pair> pairs, out HashSet<int> late, out bool allLate)
        {
            late = null;
            allLate = false;

            switch (record.Type)
            {
                case "LINE": return new[] { "AcDbLine" };
                case "POINT": return new[] { "AcDbPoint" };
                case "CIRCLE": return new[] { "AcDbCircle" };
                case "SOLID":
                case "TRACE": return new[] { "AcDbTrace" };
                case "3DFACE": return new[] { "AcDbFace" };
                case "SHAPE": return new[] { "AcDbShape" };
                case "SEQEND": return new string[0];
                case "ARC":
                    late = new HashSet<int> { 50, 51 };
                    return new[] { "AcDbCircle", "AcDbArc" };
                case "TEXT":
                    late = new HashSet<int> { 73 };
                    return new[] { "AcDbText", "AcDbText" };
                case "ATTDEF":
                    late = new HashSet<int> { 3, 2, 70, 73, 74, 280 };
                    return new[] { "AcDbText", "AcDbAttributeDefinition" };
                case "ATTRIB":
                    late = new HashSet<int> { 2, 70, 73, 74, 280 };
                    return new[] { "AcDbText", "AcDbAttribute" };
                case "INSERT":
                    return new[] { FirstWhole(record, pairs, 70) > 1 || FirstWhole(record, pairs, 71) > 1 ? "AcDbMInsertBlock" : "AcDbBlockReference" };
                case "POLYLINE":
                {
                    int flags = FirstWhole(record, pairs, 70);
                    return new[] { (flags & 8) != 0 ? "AcDb3dPolyline" : (flags & 16) != 0 ? "AcDbPolygonMesh" : (flags & 64) != 0 ? "AcDbPolyFaceMesh" : "AcDb2dPolyline" };
                }

                case "VERTEX":
                {
                    int flags = FirstWhole(record, pairs, 70);

                    if ((flags & 192) == 128)
                    {
                        return new[] { "AcDbFaceRecord" };
                    }

                    allLate = true;
                    return new[] { "AcDbVertex", (flags & 32) != 0 ? "AcDb3dPolylineVertex" : (flags & 128) != 0 ? "AcDbPolyFaceMeshVertex" : (flags & 64) != 0 ? "AcDbPolygonMeshVertex" : "AcDb2dVertex" };
                }

                default: return null;
            }
        }

        /// <summary>A record of another file in this one's forms: given AutoCAD's markers, written in the forms of R12, or copied as it is.</summary>
        private Rec Written(DxfFile source, Rec record, string handle, string owner, bool paper, bool marking, bool unmarking, bool follows)
        {
            if (marking)
            {
                return Marked(source, record, handle, owner, paper);
            }

            return unmarking ? Unmarked(source, record, handle, paper, follows) : Copied(source, record, handle, owner, paper);
        }

        // The groups each type of R12 has of its own, beside its layer, linetype and colour, as the DXF reference of R12 and
        // ezdxf give them; an ATTRIB's and an ATTDEF's tag, prompt, flags, length and vertical justification from their part
        // AcDbAttribute or AcDbAttributeDefinition, every other group of a text from its part AcDbText.
        private static readonly Dictionary<string, HashSet<int>> R12Groups = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal)
        {
            ["LINE"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 210, 220, 230 },
            ["POINT"] = new HashSet<int> { 39, 10, 20, 30, 50, 210, 220, 230 },
            ["CIRCLE"] = new HashSet<int> { 39, 10, 20, 30, 40, 210, 220, 230 },
            ["ARC"] = new HashSet<int> { 39, 10, 20, 30, 40, 50, 51, 210, 220, 230 },
            ["SOLID"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 210, 220, 230 },
            ["TRACE"] = new HashSet<int> { 39, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 210, 220, 230 },
            ["3DFACE"] = new HashSet<int> { 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 70 },
            ["SHAPE"] = new HashSet<int> { 39, 10, 20, 30, 40, 2, 50, 41, 51, 210, 220, 230 },
            ["TEXT"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 50, 41, 51, 7, 71, 72, 73, 11, 21, 31, 210, 220, 230 },
            ["ATTDEF"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 3, 2, 70, 73, 50, 41, 51, 7, 71, 72, 74, 11, 21, 31, 210, 220, 230 },
            ["ATTRIB"] = new HashSet<int> { 39, 10, 20, 30, 40, 1, 2, 70, 73, 50, 41, 51, 7, 71, 72, 74, 11, 21, 31, 210, 220, 230 },
            ["INSERT"] = new HashSet<int> { 66, 2, 10, 20, 30, 41, 42, 43, 50, 70, 71, 44, 45, 210, 220, 230 },
            ["POLYLINE"] = new HashSet<int> { 66, 10, 20, 30, 39, 70, 40, 41, 71, 72, 73, 74, 75, 210, 220, 230 },
            ["VERTEX"] = new HashSet<int> { 10, 20, 30, 40, 41, 42, 70, 50, 71, 72, 73, 74 },
            ["SEQEND"] = new HashSet<int>(),
        };

        // The groups of an attribute that stand in its part AcDbAttribute or AcDbAttributeDefinition, not in AcDbText.
        private static readonly HashSet<int> AttributeGroups = new HashSet<int> { 2, 3, 70, 73, 74 };

        /// <summary>
        /// A record of R13 or later written in the forms of R12, as AutoCAD saves a drawing as R12: its type, the handle given,
        /// its space in paper space, its layer, linetype and colour, then the groups R12 has for its type, each the first of its
        /// code from the part it belongs to, in the order the record has them; its extended data. Left out: the subclass
        /// markers, the groups of the applications, the owner, every group R12 has not (lineweight, true colour, material,
        /// plot style), and an object embedded after a group 101, as an MTEXT attribute's. Groups whose codes the parts share,
        /// as an ATTRIB's 71 and 72 of AcDbText and of AcDbAttribute, are read from the part R12 means. A POLYLINE or an INSERT
        /// followed by its vertices or attributes says so (66 of 1).
        /// </summary>
        private Rec Unmarked(DxfFile source, Rec record, string handle, bool paper, bool follows)
        {
            HashSet<int> own = R12Groups[record.Type];
            bool attribute = record.Type == "ATTRIB" || record.Type == "ATTDEF";
            var b = new Builder(this);
            b.Group(0, record.Type);

            if (handle != null)
            {
                b.Group(5, handle);
            }

            if (paper)
            {
                b.Group(67, "1");
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var written = new HashSet<int>();
            string part = string.Empty;
            bool braced = false;
            bool embedded = false;
            bool followsWritten = false;

            for (int i = 1; i < data; i++)
            {
                int code = pairs[i].Code;
                string value = source.Text(record, pairs[i]);

                if (code == 102)
                {
                    braced = value.Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced || embedded || code == 999)
                {
                    continue;
                }

                if (code == 101)
                {
                    embedded = true;
                    continue;
                }

                if (code == 100)
                {
                    part = value.Trim();
                    continue;
                }

                bool common = part.Length == 0 || part == "AcDbEntity";

                if (common)
                {
                    if ((code == 8 || code == 6 || code == 62) && written.Add(code))
                    {
                        b.Group(code, value);
                    }

                    continue;
                }

                if (!own.Contains(code))
                {
                    continue;
                }

                if (attribute && AttributeGroups.Contains(code) != part.StartsWith("AcDbAttribute", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!written.Add(code))
                {
                    continue;
                }

                if (code == 66)
                {
                    followsWritten = true;
                    b.Group(66, follows ? "1" : value);
                    continue;
                }

                // The vertices or attributes that follow are said to follow before the rest of the head's own groups.
                if (follows && !followsWritten && own.Contains(66))
                {
                    followsWritten = written.Add(66);
                    b.Group(66, "1");
                }

                b.Group(code, value);
            }

            if (follows && !followsWritten && own.Contains(66) && written.Add(66))
            {
                b.Group(66, "1");
            }

            for (int i = data; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    b.Group(pairs[i].Code, source.Text(record, pairs[i]));
                }
            }

            return b.Done(record.Type);
        }

        /// <summary>The file a copy was made from, whose objects its pointers name; null for a file read or made in code.</summary>
        internal DxfFile Origin => _origin;

        /// <summary>
        /// A record of a copy with its pointers into the drawing it was copied from taken to this one (<paramref name="target"/>):
        /// each soft or hard pointer (320 to 369, 390 to 399, 480 and 481) to the object of the same name there, a table's record
        /// by its name, an object of the named object dictionary (a material, a plot style, a visual style) by the names of its
        /// dictionary and of its entry; left out where the target has none of that name, so that the target's default holds. A
        /// handle of extended data (1005) is left out, as AutoCAD's mapping of objects between drawings leaves it null. The
        /// record itself where nothing changes.
        /// </summary>
        private Rec Pointed(Rec record, DxfFile origin, DxfFile target)
        {
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var b = new Builder(this);
            b.Raw(record, pairs[0]);
            bool changed = false;
            bool braced = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                int code = pair.Code;

                if (code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (i >= data && code == 1005)
                {
                    changed = true;
                    continue;
                }
                else if (i < data && !braced && IsPointer(code))
                {
                    string was = Text(record, pair);
                    string now = target.Equivalent(origin, was);
                    changed = true;

                    if (now != null)
                    {
                        b.Group(code, now);
                    }

                    continue;
                }

                b.Raw(record, pair);
            }

            return changed ? Recounted(b.Done(record.Type)) : record;
        }

        /// <summary>
        /// A HATCH whose boundary objects were left out of a copy, its counts made to agree with what is left: each path's count
        /// of boundary objects (97) the number of pointers (330) that follow it, nought where none does, as AutoCAD reads that
        /// many after it; and, with no boundary object left in the whole record, its associativity (71) nought, as AutoCAD's
        /// COPY into another drawing leaves a hatch that keeps none. Any other record as it is.
        /// </summary>
        private Rec Recounted(Rec record)
        {
            if (record.Type != "HATCH")
            {
                return record;
            }

            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            var counts = new Dictionary<int, int>();
            int kept = 0;
            int associative = -1;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 71 && associative < 0)
                {
                    associative = i;
                }

                if (pairs[i].Code != 97)
                {
                    continue;
                }

                int following = 0;

                while (i + 1 + following < data && pairs[i + 1 + following].Code == 330)
                {
                    following++;
                }

                kept += following;
                int written = int.TryParse(Text(record, pairs[i]).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : -1;

                if (written != following)
                {
                    counts.Add(i, following);
                }
            }

            bool unlink = kept == 0 && associative > 0 && Text(record, pairs[associative]).Trim() != "0";

            if (counts.Count == 0 && !unlink)
            {
                return record;
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (counts.TryGetValue(i, out int count))
                {
                    b.Group(97, count.ToString(CultureInfo.InvariantCulture));
                }
                else if (unlink && i == associative)
                {
                    b.Group(71, "0");
                }
                else
                {
                    b.Raw(record, pairs[i]);
                }
            }

            return b.Done(record.Type);
        }

        private static bool IsPointer(int code) => (code >= 320 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481;

        /// <summary>
        /// The handle in this file of the object a handle of another file names: the record of a table of the same name, or the
        /// object of the named object dictionary under the same dictionary and entry names; null where there is none.
        /// </summary>
        private string Equivalent(DxfFile origin, string pointer)
        {
            if (!TryHex(pointer.Trim(), out ulong handle) || handle == 0)
            {
                return null;
            }

            Dictionary<ulong, KeyValuePair<Rec, bool>> theirs = origin.Objects();

            if (!theirs.TryGetValue(handle, out KeyValuePair<Rec, bool> found))
            {
                return null;
            }

            Rec record = found.Key;

            if (found.Value)
            {
                string name = DxfReader.NameOf(origin.Record(record).Text(2) ?? string.Empty);

                foreach (Rec mine in TableRecords(record.Type))
                {
                    if (string.Equals(DxfReader.NameOf(Record(mine).Text(2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleOf(mine, out ulong own) ? own.ToString("X", CultureInfo.InvariantCulture) : null;
                    }
                }

                return null;
            }

            if (!origin.NamedPath(handle, out string dictionary, out string entry))
            {
                return null;
            }

            Dictionary<ulong, KeyValuePair<Rec, bool>> ours = Objects();
            string under = Entry(RootDictionary(), dictionary);

            if (under == null || !TryHex(under, out ulong dictionaryHandle) || !ours.TryGetValue(dictionaryHandle, out KeyValuePair<Rec, bool> dictionaryRecord))
            {
                return null;
            }

            return Entry(dictionaryRecord.Key, entry);
        }

        /// <summary>The records of the TABLES and OBJECTS sections as they stand, by handle, each with whether it is a table's.</summary>
        private Dictionary<ulong, KeyValuePair<Rec, bool>> Objects()
        {
            var objects = new Dictionary<ulong, KeyValuePair<Rec, bool>>();

            foreach (Section section in _sections)
            {
                if (section.Name != "TABLES" && section.Name != "OBJECTS")
                {
                    continue;
                }

                foreach (Rec record in Current(section.First + 1, section.Last))
                {
                    if (record.Type != "TABLE" && record.Type != "ENDTAB" && HandleOf(record, out ulong handle) && !objects.ContainsKey(handle))
                    {
                        objects.Add(handle, new KeyValuePair<Rec, bool>(record, section.Name == "TABLES"));
                    }
                }
            }

            return objects;
        }

        /// <summary>The first DICTIONARY of the OBJECTS section, the named object dictionary; null where there is none.</summary>
        private Rec? RootDictionary()
        {
            foreach (Section section in _sections)
            {
                if (section.Name != "OBJECTS")
                {
                    continue;
                }

                foreach (Rec record in Current(section.First + 1, section.Last))
                {
                    if (record.Type == "DICTIONARY")
                    {
                        return record;
                    }
                }
            }

            return null;
        }

        /// <summary>The handle a DICTIONARY gives an entry name (its 3, then 350 or 360), letters of either case the same; null where it has none.</summary>
        private string Entry(Rec? dictionary, string name)
        {
            if (dictionary == null)
            {
                return null;
            }

            Rec record = dictionary.Value;
            string last = null;

            foreach (Pair pair in PairsOf(record))
            {
                if (pair.Code == 3)
                {
                    last = Text(record, pair);
                }
                else if ((pair.Code == 350 || pair.Code == 360) && last != null)
                {
                    if (string.Equals(last.Trim(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return Text(record, pair).Trim();
                    }

                    last = null;
                }
            }

            return null;
        }

        /// <summary>The names under which the named object dictionary holds an object: its dictionary's and its entry's; false where it holds none.</summary>
        private bool NamedPath(ulong handle, out string dictionary, out string entry)
        {
            dictionary = null;
            entry = null;
            Rec? root = RootDictionary();

            if (root == null)
            {
                return false;
            }

            foreach (KeyValuePair<Rec, bool> found in Objects().Values)
            {
                Rec record = found.Key;

                if (found.Value || record.Type != "DICTIONARY")
                {
                    continue;
                }

                string last = null;

                foreach (Pair pair in PairsOf(record))
                {
                    if (pair.Code == 3)
                    {
                        last = Text(record, pair);
                    }
                    else if ((pair.Code == 350 || pair.Code == 360) && last != null)
                    {
                        if (TryHex(Text(record, pair).Trim(), out ulong value) && value == handle && HandleOf(record, out ulong dictionaryHandle))
                        {
                            Rec rootRecord = root.Value;
                            string parent = null;

                            foreach (Pair rootPair in PairsOf(rootRecord))
                            {
                                if (rootPair.Code == 3)
                                {
                                    parent = Text(rootRecord, rootPair);
                                }
                                else if ((rootPair.Code == 350 || rootPair.Code == 360) && parent != null)
                                {
                                    if (TryHex(Text(rootRecord, rootPair).Trim(), out ulong rootValue) && rootValue == dictionaryHandle)
                                    {
                                        dictionary = parent.Trim();
                                        entry = last.Trim();
                                        return true;
                                    }

                                    parent = null;
                                }
                            }
                        }

                        last = null;
                    }
                }
            }

            return false;
        }

        /// <summary>Puts the records of an entity at the end of a space: the ENTITIES section for model and paper space, before its ENDBLK for a block.</summary>
        private List<Piece> Place(DxfSpace space, Piece piece)
        {
            DxfFileBlock block = space.Block;

            if (block == null)
            {
                return PutBefore(EntitiesEnd(), piece);
            }

            if (block.Home != null)
            {
                block.Home.Insert(block.Home.IndexOf(block.EndPiece), piece);
                return block.Home;
            }

            return PutBefore(block.End, piece);
        }

        /// <summary>The handle of what owns the entities of a space: the BLOCK_RECORD of model space, of paper space, or of the block.</summary>
        private string OwnerOf(DxfSpace space)
        {
            switch (space.Kind)
            {
                case DxfSpaceKind.Model: return ModelOwner();
                case DxfSpaceKind.Paper: return BlockRecordHandle("*Paper_Space");
                default: return space.Block.Head != null ? OwnerOf(space.Block.Head) ?? BlockRecordHandle(space.Block.Name) : BlockRecordHandle(space.Block.Name);
            }
        }

        /// <summary>The handle of the BLOCK_RECORD of a name, letters of either case the same; null where there is none.</summary>
        private string BlockRecordHandle(string name)
        {
            foreach (Rec record in TableRecords("BLOCK_RECORD"))
            {
                DxfReader.Record read = Record(record);

                if (string.Equals(DxfReader.NameOf(read.Text(2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return read.Text(5)?.Trim();
                }
            }

            return null;
        }

        /// <summary>Grows the extents the header is to hold by those of an entity's shapes.</summary>
        private void GrowBy(DxfItem item)
        {
            // A record that follows an entity is drawn as part of it.
            item = item.Parent ?? item;

            if (_detached || item.Block != null)
            {
                return;
            }

            foreach (object shape in item.Shapes)
            {
                if (DxfWriter.CanWrite(shape))
                {
                    DxfWriter.RecordsOf(shape, 0, 0.0, true, true, out double[] extents);
                    Grow(extents, item.IsPaper);
                }
            }
        }

        /// <summary>The type a shape is written as in this file, checked as <see cref="DxfItem.Shape"/> checks it.</summary>
        internal string TypeWritten(object shape) => Written(shape, 0, 0.0, "shape", out _)[0].Key;

        /// <summary>Removes an entity from its space, as <see cref="DxfSpace.Remove(DxfItem)"/> does.</summary>
        internal bool Erase(DxfItem item) => item.Space != null && Remove(item.Space, item);

        #endregion

        #region Records that follow an entity

        /// <summary>
        /// The item of a record that follows an entity (a VERTEX or the SEQEND of a POLYLINE, an ATTRIB or the SEQEND of an
        /// INSERT): the same item for the record while it is there, read and written through the entity, owned by it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a record that follows the entity.</exception>
        internal DxfItem FollowerOf(DxfItem parent, int index)
        {
            List<DxfItem> followers = FollowersOf(parent);

            if (index < 1 || index >= followers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The records that follow the " + parent.Type + " are 1 to " + Number(followers.Count - 1) + ".");
            }

            return followers[index] ?? (followers[index] = new DxfItem(parent, index));
        }

        /// <summary>
        /// The list of the items of the records that follow an entity, one place for each record (none at 0); made anew, the
        /// items made before taken out, where the records were written anew by other means than these.
        /// </summary>
        private List<DxfItem> FollowersOf(DxfItem parent)
        {
            int count = RecordCount(parent);
            List<DxfItem> followers = parent.Followers;

            if (followers == null || followers.Count != count)
            {
                RetireFollowers(parent);
                followers = new List<DxfItem>(new DxfItem[count]);
                parent.Followers = followers;
            }

            return followers;
        }

        /// <summary>Takes out the items of the records that follow an entity, whose records were all written anew: each reads as it was.</summary>
        internal void RetireFollowers(DxfItem parent)
        {
            List<DxfItem> followers = parent.Followers;

            if (followers == null)
            {
                return;
            }

            parent.Followers = null;

            foreach (DxfItem follower in followers)
            {
                if (follower != null)
                {
                    Detach(follower);
                }
            }
        }

        /// <summary>A follower's item no longer of its entity: removed, reading the record it was last.</summary>
        private void Detach(DxfItem follower)
        {
            Rec last = RecordAt(follower, 0);
            follower.Parent = null;
            follower.Piece = new Piece(new List<Rec> { last });
            follower.IsRemoved = true;
            follower.Changed(last.Type);
        }

        /// <summary>The items of the records that follow an entity made in code go with it to the file it is added to.</summary>
        private static void TakeFollowers(DxfItem from, DxfItem to)
        {
            to.Followers = from.Followers;
            from.Followers = null;

            if (to.Followers == null)
            {
                return;
            }

            foreach (DxfItem follower in to.Followers)
            {
                if (follower != null)
                {
                    follower.Parent = to;
                    follower.IsPaper = to.IsPaper;
                    follower.Block = to.Block;
                    follower.Changed(follower.Type);
                }
            }
        }

        /// <summary>
        /// Puts a record made in code among those that follow an entity, before the one at an index (the count of the records to
        /// put it last, before a SEQEND where there is one, which it may not follow): its type and own groups, its look and
        /// extended data as made; a handle of its own where the file uses them, the entity as its owner and the marker
        /// AcDbEntity from R13 on, the entity's space and layer, its subclass markers left out in a file of R12. Every other
        /// record keeps its handle and bytes. The item of the new record, which the object made in code then stands for.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 1 to the place of the SEQEND.</exception>
        /// <exception cref="ArgumentException">Thrown when the record has a lineweight or true colour the file's version has not,
        /// or a linetype its table does not list.</exception>
        internal DxfItem InsertFollower(DxfItem parent, int index, DxfItem made)
        {
            Editable(parent);
            List<Rec> records = RecordsOf(parent);
            int last = records.Count > 1 && records[records.Count - 1].Type == "SEQEND" ? records.Count - 1 : records.Count;

            if (index < 1 || index > last)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A record goes in from 1 to " + Number(last) + ", before the SEQEND of the " + parent.Type + ".");
            }

            DxfFile source = made.File;
            Rec from = source.RecordAt(made, 0);
            List<Pair> pairs = PairsOf(from);
            int data = DataAt(from, pairs);
            Writing style = Style;
            Rec head = records[0];
            List<Pair> headPairs = PairsOf(head);
            int headData = DataAt(head, headPairs);

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 370 && !style.Lineweights)
                {
                    throw new ArgumentException("A DXF " + VersionSaid() + " has no lineweights, which came with R2000 (AC1015).", "value");
                }

                if (pairs[i].Code == 420 && !style.TrueColours)
                {
                    throw new ArgumentException("A DXF " + VersionSaid() + " has no true colours, which came with R2004 (AC1018).", "value");
                }

                if (pairs[i].Code == 6 && !HasLinetype(source.Text(from, pairs[i])))
                {
                    throw new ArgumentException("The linetype " + DxfReader.NameOf(source.Text(from, pairs[i])) + " is not in the file's linetype table: give one of it, BYLAYER or BYBLOCK.", "value");
                }
            }

            var b = new Builder(this);
            b.Group(0, from.Type);

            if (UsesHandles())
            {
                b.Group(5, NewHandle());
            }

            string owner = parent.Handle;

            if (style.Markers && owner != null)
            {
                b.Group(330, owner);
            }

            if (style.Markers)
            {
                b.Group(100, "AcDbEntity");
            }

            KeepSpaceAndLayer(b, head, headPairs, headData);
            bool braced = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                Pair pair = pairs[i];
                string value = source.Text(from, pair);

                if (i >= data)
                {
                    if (pair.Code == 1001)
                    {
                        EnsureApplication(value.Trim());
                    }

                    b.Group(pair.Code, value);
                    continue;
                }

                if (pair.Code == 102)
                {
                    braced = value.Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                bool kept = !braced && pair.Code != 5 && pair.Code != 105 && pair.Code != 330 && pair.Code != 360 && pair.Code != 67 && pair.Code != 410 && pair.Code != 8
                    && !(pair.Code == 100 && (!style.Markers || value.Trim() == "AcDbEntity"));

                if (kept)
                {
                    b.Group(pair.Code, value);
                }
            }

            Rec put = b.Done(from.Type);
            List<DxfItem> followers = FollowersOf(parent);
            records.Insert(index, put);
            SetRecords(parent, records);
            parent.Changed(head.Type);

            var item = new DxfItem(parent, index);
            followers.Insert(index, item);

            for (int i = index + 1; i < followers.Count; i++)
            {
                if (followers[i] != null)
                {
                    followers[i].Index = i;
                }
            }

            GrowBy(parent);
            Edited();
            return item;
        }

        /// <summary>
        /// Takes one record that follows an entity out of it, as a VERTEX erased: its handle not given again and what points at
        /// it seen to on saving; every other record keeps its handle and bytes. The item reads as it was, erased.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the record or its entity is erased, or the record is the
        /// SEQEND, which the entity keeps.</exception>
        internal void RemoveFollower(DxfItem follower)
        {
            Editable(follower);
            DxfItem parent = follower.Parent;
            List<Rec> records = RecordsOf(parent);
            int index = follower.Index;

            if (records[index].Type == "SEQEND")
            {
                throw new InvalidOperationException("The SEQEND of a " + parent.Type + " ends its records, and is kept.");
            }

            List<DxfItem> followers = FollowersOf(parent);
            Detach(follower);
            Retire(records[index]);
            records.RemoveAt(index);
            SetRecords(parent, records);
            parent.Changed(records[0].Type);
            followers.RemoveAt(index);

            for (int i = index; i < followers.Count; i++)
            {
                if (followers[i] != null)
                {
                    followers[i].Index = i;
                }
            }

            Edited();
        }

        /// <summary>
        /// Sets groups of the own parts of several records of an entity in one writing of it, each record's as
        /// <see cref="SetValuesAt"/> sets one's: for a change to every vertex of a POLYLINE at the cost of one. Nothing is
        /// written where every value is as it was.
        /// </summary>
        internal void SetValuesAtMany(DxfItem item, IList<KeyValuePair<int, IList<KeyValuePair<int, string>>>> values, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            bool changed = false;

            foreach (KeyValuePair<int, IList<KeyValuePair<int, string>>> record in values)
            {
                Rec now = records[record.Key];

                foreach (KeyValuePair<int, string> value in record.Value)
                {
                    if (WithOwnGroup(now, value.Key, value.Value, order, out Rec next))
                    {
                        now = next;
                        changed = true;
                    }
                }

                records[record.Key] = now;
            }

            if (changed)
            {
                Rewritten(item, records, geometry);
            }
        }

        #endregion

        #region Groups of a record

        /// <summary>
        /// The value of the first group of a code in the part of a record its type has of its own: from its first subclass
        /// marker other than AcDbEntity, or from its start in a file of R12, outside the groups of the applications and before
        /// its extended data, as the file writes it; null where it has none.
        /// </summary>
        internal string ValueOf(DxfItem item, int code)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int at = OwnIndexOf(head, pairs, code);
            return at < 0 ? null : Text(head, pairs[at]);
        }

        /// <summary>
        /// The value of the first group of a code in the part all entities have, before the type's own part, outside the groups
        /// of the applications; null where it has none.
        /// </summary>
        internal string CommonValueOf(DxfItem item, int code)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            int body = BodyAt(head, pairs, data);
            int end = body >= 0 ? body : data;
            bool braced = false;

            for (int i = 1; i < end; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(head, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == code)
                {
                    return Text(head, pairs[i]);
                }
            }

            return null;
        }

        /// <summary>The index of the first group of a code in the type's own part of a record, as <see cref="ValueOf"/> reads it; -1 where it has none.</summary>
        private int OwnIndexOf(Rec record, List<Pair> pairs, int code)
        {
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            bool braced = false;

            for (int i = body >= 0 ? body + 1 : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == code)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Sets groups of the type's own part of a record, each the first of its code: its value in place, its code line and
        /// line end kept and a whole number padded as the old one was; a group the record has not put where the order of the
        /// type's groups puts it; null takes a group out. The record is written anew once, the extents grown to hold the
        /// entity's shapes where asked; nothing is written where every value is as it was.
        /// </summary>
        /// <param name="item">The entity or table record.</param>
        /// <param name="values">The codes and values, in the order they are set.</param>
        /// <param name="order">The codes of the type's own part in the order AutoCAD writes them, a 100 where a subclass marker stands.</param>
        /// <param name="geometry">Whether the extents grow to hold the entity's shapes.</param>
        internal void SetValues(DxfItem item, IList<KeyValuePair<int, string>> values, IReadOnlyList<int> order, bool geometry)
            => SetValuesAt(item, 0, values, order, geometry);

        /// <summary>Sets groups of the own part of one record of an entity, the head (0) or one that follows it, as <see cref="SetValues"/> sets the head's.</summary>
        internal void SetValuesAt(DxfItem item, int index, IList<KeyValuePair<int, string>> values, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec record = records[index];
            bool changed = false;

            foreach (KeyValuePair<int, string> value in values)
            {
                if (WithOwnGroup(record, value.Key, value.Value, order, out Rec next))
                {
                    record = next;
                    changed = true;
                }
            }

            if (changed)
            {
                records[index] = record;
                Rewritten(item, records, geometry);
            }
        }

        /// <summary>An entity's records written anew: its look and shapes read again, the extents grown where asked.</summary>
        private void Rewritten(DxfItem item, List<Rec> records, bool geometry)
        {
            SetRecords(item, records);
            item.Changed(records[0].Type);

            if (geometry)
            {
                GrowBy(item);
            }

            TableEdited(item);
        }

        /// <summary>How many records an entity is made of: its own, and a POLYLINE's vertices and SEQEND, an INSERT's attributes and SEQEND.</summary>
        internal int RecordCount(DxfItem item)
            => item.Parent != null ? 1 : item.Piece != null ? item.Piece.Records.Count : _slots?[item.First] == null ? item.Count : RecordsOf(item).Count;

        /// <summary>The type of one record of an entity: its own (0), or one that follows it, as VERTEX or SEQEND.</summary>
        internal string RecordType(DxfItem item, int index) => RecordAt(item, index).Type;

        /// <summary>
        /// The groups of the own part of one record of an entity, in their order: from its first subclass marker other than
        /// AcDbEntity (that marker and those after it among them), or from its start in a file of R12, outside the groups of the
        /// applications and before its extended data; comments left out.
        /// </summary>
        internal IReadOnlyList<DxfTypedValue> OwnGroupsOf(DxfItem item, int index)
        {
            Rec record = RecordAt(item, index);
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            var groups = new List<DxfTypedValue>();
            bool braced = false;

            for (int i = body >= 0 ? body : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code != 999)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, Text(record, pairs[i])));
                }
            }

            return groups.AsReadOnly();
        }

        /// <summary>
        /// Replaces every group of some codes in the own part of one record of an entity by the groups given, put where the
        /// first of those was, or where the order puts the first given where there was none; nothing is written where the
        /// groups given are those taken, code for code and text for text. For the runs of a record, as the vertices of a
        /// LWPOLYLINE or the knots of a SPLINE.
        /// </summary>
        internal void ReplaceOwn(DxfItem item, int index, ICollection<int> codes, IList<KeyValuePair<int, string>> groups, IReadOnlyList<int> order, bool geometry)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec record = records[index];
            List<Pair> pairs = PairsOf(record);
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            var taken = new List<int>();
            bool braced = false;

            for (int i = body >= 0 ? body + 1 : 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(record, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && codes.Contains(pairs[i].Code))
                {
                    taken.Add(i);
                }
            }

            if (taken.Count == groups.Count)
            {
                bool same = true;

                for (int k = 0; k < taken.Count && same; k++)
                {
                    same = pairs[taken[k]].Code == groups[k].Key && string.Equals(Text(record, pairs[taken[k]]), groups[k].Value, StringComparison.Ordinal);
                }

                if (same)
                {
                    return;
                }
            }

            int put = taken.Count > 0 ? taken[0] : groups.Count > 0 ? OwnPlaceOf(record, pairs, groups[0].Key, order) : pairs.Count;
            var dropped = new HashSet<int>(taken);
            var b = new Builder(this);

            for (int i = 0; i <= pairs.Count; i++)
            {
                if (i == put)
                {
                    foreach (KeyValuePair<int, string> group in groups)
                    {
                        b.Group(group.Key, group.Value);
                    }
                }

                if (i < pairs.Count && !dropped.Contains(i))
                {
                    b.Raw(record, pairs[i]);
                }
            }

            records[index] = b.Done(record.Type);
            Rewritten(item, records, geometry);
        }

        /// <summary>
        /// Writes the records that follow an entity anew, as the vertices of a POLYLINE: each of the type and own groups given,
        /// with a handle of its own where the file uses handles, the entity as its owner and the marker AcDbEntity from R13 on,
        /// the entity's space and layer, then its groups, their subclass markers left out in a file of R12; the entity's SEQEND
        /// kept, and the handles of the records put out not given again.
        /// </summary>
        internal void SetFollowers(DxfItem item, IList<KeyValuePair<string, IList<KeyValuePair<int, string>>>> followers)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            bool hasEnd = records.Count > 1 && records[records.Count - 1].Type == "SEQEND";
            Writing style = Style;
            var result = new List<Rec> { head };

            foreach (KeyValuePair<string, IList<KeyValuePair<int, string>>> follower in followers)
            {
                var groups = new List<DxfTypedValue>();

                foreach (KeyValuePair<int, string> group in follower.Value)
                {
                    if (group.Key != 100 || style.Markers)
                    {
                        groups.Add(DxfTypedValue.Read(group.Key, group.Value));
                    }
                }

                result.Add(Follower(new KeyValuePair<string, List<DxfTypedValue>>(follower.Key, groups), item.Handle, f => KeepSpaceAndLayer(f, head, pairs, data)));
            }

            for (int r = 1; r < records.Count; r++)
            {
                if (!(hasEnd && r == records.Count - 1))
                {
                    Retire(records[r]);
                }
            }

            if (hasEnd)
            {
                result.Add(records[records.Count - 1]);
            }

            RetireFollowers(item);
            Rewritten(item, result, true);
        }

        /// <summary>A record with the first group of a code of its own part given a value, as <see cref="SetValues"/> sets it; false where nothing changes.</summary>
        private bool WithOwnGroup(Rec record, int code, string value, IReadOnlyList<int> order, out Rec changed)
        {
            changed = record;
            List<Pair> pairs = PairsOf(record);
            int at = OwnIndexOf(record, pairs, code);
            int put = -1;

            if (at < 0)
            {
                if (value == null)
                {
                    return false;
                }

                put = OwnPlaceOf(record, pairs, code, order);
            }
            else if (value != null)
            {
                string was = Text(record, pairs[at]);

                if (string.Equals(was, value, StringComparison.Ordinal))
                {
                    return false;
                }

                // A whole number right-aligned, as AutoCAD writes its flags, stays so.
                if (IsWhole(code) && was.Length > value.Length && was.StartsWith(" ", StringComparison.Ordinal))
                {
                    value = value.PadLeft(was.Length);
                }
            }

            var b = new Builder(this);

            for (int i = 0; i < pairs.Count; i++)
            {
                if (i == put)
                {
                    b.Group(code, value);
                }

                if (i != at)
                {
                    b.Raw(record, pairs[i]);
                }
                else if (value != null)
                {
                    b.Raw(record, pairs[i].Start, pairs[i].ValueStart);
                    b.Value(value);
                    b.Raw(record, pairs[i].ValueEnd, pairs[i].End);
                }
            }

            if (put == pairs.Count)
            {
                b.Group(code, value);
            }

            changed = b.Done(record.Type);
            return true;
        }

        /// <summary>
        /// Where a group a record has not goes in its own part: after the last group of its subclass that comes before it in
        /// the order of the type's groups, else just after the marker of its subclass; in a record with no markers, after the
        /// last group that comes before it in the order; else, or for a code the order does not list, before the extended data.
        /// </summary>
        private int OwnPlaceOf(Rec record, List<Pair> pairs, int code, IReadOnlyList<int> order)
        {
            int data = DataAt(record, pairs);
            int body = BodyAt(record, pairs, data);
            int rank = -1;
            int segment = -1;
            int segments = 0;

            for (int k = 0; k < order.Count && rank < 0; k++)
            {
                if (order[k] == 100)
                {
                    segments++;
                }
                else if (order[k] == code)
                {
                    rank = k;
                    segment = Math.Max(0, segments - 1);
                }
            }

            if (rank < 0)
            {
                return data;
            }

            int after = -1;
            int marker = -1;
            int current = -1;
            bool braced = false;

            for (int i = body >= 0 ? body : 1; i < data; i++)
            {
                Pair pair = pairs[i];

                if (pair.Code == 102)
                {
                    braced = Text(record, pair).Trim().StartsWith("{", StringComparison.Ordinal);
                    continue;
                }

                if (braced)
                {
                    continue;
                }

                if (body >= 0 && pair.Code == 100)
                {
                    current++;

                    if (current == segment)
                    {
                        marker = i;
                    }

                    continue;
                }

                if (body >= 0 && current != segment)
                {
                    continue;
                }

                int their = RankOf(order, pair.Code);

                if (their >= 0 && their < rank)
                {
                    after = i;
                }
            }

            if (after >= 0)
            {
                return after + 1;
            }

            return marker >= 0 ? marker + 1 : data;
        }

        private static int RankOf(IReadOnlyList<int> order, int code)
        {
            for (int k = 0; k < order.Count; k++)
            {
                if (order[k] == code && code != 100)
                {
                    return k;
                }
            }

            return -1;
        }

        /// <summary>Sets a group of the part all entities have, where AutoCAD writes it, as the look setters set theirs; null takes it out.</summary>
        internal void SetCommon(DxfItem item, int code, string value)
        {
            Editable(item);
            SetGroup(item, code, value, false);
        }

        /// <summary>The handle of what owns a record: its first group 330 outside the groups of the applications; null where it has none.</summary>
        internal string OwnerOf(DxfItem item)
        {
            // A record that follows an entity, an ATTRIB, is owned by the entity from R13 on, whatever its 330 says.
            if (item.Parent != null && HasMarkers)
            {
                return item.Parent.Handle;
            }

            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            bool braced = false;

            for (int i = 1; i < data; i++)
            {
                if (pairs[i].Code == 102)
                {
                    braced = Text(head, pairs[i]).Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && pairs[i].Code == 330)
                {
                    return Text(head, pairs[i]).Trim();
                }
            }

            return null;
        }

        /// <summary>The handle of a record's extension dictionary: the 360 in its group {ACAD_XDICTIONARY; null where it has none.</summary>
        internal string ExtensionDictionaryOf(DxfItem item)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            bool inDictionary = false;

            for (int i = 1; i < pairs.Count; i++)
            {
                if (pairs[i].Code == 102)
                {
                    inDictionary = Text(head, pairs[i]).Trim() == "{ACAD_XDICTIONARY";
                }
                else if (inDictionary && pairs[i].Code == 360)
                {
                    return Text(head, pairs[i]).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// The extended data of a record: its groups from the first 1001 to its end, comments left out; the texts of the
        /// application's name (1001), a string (1000) and a layer's name (1003) with their escapes (<c>\U+00E9</c>) read as
        /// letters, as a layer's name is read.
        /// </summary>
        internal IReadOnlyList<DxfTypedValue> XDataOf(DxfItem item)
        {
            Rec head = HeadOf(item);
            List<Pair> pairs = PairsOf(head);
            var groups = new List<DxfTypedValue>();

            for (int i = DataAt(head, pairs); i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    groups.Add(DxfTypedValue.Read(pairs[i].Code, DataText(head, pairs[i])));
                }
            }

            return groups.AsReadOnly();
        }

        /// <summary>The value of a group of extended data as it is read: a text with its escapes read as letters.</summary>
        private string DataText(Rec record, Pair pair)
        {
            string text = Text(record, pair);
            return pair.Code == 1000 || pair.Code == 1001 || pair.Code == 1003 ? DxfReader.Decode(text) : text;
        }

        /// <summary>
        /// Gives a record the extended data given in the place of its own, each application named added to the table of
        /// applications where it is not listed; nothing is written where the data is as it was read, and a group read as it is
        /// given keeps its bytes.
        /// </summary>
        internal void SetXData(DxfItem item, IReadOnlyList<DxfTypedValue> groups)
        {
            Editable(item);
            List<Rec> records = RecordsOf(item);
            Rec head = records[0];
            List<Pair> pairs = PairsOf(head);
            int data = DataAt(head, pairs);
            var existing = new List<Pair>();

            for (int i = data; i < pairs.Count; i++)
            {
                if (pairs[i].Code != 999)
                {
                    existing.Add(pairs[i]);
                }
            }

            bool same = existing.Count == groups.Count;

            for (int k = 0; k < existing.Count && same; k++)
            {
                same = existing[k].Code == groups[k].TypeCode && string.Equals(DataText(head, existing[k]), groups[k].Value, StringComparison.Ordinal);
            }

            if (same)
            {
                return;
            }

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 1001)
                {
                    EnsureApplication(group.Value.Trim());
                }
            }

            var b = new Builder(this);

            for (int i = 0; i < data; i++)
            {
                b.Raw(head, pairs[i]);
            }

            for (int k = 0; k < groups.Count; k++)
            {
                DxfTypedValue group = groups[k];

                if (k < existing.Count && existing[k].Code == group.TypeCode && string.Equals(DataText(head, existing[k]), group.Value, StringComparison.Ordinal))
                {
                    b.Raw(head, existing[k]);
                }
                else
                {
                    b.Group(group.TypeCode, group.Value);
                }
            }

            records[0] = b.Done(head.Type);
            SetRecords(item, records);
            item.Changed(head.Type);
            Edited();
        }

        #endregion

        #region Tables

        /// <summary>
        /// Adds an application to the table of applications (APPID) where it does not list it, letters of either case the
        /// same, as AutoCAD registers one before its extended data is written: a plain APPID record before the table's ENDTAB.
        /// </summary>
        private void EnsureApplication(string name)
        {
            if (_detached)
            {
                return;
            }

            foreach (Rec record in TableRecords("APPID"))
            {
                if (string.Equals(DxfReader.NameOf(Record(record).Text(2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            if (AddTableRecord("APPID", "AcDbRegAppTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, "0");
            }) != null)
            {
                GeometryHelperLog.Info("DxfFile: the application " + name + " was not in the table of applications, so an APPID record was added for it.");
            }
        }

        /// <summary>
        /// Adds a record to a table, before its ENDTAB: its type, a handle of its own where the file uses handles, the table as
        /// its owner and the markers of a table record from R13 on, then the groups given; the table's count raised by one.
        /// Null where the file has no such table.
        /// </summary>
        internal DxfItem AddTableRecord(string table, string marker, Action<Builder> groups)
        {
            int head = -1;
            int end = -1;

            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "TABLES" && i < section.Last && end < 0; i++)
                {
                    if (head < 0 && _types[i] == "TABLE" && string.Equals(DxfReader.NameOf(Record(Original(i)).Text(2) ?? string.Empty), table, StringComparison.OrdinalIgnoreCase))
                    {
                        head = i;
                    }
                    else if (head >= 0 && _types[i] == "ENDTAB")
                    {
                        end = i;
                    }
                }
            }

            if (end < 0)
            {
                GeometryHelperLog.Debug("DxfFile: the file has no " + table + " table to add a record to.");
                return null;
            }

            Writing style = Style;
            Rec header = Current(head, head + 1)[0];
            string tableHandle = Record(header).Text(5)?.Trim();
            var b = new Builder(this);
            b.Group(0, table);

            if (UsesHandles())
            {
                b.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (!string.IsNullOrEmpty(tableHandle))
                {
                    b.Group(330, tableHandle);
                }

                b.Group(100, "AcDbSymbolTableRecord");
                b.Group(100, marker);
            }

            groups(b);

            var piece = new Piece(new List<Rec> { b.Done(table) });
            List<Piece> home = PutBefore(end, piece);
            List<Pair> pairs = PairsOf(header);
            int count = FirstWhole(header, pairs, 70);

            if (WithGroup(header, 70, (count + 1).ToString(CultureInfo.InvariantCulture), false, out Rec counted))
            {
                Put(head, new List<Piece> { new Piece(new List<Rec> { counted }) });
            }

            return Listed(table, piece, home);
        }

        private Dictionary<string, List<DxfItem>> _tableItems;

        /// <summary>
        /// The records of a table, in the order of the file, those added after them: each the same object every time, so that
        /// the editing model has one object for each.
        /// </summary>
        internal List<DxfItem> TableItems(string table)
        {
            if (_tableItems == null)
            {
                _tableItems = new Dictionary<string, List<DxfItem>>(StringComparer.Ordinal);
            }

            if (_tableItems.TryGetValue(table, out List<DxfItem> items))
            {
                return items;
            }

            items = new List<DxfItem>();

            foreach (Section section in _sections)
            {
                if (section.Name != "TABLES")
                {
                    continue;
                }

                for (int i = section.First + 1; i < section.Last; i++)
                {
                    if (_types[i] == table)
                    {
                        items.Add(new DxfItem(this, i, 1, table, null));
                    }
                }
            }

            // Those added before the list was first asked for, by an entity set on a layer not listed.
            if (_added != null && _added.TryGetValue(table, out List<DxfItem> added))
            {
                items.AddRange(added);
            }

            _tableItems.Add(table, items);
            return items;
        }

        private Dictionary<string, List<DxfItem>> _added;

        /// <summary>A record added to a table, listed with its table's records.</summary>
        private DxfItem Listed(string table, Piece piece, List<Piece> home)
        {
            var item = new DxfItem(this, piece, table) { Home = home };

            if (_tableItems != null && _tableItems.TryGetValue(table, out List<DxfItem> items))
            {
                items.Add(item);
            }
            else
            {
                if (_added == null)
                {
                    _added = new Dictionary<string, List<DxfItem>>(StringComparer.Ordinal);
                }

                if (!_added.TryGetValue(table, out List<DxfItem> added))
                {
                    added = new List<DxfItem>();
                    _added.Add(table, added);
                }

                added.Add(item);
            }

            if (table == "LAYER")
            {
                _layers = NewLayers();
                LayerNames().Add(DxfReader.NameOf(ValueOf(item, 2) ?? string.Empty));
            }
            else if (table == "LTYPE")
            {
                _linetypes = null;
            }

            return item;
        }

        private HashSet<string> _layerNames;
        private Rec? _zeroLayer;
        private bool _zeroLayerKnown;

        /// <summary>
        /// The names of the layers of the layer table, letters of either case the same, read once and kept as layers are
        /// added, so that a layer is looked up in it at no cost.
        /// </summary>
        private HashSet<string> LayerNames()
        {
            if (_layerNames == null)
            {
                _layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfLayer layer in LayerTable)
                {
                    _layerNames.Add(layer.Name);
                }
            }

            return _layerNames;
        }

        /// <summary>Writes the lineweight (where asked) and the plot style of layer 0 into a new LAYER, as the file has them; found once.</summary>
        private void ZeroLayerLook(Builder b, bool weight)
        {
            if (!_zeroLayerKnown)
            {
                _zeroLayerKnown = true;

                foreach (Rec record in TableRecords("LAYER"))
                {
                    if (DxfReader.NameOf(Record(record).Text(2) ?? string.Empty) == "0")
                    {
                        _zeroLayer = record;
                        break;
                    }
                }
            }

            if (_zeroLayer == null)
            {
                return;
            }

            Rec zero = _zeroLayer.Value;

            foreach (Pair pair in PairsOf(zero))
            {
                if ((weight && pair.Code == 370) || pair.Code == 390)
                {
                    b.Raw(zero, pair);
                }
            }
        }

        /// <summary>What a change to a table record leaves to be read again: the layers, the linetypes and the placing.</summary>
        internal void TableEdited(DxfItem item)
        {
            if (item.Type == "LAYER")
            {
                _layers = NewLayers();
            }
            else if (item.Type == "LTYPE")
            {
                _linetypes = null;
            }

            Edited();
        }

        /// <summary>The TABLE record of a table, which holds its handle; null where the file has no such table.</summary>
        internal DxfItem TableItem(string table)
        {
            foreach (Section section in _sections)
            {
                for (int i = section.First + 1; section.Name == "TABLES" && i < section.Last; i++)
                {
                    if (_types[i] == "TABLE" && string.Equals(DxfReader.NameOf(Record(Original(i)).Text(2) ?? string.Empty), table, StringComparison.OrdinalIgnoreCase))
                    {
                        return new DxfItem(this, i, 1, "TABLE", null);
                    }
                }
            }

            return null;
        }

        /// <summary>Adds a layer to the layer table where it does not list it, as an entity set on it does.</summary>
        internal void LayerNeeded(string name) => EnsureLayer(name);

        /// <summary>Whether a linetype is in the linetype table, letters of either case the same, or is BYLAYER or BYBLOCK.</summary>
        internal bool LinetypeListed(string name) => HasLinetype(name);

        /// <summary>Whether the file's version is that of AutoCAD's number given or later: 1015 for R2000.</summary>
        internal bool Supports(int version) => Style.Version >= version;

        /// <summary>Refuses what the file's version has not: a group or variable that came with a later one, as R2000 (AC1015) brought lineweights.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the file is older than the version given.</exception>
        internal void Requires(int version, string what, string since)
        {
            if (!Supports(version))
            {
                throw new InvalidOperationException("A DXF " + VersionSaid() + " has no " + what + ", which came with " + since + ".");
            }
        }

        /// <summary>The drawing the file is opened as; null for a file that holds entities not yet in one.</summary>
        internal DxfDatabase Database { get; set; }

        /// <summary>
        /// Adds a LAYER record of a layer made in code: its name, flags, colour (below nought where it is off), true colour,
        /// linetype, plotting, lineweight and the plot style of layer 0, each where the file's version has it; null where the
        /// file has no layer table.
        /// </summary>
        internal DxfItem AddLayer(string name, int colour, int? trueColour, string linetype, int lineweight, int flags, bool plotted)
        {
            Writing style = Style;
            string spelled = Linetypes().Find(l => string.Equals(DxfReader.NameOf(l), DxfReader.NameOf(linetype), StringComparison.OrdinalIgnoreCase)) ?? linetype;

            DxfItem item = AddTableRecord("LAYER", "AcDbLayerTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, flags.ToString(CultureInfo.InvariantCulture));
                b.Group(62, colour.ToString(CultureInfo.InvariantCulture));

                if (trueColour.HasValue)
                {
                    b.Group(420, trueColour.Value.ToString(CultureInfo.InvariantCulture));
                }

                b.Group(6, spelled);

                if (style.Lineweights)
                {
                    if (!plotted)
                    {
                        b.Group(290, "0");
                    }

                    b.Group(370, lineweight.ToString(CultureInfo.InvariantCulture));

                    // The plot style of layer 0, which a layer of R2000 on has.
                    ZeroLayerLook(b, false);
                }
            });

            if (item != null)
            {
                Edited();
            }

            return item;
        }

        /// <summary>
        /// Adds an LTYPE record of a linetype made in code: its name, description, the alignment A, its count of dashes, the
        /// length of its pattern, then each dash (49), a simple one (74 of 0) from R13 on; null where the file has no linetype
        /// table.
        /// </summary>
        internal DxfItem AddLinetype(string name, string description, double length, double[] dashes)
        {
            Writing style = Style;

            return AddTableRecord("LTYPE", "AcDbLinetypeTableRecord", b =>
            {
                b.Group(2, name);
                b.Group(70, "0");
                b.Group(3, description);
                b.Group(72, "65");
                b.Group(73, dashes.Length.ToString(CultureInfo.InvariantCulture));
                b.Group(40, Numbers.Exact(length + 0.0));

                foreach (double dash in dashes)
                {
                    b.Group(49, Numbers.Exact(dash + 0.0));

                    if (style.Markers)
                    {
                        b.Group(74, "0");
                    }
                }
            });
        }

        /// <summary>
        /// Adds a block made in code, as AutoCAD writes one: from R13 on its BLOCK_RECORD in the block table, then its BLOCK and
        /// ENDBLK at the end of the BLOCKS section, owned by that record, on layer 0, with handles where the file uses them.
        /// </summary>
        internal DxfFileBlock AddBlock(string name, Geometry.GeoPoint3 origin, out DxfItem record)
        {
            int end = -1;

            foreach (Section section in _sections)
            {
                if (section.Name == "BLOCKS")
                {
                    end = section.Last;
                }
            }

            if (end < 0)
            {
                throw new InvalidOperationException("The file has no BLOCKS section to add the block " + name + " to.");
            }

            Writing style = Style;
            record = null;

            if (style.Markers)
            {
                record = AddTableRecord("BLOCK_RECORD", "AcDbBlockTableRecord", b =>
                {
                    b.Group(2, name);
                    b.Group(340, "0");

                    if (style.Version >= 1021)
                    {
                        b.Group(70, "0");
                        b.Group(280, "1");
                        b.Group(281, "0");
                    }
                });
            }

            string owner = record?.Handle;
            var head = new Builder(this);
            head.Group(0, "BLOCK");

            if (UsesHandles())
            {
                head.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (owner != null)
                {
                    head.Group(330, owner);
                }

                head.Group(100, "AcDbEntity");
            }

            head.Group(8, "0");

            if (style.Markers)
            {
                head.Group(100, "AcDbBlockBegin");
            }

            head.Group(2, name);
            head.Group(70, "0");
            head.Group(10, Numbers.Exact(origin.X + 0.0));
            head.Group(20, Numbers.Exact(origin.Y + 0.0));
            head.Group(30, Numbers.Exact(origin.Z + 0.0));
            head.Group(3, name);

            if (style.Markers)
            {
                head.Group(1, string.Empty);
            }

            var tail = new Builder(this);
            tail.Group(0, "ENDBLK");

            if (UsesHandles())
            {
                tail.Group(5, NewHandle());
            }

            if (style.Markers)
            {
                if (owner != null)
                {
                    tail.Group(330, owner);
                }

                tail.Group(100, "AcDbEntity");
            }

            tail.Group(8, "0");

            if (style.Markers)
            {
                tail.Group(100, "AcDbBlockEnd");
            }

            var headPiece = new Piece(new List<Rec> { head.Done("BLOCK") });
            var endPiece = new Piece(new List<Rec> { tail.Done("ENDBLK") });
            List<Piece> home = PutBefore(end, headPiece);
            PutBefore(end, endPiece);
            var headItem = new DxfItem(this, headPiece, "BLOCK") { Home = home };
            var block = new DxfFileBlock(this, name, origin, 0, new List<DxfItem>(), headItem, -1) { EndPiece = endPiece, Home = home };
            Edited();
            GeometryHelperLog.Info("DxfFile: the block " + name + " was added.");
            return block;
        }

        #endregion

        #region The header

        /// <summary>The groups of a variable of the header as the file would be saved; null where it has none.</summary>
        internal IReadOnlyList<DxfTypedValue> HeaderOf(string name)
        {
            FlushHeader();
            return _header.TryGetValue(name, out IReadOnlyList<DxfTypedValue> groups) ? groups : null;
        }

        /// <summary>
        /// Gives a variable of the header its groups, where it is last given: each value in place where the codes are as they
        /// were, else its groups written anew; at the end of the header where it is not given. Nothing is written where the
        /// groups are as they were.
        /// </summary>
        internal void SetHeader(string name, IReadOnlyList<DxfTypedValue> groups)
        {
            FlushHeader();
            int at = -1;
            int nine = -1;
            int last = -1;

            foreach (Section section in _sections)
            {
                for (int i = section.First; section.Name == "HEADER" && i < section.Last; i++)
                {
                    List<Rec> now = Current(i, i + 1);

                    if (now.Count != 1)
                    {
                        continue;
                    }

                    last = i;
                    List<Pair> found = PairsOf(now[0]);

                    for (int p = 1; p < found.Count; p++)
                    {
                        if (found[p].Code == 9 && Text(now[0], found[p]).Trim() == name)
                        {
                            at = i;
                            nine = p;
                        }
                    }
                }
            }

            if (last < 0)
            {
                throw new InvalidOperationException("The file has no HEADER section to set " + name + " in.");
            }

            Rec record = Current(at >= 0 ? at : last, (at >= 0 ? at : last) + 1)[0];
            List<Pair> pairs = PairsOf(record);
            var b = new Builder(this);

            if (at < 0)
            {
                foreach (Pair pair in pairs)
                {
                    b.Raw(record, pair);
                }

                b.Group(9, name);

                foreach (DxfTypedValue group in groups)
                {
                    b.Group(group.TypeCode, group.Value);
                }

                at = last;
            }
            else
            {
                int end = nine + 1;

                while (end < pairs.Count && pairs[end].Code != 9)
                {
                    end++;
                }

                bool sameCodes = end - nine - 1 == groups.Count;

                for (int k = 0; sameCodes && k < groups.Count; k++)
                {
                    sameCodes = pairs[nine + 1 + k].Code == groups[k].TypeCode;
                }

                bool changed = false;

                for (int p = 0; p < pairs.Count; p++)
                {
                    if (p <= nine || p >= end)
                    {
                        b.Raw(record, pairs[p]);
                        continue;
                    }

                    if (!sameCodes)
                    {
                        if (p == nine + 1)
                        {
                            foreach (DxfTypedValue group in groups)
                            {
                                b.Group(group.TypeCode, group.Value);
                            }

                            changed = true;
                        }

                        continue;
                    }

                    string value = groups[p - nine - 1].Value;

                    if (string.Equals(Text(record, pairs[p]), value, StringComparison.Ordinal))
                    {
                        b.Raw(record, pairs[p]);
                        continue;
                    }

                    b.Raw(record, pairs[p].Start, pairs[p].ValueStart);
                    b.Value(value);
                    b.Raw(record, pairs[p].ValueEnd, pairs[p].End);
                    changed = true;
                }

                if (!changed && sameCodes)
                {
                    return;
                }

                if (!changed)
                {
                    foreach (DxfTypedValue group in groups)
                    {
                        b.Group(group.TypeCode, group.Value);
                    }
                }
            }

            Put(at, new List<Piece> { new Piece(new List<Rec> { b.Done(record.Type) }) });
            ReadHeader();
        }

        #endregion
    }
}
