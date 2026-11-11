using System;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The decompression of the pages of a DWG file of R2004 and later (R2004, R2010, R2013, R2018): a variant of LZ77 in
    /// which runs of bytes copied from the input alternate with runs copied from earlier in the output, as the Open Design
    /// Specification for .dwg files describes it and the reader of ACadSharp (MIT) reads it. The output has the size the
    /// page gives; a run that would write past it, read before its start or past the end of the input refuses the page,
    /// so no hostile length allocates or loops beyond the bytes of the file.
    /// </summary>
    internal sealed class DwgLz77
    {
        private readonly byte[] _input;
        private readonly long _end;
        private readonly byte[] _output;
        private long _at;
        private int _written;

        private DwgLz77(byte[] input, long start, long count, byte[] output)
        {
            _input = input;
            _at = start;
            _end = start + count;
            _output = output;
        }

        /// <summary>
        /// Decompresses the <paramref name="count"/> bytes of <paramref name="input"/> from <paramref name="start"/> into
        /// <paramref name="output"/>, from its start; returns the number of bytes written. Throws
        /// <see cref="FormatException"/> where the data would run past either end or does not end with its closing code.
        /// </summary>
        public static int Decompress(byte[] input, long start, long count, byte[] output)
        {
            if (start < 0 || count < 0 || start + count > input.LongLength)
            {
                throw Broken();
            }

            var lz = new DwgLz77(input, start, count, output);
            lz.Run();
            return lz._written;
        }

        /// <summary>
        /// Compresses the <paramref name="count"/> bytes of <paramref name="input"/> from <paramref name="start"/> into the
        /// form <see cref="Decompress"/> reads, ending with the closing code 0x11 and two bytes of 0 as AutoCAD ends a page.
        /// Runs are found by a table of the last place of each three bytes, looked back at most 0x4000 bytes, the first
        /// match taken; the same bytes always give the same output.
        /// </summary>
        public static byte[] Compress(byte[] input, int start, int count)
        {
            var output = new System.IO.MemoryStream((count / 2) + 16);
            var last = new int[1 << 16];

            for (int i = 0; i < last.Length; i++)
            {
                last[i] = -1;
            }

            int end = start + count;
            int at = start;
            int literalFrom = start;
            bool first = true;
            int pendingLength = 0;
            int pendingOffset = 0;

            while (at < end)
            {
                int length = 0;
                int offset = 0;

                if (at + 3 <= end)
                {
                    int key = Key(input, at);
                    int candidate = last[key];
                    last[key] = at;

                    if (
                        candidate >= start
                        && at - candidate <= 0x4000
                        && input[candidate] == input[at]
                        && input[candidate + 1] == input[at + 1]
                        && input[candidate + 2] == input[at + 2]
                    )
                    {
                        length = 3;

                        while (at + length < end && input[candidate + length] == input[at + length])
                        {
                            length++;
                        }

                        offset = at - candidate;
                    }
                }

                if (length < 3)
                {
                    at++;
                    continue;
                }

                int literals = at - literalFrom;

                if (first)
                {
                    FirstLiteral(output, input, literalFrom, literals);
                    first = false;
                }
                else
                {
                    Match(output, pendingLength, pendingOffset, literals);
                    Literals(output, input, literalFrom, literals);
                }

                pendingLength = length;
                pendingOffset = offset;

                for (int i = at + 1; i < at + length && i + 3 <= end; i++)
                {
                    last[Key(input, i)] = i;
                }

                at += length;
                literalFrom = at;
            }

            if (first)
            {
                FirstLiteral(output, input, start, count);
            }
            else
            {
                Match(output, pendingLength, pendingOffset, end - literalFrom);
                Literals(output, input, literalFrom, end - literalFrom);
            }

            output.WriteByte(0x11);
            output.WriteByte(0);
            output.WriteByte(0);
            return output.ToArray();
        }

        private static int Key(byte[] input, int at) =>
            ((input[at] << 8) ^ (input[at + 1] << 4) ^ input[at + 2] ^ (input[at] >> 4)) & 0xFFFF;

        /// <summary>The run of literal bytes a page starts with: 1 to 3 as 0x12 to 0x14, 4 to 18 as 1 to 15, more as 0 and a long length.</summary>
        private static void FirstLiteral(System.IO.Stream output, byte[] input, int from, int count)
        {
            if (count == 0)
            {
                return;
            }

            if (count <= 3)
            {
                output.WriteByte((byte)(0x11 + count));
            }
            else
            {
                LiteralCode(output, count);
            }

            output.Write(input, from, count);
        }

        /// <summary>The code of a run of 4 or more literal bytes: 1 to 15 for 4 to 18, else 0 and a long length from 15.</summary>
        private static void LiteralCode(System.IO.Stream output, int count)
        {
            if (count <= 18)
            {
                output.WriteByte((byte)(count - 3));
                return;
            }

            output.WriteByte(0);
            LongLength(output, count - 3 - 0x0F);
        }

        /// <summary>A length past the bits of its code: a byte of 0 for each 255, then the rest, 1 to 255.</summary>
        private static void LongLength(System.IO.Stream output, int value)
        {
            while (value > 0xFF)
            {
                output.WriteByte(0);
                value -= 0xFF;
            }

            output.WriteByte((byte)value);
        }

        /// <summary>
        /// A match of <paramref name="length"/> bytes <paramref name="offset"/> back, with the count of the literal bytes
        /// after it in its last two bits where that is 1 to 3: two bytes where it is of 3 to 14 bytes at most 0x400 back,
        /// else the code 0x20 with the length and two bytes of offset.
        /// </summary>
        private static void Match(System.IO.Stream output, int length, int offset, int literals)
        {
            int low = literals >= 1 && literals <= 3 ? literals : 0;
            int back = offset - 1;

            if (length <= 14 && offset <= 0x400)
            {
                output.WriteByte((byte)(((length + 1) << 4) | ((back & 3) << 2) | low));
                output.WriteByte((byte)(back >> 2));
                return;
            }

            if (length - 2 <= 0x1F)
            {
                output.WriteByte((byte)(0x20 | (length - 2)));
            }
            else
            {
                output.WriteByte(0x20);
                LongLength(output, length - 2 - 0x1F);
            }

            output.WriteByte((byte)(((back & 0x3F) << 2) | low));
            output.WriteByte((byte)(back >> 6));
        }

        /// <summary>The literal bytes after a match: of 4 or more their code first; of 1 to 3 the bytes alone, the match holding their count.</summary>
        private static void Literals(System.IO.Stream output, byte[] input, int from, int count)
        {
            if (count >= 4)
            {
                LiteralCode(output, count);
            }

            output.Write(input, from, count);
        }

        private static FormatException Broken() => new FormatException("A compressed page of the DWG file is broken.");

        private void Run()
        {
            int op = Next();

            if (op > 0x11)
            {
                Literal(op - 0x11);
                op = Next();
            }

            if ((op & 0xF0) == 0)
            {
                Literal(LiteralCount(op) + 3);
                op = Next();
            }

            while (op != 0x11)
            {
                int length;
                int offset;

                if (op < 0x10 || op >= 0x40)
                {
                    length = (op >> 4) - 1;
                    int second = Next();
                    offset = (((op >> 2) & 3) | (second << 2)) + 1;
                }
                else if (op < 0x20)
                {
                    length = Extended(op, 7) + 2;
                    offset = (op & 8) << 11;
                    int first = Next();
                    offset |= (first >> 2) | (Next() << 6);
                    offset += 0x4000;
                    op = first;
                }
                else
                {
                    length = Extended(op, 0x1F) + 2;
                    int first = Next();
                    offset = ((first >> 2) | (Next() << 6)) + 1;
                    op = first;
                }

                Back(offset, length);
                int literal = op & 3;

                if (literal == 0)
                {
                    op = Next();

                    if ((op & 0xF0) == 0)
                    {
                        literal = LiteralCount(op) + 3;
                    }
                }

                if (literal > 0)
                {
                    Literal(literal);
                    op = Next();
                }
            }
        }

        private int Next()
        {
            if (_at >= _end)
            {
                throw Broken();
            }

            return _input[_at++];
        }

        /// <summary>A length in the low bits of a code; where they are 0, bytes of 0 each add 255 until one that is not, which adds itself and <paramref name="bits"/>.</summary>
        private int Extended(int code, int bits)
        {
            long value = code & bits;

            if (value == 0)
            {
                int b;

                while ((b = Next()) == 0)
                {
                    value += 0xFF;
                }

                value += b + bits;
            }

            return Fits(value);
        }

        /// <summary>The length of a run of literal bytes, less three: the low four bits of the code, or where they are 0 as <see cref="Extended"/> from 15.</summary>
        private int LiteralCount(int code) => Extended(code, 0x0F);

        /// <summary>A length the output can hold; the input ending first stops an extended length long before it overflows.</summary>
        private int Fits(long value)
        {
            if (value > _output.Length)
            {
                throw Broken();
            }

            return (int)value;
        }

        private void Literal(int count)
        {
            if (count > _end - _at || count > _output.Length - _written)
            {
                throw Broken();
            }

            Array.Copy(_input, _at, _output, _written, count);
            _at += count;
            _written += count;
        }

        /// <summary>Copies <paramref name="length"/> bytes from <paramref name="offset"/> back in the output; the run may overlap what it writes.</summary>
        private void Back(int offset, int length)
        {
            if (offset <= 0 || offset > _written || length < 0 || length > _output.Length - _written)
            {
                throw Broken();
            }

            int from = _written - offset;

            for (int i = 0; i < length; i++)
            {
                _output[_written++] = _output[from + i];
            }
        }
    }
}
