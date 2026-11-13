using System.Collections.Generic;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        /// <summary>
        /// Gives the words a leader as AutoCAD 2021's MLEADER writes one (probed in its console, check GHAQ): a leader whose
        /// dogleg starts at <paramref name="landing"/> and runs along <paramref name="dogleg"/> as long as
        /// <see cref="DoglegLength"/> times the <see cref="Scale"/>, its one line starting at <paramref name="arrow"/>; the base
        /// of the content (10 of the context data) at the dogleg's end and the first point of the first line (110) at the arrow.
        /// The words are not moved: they stand where the caller put them, past the dogleg and the gap. A landing at (150, 150, 0)
        /// of a dogleg of 0.36 along +x is joined by a base at (150.36, 150, 0).
        /// </summary>
        /// <returns>The index of the line.</returns>
        internal int AddMadeLeader(GeoPoint3 arrow, GeoPoint3 landing, GeoVector3 dogleg)
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            AddLeaderAt(rec, ctx, landing, dogleg);
            LeaderPart leader = ctx.Leaders[ctx.Leaders.Count - 1];
            int index = NextLine(rec);
            var line = LinePart.Made(new[] { G(91, index) });
            line.SetVertices(new List<GeoPoint3> { arrow });
            leader.Lines.Add(line);

            double length = Num(Text(rec.After, 41), 8.0) * Num(Text(ctx.Head, 40), 1.0);
            GeoVector3 along = Unit(dogleg);
            PutPoint(
                ctx.Head,
                10,
                new GeoPoint3(landing.X + along.X * length, landing.Y + along.Y * length, landing.Z + along.Z * length),
                ContextOrder
            );
            PutPoint(ctx.Head, 110, arrow, ContextOrder);
            Write(rec);
            return index;
        }

        /// <summary>
        /// Writes the matrix of the block of the content (its groups 47) as the block stands now, its scale and its place, as
        /// AutoCAD 2021's MLEADER leaves it (a block of scale 50 at (468, 100, 0) has the translation 468 and 100 in its matrix)
        /// where <see cref="BlockPosition"/> leaves the matrix as it was. Nothing is written where the MULTILEADER holds no block.
        /// </summary>
        internal void WriteBlockMatrix()
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            Section(ctx.Head, 296, BlockCodes, out int from, out int to);

            if (from < 0)
            {
                return;
            }

            DxfBlockReference placed = PlacedBlock(rec, ctx, out _);

            if (placed != null)
            {
                SetMatrix(ctx.Head, from, to, placed.BlockTransform);
                Write(rec);
            }
        }
    }
}
