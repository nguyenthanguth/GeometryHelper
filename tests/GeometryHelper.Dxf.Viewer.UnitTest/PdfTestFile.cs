using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PDF files written by the tests: objects added one by one and written with a classic cross-reference table, with a
    /// cross-reference stream and an object stream, or with a table that points nowhere. No PDF of anyone's is in the
    /// repository: every file the tests read is built here.
    /// </summary>
    internal sealed class PdfTestFile
    {
        private readonly List<byte[]> _bodies = new List<byte[]>();
        private readonly List<bool> _streams = new List<bool>();

        /// <summary>Adds an object that is no stream, its text as the file holds it; gives its number.</summary>
        internal int Add(string body)
        {
            _bodies.Add(Encoding.ASCII.GetBytes(body));
            _streams.Add(false);
            return _bodies.Count;
        }

        /// <summary>Reserves a number, to be filled by <see cref="Set"/>: for objects that name one another.</summary>
        internal int Reserve() => Add("null");

        /// <summary>Gives the object of a reserved number its text.</summary>
        internal void Set(int number, string body)
        {
            _bodies[number - 1] = Encoding.ASCII.GetBytes(body);
        }

        /// <summary>Adds a stream; the dictionary text without its brackets and /Length; gives its number.</summary>
        internal int AddStream(string dictionary, byte[] data, bool flate = false)
        {
            byte[] stored = data;

            if (flate)
            {
                stored = Zlib(data);
                dictionary += " /Filter /FlateDecode";
            }

            var bytes = new List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("<< " + dictionary + " /Length " + stored.Length + " >>\nstream\n"));
            bytes.AddRange(stored);
            bytes.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
            _bodies.Add(bytes.ToArray());
            _streams.Add(true);
            return _bodies.Count;
        }

        /// <summary>Adds a stream of text.</summary>
        internal int AddStream(string dictionary, string text, bool flate = false) =>
            AddStream(dictionary, Encoding.GetEncoding("iso-8859-1").GetBytes(text), flate);

        /// <summary>Compresses as a zlib stream: its two bytes, the deflated data, the Adler sum.</summary>
        internal static byte[] Zlib(byte[] data)
        {
            var output = new MemoryStream();
            output.WriteByte(0x78);
            output.WriteByte(0x9C);

            using (var deflate = new DeflateStream(output, CompressionMode.Compress, true))
            {
                deflate.Write(data, 0, data.Length);
            }

            uint a = 1;
            uint b = 0;

            foreach (byte v in data)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }

            uint adler = (b << 16) | a;
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
            return output.ToArray();
        }

        /// <summary>
        /// A document of one page: its content and resources; gives the number of the page, with the catalog in
        /// <paramref name="root"/>. Extra text goes into the page's dictionary (a CropBox, a Rotate).
        /// </summary>
        internal int Page(
            string content,
            string resources,
            out int root,
            string box = "[0 0 200 100]",
            string extra = "",
            bool flate = false
        )
        {
            int pages = Reserve();
            int stream = AddStream(string.Empty, content, flate);
            int page = Add(
                "<< /Type /Page /Parent "
                    + pages
                    + " 0 R /MediaBox "
                    + box
                    + " /Contents "
                    + stream
                    + " 0 R /Resources << "
                    + resources
                    + " >> "
                    + extra
                    + " >>"
            );
            Set(pages, "<< /Type /Pages /Count 1 /Kids [" + page + " 0 R] >>");
            root = Add("<< /Type /Catalog /Pages " + pages + " 0 R >>");
            return page;
        }

        /// <summary>Writes the file with a classic cross-reference table.</summary>
        internal byte[] Build(int root, string trailerExtra = "", bool brokenTable = false)
        {
            var output = new MemoryStream();
            Write(output, "%PDF-1.4\n%Ã¢Ã£ÃÃ“\n");
            var offsets = new List<long>();

            for (int i = 0; i < _bodies.Count; i++)
            {
                offsets.Add(output.Position);
                Write(output, (i + 1) + " 0 obj\n");
                output.Write(_bodies[i], 0, _bodies[i].Length);
                Write(output, "\nendobj\n");
            }

            long xref = output.Position;
            Write(output, "xref\n0 " + (_bodies.Count + 1) + "\n0000000000 65535 f \n");

            foreach (long offset in offsets)
            {
                Write(output, (brokenTable ? 7 : offset).ToString("0000000000") + " 00000 n \n");
            }

            Write(
                output,
                "trailer\n<< /Size "
                    + (_bodies.Count + 1)
                    + " /Root "
                    + root
                    + " 0 R "
                    + trailerExtra
                    + " >>\nstartxref\n"
                    + xref
                    + "\n%%EOF\n"
            );
            return output.ToArray();
        }

        /// <summary>Writes the file with its dictionaries in one object stream and a cross-reference stream.</summary>
        internal byte[] BuildCompressed(int root)
        {
            var output = new MemoryStream();
            Write(output, "%PDF-1.5\n");
            var offsets = new long[_bodies.Count + 1];
            var inStream = new List<int>();

            for (int i = 0; i < _bodies.Count; i++)
            {
                if (_streams[i])
                {
                    offsets[i + 1] = output.Position;
                    Write(output, (i + 1) + " 0 obj\n");
                    output.Write(_bodies[i], 0, _bodies[i].Length);
                    Write(output, "\nendobj\n");
                }
                else
                {
                    inStream.Add(i + 1);
                }
            }

            var head = new StringBuilder();
            var body = new StringBuilder();

            foreach (int number in inStream)
            {
                head.Append(number).Append(' ').Append(body.Length).Append(' ');
                body.Append(Encoding.ASCII.GetString(_bodies[number - 1])).Append('\n');
            }

            string headText = head.ToString();
            byte[] objectStream = Zlib(Encoding.ASCII.GetBytes(headText + body));
            int objectStreamNumber = _bodies.Count + 1;
            offsets[0] = 0;
            long streamAt = output.Position;
            Write(
                output,
                objectStreamNumber
                    + " 0 obj\n<< /Type /ObjStm /N "
                    + inStream.Count
                    + " /First "
                    + headText.Length
                    + " /Filter /FlateDecode /Length "
                    + objectStream.Length
                    + " >>\nstream\n"
            );
            output.Write(objectStream, 0, objectStream.Length);
            Write(output, "\nendstream\nendobj\n");

            int xrefNumber = objectStreamNumber + 1;
            long xrefAt = output.Position;
            var table = new List<byte>();

            for (int number = 0; number <= xrefNumber; number++)
            {
                if (number == 0)
                {
                    table.AddRange(new byte[] { 0, 0, 0, 0, 0, 0xFF, 0xFF });
                }
                else if (number == objectStreamNumber)
                {
                    table.AddRange(Entry(1, streamAt, 0));
                }
                else if (number == xrefNumber)
                {
                    table.AddRange(Entry(1, xrefAt, 0));
                }
                else if (inStream.Contains(number))
                {
                    table.AddRange(Entry(2, objectStreamNumber, inStream.IndexOf(number)));
                }
                else
                {
                    table.AddRange(Entry(1, offsets[number], 0));
                }
            }

            byte[] packed = Zlib(table.ToArray());
            Write(
                output,
                xrefNumber
                    + " 0 obj\n<< /Type /XRef /Size "
                    + (xrefNumber + 1)
                    + " /W [1 4 2] /Root "
                    + root
                    + " 0 R /Filter /FlateDecode /Length "
                    + packed.Length
                    + " >>\nstream\n"
            );
            output.Write(packed, 0, packed.Length);
            Write(output, "\nendstream\nendobj\nstartxref\n" + xrefAt + "\n%%EOF\n");
            return output.ToArray();
        }

        private static byte[] Entry(int type, long a, int b) =>
            new[] { (byte)type, (byte)(a >> 24), (byte)(a >> 16), (byte)(a >> 8), (byte)a, (byte)(b >> 8), (byte)b };

        private static void Write(Stream stream, string text)
        {
            byte[] bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>A one-page file of a content and its resources, the simplest of the files the tests read.</summary>
        internal static byte[] OnePage(
            string content,
            string resources = "",
            string box = "[0 0 200 100]",
            string extra = "",
            bool flate = false
        )
        {
            var file = new PdfTestFile();
            file.Page(content, resources, out int root, box, extra, flate);
            return file.Build(root);
        }
    }
}
