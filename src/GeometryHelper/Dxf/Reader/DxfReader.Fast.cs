using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public static partial class DxfReader
    {
        /// <summary>
        /// The shape of a LINE, ARC, CIRCLE, LWPOLYLINE, TEXT or MTEXT read straight from the bytes of its record, as
        /// <see cref="ShapesOf(Record, Tolerance)"/> reads it, to the bit, with no group made a string (but the words of a
        /// text, which <paramref name="decoding"/> reads from their bytes as the file does) and no reading object made, for
        /// <see cref="DxfFile"/>. False where it is not sure of reading it as the reader does: another type, a number
        /// that is not one or has a letter beyond ASCII (<see cref="DxfFastNumber"/> reads the rest, and leaves the odd one to
        /// the reader's own number reader), a group code that is not a number, a record that leaves out what places the entity,
        /// one of no length, no radius, no height or fewer than two distinct vertices, or any exception; the record is then
        /// read the long way, which gives the shapes, or none, and the reasons.
        /// </summary>
        internal static bool TryFastShape(
            byte[] bytes,
            int start,
            int end,
            string type,
            Tolerance tolerance,
            TextDecoding decoding,
            out object shape
        )
        {
            bool read;

            try
            {
                switch (type)
                {
                    case "LINE":
                        read = Reading.FastLine(bytes, start, end, tolerance, out shape);
                        break;
                    case "CIRCLE":
                        read = Reading.FastCircle(bytes, start, end, tolerance, out shape);
                        break;
                    case "ARC":
                        read = Reading.FastArc(bytes, start, end, tolerance, out shape);
                        break;
                    case "LWPOLYLINE":
                        read = Reading.FastLightweight(bytes, start, end, tolerance, out shape);
                        break;
                    case "TEXT":
                        read = Reading.FastText(bytes, start, end, false, decoding, out shape);
                        break;
                    case "MTEXT":
                        read = Reading.FastText(bytes, start, end, true, decoding, out shape);
                        break;
                    default:
                        shape = null;
                        return false;
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                shape = null;
                return false;
            }

            return read;
        }

        private sealed partial class Reading
        {
            /// <summary>A LINE: its two points, the segment of space between them.</summary>
            internal static bool FastLine(byte[] b, int start, int end, Tolerance tolerance, out object shape)
            {
                shape = null;
                double x0 = 0.0,
                    y0 = 0.0,
                    z0 = 0.0,
                    x1 = 0.0,
                    y1 = 0.0,
                    z1 = 0.0;
                int have = 0;
                var groups = new Groups(b, start, end);

                while (groups.Next(out int code, out int from, out int to))
                {
                    if (IsReal(code))
                    {
                        if (!DxfFastNumber.TryReal(b, from, to, out double v))
                        {
                            return false;
                        }

                        switch (code)
                        {
                            case 10:
                                First(ref have, 1, ref x0, v);
                                break;
                            case 20:
                                First(ref have, 2, ref y0, v);
                                break;
                            case 30:
                                First(ref have, 4, ref z0, v);
                                break;
                            case 11:
                                First(ref have, 8, ref x1, v);
                                break;
                            case 21:
                                First(ref have, 16, ref y1, v);
                                break;
                            case 31:
                                First(ref have, 32, ref z1, v);
                                break;
                        }
                    }
                    else if (IsWhole(code) && !DxfFastNumber.TryWhole(b, from, to, out _))
                    {
                        return false;
                    }
                }

                if (groups.Failed || (have & 7) == 0 || (have & 56) == 0)
                {
                    return false;
                }

                var first = new GeoPoint3(x0, y0, z0);
                var last = new GeoPoint3(x1, y1, z1);

                if (first.IsEqualTo(last, tolerance))
                {
                    return false;
                }

                shape = new GeoLine3(first, last);
                return true;
            }

            /// <summary>A CIRCLE: its centre, radius and extrusion, the circle of space about that normal.</summary>
            internal static bool FastCircle(byte[] b, int start, int end, Tolerance tolerance, out object shape)
            {
                shape = null;

                if (
                    !FastRound(b, start, end, false, out GeoPoint3 centre, out double radius, out _, out _, out Ocs ocs)
                )
                {
                    return false;
                }

                if (!(radius > tolerance.EqualPoint))
                {
                    return false;
                }

                shape = new GeoCircle3(ocs.World(centre), ocs.Normal, radius);
                return true;
            }

            /// <summary>An ARC: its centre, radius, angles in degrees and extrusion, the arc of space about that normal.</summary>
            internal static bool FastArc(byte[] b, int start, int end, Tolerance tolerance, out object shape)
            {
                shape = null;

                if (
                    !FastRound(
                        b,
                        start,
                        end,
                        true,
                        out GeoPoint3 centre,
                        out double radius,
                        out double startDegrees,
                        out double endDegrees,
                        out Ocs ocs
                    )
                )
                {
                    return false;
                }

                if (!(radius > tolerance.EqualPoint))
                {
                    return false;
                }

                shape = SpaceArc(centre, radius, startDegrees, endDegrees, ocs);
                return true;
            }

            /// <summary>
            /// The groups a CIRCLE or an ARC is placed by: the centre (10, 20, 30, one of them at least), the radius (40) and,
            /// of an arc, its two angles (50, 51), each of them there; and the extrusion (210, 220, 230).
            /// </summary>
            private static bool FastRound(
                byte[] b,
                int start,
                int end,
                bool arc,
                out GeoPoint3 centre,
                out double radius,
                out double startDegrees,
                out double endDegrees,
                out Ocs ocs
            )
            {
                centre = default(GeoPoint3);
                radius = 0.0;
                startDegrees = 0.0;
                endDegrees = 0.0;
                ocs = default(Ocs);
                double x = 0.0,
                    y = 0.0,
                    z = 0.0,
                    nx = 0.0,
                    ny = 0.0,
                    nz = 0.0;
                int have = 0;
                var groups = new Groups(b, start, end);

                while (groups.Next(out int code, out int from, out int to))
                {
                    if (IsReal(code))
                    {
                        if (!DxfFastNumber.TryReal(b, from, to, out double v))
                        {
                            return false;
                        }

                        switch (code)
                        {
                            case 10:
                                First(ref have, 1, ref x, v);
                                break;
                            case 20:
                                First(ref have, 2, ref y, v);
                                break;
                            case 30:
                                First(ref have, 4, ref z, v);
                                break;
                            case 40:
                                First(ref have, 8, ref radius, v);
                                break;
                            case 50:
                                First(ref have, 16, ref startDegrees, v);
                                break;
                            case 51:
                                First(ref have, 32, ref endDegrees, v);
                                break;
                            case 210:
                                First(ref have, 64, ref nx, v);
                                break;
                            case 220:
                                First(ref have, 128, ref ny, v);
                                break;
                            case 230:
                                First(ref have, 256, ref nz, v);
                                break;
                        }
                    }
                    else if (IsWhole(code) && !DxfFastNumber.TryWhole(b, from, to, out _))
                    {
                        return false;
                    }
                }

                if (groups.Failed || (have & 7) == 0 || (have & 8) == 0 || (arc && (have & 48) != 48))
                {
                    return false;
                }

                centre = new GeoPoint3(x, y, z);
                ocs = new Ocs((have & 448) == 0 ? new GeoVector3(0.0, 0.0, 1.0) : Extrusion(nx, ny, nz));
                return true;
            }

            /// <summary>
            /// A LWPOLYLINE: its vertices (each 10, 20) and their bulges (42) in the plane of its extrusion at its elevation
            /// (38), closed where the flag 1 of group 70 is set, made a chain or a loop of space as
            /// <see cref="ReadLightweight"/> makes it.
            /// </summary>
            internal static bool FastLightweight(byte[] b, int start, int end, Tolerance tolerance, out object shape)
            {
                shape = null;
                double elevation = 0.0,
                    nx = 0.0,
                    ny = 0.0,
                    nz = 0.0;
                int flags = 0;
                int have = 0;
                int expected = 0;
                List<GeoPoint3> points = null;
                List<double> bulges = null;
                bool atVertex = false;
                var groups = new Groups(b, start, end);

                while (groups.Next(out int code, out int from, out int to))
                {
                    if (IsReal(code))
                    {
                        if (!DxfFastNumber.TryReal(b, from, to, out double v))
                        {
                            return false;
                        }

                        switch (code)
                        {
                            case 10:
                                if (points == null)
                                {
                                    // The count of vertices (90) stands before them; its room is made once, as far as is sane.
                                    int room = expected > 0 && expected <= 4096 ? expected : 4;
                                    points = new List<GeoPoint3>(room);
                                    bulges = new List<double>(room);
                                }

                                points.Add(new GeoPoint3(v, 0.0, 0.0));
                                bulges.Add(0.0);
                                atVertex = true;
                                break;
                            case 20:
                                if (atVertex)
                                {
                                    GeoPoint3 last = points[points.Count - 1];
                                    points[points.Count - 1] = new GeoPoint3(last.X, v, 0.0);
                                }

                                break;
                            case 42:
                                if (atVertex)
                                {
                                    bulges[bulges.Count - 1] = v;
                                }

                                break;
                            case 38:
                                First(ref have, 1, ref elevation, v);
                                break;
                            case 210:
                                First(ref have, 2, ref nx, v);
                                break;
                            case 220:
                                First(ref have, 4, ref ny, v);
                                break;
                            case 230:
                                First(ref have, 8, ref nz, v);
                                break;
                        }
                    }
                    else if (IsWhole(code))
                    {
                        if (!DxfFastNumber.TryWhole(b, from, to, out int n))
                        {
                            return false;
                        }

                        if (code == 70 && (have & 16) == 0)
                        {
                            have |= 16;
                            flags = n;
                        }
                        else if (code == 90 && expected == 0)
                        {
                            expected = n;
                        }
                    }
                }

                if (groups.Failed || points == null)
                {
                    return false;
                }

                var ocs = new Ocs((have & 14) == 0 ? new GeoVector3(0.0, 0.0, 1.0) : Extrusion(nx, ny, nz));

                // The vertices lie at the elevation, in the plane of the extrusion, and are turned into the world.
                for (int i = 0; i < points.Count; i++)
                {
                    points[i] = new GeoPoint3(points[i].X, points[i].Y, elevation);

                    if (!ocs.IsWorld)
                    {
                        points[i] = ocs.World(points[i]);
                    }
                }

                int opened = 0;

                return TryChain(
                    tolerance,
                    null,
                    false,
                    points,
                    bulges,
                    (flags & 1) != 0,
                    false,
                    ocs.Normal,
                    out shape,
                    ref opened
                );
            }

            /// <summary>
            /// A TEXT or an MTEXT: where it stands, how tall, turned and wide, and its words, as <see cref="ReadText"/> reads it
            /// from the groups. An MTEXT's own groups are those before an object embedded in it (the first 101), as there; the
            /// groups after it are checked for a number and no more. The words are made of the bytes as the file's decoding
            /// reads the text of a group, then taken through the reader's own functions.
            /// </summary>
            internal static bool FastText(
                byte[] b,
                int start,
                int end,
                bool multiline,
                TextDecoding decoding,
                out object shape
            )
            {
                shape = null;
                double x = 0.0,
                    y = 0.0,
                    z = 0.0,
                    ax = 0.0,
                    ay = 0.0,
                    az = 0.0,
                    height = 0.0,
                    factor = 0.0,
                    degrees = 0.0,
                    nx = 0.0,
                    ny = 0.0,
                    nz = 0.0;
                int have = 0;
                int horizontal = 0;
                int vertical = 0;
                string first = null;
                StringBuilder pieces = null;
                bool embedded = false;
                var groups = new Groups(b, start, end);

                while (groups.Next(out int code, out int from, out int to))
                {
                    if (IsReal(code))
                    {
                        if (!DxfFastNumber.TryReal(b, from, to, out double v))
                        {
                            return false;
                        }

                        if (embedded)
                        {
                            continue;
                        }

                        switch (code)
                        {
                            case 10:
                                First(ref have, 1, ref x, v);
                                break;
                            case 20:
                                First(ref have, 2, ref y, v);
                                break;
                            case 30:
                                First(ref have, 4, ref z, v);
                                break;
                            case 11:
                                First(ref have, 8, ref ax, v);
                                break;
                            case 21:
                                First(ref have, 16, ref ay, v);
                                break;
                            case 31:
                                First(ref have, 32, ref az, v);
                                break;
                            case 40:
                                First(ref have, 64, ref height, v);
                                break;
                            case 41:
                                First(ref have, 128, ref factor, v);
                                break;
                            case 50:
                                First(ref have, 256, ref degrees, v);
                                break;
                            case 210:
                                First(ref have, 512, ref nx, v);
                                break;
                            case 220:
                                First(ref have, 1024, ref ny, v);
                                break;
                            case 230:
                                First(ref have, 2048, ref nz, v);
                                break;
                        }
                    }
                    else if (IsWhole(code))
                    {
                        if (!DxfFastNumber.TryWhole(b, from, to, out int n))
                        {
                            return false;
                        }

                        if (!embedded && code == 72 && (have & 4096) == 0)
                        {
                            have |= 4096;
                            horizontal = n;
                        }
                        else if (!embedded && code == 73 && (have & 8192) == 0)
                        {
                            have |= 8192;
                            vertical = n;
                        }
                    }
                    else if (embedded)
                    {
                        continue;
                    }
                    else if (code == 101 && multiline)
                    {
                        embedded = true;
                    }
                    else if (code == 1 && first == null)
                    {
                        first = decoding.GetString(b, from, to - from);
                    }
                    else if (code == 3 && multiline)
                    {
                        pieces = pieces ?? new StringBuilder();
                        pieces.Append(decoding.GetString(b, from, to - from));
                    }
                }

                // The point, as the reader takes it, a part the file leaves out being nought, and the height, above nought.
                if (groups.Failed || (have & 7) == 0 || (have & 64) == 0 || !(height > 0.0))
                {
                    return false;
                }

                var at = new GeoPoint3(x, y, z);
                GeoVector3 normal = (have & 3584) == 0 ? new GeoVector3(0.0, 0.0, 1.0) : Extrusion(nx, ny, nz);
                var ocs = new Ocs(normal);
                double rotation;
                double widthFactor = 1.0;
                double width = 0.0;
                string value;

                if (multiline)
                {
                    var direction = new GeoVector3(ax, ay, az);
                    rotation =
                        (have & 56) != 0 && direction.Length > 0.0
                            ? Math.Atan2(direction.DotProduct(ocs.V), direction.DotProduct(ocs.U))
                            : degrees * Math.PI / 180.0;
                    width = Math.Max(0.0, (have & 128) != 0 ? factor : 0.0);
                    value = MText(
                        pieces == null ? first ?? string.Empty : pieces.Append(first ?? string.Empty).ToString()
                    );
                }
                else
                {
                    bool fromFirst = vertical == 0 && (horizontal == 0 || horizontal == 3 || horizontal == 5);

                    if (!fromFirst && (have & 56) != 0)
                    {
                        at = new GeoPoint3(ax, ay, az);
                    }

                    at = ocs.World(at);
                    rotation = degrees * Math.PI / 180.0;
                    double given = (have & 128) != 0 ? factor : 1.0;
                    widthFactor = given > 0.0 ? given : 1.0;
                    value = OneLine(Signs(Decode(Carets(first ?? string.Empty)), false));
                }

                shape = DxfText.Read(at, normal, height, DxfText.Turn(rotation), value, multiline, widthFactor, width);
                return true;
            }

            /// <summary>Takes a value for the first group of its code: <paramref name="seen"/> keeps which of them came.</summary>
            private static void First(ref int seen, int bit, ref double slot, double value)
            {
                if ((seen & bit) == 0)
                {
                    seen |= bit;
                    slot = value;
                }
            }

            /// <summary>Whether a group code holds a real, as <see cref="Record"/> reads it: 10 to 59, 110 to 149, 210 to 239.</summary>
            private static bool IsReal(int c) =>
                (c >= 10 && c <= 59) || (c >= 110 && c <= 149) || (c >= 210 && c <= 239);

            /// <summary>
            /// Whether a group code holds a whole number the reader checks for: 60 to 99, 170 to 179, 270 to 289, 370 to 379
            /// and 420 to 429, bar the colour (62), the line weight (370) and the true colour (420), which the look takes or
            /// leaves.
            /// </summary>
            private static bool IsWhole(int c) =>
                c != 62
                && c != 370
                && c != 420
                && (
                    (c >= 60 && c <= 99)
                    || (c >= 170 && c <= 179)
                    || (c >= 270 && c <= 289)
                    || (c >= 370 && c <= 379)
                    || (c >= 420 && c <= 429)
                );

            /// <summary>
            /// The groups of a record in bytes after its group 0, one at a time, comments (999) left out, as
            /// <c>DxfFile.GroupWalk</c> gives them: the code, and where its value is, a carriage return at the end of the
            /// line taken off. A code that is not a number stops it, <see cref="Failed"/> set.
            /// </summary>
            private struct Groups
            {
                private readonly byte[] _bytes;
                private readonly int _end;
                private int _at;

                internal Groups(byte[] bytes, int start, int end)
                {
                    _bytes = bytes;
                    _end = end;
                    Failed = false;

                    // The group 0 that names the record: its two lines.
                    _at = start;

                    for (int line = 0; line < 2 && _at < end; line++)
                    {
                        LineEnd(bytes, _at, end, out _at);
                    }
                }

                /// <summary>Whether a group code was not a number.</summary>
                internal bool Failed { get; private set; }

                internal bool Next(out int code, out int valueStart, out int valueEnd)
                {
                    byte[] bytes = _bytes;
                    int end = _end;
                    int at = _at;

                    while (at < end)
                    {
                        int codeEnd = LineEnd(bytes, at, end, out valueStart);

                        if (!Code(bytes, at, codeEnd, out code))
                        {
                            Failed = true;
                            break;
                        }

                        valueEnd = LineEnd(bytes, valueStart, end, out at);

                        if (code != 999)
                        {
                            _at = at;
                            return true;
                        }
                    }

                    _at = end;
                    code = 0;
                    valueStart = 0;
                    valueEnd = 0;
                    return false;
                }

                /// <summary>The end of the line from a byte, a carriage return before its line feed left out, and where the next begins.</summary>
                private static int LineEnd(byte[] bytes, int at, int end, out int next)
                {
                    int feed = at;

                    while (feed < end && bytes[feed] != (byte)'\n')
                    {
                        feed++;
                    }

                    next = feed < end ? feed + 1 : end;
                    return feed > at && bytes[feed - 1] == (byte)'\r' ? feed - 1 : feed;
                }

                /// <summary>A group code: a whole number of up to nine digits, a sign before it, padded with spaces or tabs.</summary>
                private static bool Code(byte[] bytes, int start, int end, out int code)
                {
                    code = 0;

                    while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                    {
                        start++;
                    }

                    while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                    {
                        end--;
                    }

                    bool negative = start < end && bytes[start] == (byte)'-';

                    if (negative)
                    {
                        start++;
                    }

                    if (start == end || end - start > 9)
                    {
                        return false;
                    }

                    for (int i = start; i < end; i++)
                    {
                        int d = bytes[i] - '0';

                        if (d < 0 || d > 9)
                        {
                            return false;
                        }

                        code = code * 10 + d;
                    }

                    code = negative ? -code : code;
                    return true;
                }
            }
        }
    }
}
