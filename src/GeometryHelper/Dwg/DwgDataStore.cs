using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The data store of a DWG of R2013 and later (the section AcDb:AcDsPrototype_1b), which holds the ACIS data of the
    /// REGION, 3DSOLID and BODY entities as SAB, one record an entity by its handle. The store is bytes, not bits: a head
    /// naming the segment of the index of segments, then segments each with a head of 0x30 bytes. The index of data
    /// gives for each record its data segment and where its head stands; the head gives the handle and where its bytes
    /// stand after the heads; the bytes are a size and that many, or past 0x40000 bytes a reference to pages in segments
    /// of their own (blob01). The layout follows the Open Design Specification for .dwg files, chapter 24.
    /// </summary>
    internal sealed class DwgDataStore
    {
        /// <summary>The size given in place of a record's size where its bytes are in pages of their own.</summary>
        private const uint BlobMark = 0xBB106BB1;

        private readonly byte[] _store;

        private DwgDataStore(byte[] store)
        {
            _store = store;
        }

        /// <summary>The version of the store, group 70 of the DXF section ACDSDATA (2).</summary>
        public int Version { get; private set; }

        /// <summary>The value after the size of the head, group 71 of the DXF section (8).</summary>
        public int Flags { get; private set; }

        /// <summary>The records of ACIS data, each the handle of its entity and its SAB bytes, in the order of the index.</summary>
        public List<KeyValuePair<ulong, byte[]>> Records { get; } = new List<KeyValuePair<ulong, byte[]>>();

        /// <summary>
        /// Reads the store of <paramref name="store"/>. Throws <see cref="FormatException"/> where it is not a store or
        /// names data beyond its end.
        /// </summary>
        public static DwgDataStore Read(byte[] store)
        {
            var read = new DwgDataStore(store);

            if (store.Length < 0x38 || Int(store, 0) != 0x6472616A)
            {
                throw new FormatException("The data store of the DWG has no head.");
            }

            read.Flags = Int(store, 8);
            read.Version = Int(store, 12);
            long indexAt = Int(store, 24);
            int count = Int(store, 32);
            int dataIndex = Int(store, 40);
            read.Need(indexAt + 0x30, count * 12L);
            var segments = new long[count];

            for (int i = 0; i < count; i++)
            {
                segments[i] = (long)BitConverter.ToUInt64(store, (int)(indexAt + 0x30 + i * 12));
            }

            if (dataIndex <= 0 || dataIndex >= count)
            {
                return read;
            }

            long at = read.Segment(segments, dataIndex) + 0x30;
            read.Need(at, 8);
            int entries = Int(store, (int)at);
            read.Need(at + 8, entries * 12L);

            for (int i = 0; i < entries; i++)
            {
                int segment = Int(store, (int)(at + 8 + i * 12));
                long local = (uint)Int(store, (int)(at + 12 + i * 12));

                if (segment == 0)
                {
                    continue;
                }

                long start = read.Segment(segments, segment);
                long head = start + 0x30 + local;
                read.Need(head, 20);
                ulong handle = BitConverter.ToUInt64(store, (int)head + 8);
                long offset = (uint)Int(store, (int)head + 16);
                long data = start + ((long)(uint)Int(store, (int)start + 0x24) << 4) + offset;
                read.Need(data, 4);
                uint size = (uint)Int(store, (int)data);
                byte[] bytes = size == BlobMark ? read.Blob(segments, data + 4) : read.Bytes(data + 4, size);
                read.Records.Add(new KeyValuePair<ulong, byte[]>(handle, bytes));
            }

            return read;
        }

        /// <summary>The bytes of a record kept in pages: the reference to them, then each page in a blob01 segment.</summary>
        private byte[] Blob(long[] segments, long at)
        {
            Need(at, 32);
            long total = (long)BitConverter.ToUInt64(_store, (int)at);
            int pages = Int(_store, (int)at + 8);
            Need(at + 32, pages * 8L);

            if (total < 0 || total > _store.Length)
            {
                throw new FormatException("The data store of the DWG names more data than it holds.");
            }

            var bytes = new byte[total];

            for (int i = 0; i < pages; i++)
            {
                long page = Segment(segments, Int(_store, (int)(at + 32 + i * 8))) + 0x30;
                Need(page, 32);
                long from = (long)BitConverter.ToUInt64(_store, (int)page + 8);
                long size = (long)BitConverter.ToUInt64(_store, (int)page + 24);

                if (from < 0 || size < 0 || from + size > total)
                {
                    throw new FormatException("A page of the data store of the DWG runs past its record.");
                }

                Array.Copy(Bytes(page + 32, size), 0, bytes, from, size);
            }

            return bytes;
        }

        private long Segment(long[] segments, int index)
        {
            if (index <= 0 || index >= segments.Length)
            {
                throw new FormatException("The data store of the DWG names a segment it has not.");
            }

            return segments[index];
        }

        private byte[] Bytes(long at, long size)
        {
            Need(at, size);
            var bytes = new byte[size];
            Array.Copy(_store, at, bytes, 0, size);
            return bytes;
        }

        private void Need(long at, long size)
        {
            if (at < 0 || size < 0 || at + size > _store.Length)
            {
                throw new FormatException("The data store of the DWG names data beyond its end.");
            }
        }

        private static int Int(byte[] bytes, int at) => BitConverter.ToInt32(bytes, at);
    }
}
