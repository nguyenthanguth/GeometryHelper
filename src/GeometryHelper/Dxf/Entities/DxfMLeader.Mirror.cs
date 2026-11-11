using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        #region Mirrored by MIRROR

        /// <summary>
        /// Puts right a MULTILEADER that <see cref="DxfEntity.TransformBy"/> has mirrored, as AutoCAD 2021's MIRROR command
        /// leaves one, which is not as its <c>TransformBy</c> leaves one (probed in its console, mirror-mleader GHMM1 and GHMM3).
        /// The plane stays the drawing's: its x axis the mirrored one turned back where the mirror turns it back (a mirror across
        /// an upright), else its y axis the mirrored one turned back, 297 nought. Words kept readable (MIRRTEXT 0) keep their
        /// normal and their direction in the plane, and stand where they stood from the mirrored landing of the first leader,
        /// across it where the mirror turns the plane's x axis back; words mirrored (MIRRTEXT 1) are mirrored with the rest.
        /// Where the mirror turns the plane's x axis back, the words are held by their other side (171 of 1 made 3), the
        /// content lined up the other way (176), with bit 14 of the overrides. The base point of words is then the left of their
        /// box, along the plane's x axis, short of it by the landing gap; that of a block where a leader from the left meets it,
        /// and the block's matrix its transform where it now stands. Words that run against the plane's x axis say so (294 of
        /// 1). "ABC" of arial.ttf 0.18 high at (30.45, 20.09) held at its top left, landed at (30, 20) by a dogleg of 0.36,
        /// mirrored across x = 100 stands at (169.55, 20.09) held at its top right, its base point at (168.95, 20); mirrored
        /// across y = 100, at (30.45, 180.09).
        /// </summary>
        /// <param name="source">The MULTILEADER as it was before the mirror, which is left as it is.</param>
        /// <param name="mirror">The mirror, across a plane upright to the MULTILEADER's.</param>
        /// <param name="mirrorWords">Whether the words are mirrored with the rest (MIRRTEXT 1), not kept readable.</param>
        /// <param name="width">The width of the ink of the words' widest line, from where it starts, as AutoCAD's ActualWidth.</param>
        /// <param name="height">The height of the words, as AutoCAD's ActualHeight.</param>
        internal void FinishMirror(
            DxfMLeader source,
            GeoTransform3 mirror,
            bool mirrorWords,
            double width,
            double height
        )
        {
            Ctx was = Context(source.Read());
            GeoVector3 x = Unit(PointOf(was.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            GeoVector3 y = Unit(PointOf(was.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector());
            GeoVector3 mx = Unit(mirror.Transform(x));
            GeoVector3 my = Unit(mirror.Transform(y));
            // A mirror line at 45 degrees to the plane's x axis turns it square to itself: not back, as AutoCAD 2021 reads it
            // (mirror-insert GHMI1, across y = x), though the dot product comes out a hair below nought.
            bool across = mx.DotProduct(x) < -1E-9;
            GeoVector3 nx = across ? mx.Negate() : mx;
            GeoVector3 ny = across ? my : my.Negate();

            Rec rec = Read();
            Ctx ctx = Context(rec);
            PutPoint(ctx.Head, 111, Pt(nx), ContextOrder);
            PutPoint(ctx.Head, 112, Pt(ny), ContextOrder);

            if (Text(ctx.Head, 297) != null)
            {
                Put(ctx.Head, 297, "0", ContextOrder);
            }

            Section(was.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                List<KeyValuePair<int, string>> m = was.Head.GetRange(from, to - from);
                GeoPoint3 baseAt = PointOf(was.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
                GeoPoint3 landing = was.Leaders.Count > 0 ? PointOf(was.Leaders[0].Head, 10, baseAt) : baseAt;
                Words(
                    rec,
                    ctx,
                    m,
                    mirror,
                    mirrorWords,
                    across,
                    x,
                    y,
                    nx,
                    ny,
                    landing,
                    mirror.Transform(baseAt),
                    width,
                    height
                );

                int aligned = Whole(Text(was.Head, 176), 0);

                if (across && (aligned == 0 || aligned == 2))
                {
                    Put(ctx.Head, 176, (2 - aligned).ToString(CultureInfo.InvariantCulture), ContextOrder);
                }
            }

            if (across)
            {
                SetBit(rec, 14);
            }

            // A block: its matrix where it now stands and, where the plane's x axis is turned back, the base point where a leader
            // from the left meets it, as TransformBy puts them for a MULTILEADER in a drawing (a copy is mirrored before it is
            // added to one).
            Section(ctx.Head, 296, BlockCodes, out from, out to);

            if (from >= 0)
            {
                DxfBlockReference placed = PlacedBlock(rec, ctx, out _);

                if (placed != null)
                {
                    SetMatrix(ctx.Head, from, to, placed.BlockTransform);
                }

                if (across && BlockMeets(rec, ctx, -double.MaxValue, out _, out GeoPoint3 left))
                {
                    PutPoint(ctx.Head, 10, left, ContextOrder);
                }
            }

            Write(rec);
        }

        /// <summary>
        /// The words of a MULTILEADER mirrored by MIRROR (<see cref="FinishMirror"/>): their normal, place, direction, turn and
        /// attachment, whether they run against the plane's x axis (294), and the base point at the left of their box.
        /// </summary>
        private static void Words(
            Rec rec,
            Ctx ctx,
            List<KeyValuePair<int, string>> m,
            GeoTransform3 mirror,
            bool mirrorWords,
            bool across,
            GeoVector3 x,
            GeoVector3 y,
            GeoVector3 nx,
            GeoVector3 ny,
            GeoPoint3 landing,
            GeoPoint3 mirroredBase,
            double width,
            double height
        )
        {
            GeoPoint3 at = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));
            GeoVector3 normal = PointOf(m, 11, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
            normal = normal.Length > 0.0 ? Unit(normal) : new GeoVector3(0.0, 0.0, 1.0);
            GeoVector3 direction = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());
            int attached = Math.Max(1, Math.Min(9, Whole(Text(m, 171), 1)));
            int column = (attached - 1) % 3;
            int row = (attached - 1) / 3;
            int now = across ? 2 - column : column;
            GeoVector3 along;
            GeoPoint3 place;
            double turn;

            if (mirrorWords)
            {
                // Mirrored with the rest, held by the same side of their box, which the other column names where the plane's x axis
                // is turned back.
                var ocs = new DxfOcsTransform(normal, mirror);
                along = Unit(mirror.Transform(direction));
                normal = ocs.NewExtrusion;
                double radians = Num(Text(m, 42), 0.0);
                GeoVector3 d = ocs.TransformDirection(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
                turn = Math.Atan2(d.Y, d.X);
                place = mirror.Transform(at).Add(along.Multiply(width * (now - column) / 2.0));
            }
            else
            {
                // Readable: as they ran in the plane, and as far from the mirrored landing as from the landing, across it where the
                // plane's x axis is turned back.
                along = Unit(nx.Multiply(direction.DotProduct(x)).Add(ny.Multiply(direction.DotProduct(y))));
                GeoVector3 off = landing.GetVectorTo(at);
                double ox = off.DotProduct(x);
                double oy = off.DotProduct(y);
                place = mirror
                    .Transform(landing)
                    .Add(nx.Multiply(across ? -ox : ox))
                    .Add(ny.Multiply(oy))
                    .Add(normal.Multiply(off.DotProduct(normal)));
                GeoVector3 local = new DxfOcsTransform.Axes(normal).FromWorld(along);
                turn = Math.Atan2(local.Y, local.X);
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);
            PutPoint(ctx.Head, 11, Pt(normal), ContextOrder, from, to);
            Section(ctx.Head, 290, MTextCodes, out from, out to);
            PutPoint(ctx.Head, 12, place, ContextOrder, from, to);
            Section(ctx.Head, 290, MTextCodes, out from, out to);
            PutPoint(ctx.Head, 13, Pt(along), ContextOrder, from, to);
            Section(ctx.Head, 290, MTextCodes, out from, out to);

            if (ctx.Head.FindIndex(from, to - from, g => g.Key == 42) >= 0)
            {
                PutNumber(ctx.Head, 42, turn < 0.0 ? turn + (2.0 * Math.PI) : turn, 0.0, ContextOrder, from, to);
                Section(ctx.Head, 290, MTextCodes, out from, out to);
            }

            Put(ctx.Head, 171, (row * 3 + now + 1).ToString(CultureInfo.InvariantCulture), ContextOrder, from, to);
            Put(rec.After, 294, along.DotProduct(nx) < 0.0 ? "1" : "0", TopOrder);

            // The base point: the left of the box of the words along the plane's x axis, short of it by the landing gap, level
            // with the mirrored base point.
            GeoVector3 up = Unit(DxfOcsTransform.Cross(normal, along));
            double left = double.MaxValue;

            foreach (double a in new[] { -now * width / 2.0, (2 - now) * width / 2.0 })
            {
                foreach (double b in new[] { -(2 - row) * height / 2.0, row * height / 2.0 })
                {
                    GeoPoint3 corner = place.Add(along.Multiply(a)).Add(up.Multiply(b));
                    left = Math.Min(left, corner.ToVector().DotProduct(nx));
                }
            }

            double gap = Num(Text(ctx.Head, 145), 2.0);
            double shift = left - gap - mirroredBase.ToVector().DotProduct(nx);
            PutPoint(ctx.Head, 10, mirroredBase.Add(nx.Multiply(shift)), ContextOrder);
        }

        #endregion
    }
}
