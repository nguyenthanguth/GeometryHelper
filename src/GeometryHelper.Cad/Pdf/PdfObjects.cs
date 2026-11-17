using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>Why a PDF cannot be read: a file that is no PDF, an encrypted one, or one cut short beyond repair.</summary>
    internal sealed class PdfException : Exception
    {
        /// <summary>Makes the exception with the reason, in plain words.</summary>
        public PdfException(string message)
            : base(message) { }
    }

    /// <summary>A name object of a PDF, such as <c>/Page</c>, kept apart from a string.</summary>
    internal sealed class PdfName
    {
        public PdfName(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override string ToString() => "/" + Value;

        public override bool Equals(object obj) => obj is PdfName other && other.Value == Value;

        public override int GetHashCode() => Value.GetHashCode();
    }

    /// <summary>An operator of a content stream, or a keyword of the file (<c>obj</c>, <c>endobj</c>, <c>stream</c>, <c>true</c>).</summary>
    internal sealed class PdfKeyword
    {
        public PdfKeyword(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override string ToString() => Value;
    }

    /// <summary>An indirect reference, <c>12 0 R</c>.</summary>
    internal sealed class PdfRef
    {
        public PdfRef(int number, int generation)
        {
            Number = number;
            Generation = generation;
        }

        public int Number { get; }

        public int Generation { get; }

        public override string ToString() => Number + " " + Generation + " R";
    }

    /// <summary>A string object: its bytes as the file writes them, read as text where it is one.</summary>
    internal sealed class PdfString
    {
        public PdfString(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; }

        /// <summary>The bytes read as text: UTF-16 where they start with a byte order mark, else Latin-1.</summary>
        public string Text
        {
            get
            {
                if (Bytes.Length >= 2 && Bytes[0] == 0xFE && Bytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(Bytes, 2, Bytes.Length - 2);
                }

                if (Bytes.Length >= 2 && Bytes[0] == 0xFF && Bytes[1] == 0xFE)
                {
                    return Encoding.Unicode.GetString(Bytes, 2, Bytes.Length - 2);
                }

                if (Bytes.Length >= 3 && Bytes[0] == 0xEF && Bytes[1] == 0xBB && Bytes[2] == 0xBF)
                {
                    return Encoding.UTF8.GetString(Bytes, 3, Bytes.Length - 3);
                }

                return Encoding.GetEncoding("iso-8859-1").GetString(Bytes);
            }
        }
    }

    /// <summary>A dictionary object: its keys without the slash.</summary>
    internal sealed class PdfDict : Dictionary<string, object>
    {
        public PdfDict()
            : base(StringComparer.Ordinal) { }
    }

    /// <summary>A stream object: its dictionary and where its bytes lie in the file, undecoded.</summary>
    internal sealed class PdfStream
    {
        public PdfStream(PdfDict dictionary, byte[] data, int offset, int length)
        {
            Dictionary = dictionary;
            Data = data;
            Offset = offset;
            Length = length;
        }

        public PdfDict Dictionary { get; }

        /// <summary>The bytes of the whole file the stream lies in.</summary>
        public byte[] Data { get; }

        public int Offset { get; }

        public int Length { get; }

        /// <summary>The raw bytes, still encoded by the stream's filters.</summary>
        public byte[] Raw()
        {
            var bytes = new byte[Math.Max(0, Math.Min(Length, Data.Length - Offset))];
            Buffer.BlockCopy(Data, Offset, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>A stream whose bytes are already decoded, as one taken from an object stream or made in code.</summary>
        public static PdfStream Decoded(PdfDict dictionary, byte[] bytes)
        {
            dictionary.Remove("Filter");
            dictionary.Remove("DecodeParms");
            return new PdfStream(dictionary, bytes, 0, bytes.Length);
        }
    }
}
