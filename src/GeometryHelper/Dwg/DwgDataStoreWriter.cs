using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes the data store of a DWG of R2013 and later (the section AcDb:AcDsPrototype_1b) that holds the ACIS data of the
    /// REGION, 3DSOLID and BODY entities, the mirror of <see cref="DwgDataStore"/>. The store is a head and segments, each
    /// with a head of 0x30 bytes and padded with 0x70 to a whole number of 0x80 bytes, laid out as AutoCAD 2021 lays them:
    /// the index of segments (1), the index of data (2), the data (3), the index and the data of the schemas (4 and 5), a
    /// segment for each page of a record past 0x40000 bytes (blob01), and the search segment last. The layout follows the
    /// Open Design Specification for .dwg files, chapter 24, and AutoCAD's own files.
    /// </summary>
    internal static class DwgDataStoreWriter
    {
        /// <summary>The size given in place of a record's size where its bytes are in pages of their own.</summary>
        private const uint BlobMark = 0xBB106BB1;

        /// <summary>The size past which a record's bytes go into pages of their own.</summary>
        private const int BlobFrom = 0x40000;

        /// <summary>The most bytes a page of a record holds.</summary>
        private const int PageMost = 0xFFFB0;

        /// <summary>The segment of the data of the schemas, to which the index of the schemas points.</summary>
        private const int SchemaData = 5;

        /// <summary>The names of the schemas of ACIS data, as the index of the schemas lists them.</summary>
        private static readonly string[] SchemaNames =
        {
            "AcDb3DSolid_ASM_Data",
            "AcDbDs::TreatedAsObjectDataSchema",
            "AcDbDs::LegacySchema",
            "AcDbDs::IndexedPropertySchema",
            "AcDbDs::HandleAttributeSchema",
        };

        /// <summary>The names of the properties of the schemas, which a property gives by its place here.</summary>
        private static readonly string[] PropertyNames =
        {
            "AcDbDs::ID",
            "ASM_Data",
            "AcDbDs::TreatedAsObjectData",
            "AcDbDs::Legacy",
            "AcDs:Indexable",
            "AcDbDs::HandleAttribute",
        };

        /// <summary>
        /// The bytes of the store of <paramref name="records"/>, each the handle of its entity and its SAB bytes, which the
        /// index of data lists in their order. Null where there is no record: AutoCAD writes no store then.
        /// </summary>
        public static byte[] Write(IList<KeyValuePair<ulong, byte[]>> records)
        {
            if (records.Count == 0)
            {
                return null;
            }

            // The pages of each record kept in pages of its own, numbered as the segments after the schemas' data.
            var pages = new List<int[]>();
            var firstPage = new int[records.Count];

            for (int i = 0; i < records.Count; i++)
            {
                byte[] bytes = records[i].Value;
                firstPage[i] = pages.Count;

                if (bytes.Length <= BlobFrom)
                {
                    continue;
                }

                for (int at = 0; at < bytes.Length; at += PageMost)
                {
                    pages.Add(new[] { i, at, Math.Min(PageMost, bytes.Length - at) });
                }
            }

            int search = 6 + pages.Count;
            int count = search + 1;
            var segments = new List<byte[]>
            {
                null,
                null,
                Segment("datidx", 2, DataIndex(records.Count), 0, 0, false),
                Data(records, firstPage, pages),
                Segment("schidx", 4, SchemaIndex(), 0xC, 0, false),
                Segment("schdat", 5, SchemaDataBody(), 0xE, 0, false),
            };

            foreach (int[] page in pages)
            {
                segments.Add(Segment("blob01", segments.Count, Page(records, page, pages), 0, 0, true));
            }

            segments.Add(Segment("search", search, Search(records), 0, 0, false));

            // The index of segments places every segment, itself among them, after the head of 0x80 bytes.
            int indexSize = Round(0x30 + count * 12);
            var offsets = new long[count];
            long next = 0x80;

            for (int i = 1; i < count; i++)
            {
                offsets[i] = next;
                next += i == 1 ? indexSize : segments[i].Length;
            }

            var index = new MemoryStream();

            for (int i = 0; i < count; i++)
            {
                Long64(index, i == 0 ? 0 : offsets[i]);
                Long(
                    index,
                    i == 0 ? 0
                        : i == 1 ? indexSize
                        : segments[i].Length
                );
            }

            segments[1] = Segment("segidx", 1, index.ToArray(), 0, 0, false);

            var store = new MemoryStream();
            var head = new byte[0x80];
            Put(head, 0, 0x6472616A);
            Put(head, 4, 0x80);
            Put(head, 8, 8);
            Put(head, 12, 2);
            Put(head, 20, 2);
            Put(head, 24, 0x80);
            Put(head, 32, count);
            Put(head, 36, 4);
            Put(head, 40, 2);
            Put(head, 44, search);
            Put(head, 52, (int)next);
            store.Write(head, 0, head.Length);

            for (int i = 1; i < count; i++)
            {
                store.Write(segments[i], 0, segments[i].Length);
            }

            return store.ToArray();
        }

        /// <summary>The index of data: for each record the data segment (3), where its head stands and the schema (0).</summary>
        private static byte[] DataIndex(int records)
        {
            var body = new MemoryStream();
            Long(body, records);
            Long(body, 0);

            for (int i = 0; i < records; i++)
            {
                Long(body, 3);
                Long(body, i * 20);
                Long(body, 0);
            }

            return body.ToArray();
        }

        /// <summary>
        /// The data segment: a head of 20 bytes for each record (its size, 1, its handle, where its bytes stand), padded with
        /// 0x62 to the next 16 bytes of the segment, then each record's size and bytes, or for a record in pages the mark
        /// 0xBB106BB1 and the reference to its pages.
        /// </summary>
        private static byte[] Data(IList<KeyValuePair<ulong, byte[]>> records, int[] firstPage, List<int[]> pages)
        {
            var data = new MemoryStream();
            var heads = new MemoryStream();

            for (int i = 0; i < records.Count; i++)
            {
                byte[] bytes = records[i].Value;
                Long(heads, 20);
                Long(heads, 1);
                Long64(heads, (long)records[i].Key);
                Long(heads, (int)data.Position);

                if (bytes.Length <= BlobFrom)
                {
                    Long(data, bytes.Length);
                    data.Write(bytes, 0, bytes.Length);
                    continue;
                }

                int count = (bytes.Length + PageMost - 1) / PageMost;
                Long(data, unchecked((int)BlobMark));
                Long64(data, bytes.Length);
                Long(data, count);
                Long(data, 32 + (count * 8));
                Long(data, Math.Min(PageMost, bytes.Length));
                Long(data, bytes.Length - ((count - 1) * PageMost));
                Long(data, 0);
                Long(data, 0);

                for (int k = 0; k < count; k++)
                {
                    Long(data, 6 + firstPage[i] + k);
                    Long(data, pages[firstPage[i] + k][2]);
                }
            }

            int align = (0x30 + (int)heads.Length + 15) / 16;

            while (0x30 + heads.Length < align * 16)
            {
                heads.WriteByte(0x62);
            }

            heads.Write(data.ToArray(), 0, (int)data.Length);
            return Segment("_data_", 3, heads.ToArray(), 0, align, false);
        }

        /// <summary>A page of a record: the record's size, where the page starts in it, its place and count, its size and bytes.</summary>
        private static byte[] Page(IList<KeyValuePair<ulong, byte[]>> records, int[] page, List<int[]> pages)
        {
            byte[] bytes = records[page[0]].Value;
            int of = pages.FindAll(p => p[0] == page[0]).Count;
            var body = new MemoryStream();
            Long64(body, bytes.Length);
            Long64(body, page[1]);
            Long(body, page[1] / PageMost);
            Long(body, of);
            Long64(body, page[2]);
            body.Write(bytes, page[1], page[2]);
            return body.ToArray();
        }

        /// <summary>
        /// The index of the schemas: the five schemas, each its place and where it stands in the schemas' data, the four
        /// properties that stand before them, and at 0xC0 of the segment the names of the schemas.
        /// </summary>
        private static byte[] SchemaIndex()
        {
            var body = new MemoryStream();
            int[] at = { 0x20, 0x60, 0x72, 0x84, 0x96 };
            Long(body, at.Length);
            Long(body, 0);

            for (int i = 0; i < at.Length; i++)
            {
                Long(body, i);
                Long(body, SchemaData);
                Long(body, at[i]);
            }

            Long64(body, 0x0AF10C);
            int[][] properties = { new[] { 0, 2 }, new[] { 8, 3 }, new[] { 0x10, 4 }, new[] { 0x18, 1 } };
            Long(body, properties.Length);
            Long(body, 0);

            foreach (int[] property in properties)
            {
                Long(body, SchemaData);
                Long(body, property[0]);
                Long(body, property[1]);
            }

            Pad(body, 0xC0 - 0x30, 0x73);
            Names(body, SchemaNames);
            return body.ToArray();
        }

        /// <summary>
        /// The data of the schemas: four properties of 8 bytes, the schema of the records of ACIS data (an ID of 8 bytes
        /// with the values 1 and 2, and the data), the four schemas of one property each, and at 0xE0 of the segment the
        /// names of the properties.
        /// </summary>
        private static byte[] SchemaDataBody()
        {
            var body = new MemoryStream();

            foreach (int flags in new[] { 1, 1, 0, 1 })
            {
                Long(body, 8);
                Long(body, flags);
            }

            Short(body, 2);
            Long64(body, 3);
            Long64(body, 0);
            Short(body, 2);
            Property(body, 0, 0, 10);
            Short(body, 2);
            Long64(body, 1);
            Long64(body, 2);
            Property(body, 0, 1, 15);
            Short(body, 0);

            for (int name = 2; name <= 4; name++)
            {
                Short(body, 0);
                Short(body, 1);
                Property(body, 0, name, 1);
                Short(body, 0);
            }

            Short(body, 0);
            Short(body, 1);
            Property(body, 8, 5, 7);
            Long(body, 1);
            Short(body, 1);
            body.WriteByte(0);
            Pad(body, 0xE0 - 0x30, 0x73);
            Names(body, PropertyNames);
            return body.ToArray();
        }

        /// <summary>
        /// The search segment: one schema, the indexes of the records 0 to n − 1, and the handles in their order, each with
        /// the place of its record in the index of data.
        /// </summary>
        private static byte[] Search(IList<KeyValuePair<ulong, byte[]>> records)
        {
            var body = new MemoryStream();
            Long(body, 1);
            Long(body, 0);
            Long64(body, records.Count);

            for (int i = 0; i < records.Count; i++)
            {
                Long64(body, i);
            }

            var order = new List<int>();

            for (int i = 0; i < records.Count; i++)
            {
                order.Add(i);
            }

            order.Sort(
                (a, b) =>
                {
                    int byHandle = records[a].Key.CompareTo(records[b].Key);
                    return byHandle != 0 ? byHandle : a.CompareTo(b);
                }
            );
            Long64(body, 1);
            Long(body, records.Count);

            foreach (int i in order)
            {
                Long64(body, (long)records[i].Key);
                Long64(body, 1);
                Long64(body, i);
            }

            return body.ToArray();
        }

        /// <summary>
        /// A segment: its head (the signature 0xD5AC, its name, its index, 1 for a page of a record, its size, 1, where the
        /// schemas' data and the records' data stand in sixteens, eight 0x55), its body and the padding of 0x70.
        /// </summary>
        private static byte[] Segment(string name, int index, byte[] body, int system, int data, bool page)
        {
            int size = Round(0x30 + body.Length);
            var segment = new byte[size];
            segment[0] = 0xAC;
            segment[1] = 0xD5;
            Encoding.ASCII.GetBytes(name).CopyTo(segment, 2);
            Put(segment, 8, index);
            Put(segment, 12, page ? 1 : 0);
            Put(segment, 16, page ? 0x30 : size);
            Put(segment, 24, 1);
            Put(segment, 32, system);
            Put(segment, 36, data);

            for (int i = 40; i < 48; i++)
            {
                segment[i] = 0x55;
            }

            body.CopyTo(segment, 0x30);

            for (int i = 0x30 + body.Length; i < size; i++)
            {
                segment[i] = 0x70;
            }

            return segment;
        }

        private static void Property(MemoryStream body, int flags, int name, int type)
        {
            Long(body, flags);
            Long(body, name);
            Long(body, type);
        }

        private static void Names(MemoryStream body, string[] names)
        {
            Long(body, names.Length);

            foreach (string name in names)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(name);
                body.Write(bytes, 0, bytes.Length);
                body.WriteByte(0);
            }
        }

        private static void Pad(MemoryStream body, int to, byte with)
        {
            while (body.Length < to)
            {
                body.WriteByte(with);
            }
        }

        private static int Round(int size) => (size + 0x7F) & ~0x7F;

        private static void Short(MemoryStream body, int value)
        {
            body.WriteByte((byte)value);
            body.WriteByte((byte)(value >> 8));
        }

        private static void Long(MemoryStream body, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                body.WriteByte((byte)(value >> (8 * i)));
            }
        }

        private static void Long64(MemoryStream body, long value)
        {
            for (int i = 0; i < 8; i++)
            {
                body.WriteByte((byte)(value >> (8 * i)));
            }
        }

        private static void Put(byte[] bytes, int at, int value)
        {
            for (int i = 0; i < 4; i++)
            {
                bytes[at + i] = (byte)(value >> (8 * i));
            }
        }
    }
}
