using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Copies the named records of one drawing into another, as the Layer Translator brings a linetype of the standards file
    /// and DesignCenter brings a text style, a linetype or a block definition: a record the target has is kept as it is and
    /// not copied, as AutoCAD keeps it. Each runs inside an edit of the target, which the caller makes.
    /// </summary>
    internal static class CadSymbolCopy
    {
        /// <summary>
        /// Copies a linetype with its dashes: their lengths, and for a dash of a text or a shape its text or shape number, the
        /// text style it is drawn in (copied too where the target has none of the name), scale, rotation, offset and the turn
        /// from the x axis. ByLayer, ByBlock and Continuous are always there and are not copied.
        /// </summary>
        /// <returns>True where it was copied; false where the target has the linetype or the source has none of the name.</returns>
        internal static bool Linetype(DxfDatabase target, DxfDatabase source, string name)
        {
            if (
                source == null
                || string.IsNullOrEmpty(name)
                || target.LinetypeTable.Has(name)
                || !source.LinetypeTable.Has(name)
            )
            {
                return false;
            }

            DxfLinetypeTableRecord from = source.LinetypeTable[name];
            var made = new DxfLinetypeTableRecord
            {
                Name = from.Name,
                AsciiDescription = from.AsciiDescription,
                NumDashes = from.NumDashes,
            };

            for (int i = 0; i < made.NumDashes; i++)
            {
                made.SetDashLengthAt(i, from.DashLengthAt(i));
                DxfTextStyleTableRecord style = from.ShapeStyleAt(i);
                string text = from.TextAt(i);
                int shape = from.ShapeNumberAt(i);

                if (style == null || (text.Length == 0 && shape == 0))
                {
                    continue;
                }

                TextStyle(target, source, style.Name);

                if (!target.TextStyleTable.Has(style.Name))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    made.SetTextAt(i, text);
                }
                else
                {
                    made.SetShapeNumberAt(i, shape);
                }

                made.SetShapeStyleAt(i, target.TextStyleTable[style.Name]);
                made.SetShapeScaleAt(i, from.ShapeScaleAt(i));
                made.SetShapeRotationAt(i, from.ShapeRotationAt(i));
                made.SetShapeOffsetAt(i, from.ShapeOffsetAt(i));
                made.SetShapeIsUcsOrientedAt(i, from.ShapeIsUcsOrientedAt(i));
            }

            made.PatternLength = from.PatternLength;
            target.LinetypeTable.Add(made);
            return true;
        }

        /// <summary>Copies a text style: its font file, big font, height, width factor, oblique angle, flags and font.</summary>
        /// <returns>True where it was copied; false where the target has the style or the source has none of the name.</returns>
        internal static bool TextStyle(DxfDatabase target, DxfDatabase source, string name)
        {
            if (
                source == null
                || string.IsNullOrEmpty(name)
                || target.TextStyleTable.Has(name)
                || !source.TextStyleTable.Has(name)
            )
            {
                return false;
            }

            DxfTextStyleTableRecord from = source.TextStyleTable[name];
            target.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = from.Name,
                    FileName = from.FileName,
                    BigFontFileName = from.BigFontFileName,
                    TextSize = from.TextSize,
                    XScale = from.XScale,
                    ObliquingAngle = from.ObliquingAngle,
                    FlagBits = from.FlagBits,
                }
            );
            target.TextStyleTable[from.Name].Font = from.Font;
            return true;
        }

        /// <summary>
        /// Copies a block's definition with what it draws by (its layers, linetypes, styles and the blocks it places), as an
        /// INSERT of it copied between the drawings would bring them, the INSERT itself left out. The source takes a copy of
        /// the INSERT in its model space to carry the block over, which is why it is a drawing the caller owns.
        /// </summary>
        /// <returns>True where it was copied; false where the target has a block of the name or the source has none.</returns>
        internal static bool Block(DxfDatabase target, DxfDatabase source, string name)
        {
            if (
                source == null
                || string.IsNullOrEmpty(name)
                || target.BlockTable.Has(name)
                || !source.BlockTable.Has(name)
                || source.BlockTable[name].IsLayout
            )
            {
                return false;
            }

            var carrier = new DxfBlockReference(GeoPoint3.Origin, source.BlockTable[name]);
            source.ModelSpace.AppendEntity(carrier);
            IReadOnlyList<DxfEntity> copies;

            try
            {
                copies = source.WblockCloneObjects(new[] { carrier }, target.ModelSpace);
            }
            finally
            {
                carrier.Erase();
            }

            foreach (DxfEntity copy in copies)
            {
                copy.Erase();
            }

            return target.BlockTable.Has(name);
        }
    }
}
