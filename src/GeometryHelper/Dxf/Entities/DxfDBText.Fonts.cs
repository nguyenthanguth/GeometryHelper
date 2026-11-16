using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public partial class DxfDBText
    {
        // Set while the text's place is worked out from its words, so that what that writes does not work it out again.
        private bool _realigning;

        /// <summary>
        /// Gets the box the text's words fill, as AutoCAD's <c>GeometricExtents</c>: where the drawing's
        /// <see cref="DxfDatabase.FontSearchPath"/> finds the SHX font of its style, the box of the ink of the whole line of words
        /// upright, its four corners slanted by <see cref="Oblique"/>, turned and placed as the text stands at its
        /// <see cref="Position"/>, as AutoCAD 2021 boxes it (check F1a), the position itself not among them: " A" of txt.shx 4
        /// high at the origin from (4, 0, 0) to (6.667, 4, 0), "A" from (0, 0, 0) to (2.667, 4, 0), and slanted 15 degrees to
        /// (3.738, 4, 0), its top corner moved by 4 times the tangent. A text of no ink is boxed at its position. Of a TrueType
        /// font, the box from the start of the baseline to the last pen, the ink past it counted, each letter boxed from the
        /// lower of its bottom and the baseline as high as it is (check F2): "H" of arial.ttf 4 high from (0, 0, 0) to
        /// (4.035, 4, 0), its advance, and the degree sign from (0, 0, 0) to (2.235, 1.514, 0), though inked from 2.554 up.
        /// Elsewhere as <see cref="DxfEntity.GeometricExtents"/> gives it, the position alone.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown, with no font, as <see cref="DxfEntity.GeometricExtents"/> throws.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                DxfTextLine line = Words(Height, WidthFactor, out _, out _);
                return line == null ? base.GeometricExtents : ExtentsOf(line);
            }
        }

        /// <summary>
        /// The box the words <paramref name="line"/> holds fill as the text stands, as <see cref="GeometricExtents"/> gives it:
        /// the box of their ink upright, its four corners slanted by <see cref="Oblique"/>, turned and placed at its
        /// <see cref="Position"/>.
        /// </summary>
        private GeoAabb3 ExtentsOf(DxfTextLine line)
        {
            GeoVector3 normal = Normal;
            GeoPoint3 position = Position;
            GeoPoint3 at = WorldToOcs(normal, position);
            double turn = Rotation;
            double slant = Math.Tan(Oblique);
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            double minX = position.X,
                minY = position.Y,
                minZ = position.Z;
            double maxX = minX,
                maxY = minY,
                maxZ = minZ;

            if (line.Box(out double left, out double right, out double bottom, out double top))
            {
                minX = minY = minZ = double.PositiveInfinity;
                maxX = maxY = maxZ = double.NegativeInfinity;

                foreach (double x in new[] { left, right })
                {
                    foreach (double y in new[] { bottom, top })
                    {
                        double u = x + (y * slant);
                        GeoPoint3 corner = OcsToWorld(
                            normal,
                            new GeoPoint3(at.X + (u * cos) - (y * sin), at.Y + (u * sin) + (y * cos), at.Z)
                        );
                        minX = Math.Min(minX, corner.X);
                        minY = Math.Min(minY, corner.Y);
                        minZ = Math.Min(minZ, corner.Z);
                        maxX = Math.Max(maxX, corner.X);
                        maxY = Math.Max(maxY, corner.Y);
                        maxZ = Math.Max(maxZ, corner.Z);
                    }
                }
            }

            return new GeoAabb3(new GeoPoint3(minX, minY, minZ), new GeoPoint3(maxX, maxY, maxZ));
        }

        /// <summary>
        /// The box the text's words fill as <see cref="GeometryHelper.Dxf.DxfDBText.GeometricExtents"/> gives it, measured in the
        /// TrueType font arial.ttf where the drawing's <see cref="DxfDatabase.FontSearchPath"/> finds it, whatever the font of
        /// the text's style: the words as AutoCAD 2021 measures an attribute definition of a table's block cell, where it
        /// asks the room a block needs. False where the drawing finds no arial.ttf, or the words are not measured
        /// (<see cref="IsMeasured"/>).
        /// </summary>
        internal bool TryArialExtents(out GeoAabb3 extents)
        {
            extents = default(GeoAabb3);
            DxfFonts fonts = (IsResident ? Item.File : Item.File.Origin)?.Database?.Fonts;

            if (fonts == null || IsMirroredInX || IsMirroredInY || (IsAttribute && Embedded))
            {
                return false;
            }

            DxfTextLine line = fonts.Find("arial.ttf") is DxfTrueTypeFont arial
                ? DxfFonts.Text(arial, null, TextString, Height, WidthFactor)
                : null;

            if (line == null)
            {
                return false;
            }

            extents = ExtentsOf(line);
            return true;
        }

        /// <summary>
        /// The text's words measured in the SHX or TrueType font of its style at a height and width factor, where its drawing's
        /// <see cref="DxfDatabase.FontSearchPath"/> finds it, a copy in no file measured in the drawing it was copied from; null
        /// where it does not, the text is in no drawing, is written backwards or upside down, or is an attribute of several
        /// lines; and where its style's big font is read and its words hold a multibyte code (<c>\M+1D6D0</c>), which AutoCAD
        /// 2021 draws from the big font by its two bytes, whatever its code page (check F1a), and the words read in their own.
        /// </summary>
        private DxfTextLine Words(double height, double widthFactor, out DxfFont font, out DxfFonts.Big big)
        {
            font = null;
            big = null;
            DxfFonts fonts = (IsResident ? Item.File : Item.File.Origin)?.Database?.Fonts;

            if (fonts == null || IsMirroredInX || IsMirroredInY || (IsAttribute && Embedded))
            {
                return null;
            }

            font = fonts.FontOf(TextStyle, out big);
            string raw = big?.Font != null ? GetString(1) ?? string.Empty : string.Empty;

            if (
                raw.IndexOf("\\M+", StringComparison.Ordinal) >= 0
                || raw.IndexOf("\\m+", StringComparison.Ordinal) >= 0
            )
            {
                return null;
            }

            return font == null ? null : DxfFonts.Text(font, big, TextString, height, widthFactor);
        }

        /// <summary>
        /// Puts the text where its words stand about its second point, as AutoCAD 2021's <c>AdjustAlignment</c> puts it, where the
        /// drawing's <see cref="DxfDatabase.FontSearchPath"/> finds the SHX or TrueType font of its style; nothing elsewhere, or for a text
        /// aligned left on its baseline. Its first point (10) is worked out from its second (11), its height, width factor,
        /// turn and modes: across, nought for the left, the middle of the box of its words for the centre and the middle, and
        /// the box's right for the right; up, the height down for the top, half of it for the middle, the font's depth below the
        /// baseline for the bottom, and the middle of the box for the middle of the words (72 of 4), the box of the ink, or
        /// from the baseline's start to the last pen where the words end in a space. An annotative text's every context is put
        /// so too, by its own second point, height, turn and horizontal mode. An aligned text (72 of 3) is given the height,
        /// and a fitted one (5) the width factor, its two points apart over the box's right gives the one it has.
        /// </summary>
        internal void Realign()
        {
            if (_realigning || !IsResident || Item.File.Database?.Fonts == null)
            {
                return;
            }

            DxfTextLine words = Words(Height, WidthFactor, out DxfFont font, out DxfFonts.Big big);

            if (words == null)
            {
                return;
            }

            _realigning = true;

            try
            {
                int vertical = TextInt(VerticalCode, 0);
                int horizontal = TextInt(72, 0);
                double height = TextDouble(40, 2.5);
                double widthFactor = WidthFactor;
                GeoPoint3 first = TextPoint(10) ?? Origin;
                GeoPoint3 second = TextPoint(11) ?? Origin;

                if ((horizontal == 3 || horizontal == 5) && vertical == 0)
                {
                    Fit(font, big, horizontal, height, widthFactor, first, second);
                    return;
                }

                string text = TextString;
                double[] placed = Placed(
                    font,
                    big,
                    text,
                    horizontal,
                    vertical,
                    height,
                    widthFactor,
                    TextDouble(50, 0.0),
                    second.X,
                    second.Y,
                    BitConverter.DoubleToInt64Bits(height) == BitConverter.DoubleToInt64Bits(Height) ? words : null
                );

                if (placed != null)
                {
                    SetText(TextPointGroups(10, new GeoPoint3(placed[0], placed[1], first.Z)));
                }

                ShownContexts contexts = TextContexts();

                if (contexts == null)
                {
                    return;
                }

                for (int i = 0; i < contexts.All.Count; i++)
                {
                    ContextGroups context = ContextText.Read(contexts.All[i]);
                    int mode = context.Has(ContextText.Horizontal)
                        ? (int)context.Value(ContextText.Horizontal, 0.0)
                        : horizontal;
                    double ratio = contexts.Ratios[i];
                    double tall =
                        Guard.IsFinite(ratio) && ratio > 0.0 ? height * contexts.DefaultRatio / ratio : height;
                    double degrees = context.Value(ContextText.Degrees, TextDouble(50, 0.0));
                    placed = Placed(
                        font,
                        big,
                        text,
                        mode,
                        vertical,
                        tall,
                        widthFactor,
                        degrees,
                        context.Value(ContextText.AlignedX, second.X),
                        context.Value(ContextText.AlignedY, second.Y)
                    );

                    if (placed != null && context.Has(ContextText.X) && context.Has(ContextText.Y))
                    {
                        context.Write(ContextText.X, placed[0], ContextText.Y, placed[1]);
                    }
                }
            }
            finally
            {
                _realigning = false;
            }
        }

        /// <summary>Whether the drawing's fonts measure the text's words, as <see cref="GeometricExtents"/> measures them.</summary>
        internal bool IsMeasured => Words(Height, WidthFactor, out _, out _) != null;

        /// <summary>
        /// How far a mirror across a line that crosses the text's baseline square moves the text where it keeps it readable, as
        /// a flip of AutoCAD 2021 keeps an attribute definition (check F1b), the text upright and turned as before: one aligned
        /// right, aligned or fitted on its baseline (72 of 2, 3 and 5) so that its first point is the second mirrored, and its
        /// second the first; one held by the middle of its words (72 of 4) so that its second point is mirrored; any other so
        /// that the box of its ink is mirrored, "L1" of txt.shx 5 high at 20, inked 6.67 to the right of it, mirrored about
        /// x = 45 standing at 63.33; of a TrueType font, the ink of its words without the spaces at either end, as if they were
        /// not there (check F2), "L1 " and " L1" of Arial both as "L1". Null where its words are not measured
        /// (<see cref="IsMeasured"/>), or the mirror does not reverse its baseline and keep its upright.
        /// </summary>
        internal GeoVector3? Readable(GeoTransform3 mirror)
        {
            DxfTextLine words = Words(Height, WidthFactor, out DxfFont font, out DxfFonts.Big big);

            if (words != null && font is DxfTrueTypeFont)
            {
                words = DxfFonts.Text(font, big, TextString.Trim(' '), Height, WidthFactor);
            }

            if (words == null || !words.HasInk)
            {
                return null;
            }

            int horizontal = Horizontal();
            int vertical = TextInt(VerticalCode, 0);
            GeoVector3 normal = Normal;
            GeoPoint3 at = WorldToOcs(normal, Position);
            double turn = Rotation;
            GeoVector3 along = OcsToWorld(normal, new GeoPoint3(at.X + Math.Cos(turn), at.Y + Math.Sin(turn), at.Z))
                .ToVector()
                .Subtract(Position.ToVector());
            GeoVector3 up = normal.CrossProduct(along);
            GeoVector3 mirroredAlong = mirror.Transform(along);
            GeoVector3 mirroredUp = mirror.Transform(up);

            if (!(mirroredAlong.DotProduct(along) < -1.0 + 1E-9) || !(mirroredUp.DotProduct(up) > 1.0 - 1E-9))
            {
                return null;
            }

            if (vertical == 0 && (horizontal == 2 || horizontal == 3 || horizontal == 5))
            {
                return Position.GetVectorTo(mirror.Transform(AlignmentPoint));
            }

            if (vertical == 0 && horizontal == 4)
            {
                return AlignmentPoint.GetVectorTo(mirror.Transform(AlignmentPoint));
            }

            GeoPoint3 placed = mirror.Transform(Position).Add(along.Multiply(-(words.InkLeft + words.InkRight)));
            return Position.GetVectorTo(placed);
        }

        /// <summary>
        /// The turn-over a mirror gives a text so that its words stay readable, as AutoCAD 2021's <c>TransformBy</c> does it
        /// (probed in its console on texts of the planes (0, 0, 1), (0, 1, 0), (1, 1, 1) and (0, 0, -1) under 6 mirrors; the
        /// words measured in txt.shx 3 high): a reflection in the text's own plane before the mirror, across the upright of
        /// its words where its baseline mirrored and reversed reads in the half of the circle it reads in now, else across its
        /// baseline, through the middle of the box of its ink along it or up it. Mirrored after it, the transformation is a
        /// turn and a move, so that the words are not written backwards and its point moves with the width of its ink: "ABC"
        /// 3 high at (20, 40), its ink 8.445 wide, mirrored across x = 40 stands at (51.555, 40), still reading along x, and
        /// mirrored across the plane it lies in at (28.445, 40) on the normal (0, 0, -1), its first point (-28.445, 40) there.
        /// A text held by a second point is turned over with its first point (10) worked out from that point as
        /// <see cref="Realign"/> works it out, and both points carried, as AutoCAD 2021 carries them (probed on 196 texts, 14
        /// modes, in 4 planes at 3 angles, under 7 mirrors): those held by the left, the centre or the right of their ink at the
        /// baseline, the bottom, the middle or the top (72 of 0 to 2, 73 of 1 to 3, and 1 with 73 of nought) as one aligned left
        /// on its baseline is, "ABC" centred on (30, 45), its first point (26, 45), across x = 40 standing at (45.555, 45)
        /// held at (49.555, 45); and those whose two points are the ends of their baseline (72 of 3 and 5, and 2 with 73 of
        /// nought) across the middle of those two points, so that the first point is the second mirrored and the second the
        /// first, the ink not asked: "ABC" aligned on (20, 40) and (40, 48) across x = 40 runs from (60, 40) to (40, 48),
        /// reading from the first to the second as it did. A text held by its middle (72 of 4), where it is turned across its
        /// upright, is mirrored as it stands, each point carried as the mirror carries it, so that its second point is the
        /// middle of its words again and its first, which AutoCAD leaves where the mirror takes it, is given back in
        /// <paramref name="first"/>; across its baseline it is turned over like the others. An annotative text turns each of
        /// its contexts over so, by the words at that context's height (<see cref="TurnedContexts"/>), and its record as the
        /// default context. Null where the words are not measured (<see cref="IsMeasured"/>), as with no fonts, except for the
        /// turn across the upright of a text held by its two ends or its middle, which needs none; a text written backwards or
        /// upside down, or of several lines; and one of 72 of 3, 4 or 5 with a 73 not nought, which AutoCAD reads with no
        /// vertical mode.
        /// </summary>
        /// <param name="mirror">The mirror.</param>
        /// <param name="first">The point (world coordinates) the text's first point is to be left at after the mirror, where it
        /// is not where the turn-over and the mirror put it; null elsewhere.</param>
        /// <returns>The turn-over, to be followed by the mirror; null where the text is mirrored as it is.</returns>
        internal GeoTransform3 TurnedOver(GeoTransform3 mirror, out GeoPoint3? first)
        {
            first = null;

            if (!Turnable)
            {
                return null;
            }

            int horizontal = TextInt(72, 0);
            int vertical = TextInt(VerticalCode, 0);

            // A text held by its second point has its first worked out from it first, as AutoCAD reads it.
            if (horizontal != 0 || vertical != 0)
            {
                Realign();
            }

            GeoVector3 normal = Normal;
            return Flip(
                mirror,
                OcsToWorld(normal, TextPoint(10) ?? Origin),
                OcsToWorld(normal, TextPoint(11) ?? Origin),
                TextDouble(50, 0.0) * (Math.PI / 180.0),
                TextDouble(40, 2.5),
                horizontal,
                vertical,
                out first
            );
        }

        /// <summary>Whether a mirror may turn the text over: it is not written backwards or upside down, nor an attribute of several lines.</summary>
        private bool Turnable => !IsMirroredInX && !IsMirroredInY && !(IsAttribute && Embedded);

        /// <summary>
        /// The turn-over a mirror gives each context of an annotative text, as <see cref="TurnedOver"/> gives the record's: the
        /// context's point, second point, angle and horizontal mode, and the words at the height of its scale; each with the
        /// point to leave the first at, or none, where the context is mirrored as it is.
        /// </summary>
        /// <param name="mirror">The mirror.</param>
        /// <returns>One turn-over and one first point to each context, null for none; null for a text of no contexts.</returns>
        private List<KeyValuePair<GeoTransform3, GeoPoint3?>> TurnedContexts(GeoTransform3 mirror)
        {
            ShownContexts contexts = TextContexts();

            if (contexts == null)
            {
                return null;
            }

            var turns = new List<KeyValuePair<GeoTransform3, GeoPoint3?>>(contexts.All.Count);
            GeoVector3 normal = Normal;
            GeoPoint3 own = TextPoint(10) ?? Origin;
            GeoPoint3 ownSecond = TextPoint(11) ?? Origin;
            double height = TextDouble(40, 2.5);

            for (int i = 0; i < contexts.All.Count; i++)
            {
                GeoPoint3? first = null;
                GeoTransform3 turn = null;

                if (Turnable)
                {
                    ContextGroups context = ContextText.Read(contexts.All[i]);
                    double ratio = contexts.Ratios[i];
                    double tall =
                        Guard.IsFinite(ratio) && ratio > 0.0 ? height * contexts.DefaultRatio / ratio : height;
                    turn = Flip(
                        mirror,
                        OcsToWorld(
                            normal,
                            new GeoPoint3(
                                context.Value(ContextText.X, own.X),
                                context.Value(ContextText.Y, own.Y),
                                own.Z
                            )
                        ),
                        OcsToWorld(
                            normal,
                            new GeoPoint3(
                                context.Value(ContextText.AlignedX, ownSecond.X),
                                context.Value(ContextText.AlignedY, ownSecond.Y),
                                own.Z
                            )
                        ),
                        context.Value(ContextText.Degrees, TextDouble(50, 0.0)) * (Math.PI / 180.0),
                        tall,
                        (int)context.Value(ContextText.Horizontal, TextInt(72, 0)),
                        TextInt(VerticalCode, 0),
                        out first
                    );
                }

                turns.Add(new KeyValuePair<GeoTransform3, GeoPoint3?>(turn, first));
            }

            return turns;
        }

        /// <summary>
        /// The turn-over of a text at a place, as <see cref="TurnedOver"/> works it out: the text's first point, second point,
        /// angle (in radians), height and horizontal and vertical modes given, in the world.
        /// </summary>
        private GeoTransform3 Flip(
            GeoTransform3 mirror,
            GeoPoint3 position,
            GeoPoint3 second,
            double turn,
            double height,
            int horizontal,
            int vertical,
            out GeoPoint3? first
        )
        {
            first = null;

            if (horizontal >= 3 && horizontal <= 5 && vertical != 0)
            {
                return null;
            }

            bool ends = horizontal == 3 || horizontal == 5 || (horizontal == 2 && vertical == 0);
            bool middle = horizontal == 4;
            GeoVector3 normal = Normal;
            GeoPoint3 at = WorldToOcs(normal, position);
            GeoVector3 along = OcsToWorld(normal, new GeoPoint3(at.X + Math.Cos(turn), at.Y + Math.Sin(turn), at.Z))
                .ToVector()
                .Subtract(position.ToVector());
            GeoVector3 up = normal.CrossProduct(along);

            if (!(along.Length > 0.0) || !(up.Length > 0.0))
            {
                return null;
            }

            along = along.Divide(along.Length);
            up = up.Divide(up.Length);
            bool upright = DxfOcsTransform.AcrossUpright(along, normal, mirror);

            if (upright && middle)
            {
                first = mirror.Transform(position);
                return GeoTransform3.Mirror(new GeoPlane3(second, along));
            }

            if (upright && ends)
            {
                return GeoTransform3.Mirror(
                    new GeoPlane3(
                        position.Add(along.Multiply(position.GetVectorTo(second).DotProduct(along) / 2.0)),
                        along
                    )
                );
            }

            DxfTextLine words = Words(height, WidthFactor, out DxfFont font, out DxfFonts.Big big);

            if (words != null && font is DxfTrueTypeFont)
            {
                words = DxfFonts.Text(font, big, TextString.Trim(' '), height, WidthFactor);
            }

            if (words == null || !words.HasInk)
            {
                return null;
            }

            if (upright)
            {
                return GeoTransform3.Mirror(
                    new GeoPlane3(position.Add(along.Multiply((words.InkLeft + words.InkRight) / 2.0)), along)
                );
            }

            double across = words.Box(out _, out _, out double bottom, out double top)
                ? (bottom + top) / 2.0
                : height / 2.0;
            return GeoTransform3.Mirror(new GeoPlane3(position.Add(up.Multiply(across)), up));
        }

        /// <summary>The height of an aligned text, or the width factor of a fitted one, worked out anew after its first point is set, as <see cref="Realign"/> works it out; nothing for a text of another justification, whose first point is its own.</summary>
        private void Refit()
        {
            int horizontal = TextInt(72, 0);

            if (horizontal == 3 || horizontal == 5)
            {
                Realign();
            }
        }

        /// <summary>
        /// Where a text's first point stands, in the system of its plane, for its words held by its second point as its modes
        /// say, turned by an angle in degrees, as <see cref="Realign"/> works it out; null for modes AutoCAD has no place for, for
        /// words of no box, and for one at the bottom with a big font that is not read. One at the bottom with a big font that is
        /// stands on the big font's depth, as AutoCAD 2021 stands it whatever its letters (checks F1a and F3): txt and gbcbig 4
        /// high 32 up, its depth 64 over its height 8. The words measured already at the height and width factor are given where
        /// they are.
        /// </summary>
        private static double[] Placed(
            DxfFont font,
            DxfFonts.Big big,
            string text,
            int horizontal,
            int vertical,
            double height,
            double widthFactor,
            double degrees,
            double x,
            double y,
            DxfTextLine measured = null
        )
        {
            if (
                horizontal < 0
                || horizontal > 4
                || horizontal == 3
                || vertical < 0
                || vertical > 3
                || (horizontal == 4 && vertical != 0)
                || (horizontal == 0 && vertical == 0)
            )
            {
                return null;
            }

            DxfTextLine line = measured ?? DxfFonts.Text(font, big, text, height, widthFactor);

            if (
                line == null
                || !line.AlignmentBox(out double left, out double right, out double bottom, out double top)
                || (vertical == 1 && big != null && big.Font == null)
            )
            {
                return null;
            }

            DxfFont deep = big?.Font ?? font;
            double across =
                horizontal == 0 ? 0.0
                : horizontal == 2 ? -right
                : -(left + right) / 2.0;
            double up =
                horizontal == 4 ? -(bottom + top) / 2.0
                : vertical == 3 ? -height
                : vertical == 2 ? -height / 2.0
                : vertical == 1 ? height * deep.Below / deep.Above
                : 0.0;
            double radians = degrees * (Math.PI / 180.0);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new[] { x + (across * cos) - (up * sin), y + (across * sin) + (up * cos) };
        }

        /// <summary>
        /// The height of an aligned text, or the width factor of a fitted one, its two points apart over the right of the box of
        /// its words gives the one it has, as AutoCAD 2021 gives them (check F1a): "ABC" of txt.shx 4 high aligned on two points
        /// 30 apart is 11.25 high. Left as it is where its words have no box, or reach no way right, or its points are one.
        /// </summary>
        private void Fit(
            DxfFont font,
            DxfFonts.Big big,
            int horizontal,
            double height,
            double widthFactor,
            GeoPoint3 first,
            GeoPoint3 second
        )
        {
            DxfTextLine line = DxfFonts.Text(font, big, TextString, height, widthFactor);
            double apart = Math.Sqrt(
                ((second.X - first.X) * (second.X - first.X)) + ((second.Y - first.Y) * (second.Y - first.Y))
            );

            if (
                line == null
                || !line.AlignmentBox(out _, out double right, out _, out _)
                || !(right > 0.0)
                || !(apart > 0.0)
            )
            {
                return;
            }

            double factor = apart / right;

            if (horizontal == 3)
            {
                SetText(NumberGroup(40, height * factor, height));
            }
            else
            {
                SetText(NumberGroup(41, widthFactor * factor, widthFactor));
            }
        }
    }
}
