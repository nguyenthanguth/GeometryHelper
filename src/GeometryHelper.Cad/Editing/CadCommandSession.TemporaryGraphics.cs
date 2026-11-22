using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// One vector AutoLISP's <c>grdraw</c> or <c>grvecs</c> drew on the screen: its two ends in drawing units (millimetres), the
    /// colour index it was drawn in and whether it was drawn highlighted. It is not part of the drawing and is never saved.
    /// </summary>
    internal readonly struct CadTemporaryVector
    {
        /// <summary>Makes a vector.</summary>
        /// <param name="from">Where it starts, in the world coordinates of the drawing.</param>
        /// <param name="to">Where it ends.</param>
        /// <param name="colour">The colour index: 1 to 255, or 0 or less for AutoCAD's XOR colour, which the window shows as the colour of the cursor.</param>
        /// <param name="highlight">Whether it is drawn dashed, as AutoCAD highlights a vector.</param>
        internal CadTemporaryVector(GeoPoint3 from, GeoPoint3 to, int colour, bool highlight)
        {
            From = from;
            To = to;
            Colour = colour;
            Highlight = highlight;
        }

        /// <summary>Gets where the vector starts.</summary>
        internal GeoPoint3 From { get; }

        /// <summary>Gets where the vector ends.</summary>
        internal GeoPoint3 To { get; }

        /// <summary>Gets the colour index.</summary>
        internal int Colour { get; }

        /// <summary>Gets whether the vector is drawn highlighted.</summary>
        internal bool Highlight { get; }
    }

    /// <summary>The temporary graphics of the session: what <c>grdraw</c> and <c>grvecs</c> put on the screen until a redraw takes it off.</summary>
    public sealed partial class CadCommandSession
    {
        private readonly List<CadTemporaryVector> _temporaryVectors = new List<CadTemporaryVector>();

        /// <summary>Raised on the owner's thread when the temporary vectors change, so that the window draws them or takes them off.</summary>
        internal event EventHandler TemporaryGraphicsChanged;

        /// <summary>Gets the vectors on the screen now, in the order they were drawn; a new list on each call.</summary>
        internal IReadOnlyList<CadTemporaryVector> TemporaryVectors => _temporaryVectors.ToArray();

        /// <summary>Puts vectors on the screen, as <c>grdraw</c> and <c>grvecs</c> do.</summary>
        /// <param name="vectors">The vectors.</param>
        internal void AddTemporaryVectors(IEnumerable<CadTemporaryVector> vectors)
        {
            int before = _temporaryVectors.Count;
            _temporaryVectors.AddRange(vectors);

            if (_temporaryVectors.Count != before)
            {
                TemporaryGraphicsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Takes every temporary vector off the screen, as REDRAW, REGEN and <c>grclear</c> do.</summary>
        internal void ClearTemporaryGraphics()
        {
            if (_temporaryVectors.Count == 0)
            {
                return;
            }

            _temporaryVectors.Clear();
            TemporaryGraphicsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
