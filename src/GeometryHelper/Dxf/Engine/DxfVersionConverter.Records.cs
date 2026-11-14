using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region One record

        /// <summary>
        /// Gives a record the form of the version: what the kind has in another form, its groups limited to those of the
        /// version, and what the version has and the record has not added where AutoCAD would write it.
        /// </summary>
        private void Reform(Record record)
        {
            string type = record.Type;

            switch (type)
            {
                case "HATCH":
                    if (_target < 1018)
                    {
                        Solid(record);
                    }

                    FitCounts(record);
                    break;
                case "ACAD_TABLE":
                    if (_target >= 1024)
                    {
                        TableVersion(record);
                    }

                    break;
                case "ACDBDATATABLE":
                case "DATATABLE":
                    DataTable(record);
                    break;
                case "TABLESTYLE":
                    if (_source >= 1021 && _target < 1021)
                    {
                        KeepStyle(record);
                    }
                    else if (_source < 1021 && _target >= 1021)
                    {
                        StyleUp(record);
                    }

                    if (_target < 1024)
                    {
                        TableStyle(record);
                    }
                    else
                    {
                        StyleVersion(record);
                    }

                    break;
                case "MULTILEADER":
                    if (_target < 1027)
                    {
                        ExtendToText(record);
                    }

                    break;
                case "MTEXT":
                    if (_target >= 1021 && _source < 1021)
                    {
                        ColumnHeight(record);
                    }

                    if (_target < 1032)
                    {
                        Columns(record);
                    }
                    else if (_source < 1032)
                    {
                        ColumnsUp(record);
                    }

                    if (_target < 1018)
                    {
                        Paragraphs(record);
                    }

                    if (_target < 1018)
                    {
                        MTextData(record);
                    }
                    else if (_source < 1018)
                    {
                        NoMTextData(record);
                    }

                    break;
                case "ATTRIB":
                case "ATTDEF":
                    if (_target < 1032)
                    {
                        SingleLine(record);
                    }

                    if (_target < 1024)
                    {
                        AttributeVersion(record);
                    }

                    break;
                case "ARC_DIMENSION":
                    ArcKind(record);
                    break;
                case "BLOCK_RECORD":
                    if (_source < 1021 && _target >= 1021)
                    {
                        SpaceSettings(record);
                    }

                    break;
                case "LAYOUT":
                    if (_source < 1018 && _target >= 1018)
                    {
                        ShadePlot(record);
                    }

                    break;
            }

            if (HasStyleOverrides(type))
            {
                DimensionOverrides(record);
            }

            Trim(record, type);
        }

        /// <summary>
        /// Limits the groups of a record to those of the version: each of a subclass that came with a later version taken out,
        /// its marker with them where it has nothing else, the groups of a dimension style by the version of the variable each
        /// holds, and the size of the graphics of a proxy (92, 160 from R2010) in the group the version has.
        /// </summary>
        private void Trim(Record record, string type)
        {
            List<Group> groups = record.Groups;
            List<Group> kept = null;
            string subclass = string.Empty;
            bool data = false;
            bool styled = type == "DIMSTYLE";
            int seventies = 0;

            for (int i = 1; i < groups.Count; i++)
            {
                Group group = groups[i];
                bool drop = false;
                Group written = group;

                if (group.Code == 1001)
                {
                    data = true;
                }

                if (!data && group.Code == 100)
                {
                    subclass = group.Value;
                    seventies = 0;
                    drop = DxfVersions.SinceOfSubclass(subclass) > _target;
                }
                else if (!data && subclass.Length > 0)
                {
                    if (styled)
                    {
                        seventies += group.Code == 70 ? 1 : 0;
                        drop = DxfFile.SinceOf("DIMSTYLE", group.Code, seventies > 1) > _target;
                    }
                    else
                    {
                        drop = DxfVersions.SinceOfGroup(subclass, group.Code) > _target;
                    }

                    if (!drop && subclass == "AcDbEntity" && (group.Code == 92 || group.Code == 160))
                    {
                        int wanted = _target >= 1024 ? 160 : 92;

                        if (group.Code != wanted)
                        {
                            written = new Group(wanted, group.Value);
                        }
                    }
                }

                if (drop || written.Code != group.Code)
                {
                    if (kept == null)
                    {
                        kept = groups.GetRange(0, i);
                    }
                }

                if (!drop)
                {
                    kept?.Add(written);
                }
            }

            if (kept != null)
            {
                record.Groups = kept;
            }
        }

        /// <summary>Where a subclass of a record stands: from its marker to the next, or to the extended data, or to the end.</summary>
        private static bool Subclass(List<Group> groups, string name, out int start, out int end)
        {
            start = -1;
            end = groups.Count;

            for (int i = 1; i < groups.Count; i++)
            {
                if (groups[i].Code == 1001)
                {
                    end = Math.Min(end, i);
                    break;
                }

                if (groups[i].Code == 100)
                {
                    if (start >= 0)
                    {
                        end = i;
                        break;
                    }

                    if (groups[i].Value == name)
                    {
                        start = i;
                    }
                }
            }

            return start >= 0;
        }

        #endregion

        #region A table made a block

        /// <summary>
        /// Makes a table the INSERT of the block it is drawn with, its anonymous block: the groups of its entity and of the
        /// block reference it also is, which hold the block's name and where it stands, and none of its cells, its graphics
        /// of a proxy or its extension dictionary, which is left out with what it owns.
        /// </summary>
        private void Block(Record table)
        {
            List<Group> groups = table.Groups;
            var made = new List<Group>(groups.Count) { new Group(0, "INSERT") };
            string subclass = string.Empty;

            for (int i = 1; i < groups.Count; i++)
            {
                Group group = groups[i];

                if (group.Code == 1001 || (group.Code == 100 && group.Value == "AcDbTable"))
                {
                    break;
                }

                if (group.Code == 100)
                {
                    subclass = group.Value;
                }
                else if (subclass == "AcDbEntity" && (group.Code == 92 || group.Code == 160 || group.Code == 310))
                {
                    continue;
                }
                else if (group.Code == 102 && group.Value == "{ACAD_XDICTIONARY" && i + 2 < groups.Count)
                {
                    _owning.Add(groups[i + 1].Value.Trim());
                    i += 2;
                    continue;
                }

                made.Add(group);
            }

            table.Groups = made;
            _replaced["ACAD_TABLE"] = _replaced.TryGetValue("ACAD_TABLE", out int count) ? count + 1 : 1;
        }

        #endregion

        #region Hatches

        /// <summary>
        /// Leaves a hatch filled with a gradient, which R2000 has not, filled solid with the first colour of the gradient: that
        /// colour (63) is the colour of the hatch (62) then, as AutoCAD's SaveAs gives it.
        /// </summary>
        private void Solid(Record record)
        {
            List<Group> groups = record.Groups;

            if (!Subclass(groups, "AcDbHatch", out int start, out int end))
            {
                return;
            }

            int gradient = groups.FindIndex(start, end - start, g => g.Code == 450);
            int colour = gradient < 0 ? -1 : groups.FindIndex(gradient, end - gradient, g => g.Code == 63);

            if (colour < 0 || !Number(groups[gradient].Value, out int on) || on != 1)
            {
                return;
            }

            Group made = new Group(62, groups[colour].Value);

            if (!Subclass(groups, "AcDbEntity", out int first, out int last))
            {
                return;
            }

            int own = groups.FindIndex(first, last - first, g => g.Code == 62);

            if (own >= 0)
            {
                groups[own] = made;
                return;
            }

            // After the layer and the linetype, where AutoCAD writes the colour.
            int after = first + 1;

            for (int i = first + 1; i < last; i++)
            {
                if (groups[i].Code == 8 || groups[i].Code == 6 || groups[i].Code == 67 || groups[i].Code == 410)
                {
                    after = i + 1;
                }
            }

            groups.Insert(after, made);
        }

        /// <summary>
        /// Takes the group 295 a multileader of R2013 and later has after the attachment types of its text (273), whether its leader
        /// is extended to the text, out; the 295 before it, whether its text is a block, is every version's.
        /// </summary>
        private void ExtendToText(Record record)
        {
            List<Group> groups = record.Groups;
            int types = -1;

            for (int i = 2; i + 1 < groups.Count && types < 0; i++)
            {
                if (groups[i].Code == 273 && groups[i - 1].Code == 272 && groups[i - 2].Code == 271)
                {
                    types = i;
                }
            }

            if (types >= 0 && groups[types + 1].Code == 295)
            {
                groups.RemoveAt(types + 1);
            }
        }

        /// <summary>
        /// Names the data table a table keeps its round trip in as the version does: <c>ACDBDATATABLE</c>, in the subclass of that
        /// name, in R2000, <c>DATATABLE</c> in <c>AcDbDataTable</c> from R2004.
        /// </summary>
        private void DataTable(Record record)
        {
            bool old = _target < 1018;
            record.Groups[0] = new Group(0, old ? "ACDBDATATABLE" : "DATATABLE");
            int at = record.Groups.FindIndex(g =>
                g.Code == 100 && (g.Value == "ACDBDATATABLE" || g.Value == "AcDbDataTable")
            );

            if (at >= 0)
            {
                record.Groups[at] = new Group(100, old ? "ACDBDATATABLE" : "AcDbDataTable");
            }
        }

        /// <summary>Gives a table the group 280 R2010 and later write first in it, after its subclass marker (the version of its data, nought), where it has none there (a 280 after its flags is another group).</summary>
        private void TableVersion(Record record)
        {
            List<Group> groups = record.Groups;

            if (
                Subclass(groups, "AcDbTable", out int start, out int end)
                && !(start + 1 < end && groups[start + 1].Code == 280)
            )
            {
                groups.Insert(start + 1, Whole(280, 0));
            }
        }

        /// <summary>Takes the group 280 R2010 and later write first in a table style, before its description (3), out.</summary>
        private void TableStyle(Record record)
        {
            List<Group> groups = record.Groups;

            if (
                Subclass(groups, "AcDbTableStyle", out int start, out _)
                && start + 2 < groups.Count
                && groups[start + 1].Code == 280
                && groups[start + 2].Code == 3
            )
            {
                groups.RemoveAt(start + 1);
            }
        }

        /// <summary>
        /// Gives each spline edge of a hatch the count of its fit points (97) the version writes: from R2010 a 97 of nought where
        /// it has none, before it none; a spline with fit points keeps its count. The count of a path's source boundary objects
        /// (97 too) is told from it by what follows.
        /// </summary>
        private void FitCounts(Record record)
        {
            List<Group> groups = record.Groups;
            bool want = _target >= 1024;
            int paths = 0;
            int at = 0;

            for (int i = 1; i < groups.Count && at == 0; i++)
            {
                if (groups[i].Code == 91 && Number(groups[i].Value, out paths))
                {
                    at = i + 1;
                }
            }

            if (paths <= 0 || at == 0)
            {
                return;
            }

            var made = new List<Group>(groups.Count + 8);
            made.AddRange(groups.GetRange(0, at));

            for (int p = 0; p < paths; p++)
            {
                if (at >= groups.Count || groups[at].Code != 92 || !Number(groups[at].Value, out int flags))
                {
                    return;
                }

                made.Add(groups[at++]);

                if ((flags & 2) != 0)
                {
                    // A polyline: its flags (72, 73), its count of vertices (93), and the vertices, each with its bulge (42) if it has one.
                    while (at < groups.Count && groups[at].Code != 97)
                    {
                        made.Add(groups[at++]);
                    }
                }
                else
                {
                    if (at >= groups.Count || groups[at].Code != 93 || !Number(groups[at].Value, out int edges))
                    {
                        return;
                    }

                    made.Add(groups[at++]);

                    for (int e = 0; e < edges; e++)
                    {
                        if (at >= groups.Count || groups[at].Code != 72 || !Number(groups[at].Value, out int kind))
                        {
                            return;
                        }

                        made.Add(groups[at++]);
                        int start = at;

                        while (
                            at < groups.Count && groups[at].Code != 72 && !IsSources(groups, at, kind, e == edges - 1)
                        )
                        {
                            at++;
                        }

                        if (kind == 4)
                        {
                            made.AddRange(FitCount(groups.GetRange(start, at - start), want));
                        }
                        else
                        {
                            made.AddRange(groups.GetRange(start, at - start));
                        }
                    }
                }

                // The path's own source boundary objects: their count (97) and the pointers (330) after it.
                if (at < groups.Count && groups[at].Code == 97)
                {
                    made.Add(groups[at]);
                    Number(groups[at++].Value, out int sources);

                    for (int s = 0; s < sources && at < groups.Count && groups[at].Code == 330; s++)
                    {
                        made.Add(groups[at++]);
                    }
                }
            }

            made.AddRange(groups.GetRange(at, groups.Count - at));
            record.Groups = made;
        }

        /// <summary>
        /// Whether a 97 after the last edge of a path is the path's own count of source objects and not a spline's count of
        /// fit points, which comes before its fit points (11) and tangents (12, 13): the count of an edge that is not a spline
        /// always is; that of a spline is a nought followed by the path's 97 or a tangent, or a count of one or more followed
        /// by the fit points.
        /// </summary>
        private static bool IsSources(List<Group> groups, int at, int kind, bool last)
        {
            if (!last || groups[at].Code != 97)
            {
                return false;
            }

            if (kind != 4 || !Number(groups[at].Value, out int count))
            {
                return true;
            }

            int next = at + 1 < groups.Count ? groups[at + 1].Code : -1;
            return count > 0 ? next != 11 : next != 97 && next != 12 && next != 13;
        }

        /// <summary>Gives the groups of a spline edge the fit count the version writes, before its tangents (12, 13) or at its end; one with fit points as it is.</summary>
        private static List<Group> FitCount(List<Group> edge, bool want)
        {
            if (edge.Exists(g => g.Code == 11))
            {
                return edge;
            }

            int count = edge.FindIndex(g => g.Code == 97);

            if (count >= 0)
            {
                if (!want)
                {
                    edge.RemoveAt(count);
                }
            }
            else if (want)
            {
                int tangent = edge.FindIndex(g => g.Code == 12 || g.Code == 13);
                edge.Insert(tangent < 0 ? edge.Count : tangent, new Group(97, "0"));
            }

            return edge;
        }

        private static bool Number(string text, out int value) =>
            int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        #endregion

        #region Text

        /// <summary>
        /// Gives an MTEXT of R2018 in columns, whose columns are an embedded object after its text, the form of R2013 and
        /// before: the columns' type, count, width, gutter and heights in its extended data of ACAD
        /// (<c>ACAD_MTEXT_COLUMN_INFO_BEGIN</c>), the embedded object out. An MTEXT with no embedded object as it is.
        /// </summary>
        private void Columns(Record record)
        {
            List<Group> groups = record.Groups;

            if (!Subclass(groups, "AcDbMText", out int start, out int end))
            {
                return;
            }

            int embedded = groups.FindIndex(start, end - start, g => g.Code == 101);

            if (embedded < 0)
            {
                return;
            }

            string type = "2";
            string count = "1";
            string width = "0.0";
            string gutter = "0.0";
            string auto = "0";
            string reversed = "0";
            var heights = new List<string>();

            for (int i = embedded + 1; i < end; i++)
            {
                switch (groups[i].Code)
                {
                    case 71:
                        type = groups[i].Value.Trim();
                        break;
                    case 72:
                        count = groups[i].Value.Trim();
                        break;
                    case 44:
                        width = groups[i].Value;
                        break;
                    case 45:
                        gutter = groups[i].Value;
                        break;
                    case 73:
                        auto = groups[i].Value.Trim();
                        break;
                    case 74:
                        reversed = groups[i].Value.Trim();
                        break;
                    case 46:
                        heights.Add(groups[i].Value);
                        break;
                }
            }

            var info = new List<Group>
            {
                new Group(1000, "ACAD_MTEXT_COLUMN_INFO_BEGIN"),
                Whole(1070, 75),
                new Group(1070, type.PadLeft(_padded ? 6 : 0)),
                Whole(1070, 79),
                new Group(1070, auto.PadLeft(_padded ? 6 : 0)),
                Whole(1070, 76),
                new Group(1070, count.PadLeft(_padded ? 6 : 0)),
                Whole(1070, 78),
                new Group(1070, reversed.PadLeft(_padded ? 6 : 0)),
                Whole(1070, 48),
                new Group(1040, width),
                Whole(1070, 49),
                new Group(1040, gutter),
                Whole(1070, 50),
                Whole(1070, heights.Count),
            };

            foreach (string height in heights)
            {
                info.Add(new Group(1040, height));
            }

            info.Add(new Group(1000, "ACAD_MTEXT_COLUMN_INFO_END"));
            groups.RemoveRange(embedded, end - embedded);
            int app = groups.FindIndex(g => g.Code == 1001 && g.Value == "ACAD");

            if (app < 0)
            {
                groups.Add(new Group(1001, "ACAD"));
                groups.AddRange(info);
                return;
            }

            int next = groups.FindIndex(app + 1, g => g.Code == 1001);
            groups.InsertRange(next < 0 ? groups.Count : next, info);
        }

        /// <summary>
        /// Gives an attribute of R2018 of several lines, whose text is an MTEXT embedded in it, the form of R2013 and before: its
        /// text in the text of the attribute itself, its type (71), alignment and embedded object out. An attribute with no
        /// embedded object as it is.
        /// </summary>
        private void SingleLine(Record record)
        {
            List<Group> groups = record.Groups;
            string subclass = record.Type == "ATTRIB" ? "AcDbAttribute" : "AcDbAttributeDefinition";

            if (!Subclass(groups, subclass, out int start, out int end))
            {
                return;
            }

            int embedded = groups.FindIndex(start, end - start, g => g.Code == 101);

            if (embedded < 0)
            {
                return;
            }

            var text = new StringBuilder();
            string last = null;

            for (int i = embedded + 1; i < end; i++)
            {
                if (groups[i].Code == 3)
                {
                    text.Append(groups[i].Value);
                }
                else if (groups[i].Code == 1)
                {
                    last = groups[i].Value;
                }
            }

            if (last != null && Subclass(groups, "AcDbText", out int textStart, out int textEnd))
            {
                int main = groups.FindIndex(textStart, textEnd - textStart, g => g.Code == 1);

                if (main >= 0)
                {
                    groups[main] = new Group(1, text.Append(last).ToString());
                }
            }

            int type = groups.FindIndex(start, embedded - start, g => g.Code == 71);
            groups.RemoveRange(type >= 0 ? type : embedded, end - (type >= 0 ? type : embedded));
        }

        /// <summary>
        /// Writes the paragraphs of an MTEXT the way R2000 does: its text (1, and 3 for what runs past 250 letters) holds the end
        /// of a paragraph as <c>\P</c>, where R2004 and later write the line feed, as <c>^J</c>.
        /// </summary>
        private void Paragraphs(Record record)
        {
            List<Group> groups = record.Groups;

            for (int i = 1; i < groups.Count; i++)
            {
                Group group = groups[i];

                if (group.Code == 1001)
                {
                    return;
                }

                if ((group.Code == 1 || group.Code == 3) && group.Value.IndexOf("^J", StringComparison.Ordinal) >= 0)
                {
                    groups[i] = new Group(group.Code, group.Value.Replace("^J", "\\P"));
                }
            }
        }

        /// <summary>
        /// Gives an MTEXT the extended data R2000 writes of every one (application ACAD, <c>MTEXTBEGIN</c> to <c>MTEXTEND</c>): its line
        /// spacing style (73) and factor (44) again, and a 74 of nought. They stand in the application the MTEXT already has data of, after the data of its columns, or in one made, after the groups of
        /// the entity and before the data of the other applications.
        /// </summary>
        private void MTextData(Record record)
        {
            List<Group> groups = record.Groups;

            if (
                groups.Exists(g => g.Code == 1000 && g.Value == "MTEXTBEGIN")
                || !Subclass(groups, "AcDbMText", out int start, out int end)
            )
            {
                return;
            }

            int style = groups.FindIndex(start, end - start, g => g.Code == 73);
            int factor = groups.FindIndex(start, end - start, g => g.Code == 44);
            var data = new List<Group>
            {
                new Group(1000, "MTEXTBEGIN"),
                Whole(1070, 73),
                Whole(
                    1070,
                    style >= 0
                    && int.TryParse(
                        groups[style].Value.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int spacing
                    )
                        ? spacing
                        : 1
                ),
                Whole(1070, 44),
                new Group(1040, factor < 0 ? "1.0" : groups[factor].Value.Trim()),
                Whole(1070, 74),
                Whole(1070, 0),
                new Group(1000, "MTEXTEND"),
            };
            data.AddRange(BackgroundData(groups, start, end));
            int app = groups.FindIndex(g => g.Code == 1001 && g.Value == "ACAD");

            if (app >= 0)
            {
                // After the data of the columns, which AutoCAD writes before it.
                int at = app + 1;
                int stop = groups.FindIndex(app + 1, g => g.Code == 1001);

                for (int i = app + 1; i < (stop < 0 ? groups.Count : stop); i++)
                {
                    if (
                        groups[i].Code == 1000
                        && (
                            groups[i].Value == "ACAD_MTEXT_COLUMN_INFO_END"
                            || groups[i].Value == "ACAD_MTEXT_COLUMNS_END"
                        )
                    )
                    {
                        at = i + 1;
                    }
                }

                groups.InsertRange(at, data);
                return;
            }

            data.Insert(0, new Group(1001, "ACAD"));
            int first = groups.FindIndex(g => g.Code == 1001);
            groups.InsertRange(first < 0 ? groups.Count : first, data);
        }

        /// <summary>Gives an MTEXT going to R2007 or later the height of its column (46) AutoCAD writes after its width (41), a nought where it has none.</summary>
        private static void ColumnHeight(Record record)
        {
            List<Group> groups = record.Groups;

            if (
                Subclass(groups, "AcDbMText", out int start, out int end)
                && !groups.Exists(g => g.Code == 46)
                && groups.FindIndex(start, end - start, g => g.Code == 41) is int width
                && width >= 0
            )
            {
                groups.Insert(width + 1, new Group(46, "0.0"));
            }
        }

        /// <summary>
        /// The extended data R2000 keeps of the background of an MTEXT (<c>ACAD_MTEXT_BBRT</c> to <c>ACAD_MTEXT_BERT</c>), where R2004
        /// and later have the groups 90 (the flags, 91 here), 45 (the scale, 46), 63 and 421 (the colour, 64 as one number: 0xC3 in the
        /// top byte and the index, or 0xC2 and the red, green and blue), an empty colour name (65) and book name (66), and 441 (the
        /// transparency, 442). No data where the MTEXT has no background (no 90).
        /// </summary>
        private List<Group> BackgroundData(List<Group> groups, int start, int end)
        {
            var data = new List<Group>();
            int flags = groups.FindIndex(start, end - start, g => g.Code == 90);

            if (flags < 0)
            {
                return data;
            }

            int Find(int code) => groups.FindIndex(start, end - start, g => g.Code == code);

            int scale = Find(45);
            int index = Find(63);
            int rgb = Find(421);
            int transparency = Find(441);
            int.TryParse(
                index < 0 ? "0" : groups[index].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int aci
            );
            int colour =
                rgb >= 0
                && int.TryParse(
                    groups[rgb].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int value
                )
                    ? unchecked((int)(0xC2000000u | (uint)value))
                    : unchecked((int)(0xC3000000u | (uint)aci));

            data.Add(new Group(1000, "ACAD_MTEXT_BBRT"));
            data.Add(Whole(1070, 91));
            data.Add(Whole(1071, WholeValue(groups[flags].Value)));
            data.Add(Whole(1070, 46));
            data.Add(new Group(1040, scale < 0 ? "1.5" : groups[scale].Value.Trim()));
            data.Add(Whole(1070, 64));
            data.Add(Whole(1071, colour));
            data.Add(Whole(1070, 65));
            data.Add(new Group(1000, string.Empty));
            data.Add(Whole(1070, 66));
            data.Add(new Group(1000, string.Empty));
            data.Add(Whole(1070, 442));
            data.Add(Whole(1071, transparency < 0 ? 0 : WholeValue(groups[transparency].Value)));
            data.Add(new Group(1000, "ACAD_MTEXT_BERT"));
            return data;
        }

        /// <summary>
        /// Takes the extended data of R2000 that repeat the line spacing of an MTEXT (<c>MTEXTBEGIN</c> to <c>MTEXTEND</c>) out, and
        /// the application they are the only data of; the background it keeps in the data <c>ACAD_MTEXT_BBRT</c> comes out as the
        /// groups of R2004 and later, as <see cref="BackgroundData"/> made it.
        /// </summary>
        private void NoMTextData(Record record)
        {
            List<Group> groups = record.Groups;
            int background = groups.FindIndex(g => g.Code == 1000 && g.Value == "ACAD_MTEXT_BBRT");

            if (background >= 0)
            {
                BackgroundUp(record, background);
            }

            int begin = groups.FindIndex(g => g.Code == 1000 && g.Value == "MTEXTBEGIN");
            int end = begin < 0 ? -1 : groups.FindIndex(begin, g => g.Code == 1000 && g.Value == "MTEXTEND");

            if (end < 0)
            {
                return;
            }

            groups.RemoveRange(begin, end - begin + 1);

            if (groups[begin - 1].Code == 1001 && (begin == groups.Count || groups[begin].Code == 1001))
            {
                groups.RemoveAt(begin - 1);
            }
        }

        /// <summary>
        /// Turns the background an MTEXT of R2000 keeps in extended data into its groups (90, 63, 421 where the colour is a true
        /// one, 45 and 441, at the end of the MTEXT's own), and takes the data out, the application with it where it holds no more.
        /// </summary>
        private void BackgroundUp(Record record, int begin)
        {
            List<Group> groups = record.Groups;
            int end = groups.FindIndex(begin, g => g.Code == 1000 && g.Value == "ACAD_MTEXT_BERT");

            if (end < 0 || !Subclass(groups, "AcDbMText", out _, out int stop))
            {
                return;
            }

            // The values, each after the number (1070) that names it.
            string Of(int name)
            {
                for (int i = begin + 1; i + 1 < end; i++)
                {
                    if (groups[i].Code == 1070 && groups[i].Value.Trim() == name.ToString(CultureInfo.InvariantCulture))
                    {
                        return groups[i + 1].Value.Trim();
                    }
                }

                return null;
            }

            string flags = Of(91);
            string colour = Of(64);

            if (
                flags == null
                || colour == null
                || !int.TryParse(colour, NumberStyles.Integer, CultureInfo.InvariantCulture, out int packed)
            )
            {
                return;
            }

            uint bits = unchecked((uint)packed);
            var made = new List<Group> { Whole(90, WholeValue(flags)) };

            if ((bits >> 24) == 0xC2)
            {
                // A true colour: group 63 holds the index nearest it, as AutoCAD writes it (9 for the grey of the window's
                // background 0xC8C8C8, 102 for 0x0AC81E).
                made.Add(Whole(63, DxfColor.FromRgb((byte)(bits >> 16), (byte)(bits >> 8), (byte)bits).ColorIndex));
                made.Add(Whole(421, (int)(bits & 0xFFFFFF)));
            }
            else
            {
                made.Add(Whole(63, (int)(bits & 0xFFFF)));
            }

            made.Add(new Group(45, Of(46) ?? "1.5"));
            made.Add(Whole(441, WholeValue(Of(442) ?? "0")));
            groups.RemoveRange(begin, end - begin + 1);

            if (begin > 0 && groups[begin - 1].Code == 1001 && (begin == groups.Count || groups[begin].Code == 1001))
            {
                groups.RemoveAt(begin - 1);
            }

            groups.InsertRange(stop, made);
        }

        /// <summary>Takes the group 280 R2010 and later write first in an attribute, before its tag (2), out.</summary>
        private void AttributeVersion(Record record)
        {
            List<Group> groups = record.Groups;
            string subclass = record.Type == "ATTRIB" ? "AcDbAttribute" : "AcDbAttributeDefinition";

            if (
                Subclass(groups, subclass, out int start, out _)
                && start + 2 < groups.Count
                && groups[start + 1].Code == 280
                && (groups[start + 2].Code == 2 || groups[start + 2].Code == 3)
            )
            {
                groups.RemoveAt(start + 1);
            }
        }

        /// <summary>Gives an arc dimension the kind the version writes in its group 70: 5, and 8 from R2018.</summary>
        private void ArcKind(Record record)
        {
            List<Group> groups = record.Groups;

            if (!Subclass(groups, "AcDbDimension", out int start, out int end))
            {
                return;
            }

            int at = groups.FindIndex(start, end - start, g => g.Code == 70);

            if (at >= 0 && Number(groups[at].Value, out int kind))
            {
                groups[at] = Whole(70, (kind & ~15) | (_target >= 1032 ? 8 : 5));
            }
        }

        #endregion

        #region Dimensions

        /// <summary>Whether a kind keeps the overrides of its dimension style in its extended data: a dimension, a leader or a tolerance.</summary>
        private static bool HasStyleOverrides(string type) =>
            type == "DIMENSION"
            || type == "ARC_DIMENSION"
            || type == "LARGE_RADIAL_DIMENSION"
            || type == "LEADER"
            || type == "TOLERANCE";

        /// <summary>
        /// Takes the overrides of a dimension style (ACAD, DSTYLE) of the variables a later version brought out of the extended
        /// data, each a 1070 naming the variable and the value after it, as AutoCAD's SaveAs leaves them out.
        /// </summary>
        private void DimensionOverrides(Record record)
        {
            List<Group> groups = record.Groups;
            int dstyle = -1;

            for (int i = 1; i + 1 < groups.Count; i++)
            {
                if (groups[i].Code == 1000 && groups[i].Value == "DSTYLE" && groups[i + 1].Code == 1002)
                {
                    dstyle = i + 2;
                    break;
                }
            }

            if (dstyle < 0)
            {
                return;
            }

            List<Group> kept = groups.GetRange(0, dstyle);
            bool changed = false;
            int at = dstyle;

            while (at < groups.Count && groups[at].Code != 1002)
            {
                Group group = groups[at];

                // A 1070 names a variable and the group after it holds its value; any other group stands by itself.
                if (group.Code != 1070 || at + 1 >= groups.Count)
                {
                    kept.Add(group);
                    at++;
                    continue;
                }

                DxfDimVariable variable = Number(group.Value, out int code) ? DxfDimVariable.OfCode(code, false) : null;

                if (variable != null && variable.Since > _target)
                {
                    changed = true;
                }
                else
                {
                    kept.Add(group);
                    kept.Add(groups[at + 1]);
                }

                at += 2;
            }

            if (changed)
            {
                kept.AddRange(groups.GetRange(at, groups.Count - at));
                record.Groups = kept;
            }
        }
        #endregion

        #region What a version holds that the record has not

        /// <summary>Gives a block record its unit (none), that it may be exploded and its scaling, which AutoCAD writes from R2007.</summary>
        private void SpaceSettings(Record record)
        {
            List<Group> groups = record.Groups;

            if (
                !Subclass(groups, "AcDbBlockTableRecord", out int start, out int end)
                || groups.Exists(g => g.Code == 280)
            )
            {
                return;
            }

            groups.InsertRange(end, new[] { Whole(70, 0), Whole(280, 1), Whole(281, 0) });
        }

        /// <summary>Gives a layout's page set-up its shade plot as displayed (76), at normal quality (77) and 300 dots an inch (78), as AutoCAD writes it from R2004.</summary>
        private void ShadePlot(Record record)
        {
            List<Group> groups = record.Groups;

            if (!Subclass(groups, "AcDbPlotSettings", out int start, out int end))
            {
                return;
            }

            if (groups.FindIndex(start, end - start, g => g.Code == 76) >= 0)
            {
                return;
            }

            int after = end;

            for (int i = start; i < end; i++)
            {
                if (groups[i].Code == 147)
                {
                    after = i + 1;
                }
            }

            groups.InsertRange(after, new[] { Whole(76, 0), Whole(77, 2), Whole(78, 300) });
        }

        #endregion
    }
}
