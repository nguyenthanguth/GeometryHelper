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
                insert.Normal = normal;
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

                own = own == null
                    ? new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }
                    : new[] { Math.Min(own[0], box.Min.X), Math.Min(own[1], box.Min.Y), Math.Min(own[2], box.Min.Z), Math.Max(own[3], box.Max.X), Math.Max(own[4], box.Max.Y), Math.Max(own[5], box.Max.Z) };
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
                GeoVector3 c = placed.Transform(new GeoPoint3(own[(corner & 1) * 3], own[1 + ((corner >> 1) & 1) * 3], own[2 + ((corner >> 2) & 1) * 3])).ToVector();
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
            GeoVector3 across = position.Subtract(x.Multiply(position.DotProduct(x))).Subtract(y.Multiply(position.DotProduct(y)));
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
                PutPoint(ctx.Head, 16, new GeoPoint3(drawn.X * factor, drawn.Y * factor, drawn.Z * factor), ContextOrder, from, to);
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
                GeoPoint3 target = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0)).Add(dogleg.Multiply(Num(Text(leader.Head, 40), 0.0)));

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
        private void Landed(Rec rec, Ctx ctx)
        {
            Axes(ctx, out GeoVector3 x, out _);
            bool block = HasSection(ctx, 296, BlockCodes);
            var wordsMeet = new GeoPoint3(0.0, 0.0, 0.0);
            bool words = !block && WordsMeet(rec, ctx, out wordsMeet);

            foreach (LeaderPart leader in ctx.Leaders)
            {
                int side = Side(leader, x);
                GeoPoint3 end = wordsMeet;

                if (side == 0 || (block ? !BlockMeets(rec, ctx, side * double.MaxValue, out _, out end) : !words || side > 0))
                {
                    continue;
                }

                GeoVector3 dogleg = Unit(PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                PutPoint(leader.Head, 10, end.Add(dogleg.Multiply(-Num(Text(leader.Head, 40), 0.0))), LeaderOrder);
            }
        }

        /// <summary>
        /// Where a leader meets the words on their left, as AutoCAD 2021 lands one: the <see cref="LandingGap"/> short of their left
        /// edge, as far below their top as the attachment of the left side says (<see cref="Below"/>); false where the words are
        /// not held at their top left (171 of 1) along the plane's x axis, met on their sides (271 of nought), or there are none.
        /// Words "ABC" 4 high at (160, 40, 0), met at the middle of their top line with a gap of 2, are met at (158, 38, 0).
        /// </summary>
        private static bool WordsMeet(Rec rec, Ctx ctx, out GeoPoint3 meet)
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

            if (Whole(Text(m, 171), 1) != 1 || along.Subtract(x).Length > 1E-9 || double.IsNaN(below))
            {
                return false;
            }

            meet = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0)).Add(x.Multiply(-Num(Text(ctx.Head, 145), 2.0))).Add(y.Multiply(-below));
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

        /// <summary>The unit x and y axes of the plane of the context data (111 and 112).</summary>
        private static void Axes(Ctx ctx, out GeoVector3 x, out GeoVector3 y)
        {
            x = Unit(PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            y = Unit(PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector());
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
            return along >= 1.0 - 1E-9 ? -1 : along <= -1.0 + 1E-9 ? 1 : 0;
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
        /// running along the plane's x axis (13), or a block; and the MULTILEADER is not being set from its style.
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
                GeoVector3 along = Unit(PointOf(ctx.Head.GetRange(from, to - from), 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
                return along.Subtract(x).Length <= 1E-9;
            }

            return !words && HasSection(ctx, 296, BlockCodes);
        }

        /// <summary>
        /// How far below the top of the words a leader meets them for an attachment of a side (173 of the left, 95 of the right),
        /// as AutoCAD 2021 lands it: nought at the top of the top line, half the height of the words at its middle, all of it at
        /// its bottom and a fifth more under it; the middle of the words, the middle of the bottom line, its bottom and a fifth
        /// more under it as the lines stand, each 5/3 of their height times their spacing below the one above, the lines counted
        /// by their breaks (<c>\P</c>); NaN for another attachment, or where the MULTILEADER holds no words.
        /// </summary>
        private static double Below(Ctx ctx, int attachment, double height)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0)
            {
                return double.NaN;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            double whole = height + (Breaks(Text(m, 304)) * 5.0 / 3.0 * height * Num(Text(m, 45), 1.0));

            switch (attachment)
            {
                case 0: return 0.0;
                case 1: return height / 2.0;
                case 2: return whole / 2.0;
                case 3: return whole - (height / 2.0);
                case 4: return whole;
                case 5: return whole + (height / 5.0);
                case 6: return height + (height / 5.0);
                case 7: return height;
                case 8: return height + (height / 5.0);
                default: return double.NaN;
            }
        }

        /// <summary>The height of the words (41 of the context data).</summary>
        private static double Height(Ctx ctx) => Num(Text(ctx.Head, 41), 4.0);

        /// <summary>The attachment of a side of the words: the left (173) or the right (95).</summary>
        private static int AttachmentOf(Rec rec, bool left) => Whole(Text(rec.After, left ? 173 : 95), 1);

        /// <summary>
        /// Moves the content, the words (12) or the block (15, and its matrix), along a vector, and the base point of the content
        /// (10 of the context data) along another, as AutoCAD 2021 moves them to keep a landing.
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
                Shift(ctx.Head, by.X, by.Y, by.Z);
            }

            MovePoint(ctx.Head, 10, p => p.Add(basePointBy), ContextOrder, 0, -1);
        }

        /// <summary>Moves the point a leader's dogleg starts at (its 10) along a vector.</summary>
        private static void MoveLanding(LeaderPart leader, GeoVector3 by) => MovePoint(leader.Head, 10, p => p.Add(by), LeaderOrder, 0, -1);

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
        /// none on the left, else the leaders on the right are landed anew. A side with no leader moves nothing.
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

            if (double.IsNaN(change) || (left ? lefts : rights) == 0)
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
        /// them wide.
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

            if (left > 0 && right == 0 && !double.IsNaN(change))
            {
                MoveContent(ctx, y.Multiply(change), new GeoVector3(0.0, 0.0, 0.0));
                Landed(rec, ctx);
            }
        }

        /// <summary>
        /// The words moved as AutoCAD 2021 moves them when the scale changes, where every leader meets them on the left, their
        /// doglegs alike: along x by as much as the doglegs and the gap grew, the base point by the doglegs, and up by as much
        /// more as the left is met below their top. Met by several leaders on their left, the words move by the gap alone and up,
        /// the base point kept, and the leaders are landed anew at them (d-two-scale2, <see cref="Landed"/>): "ABC" at
        /// (160, 40, 0) met from (150, 38, 0) twice stand at (162, 42, 0) at a scale of 2, both doglegs from (142, 38, 0). Words
        /// with a leader on their right are left, AutoCAD landing them by their font's width.
        /// </summary>
        private void Rescaled(Rec rec, Ctx ctx, List<KeyValuePair<LeaderPart, double>> before, double gapChange, double wasHeight)
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

            if (left == 0 || right > 0 || double.IsNaN(rise))
            {
                return;
            }

            if (left > 1)
            {
                MoveContent(ctx, x.Multiply(gapChange).Add(y.Multiply(rise)), new GeoVector3(0.0, 0.0, 0.0));
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

            MoveContent(ctx, x.Multiply(change + gapChange).Add(y.Multiply(rise)), x.Multiply(change));
        }

        /// <summary>
        /// The words kept landed as AutoCAD 2021 keeps them when they are given anew (<see cref="MText"/>) where leaders meet them
        /// on a side, the place given not taken: met on the left only, where they were, moved up by as much more as the left is
        /// met below their top; met on the right, where they were if they are as wide as before (the same words, height, width
        /// and spacing). Words AutoCAD would land by their font's width take the place given.
        /// </summary>
        private void Relocated(Rec rec, Ctx ctx, GeoPoint3 wasAt, string wasLook, double wasBelow)
        {
            if (!Kept(rec, ctx, true))
            {
                return;
            }

            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            Sides(ctx, x, out int left, out int right);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            string look = WordsLook(ctx);
            double change = Below(ctx, AttachmentOf(rec, true), Height(ctx)) - wasBelow;

            if (left > 0 && right == 0 && !double.IsNaN(change))
            {
                PutPoint(ctx.Head, 12, wasAt.Add(y.Multiply(change)), ContextOrder, from, to);
            }
            else if (right > 0 && string.Equals(look, wasLook, StringComparison.Ordinal))
            {
                PutPoint(ctx.Head, 12, wasAt, ContextOrder, from, to);
            }
        }

        /// <summary>What the width of the words follows, as read: their letters (304), width (43), spacing (45 and 170) and height (41 of the context data).</summary>
        private static string WordsLook(Ctx ctx)
        {
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            List<KeyValuePair<int, string>> m = from < 0 ? new List<KeyValuePair<int, string>>() : ctx.Head.GetRange(from, to - from);
            return Text(m, 304) + "|" + Real(Num(Text(m, 43), 0.0)) + "|" + Real(Num(Text(m, 45), 1.0)) + "|" + Whole(Text(m, 170), 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + Real(Height(ctx));
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

            if (handle == null || database == null || !database.TryGetObject(handle, out DxfDBObject found) || !(found is DxfMLeaderStyle style))
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
