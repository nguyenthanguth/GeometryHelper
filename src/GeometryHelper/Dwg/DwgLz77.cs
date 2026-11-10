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
