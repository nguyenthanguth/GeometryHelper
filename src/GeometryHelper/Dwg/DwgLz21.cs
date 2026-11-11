using System;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The decompression of R2007 (AC1021), a variant of LZ77 of its own, as the Open Design Specification for .dwg files
    /// gives it (paragraph 5.10) and the reader of ACadSharp (MIT) reads it: runs of bytes taken from the input, in an order
    /// of their own within each run, alternate with runs copied from earlier in the output. A run that would write past the
    /// output, copy from before its start or read past the end of the input refuses the data, so no hostile length
    /// allocates or loops beyond the bytes of the file.
    /// </summary>
    internal sealed class DwgLz21
    {
        /// <summary>
        /// The order in which a run of 1 to 31 literal bytes is laid into the output: entry n gives, for each byte written,
        /// the index of the input byte it comes from. A run of 32 or more is laid 32 bytes at a time, in four groups of eight
        /// from the last to the first, and its rest by this table.
        /// </summary>
        private static readonly int[][] Orders = MakeOrders(
            "",
            "0",
            "1,0",
            "2,1,0",
            "0,1,2,3",
            "4,0,1,2,3",
            "5,1,2,3,4,0",
            "6,5,1,2,3,4,0",
            "0,1,2,3,4,5,6,7",
            "8,0,1,2,3,4,5,6,7",
            "9,1,2,3,4,5,6,7,8,0",
            "10,9,1,2,3,4,5,6,7,8,0",
            "8,9,10,11,0,1,2,3,4,5,6,7",
            "12,8,9,10,11,0,1,2,3,4,5,6,7",
            "13,9,10,11,12,1,2,3,4,5,6,7,8,0",
            "14,13,9,10,11,12,1,2,3,4,5,6,7,8,0",
            "8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "9,10,11,12,13,14,15,16,8,0,1,2,3,4,5,6,7",
            "17,9,10,11,12,13,14,15,16,1,2,3,4,5,6,7,8,0",
            "18,17,16,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "16,17,18,19,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "20,16,17,18,19,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "21,20,16,17,18,19,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "22,21,20,16,17,18,19,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "16,17,18,19,20,21,22,23,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "17,18,19,20,21,22,23,24,16,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "25,17,18,19,20,21,22,23,24,16,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "26,25,17,18,19,20,21,22,23,24,16,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "24,25,26,27,16,17,18,19,20,21,22,23,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "28,24,25,26,27,16,17,18,19,20,21,22,23,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "29,28,24,25,26,27,16,17,18,19,20,21,22,23,8,9,10,11,12,13,14,15,0,1,2,3,4,5,6,7",
            "30,26,27,28,29,18,19,20,21,22,23,24,25,10,11,12,13,14,15,16,17,2,3,4,5,6,7,8,9,1,0"
        );

        private readonly byte[] _input;
        private readonly long _end;
        private readonly byte[] _output;
        private long _at;
        private int _written;
        private int _op;
        private int _length;
        private int _offset;

        private DwgLz21(byte[] input, long start, long count, byte[] output)
        {
            _input = input;
            _at = start;
            _end = start + count;
            _output = output;
        }

        /// <summary>
        /// Decompresses the <paramref name="count"/> bytes of <paramref name="input"/> from <paramref name="start"/> into
        /// <paramref name="output"/>, from its start; returns the number of bytes written. Throws
        /// <see cref="FormatException"/> where the data would run past either end.
        /// </summary>
        public static int Decompress(byte[] input, long start, long count, byte[] output)
        {
            if (start < 0 || count <= 0 || start + count > input.LongLength)
            {
                throw Broken();
            }

            var lz = new DwgLz21(input, start, count, output);
            lz.Run();
            return lz._written;
        }

        private static FormatException Broken() => new FormatException("A compressed page of the DWG file is broken.");

        private static int[][] MakeOrders(params string[] rows)
        {
            var orders = new int[rows.Length][];

            for (int n = 0; n < rows.Length; n++)
            {
                string[] parts = rows[n].Length == 0 ? new string[0] : rows[n].Split(',');
                orders[n] = new int[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    orders[n][i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
                }
            }

            return orders;
        }

        private void Run()
        {
            _op = Next();

            if (_at >= _end)
            {
                return;
            }

            if ((_op & 0xF0) == 0x20)
            {
                Next();
                Next();
                _length = Next() & 7;
            }

            while (_at < _end)
            {
                if (_length == 0)
                {
                    _length = LiteralLength();
                }

                Literal(_length);

                if (_at >= _end)
                {
                    break;
                }

                CopyRuns();
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

        /// <summary>The length of a run of literal bytes from the code: the code and 8, and where that is 0x17 a byte more, and where that byte is 0xFF shorts more until one that is not 0xFFFF.</summary>
        private int LiteralLength()
        {
            long length = _op + 8;

            if (length == 0x17)
            {
                int n = Next();
                length += n;

                if (n == 0xFF)
                {
                    do
                    {
                        n = Next();
                        n |= Next() << 8;
                        length += n;

                        if (length > _output.Length)
                        {
                            throw Broken();
                        }
                    } while (n == 0xFFFF);
                }
            }

            return (int)length;
        }

        private void Literal(int count)
        {
            if (count > _end - _at || count > _output.Length - _written)
            {
                throw Broken();
            }

            while (count >= 32)
            {
                for (int group = 0; group < 4; group++)
                {
                    Array.Copy(_input, _at + 24 - (group * 8), _output, _written + (group * 8), 8);
                }

                _at += 32;
                _written += 32;
                count -= 32;
            }

            int[] order = Orders[count];

            for (int i = 0; i < count; i++)
            {
                _output[_written + i] = _input[_at + order[i]];
            }

            _at += count;
            _written += count;
        }

        /// <summary>One or more runs copied from earlier in the output, until a code that asks for literal bytes.</summary>
        private void CopyRuns()
        {
            _length = 0;
            _op = Next();
            Instruction();

            while (true)
            {
                Back(_offset, _length);
                _length = _op & 7;

                if (_length != 0 || _at >= _end)
                {
                    break;
                }

                _op = Next();

                if (_op >> 4 == 0)
                {
                    break;
                }

                if (_op >> 4 == 15)
                {
                    _op &= 15;
                }

                Instruction();
            }
        }

        /// <summary>The offset and the length of a run copied from the output, and the next code, from the code just read.</summary>
        private void Instruction()
        {
            switch (_op >> 4)
            {
                case 0:
                    _length = (_op & 0xF) + 0x13;
                    _offset = Next();
                    _op = Next();
                    _length += (_op >> 3) & 0x10;
                    _offset += ((_op & 0x78) << 5) + 1;
                    break;
                case 1:
                    _length = (_op & 0xF) + 3;
                    _offset = Next();
                    _op = Next();
                    _offset += ((_op & 0xF8) << 5) + 1;
                    break;
                case 2:
                    _offset = Next();
                    _offset |= Next() << 8;
                    _length = _op & 7;

                    if ((_op & 8) == 0)
                    {
                        _op = Next();
                        _length += _op & 0xF8;
                    }
                    else
                    {
                        _offset++;
                        _length += Next() << 3;
                        _op = Next();
                        _length += ((_op & 0xF8) << 8) + 0x100;
                    }

                    break;
                default:
                    _length = _op >> 4;
                    _offset = _op & 15;
                    _op = Next();
                    _offset += ((_op & 0xF8) << 1) + 1;
                    break;
            }
        }

        /// <summary>Copies <paramref name="length"/> bytes from <paramref name="offset"/> back in the output; the run may overlap what it writes.</summary>
        private void Back(int offset, int length)
        {
            if (length == 0)
            {
                return;
            }

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
