using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region The round trip of a dynamic block in R2007

        /// <summary>The layer AutoCAD draws the dimensions of the constraints of a dynamic block on.</summary>
        private const string ConstraintLayer = "*ADSK_CONSTRAINTS";

        /// <summary>
        /// Gives a drawing going down to R2007 the two records AutoCAD 2021 keeps for what that version has no place for, in
        /// an extension dictionary under <c>ACAD_XREC_ROUNDTRIP</c> as an XRECORD: the identifiers of the vertices of a
        /// LWPOLYLINE (<c>VERTEXIDENTIFIER</c>: the count of the whole numbers that follow, itself one, then each identifier, as
        /// bytes) and, on each dimension of the constraints of a dynamic block, <c>ISDYNAMIC</c> with a 1. Such a dimension is
        /// made invisible and loses its text, as AutoCAD does; the record brings it back. The constraint groups of the drawing
        /// need both to be kept in a DWG of R2007 (see <c>DwgObjectWriter</c>). A polyline whose identifiers are all 0, and a
        /// dimension that is not of the constraints, are not given a record.
        /// </summary>
        private void RoundTripOf2007()
        {
            if (_target != 1021 || _source <= 1021)
            {
                return;
            }

            foreach (string name in new[] { "BLOCKS", "ENTITIES" })
            {
                Section section = Find(name);

                if (section == null)
                {
                    continue;
                }

                foreach (Record record in section.Records)
                {
                    if (record.Dropped)
                    {
                        continue;
                    }

                    string type = record.Type;

                    if (type == "LWPOLYLINE")
                    {
                        VertexIdentifiers(record);
                    }
                    else if (type == "DIMENSION")
                    {
                        DynamicDimension(record);
                    }
                }
            }
        }

        /// <summary>The identifiers of the vertices of a LWPOLYLINE (group 91 after each vertex, 0 for a vertex with none) kept in a record.</summary>
        private void VertexIdentifiers(Record polyline)
        {
            int start = polyline.Groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbPolyline");

            if (start < 0)
            {
                return;
            }

            var ids = new List<int>();
            bool any = false;

            for (int i = start + 1; i < polyline.Groups.Count && polyline.Groups[i].Code < 1000; i++)
            {
                Group group = polyline.Groups[i];

                if (group.Code == 10)
                {
                    ids.Add(0);
                }
                else if (
                    group.Code == 91
                    && ids.Count > 0
                    && int.TryParse(group.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                )
                {
                    ids[ids.Count - 1] = id;
                    any |= id != 0;
                }
            }

            if (!any)
            {
                return;
            }

            var bytes = new byte[(ids.Count + 1) * 4];
            BitConverter.GetBytes(ids.Count + 1).CopyTo(bytes, 0);

            for (int i = 0; i < ids.Count; i++)
            {
                BitConverter.GetBytes(ids[i]).CopyTo(bytes, (i + 1) * 4);
            }

            var data = new List<Group> { new Group(102, "VERTEXIDENTIFIER") };

            // A group of bytes holds at most 127 of them.
            for (int at = 0; at < bytes.Length; at += 127)
            {
                var hex = new StringBuilder();

                for (int i = at; i < Math.Min(at + 127, bytes.Length); i++)
                {
                    hex.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
                }

                data.Add(new Group(310, hex.ToString()));
            }

            RoundTripRecord(polyline, data);
        }

        /// <summary>A dimension of the constraints of a dynamic block (on its layer, with an association among its reactors) made invisible, without its text, and marked <c>ISDYNAMIC</c>.</summary>
        private void DynamicDimension(Record dimension)
        {
            Group layer = dimension.Groups.Find(g => g.Code == 8);

            if (
                layer.Value == null
                || !string.Equals(layer.Value.Trim(), ConstraintLayer, StringComparison.OrdinalIgnoreCase)
                || !IsReactedByAssociation(dimension)
            )
            {
                return;
            }

            // The text AutoCAD gives the dimension is dropped: the value comes back from the constraint.
            int at = dimension.Groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbDimension");
            int end = at < 0 ? -1 : dimension.Groups.FindIndex(at + 1, g => g.Code == 100);

            for (int i = (end < 0 ? dimension.Groups.Count : end) - 1; at >= 0 && i > at; i--)
            {
                if (dimension.Groups[i].Code == 1)
                {
                    dimension.Groups.RemoveAt(i);
                }
            }

            Invisible(dimension);
            RoundTripRecord(dimension, new List<Group> { new Group(102, "ISDYNAMIC"), Whole(70, 1) });
        }

        /// <summary>Whether a reactor of the record is an association of the dynamic block (a type that starts with <c>ACDBASSOC</c>).</summary>
        private bool IsReactedByAssociation(Record record)
        {
            bool listed = false;

            foreach (Group group in record.Groups)
            {
                if (group.Code == 100)
                {
                    return false;
                }

                if (group.Code == 102)
                {
                    listed = group.Value == "{ACAD_REACTORS";
                }
                else if (
                    group.Code == 330
                    && listed
                    && ObjectsByHandle().TryGetValue(group.Value.Trim(), out Record reactor)
                    && reactor.Type.StartsWith("ACDBASSOC", StringComparison.Ordinal)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Sets the group 60 of an entity to 1, after the groups of its layer, line type, colour, line type scale and line weight.</summary>
        private void Invisible(Record entity)
        {
            int start = entity.Groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbEntity");

            if (start < 0)
            {
                return;
            }

            int stop = entity.Groups.FindIndex(start + 1, g => g.Code == 100);
            stop = stop < 0 ? entity.Groups.Count : stop;
            int place = start + 1;

            for (int i = start + 1; i < stop; i++)
            {
                switch (entity.Groups[i].Code)
                {
                    case 60:
                        entity.Groups[i] = Whole(60, 1);
                        return;
                    case 8:
                    case 6:
                    case 62:
                    case 370:
                    case 48:
                        place = i + 1;
                        break;
                }
            }

            entity.Groups.Insert(place, Whole(60, 1));
        }

        /// <summary>The records of the OBJECTS section by handle, found once.</summary>
        private Dictionary<string, Record> ObjectsByHandle()
        {
            if (_objects == null)
            {
                _objects = new Dictionary<string, Record>(StringComparer.OrdinalIgnoreCase);

                foreach (Record one in Find("OBJECTS")?.Records ?? new List<Record>())
                {
                    string handle = HandleOf(one);

                    if (handle != null)
                    {
                        _objects[handle] = one;
                    }
                }
            }

            return _objects;
        }

        /// <summary>
        /// Puts an XRECORD with <paramref name="data"/> into the extension dictionary of <paramref name="entity"/> under
        /// <c>ACAD_XREC_ROUNDTRIP</c>, the dictionary made where the entity has none; nothing where the entity holds that entry
        /// already, or the drawing has no handle seed.
        /// </summary>
        private void RoundTripRecord(Record entity, List<Group> data)
        {
            string owner = HandleOf(entity);
            int at = entity.Groups.FindIndex(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY");
            Record dictionary = null;

            if (owner == null)
            {
                return;
            }

            if (at >= 0)
            {
                // The entity has an extension dictionary: its round trip joins it, unless it keeps one already.
                if (
                    at + 1 >= entity.Groups.Count
                    || entity.Groups[at + 1].Code != 360
                    || !ObjectsByHandle().TryGetValue(entity.Groups[at + 1].Value.Trim(), out dictionary)
                    || dictionary.Groups.Exists(g => g.Code == 3 && g.Value == "ACAD_XREC_ROUNDTRIP")
                )
                {
                    return;
                }
            }

            string dictionaryHandle = dictionary == null ? NewHandle() : HandleOf(dictionary);
            string xrecordHandle = dictionaryHandle == null ? null : NewHandle();

            if (xrecordHandle == null)
            {
                return;
            }

            var body = new List<Group>
            {
                new Group(0, "XRECORD"),
                new Group(5, xrecordHandle),
                new Group(102, "{ACAD_REACTORS"),
                new Group(330, dictionaryHandle),
                new Group(102, "}"),
                new Group(330, dictionaryHandle),
                new Group(100, "AcDbXrecord"),
                Whole(280, 1),
            };
            body.AddRange(data);
            _added.Add(new Record { Groups = body });

            if (dictionary != null)
            {
                dictionary.Groups.Add(new Group(3, "ACAD_XREC_ROUNDTRIP"));
                dictionary.Groups.Add(new Group(360, xrecordHandle));
                return;
            }

            var made = new Record();
            made.Groups.AddRange(
                new[]
                {
                    new Group(0, "DICTIONARY"),
                    new Group(5, dictionaryHandle),
                    new Group(330, owner),
                    new Group(100, "AcDbDictionary"),
                    Whole(280, 1),
                    Whole(281, 1),
                    new Group(3, "ACAD_XREC_ROUNDTRIP"),
                    new Group(360, xrecordHandle),
                }
            );
            _added.Add(made);
            entity.Groups.InsertRange(
                2,
                new[] { new Group(102, "{ACAD_XDICTIONARY"), new Group(360, dictionaryHandle), new Group(102, "}") }
            );
        }

        #endregion
    }
}
