using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What editing reads of an MTEXT's layout (<see cref="CadMTextLayout"/>): the box its grips stand at, and the TEXTs and
    /// LINEs EXPLODE breaks it into, each where the picture draws it.
    /// </summary>
    internal static class CadMTextEdit
    {
        /// <summary>
        /// The layout of an MTEXT of a drawing as the document showing the drawing draws it, in that document's fonts (with no
        /// document, with no font found); null for an MTEXT in no drawing.
        /// </summary>
        internal static CadMTextLayout LayoutOf(DxfMText text)
        {
            DxfDatabase database = text.IsResident ? text.Item.File.Database : null;

            if (database == null)
            {
                return null;
            }

            CadDocument document = CadDocument.Showing(database);
            return document != null ? document.LayoutOf(text) : new DisplayBuilder(database).LayoutOf(text);
        }

        /// <summary>The column (0 left, 1 centre, 2 right) and the row (0 top, 1 middle, 2 bottom) of an attachment point.</summary>
        internal static void Place(DxfMText text, out int column, out int row)
        {
            int attachment = (int)text.Attachment;
            attachment = attachment >= 1 && attachment <= 9 ? attachment - 1 : 0;
            column = attachment % 3;
            row = attachment / 3;
        }

        /// <summary>
        /// The box AutoCAD 2021 puts an MTEXT's grips at, in the MTEXT's own frame (its location at nought, x along its
        /// direction, y up): its defined width across, and down its defined height, or with none the height of its lines, set
        /// about the location as the attachment says. False for an MTEXT of no width, in columns, or in no drawing, which has
        /// the grip at its location alone.
        /// </summary>
        internal static bool TryGetGripBox(
            DxfMText text,
            out double left,
            out double bottom,
            out double right,
            out double top
        )
        {
            left = bottom = right = top = 0.0;
            double width = text.Width;

            if (!(width > 0.0) || text.ColumnType != DxfColumnType.NoColumns)
            {
                return false;
            }

            double high = text.Height;

            if (!(high > 0.0))
            {
                CadMTextLayout layout = LayoutOf(text);

                if (layout == null)
                {
                    return false;
                }

                high = layout.ActualHeight;
            }

            Place(text, out int column, out int row);
            left = -column * width / 2.0;
            right = left + width;
            top = row * high / 2.0;
            bottom = top - high;
            return true;
        }

        /// <summary>The way an MTEXT's lines run and the way up from them, in the plan, each of length one.</summary>
        internal static void Axes(DxfMText text, out GeoVector2 along, out GeoVector2 up)
        {
            GeoVector3 direction = text.Direction;
            double length = Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y));
            along =
                length > 0.0 ? new GeoVector2(direction.X / length, direction.Y / length) : new GeoVector2(1.0, 0.0);

            // The normal crossed with the direction: to the left of it seen from above, to the right seen from below.
            up = text.Normal.Z < 0.0 ? new GeoVector2(along.Y, -along.X) : new GeoVector2(-along.Y, along.X);
        }

        /// <summary>
        /// The pieces EXPLODE makes of an MTEXT, as AutoCAD 2021 makes them: a TEXT for each run of letters of each line where
        /// the layout draws it, of its height, width factor and slant, in the MTEXT's turn and plane, a line under, over or
        /// through it written <c>%%U</c>, <c>%%O</c> or <c>%%K</c> before its letters, and a LINE for the bar of each stack;
        /// all with the MTEXT's layer, linetype and lineweight. A run in its own colour (<c>\C1;</c>, <c>\C256;</c> ByLayer,
        /// <c>\C0;</c> ByBlock, <c>\c16711680;</c> the true colour 0,0,255) is a TEXT of that colour, the rest of the MTEXT's. A
        /// run in an SHX font of its own (<c>\Ftxt.shx;</c>) is a TEXT of the style <c>MtXpl_txt_shx</c> of that font, which
        /// <paramref name="styles"/> is given (name and font file) for the drawing to have; the rest, a run in a TrueType font
        /// of its own too, is of the MTEXT's style, as AutoCAD 2021's <c>Explode</c> makes them (probed). Null for an MTEXT in
        /// no drawing or of no letters.
        /// </summary>
        internal static IReadOnlyList<DxfEntity> Explode(DxfMText text, IDictionary<string, string> styles)
        {
            CadMTextLayout layout = LayoutOf(text);

            if (layout == null || layout.Pieces.Count == 0)
            {
                return null;
            }

            GeoVector3 normal = text.Normal;
            GeoVector3 along = text.Direction;
            GeoVector3 up = normal.CrossProduct(along);
            double upLength = Math.Sqrt((up.X * up.X) + (up.Y * up.Y) + (up.Z * up.Z));
            double alongLength = Math.Sqrt((along.X * along.X) + (along.Y * along.Y) + (along.Z * along.Z));

            if (!(upLength > 0.0) || !(alongLength > 0.0))
            {
                return null;
            }

            GeoPoint3 location = text.Location;

            GeoPoint3 At(double x, double y)
            {
                double a = x / alongLength;
                double u = y / upLength;
                return new GeoPoint3(
                    location.X + (along.X * a) + (up.X * u),
                    location.Y + (along.Y * a) + (up.Y * u),
                    location.Z + (along.Z * a) + (up.Z * u)
                );
            }

            var pieces = new List<DxfEntity>(layout.Pieces.Count);
            string style = text.TextStyleName;
            double turn = text.Rotation;

            foreach (CadMTextLayout.Piece piece in layout.Pieces)
            {
                if (string.IsNullOrEmpty(piece.Text))
                {
                    continue;
                }

                string marks =
                    (piece.Look.Underline ? "%%U" : string.Empty)
                    + (piece.Look.Overline ? "%%O" : string.Empty)
                    + (piece.Look.Strikethrough ? "%%K" : string.Empty);
                var made = new DxfDBText();
                made.SetPropertiesFrom(text);
                made.Normal = normal;
                made.Position = At(piece.X, piece.Y);
                made.Height = piece.Height;
                made.TextString = marks + piece.Text;
                made.Rotation = turn;

                if (Math.Abs(piece.WidthFactor - 1.0) > 1E-9)
                {
                    made.WidthFactor = piece.WidthFactor;
                }

                if (Math.Abs(piece.Slant) > 1E-9)
                {
                    made.Oblique = piece.Slant;
                }

                if (piece.Look.Font != null && piece.Look.IsShx)
                {
                    string own = ExplodedStyleName(piece.Look.Font);
                    styles[own] = piece.Look.Font;
                    made.TextStyleName = own;
                }
                else if (!string.IsNullOrEmpty(style))
                {
                    made.TextStyleName = style;
                }

                if (piece.Look.TrueColor.HasValue)
                {
                    int rgb = piece.Look.TrueColor.Value;
                    made.Color = DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                }
                else if (piece.Look.ColorIndex.HasValue)
                {
                    int index = piece.Look.ColorIndex.Value;
                    made.Color =
                        index <= 0 ? DxfColor.ByBlock
                        : index >= 256 ? DxfColor.ByLayer
                        : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);
                }

                pieces.Add(made);
            }

            foreach (CadMTextLayout.Stroke stroke in layout.Strokes)
            {
                if (!stroke.IsBar)
                {
                    continue;
                }

                var bar = new DxfLine { StartPoint = At(stroke.X0, stroke.Y0), EndPoint = At(stroke.X1, stroke.Y1) };
                bar.SetPropertiesFrom(text);
                pieces.Add(bar);
            }

            return pieces.Count == 0 ? null : pieces;
        }

        /// <summary>The style AutoCAD 2021's EXPLODE gives a run of an SHX font of its own: <c>MtXpl_txt_shx</c> for txt.shx.</summary>
        internal static string ExplodedStyleName(string font) => "MtXpl_" + font.Replace('.', '_');

        /// <summary>Adds to a drawing the styles of the runs of SHX fonts of their own <see cref="Explode"/> made TEXTs of, those it has not.</summary>
        internal static void AddExplodedStyles(DxfDatabase database, IDictionary<string, string> styles)
        {
            foreach (KeyValuePair<string, string> style in styles.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                if (!database.TextStyleTable.Has(style.Key))
                {
                    database.TextStyleTable.Add(
                        new DxfTextStyleTableRecord { Name = style.Key, FileName = style.Value }
                    );
                }
            }
        }
    }
}
