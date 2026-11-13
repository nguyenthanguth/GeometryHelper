using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Reads the ACIS data of a REGION, as SAT text or as SAB bytes, into the planar body it holds: the faces on planes, their
    /// loops of lines and arcs of ellipses. Anything else (a face on a cone or a spline, an edge that is a spline) is not read:
    /// the data gives no body.
    /// </summary>
    internal static class AcisReader
    {
        /// <summary>A value of a record, the same whether it came from SAT or from SAB: a pointer, a number, or a flag.</summary>
        private readonly struct Token
        {
            internal Token(char kind, double number, string text)
            {
                Kind = kind;
                Number = number;
                Text = text;
            }

            /// <summary>p a pointer, d a number, b a flag (nought the first word, 1 the second; for a bound 1 where it is finite), s a text.</summary>
            internal char Kind { get; }

            internal double Number { get; }

            internal string Text { get; }
        }

        private sealed class Record
        {
            internal string Name;
            internal readonly List<Token> Tokens = new List<Token>();
        }

        /// <summary>The body in SAT text, its lines as AutoCAD keeps them in DXF with the cipher undone; null where it holds no planar body.</summary>
        internal static AcisBody FromSat(IReadOnlyList<string> lines)
        {
            List<Record> records = SatRecords(lines);
            return records == null ? null : Body(records);
        }

        /// <summary>
        /// The wires of the body in SAT text, as <see cref="FromSat"/> takes its lines: every use of an edge by a face, whatever
        /// the face lies on; null where the data holds no body or is not read.
        /// </summary>
        internal static AcisWires WiresFromSat(IReadOnlyList<string> lines)
        {
            List<Record> records = SatRecords(lines);

            try
            {
                return records == null ? null : Wires(records);
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static List<Record> SatRecords(IReadOnlyList<string> lines)
        {
            if (lines.Count < 4)
            {
                return null;
            }

            var records = new List<Record>();
            var text = new StringBuilder();

            for (int i = 3; i < lines.Count; i++)
            {
                text.Append(lines[i]).Append(' ');
            }

            foreach (string piece in text.ToString().Split('#'))
            {
                Record record = SatRecord(piece);

                if (record != null)
                {
                    records.Add(record);
                }
            }

            return records;
        }

        private static Record SatRecord(string piece)
        {
            string trimmed = piece.Trim();

            if (trimmed.Length == 0)
            {
                return null;
            }

            var record = new Record();
            int at = 0;
            record.Name = Word(trimmed, ref at);

            while (at < trimmed.Length)
            {
                string word = Word(trimmed, ref at);

                if (word.Length == 0)
                {
                    break;
                }

                if (
                    word[0] == '$'
                    && int.TryParse(
                        word.Substring(1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int pointer
                    )
                )
                {
                    record.Tokens.Add(new Token('p', pointer, null));
                }
                else if (
                    word[0] == '@'
                    && int.TryParse(
                        word.Substring(1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int length
                    )
                )
                {
                    // A text: its length, then that many letters after one space.
                    int start = Math.Min(at + 1, trimmed.Length);
                    int count = Math.Min(length, trimmed.Length - start);
                    record.Tokens.Add(new Token('s', 0.0, trimmed.Substring(start, count)));
                    at = start + count;
                }
                else if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    record.Tokens.Add(new Token('d', number, null));
                }
                else
                {
                    switch (word)
                    {
                        case "forward":
                        case "forward_v":
                        case "single":
                        case "out":
                        case "I":
                            record.Tokens.Add(new Token('b', 0.0, null));
                            break;
                        case "reversed":
                        case "reverse_v":
                        case "double":
                        case "in":
                        case "F":
                            record.Tokens.Add(new Token('b', 1.0, null));
                            break;
                        default:
                            record.Tokens.Add(new Token('s', 0.0, word));
                            break;
                    }
                }
            }

            return record;
        }

        private static string Word(string text, ref int at)
        {
            while (at < text.Length && text[at] == ' ')
            {
                at++;
            }

            int start = at;

            while (at < text.Length && text[at] != ' ')
            {
                at++;
            }

            return text.Substring(start, at - start);
        }

        /// <summary>The body in SAB bytes; null where they hold no planar body or are not SAB as AutoCAD writes it.</summary>
        internal static AcisBody FromSab(byte[] sab)
        {
            try
            {
                return Body(SabRecords(sab));
            }
            catch (Exception e)
                when (e is FormatException
                    || e is ArgumentException
                    || e is IndexOutOfRangeException
                    || e is NotSupportedException
                )
            {
                return null;
            }
        }

        /// <summary>The wires of the body in SAB bytes, as <see cref="WiresFromSat"/> gives them; null where the bytes are not SAB as AutoCAD writes it.</summary>
        internal static AcisWires WiresFromSab(byte[] sab)
        {
            try
            {
                return Wires(SabRecords(sab, true));
            }
            catch (Exception e)
                when (e is FormatException
                    || e is ArgumentException
                    || e is IndexOutOfRangeException
                    || e is NotSupportedException
                )
            {
                return null;
            }
        }

        private static List<Record> SabRecords(byte[] sab, bool subtypes = false)
        {
            var records = new List<Record>();
            int at = 15 + 16;

            string magic = sab == null || sab.Length < at ? null : Encoding.ASCII.GetString(sab, 0, 15);

            if (magic != "ACIS BinaryFile" && magic != "ASM BinaryFile4")
            {
                throw new FormatException("Not SAB.");
            }

            var name = new StringBuilder();
            Record record = new Record();
            bool named = false;
            bool head = true;

            while (at < sab.Length)
            {
                byte tag = sab[at++];

                switch (tag)
                {
                    case 0x0E:
                    case 0x0D:
                    {
                        int length = sab[at++];
                        string part = Encoding.ASCII.GetString(sab, at, length);
                        at += length;
                        name.Append(part);

                        if (tag == 0x0E)
                        {
                            name.Append('-');
                        }
                        else
                        {
                            record.Name = name.ToString();
                            name.Clear();
                            named = true;
                            head = false;
                        }

                        break;
                    }
                    case 0x11:
                        if (named)
                        {
                            records.Add(record);
                        }

                        record = new Record();
                        named = false;
                        break;
                    case 0x04:
                    case 0x15:
                    case 0x03:
                    case 0x02:
                    case 0x0C:
                    {
                        int size =
                            tag == 0x02 ? 1
                            : tag == 0x03 ? 2
                            : 4;
                        long value =
                            size == 1 ? sab[at]
                            : size == 2 ? BitConverter.ToInt16(sab, at)
                            : BitConverter.ToInt32(sab, at);
                        at += size;

                        if (!head)
                        {
                            record.Tokens.Add(tag == 0x0C ? new Token('p', value, null) : new Token('d', value, null));
                        }

                        break;
                    }
                    case 0x06:
                        if (!head)
                        {
                            record.Tokens.Add(new Token('d', BitConverter.ToDouble(sab, at), null));
                        }

                        at += 8;
                        break;
                    case 0x13:
                    case 0x14:
                        for (int i = 0; i < 3; i++)
                        {
                            record.Tokens.Add(new Token('d', BitConverter.ToDouble(sab, at + 8 * i), null));
                        }

                        at += 24;
                        break;
                    case 0x0A:
                    case 0x0B:
                        record.Tokens.Add(new Token('b', tag == 0x0A ? 1.0 : 0.0, null));
                        break;
                    case 0x07:
                    case 0x08:
                    case 0x09:
                    {
                        int lengthBytes =
                            tag == 0x07 ? 1
                            : tag == 0x08 ? 2
                            : 4;
                        int length =
                            lengthBytes == 1 ? sab[at]
                            : lengthBytes == 2 ? BitConverter.ToUInt16(sab, at)
                            : (int)BitConverter.ToUInt32(sab, at);
                        at += lengthBytes + length;

                        if (!head)
                        {
                            record.Tokens.Add(new Token('s', 0.0, Encoding.ASCII.GetString(sab, at - length, length)));
                        }

                        break;
                    }
                    case 0x0F:
                    case 0x10:
                        // The braces of a spline's data stand inside one record: the wires of a body, which reads only
                        // the records of its edges, go past them.
                        if (!subtypes)
                        {
                            throw new FormatException("A subtype: not a planar body.");
                        }

                        break;
                    default:
                        throw new FormatException("A tag not known.");
                }
            }

            return records;
        }

        // The own fields of a record start after its attribute pointer and, from R2004, its history number and pointer.
        private static int Own(bool history) => history ? 3 : 1;

        private static AcisBody Body(List<Record> records)
        {
            bool history = records.Count > 0 && records[0].Name == "asmheader";
            int o = Own(history);

            Record At(Token pointer) =>
                pointer.Kind == 'p' && pointer.Number >= 0 && pointer.Number < records.Count
                    ? records[(int)pointer.Number]
                    : null;
            Token Field(Record record, int index) =>
                index < record.Tokens.Count ? record.Tokens[index] : default(Token);

            var faces = new List<AcisFace>();
            Record body = records.Find(r => r.Name == "body");

            if (body == null)
            {
                return null;
            }

            try
            {
                for (Record lump = At(Field(body, o)); lump != null; lump = At(Field(lump, o)))
                {
                    for (Record shell = At(Field(lump, o + 1)); shell != null; shell = At(Field(shell, o)))
                    {
                        for (Record face = At(Field(shell, o + 2)); face != null; face = At(Field(face, o)))
                        {
                            AcisFace read = Face(face, o, history, At, Field);

                            if (read == null)
                            {
                                return null;
                            }

                            faces.Add(read);
                        }
                    }
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException)
            {
                return null;
            }

            if (faces.Count == 0)
            {
                return null;
            }

            // AutoCAD moves and turns a body by a transform record it keeps beside the geometry (a scale it writes into the geometry);
            // the record has no history pointer, so its own fields start one place sooner than the others'.
            Record transform = At(Field(body, o + 2));
            return transform != null && transform.Name == "transform"
                ? Moved(new AcisBody(faces), transform, history ? 2 : 1)
                : new AcisBody(faces);
        }

        /// <summary>
        /// Every use of an edge by a face of the body, in the order the data lists them (the lumps, shells, faces, loops and
        /// coedges, each by the pointer to the next): a straight edge or an arc of an ellipse as the edge itself runs, a vertex
        /// where an edge has no curve, nothing for a coedge of any other curve, which is counted. Faces of any surface give
        /// theirs; only a flat face gives its normal.
        /// </summary>
        private static AcisWires Wires(List<Record> records)
        {
            bool history = records.Count > 0 && records[0].Name == "asmheader";
            int o = Own(history);

            Record At(Token pointer) =>
                pointer.Kind == 'p' && pointer.Number >= 0 && pointer.Number < records.Count
                    ? records[(int)pointer.Number]
                    : null;
            Token Field(Record record, int index) =>
                index < record.Tokens.Count ? record.Tokens[index] : default(Token);

            Record body = records.Find(r => r.Name == "body");

            if (body == null)
            {
                return null;
            }

            var items = new List<AcisWire>();
            int skipped = 0;

            for (Record lump = At(Field(body, o)); lump != null; lump = At(Field(lump, o)))
            {
                for (Record shell = At(Field(lump, o + 1)); shell != null; shell = At(Field(shell, o)))
                {
                    for (Record face = At(Field(shell, o + 2)); face != null; face = At(Field(face, o)))
                    {
                        Record surface = At(Field(face, o + 4));
                        bool planar = surface != null && surface.Name == "plane-surface";
                        AcisVec normal = default(AcisVec);

                        if (planar)
                        {
                            normal = Vec(surface, o + 3, Field).Unit;

                            if (Field(face, o + 5).Number != 0.0)
                            {
                                normal = -normal;
                            }
                        }

                        for (Record loop = At(Field(face, o + 1)); loop != null; loop = At(Field(loop, o)))
                        {
                            Record first = At(Field(loop, o + 1));
                            Record coedge = first;
                            int guard = 0;

                            while (coedge != null && guard++ < 1000000)
                            {
                                Record edge = At(Field(coedge, o + 3));
                                Record curve = edge == null ? null : At(Field(edge, history ? o + 5 : o + 3));
                                AcisWire wire = null;

                                try
                                {
                                    if (edge == null)
                                    {
                                        skipped++;
                                    }
                                    else if (curve == null)
                                    {
                                        // An edge of no curve is its vertex alone, as the apex of a cone.
                                        Record vertex = At(Field(edge, o));
                                        wire =
                                            vertex == null
                                                ? null
                                                : new AcisWire(null, PointOf(vertex, o, At, Field), planar, normal);
                                        skipped += wire == null ? 1 : 0;
                                    }
                                    else if (curve.Name == "straight-curve" || curve.Name == "ellipse-curve")
                                    {
                                        wire = new AcisWire(
                                            Edge(coedge, o, history, At, Field, false),
                                            default(AcisVec),
                                            planar,
                                            normal
                                        );
                                    }
                                    else
                                    {
                                        skipped++;
                                    }
                                }
                                catch (InvalidOperationException)
                                {
                                    skipped++;
                                }

                                if (wire != null)
                                {
                                    items.Add(wire);
                                }

                                coedge = At(Field(coedge, o));

                                if (coedge == first)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            var wires = new AcisWires(items, skipped);
            Record transform = At(Field(body, o + 2));

            if (transform == null || transform.Name != "transform")
            {
                return wires;
            }

            return TryTransform(transform, history ? 2 : 1, out GeoTransform3 moved)
                ? moved == null
                    ? wires
                    : wires.Transformed(moved)
                : null;
        }

        /// <summary>
        /// The body with the transform record of its body applied: a matrix of three rows (a point is a row, so its image is the
        /// point times the matrix), a translation and a scale, as ACIS keeps them. The identity leaves the body as it is; a body
        /// with a shear is not read.
        /// </summary>
        private static AcisBody Moved(AcisBody body, Record transform, int o)
        {
            if (!TryTransform(transform, o, out GeoTransform3 moved))
            {
                return null;
            }

            return moved == null ? body : body.Transformed(moved);
        }

        /// <summary>
        /// The transformation a transform record holds: null for the identity, which leaves a body as it is; false where the
        /// record shears.
        /// </summary>
        private static bool TryTransform(Record transform, int o, out GeoTransform3 moved)
        {
            moved = null;
            double Number(int index) => transform.Tokens.Count > o + index ? transform.Tokens[o + index].Number : 0.0;

            double scale = Number(12);
            bool identity = Math.Abs(scale - 1.0) < 1E-15;

            for (int i = 0; i < 12 && identity; i++)
            {
                identity = Math.Abs(Number(i) - (i < 9 && i % 4 == 0 ? 1.0 : 0.0)) < 1E-15;
            }

            if (identity)
            {
                return true;
            }

            // The flags that follow: rotate, reflect, shear. A shear is a transformation that scales more one way than another.
            Token shear = transform.Tokens.Count > o + 15 ? transform.Tokens[o + 15] : default(Token);

            if (shear.Kind == 's' ? shear.Text == "shear" : shear.Number != 0.0)
            {
                return false;
            }

            var matrix = new double[4, 4];

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    matrix[r, c] = scale * Number(c * 3 + r);
                }

                matrix[r, 3] = Number(9 + r);
            }

            matrix[3, 3] = 1.0;
            moved = new GeoTransform3(matrix);
            return true;
        }

        private static AcisFace Face(
            Record face,
            int o,
            bool history,
            Func<Token, Record> at,
            Func<Record, int, Token> field
        )
        {
            Record surface = at(field(face, o + 4));

            if (surface == null || surface.Name != "plane-surface")
            {
                return null;
            }

            var origin = Vec(surface, o, field);
            AcisVec normal = Vec(surface, o + 3, field).Unit;

            if (field(face, o + 5).Number != 0.0)
            {
                normal = -normal;
            }

            var loops = new List<AcisLoop>();

            for (Record loop = at(field(face, o + 1)); loop != null; loop = at(field(loop, o)))
            {
                var curves = new List<AcisCurve>();
                Record first = at(field(loop, o + 1));
                Record coedge = first;

                do
                {
                    AcisCurve curve = Edge(coedge, o, history, at, field);

                    if (curve == null)
                    {
                        return null;
                    }

                    curves.Add(curve);
                    coedge = at(field(coedge, o));

                    if (curves.Count > 1000000)
                    {
                        return null;
                    }
                } while (coedge != null && coedge != first);

                AcisVec turn = default(AcisVec);

                foreach (AcisCurve curve in curves)
                {
                    turn += curve.AreaVector();
                }

                loops.Add(new AcisLoop(curves, turn.Dot(normal) < 0.0));
            }

            return new AcisFace(origin, normal, loops);
        }

        private static AcisVec Vec(Record record, int index, Func<Record, int, Token> field) =>
            new AcisVec(field(record, index).Number, field(record, index + 1).Number, field(record, index + 2).Number);

        /// <summary>
        /// The edge of a coedge as it runs along the loop: its curve between its parameters, turned over where the coedge runs
        /// against the edge; as the edge itself runs where <paramref name="alongLoop"/> is false.
        /// </summary>
        private static AcisCurve Edge(
            Record coedge,
            int o,
            bool history,
            Func<Token, Record> at,
            Func<Record, int, Token> field,
            bool alongLoop = true
        )
        {
            Record edge = at(field(coedge, o + 3));

            if (edge == null)
            {
                throw new InvalidOperationException();
            }

            Record startVertex = at(field(edge, o));
            Record endVertex = at(field(edge, history ? o + 2 : o + 1));
            int curveAt = history ? o + 5 : o + 3;
            int senseAt = history ? o + 6 : o + 4;
            Record curve = at(field(edge, curveAt));

            if (startVertex == null || endVertex == null || curve == null)
            {
                throw new InvalidOperationException();
            }

            AcisVec start = PointOf(startVertex, o, at, field);
            AcisVec end = PointOf(endVertex, o, at, field);
            AcisCurve read;

            if (curve.Name == "straight-curve")
            {
                read = AcisCurve.Line(start, end);
            }
            else if (curve.Name == "ellipse-curve")
            {
                AcisVec centre = Vec(curve, o, field);
                AcisVec normal = Vec(curve, o + 3, field);
                AcisVec major = Vec(curve, o + 6, field);
                double ratio = field(curve, o + 9).Number;
                double t0;
                double t1;

                if (history)
                {
                    t0 = field(edge, o + 1).Number;
                    t1 = field(edge, o + 3).Number;
                }
                else
                {
                    int b = o + 10;
                    bool finite0 = field(curve, b).Number != 0.0;
                    t0 = finite0 ? field(curve, b + 1).Number : 0.0;
                    int b1 = finite0 ? b + 2 : b + 1;
                    bool finite1 = field(curve, b1).Number != 0.0;
                    t1 = finite1 ? field(curve, b1 + 1).Number : 0.0;

                    if (!finite0 || !finite1)
                    {
                        // A whole ellipse, bounds none: the edge starts and ends at its one vertex.
                        t0 = -Math.PI;
                        t1 = Math.PI;
                    }

                    // AutoCAD's R2000 data of the result of a boolean gives an arc the bounds of its whole ellipse, or none, and
                    // lets the two vertices of the edge cut it: the arc runs from one to the other along the curve.
                    if (Math.Abs(t1 - t0 - 2.0 * Math.PI) < 1E-9 && start.DistanceTo(end) > 1E-9 * (1.0 + major.Length))
                    {
                        AcisCurve whole = AcisCurve.Ellipse(centre, normal, major, ratio, 0.0, 1.0);
                        t0 = whole.ParameterOf(start);
                        t1 = whole.ParameterOf(end);

                        while (t1 <= t0)
                        {
                            t1 += 2.0 * Math.PI;
                        }
                    }
                }

                read = AcisCurve.Ellipse(centre, normal, major, ratio, t0, t1);
            }
            else
            {
                throw new InvalidOperationException();
            }

            // The edge's own sense says whether it runs against its curve; the coedge's whether the loop runs against the edge.
            if (field(edge, senseAt).Number != 0.0)
            {
                read = read.Reversed();
            }

            return alongLoop && field(coedge, o + 4).Number != 0.0 ? read.Reversed().MarkedAgainstEdge() : read;
        }

        private static AcisVec PointOf(Record vertex, int o, Func<Token, Record> at, Func<Record, int, Token> field)
        {
            // The point is the last pointer of the vertex.
            Record point = null;

            for (int i = vertex.Tokens.Count - 1; i >= 0 && point == null; i--)
            {
                if (vertex.Tokens[i].Kind == 'p')
                {
                    point = at(vertex.Tokens[i]);
                }
            }

            if (point == null || point.Name != "point")
            {
                throw new InvalidOperationException();
            }

            return Vec(point, o, field);
        }
    }
}
