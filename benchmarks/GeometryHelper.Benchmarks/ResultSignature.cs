using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// A SHA-256 over every value of a result, written out to the last bit and in order, so that two results sign the
    /// same only when they are the same.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A double goes in as its 64 IEEE-754 bits (<see cref="BitConverter.DoubleToInt64Bits(double)"/>), so 0.1 + 0.2
    /// and 0.3 sign apart, and so do 0.0 and -0.0. Integers, booleans and enumerations go in as they are, and every
    /// list goes in as its length and then its elements in their order: two triangles swapped, or a point moved from
    /// one list to the next, change the signature. Nothing goes through <see cref="object.GetHashCode"/>, whose value
    /// is not promised from one run to the next.
    /// </para>
    /// <para>
    /// The bytes are little-endian whatever the machine, as <see cref="BinaryWriter"/> writes them.
    /// </para>
    /// </remarks>
    internal sealed class ResultSignature : IDisposable
    {
        private readonly SHA256 _hash;
        private readonly CryptoStream _stream;
        private readonly BinaryWriter _writer;
        private bool _finished;

        /// <summary>
        /// Initializes an empty signature.
        /// </summary>
        internal ResultSignature()
        {
            _hash = SHA256.Create();
            _stream = new CryptoStream(Stream.Null, _hash, CryptoStreamMode.Write);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, true);
        }

        /// <summary>Adds a double, by its bits.</summary>
        internal void Add(double value) => _writer.Write(BitConverter.DoubleToInt64Bits(value));

        /// <summary>Adds an integer: a count, an index, or an enumeration's value.</summary>
        internal void Add(int value) => _writer.Write(value);

        /// <summary>Adds a boolean, as one byte, 1 for true.</summary>
        internal void Add(bool value) => _writer.Write((byte)(value ? 1 : 0));

        /// <summary>Adds a text, by its length in UTF-8 bytes and then the bytes; null signs apart from empty.</summary>
        internal void Add(string value)
        {
            Add(value != null);

            if (value != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                Add(bytes.Length);
                _writer.Write(bytes);
            }
        }

        /// <summary>Adds a point in the plane: X, then Y.</summary>
        internal void Add(GeoPoint2 point)
        {
            Add(point.X);
            Add(point.Y);
        }

        /// <summary>Adds a vector in the plane: X, then Y.</summary>
        internal void Add(GeoVector2 vector)
        {
            Add(vector.X);
            Add(vector.Y);
        }

        /// <summary>Adds a point in space: X, Y, then Z.</summary>
        internal void Add(GeoPoint3 point)
        {
            Add(point.X);
            Add(point.Y);
            Add(point.Z);
        }

        /// <summary>Adds a segment: its start, then its end.</summary>
        internal void Add(GeoLine3 line)
        {
            Add(line.StartPoint);
            Add(line.EndPoint);
        }

        /// <summary>Adds a triangle: A, B, then C.</summary>
        internal void Add(GeoTriangle3 triangle)
        {
            Add(triangle.A);
            Add(triangle.B);
            Add(triangle.C);
        }

        /// <summary>Adds a polygon: how many corners, then each in order.</summary>
        internal void Add(GeoPolygon3 polygon)
        {
            IReadOnlyList<GeoPoint3> vertices = polygon.Vertices;
            Add(vertices.Count);

            foreach (GeoPoint3 vertex in vertices)
            {
                Add(vertex);
            }
        }

        /// <summary>Adds a face: its boundary, then how many holes, then each in order.</summary>
        internal void Add(GeoFace3 face)
        {
            Add(face.Boundary);
            Add(face.Holes.Count);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                Add(hole);
            }
        }

        /// <summary>Adds a solid: how many faces, then each in order, then how many openings, then each in order.</summary>
        internal void Add(GeoSolid3 solid)
        {
            Add(solid.Faces.Count);

            foreach (GeoFace3 face in solid.Faces)
            {
                Add(face);
            }

            Add(solid.Openings.Count);

            foreach (GeoSolid3 opening in solid.Openings)
            {
                Add(opening);
            }
        }

        /// <summary>
        /// Ends the signature and gives it.
        /// </summary>
        /// <returns>The SHA-256 of everything added, as 64 lower-case hexadecimal digits.</returns>
        internal string Finish()
        {
            if (_finished)
            {
                throw new InvalidOperationException("A signature is finished once.");
            }

            _finished = true;
            _writer.Flush();
            _stream.FlushFinalBlock();

            var text = new StringBuilder(64);

            foreach (byte b in _hash.Hash)
            {
                text.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        /// <summary>
        /// Releases the hash.
        /// </summary>
        public void Dispose()
        {
            _writer.Dispose();
            _stream.Dispose();
            _hash.Dispose();
        }
    }
}
