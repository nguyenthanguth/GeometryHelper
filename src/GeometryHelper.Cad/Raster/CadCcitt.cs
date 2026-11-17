using System;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The fax coding of ITU-T T.4 and T.6 as a TIFF holds it: modified Huffman runs (compression 2), Group 3 in one
    /// dimension or two with its end-of-line codes (compression 3) and Group 4, two-dimensional with none (compression 4).
    /// It turns the coded bytes of a strip or a tile into rows of bits, 1 for black, each row padded to a whole byte.
    /// </summary>
    internal static class CadCcitt
    {
        // The code of each run of 0 to 63 and of each multiple of 64 up to 1728, white then black, and of the multiples of 64
        // from 1792 to 2560 that both colours share, as ITU-T T.4 lists them.
        private static readonly string[] WhiteTerminating =
        {
            "00110101",
            "000111",
            "0111",
            "1000",
            "1011",
            "1100",
            "1110",
            "1111",
            "10011",
            "10100",
            "00111",
            "01000",
            "001000",
            "000011",
            "110100",
            "110101",
            "101010",
            "101011",
            "0100111",
            "0001100",
            "0001000",
            "0010111",
            "0000011",
            "0000100",
            "0101000",
            "0101011",
            "0010011",
            "0100100",
            "0011000",
            "00000010",
            "00000011",
            "00011010",
            "00011011",
            "00010010",
            "00010011",
            "00010100",
            "00010101",
            "00010110",
            "00010111",
            "00101000",
            "00101001",
            "00101010",
            "00101011",
            "00101100",
            "00101101",
            "00000100",
            "00000101",
            "00001010",
            "00001011",
            "01010010",
            "01010011",
            "01010100",
            "01010101",
            "00100100",
            "00100101",
            "01011000",
            "01011001",
            "01011010",
            "01011011",
            "01001010",
            "01001011",
            "00110010",
            "00110011",
            "00110100",
        };

        private static readonly string[] WhiteMakeUp =
        {
            "11011",
            "10010",
            "010111",
            "0110111",
            "00110110",
            "00110111",
            "01100100",
            "01100101",
            "01101000",
            "01100111",
            "011001100",
            "011001101",
            "011010010",
            "011010011",
            "011010100",
            "011010101",
            "011010110",
            "011010111",
            "011011000",
            "011011001",
            "011011010",
            "011011011",
            "010011000",
            "010011001",
            "010011010",
            "011000",
            "010011011",
        };

        private static readonly string[] BlackTerminating =
        {
            "0000110111",
            "010",
            "11",
            "10",
            "011",
            "0011",
            "0010",
            "00011",
            "000101",
            "000100",
            "0000100",
            "0000101",
            "0000111",
            "00000100",
            "00000111",
            "000011000",
            "0000010111",
            "0000011000",
            "0000001000",
            "00001100111",
            "00001101000",
            "00001101100",
            "00000110111",
            "00000101000",
            "00000010111",
            "00000011000",
            "000011001010",
            "000011001011",
            "000011001100",
            "000011001101",
            "000001101000",
            "000001101001",
            "000001101010",
            "000001101011",
            "000011010010",
            "000011010011",
            "000011010100",
            "000011010101",
            "000011010110",
            "000011010111",
            "000001101100",
            "000001101101",
            "000011011010",
            "000011011011",
            "000001010100",
            "000001010101",
            "000001010110",
            "000001010111",
            "000001100100",
            "000001100101",
            "000001010010",
            "000001010011",
            "000000100100",
            "000000110111",
            "000000111000",
            "000000100111",
            "000000101000",
            "000001011000",
            "000001011001",
            "000000101011",
            "000000101100",
            "000001011010",
            "000001100110",
            "000001100111",
        };

        private static readonly string[] BlackMakeUp =
        {
            "0000001111",
            "000011001000",
            "000011001001",
            "000001011011",
            "000000110011",
            "000000110100",
            "000000110101",
            "0000001101100",
            "0000001101101",
            "0000001001010",
            "0000001001011",
            "0000001001100",
            "0000001001101",
            "0000001110010",
            "0000001110011",
            "0000001110100",
            "0000001110101",
            "0000001110110",
            "0000001110111",
            "0000001010010",
            "0000001010011",
            "0000001010100",
            "0000001010101",
            "0000001011010",
            "0000001011011",
            "0000001100100",
            "0000001100101",
        };

        private static readonly string[] ExtendedMakeUp =
        {
            "00000001000",
            "00000001100",
            "00000001101",
            "000000010010",
            "000000010011",
            "000000010100",
            "000000010101",
            "000000010110",
            "000000010111",
            "000000011100",
            "000000011101",
            "000000011110",
            "000000011111",
        };

        // What the next 13 bits begin, for white runs and for black: the length of the code in the top 16 bits and the run
        // in the bottom; 0 where no code begins so.
        private static readonly int[] WhiteLookup = Table(WhiteTerminating, WhiteMakeUp);
        private static readonly int[] BlackLookup = Table(BlackTerminating, BlackMakeUp);

        /// <summary>The code of each run the tables hold, for the tests to check the tables against what a writer made.</summary>
        internal static void Codes(bool white, Action<int, string> each)
        {
            string[] terminating = white ? WhiteTerminating : BlackTerminating;
            string[] makeUp = white ? WhiteMakeUp : BlackMakeUp;

            for (int i = 0; i < terminating.Length; i++)
            {
                each(i, terminating[i]);
            }

            for (int i = 0; i < makeUp.Length; i++)
            {
                each(64 * (i + 1), makeUp[i]);
            }

            for (int i = 0; i < ExtendedMakeUp.Length; i++)
            {
                each(1792 + 64 * i, ExtendedMakeUp[i]);
            }
        }

        private static int[] Table(string[] terminating, string[] makeUp)
        {
            var table = new int[1 << 13];

            for (int i = 0; i < terminating.Length; i++)
            {
                Fill(table, terminating[i], i);
            }

            for (int i = 0; i < makeUp.Length; i++)
            {
                Fill(table, makeUp[i], 64 * (i + 1));
            }

            for (int i = 0; i < ExtendedMakeUp.Length; i++)
            {
                Fill(table, ExtendedMakeUp[i], 1792 + 64 * i);
            }

            return table;
        }

        private static void Fill(int[] table, string code, int run)
        {
            int value = 0;

            foreach (char c in code)
            {
                value = (value << 1) | (c == '1' ? 1 : 0);
            }

            int free = 13 - code.Length;
            int first = value << free;

            for (int i = 0; i < 1 << free; i++)
            {
                table[first + i] = (code.Length << 16) | run;
            }
        }

        /// <summary>
        /// Decodes the rows of a strip or tile.
        /// </summary>
        /// <param name="data">The coded bytes, the bits of each byte in the order the file's FillOrder says is first at the top.</param>
        /// <param name="start">The first byte coded.</param>
        /// <param name="length">The bytes coded.</param>
        /// <param name="width">The pixels across a row.</param>
        /// <param name="rows">The rows to decode.</param>
        /// <param name="compression">2 for modified Huffman, 3 for Group 3, 4 for Group 4.</param>
        /// <param name="twoDimensional">For Group 3, whether lines may be coded against the line above (T4Options bit 0).</param>
        /// <returns>The rows, 1 bit a pixel, 1 for black, each padded to a whole byte; a row the data does not hold is white.</returns>
        internal static byte[] Decode(
            byte[] data,
            int start,
            int length,
            int width,
            int rows,
            int compression,
            bool twoDimensional
        )
        {
            int rowBytes = (width + 7) / 8;
            var result = new byte[rowBytes * rows];
            var bits = new Bits(data, start, length);
            var previous = new int[width + 4];
            var current = new int[width + 4];
            int previousCount = 0;

            for (int y = 0; y < rows; y++)
            {
                bool oneDimensional = compression == 2 || (compression == 3 && !twoDimensional);

                if (compression == 3)
                {
                    bool eol = bits.SkipEol();

                    if (bits.PeekAtEol())
                    {
                        // Return to control: six end-of-line codes, then nothing.
                        break;
                    }

                    if (twoDimensional)
                    {
                        // After an end of line a bit tells whether the line is coded in one dimension (1) or two (0).
                        oneDimensional = !eol || bits.Read(1) == 1;
                    }
                }

                if (compression != 4 && bits.AtEnd)
                {
                    break;
                }

                int count;
                bool ok;

                if (oneDimensional)
                {
                    ok = Row1D(bits, width, current, out count);
                }
                else
                {
                    ok = Row2D(bits, width, previous, previousCount, current, out count);
                }

                Paint(result, y * rowBytes, current, count, width);

                if (!ok)
                {
                    if (compression == 4)
                    {
                        break;
                    }

                    // A line that cannot be read ends at the code that failed; the next starts at an end of line.
                    bits.SkipToEol();
                }

                if (compression == 2)
                {
                    bits.AlignToByte();
                }

                int[] swap = previous;
                previous = current;
                current = swap;
                previousCount = count;
            }

            return result;
        }

        private static void Paint(byte[] result, int rowStart, int[] changes, int count, int width)
        {
            for (int i = 0; i < count; i += 2)
            {
                int from = Math.Min(changes[i], width);
                int to = i + 1 < count ? Math.Min(changes[i + 1], width) : width;

                for (int x = from; x < to; x++)
                {
                    result[rowStart + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        private static bool Row1D(Bits bits, int width, int[] changes, out int count)
        {
            count = 0;
            int at = 0;
            bool white = true;

            while (at < width)
            {
                int run = Run(bits, white);

                if (run < 0)
                {
                    return false;
                }

                at = Math.Min(width, at + run);

                if (count >= changes.Length)
                {
                    return false;
                }

                changes[count++] = at;
                white = !white;
            }

            return true;
        }

        private static bool Row2D(
            Bits bits,
            int width,
            int[] reference,
            int referenceCount,
            int[] changes,
            out int count
        )
        {
            count = 0;
            int a0 = -1;
            bool white = true;
            int b = 0;

            while (a0 < width)
            {
                // b1: the first change on the line above to the right of a0 that goes to the other colour; b2 the one after.
                while (b > 0 && reference[b - 1] > a0)
                {
                    b--;
                }

                while (b < referenceCount && (reference[b] <= a0 || (b & 1) != (white ? 0 : 1)))
                {
                    b++;
                }

                int b1 = b < referenceCount ? reference[b] : width;
                int b2 = b + 1 < referenceCount ? reference[b + 1] : width;
                int a1;
                int bitsPeek = bits.Peek(7);

                if ((bitsPeek & 0x40) != 0)
                {
                    bits.Skip(1);
                    a1 = b1;
                }
                else if ((bitsPeek >> 4) == 3)
                {
                    bits.Skip(3);
                    a1 = b1 + 1;
                }
                else if ((bitsPeek >> 4) == 2)
                {
                    bits.Skip(3);
                    a1 = b1 - 1;
                }
                else if ((bitsPeek >> 4) == 1)
                {
                    bits.Skip(3);
                    int first = Run(bits, white);
                    int second = first < 0 ? -1 : Run(bits, !white);

                    if (second < 0)
                    {
                        return false;
                    }

                    int begin = Math.Max(a0, 0);
                    int e1 = Math.Min(width, begin + first);
                    int e2 = Math.Min(width, e1 + second);

                    if (count + 2 > changes.Length)
                    {
                        return false;
                    }

                    changes[count++] = e1;
                    changes[count++] = e2;
                    a0 = e2;

                    if (e2 >= width)
                    {
                        break;
                    }

                    continue;
                }
                else if ((bitsPeek >> 3) == 1)
                {
                    bits.Skip(4);
                    a0 = b2;
                    continue;
                }
                else if ((bitsPeek >> 1) == 3)
                {
                    bits.Skip(6);
                    a1 = b1 + 2;
                }
                else if ((bitsPeek >> 1) == 2)
                {
                    bits.Skip(6);
                    a1 = b1 - 2;
                }
                else if (bitsPeek == 3)
                {
                    bits.Skip(7);
                    a1 = b1 + 3;
                }
                else if (bitsPeek == 2)
                {
                    bits.Skip(7);
                    a1 = b1 - 3;
                }
                else
                {
                    // An extension, an end-of-line code or a code that is none.
                    return false;
                }

                if (a1 < Math.Max(a0, 0) || a1 > width || count >= changes.Length)
                {
                    return false;
                }

                changes[count++] = a1;
                a0 = a1;
                white = !white;

                if (a0 >= width)
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>A run of one colour: any make-up codes added to its terminating code; -1 for a code that is none.</summary>
        private static int Run(Bits bits, bool white)
        {
            int[] table = white ? WhiteLookup : BlackLookup;
            int total = 0;

            while (true)
            {
                int entry = table[bits.Peek(13)];

                if (entry == 0)
                {
                    return -1;
                }

                bits.Skip(entry >> 16);
                int run = entry & 0xFFFF;
                total += run;

                if (run < 64)
                {
                    return total;
                }
            }
        }

        /// <summary>The bits of a strip, read from the most significant of each byte.</summary>
        private sealed class Bits
        {
            private readonly byte[] _data;
            private readonly long _end;
            private long _at;

            internal Bits(byte[] data, int start, int length)
            {
                _data = data;
                _at = (long)start * 8;
                _end = ((long)start + length) * 8;
            }

            internal bool AtEnd => _at >= _end;

            /// <summary>The next <paramref name="count"/> bits (up to 16), the ones past the end read as 0.</summary>
            internal int Peek(int count)
            {
                int value = 0;

                for (int i = 0; i < count; i++)
                {
                    long p = _at + i;
                    int bit = 0;

                    if (p < _end)
                    {
                        bit = (_data[p >> 3] >> (7 - (int)(p & 7))) & 1;
                    }

                    value = (value << 1) | bit;
                }

                return value;
            }

            internal void Skip(int count) => _at += count;

            internal int Read(int count)
            {
                int value = Peek(count);
                _at += count;
                return value;
            }

            internal void AlignToByte() => _at = (_at + 7) & ~7L;

            /// <summary>Whether the next 11 bits are zero: an end-of-line code, with any fill bits before its 1.</summary>
            internal bool PeekAtEol() => Peek(11) == 0 && !AtEnd && FindOne();

            private bool FindOne()
            {
                long p = _at;

                while (p < _end && ((_data[p >> 3] >> (7 - (int)(p & 7))) & 1) == 0)
                {
                    p++;
                }

                return p < _end;
            }

            /// <summary>Passes the zeros and the 1 of an end-of-line code where one is next; whether there was one.</summary>
            internal bool SkipEol()
            {
                if (Peek(11) != 0)
                {
                    return false;
                }

                while (_at < _end && Peek(1) == 0)
                {
                    _at++;
                }

                if (_at < _end)
                {
                    _at++;
                }

                return true;
            }

            /// <summary>Goes forward to the next end-of-line code, leaving it to be read.</summary>
            internal void SkipToEol()
            {
                while (_at < _end)
                {
                    if (Peek(12) == 1)
                    {
                        return;
                    }

                    _at++;
                }
            }
        }
    }
}
