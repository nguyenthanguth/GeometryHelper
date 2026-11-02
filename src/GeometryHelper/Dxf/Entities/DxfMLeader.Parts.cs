using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        #region Made in code and added

        /// <summary>The groups of a new MULTILEADER, as ezdxf writes one with no content and no leaders, its style's handles given when it is added.</summary>
        private static DxfTypedValue[] Fresh()
        {
            var groups = new List<KeyValuePair<int, string>>
            {
                G(100, "AcDbMLeader"), G(270, "2"), G(300, "CONTEXT_DATA{"), G(40, "1"), G(10, "0"), G(20, "0"), G(30, "0"), G(41, "4"),
                G(140, "4"), G(145, "2"), G(174, "1"), G(175, "1"), G(176, "0"), G(177, "0"), G(290, "0"), G(296, "0"), G(110, "0"),
                G(120, "0"), G(130, "0"), G(111, "1"), G(121, "0"), G(131, "0"), G(112, "0"), G(122, "1"), G(132, "0"), G(297, "0"),
                G(272, "9"), G(273, "9"), G(301, "}"), G(340, "0"), G(90, "0"), G(170, "1"), G(91, ByBlock), G(171, "-2"), G(290, "1"),
                G(291, "1"), G(41, "8"), G(42, "4"), G(172, "0"), G(343, "0"), G(173, "1"), G(95, "1"), G(174, "1"), G(175, "0"),
                G(92, ByBlock), G(292, "0"), G(93, ByBlock), G(10, "1"), G(20, "1"), G(30, "1"), G(43, "0"), G(176, "0"), G(293, "0"),
                G(294, "0"), G(178, "0"), G(179, "1"), G(45, "1"), G(271, "0"), G(272, "9"), G(273, "9"), G(295, "0"),
            };

            return groups.ConvertAll(g => new DxfTypedValue(g.Key, g.Value)).ToArray();
        }

        /// <summary>
        /// Gives a MULTILEADER made in code the handles of the drawing it is added to: its multileader style (340), the drawing's
        /// CMLEADERSTYLE or Standard, and the text style (343) of that style's words, else Standard, where it names none of the
        /// drawing's; its words (340 of the context's MTEXT) are given the same text style, as AutoCAD 2021 writes them, or the
        /// text style they named when given, where the drawing has it (with bit 11 where it is not the style's).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the drawing has no multileader style of the name, or no text style Standard.</exception>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            DxfDatabase database = owner?.File?.Database;

            if (database == null)
            {
                return;
            }

            Rec rec = Read();

            if (Text(rec.After, 340)?.Trim() == "0")
            {
                string wanted = database.MLeaderstyle;
                string handle = null;

                foreach (DxfDBDictionaryEntry entry in Styles(database))
                {
                    if (handle == null && entry.Value?.Handle != null && (string.Equals(entry.Key, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(entry.Key, "Standard", StringComparison.OrdinalIgnoreCase)))
                    {
                        handle = entry.Value.Handle;
                    }
                }

                if (handle == null)
                {
                    throw new ArgumentException("The drawing has no multileader style " + wanted + " in ACAD_MLEADERSTYLE for the MULTILEADER to take.", "entity");
                }

                Put(rec.After, 340, handle, TopOrder);

                // The style's linetype of the leader lines and its arrow block, as AutoCAD 2021 writes them on a new multileader.
                TakeStyleLook(rec, database);
            }

            if (Text(rec.After, 343)?.Trim() == "0")
            {
                if (!database.TextStyleTable.Has("Standard"))
                {
                    throw new ArgumentException("The drawing has no text style Standard for the MULTILEADER to take.", "entity");
                }

                Put(rec.After, 343, database.TextStyleTable["Standard"].Handle, TopOrder);
            }

            // The words of a MULTILEADER given them before it was added, of no text style yet, take its own.
            if (rec.Context != null)
            {
                Section(rec.Context.Head, 290, MTextCodes, out int from, out int to);

                if (from >= 0 && Text(rec.Context.Head.GetRange(from, to - from), 340)?.Trim() == "0")
                {
                    Put(rec.Context.Head, 340, Text(rec.After, 343), ContextOrder, from, to);
                }

                // Words that named a text style of the drawing keep it, with the bit of the overrides where it is not the
                // style's, as AutoCAD 2021 keeps those of a multileader made in memory (f6-def-words-ts2, t-ts2-words-ts2).
                if (from >= 0 && wordsTextStyle != null && database.TextStyleTable.Has(wordsTextStyle))
                {
                    string textStyle = database.TextStyleTable[wordsTextStyle].Handle;
                    Put(rec.Context.Head, 340, textStyle, ContextOrder, from, to);

                    if (!string.Equals(Text(rec.After, 343)?.Trim(), textStyle, StringComparison.OrdinalIgnoreCase))
                    {
                        Put(rec.After, 343, textStyle, TopOrder);
                        SetBit(rec, 11);
                    }
                }
            }

            wordsTextStyle = null;
            Write(rec);
        }

        #endregion

        #region Moving

        /// <summary>
        /// Moves, turns, scales or mirrors the MULTILEADER, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: the base point,
        /// the plane, the leaders' points, doglegs and breaks, the lines' vertices, the MTEXT's place, direction and normal and the
        /// block's place, normal, scale and turn as it is drawn, its matrix written as its transform where it then stands, as
        /// AutoCAD 2021 writes it; the sizes the words, arrows, landing and doglegs are drawn with scaled with it (41, 140 and 145
        /// of the context data, 40 of each leader, and the widths of the words), and the scale of the context data (40), the
        /// MULTILEADER's own scale (45), length of the doglegs (41), size of the arrows (42), block scale (10) and block turn
        /// (43), and the arrow sizes leader lines have of their own, left as they are, as AutoCAD 2021's <c>TransformBy</c> leaves
        /// them: a multileader of scale 1, its doglegs 8, scaled by 2 is still of scale 1, its doglegs drawn 16 and 41 still 8,
        /// its words 60 wide then 120 wide, a line's own arrows of 2 still drawn 2, and its block drawn twice the size with
        /// <see cref="BlockScale"/> still 1. A block mirrored is drawn where the mirror puts it, its normal mirrored with it, its x
        /// scale turned negative and its turn read from its y axis, and the plane of a multileader of a block takes the mirrored
        /// normal, an x axis on the side the old one keeps and its y axis across them (297 of nought), as AutoCAD 2021 writes
        /// them: the box of lines from (-10, -5) to (20, 15) at (100, 50, 0), turned 30 degrees and mirrored across the XY plane,
        /// is of normal (0, 0, -1), scales (-1, 1, 1) and turn 330 degrees, its plane's y axis (0, -1, 0); mirrored across the YZ
        /// plane, of normal (0, 0, 1), its plane's x axis (1, 0, 0). Words mirrored are carried as ezdxf carries them.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentException">Thrown when it scales the MULTILEADER more one way than another, which ezdxf and AutoCAD refuse, or shears it, keeping its axes as long as each other but not square, which AutoCAD refuses too.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the MULTILEADER is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            Rec rec = Read();
            Ctx ctx = Context(rec);
            GeoVector3 x = transform.Transform(new GeoVector3(1.0, 0.0, 0.0));
            GeoVector3 y = transform.Transform(new GeoVector3(0.0, 1.0, 0.0));
            GeoVector3 z = transform.Transform(new GeoVector3(0.0, 0.0, 1.0));
            GeoVector3 planeX = PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
            GeoVector3 planeY = PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector();
            bool flat = Math.Abs(planeX.Z) <= 1E-12 && Math.Abs(planeY.Z) <= 1E-12;

            // Past ezdxf, which looks at the lengths of the axes only: a shear keeps them as long as each other but no longer square.
            double square = 1E-9 * x.DotProduct(x);

            if (!Close(x.DotProduct(x), y.DotProduct(y)) || (!flat && !Close(x.DotProduct(x), z.DotProduct(z))) || Math.Abs(x.DotProduct(y)) > square
                || (!flat && (Math.Abs(x.DotProduct(z)) > square || Math.Abs(y.DotProduct(z)) > square)))
            {
                throw new ArgumentException("A MULTILEADER is scaled the same along every direction, and not sheared, as AutoCAD scales one.", nameof(transform));
            }

            double scale = x.Length;

            // The context data: its base point, sizes and plane; its scale (40) left as it is, as AutoCAD 2021 leaves it.
            MovePoint(ctx.Head, 10, transform.Transform, ContextOrder, 0, -1);

            foreach (int code in new[] { 41, 140, 145 })
            {
                ScaleNumber(ctx.Head, code, scale, ContextOrder);
            }

            MovePoint(ctx.Head, 110, transform.Transform, ContextOrder, 0, -1);
            GeoVector3 newX = Unit(transform.Transform(planeX));
            GeoVector3 newY = Unit(transform.Transform(planeY));
            bool mirrored = DxfOcsTransform.Cross(x, y).DotProduct(z) < 0.0;
            bool flipped = false;

            // A multileader of a block mirrored: the plane's normal mirrored with it, its x axis the side of the mirrored one
            // the old keeps, and its y axis across them, as AutoCAD 2021 writes it (e5-k-mirrorxy, g-k-mirroryz).
            if (mirrored && HasSection(ctx, 296, BlockCodes))
            {
                GeoVector3 mirroredZ = Unit(transform.Transform(DxfOcsTransform.Cross(planeX, planeY)));
                flipped = newX.DotProduct(planeX) < 0.0;
                newX = flipped ? newX.Negate() : newX;
                newY = Unit(DxfOcsTransform.Cross(mirroredZ, newX));
            }

            PutPoint(ctx.Head, 111, Pt(newX), ContextOrder);
            PutPoint(ctx.Head, 112, Pt(newY), ContextOrder);
            GeoVector3 planeZ = Unit(DxfOcsTransform.Cross(newX, newY));
            GeoVector3 worldZ = Unit(z);
            bool reversed = worldZ.Subtract(planeZ.Multiply(-1.0)).Length <= 1E-9;

            if (Text(ctx.Head, 297) != null || reversed)
            {
                Put(ctx.Head, 297, reversed ? "1" : "0", ContextOrder);
            }

            // The MTEXT: its normal as an extrusion, its place, direction and turn; its widths scaled.
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                GeoVector3 normal = PointOf(ctx.Head.GetRange(from, to - from), 11, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
                var ocs = new DxfOcsTransform(normal.Length > 0.0 ? normal : new GeoVector3(0.0, 0.0, 1.0), transform);
                PutPoint(ctx.Head, 11, Pt(ocs.NewExtrusion), ContextOrder, from, to);
                Section(ctx.Head, 290, MTextCodes, out from, out to);
                MovePoint(ctx.Head, 12, transform.Transform, ContextOrder, from, to);
                GeoVector3 direction = PointOf(ctx.Head.GetRange(from, to - from), 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                PutPoint(ctx.Head, 13, Pt(Unit(transform.Transform(direction))), ContextOrder, from, to);
                Section(ctx.Head, 290, MTextCodes, out from, out to);
                int turn = ctx.Head.FindIndex(from, to - from, g => g.Key == 42);

                if (turn >= 0)
                {
                    double radians = Num(ctx.Head[turn].Value, 0.0);
                    GeoVector3 d = ocs.TransformDirection(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
                    PutNumber(ctx.Head, 42, Math.Atan2(d.Y, d.X), 0.0, ContextOrder, from, to);
                }

                if (Math.Abs(scale - 1.0) > 1E-9 * Math.Max(1.0, scale) && scale > 1E-12)
                {
                    for (int i = from; i < to; i++)
                    {
                        int code = ctx.Head[i].Key;

                        if (code == 43 || code == 44 || code == 142 || code == 143 || code == 144)
                        {
                            ctx.Head[i] = G(code, Num(ctx.Head[i].Value, 0.0) * scale);
                        }
                    }
                }
            }

            // The block: its normal as an extrusion, its place, scale, turn and matrix.
            Section(ctx.Head, 296, BlockCodes, out from, out to);
            GeoPoint3? blockScale = null;
            double? blockTurn = null;

            if (from >= 0)
            {
                List<KeyValuePair<int, string>> b = ctx.Head.GetRange(from, to - from);
                GeoVector3 normal = PointOf(b, 14, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
                normal = normal.Length > 0.0 ? normal : new GeoVector3(0.0, 0.0, 1.0);
                GeoPoint3 old = PointOf(b, 16, new GeoPoint3(1.0, 1.0, 1.0));
                var axes = new DxfOcsTransform.Axes(normal);
                GeoVector3 ux = transform.Transform(axes.X);
                GeoVector3 uy = transform.Transform(axes.Y);
                GeoVector3 uz = transform.Transform(axes.Z);
                double radians = Num(Text(b, 46), 0.0);
                DxfOcsTransform ocs;

                if (mirrored)
                {
                    // Mirrored: the normal mirrored with the block, its x scale turned negative and its turn read from its y axis,
                    // as AutoCAD 2021 writes it, from nought to a whole turn: the box turned 30 degrees and mirrored across the XY
                    // plane is drawn of normal (0, 0, -1), scales (-1, 1, 1) and turn 330 (e5-k-rot30-mirrorxy, g-k-rot30-mirroryz).
                    ocs = new DxfOcsTransform(normal, Unit(uz), transform);
                    blockScale = new GeoPoint3(-ux.Length * old.X, uy.Length * old.Y, uz.Length * old.Z);
                    GeoVector3 up = ocs.TransformDirection(new GeoVector3(-Math.Sin(radians), Math.Cos(radians), 0.0));
                    double turn = Math.Atan2(up.Y, up.X) - Math.PI / 2.0;
                    blockTurn = turn < 0.0 ? turn + 2.0 * Math.PI : turn;
                }
                else
                {
                    ocs = new DxfOcsTransform(normal, transform);
                    double sy = uy.Length * old.Y;

                    // The y axis against the new extrusion, which a half turn about an axis in the plane turns over, as AutoCAD
                    // 2021 turns it: a block turned half round about the y axis is drawn mirrored, its scales kept (k-transform-mirror).
                    GeoVector3 expected = Unit(DxfOcsTransform.Cross(ocs.NewExtrusion, ux));

                    if (expected.Subtract(Unit(uy)).Length > 1E-12)
                    {
                        sy = -sy;
                    }

                    blockScale = new GeoPoint3(ux.Length * old.X, sy, uz.Length * old.Z);
                    GeoVector3 d = ocs.TransformDirection(new GeoVector3(Math.Cos(radians), Math.Sin(radians), 0.0));
                    blockTurn = Math.Atan2(d.Y, d.X);
                }
                PutPoint(ctx.Head, 14, Pt(ocs.NewExtrusion), ContextOrder, from, to);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
                MovePoint(ctx.Head, 15, transform.Transform, ContextOrder, from, to);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
                PutPoint(ctx.Head, 16, blockScale.Value, ContextOrder, from, to);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
                PutNumber(ctx.Head, 46, blockTurn.Value, 0.0, ContextOrder, from, to);
                Section(ctx.Head, 296, BlockCodes, out from, out to);
                DxfBlockReference placed = PlacedBlock(rec, ctx, out _);

                // The matrix the block's transform where it now stands, as AutoCAD 2021 writes it (s2-k-transform2); carried by
                // the transformation where the drawing has no such block.
                if (placed != null)
                {
                    SetMatrix(ctx.Head, from, to, placed.BlockTransform);
                }
                else
                {
                    Matrix(ctx.Head, from, to, transform);
                }

                // The plane's x axis turned back: the base point where a leader from the left now meets the block, as AutoCAD 2021
                // puts it, the box mirrored across the YZ plane met at (-120, 55) (g-k-mirroryz).
                if (flipped && BlockMeets(rec, ctx, -double.MaxValue, out _, out GeoPoint3 left))
                {
                    PutPoint(ctx.Head, 10, left, ContextOrder);
                }
            }

            // The leaders: their points, doglegs and breaks; their lines' vertices and breaks.
            foreach (LeaderPart leader in ctx.Leaders)
            {
                MovePoint(leader.Head, 10, transform.Transform, LeaderOrder, 0, -1);
                GeoVector3 dogleg = PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
                double length = Num(Text(leader.Head, 40), 1.0);
                GeoVector3 turned = transform.Transform(dogleg.Length > 0.0 ? dogleg.Multiply(length / dogleg.Length) : new GeoVector3(length, 0.0, 0.0));
                double newLength = turned.Length;
                PutPoint(leader.Head, 11, Pt(newLength > 0.0 ? turned.Multiply(1.0 / newLength) : dogleg), LeaderOrder);

                if (Text(leader.Head, 40) != null)
                {
                    PutNumber(leader.Head, 40, newLength, 0.0, LeaderOrder);
                }

                MoveAll(leader.Head, new[] { 12, 13 }, transform);

                foreach (LinePart line in leader.Lines)
                {
                    MoveAll(line.Groups, new[] { 10, 11, 12 }, transform);
                }
            }

            // The MULTILEADER's own block scale (10) and turn (43), sizes (41, 42) and scale (45) left as they are, as AutoCAD 2021
            // leaves them (s2-k-transform2, k-transform-rot90).
            Write(rec);
        }

        private static GeoPoint3 Pt(GeoVector3 v) => new GeoPoint3(v.X, v.Y, v.Z);

        private static bool Close(double a, double b) => Math.Abs(a - b) <= 1E-9 * Math.Max(Math.Abs(a), Math.Abs(b));

        private static GeoVector3 Unit(GeoVector3 v)
        {
            double length = v.Length;
            return length > 0.0 ? v.Multiply(1.0 / length) : v;
        }

        /// <summary>Scales a number of a run where it has one.</summary>
        private static void ScaleNumber(List<KeyValuePair<int, string>> groups, int code, double scale, int[] order)
        {
            if (Text(groups, code) != null)
            {
                PutNumber(groups, code, Num(Text(groups, code), 0.0) * scale, 0.0, order);
            }
        }

        /// <summary>Moves the matrix of the block of the context data (its 16 groups 47, as ezdxf keeps it) along a vector: its translation.</summary>
        private static void Shift(List<KeyValuePair<int, string>> head, double x, double y, double z)
        {
            Section(head, 296, BlockCodes, out int from, out int to);
            var at = new List<int>();

            for (int i = Math.Max(from, 0); from >= 0 && i < to; i++)
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

            double[] by = { x, y, z };

            for (int r = 0; r < 3; r++)
            {
                if (by[r] != 0.0)
                {
                    head[at[4 * r + 3]] = G(47, Num(head[at[4 * r + 3]].Value, 0.0) + by[r]);
                }
            }
        }

        /// <summary>Moves the first point of a code in a part of a run where it has one.</summary>
        private static void MovePoint(List<KeyValuePair<int, string>> groups, int code, Func<GeoPoint3, GeoPoint3> move, int[] order, int from, int to)
        {
            to = to < 0 ? groups.Count : to;
            int at = groups.FindIndex(from, to - from, g => g.Key == code);

            if (at >= 0)
            {
                PutPoint(groups, code, move(PointAt(groups, at)), order, from, to);
            }
        }

        /// <summary>Moves every point of some codes in a run, each with the groups 10 and 20 above it.</summary>
        private static void MoveAll(List<KeyValuePair<int, string>> groups, int[] codes, GeoTransform3 transform)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (Array.IndexOf(codes, groups[i].Key) < 0)
                {
                    continue;
                }

                int code = groups[i].Key;
                GeoPoint3 p = transform.Transform(PointAt(groups, i));
                groups[i] = G(code, p.X);

                for (int k = 1; k <= 2 && i + 1 < groups.Count && groups[i + 1].Key == code + 10 * k; k++)
                {
                    i++;
                    groups[i] = G(code + 10 * k, k == 1 ? p.Y : p.Z);
                }
            }
        }

        /// <summary>The matrix of the block (16 groups 47), as ezdxf keeps it, carried by a transformation: the transformation times it.</summary>
        private static void Matrix(List<KeyValuePair<int, string>> head, int from, int to, GeoTransform3 transform)
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

            var m = new double[16];

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    double sum = 0.0;

                    for (int k = 0; k < 4; k++)
                    {
                        sum += transform[r, k] * Num(head[at[4 * k + c]].Value, 0.0);
                    }

                    m[4 * r + c] = sum;
                }
            }

            for (int i = 0; i < 16; i++)
            {
                if (!Same(m[i], Num(head[at[i]].Value, double.NaN)))
                {
                    head[at[i]] = G(47, m[i]);
                }
            }
        }

        #endregion

        #region The record

        /// <summary>The own groups of the MULTILEADER, read.</summary>
        private Rec Read() => Rec.Read(Run(GetOwnGroups(0)));

        /// <summary>Writes the own groups of the MULTILEADER in one writing; nothing where they are as they were.</summary>
        private void Write(Rec rec)
        {
            List<KeyValuePair<int, string>> run = rec.Groups();
            ReplaceGroups(0, Codes(GetOwnGroups(0), run), run.ToArray());
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Array.AsReadOnly(new[] { 100 });

        /// <summary>The context data of the record.</summary>
        /// <exception cref="InvalidOperationException">Thrown where the record has none.</exception>
        private static Ctx Context(Rec rec) => rec.Context ?? throw new InvalidOperationException("The MULTILEADER has no context data (CONTEXT_DATA{), which AutoCAD draws it from.");

        private static bool HasSection(Ctx ctx, int flag, HashSet<int> codes)
        {
            Section(ctx.Head, flag, codes, out int from, out _);
            return from >= 0;
        }

        /// <summary>Takes a section of the context data out, its flag nought.</summary>
        private static void DropSection(Ctx ctx, int flag, HashSet<int> codes)
        {
            Section(ctx.Head, flag, codes, out int from, out int to);

            if (from >= 0)
            {
                ctx.Head.RemoveRange(from, to - from);
                ctx.Head[from - 1] = G(flag, "0");
            }
        }

        /// <summary>Gives the context data an MTEXT as ezdxf writes one: at the base point, its style the MULTILEADER's, held at the top left.</summary>
        private static void MakeMText(Rec rec, Ctx ctx)
        {
            int flag = ctx.Head.FindIndex(g => g.Key == 290);

            if (flag < 0)
            {
                Put(ctx.Head, 290, "1", ContextOrder);
                flag = ctx.Head.FindIndex(g => g.Key == 290);
            }

            ctx.Head[flag] = G(290, "1");
            GeoPoint3 at = PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
            var groups = new List<KeyValuePair<int, string>> { G(304, string.Empty), G(11, "0"), G(21, "0"), G(31, "1"), G(340, Text(rec.After, 343) ?? "0") };
            groups.AddRange(PointGroups(12, at));
            groups.AddRange(new[]
            {
                G(13, "1"), G(23, "0"), G(33, "0"), G(42, "0"), G(43, "0"), G(44, "0"), G(45, "1"), G(170, "1"), G(90, ByBlock), G(171, "1"),
                G(172, "1"), G(91, "-939524096"), G(141, "1.5"), G(92, "0"), G(291, "0"), G(292, "0"), G(173, "0"), G(293, "0"), G(142, "0"),
                G(143, "0"), G(294, "0"), G(295, "1"),
            });
            ctx.Head.InsertRange(flag + 1, groups);
        }

        /// <summary>
        /// Gives the context data a block as ezdxf writes one: at the base point, in the plane's normal, of the MULTILEADER's
        /// scale of the block times its scale (40 of the context data), as AutoCAD 2021 draws it, its turn and colour, its matrix
        /// the identity.
        /// </summary>
        private static void MakeBlock(Rec rec, Ctx ctx)
        {
            int flag = ctx.Head.FindIndex(g => g.Key == 296);

            if (flag < 0)
            {
                Put(ctx.Head, 296, "1", ContextOrder);
                flag = ctx.Head.FindIndex(g => g.Key == 296);
            }

            ctx.Head[flag] = G(296, "1");
            GeoVector3 x = PointOf(ctx.Head, 111, new GeoPoint3(1.0, 0.0, 0.0)).ToVector();
            GeoVector3 y = PointOf(ctx.Head, 112, new GeoPoint3(0.0, 1.0, 0.0)).ToVector();
            GeoVector3 normal = Unit(DxfOcsTransform.Cross(x, y));
            var groups = new List<KeyValuePair<int, string>> { G(341, "0") };
            groups.AddRange(PointGroups(14, normal.Length > 0.0 ? Pt(normal) : new GeoPoint3(0.0, 0.0, 1.0)));
            groups.AddRange(PointGroups(15, PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0))));
            GeoPoint3 own = PointOf(rec.After, 10, new GeoPoint3(1.0, 1.0, 1.0));
            double scale = ScaleOf(ctx);
            groups.AddRange(PointGroups(16, new GeoPoint3(own.X * scale, own.Y * scale, own.Z * scale)));
            groups.Add(G(46, Text(rec.After, 43) ?? "0"));
            groups.Add(G(93, Text(rec.After, 93) ?? ByBlock));

            for (int i = 0; i < 16; i++)
            {
                groups.Add(G(47, i % 5 == 0 ? "1" : "0"));
            }

            ctx.Head.InsertRange(flag + 1, groups);
        }

        /// <summary>Sets a group of the MULTILEADER's own, and its bit of the overrides where one is given (-1 for none).</summary>
        private static void SetTop(Rec rec, int code, string value, int bit)
        {
            if (Put(rec.After, code, value, TopOrder) && bit >= 0)
            {
                SetBit(rec, bit);
            }
        }

        /// <summary>Sets a bit of the overrides of the style (90).</summary>
        private static void SetBit(Rec rec, int bit)
        {
            int flags = Whole(Text(rec.After, 90), 0);
            int now = flags | (1 << bit);

            if (now != flags || Text(rec.After, 90) == null)
            {
                Put(rec.After, 90, now.ToString(CultureInfo.InvariantCulture), TopOrder);
            }
        }

        /// <summary>
        /// Sets a bit of the overrides of the style (90) alone, as AutoCAD 2021 sets it for a property given the value it has
        /// (-1 for none).
        /// </summary>
        private void Mark(int bit)
        {
            if (bit >= 0)
            {
                Rec rec = Read();
                SetBit(rec, bit);
                Write(rec);
            }
        }

        private void SetTopFlag(int code, bool value, bool now, int bit)
        {
            if (value == now)
            {
                Mark(bit);
                return;
            }

            Rec rec = Read();
            SetTop(rec, code, value ? "1" : "0", bit);
            Write(rec);
        }

        private void SetTopEnum(int code, int value, int now, int least, int most, int bit)
        {
            if (value == now)
            {
                Mark(bit);
                return;
            }

            if (value < least || value > most)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The value is not one AutoCAD has for it, " + least.ToString(CultureInfo.InvariantCulture) + " to " + most.ToString(CultureInfo.InvariantCulture) + ".");
            }

            Rec rec = Read();
            SetTop(rec, code, value.ToString(CultureInfo.InvariantCulture), bit);
            Write(rec);
        }

        /// <summary>
        /// Sets a size of the context data, given unscaled and written times the scale (40 of the context data), and its bit of
        /// the overrides, the bit alone where it is the size read (<see cref="Unscaled"/>); then lets the content follow, given
        /// the size it was drawn.
        /// </summary>
        private void SetContextNumber(int code, double value, double fallback, int bit, bool positive, Action<Rec, Ctx, double> follow)
        {
            Guard.Finite(value, nameof(value), "The number has to be a finite number.");
            Rec rec = Read();
            Ctx ctx = Context(rec);
            double was = Num(Text(ctx.Head, code), fallback);

            if (Same(value, Unscaled(ctx, code, fallback)))
            {
                SetBit(rec, bit);
                Write(rec);
                return;
            }

            if (positive)
            {
                Guard.Positive(value, nameof(value), "The number has to be a finite number above nought.");
            }
            else
            {
                Guard.NonNegative(value, nameof(value), "The number has to be a finite number, nought or more.");
            }

            PutNumber(ctx.Head, code, value * ScaleOf(ctx), fallback, ContextOrder);
            SetBit(rec, bit);
            follow(rec, ctx, was);
            Write(rec);
        }

        /// <summary>The scale of the context data (40), 1 where it gives none or one of no size, which sizes are drawn times.</summary>
        private static double ScaleOf(Ctx ctx)
        {
            double scale = Num(Text(ctx.Head, 40), 1.0);
            return Math.Abs(scale) > 1E-9 ? scale : 1.0;
        }

        /// <summary>
        /// A size of the context data as AutoCAD 2021's API reads it, unscaled: as drawn (the code's group) over the scale
        /// (<see cref="ScaleOf"/>); the fallback where the context data gives none.
        /// </summary>
        private static double Unscaled(Ctx ctx, int code, double fallback)
        {
            string text = Text(ctx.Head, code);
            double scale = ScaleOf(ctx);
            return text == null ? fallback : Num(text, fallback * scale) / scale;
        }

        /// <summary>
        /// Sets a colour of the MULTILEADER's own, and the one of a section of its context data where it has it; the colour it
        /// has sets the bit of the overrides alone where asked (<paramref name="marked"/>), as AutoCAD 2021 sets it.
        /// </summary>
        private void SetColour(int code, int section, int flag, HashSet<int> codes, DxfColor value, int bit, bool marked)
        {
            string text = DxfMLeaderColors.Text(value, nameof(value));
            Rec rec = Read();

            if (Whole(Text(rec.After, code) ?? ByBlock, 0) == Whole(text, 1))
            {
                if (marked)
                {
                    Mark(bit);
                }

                return;
            }

            SetTop(rec, code, text, bit);

            if (codes != null && rec.Context != null)
            {
                Section(rec.Context.Head, flag, codes, out int from, out int to);

                if (from >= 0)
                {
                    Put(rec.Context.Head, section, text, ContextOrder, from, to);
                }
            }

            Write(rec);
        }

        /// <summary>A point of a section of the context data; the base point of the content (10) where it has no such section.</summary>
        private GeoPoint3 ContentPoint(int flag, HashSet<int> codes, int code)
        {
            Ctx ctx = Context(Read());
            GeoPoint3 basePoint = PointOf(ctx.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
            Section(ctx.Head, flag, codes, out int from, out int to);
            return from < 0 ? basePoint : PointOf(ctx.Head.GetRange(from, to - from), code, basePoint);
        }

        /// <summary>The groups of the context's MTEXT.</summary>
        /// <exception cref="InvalidOperationException">Thrown where the MULTILEADER holds no words.</exception>
        private static List<KeyValuePair<int, string>> MTextGroups(Rec rec, out int from, out int to)
        {
            Ctx ctx = Context(rec);
            Section(ctx.Head, 290, MTextCodes, out from, out to);
            return from < 0 ? throw NoWords() : ctx.Head.GetRange(from, to - from);
        }

        /// <summary>The groups of the context's block.</summary>
        /// <exception cref="InvalidOperationException">Thrown where the MULTILEADER holds no block.</exception>
        private static List<KeyValuePair<int, string>> BlockGroups(Rec rec, out int from, out int to)
        {
            Ctx ctx = Context(rec);
            Section(ctx.Head, 296, BlockCodes, out from, out to);
            return from < 0 ? throw new InvalidOperationException("The MULTILEADER holds no block: give it one by BlockContent.") : ctx.Head.GetRange(from, to - from);
        }

        /// <summary>A group of the context's block; null where it holds none.</summary>
        private static string BlockText(Rec rec, int code)
        {
            if (rec.Context == null)
            {
                return null;
            }

            Section(rec.Context.Head, 296, BlockCodes, out int from, out int to);
            return from < 0 ? null : Text(rec.Context.Head.GetRange(from, to - from), code);
        }

        private static InvalidOperationException NoWords() => new InvalidOperationException("The MULTILEADER holds no words: give it some by MText.");

        private static double Positive(double value, double fallback) => value > 0.0 ? value : fallback;

        private static void Finite(GeoPoint3 point, string parameter)
        {
            if (!point.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + point + " is not.", parameter);
            }
        }

        private static void Attachment(DxfTextAttachmentType value)
        {
            if (value < DxfTextAttachmentType.AttachmentTopOfTop || value > DxfTextAttachmentType.AttachmentLinedCenter)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "An attachment is one of AutoCAD's, 0 to 10.");
            }
        }

        /// <summary>The MULTILEADER's group of a side, its context data's, AutoCAD's own value and the bit of the overrides.</summary>
        /// <exception cref="ArgumentException">Thrown when the side is not known.</exception>
        private static void AttachmentCodes(DxfLeaderDirectionType direction, out int code, out int context, out int fallback, out int bit)
        {
            switch (direction)
            {
                case DxfLeaderDirectionType.LeftLeader: code = 173; context = 174; fallback = 1; bit = 12; return;
                case DxfLeaderDirectionType.RightLeader: code = 95; context = 175; fallback = 1; bit = 25; return;
                case DxfLeaderDirectionType.TopLeader: code = 273; context = 273; fallback = 9; bit = 28; return;
                case DxfLeaderDirectionType.BottomLeader: code = 272; context = 272; fallback = 9; bit = 29; return;
                default: throw new ArgumentException("A side is the left, the right, the top or the bottom: " + direction + " is none of them.", "leaderDirection");
            }
        }

        /// <summary>Where the entry of an attribute's definition begins among the MULTILEADER's own groups (its 330); -1 where it has none.</summary>
        private static int AttributeAt(List<KeyValuePair<int, string>> after, string handle)
        {
            if (handle == null)
            {
                return -1;
            }

            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].Key == 330 && string.Equals(after[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private DxfDatabase Database() => IsResident ? Item.File.Database : null;

        /// <summary>The block of the handle of its BLOCK_RECORD; null where the drawing has none, or the handle is nought.</summary>
        private DxfBlockTableRecord BlockByHandle(string handle) => BlockByHandle(Database(), handle);

        /// <summary>The block of the handle of its BLOCK_RECORD in a drawing; null where it has none, or the handle is nought.</summary>
        internal static DxfBlockTableRecord BlockByHandle(DxfDatabase database, string handle)
        {
            if (database == null || handle.Length == 0 || handle == "0")
            {
                return null;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (string.Equals(block.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return block;
                }
            }

            return null;
        }

        private IEnumerable<DxfDBDictionaryEntry> Styles() => Styles(Database());

        /// <summary>The entries of the drawing's ACAD_MLEADERSTYLE; none where it has none.</summary>
        private static IEnumerable<DxfDBDictionaryEntry> Styles(DxfDatabase database)
        {
            DxfFile file = database?.File;
            DxfItem rootItem = file != null && file.HoldsObjects ? file.ObjectItem(file.RootDictionaryHandle()) : null;

            if (rootItem != null && DxfDBObject.OfObject(rootItem) is DxfDBDictionary root && root.Contains("ACAD_MLEADERSTYLE") && root.GetAt("ACAD_MLEADERSTYLE") is DxfDBDictionary styles)
            {
                return styles;
            }

            return new DxfDBDictionaryEntry[0];
        }

        /// <summary>The leader of an index (its 90).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown where there is none.</exception>
        private static LeaderPart FindLeader(Rec rec, int index)
        {
            foreach (LeaderPart leader in rec.Context?.Leaders ?? new List<LeaderPart>())
            {
                if (leader.Index == index)
                {
                    return leader;
                }
            }

            throw new ArgumentOutOfRangeException("leaderIndex", index, "The MULTILEADER has no leader of the index.");
        }

        /// <summary>The leader line of an index (its 91), and its leader.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown where there is none.</exception>
        private static LinePart FindLine(Rec rec, int index, out LeaderPart leader)
        {
            foreach (LeaderPart each in rec.Context?.Leaders ?? new List<LeaderPart>())
            {
                foreach (LinePart line in each.Lines)
                {
                    if (line.Index == index)
                    {
                        leader = each;
                        return line;
                    }
                }
            }

            throw new ArgumentOutOfRangeException("leaderLineIndex", index, "The MULTILEADER has no leader line of the index.");
        }

        /// <summary>The index of a new leader line: one above every line's.</summary>
        private static int NextLine(Rec rec)
        {
            int index = 0;

            foreach (LeaderPart leader in Context(rec).Leaders)
            {
                foreach (LinePart line in leader.Lines)
                {
                    index = Math.Max(index, line.Index + 1);
                }
            }

            return index;
        }

        /// <summary>
        /// The vertices of a leader line: its own, then the end of its leader's landing (<see cref="LastOf"/>); its own alone where
        /// the leader has no landing yet (<see cref="HasLanding"/>), as AutoCAD 2021 counts them (a1-addleader-line).
        /// </summary>
        private static List<GeoPoint3> Vertices(Rec rec, int index, out LinePart line, out LeaderPart leader)
        {
            line = FindLine(rec, index, out leader);
            List<GeoPoint3> vertices = line.Vertices();

            if (HasLanding(leader))
            {
                vertices.Add(LastOf(rec, leader));
            }

            return vertices;
        }

        /// <summary>
        /// Whether a leader has its landing (290 of the leader, its last leader line point): AutoCAD 2021 writes a leader just
        /// added by <c>AddLeader</c> with none, which the first vertex given one of its lines then becomes, and draws no dogleg
        /// for it.
        /// </summary>
        private static bool HasLanding(LeaderPart leader) => Whole(Text(leader.Head, 290), 1) != 0;

        /// <summary>
        /// The last vertex of the lines of a leader, as AutoCAD 2021's <c>GetLastVertex</c> gives it: the point the dogleg starts
        /// at (10 of the leader), or, where the leader meets the words on a side and its dogleg is not drawn, the end the dogleg
        /// would have, which its lines run on to (<see cref="Path"/>).
        /// </summary>
        private static GeoPoint3 LastOf(Rec rec, LeaderPart leader)
        {
            if (Doglegged(rec.After))
            {
                return PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
            }

            Landing(leader, out GeoPoint3 last, out bool horizontal, out GeoVector3 along);
            return horizontal ? last.Add(along) : last;
        }

        private static void VertexIndex(List<GeoPoint3> vertices, int index, string parameter)
        {
            if (index < 0 || index >= vertices.Count)
            {
                throw new ArgumentOutOfRangeException(parameter, index, "The leader line has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + " vertices, from nought.");
            }
        }

        /// <summary>
        /// Writes the vertices of a leader line: all but the last its own, the last the end of its leader's landing, as
        /// <see cref="LastOf"/> reads it, the point the dogleg starts at written so; left as it is where the last is. A leader of
        /// no landing takes the last as its landing, as AutoCAD 2021 takes the first vertex given a line of it (a1-addfirst),
        /// the content left where it is.
        /// </summary>
        private void WriteVertices(Rec rec, LinePart line, LeaderPart leader, List<GeoPoint3> vertices, bool carry = false)
        {
            GeoPoint3 last = vertices[vertices.Count - 1];

            if (!HasLanding(leader))
            {
                Landing(leader, out _, out bool flat, out GeoVector3 dogleg);
                PutPoint(leader.Head, 10, flat && !Doglegged(rec.After) ? last.Add(dogleg.Negate()) : last, LeaderOrder);
                Put(leader.Head, 290, "1", LeaderOrder);
            }
            else if (Text(leader.Head, 10) == null || !SamePoint(last, LastOf(rec, leader)))
            {
                GeoPoint3 was = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
                bool had = Text(leader.Head, 10) != null;
                Landing(leader, out _, out bool horizontal, out GeoVector3 along);
                PutPoint(leader.Head, 10, horizontal && !Doglegged(rec.After) ? last.Add(along.Negate()) : last, LeaderOrder);

                // The last vertex set: the content and the other landings carried with the landing, as AutoCAD 2021 carries them.
                if (carry && had)
                {
                    Relanded(rec, Context(rec), leader, was.GetVectorTo(PointOf(leader.Head, 10, was)));
                }
            }

            line.SetVertices(vertices.GetRange(0, vertices.Count - 1));
            Write(rec);
        }

        private void RemoveVertex(int leaderLineIndex, bool first)
        {
            Rec rec = Read();
            List<GeoPoint3> vertices = Vertices(rec, leaderLineIndex, out LinePart line, out LeaderPart leader);

            if (vertices.Count <= 2)
            {
                throw new InvalidOperationException("A leader line keeps two vertices at least, and this one has " + vertices.Count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            vertices.RemoveAt(first ? 0 : vertices.Count - 1);
            WriteVertices(rec, line, leader, vertices);
        }

        /// <summary>Takes out the arrow of a leader line (its 94 and the 345 after it).</summary>
        private static void DropArrow(Rec rec, int lineIndex)
        {
            for (int i = 0; i + 1 < rec.After.Count; i++)
            {
                if (rec.After[i].Key == 94 && rec.After[i + 1].Key == 345 && Whole(rec.After[i].Value, -1) == lineIndex)
                {
                    rec.After.RemoveRange(i, 2);
                    return;
                }
            }
        }

        #endregion
    }
}
