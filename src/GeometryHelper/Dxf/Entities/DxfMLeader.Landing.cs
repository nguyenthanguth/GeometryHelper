using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        // Set while SetFromStyle writes the style's values through the properties, which then leave the content and the landings
        // where they are, as the library wrote them before AutoCAD 2021's moving of them was read.
        private bool styling;

        #region The landing of a block

        /// <summary>
        /// Where AutoCAD 2021 lands a new leader from a point on a block (<see cref="AddLeaderLine(GeoPoint3)"/>): the point its
        /// dogleg starts at and the dogleg's direction, along the plane's x axis on the side of the block's box the point is on
        /// (its middle along x, the point on it taken to the right), the dogleg ending where the block's shapes meet the line along
        /// x through the middle of its box (block connected at its extents, 176 of nought) or through its position (at its base,
        /// 1), the outermost such point on that side, else that point of the line; no gap. The box is the block's own, of the shapes
        /// of its entities the library reads, carried as the block is drawn (15, 16, 46 and 14 of the context data) and boxed
        /// in the plane. False where the MULTILEADER holds no block of its drawing, or the block has no shape the library reads.
        /// </summary>
        private bool BlockLanding(Rec rec, Ctx ctx, GeoPoint3 point, out GeoPoint3 landing, out GeoVector3 dogleg)
        {
            landing = point;
            dogleg = new GeoVector3(1.0, 0.0, 0.0);
            Axes(ctx, out GeoVector3 x, out _);

            if (!BlockMeets(rec, ctx, point.ToVector().DotProduct(x), out bool onLeft, out GeoPoint3 end))
            {
                return false;
            }

            dogleg = onLeft ? x : x.Negate();
            double length = Num(Text(rec.After, 41), 8.0) * Num(Text(ctx.Head, 40), 1.0);
            landing = end.Add(dogleg.Multiply(-length));
            return true;
        }

        /// <summary>
        /// The block of the content as it is drawn (the context's block: 15, else the base point of the content; 16, else the
        /// MULTILEADER's own 10; 46, else its own 43; and 14); null where the MULTILEADER holds no block of its drawing.
        /// </summary>
        private DxfBlockReference PlacedBlock(Rec rec, Ctx ctx, out DxfBlockTableRecord block)
        {
            Section(ctx.Head, 296, BlockCodes, out int from, out int to);
            string handle = from < 0 ? null : BlockText(rec, 341) ?? Text(rec.After, 344);
            block = handle == null ? null : BlockByHandle(Database(), handle.Trim());

            if (block == null)
            {
                return null;
            }

            List<KeyValuePair<int, string>> b = ctx.Head.GetRange(from, to - from);
            GeoPoint3 position = PointOf(b, 15, PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0)));
            GeoPoint3 scale = PointOf(b, 16, PointOf(rec.After, 10, new GeoPoint3(1.0, 1.0, 1.0)));
            double turn = Num(Text(b, 46), Num(Text(rec.After, 43), 0.0));
            GeoVector3 normal = PointOf(b, 14, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
            var insert = new DxfBlockReference(position, block) { ScaleFactors = scale.ToVector(), Rotation = turn };

            if (normal.Length > 0.0)
            {
                insert.SetNormalKeepingPlace(normal);
            }

            return insert;
        }

        /// <summary>
        /// Where a leader meets the block of the content as AutoCAD 2021 lands one, on the side of its box a point along the
        /// plane's x axis (<paramref name="side"/>) is on (its middle, the point on it taken to the right): where the block's
        /// shapes meet the line along x through the middle of its box (block connected at its extents, 176 of nought) or through
        /// its position (at its base, 1), the outermost such point on that side, else that point of the line. The box is the
        /// block's own, of the shapes of its entities the library reads, carried as the block is drawn (<see cref="PlacedBlock"/>)
        /// and boxed in the plane. False where the MULTILEADER holds no block of its drawing, or the block has no shape the library
        /// reads.
        /// </summary>
        private bool BlockMeets(Rec rec, Ctx ctx, double side, out bool onLeft, out GeoPoint3 end)
        {
            onLeft = true;
            end = new GeoPoint3(0.0, 0.0, 0.0);
            DxfBlockReference insert = PlacedBlock(rec, ctx, out DxfBlockTableRecord block);

            if (insert == null)
            {
                return false;
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);

            // The block's own box, its corners carried as the block is drawn and boxed along the plane's axes.
            double[] own = null;

            foreach (DxfEntity entity in block)
            {
                GeoAabb3 box;

                try
                {
                    box = entity.GeometricExtents;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                own =
                    own == null
                        ? new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }
                        : new[]
                        {
                            Math.Min(own[0], box.Min.X),
                            Math.Min(own[1], box.Min.Y),
                            Math.Min(own[2], box.Min.Z),
                            Math.Max(own[3], box.Max.X),
                            Math.Max(own[4], box.Max.Y),
                            Math.Max(own[5], box.Max.Z),
                        };
            }

            if (own == null)
            {
                return false;
            }

            GeoTransform3 placed = insert.BlockTransform;
            double left = double.PositiveInfinity;
            double right = double.NegativeInfinity;
            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;

            for (int corner = 0; corner < 8; corner++)
            {
                GeoVector3 c = placed
                    .Transform(
                        new GeoPoint3(
                            own[(corner & 1) * 3],
                            own[1 + ((corner >> 1) & 1) * 3],
                            own[2 + ((corner >> 2) & 1) * 3]
                        )
                    )
                    .ToVector();
                left = Math.Min(left, c.DotProduct(x));
                right = Math.Max(right, c.DotProduct(x));
                low = Math.Min(low, c.DotProduct(y));
                high = Math.Max(high, c.DotProduct(y));
            }

            double middle = (left + right) / 2.0;
            onLeft = side < middle;
            GeoVector3 position = insert.Position.ToVector();
            bool atBase = Whole(Text(rec.After, 176), 0) == 1;

            // The line along x the leader meets the block on, through the middle of the box or the block's position.
            double along = atBase ? position.DotProduct(x) : middle;
            double up = atBase ? position.DotProduct(y) : (low + high) / 2.0;
            GeoVector3 across = position
                .Subtract(x.Multiply(position.DotProduct(x)))
                .Subtract(y.Multiply(position.DotProduct(y)));
            GeoPoint3 Point(double u) => Pt(across.Add(x.Multiply(u)).Add(y.Multiply(up)));
            double reach = right - left + Math.Abs(along - middle) + 1.0;
            var line = new DxfLine(Point(Math.Min(left, along) - reach), Point(Math.Max(right, along) + reach));
            double met = double.NaN;

            foreach (DxfEntity entity in insert.Explode())
            {
                IReadOnlyList<GeoPoint3> points;

                try
                {
                    points = entity.IntersectWith(line, DxfIntersect.OnBothOperands);
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                foreach (GeoPoint3 p in points)
                {
                    double u = p.ToVector().DotProduct(x);
                    met = double.IsNaN(met) || (onLeft ? u < met : u > met) ? u : met;
                }
            }

            end = Point(double.IsNaN(met) ? along : met);
            return true;
        }

        /// <summary>
        /// The block of the content as AutoCAD 2021 scales it with the MULTILEADER (<see cref="Scale"/>), its drawn scale (16)
        /// scaled by a factor: its matrix (47) made the block's transform where it stands, before it moves; then, with one leader
        /// on one side, the block moved so that it meets that leader's dogleg, now as long as drawn, at its end, the base point of
        /// the content (10) put where a leader from the left meets it (k2-BOX-scale2-after, k5-BOX-scale2-right); with leaders on
        /// both sides or several on one, the block kept and each leader on a side landed anew at it (k5-BOX-scale2-both).
        /// </summary>
        private void Reblocked(Rec rec, Ctx ctx, double factor)
        {
            Section(ctx.Head, 296, BlockCodes, out int from, out int to);

            if (from < 0)
            {
                return;
            }

            int at = ctx.Head.FindIndex(from, to - from, g => g.Key == 16);

            if (at >= 0)
            {
                GeoPoint3 drawn = PointAt(ctx.Head, at);
                PutPoint(
                    ctx.Head,
                    16,
                    new GeoPoint3(drawn.X * factor, drawn.Y * factor, drawn.Z * factor),
                    ContextOrder,
                    from,
                    to
                );
                Section(ctx.Head, 296, BlockCodes, out from, out to);
            }

            DxfBlockReference placed = PlacedBlock(rec, ctx, out _);

            if (placed != null)
            {
                SetMatrix(ctx.Head, from, to, placed.BlockTransform);
            }

            if (placed == null || !Kept(rec, ctx, false))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out _);
            Sides(ctx, x, out int left, out int right);

            if (left + right == 1)
            {
                LeaderPart leader = ctx.Leaders.Find(l => Side(l, x) != 0);
                int side = Side(leader, x);
                GeoVector3 dogleg = Unit(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                GeoPoint3 target = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0))
                    .Add(dogleg.Multiply(Num(Text(leader.Head, 40), 0.0)));

                if (BlockMeets(rec, ctx, side * double.MaxValue, out _, out GeoPoint3 end))
                {
                    GeoVector3 by = end.GetVectorTo(target);
                    Section(ctx.Head, 296, BlockCodes, out from, out to);
                    MovePoint(ctx.Head, 15, p => p.Add(by), ContextOrder, from, to);

                    if (BlockMeets(rec, ctx, -double.MaxValue, out _, out GeoPoint3 basePoint))
                    {
                        PutPoint(ctx.Head, 10, basePoint, ContextOrder);
                    }
                }

                return;
            }

            Landed(rec, ctx);
        }

        /// <summary>
        /// The block moved as AutoCAD 2021 moves it when its scale or turn is set (<see cref="BlockScale"/>,
        /// <see cref="BlockRotation"/>), with leaders or none: so that a leader from the left meets it at the base point of the
        /// content (10 of the context data), its matrix (47) made its transform there, and each leader on a side landed anew at it
        /// (b-k-blockscale2-*, b-k-blockrot30-*, k-both-scale2-blockscale3). The box of lines from (-10, -5) to (20, 15) placed at
        /// (100, 50, 0), its base point (90, 55, 0), scaled by 2 stands at (110, 45, 0).
        /// </summary>
        private void Rebased(Rec rec, Ctx ctx)
        {
            DxfBlockReference placed = PlacedBlock(rec, ctx, out _);

            if (placed == null || !Kept(rec, ctx, false))
            {
                return;
            }

            Section(ctx.Head, 296, BlockCodes, out int from, out int to);

            if (Text(ctx.Head, 10) != null && BlockMeets(rec, ctx, -double.MaxValue, out _, out GeoPoint3 left))
            {
                GeoVector3 by = left.GetVectorTo(PointOf(ctx.Head, 10, left));
                MovePoint(ctx.Head, 15, p => p.Add(by), ContextOrder, from, to);
                placed = PlacedBlock(rec, ctx, out _);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
            }

            SetMatrix(ctx.Head, from, to, placed.BlockTransform);
            Landed(rec, ctx);
        }

        /// <summary>
        /// Each leader on a side of the content landed anew at it, as AutoCAD 2021 lands the leaders it keeps the content for, a
        /// leader moved off its landing snapped back (d6-two-setdl0): its dogleg ending where it meets the block
        /// (<see cref="BlockMeets"/>), or the words on their left (<see cref="WordsMeet"/>). Leaders on the right of words, which
        /// AutoCAD lands by their font's width, are left.
        /// </summary>
        private void Landed(Rec rec, Ctx ctx) => LandAnew(rec, ctx, ctx.Leaders);

        /// <summary>The leaders given landed anew at the content, as <see cref="Landed"/> lands each.</summary>
        private void LandAnew(Rec rec, Ctx ctx, List<LeaderPart> leaders)
        {
            Axes(ctx, out GeoVector3 x, out _);
            bool block = HasSection(ctx, 296, BlockCodes);
            var wordsMeet = new GeoPoint3(0.0, 0.0, 0.0);
            var rightMeet = new GeoPoint3(0.0, 0.0, 0.0);
            bool words = !block && WordsMeet(rec, ctx, out wordsMeet);
            bool right = !block && WordsMeetRight(rec, ctx, out rightMeet);

            foreach (LeaderPart leader in leaders)
            {
                int side = Side(leader, x);
                GeoPoint3 end = side > 0 ? rightMeet : wordsMeet;

                if (
                    side == 0
                    || (
                        block ? !BlockMeets(rec, ctx, side * double.MaxValue, out _, out end)
                        : side > 0 ? !right
                        : !words
                    )
                )
                {
                    continue;
                }

                GeoVector3 dogleg = Unit(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                PutPoint(leader.Head, 10, end.Add(dogleg.Multiply(-Num(Text(leader.Head, 40), 0.0))), LeaderOrder);
            }
        }

        /// <summary>
        /// Where a leader meets the words on their left, as AutoCAD 2021 lands one: the <see cref="LandingGap"/> short of their left
        /// edge, as far below their top as the attachment of the left side says (<see cref="Below(Ctx, int, double)"/>); false where the words are
        /// not held at their top along the plane's x axis, met on their sides (271 of nought), or there are none.
        /// Words "ABC" 4 high at (160, 40, 0), met at the middle of their top line with a gap of 2, are met at (158, 38, 0). Words
        /// that run against the x axis (<see cref="Reversed"/>) are met the gap short of their far end, where the drawing's fonts
        /// measure them (check D38): "ABC" of txt.shx 10.67 wide mirrored across the XY plane, held at (110.67, 50, 0), at
        /// (98, 48, 0); so too words held at their top centre or right, from their left edge (check D38b, <see cref="FromLeft"/>).
        /// </summary>
        private bool WordsMeet(Rec rec, Ctx ctx, out GeoPoint3 meet)
        {
            meet = new GeoPoint3(0.0, 0.0, 0.0);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0 || Whole(Text(rec.After, 271), 0) != 0)
            {
                return false;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            double below = Below(ctx, AttachmentOf(rec, true), Height(ctx));
            double offset = FromLeft(ctx, m, along, x);

            if (double.IsNaN(offset) || !AlongPlane(ctx, along, x) || double.IsNaN(below))
            {
                return false;
            }

            GeoPoint3 left = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));

            if (offset != 0.0)
            {
                left = left.Add(x.Multiply(-offset));
            }

            meet = left.Add(x.Multiply(-Num(Text(ctx.Head, 145), 2.0))).Add(y.Multiply(-below));
            return true;
        }

        /// <summary>
        /// How far along the plane's x axis the words are held from their left edge, as AutoCAD 2021 meets them: nought for words
        /// held at their top left along it; all of their width for words that run against it held at their top left
        /// (<see cref="Reversed"/>, check D38), and half of it or all of it for words along it held at their top centre or right
        /// (check D38b), the width as the drawing's <see cref="DxfDatabase.FontSearchPath"/> measures them (<see cref="WordsWidth"/>):
        /// "ABC" of txt.shx 10.67 wide held at their top right, 10.67. NaN where that needs a width no font gives, or the words are
        /// held otherwise.
        /// </summary>
        private double FromLeft(Ctx ctx, List<KeyValuePair<int, string>> m, GeoVector3 along, GeoVector3 x)
        {
            int held = Whole(Text(m, 171), 1);
            bool backward = along.Subtract(x).Length > 1E-9;

            if (held == 1 && !backward)
            {
                return 0.0;
            }

            if (held < 1 || held > 3 || (backward && held != 1) || !WordsWidth(ctx, out double width))
            {
                return double.NaN;
            }

            return held == 2 ? width / 2.0 : width;
        }

        /// <summary>
        /// Where a leader meets the words on their right, as AutoCAD 2021 lands one (check F1a): the <see cref="LandingGap"/> past
        /// the right of their ink, as wide as the words' <see cref="DxfMText.ActualWidth"/> from where they are held, as far below
        /// their top as the attachment of the right side says (<see cref="Below(Ctx, int, double)"/>); false where the drawing's
        /// <see cref="DxfDatabase.FontSearchPath"/> does not measure them (<see cref="WordsWidth"/>), or they are not held at
        /// their top left along the plane's x axis and met on their sides. "ABC" of txt.shx 4 high at (100, 50, 0), met at the
        /// middle of their top line with a gap of 2, are met at (112.67, 48, 0), a dogleg of 8 starting at (120.67, 48, 0). Words
        /// that run against the x axis (<see cref="Reversed"/>) are met the gap past where they are held, their right along the
        /// plane (check D38): mirrored across the XY plane and held at (110.67, 50, 0), at (112.67, 48, 0); words held at their top
        /// centre the gap past half their width from it, at their top right the gap past it (check D38b).
        /// </summary>
        private bool WordsMeetRight(Rec rec, Ctx ctx, out GeoPoint3 meet)
        {
            meet = new GeoPoint3(0.0, 0.0, 0.0);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0 || Whole(Text(rec.After, 271), 0) != 0 || !WordsWidth(ctx, out double width))
            {
                return false;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            double below = Below(ctx, AttachmentOf(rec, false), Height(ctx));
            double offset = FromLeft(ctx, m, along, x);

            if (double.IsNaN(offset) || !AlongPlane(ctx, along, x) || double.IsNaN(below))
            {
                return false;
            }

            int held = Whole(Text(m, 171), 1);
            double gap = Num(Text(ctx.Head, 145), 2.0);
            double past =
                along.Subtract(x).Length > 1E-9 || held == 3 ? gap
                : held == 2 ? (width / 2.0) + gap
                : width + gap;
            meet = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0)).Add(x.Multiply(past)).Add(y.Multiply(-below));
            return true;
        }

        /// <summary>
        /// The width of the words of one line, as AutoCAD 2021 measures them for <see cref="DxfMText.ActualWidth"/>
        /// (<see cref="Measured(Ctx)"/>); false where they are not measured.
        /// </summary>
        private bool WordsWidth(Ctx ctx, out double width)
        {
            DxfTextLine line = Measured(ctx);
            width = line == null ? 0.0 : line.ActualWidth;
            return line != null;
        }

        /// <summary>
        /// The words of one line measured as AutoCAD 2021 measures an MTEXT's (<see cref="DxfMText.ActualWidth"/>), at their height
        /// (41 of the context data) in the SHX or TrueType font of their text style (340), where the drawing's
        /// <see cref="DxfDatabase.FontSearchPath"/> finds it; null where it does not, the style is not as wide as high and upright,
        /// or the words are not one line as <see cref="DxfMText.ActualWidth"/> measures one.
        /// </summary>
        private DxfTextLine Measured(Ctx ctx) => Measured(ctx, Height(ctx));

        /// <summary>The words of one line measured as <see cref="Measured(Ctx)"/> measures them, at the height given.</summary>
        private DxfTextLine Measured(Ctx ctx, double height) => Measured(ctx, height, null, Database());

        /// <summary>
        /// The words of one line measured as <see cref="Measured(Ctx)"/> measures them, at the height given, in the font of the
        /// text style of the handle given (the words' own, 340 of the context's MTEXT, where null) of the drawing given.
        /// </summary>
        private static DxfTextLine Measured(Ctx ctx, double height, string styleHandle, DxfDatabase database)
        {
            DxfFonts fonts = database?.Fonts;

            if (fonts == null)
            {
                return null;
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0)
            {
                return null;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            string handle = styleHandle ?? Text(m, 340)?.Trim();

            if (
                handle == null
                || !database.TryGetObject(handle, out DxfDBObject found)
                || !(found is DxfTextStyleTableRecord style)
                || !Same(style.XScale, 1.0)
                || style.ObliquingAngle != 0.0
            )
            {
                return null;
            }

            DxfFont font = fonts.FontOf(style, out DxfFonts.Big big);
            DxfTextLine line = font == null ? null : DxfFonts.MText(font, big, Text(m, 304) ?? string.Empty, height);
            double wrap = Num(Text(m, 43), 0.0);
            return line == null || (wrap > 0.0 && !(line.ActualWidth < wrap)) ? null : line;
        }

        /// <summary>
        /// The words moved to the landing of the first leader on their right, as AutoCAD 2021 moves them where they are measured
        /// (<see cref="WordsMeetRight"/>) and met on their right alone: so that it meets them again, the gap past the right of
        /// their ink and as far below their top as the attachment of the right side says; the base point of the content moved
        /// along x with the words' left, a gap short of it, and the other leaders on the right landed anew (<see cref="Landed"/>).
        /// "ABC" 4 high at (100, 50, 0), met from (120.67, 48, 0), made 10 high stand at (84, 53, 0), the base point from
        /// (98, 48, 0) at (82, 48, 0) (m-right-height10); at a scale of 2, at (79.33, 52, 0), the base point at (75.33, 48, 0)
        /// (m-right-scale2). Words that run against the x axis (<see cref="Reversed"/>), held at their right, move their base point
        /// with their left by how much wider they now are too (<paramref name="widened"/>, check D38): "ABC" mirrored across the XY
        /// plane, held at (110.67, 50, 0) and met from (120.67, 48, 0), made 10 high stand at (110.67, 53, 0), the base point at
        /// (82, 48, 0). False where they are not measured, or no leader on their right has a landing.
        /// </summary>
        private bool Rightward(Rec rec, Ctx ctx, double gapChange, double widened)
        {
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            LeaderPart leader = ctx.Leaders.Find(l => Side(l, x) > 0 && HasLanding(l));
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (leader == null || from < 0 || double.IsNaN(widened) || !WordsMeetRight(rec, ctx, out GeoPoint3 meet))
            {
                return false;
            }

            GeoVector3 dogleg = Unit(PointOf(leader.Head, 11, new GeoPoint3(-1.0, 0.0, 0.0)).ToVector());
            GeoPoint3 end = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0))
                .Add(dogleg.Multiply(Num(Text(leader.Head, 40), 0.0)));
            GeoVector3 by = meet.GetVectorTo(end);
            double along = by.DotProduct(x) - gapChange;
            MoveContent(ctx, by, x.Multiply(widened == 0.0 ? along : along - widened));
            Landed(rec, ctx);
            return true;
        }

        /// <summary>Writes the matrix of the block of the context data (its 16 groups 47, as ezdxf keeps it): a transformation's, row by row.</summary>
        private static void SetMatrix(List<KeyValuePair<int, string>> head, int from, int to, GeoTransform3 transform)
        {
            var at = new List<int>();

            for (int i = from; i < to; i++)
            {
                if (head[i].Key == 47)
                {
                    at.Add(i);
                }
            }

            if (at.Count != 16)
            {
                return;
            }

            for (int i = 0; i < 16; i++)
            {
                if (!Same(transform[i / 4, i % 4], Num(head[at[i]].Value, double.NaN)))
                {
                    head[at[i]] = G(47, transform[i / 4, i % 4]);
                }
            }
        }

        #endregion

        #region The landing kept

        /// <summary>
        /// The unit x and y axes of the plane of the context data (111 and 112), the y axis turned back where the words run
        /// against the x axis (<see cref="Reversed"/>), so that it is their up, as AutoCAD 2021 moves them (check D38).
        /// </summary>
        private static void Axes(Ctx ctx, out GeoVector3 x, out GeoVector3 y)
        {
            x = Unit(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            y = Unit(PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector());

            if (Reversed(ctx))
            {
                y = y.Negate();
            }
        }

        /// <summary>
        /// Whether the words run against the plane's x axis (13 of the context's MTEXT, 111) and say so (294 of 1, as AutoCAD
        /// 2021 writes it), as those of a multileader mirrored across its plane do: held at the top left of their own reading,
        /// which is their right along the plane, their up against its y axis. "ABC" 10.67 wide at (100, 50, 0) mirrored across
        /// the XY plane is held at (110.67, 50, 0).
        /// </summary>
        private static bool Reversed(Ctx ctx)
        {
            if (ctx.Top == null || Whole(Text(ctx.Top, 294), 0) != 1)
            {
                return false;
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0)
            {
                return false;
            }

            GeoVector3 x = Unit(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoVector3 along = Unit(
                PointOf(ctx.Head.GetRange(from, to - from), 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector()
            );
            return along.Add(x).Length <= 1E-9;
        }

        /// <summary>Whether the words run along the plane's x axis, or against it as they say (<see cref="Reversed"/>).</summary>
        private static bool AlongPlane(Ctx ctx, GeoVector3 along, GeoVector3 x) =>
            along.Subtract(x).Length <= 1E-9 || Reversed(ctx);

        /// <summary>
        /// How much more the words are now wide than at a height before, where they run against the plane's x axis
        /// (<see cref="Reversed"/>) and the drawing's fonts measure them at both, as AutoCAD 2021 moves such words held at their
        /// right to keep their left edge (check D38); nought where they run along it; NaN where they are not measured.
        /// </summary>
        private double Widened(Ctx ctx, double wasHeight)
        {
            if (!Reversed(ctx))
            {
                return 0.0;
            }

            DxfTextLine now = Measured(ctx);
            DxfTextLine was = Measured(ctx, wasHeight);
            return now == null || was == null ? double.NaN : now.ActualWidth - was.ActualWidth;
        }

        /// <summary>
        /// The side of the content a leader meets it on: -1 the left, its dogleg along the plane's x axis, 1 the right, its
        /// dogleg against it, the leader meeting the content on its sides (271 of nought); nought for one that meets the words at
        /// their top or bottom, or whose dogleg runs another way.
        /// </summary>
        private static int Side(LeaderPart leader, GeoVector3 x)
        {
            if (Whole(Text(leader.Head, 271) ?? Text(leader.Tail, 271), 0) != 0)
            {
                return 0;
            }

            GeoVector3 dogleg = PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
            double length = dogleg.Length;
            double along = length > 0.0 ? dogleg.DotProduct(x) / length : 0.0;
            return along >= 1.0 - 1E-9 ? -1
                : along <= -1.0 + 1E-9 ? 1
                : 0;
        }

        /// <summary>How many leaders meet the content on its left and on its right (<see cref="Side"/>).</summary>
        private static void Sides(Ctx ctx, GeoVector3 x, out int left, out int right)
        {
            left = 0;
            right = 0;

            foreach (LeaderPart leader in ctx.Leaders)
            {
                int side = Side(leader, x);
                left += side < 0 ? 1 : 0;
                right += side > 0 ? 1 : 0;
            }
        }

        /// <summary>
        /// Whether the content is moved with its landings, as AutoCAD 2021 moves it: words met on their sides (271 of nought),
        /// running along the plane's x axis (13) or against it, as words mirrored across their plane run (check D38), or a block;
        /// and the MULTILEADER is not being set from its style.
        /// </summary>
        private bool Kept(Rec rec, Ctx ctx, bool words)
        {
            if (styling || Whole(Text(rec.After, 271), 0) != 0)
            {
                return false;
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                Axes(ctx, out GeoVector3 x, out _);
                GeoVector3 along = Unit(
                    PointOf(ctx.Head.GetRange(from, to - from), 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector()
                );
                return AlongPlane(ctx, along, x);
            }

            return !words && HasSection(ctx, 296, BlockCodes);
        }

        /// <summary>
        /// How far below the top of the words a leader meets them for an attachment of a side (173 of the left, 95 of the right),
        /// as AutoCAD 2021 lands it: nought at the top of the top line, half the height of the words at its middle, all of it at
        /// its bottom and a fifth more under it; the middle of the words, the middle of the bottom line, its bottom and a fifth
        /// more under it as the lines stand, each 5/3 of their height times their spacing below the one above, the lines counted
        /// by their breaks (<c>\P</c>); NaN for another attachment, or where the MULTILEADER holds no words. Words of one line the
        /// drawing's <see cref="DxfDatabase.FontSearchPath"/> measures (<see cref="Measured(Ctx)"/>) whose ink reaches below their
        /// baseline are as high as their height and that depth together, as AutoCAD 2021 lands them (check F1b): "Ag" of txt.shx
        /// 4 high, its g 1.33 below the baseline, are met 2.67 below their top at the middle of the top line, and 5.33 at its
        /// bottom, under it and at the underline of attachment 8 (check D38b), where 4.8 is not lower.
        /// </summary>
        private double Below(Ctx ctx, int attachment, double height) => Below(ctx, attachment, height, Measured(ctx));

        /// <summary>How far below the top of the words a leader meets them, as <see cref="Below(Ctx, int, double)"/> says, the words measured as given (none where null).</summary>
        private static double Below(Ctx ctx, int attachment, double height, DxfTextLine words)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0)
            {
                return double.NaN;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            double whole = height + (Breaks(Text(m, 304)) * 5.0 / 3.0 * height * Num(Text(m, 45), 1.0));
            double depth = words == null || !(words.Depth > 0.0) ? 0.0 : words.Depth * height / Height(ctx);
            double line = height + depth;
            whole = depth > 0.0 ? line : whole;

            switch (attachment)
            {
                case 0:
                    return 0.0;
                case 1:
                    return line / 2.0;
                case 2:
                    return whole / 2.0;
                case 3:
                    return whole - (line / 2.0);
                case 4:
                    return whole;
                case 5:
                    return depth > 0.0 ? Math.Max(whole, height + (height / 5.0)) : whole + (height / 5.0);
                case 6:
                    return depth > 0.0 ? Math.Max(line, height + (height / 5.0)) : height + (height / 5.0);
                case 7:
                    return line;
                case 8:
                    return depth > 0.0 ? Math.Max(line, height + (height / 5.0)) : height + (height / 5.0);
                default:
                    return double.NaN;
            }
        }

        /// <summary>The height of the words (41 of the context data).</summary>
        private static double Height(Ctx ctx) => Num(Text(ctx.Head, 41), 4.0);

        /// <summary>The attachment of a side of the words: the left (173) or the right (95).</summary>
        private static int AttachmentOf(Rec rec, bool left) => Whole(Text(rec.After, left ? 173 : 95), 1);

        /// <summary>
        /// Moves the content, the words (12) or the block (15, its matrix left, k-right2-base, b-k-scale2-dl5), along a vector, and
        /// the base point of the content (10 of the context data) along another, as AutoCAD 2021 moves them to keep a landing.
        /// </summary>
        private static void MoveContent(Ctx ctx, GeoVector3 by, GeoVector3 basePointBy)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                MovePoint(ctx.Head, 12, p => p.Add(by), ContextOrder, from, to);
            }

            Section(ctx.Head, 296, BlockCodes, out from, out to);

            if (from >= 0)
            {
                MovePoint(ctx.Head, 15, p => p.Add(by), ContextOrder, from, to);
            }

            MovePoint(ctx.Head, 10, p => p.Add(basePointBy), ContextOrder, 0, -1);
        }

        /// <summary>Moves the point a leader's dogleg starts at (its 10) along a vector.</summary>
        private static void MoveLanding(LeaderPart leader, GeoVector3 by) =>
            MovePoint(leader.Head, 10, p => p.Add(by), LeaderOrder, 0, -1);

        /// <summary>
        /// The words moved as AutoCAD 2021 moves them when the landing gap changes by a length: along x by it, with the leaders on
        /// the right of the words, which land a gap more away from words a gap further on, by twice it; the base point and the
        /// leaders on the left kept, one moved off its landing snapped back (d-two-gap5, <see cref="Landed"/>). A gap of 5 for 2
        /// moves words at (100, 50) to (103, 50) and a dogleg on their right from (120.67, 48) to (126.67, 48).
        /// </summary>
        private void Regapped(Rec rec, Ctx ctx, double change)
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out _);
            MoveContent(ctx, x.Multiply(change), new GeoVector3(0.0, 0.0, 0.0));

            foreach (LeaderPart leader in ctx.Leaders)
            {
                if (Side(leader, x) > 0)
                {
                    MoveLanding(leader, x.Multiply(2.0 * change));
                }
            }

            Landed(rec, ctx);
        }

        /// <summary>
        /// The content and the landings as AutoCAD 2021 keeps them when the doglegs of some leaders (the lengths before, by leader)
        /// change: with one leader, on one side, the content moved along x away from it by the change, its base point with it;
        /// with leaders on both sides, or several on one (b-two-landed-dl15, b-two-landed-setdl1-15), the content kept and each
        /// leader changed landed anew, its dogleg's start moved away from the content by the change, then every leader on a side
        /// landed at the content where the library can tell where it meets it (<see cref="Landed"/>), one moved off its landing
        /// snapped back, the lengths the same too (d6-two-setdl0, d-two-dl-same).
        /// </summary>
        private void Relengthened(Rec rec, Ctx ctx, List<KeyValuePair<LeaderPart, double>> before)
        {
            if (!Kept(rec, ctx, false))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out _);
            Sides(ctx, x, out int left, out int right);

            if ((left > 0 && right > 0) || left > 1 || right > 1)
            {
                foreach (KeyValuePair<LeaderPart, double> was in before)
                {
                    int side = Side(was.Key, x);
                    double change = Num(Text(was.Key.Head, 40), 1.0) - was.Value;

                    if (side != 0)
                    {
                        MoveLanding(was.Key, x.Multiply(side * change));
                    }
                }

                Landed(rec, ctx);
                return;
            }

            int only = left > 0 ? -1 : 1;
            double common = double.NaN;

            foreach (LeaderPart leader in ctx.Leaders)
            {
                if (Side(leader, x) != only)
                {
                    continue;
                }

                // A leader of no landing yet has none to keep: the content stays (none-addleader-dl15).
                if (!HasLanding(leader))
                {
                    return;
                }

                int at = before.FindIndex(p => ReferenceEquals(p.Key, leader));
                double change = at < 0 ? 0.0 : Num(Text(leader.Head, 40), 1.0) - before[at].Value;

                if (!double.IsNaN(common) && !Same(common, change))
                {
                    return;
                }

                common = change;
            }

            if (!double.IsNaN(common))
            {
                GeoVector3 by = x.Multiply(-only * common);
                MoveContent(ctx, by, by);
            }
        }

        /// <summary>
        /// The content and the other landings moved with a leader's landing, as AutoCAD 2021 moves them when the last vertex of a
        /// line of a leader on a side of the content is set: by as much as the dogleg's start moved.
        /// </summary>
        private void Relanded(Rec rec, Ctx ctx, LeaderPart moved, GeoVector3 by)
        {
            if (!Kept(rec, ctx, false))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out _);

            if (Side(moved, x) == 0)
            {
                return;
            }

            MoveContent(ctx, by, by);

            foreach (LeaderPart leader in ctx.Leaders)
            {
                if (!ReferenceEquals(leader, moved) && Side(leader, x) != 0)
                {
                    MoveLanding(leader, by);
                }
            }
        }

        /// <summary>
        /// The words moved as AutoCAD 2021 moves them when the attachment of a side changes from one to another, keeping the
        /// landings of that side: up by as much more as the side is met below their top. With leaders on the left, the words and
        /// the leaders on the right move, for the left; with leaders on the right, for the right, the words move where there are
        /// none on the left, else the leaders on the right are landed anew. The left of no leader moves words of no leader to meet
        /// the base point again, and puts the base point of words met on their right where the left now meets them, as AutoCAD
        /// 2021 does (check D38b): "ABC" at (100, 50, 0) of no leader stand at (100, 52.8, 0) met at the bottom line. Another side
        /// with no leader moves nothing.
        /// </summary>
        private void Reattached(Rec rec, Ctx ctx, bool left, int was, int now)
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            Sides(ctx, x, out int lefts, out int rights);
            double change = Below(ctx, now, Height(ctx)) - Below(ctx, was, Height(ctx));

            if (double.IsNaN(change))
            {
                return;
            }

            // The left with no leader on it: words of no leader moved to meet the base point again, the base point of words met
            // on their right put where the left now meets them, as AutoCAD 2021 moves them (check D38b b-att4set-N,
            // r-rot180-att4-L).
            if (left && lefts == 0)
            {
                if (rights == 0)
                {
                    MoveContent(ctx, y.Multiply(change), new GeoVector3(0.0, 0.0, 0.0));
                }
                else if (WordsMeet(rec, ctx, out GeoPoint3 meet))
                {
                    PutPoint(ctx.Head, 10, meet, ContextOrder);
                }

                return;
            }

            if ((left ? lefts : rights) == 0)
            {
                return;
            }

            GeoVector3 up = y.Multiply(change);

            // The right alone: the words move to the leaders, the base point with them.
            if (!left && lefts == 0)
            {
                MoveContent(ctx, up, up);
                return;
            }

            // The left: the words move, the base point stays, and the leaders on the right follow the words; the right with
            // leaders on the left: the words stay and the leaders on the right are landed lower by as much.
            if (left)
            {
                MoveContent(ctx, up, new GeoVector3(0.0, 0.0, 0.0));
            }

            foreach (LeaderPart leader in ctx.Leaders)
            {
                if (Side(leader, x) > 0)
                {
                    MoveLanding(leader, left ? up : up.Negate());
                }
            }
        }

        /// <summary>
        /// The words moved as AutoCAD 2021 moves them when their height changes, where every leader meets them on the left: up by
        /// as much more as the left is met below their top, a leader moved off its landing snapped back (two-unlanded-height10,
        /// <see cref="Landed"/>). Words with a leader on their right are left, AutoCAD landing it as far right as its font makes
        /// them wide. Words that run against the plane's x axis (<see cref="Reversed"/>), held at their right, move along it by
        /// how much wider they are too, to keep their left edge, where the drawing's fonts measure them, and are left where they
        /// do not (check D38): "ABC" mirrored across the XY plane, held at (110.67, 50, 0) and met from the left, made 10 high
        /// stand at (126.67, 53, 0).
        /// </summary>
        private void Reheighted(Rec rec, Ctx ctx, double was)
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            Sides(ctx, x, out int left, out int right);
            int attachment = AttachmentOf(rec, true);
            double change = Below(ctx, attachment, Height(ctx)) - Below(ctx, attachment, was);
            double widened = Widened(ctx, was);

            if (double.IsNaN(widened))
            {
                return;
            }

            if (left > 0 && right == 0 && !double.IsNaN(change))
            {
                MoveContent(
                    ctx,
                    widened == 0.0 ? y.Multiply(change) : y.Multiply(change).Add(x.Multiply(widened)),
                    new GeoVector3(0.0, 0.0, 0.0)
                );
                Landed(rec, ctx);
            }
            else if (right > 0 && WordsWidth(ctx, out _))
            {
                // Measured: met on the right alone, the words move to its landing; met on both sides, as on the left, every
                // leader then landed anew (m-right-height10, m2-both-height10).
                if (left == 0)
                {
                    Rightward(rec, ctx, 0.0, widened);
                }
                else if (!double.IsNaN(change))
                {
                    MoveContent(
                        ctx,
                        widened == 0.0 ? y.Multiply(change) : y.Multiply(change).Add(x.Multiply(widened)),
                        new GeoVector3(0.0, 0.0, 0.0)
                    );
                    Landed(rec, ctx);
                }
            }
        }

        /// <summary>
        /// The words moved as AutoCAD 2021 moves them when the scale changes, where every leader meets them on the left, their
        /// doglegs alike: along x by as much as the doglegs and the gap grew, the base point by the doglegs, and up by as much
        /// more as the left is met below their top. Met by several leaders on their left, the words move by the gap alone and up,
        /// the base point kept, and the leaders are landed anew at them (d-two-scale2, <see cref="Landed"/>): "ABC" at
        /// (160, 40, 0) met from (150, 38, 0) twice stand at (162, 42, 0) at a scale of 2, both doglegs from (142, 38, 0). Words
        /// with a leader on their right are left, AutoCAD landing them by their font's width. Words that run against the plane's
        /// x axis (<see cref="Reversed"/>) move along it by how much wider they are too, where the drawing's fonts measure them,
        /// and are left where they do not (check D38): "ABC" mirrored across the XY plane, held at (110.67, 50, 0) and met from
        /// the left, stand at (131.33, 52, 0) at a scale of 2. Words of no leader move to meet the base point again, by the gap
        /// and up, as AutoCAD 2021 moves them (check D38b): "ABC" at (100, 50, 0) stand at (102, 52, 0) at a scale of 2.
        /// </summary>
        private void Rescaled(
            Rec rec,
            Ctx ctx,
            List<KeyValuePair<LeaderPart, double>> before,
            double gapChange,
            double wasHeight
        )
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            Sides(ctx, x, out int left, out int right);
            int attachment = AttachmentOf(rec, true);
            double rise = Below(ctx, attachment, Height(ctx)) - Below(ctx, attachment, wasHeight);
            double change = double.NaN;
            bool measured = right > 0 && WordsWidth(ctx, out _);
            double widened = Widened(ctx, wasHeight);

            if (double.IsNaN(widened))
            {
                return;
            }

            // Measured words met on the right alone move to its landing (m-right-scale2).
            if (measured && left == 0)
            {
                Rightward(rec, ctx, gapChange, widened);
                return;
            }

            if (left == 0 || (right > 0 && !measured) || double.IsNaN(rise))
            {
                // Met by no leader, the words move to meet the base point again, by the gap and up, as AutoCAD 2021 moves them
                // (check D38b b-scale2-N): "ABC" at (100, 50, 0) stand at (102, 52, 0) at a scale of 2.
                if (left == 0 && right == 0 && !double.IsNaN(rise) && !Reversed(ctx))
                {
                    MoveContent(ctx, x.Multiply(gapChange).Add(y.Multiply(rise)), new GeoVector3(0.0, 0.0, 0.0));
                }

                return;
            }

            // Met by several leaders on the left, or on both sides, the words move by the gap alone and up (m2-both-scale2).
            if (left > 1 || right > 0)
            {
                GeoVector3 by = x.Multiply(gapChange).Add(y.Multiply(rise));
                MoveContent(ctx, widened == 0.0 ? by : by.Add(x.Multiply(widened)), new GeoVector3(0.0, 0.0, 0.0));
                Landed(rec, ctx);
                return;
            }

            foreach (KeyValuePair<LeaderPart, double> was in before)
            {
                if (Side(was.Key, x) < 0)
                {
                    double grew = Num(Text(was.Key.Head, 40), 1.0) - was.Value;

                    if (!double.IsNaN(change) && !Same(change, grew))
                    {
                        return;
                    }

                    change = grew;
                }
            }

            GeoVector3 moved = x.Multiply(change + gapChange).Add(y.Multiply(rise));
            MoveContent(ctx, widened == 0.0 ? moved : moved.Add(x.Multiply(widened)), x.Multiply(change));
        }

        /// <summary>
        /// The words kept landed as AutoCAD 2021 keeps them when they are given anew (<see cref="MText"/>) where leaders meet them
        /// on a side, the place given not taken: met on the left only, where they were, moved up by as much more as the left is
        /// met below their top; met on the right, where they were if they are as wide as before (the same words, height, width
        /// and spacing). Words AutoCAD would land by their font's width take the place given. Words that run against the plane's
        /// x axis (<see cref="Reversed"/>), held at their right, move along it by how much wider they are than before
        /// (<paramref name="wasWidth"/>) too, where the drawing's fonts measure them both times, and take the place given where
        /// they do not (check D38): "ABCDEF" given to "ABC" mirrored across the XY plane, held at (110.67, 50, 0) and met from the
        /// left, stand at (122.67, 50, 0).
        /// </summary>
        private void Relocated(Rec rec, Ctx ctx, GeoPoint3 wasAt, string wasLook, double wasBelow, double wasWidth)
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            double widened = 0.0;

            if (Reversed(ctx))
            {
                widened = WordsWidth(ctx, out double width) ? width - wasWidth : double.NaN;

                if (double.IsNaN(widened))
                {
                    return;
                }
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            Sides(ctx, x, out int left, out int right);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            string look = WordsLook(ctx);
            double change = Below(ctx, AttachmentOf(rec, true), Height(ctx)) - wasBelow;

            if (left > 0 && right == 0 && !double.IsNaN(change))
            {
                PutPoint(
                    ctx.Head,
                    12,
                    widened == 0.0
                        ? wasAt.Add(y.Multiply(change))
                        : wasAt.Add(y.Multiply(change)).Add(x.Multiply(widened)),
                    ContextOrder,
                    from,
                    to
                );
            }
            else if (right > 0 && string.Equals(look, wasLook, StringComparison.Ordinal))
            {
                PutPoint(ctx.Head, 12, wasAt, ContextOrder, from, to);
            }
            else if (right > 0 && WordsWidth(ctx, out _))
            {
                // Measured words of another width: met on the right alone, moved to its landing from where they were; met on
                // both sides, kept landed on the left and the leaders on the right landed anew.
                if (left == 0)
                {
                    PutPoint(ctx.Head, 12, wasAt, ContextOrder, from, to);
                    Rightward(rec, ctx, 0.0, widened);
                }
                else if (!double.IsNaN(change))
                {
                    PutPoint(
                        ctx.Head,
                        12,
                        widened == 0.0
                            ? wasAt.Add(y.Multiply(change))
                            : wasAt.Add(y.Multiply(change)).Add(x.Multiply(widened)),
                        ContextOrder,
                        from,
                        to
                    );
                    Landed(rec, ctx);
                }
            }
        }

        /// <summary>
        /// The words given first moved as AutoCAD 2021 moves those of a text style other than the multileader's own
        /// (<paramref name="wasStyle"/>, its 343 before them), where the drawing's fonts measure them in both (check D38): AutoCAD
        /// puts the base point of the content where a leader from the left would meet them in the multileader's font, then the
        /// words where they meet it in their own, so that a leader added is landed by the multileader's font and the words moved
        /// to it. "Ag" of arial.ttf 4 high given at (100, 50, 0) to a multileader of txt.shx, met at the middle of their top line,
        /// stand at (100, 49.92, 0): 2.59 below their top for 2.67 in txt.shx, a leader from the left landed at (90, 47.33, 0).
        /// Words held otherwise than at their top left along the plane, or met at their top or bottom, are left.
        /// </summary>
        private void Restyled(Rec rec, Ctx ctx, string wasStyle, DxfDatabase database)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0 || wasStyle == null || !Kept(rec, ctx, true))
            {
                return;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            string now = Text(m, 340)?.Trim();

            if (
                now == null
                || Whole(Text(m, 171), 1) != 1
                || string.Equals(now, wasStyle.Trim(), StringComparison.OrdinalIgnoreCase)
            )
            {
                return;
            }

            int attachment = AttachmentOf(rec, true);
            double height = Height(ctx);
            DxfTextLine own = Measured(ctx, height, wasStyle.Trim(), database);
            DxfTextLine words = Measured(ctx, height, now, database);

            if (own == null || words == null)
            {
                return;
            }

            double shift = Below(ctx, attachment, height, words) - Below(ctx, attachment, height, own);

            if (double.IsNaN(shift) || shift == 0.0)
            {
                return;
            }

            Axes(ctx, out _, out GeoVector3 y);
            MovePoint(ctx.Head, 12, p => p.Add(y.Multiply(shift)), ContextOrder, from, to);
        }

        /// <summary>
        /// The plane of the context data turned about its normal with words turned in it, as AutoCAD 2021 turns it where no leader
        /// is landed yet (check D38b r-*): its x axis (111) their direction (13), its y axis (112) across them. "ABC" given turned
        /// half round to a multileader of the floor make its x axis (-1, 0, 0) and its y axis (0, -1, 0); turned 30 degrees, its x
        /// axis (0.87, 0.5, 0). Words facing another way than the plane, or off it, leave it.
        /// </summary>
        private static void TurnPlane(Ctx ctx)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            GeoVector3 x = Unit(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoVector3 normal = Unit(
                DxfOcsTransform.Cross(x, Unit(PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector()))
            );
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoVector3 facing = Unit(PointOf(m, 11, new GeoPoint3(0.0, 0.0, 1.0)).ToVector());

            if (
                !(along.Length > 0.0)
                || !(normal.Length > 0.0)
                || facing.Subtract(normal).Length > 1E-9
                || Math.Abs(along.DotProduct(normal)) > 1E-9
                || along.Subtract(x).Length <= 1E-9
            )
            {
                return;
            }

            PutPoint(ctx.Head, 111, Pt(along), ContextOrder);
            PutPoint(ctx.Head, 112, Pt(Unit(DxfOcsTransform.Cross(normal, along))), ContextOrder);
        }

        /// <summary>
        /// The base point of the content (10 of the context data) of words given where no leader is landed, as AutoCAD 2021 puts
        /// it (check D38b): where a leader from the left of the plane would meet them held at their top left where they are, the
        /// <see cref="LandingGap"/> short of them and as far below their top as the attachment of the left side says, which needs
        /// no font, so that AutoCAD lands a leader it adds there and leaves the words; words held at their top centre or right,
        /// whose left edge AutoCAD puts where they are given, moved along the plane by half their width or all of it where the
        /// drawing's <see cref="DxfDatabase.FontSearchPath"/> measures them. "ABC" 4 high given at (100, 50, 0), met at the middle
        /// of the top line, put it at (98, 48, 0); held at their top right, 10.67 wide in txt.shx, they stand at (110.67, 50, 0).
        /// Words held otherwise, met at their top or bottom, running against the plane's x axis or off it put it at their place
        /// where they are given first, as before, and leave it where they are given anew.
        /// </summary>
        private static void Based(Rec rec, Ctx ctx, bool first, DxfDatabase database)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            GeoPoint3 at = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            int held = Whole(Text(m, 171), 1);
            DxfTextLine words = Measured(ctx, Height(ctx), null, database);
            double below = Below(ctx, AttachmentOf(rec, true), Height(ctx), words);

            if (
                Reversed(ctx)
                || along.Subtract(x).Length > 1E-9
                || held < 1
                || held > 3
                || double.IsNaN(below)
                || Whole(Text(rec.After, 271), 0) != 0
            )
            {
                if (first)
                {
                    PutPoint(ctx.Head, 10, at, ContextOrder);
                }

                return;
            }

            if (held != 1 && words != null)
            {
                PutPoint(
                    ctx.Head,
                    12,
                    at.Add(x.Multiply(held == 3 ? words.ActualWidth : words.ActualWidth / 2.0)),
                    ContextOrder,
                    from,
                    to
                );
            }

            PutPoint(
                ctx.Head,
                10,
                at.Add(x.Multiply(-Num(Text(ctx.Head, 145), 2.0))).Add(y.Multiply(-below)),
                ContextOrder
            );
        }

        /// <summary>
        /// Words given first to a MULTILEADER whose leaders are landed moved to meet the first leader on a side, as AutoCAD 2021
        /// moves them (check D38b b-o4-*): the <see cref="LandingGap"/> past the end of its dogleg on their left, as far below their
        /// top as the attachment of the side says; on their right, where the drawing's fonts measure them, their right edge the gap
        /// short of it. "ABC" 4 high given at (100, 50, 0) to a multileader of one line landed at (0, 0, 0), its dogleg 8 along x,
        /// stand at (10, 2, 0); the base point then where a leader from their left meets them, at (8, 0, 0). Words AutoCAD would
        /// place by a width no font gives, or that the plane cannot meet, stay where given, the base point put as
        /// <see cref="Based"/> puts it.
        /// </summary>
        private void Met(Rec rec, Ctx ctx)
        {
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            LeaderPart leader = ctx.Leaders.Find(l => HasLanding(l) && Side(l, x) != 0);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            int held = Whole(Text(m, 171), 1);

            if (
                leader != null
                && !Reversed(ctx)
                && along.Subtract(x).Length <= 1E-9
                && held >= 1
                && held <= 3
                && Whole(Text(rec.After, 271), 0) == 0
            )
            {
                int side = Side(leader, x);
                GeoVector3 dogleg = Unit(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                GeoPoint3 end = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0))
                    .Add(dogleg.Multiply(Num(Text(leader.Head, 40), 0.0)));
                double gap = Num(Text(ctx.Head, 145), 2.0);
                double below = Below(ctx, AttachmentOf(rec, side < 0), Height(ctx));
                double left = Below(ctx, AttachmentOf(rec, true), Height(ctx));
                bool measured = WordsWidth(ctx, out double width);

                if (!double.IsNaN(below) && !double.IsNaN(left) && (measured || (side < 0 && held == 1)))
                {
                    // Their left edge, then the point they are held by.
                    GeoPoint3 edge = end.Add(x.Multiply(side < 0 ? gap : -(gap + width))).Add(y.Multiply(below - left));
                    GeoPoint3 top = edge.Add(y.Multiply(left));
                    PutPoint(
                        ctx.Head,
                        12,
                        top.Add(
                            x.Multiply(
                                held == 3 ? width
                                : held == 2 ? width / 2.0
                                : 0.0
                            )
                        ),
                        ContextOrder,
                        from,
                        to
                    );
                    PutPoint(ctx.Head, 10, edge.Add(x.Multiply(-gap)), ContextOrder);
                    return;
                }
            }

            Based(rec, ctx, true, Database());
        }

        /// <summary>What the width of the words follows, as read: their letters (304), width (43), spacing (45 and 170) and height (41 of the context data).</summary>
        private static string WordsLook(Ctx ctx)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            List<KeyValuePair<int, string>> m =
                from < 0 ? new List<KeyValuePair<int, string>>() : ctx.Head.GetRange(from, to - from);
            return Text(m, 304)
                + "|"
                + Real(Num(Text(m, 43), 0.0))
                + "|"
                + Real(Num(Text(m, 45), 1.0))
                + "|"
                + Whole(Text(m, 170), 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|"
                + Real(Height(ctx));
        }

        #endregion

        #region The underline of attachment 8

        /// <summary>
        /// Whether a leader meets the words on a side whose attachment is the underline of all their lines (8), where AutoCAD 2021
        /// underlines the words (<c>\L</c>).
        /// </summary>
        private static bool AllLined(Rec rec, Ctx ctx)
        {
            Axes(ctx, out GeoVector3 x, out _);

            foreach (LeaderPart leader in ctx.Leaders)
            {
                int side = Side(leader, x);

                if (side != 0 && AttachmentOf(rec, side < 0) == 8)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The words underlined as AutoCAD 2021 underlines them for attachment 8: <c>\L</c> put before words that do not begin with
        /// it where a leader meets them on a side of that attachment (<see cref="AllLined"/>), "ABC" written <c>\LABC</c>; taken
        /// off again where no leader does once one did. Words of no letters are left.
        /// </summary>
        private void Underline(Rec rec, Ctx ctx, bool was)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (styling || from < 0)
            {
                return;
            }

            string words = Text(ctx.Head.GetRange(from, to - from), 304) ?? string.Empty;
            bool lined = words.StartsWith("\\L", StringComparison.Ordinal);

            if (AllLined(rec, ctx))
            {
                if (!lined && words.Length > 0)
                {
                    Put(ctx.Head, 304, "\\L" + words, ContextOrder, from, to);
                }
            }
            else if (was && lined)
            {
                Put(ctx.Head, 304, words.Substring(2), ContextOrder, from, to);
            }
        }

        #endregion

        #region The style's look

        /// <summary>
        /// Gives the MULTILEADER the linetype of its style's leader lines (341, the style's 340), its style's arrow block (342,
        /// the style's 341; none where the style names none, l6-s2-to-standard) and the text style of its style's words (343 and
        /// the words' 340, the style's 342, t-ts2-set-after), as AutoCAD 2021 writes them when a multileader takes a style, where
        /// the MULTILEADER does not override them (bits 2, 8 and 11 of its 90; l6-s2-own-to-standard, b-ts3-then-s4) and the
        /// drawing has the records.
        /// </summary>
        private void TakeStyleLook(Rec rec, DxfDatabase database)
        {
            string handle = Text(rec.After, 340)?.Trim();

            if (
                handle == null
                || database == null
                || !database.TryGetObject(handle, out DxfDBObject found)
                || !(found is DxfMLeaderStyle style)
            )
            {
                return;
            }

            int flags = Whole(Text(rec.After, 90), 0);
            string linetype = Named<DxfLinetypeTableRecord>(database, style.Text(340));
            string named = style.Text(341)?.Trim();
            string arrow = Named<DxfBlockTableRecord>(database, named);
            string textStyle = Named<DxfTextStyleTableRecord>(database, style.Text(342));

            if (linetype != null && (flags & (1 << 2)) == 0)
            {
                Put(rec.After, 341, linetype, TopOrder);
            }

            if ((flags & (1 << 8)) == 0)
            {
                if (arrow != null)
                {
                    Put(rec.After, 342, arrow, TopOrder);
                }
                else if (string.IsNullOrEmpty(named) || named == "0")
                {
                    // A style of AutoCAD's closed filled arrow, which needs no block.
                    Put(rec.After, 342, null, TopOrder);
                }
            }

            if (textStyle != null && (flags & (1 << 11)) == 0)
            {
                Put(rec.After, 343, textStyle, TopOrder);

                if (rec.Context != null)
                {
                    Section(rec.Context.Head, 290, MTextCodes, out int from, out int to);

                    if (from >= 0)
                    {
                        Put(rec.Context.Head, 340, textStyle, ContextOrder, from, to);
                    }
                }
            }
        }

        #endregion
    }
}
