using System;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A picture read from a raster file: its size in pixels, the millimetres a pixel is across and up where the file gives a
    /// resolution, whether it is bitonal (one bit a pixel, as a scanned drawing is), and its pixels as 8-bit red, green, blue
    /// and alpha, row by row from the top, the alpha not multiplied into the colour. A picture read for its header alone has no
    /// pixels.
    /// </summary>
    internal sealed class CadRaster
    {
        /// <summary>Gets or sets the pixels across the file holds.</summary>
        internal int Width { get; set; }

        /// <summary>Gets or sets the pixels up the file holds.</summary>
        internal int Height { get; set; }

        /// <summary>Gets or sets the millimetres a pixel is across; nought where the file gives no resolution.</summary>
        internal double MillimetresX { get; set; }

        /// <summary>Gets or sets the millimetres a pixel is up; nought where the file gives no resolution.</summary>
        internal double MillimetresY { get; set; }

        /// <summary>Gets or sets whether the picture is bitonal: one bit a pixel and one plane.</summary>
        internal bool IsBitonal { get; set; }

        /// <summary>
        /// Gets or sets how many pixels of the file went into each pixel kept across and up: 1 for a picture kept whole, more
        /// where it holds more pixels than a screen shows and each kept is the mean of a square of them.
        /// </summary>
        internal int Reduction { get; set; } = 1;

        /// <summary>Gets or sets the pixels kept across: <see cref="Width"/> divided by <see cref="Reduction"/>, rounded up.</summary>
        internal int KeptWidth { get; set; }

        /// <summary>Gets or sets the pixels kept up: <see cref="Height"/> divided by <see cref="Reduction"/>, rounded up.</summary>
        internal int KeptHeight { get; set; }

        /// <summary>Gets or sets the pixels kept, 4 bytes each; null for a picture read for its header alone.</summary>
        internal byte[] Rgba { get; set; }
    }

    /// <summary>
    /// Receives the rows of a picture from the top and keeps them, as they are or, where the picture holds more pixels than
    /// <see cref="MostPixels"/>, each square of pixels as the mean of its colours, so that the thin line of a scanned drawing
    /// is a grey line and not lost.
    /// </summary>
    internal sealed class CadRasterSink
    {
        /// <summary>The most pixels kept: a picture of more is reduced, as the screen shows no more.</summary>
        internal const double MostPixels = 16E6;

        private readonly CadRaster _raster;
        private readonly int _width;
        private readonly int _reduction;
        private readonly int _keptWidth;
        private readonly int _keptHeight;
        private readonly byte[] _rgba;
        private readonly int[] _sums;
        private int _inBlock;
        private int _row;

        /// <summary>Makes a sink for a picture.</summary>
        /// <param name="raster">The picture whose size is set and whose pixels are filled.</param>
        internal CadRasterSink(CadRaster raster)
        {
            _raster = raster;
            _width = raster.Width;
            double pixels = (double)raster.Width * raster.Height;
            _reduction = pixels <= MostPixels ? 1 : (int)Math.Ceiling(Math.Sqrt(pixels / MostPixels));
            _keptWidth = (raster.Width + _reduction - 1) / _reduction;
            _keptHeight = (raster.Height + _reduction - 1) / _reduction;
            _rgba = new byte[_keptWidth * _keptHeight * 4];

            if (_reduction > 1)
            {
                _sums = new int[_keptWidth * 4];
            }

            raster.Reduction = _reduction;
            raster.KeptWidth = _keptWidth;
            raster.KeptHeight = _keptHeight;
            raster.Rgba = _rgba;
        }

        /// <summary>
        /// Takes the next row from the top: <see cref="CadRaster.Width"/> pixels of 4 bytes, from <paramref name="offset"/>.
        /// </summary>
        /// <param name="rgba">The bytes of the row.</param>
        /// <param name="offset">Where the row starts in them.</param>
        internal void Add(byte[] rgba, int offset)
        {
            if (_row >= _raster.Height)
            {
                return;
            }

            if (_reduction == 1)
            {
                Buffer.BlockCopy(rgba, offset, _rgba, _row * _width * 4, _width * 4);
                _row++;
                return;
            }

            for (int x = 0; x < _width; x++)
            {
                int to = x / _reduction * 4;
                int from = offset + x * 4;
                _sums[to] += rgba[from];
                _sums[to + 1] += rgba[from + 1];
                _sums[to + 2] += rgba[from + 2];
                _sums[to + 3] += rgba[from + 3];
            }

            _inBlock++;
            _row++;

            if (_inBlock == _reduction || _row == _raster.Height)
            {
                Flush();
            }
        }

        /// <summary>Gives the pixels kept, the rows not given left transparent.</summary>
        internal CadRaster Finish()
        {
            if (_reduction > 1 && _inBlock > 0)
            {
                Flush();
            }

            return _raster;
        }

        private void Flush()
        {
            int outRow = (_row - 1) / _reduction;
            int rows = _inBlock;

            for (int ox = 0; ox < _keptWidth; ox++)
            {
                int columns = Math.Min(_reduction, _width - ox * _reduction);
                int count = columns * rows;
                int at = (outRow * _keptWidth + ox) * 4;

                for (int c = 0; c < 4; c++)
                {
                    _rgba[at + c] = (byte)((_sums[ox * 4 + c] + count / 2) / count);
                    _sums[ox * 4 + c] = 0;
                }
            }

            _inBlock = 0;
        }
    }
}
