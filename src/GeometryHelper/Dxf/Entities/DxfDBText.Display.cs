using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public partial class DxfDBText
    {
        /// <summary>What a drawing is made of a text from: the properties the display reads, one for one.</summary>
        internal struct DisplayRead
        {
            internal string TextString;
            internal double Height;
            internal bool IsDefaultAlignment;

            /// <summary>The point the text is placed by: its <see cref="Position"/>, or its <see cref="AlignmentPoint"/> where it is not aligned left on its baseline.</summary>
            internal GeoPoint3 At;
            internal DxfTextHorizontalMode HorizontalMode;
            internal DxfTextVerticalMode VerticalMode;
            internal double Rotation;
            internal bool IsMirroredInX;
            internal bool IsMirroredInY;
            internal string TextStyleName;
            internal double WidthFactor;
            internal double Oblique;
        }

        /// <summary>
        /// Reads what the display draws a text by, as the properties <see cref="TextString"/>, <see cref="Height"/>,
        /// <see cref="IsDefaultAlignment"/>, <see cref="Position"/> or <see cref="AlignmentPoint"/>, <see cref="HorizontalMode"/>,
        /// <see cref="VerticalMode"/>, <see cref="Rotation"/>, <see cref="IsMirroredInX"/>, <see cref="IsMirroredInY"/>,
        /// <see cref="TextStyleName"/>, <see cref="WidthFactor"/> and <see cref="Oblique"/> give it, to the bit: of a TEXT that
        /// has no contexts, from one pass over the groups of its record, where each property read alone found its own among
        /// them again; an attribute, a text with an extension dictionary and a copy that carries contexts read each property.
        /// Nothing is kept: the pass is read afresh each time, so that an edit of the text is seen by the next one.
        /// </summary>
        internal DisplayRead ReadForDisplay()
        {
            var read = new DisplayRead();

            if (!IsAttribute && Item.File.TryTextGroups(Item, out DxfFile.TextGroups g))
            {
                int have = g.Have;
                GeoVector3 normal = NormalOf(
                    (
                        have
                        & (
                            DxfFile.TextGroups.HasNormalX
                            | DxfFile.TextGroups.HasNormalY
                            | DxfFile.TextGroups.HasNormalZ
                        )
                    ) != 0,
                    g.NormalX,
                    g.NormalY,
                    g.NormalZ
                );
                read.TextString = DxfReader.Decode(DxfReader.Carets(g.Words ?? string.Empty));
                read.Height = (have & DxfFile.TextGroups.HasHeight) != 0 ? g.Height : 2.5;
                read.IsDefaultAlignment = g.Horizontal == 0 && g.Vertical == 0;

                // The point of the groups 10 or 11, a coordinate the record leaves out nought, the origin where it has none of the three.
                bool simple = read.IsDefaultAlignment;
                int first = simple ? DxfFile.TextGroups.HasX : DxfFile.TextGroups.HasAlignedX;
                int any = first | (first << 1) | (first << 2);
                GeoPoint3 point =
                    (have & any) == 0 ? Origin
                    : simple ? new GeoPoint3(g.X, g.Y, g.Z)
                    : new GeoPoint3(g.AlignedX, g.AlignedY, g.AlignedZ);
                read.At = OcsToWorld(normal, point);
                read.HorizontalMode = (DxfTextHorizontalMode)g.Horizontal;
                read.VerticalMode = (DxfTextVerticalMode)g.Vertical;
                read.Rotation = ((have & DxfFile.TextGroups.HasDegrees) != 0 ? g.Degrees : 0.0) * (Math.PI / 180.0);
                read.IsMirroredInX = (g.Mirror & 2) != 0;
                read.IsMirroredInY = (g.Mirror & 4) != 0;
                string style = DxfReader.NameOf(g.Style ?? string.Empty);
                read.TextStyleName = style.Length == 0 ? "Standard" : style;
                read.WidthFactor = (have & DxfFile.TextGroups.HasFactor) != 0 ? g.Factor : 1.0;
                read.Oblique = ((have & DxfFile.TextGroups.HasOblique) != 0 ? g.Oblique : 0.0) * (Math.PI / 180.0);
                return read;
            }

            read.TextString = TextString;
            read.Height = Height;
            read.IsDefaultAlignment = IsDefaultAlignment;
            read.At = read.IsDefaultAlignment ? Position : AlignmentPoint;
            read.HorizontalMode = HorizontalMode;
            read.VerticalMode = VerticalMode;
            read.Rotation = Rotation;
            read.IsMirroredInX = IsMirroredInX;
            read.IsMirroredInY = IsMirroredInY;
            read.TextStyleName = TextStyleName;
            read.WidthFactor = WidthFactor;
            read.Oblique = Oblique;
            return read;
        }
    }
}
