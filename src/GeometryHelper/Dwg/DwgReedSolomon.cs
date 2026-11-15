using System;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The Reed-Solomon codes of R2007 (AC1021), as the Open Design Specification for .dwg files names them and AutoCAD's
    /// own files carry them: a block of 255 bytes holds data and parity, the parity being the remainder of the data, taken
    /// as a polynomial with its first byte the highest term, divided by the generator whose roots are consecutive powers
    /// of 2 of a field of 256 elements. The file header and the page maps use (255, 239), 239 data bytes and 16 of parity,
    /// in the field made by <c>x^8 + x^6 + x^5 + x^3 + 1</c> with the roots 2^239 to 2^254; the data pages use (255, 251),
    /// 251 data bytes and 4 of parity, in the field of <c>x^8 + x^4 + x^3 + x^2 + 1</c> with the roots 2^251 to 2^254. Both
    /// were found by taking the blocks of AutoCAD's files and testing which field and which roots make them codewords.
    /// The reader does not use the parity (<see cref="DwgContainer"/> takes the data bytes out and lets the CRCs say
    /// whether they are whole); AutoCAD may, so the writer lays it down right.
    /// </summary>
    internal sealed class DwgReedSolomon
    {
        /// <summary>The code of the file header and the page maps: 239 data bytes and 16 of parity in a block of 255.</summary>
        public static readonly DwgReedSolomon Of239 = new DwgReedSolomon(0x169, 239, 16);

        /// <summary>The code of the data pages: 251 data bytes and 4 of parity in a block of 255.</summary>
        public static readonly DwgReedSolomon Of251 = new DwgReedSolomon(0x11D, 251, 4);

        private readonly int _data;
        private readonly int _parity;

        /// <summary>For each term of the generator below the first, the product of the term with each byte: one run of 256 per term.</summary>
        private readonly byte[][] _times;

        private DwgReedSolomon(int polynomial, int data, int parity)
        {
            _data = data;
            _parity = parity;
            var exp = new int[512];
            var log = new int[256];
            int x = 1;

            for (int i = 0; i < 255; i++)
            {
                exp[i] = x;
                log[x] = i;
                x <<= 1;

                if ((x & 0x100) != 0)
                {
                    x ^= polynomial;
                }
            }

            for (int i = 255; i < 512; i++)
            {
                exp[i] = exp[i - 255];
            }

            // The generator, the product of (x - root) for the roots 2^(255 - parity) to 2^254; the first term is 1.
            var generator = new int[parity + 1];
            generator[0] = 1;

            for (int k = 0; k < parity; k++)
            {
                int root = exp[(255 - parity + k) % 255];

                for (int i = k + 1; i >= 1; i--)
                {
                    int product = generator[i - 1] == 0 ? 0 : exp[log[generator[i - 1]] + log[root]];
                    generator[i] ^= product;
                }
            }

            _times = new byte[parity][];

            for (int i = 0; i < parity; i++)
            {
                _times[i] = new byte[256];

                for (int b = 1; b < 256; b++)
                {
                    _times[i][b] = generator[i + 1] == 0 ? (byte)0 : (byte)exp[log[generator[i + 1]] + log[b]];
                }
            }
        }

        /// <summary>
        /// Writes the 239 or 251 data bytes (as the code is) of <paramref name="data"/> from <paramref name="start"/> and their parity as
        /// one block of 255 bytes into <paramref name="block"/>.
        /// </summary>
        public void Encode(byte[] data, long start, byte[] block)
        {
            var remainder = new byte[_parity];

            for (int i = 0; i < _data; i++)
            {
                byte b = data[start + i];
                block[i] = b;
                int feedback = b ^ remainder[0];

                Array.Copy(remainder, 1, remainder, 0, _parity - 1);
                remainder[_parity - 1] = 0;

                if (feedback != 0)
                {
                    for (int k = 0; k < _parity; k++)
                    {
                        remainder[k] ^= _times[k][feedback];
                    }
                }
            }

            Array.Copy(remainder, 0, block, _data, _parity);
        }

        /// <summary>
        /// Codes <paramref name="data"/> as <paramref name="blocks"/> blocks of 255 bytes laid out one byte of each block
        /// after the other (byte i of block j at <c>i * blocks + j</c>), as the file header, the page maps and the pages of
        /// the sections of coding 4 are. The data goes into the blocks in turn, 239 or 251 bytes to each, and what
        /// lacks of the last block is zero; the data must fit the blocks.
        /// </summary>
        public byte[] Interleave(byte[] data, int blocks)
        {
            if ((long)blocks * _data < data.LongLength)
            {
                throw new ArgumentException("The data does not fit the blocks.", nameof(data));
            }

            var coded = new byte[(long)blocks * 255];
            var block = new byte[255];
            var padded = new byte[_data];

            for (int j = 0; j < blocks; j++)
            {
                long start = (long)j * _data;
                long left = Math.Max(0, Math.Min(_data, data.LongLength - start));

                if (left == _data)
                {
                    Encode(data, start, block);
                }
                else
                {
                    Array.Clear(padded, 0, padded.Length);
                    if (left > 0)
                    {
                        Array.Copy(data, start, padded, 0, left);
                    }
                    Encode(padded, 0, block);
                }

                for (int i = 0; i < 255; i++)
                {
                    coded[((long)i * blocks) + j] = block[i];
                }
            }

            return coded;
        }
    }
}
