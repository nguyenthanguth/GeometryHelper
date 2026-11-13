using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The extended data AutoCAD 2021 gives the INSERT a hatch is broken into when it saves R12: the hatch as the application
    /// <c>ACAD</c> holds it for R14, which the pieces alone would lose. A hatch of a pattern is <c>HATCH</c> with its name, scale
    /// and angle, the objects it follows (<c>ASC_BOUNDS</c>) and its seed points (<c>ASC_SEEDPOINT</c>) when it is associative,
    /// then <c>R14_HATCH_DATA</c>; a hatch filled solid or with a gradient holds <c>R14_HATCH_DATA</c> alone.
    /// </summary>
    internal sealed partial class DxfVersionConverter
    {
        // The handles of the entities each associative hatch follows, by the handle of the entity: the boundary carries the
        // hatch in its own extended data.
        private readonly Dictionary<string, List<string>> _bounds = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>
        /// The applications of the extended data of the INSERT a hatch is broken into: those the hatch holds but <c>ACAD</c>, in
        /// capitals, then the hatch as <c>ACAD</c> holds it; the old ones alone where the hatch cannot be read.
        /// </summary>
        private List<Group> HatchData(Record hatch)
        {
            var kept = new List<Group>();
            bool application = false;
            bool skipping = false;
            int at = -1;

            for (int i = 0; i < hatch.Groups.Count; i++)
            {
                Group group = hatch.Groups[i];

                if (group.Code == 1001)
                {
                    application = true;
                    skipping = string.Equals(group.Value.Trim(), "ACAD", StringComparison.OrdinalIgnoreCase);

                    if (skipping)
                    {
                        at = kept.Count;
                        continue;
                    }

                    kept.Add(new Group(1001, group.Value.ToUpperInvariant()));
                    continue;
                }

                if (application && !skipping && group.Code >= 1000)
                {
                    kept.Add(group);
                }
            }

            List<Group> data = ReadHatch(hatch);

            if (data == null)
            {
                return kept;
            }

            kept.InsertRange(at < 0 ? kept.Count : at, data);
            return kept;
        }

        /// <summary>A real number as a group of R12 writes it: as short as it reads back, a whole one with its <c>.0</c>, never an exponent.</summary>
        private static string Plain(double value)
        {
            string text = value.ToString("R", CultureInfo.InvariantCulture);

            if (text.IndexOf('E') >= 0)
            {
                text = value.ToString("0.#################", CultureInfo.InvariantCulture);
            }

            return text.IndexOf('.') < 0 ? text + ".0" : text;
        }

        // A reader over the groups of a hatch: each Take wants the code it is given, and fails the whole reading where it meets
        // another, which a hatch of an odd form (a boundary kind R12 has no form for) does.
        private sealed class HatchReader
        {
            private readonly List<Group> _groups;
            private int _at;

            internal HatchReader(List<Group> groups, int at)
            {
                _groups = groups;
                _at = at;
            }

            internal bool Failed { get; private set; }

            internal bool Has(int code) => _at < _groups.Count && _groups[_at].Code == code;

            internal string Text(int code)
            {
                if (!Has(code))
                {
                    Failed = true;
                    return string.Empty;
                }

                return _groups[_at++].Value.Trim();
            }

            internal double Real(int code)
            {
                string text = Text(code);

                if (
                    !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    && !Failed
                )
                {
                    Failed = true;
                }

                return value;
            }

            internal int Whole(int code)
            {
                string text = Text(code);

                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && !Failed)
                {
                    Failed = true;
                }

                return value;
            }
        }

        // A point of R14's hatch data: its x, then two zeros, then its y and z, under the codes 1011, 1021, 1031, 1021, 1031.
        private static void Point(List<Group> to, double x, double y, double z)
        {
            to.Add(new Group(1011, Plain(x)));
            to.Add(new Group(1021, "0.0"));
            to.Add(new Group(1031, "0.0"));
            to.Add(new Group(1021, Plain(y)));
            to.Add(new Group(1031, Plain(z)));
        }

        /// <summary>A point of the plane of a hatch as the world holds it, by the arbitrary axis algorithm of the plane's normal.</summary>
        private static double[] World(double x, double y, double z, double nx, double ny, double nz)
        {
            double ax;
            double ay;
            double az;

            if (Math.Abs(nx) < 1.0 / 64.0 && Math.Abs(ny) < 1.0 / 64.0)
            {
                // The world Y crossed with the normal.
                ax = nz;
                ay = 0.0;
                az = -nx;
            }
            else
            {
                // The world Z crossed with the normal.
                ax = -ny;
                ay = nx;
                az = 0.0;
            }

            double length = Math.Sqrt(ax * ax + ay * ay + az * az);
            ax /= length;
            ay /= length;
            az /= length;
            double bx = ny * az - nz * ay;
            double by = nz * ax - nx * az;
            double bz = nx * ay - ny * ax;
            return new[] { x * ax + y * bx + z * nx, x * ay + y * by + z * ny, x * az + y * bz + z * nz };
        }

        /// <summary>The <c>ACAD</c> application of the INSERT of a hatch, as AutoCAD writes it; null where the hatch has a form it cannot read.</summary>
        private List<Group> ReadHatch(Record hatch)
        {
            int start = hatch.Groups.FindIndex(g => g.Code == 100 && g.Value.Trim() == "AcDbHatch");

            if (start < 0)
            {
                return null;
            }

            var reader = new HatchReader(hatch.Groups, start + 1);
            reader.Real(10);
            reader.Real(20);
            double elevation = reader.Real(30);
            double nx = reader.Real(210);
            double ny = reader.Real(220);
            double nz = reader.Real(230);
            string name = reader.Text(2);
            int solid = reader.Whole(70);
            int associative = reader.Whole(71);
            int count = reader.Whole(91);

            if (reader.Failed)
            {
                return null;
            }

            // The loops: the data of each as R14 holds it, and what each follows.
            var loops = new List<Group>();
            var bounds = new List<KeyValuePair<int, List<string>>>();

            for (int loop = 0; loop < count && !reader.Failed; loop++)
            {
                int flags = reader.Whole(92);
                loops.Add(Long(flags));
                bool polyline = (flags & 2) != 0;
                bool bulges = false;

                if (polyline)
                {
                    bulges = reader.Whole(72) != 0;
                    loops.Add(Whole(1070, bulges ? 1 : 0));
                    loops.Add(Whole(1070, reader.Whole(73)));
                }

                int edges = reader.Whole(93);
                loops.Add(Long(edges));

                for (int edge = 0; edge < edges && !reader.Failed; edge++)
                {
                    if (polyline)
                    {
                        loops.Add(new Group(1040, Plain(reader.Real(10))));
                        loops.Add(new Group(1040, Plain(reader.Real(20))));

                        if (bulges && reader.Has(42))
                        {
                            loops.Add(new Group(1040, Plain(reader.Real(42))));
                        }
                        else if (bulges)
                        {
                            loops.Add(new Group(1040, "0.0"));
                        }

                        continue;
                    }

                    int type = reader.Whole(72);
                    loops.Add(Whole(1070, type));

                    switch (type)
                    {
                        case 1:
                            foreach (int code in new[] { 10, 20, 11, 21 })
                            {
                                loops.Add(new Group(1040, Plain(reader.Real(code))));
                            }

                            break;
                        case 2:
                            foreach (int code in new[] { 10, 20, 40 })
                            {
                                loops.Add(new Group(1040, Plain(reader.Real(code))));
                            }

                            loops.Add(new Group(1040, Plain(reader.Real(50) * Math.PI / 180.0)));
                            loops.Add(new Group(1040, Plain(reader.Real(51) * Math.PI / 180.0)));
                            loops.Add(Whole(1070, reader.Whole(73)));
                            break;
                        case 3:
                            foreach (int code in new[] { 10, 20, 11, 21, 40 })
                            {
                                loops.Add(new Group(1040, Plain(reader.Real(code))));
                            }

                            loops.Add(new Group(1040, Plain(reader.Real(50) * Math.PI / 180.0)));
                            loops.Add(new Group(1040, Plain(reader.Real(51) * Math.PI / 180.0)));
                            loops.Add(Whole(1070, reader.Whole(73)));
                            break;
                        case 4:
                            Spline(reader, loops);
                            break;
                        default:
                            return null;
                    }
                }

                int sources = reader.Whole(97);
                loops.Add(Long(sources));
                var handles = new List<string>(sources);

                for (int k = 0; k < sources && !reader.Failed; k++)
                {
                    string handle = reader.Text(330);
                    handles.Add(handle);
                    loops.Add(new Group(1005, handle));
                }

                bounds.Add(new KeyValuePair<int, List<string>>(flags, handles));
            }

            int style = reader.Whole(75);
            int kind = reader.Whole(76);
            var pattern = new List<Group>();
            double angle = 0.0;
            double scale = 1.0;

            if (solid == 0)
            {
                angle = reader.Real(52);
                scale = reader.Real(41);
                pattern.Add(new Group(1040, Plain(angle * Math.PI / 180.0)));
                pattern.Add(new Group(1040, Plain(scale)));
                pattern.Add(Whole(1070, reader.Whole(77)));
                int lines = reader.Whole(78);
                pattern.Add(Whole(1070, lines));

                for (int line = 0; line < lines && !reader.Failed; line++)
                {
                    pattern.Add(new Group(1040, Plain(reader.Real(53) * Math.PI / 180.0)));

                    foreach (int code in new[] { 43, 44, 45, 46 })
                    {
                        pattern.Add(new Group(1040, Plain(reader.Real(code))));
                    }

                    int dashes = reader.Whole(79);
                    pattern.Add(Whole(1070, dashes));

                    for (int dash = 0; dash < dashes && !reader.Failed; dash++)
                    {
                        pattern.Add(new Group(1040, Plain(reader.Real(49))));
                    }
                }
            }

            if (reader.Has(47))
            {
                pattern.Add(new Group(1040, Plain(reader.Real(47))));
            }

            int seeds = reader.Whole(98);
            var points = new List<double[]>(seeds);

            for (int seed = 0; seed < seeds && !reader.Failed; seed++)
            {
                double x = reader.Real(10);
                double y = reader.Real(20);
                points.Add(World(x, y, elevation, nx, ny, nz));
            }

            if (reader.Failed)
            {
                return null;
            }

            var data = new List<Group> { new Group(1001, "ACAD") };

            if (solid == 0)
            {
                data.Add(new Group(1000, "HATCH"));
            }

            data.Add(new Group(1002, "{"));

            if (solid == 0)
            {
                data.Add(Whole(1070, 19));
                data.Add(new Group(1000, name));
                data.Add(new Group(1040, Plain(scale)));
                data.Add(new Group(1040, Plain(angle * Math.PI / 180.0)));

                if (associative != 0)
                {
                    var following = bounds.Where(b => b.Value.Count > 0).Reverse().ToList();
                    data.Add(new Group(1000, "ASC_BOUNDS"));
                    data.Add(Whole(1070, following.Count));

                    foreach (KeyValuePair<int, List<string>> follow in following)
                    {
                        data.Add(Whole(1070, (follow.Key & 2) != 0 ? 1 : 2));
                        data.Add(Whole(1070, follow.Value.Count));
                        data.AddRange(follow.Value.Select(h => new Group(1005, h)));
                    }

                    data.Add(new Group(1000, "ASC_SEEDPOINT"));

                    foreach (double[] point in points)
                    {
                        Point(data, point[0], point[1], point[2]);
                    }
                }
            }

            data.Add(new Group(1000, "R14_HATCH_DATA"));
            data.Add(new Group(1000, HandleOf(hatch) ?? string.Empty));
            Point(data, 1.0, 0.0, 0.0);
            Point(data, 0.0, 1.0, 0.0);
            Point(data, 0.0, 0.0, 1.0);
            data.Add(new Group(1040, Plain(elevation)));
            data.Add(new Group(1010, Plain(nx)));
            data.Add(new Group(1020, Plain(ny)));
            data.Add(new Group(1030, Plain(nz)));
            data.Add(new Group(1000, name));
            data.Add(Whole(1070, solid));
            data.Add(Whole(1070, associative));
            data.Add(Long(count));
            data.AddRange(loops);
            data.Add(Whole(1070, style));
            data.Add(Whole(1070, kind));
            data.AddRange(pattern);
            data.Add(Long(seeds));

            // Each seed point as the world holds it: its x, y and z.
            foreach (double[] point in points)
            {
                data.Add(new Group(1040, Plain(point[0])));
                data.Add(new Group(1040, Plain(point[1])));
                data.Add(new Group(1040, Plain(point[2])));
            }

            data.Add(new Group(1002, "}"));
            return data;
        }

        // The spline edge of a loop: its degree, whether it is rational and periodic, its knots and control points; the fit points
        // and tangents of AutoCAD's own edge are not written.
        private void Spline(HatchReader reader, List<Group> to)
        {
            to.Add(Whole(1070, reader.Whole(94)));
            int rational = reader.Whole(73);
            to.Add(Whole(1070, rational));
            to.Add(Whole(1070, reader.Whole(74)));
            int knots = reader.Whole(95);
            int controls = reader.Whole(96);
            to.Add(Long(knots));
            to.Add(Long(controls));

            for (int i = 0; i < knots && !reader.Failed; i++)
            {
                to.Add(new Group(1040, Plain(reader.Real(40))));
            }

            for (int i = 0; i < controls && !reader.Failed; i++)
            {
                to.Add(new Group(1040, Plain(reader.Real(10))));
                to.Add(new Group(1040, Plain(reader.Real(20))));

                if (rational != 0)
                {
                    to.Add(new Group(1040, Plain(reader.Real(42))));
                }
            }

            // The fit points and the tangents, when the edge has any (a count in 97), are read and left out.
            if (reader.Has(97))
            {
                int fits = reader.Whole(97);

                for (int i = 0; i < fits && !reader.Failed; i++)
                {
                    reader.Real(11);
                    reader.Real(21);
                }

                reader.Real(12);
                reader.Real(22);
                reader.Real(13);
                reader.Real(23);
            }
        }

        /// <summary>Notes, for each associative hatch of a section, the entities it follows, so that each can say which hatch follows it.</summary>
        private void CollectBounds(Section section)
        {
            foreach (Record record in section.Records.Where(r => r.Type == "HATCH"))
            {
                string hatch = HandleOf(record);
                Group associative = record.Groups.FirstOrDefault(g => g.Code == 71);
                Group solid = record.Groups.FirstOrDefault(g => g.Code == 70);

                // Only a hatch of a pattern says what it follows: one filled solid or with a gradient is SOLIDs, no more.
                if (
                    hatch == null
                    || associative.Value == null
                    || associative.Value.Trim() == "0"
                    || solid.Value == null
                    || solid.Value.Trim() != "0"
                )
                {
                    continue;
                }

                for (int i = 0; i < record.Groups.Count; i++)
                {
                    if (
                        record.Groups[i].Code != 97
                        || !int.TryParse(
                            record.Groups[i].Value.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int count
                        )
                    )
                    {
                        continue;
                    }

                    for (int k = 1; k <= count && i + k < record.Groups.Count && record.Groups[i + k].Code == 330; k++)
                    {
                        string boundary = record.Groups[i + k].Value.Trim();

                        if (!_bounds.TryGetValue(boundary, out List<string> hatches))
                        {
                            _bounds[boundary] = hatches = new List<string>(1);
                        }

                        if (!hatches.Contains(hatch))
                        {
                            hatches.Add(hatch);
                        }
                    }
                }
            }
        }

        /// <summary>Gives an entity a hatch follows the extended data AutoCAD gives it: the hatches that follow it.</summary>
        private void AddBounds(Record entity, List<string> hatches)
        {
            entity.Groups.Add(new Group(1001, "ACAD"));
            entity.Groups.Add(new Group(1000, "ASC_BOUNDS"));
            entity.Groups.Add(new Group(1002, "{"));
            entity.Groups.Add(Whole(1070, 19));
            entity.Groups.AddRange(hatches.Select(h => new Group(1005, h)));
            entity.Groups.Add(new Group(1002, "}"));
        }

        /// <summary>A whole number under the code 1071, as AutoCAD writes the long integers of extended data, padded like the codes 90 to 99.</summary>
        private Group Long(int value) => Whole(1071, value);
    }
}
