using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        // The arrows ezdxf draws from their insertion point, with no length to take off the line: by name, the "_" left off.
        private static readonly HashSet<string> NoLength = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ARCHTICK", "OBLIQUE", "DOTSMALL", "SMALL", "INTEGRAL", "NONE" };

        /// <summary>
        /// Gets the entities the multileader breaks into, as AutoCAD's <c>Explode</c>, as ezdxf's <c>virtual_entities</c> gives
        /// them from its context data: its content first, an MTEXT of its words (<see cref="MText"/>, as wide as the context's
        /// scale makes it, in its text style and colour) or an INSERT of its block (<see cref="BlockContent"/> at
        /// <see cref="BlockPosition"/>, scaled, turned and coloured so, its attributes given the values the multileader holds);
        /// then, leader by leader and line by line, unless its leaders are invisible, the arrowhead at the first vertex and the
        /// LINEs through the vertices, the last point of the leader and, for a leader on the left or right of the words, the
        /// end of its dogleg, the dogleg itself where the multileader has one; a splined leader is one SPLINE fitted through the
        /// same points, leaving along its first segment and reaching along its dogleg. The arrowhead is drawn where the first
        /// segment is more than twice as long as the arrow, as AutoCAD draws it: an INSERT of the line's or the multileader's
        /// arrow block, turned to point away from the line and scaled to the arrow's size, or AutoCAD's closed filled arrow as a
        /// SOLID a third as wide as long; the line then starts at the back of the arrow. Each part is on the multileader's layer;
        /// the lines and arrows have the colour of their line where it is not BYBLOCK, else that of the leader lines, and the
        /// leader lines' linetype and lineweight. The words left on the left of two leaders 8 long, with a dogleg of 8, give an
        /// MTEXT, two arrows and five LINEs. A multileader scaled to nothing gives nothing. The lines AutoCAD draws under or over
        /// the words, as long as the words are wide, are AutoCAD's to work out, and are not among them. A copy in no file, made
        /// by <see cref="DxfEntity.Clone"/>, finds the blocks, the linetype and the text style its record names in the drawing it
        /// was copied from, as a dimension's copy finds its block, and gives what the original gives, the values of its attributes
        /// among it; where that drawing has erased a block it names since, it is refused, rather than drawn without it.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the multileader has no context data, which AutoCAD draws it from;
        /// or is a copy in no file whose content or arrows are a block the drawing it was copied from has erased since, the
        /// handle of the block said.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            var parts = new List<DxfEntity>();
            double scale = Num(Text(ctx.Head, 40), 1.0);

            if (!(Math.Abs(scale) > 1E-9))
            {
                return parts.AsReadOnly();
            }

            string layer = Layer;

            // A copy in no file reads what its record names in the drawing it was copied from.
            DxfDatabase database = IsResident ? Database() : Item.File.IsDetached ? Item.File.NamingDrawing?.Database : null;
            Content(rec, ctx, scale, layer, database, parts);
            DxfLeaderType type = LeaderLineType;

            if (type == DxfLeaderType.InVisibleLeader)
            {
                return parts.AsReadOnly();
            }

            string linetypeHandle = Text(rec.After, 341)?.Trim();
            string linetype = linetypeHandle != null && database != null && database.TryGetObject(linetypeHandle, out DxfDBObject found) && found is DxfLinetypeTableRecord record ? record.Name : null;
            DxfLineWeight weight = LeaderLineWeight;
            string leaderColour = Text(rec.After, 91) ?? ByBlock;
            bool dogleg = EnableDogleg && type != DxfLeaderType.SplineLeader;
            double size = Num(Text(ctx.Head, 140), ArrowSize);
            GeoVector3 normal = Normal;
            double tolerance = Tolerance.Default.EqualPoint;
            var arrows = Arrows(rec.After);
            string arrowHandle = Text(rec.After, 342);
            DxfBlockTableRecord arrow = arrowHandle == null ? null : Block(database, arrowHandle, "its arrows");

            void Give(DxfEntity part, string colour)
            {
                part.Layer = layer;
                Colour(part, colour != null && colour.Trim() != ByBlock ? colour : leaderColour);

                if (linetype != null)
                {
                    part.Linetype = linetype;
                }

                part.LineWeight = weight;
                parts.Add(part);
            }

            foreach (LeaderPart leader in ctx.Leaders)
            {
                Landing(leader, out GeoPoint3 last, out bool horizontal, out GeoVector3 along);

                foreach (LinePart line in leader.Lines)
                {
                    List<GeoPoint3> vertices = Path(line, last, horizontal, dogleg, along);

                    if (vertices.Count < 2)
                    {
                        continue;
                    }

                    string colour = Text(line.Groups, 92);
                    GeoVector3 first = vertices[0].GetVectorTo(vertices[1]);
                    double run = first.Length;
                    GeoVector3 direction = run > 0.0 ? first.Multiply(1.0 / run) : new GeoVector3(1.0, 0.0, 0.0);

                    if (run * size > 0.0 && run > 2.0 * size)
                    {
                        DxfBlockTableRecord block = arrows.TryGetValue(Whole(Text(line.Groups, 91), 0), out string handle) ? Block(database, handle, "the arrow of a leader line") : arrow;
                        Give(Arrowhead(vertices[0], direction, size, normal, block), colour);
                        string name = block?.Name.TrimStart('_');
                        vertices[0] = vertices[0].Add(direction.Multiply(name != null && NoLength.Contains(name) ? 0.0 : size));
                    }

                    if (type == DxfLeaderType.SplineLeader)
                    {
                        GeoVector3 end = horizontal ? along : vertices[vertices.Count - 2].GetVectorTo(vertices[vertices.Count - 1]);
                        DxfSpline spline = DxfLeader.FittedSpline(vertices, tolerance, direction, end.Length > 0.0 ? end : (GeoVector3?)null);

                        if (spline != null)
                        {
                            Give(spline, colour);
                        }

                        continue;
                    }

                    for (int i = 0; i + 1 < vertices.Count; i++)
                    {
                        if (vertices[i].DistanceTo(vertices[i + 1]) > tolerance)
                        {
                            Give(new DxfLine(vertices[i], vertices[i + 1]), colour);
                        }
                    }
                }
            }

            return parts.AsReadOnly();
        }

        /// <summary>
        /// Where the lines of a leader end, as <see cref="Explode"/> draws them: the last point of the leader (10), whether its
        /// dogleg runs left or right of the content (271 of 0), and the dogleg, along the leader's direction (11, where 291 gives
        /// one), else x, its length (40) long.
        /// </summary>
        private static void Landing(LeaderPart leader, out GeoPoint3 last, out bool horizontal, out GeoVector3 along)
        {
            last = PointOf(leader.Head, 10, new GeoPoint3(0.0, 0.0, 0.0));
            horizontal = Whole(Text(leader.Head, 271) ?? Text(leader.Tail, 271), 0) == 0;
            double length = Num(Text(leader.Head, 40), 1.0);
            along = Whole(Text(leader.Head, 291), 0) != 0 ? PointOf(leader.Head, 11, new GeoPoint3(1.0, 0.0, 0.0)).ToVector() : new GeoVector3(1.0, 0.0, 0.0);
            along = along.Length > 0.0 ? DxfOcsTransform.Normalized(along).Multiply(length) : new GeoVector3(length, 0.0, 0.0);
        }

        /// <summary>
        /// The points a leader line runs through, as <see cref="Explode"/> draws it before it steps back for its arrow: its
        /// vertices, the last point of its leader where the dogleg runs left or right and is drawn, and the end of the dogleg
        /// there, else the last point.
        /// </summary>
        private static List<GeoPoint3> Path(LinePart line, GeoPoint3 last, bool horizontal, bool dogleg, GeoVector3 along)
        {
            List<GeoPoint3> vertices = line.Vertices();

            if (horizontal && dogleg)
            {
                vertices.Add(last);
            }

            vertices.Add(horizontal ? last.Add(along) : last);
            return vertices;
        }

        /// <summary>
        /// The lines of the leaders of a MULTILEADER's record, for <see cref="DxfReader"/>, which reads a MULTILEADER as them: for
        /// each leader line, along the path <see cref="Explode"/> draws (<see cref="Path"/>) from its first vertex, where its arrow
        /// points, the points it runs through or, splined, the curve fitted through them with the tangents <see cref="Explode"/>
        /// fits it with, leaving along its first segment and reaching along the dogleg or its last segment; from the arrow's
        /// point, where <see cref="Explode"/> starts at the back of the arrow, so that the two part by up to the arrow's size near
        /// it. None for invisible leaders, a multileader scaled to nothing, or one with no context data.
        /// </summary>
        /// <param name="groups">The groups of the record as the file holds them, its type left out.</param>
        /// <param name="tolerance">The point tolerance a vertex on the one before it is left out within, in the fitting.</param>
        internal static List<LinePath> LinesOf(IReadOnlyList<KeyValuePair<int, string>> groups, double tolerance)
        {
            var lines = new List<LinePath>();
            Rec rec = Rec.Read(OwnRun(groups));
            Ctx ctx = rec.Context;
            var type = (DxfLeaderType)Whole(Text(rec.After, 170), 1);

            if (ctx == null || !(Math.Abs(Num(Text(ctx.Head, 40), 1.0)) > 1E-9) || type == DxfLeaderType.InVisibleLeader)
            {
                return lines;
            }

            bool dogleg = Whole(Text(rec.After, 291), 1) != 0 && type != DxfLeaderType.SplineLeader;

            foreach (LeaderPart leader in ctx.Leaders)
            {
                Landing(leader, out GeoPoint3 last, out bool horizontal, out GeoVector3 along);

                foreach (LinePart line in leader.Lines)
                {
                    List<GeoPoint3> vertices = Path(line, last, horizontal, dogleg, along);

                    if (vertices.Count < 2)
                    {
                        continue;
                    }

                    if (type != DxfLeaderType.SplineLeader)
                    {
                        lines.Add(new LinePath(vertices, null));
                        continue;
                    }

                    GeoVector3 first = vertices[0].GetVectorTo(vertices[1]);
                    double run = first.Length;
                    GeoVector3 direction = run > 0.0 ? first.Multiply(1.0 / run) : new GeoVector3(1.0, 0.0, 0.0);
                    GeoVector3 end = horizontal ? along : vertices[vertices.Count - 2].GetVectorTo(vertices[vertices.Count - 1]);
                    lines.Add(new LinePath(vertices, DxfLeader.FittedCurve(vertices, tolerance, direction, end.Length > 0.0 ? end : (GeoVector3?)null)));
                }
            }

            return lines;
        }

        /// <summary>
        /// The own groups of a record, as <see cref="Read"/> reads them: from its first subclass marker past AcDbEntity, that
        /// marker left out, or from its first group where it has no such marker, as a record written without them, to its
        /// extended data, the groups of the applications and the comments left out.
        /// </summary>
        private static List<KeyValuePair<int, string>> OwnRun(IReadOnlyList<KeyValuePair<int, string>> groups)
        {
            int data = groups.Count;
            int body = -1;
            bool braced = false;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Key == 1001)
                {
                    data = i;
                    break;
                }

                if (groups[i].Key == 102)
                {
                    braced = groups[i].Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (body < 0 && !braced && groups[i].Key == 100 && groups[i].Value.Trim() != "AcDbEntity")
                {
                    body = i;
                }
            }

            var run = new List<KeyValuePair<int, string>>(data);
            braced = false;

            for (int i = body + 1; i < data; i++)
            {
                if (groups[i].Key == 102)
                {
                    braced = groups[i].Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && groups[i].Key != 999)
                {
                    run.Add(groups[i]);
                }
            }

            return run;
        }

        /// <summary>A leader line as <see cref="LinesOf"/> gives it: the points it runs through, and the curve fitted through them where it is splined.</summary>
        internal sealed class LinePath
        {
            internal LinePath(List<GeoPoint3> points, NurbsCurve curve)
            {
                Points = points;
                Curve = curve;
            }

            /// <summary>The points of the line, its first where the arrow points.</summary>
            internal List<GeoPoint3> Points { get; }

            /// <summary>The curve of a splined line; null for a straight one, or a splined one whose points are all one.</summary>
            internal NurbsCurve Curve { get; }
        }

        /// <summary>
        /// The content of the multileader as <see cref="Explode"/> gives it: its MTEXT or its INSERT, on its layer; none where it
        /// has neither. Its text style and block are read in the drawing given, the one a copy in no file was copied from.
        /// </summary>
        private void Content(Rec rec, Ctx ctx, double scale, string layer, DxfDatabase database, List<DxfEntity> parts)
        {
            if (HasSection(ctx, 290, MTextCodes))
            {
                DxfMText mtext = MText;
                Section(ctx.Head, 290, MTextCodes, out int from, out int to);
                List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
                mtext.Width *= scale;
                mtext.Layer = layer;
                Colour(mtext, Text(m, 90) ?? ByBlock);
                string style = Text(m, 340)?.Trim();

                if (style != null && database != null && database.TryGetObject(style, out DxfDBObject found) && found is DxfTextStyleTableRecord record)
                {
                    mtext.TextStyleName = record.Name;
                }

                parts.Add(mtext);
                return;
            }

            string handle = HasSection(ctx, 296, BlockCodes) ? BlockText(rec, 341) ?? Text(rec.After, 344) : null;
            DxfBlockTableRecord block = handle == null ? null : Block(database, handle, "its content");

            if (block == null)
            {
                return;
            }

            List<KeyValuePair<int, string>> b = BlockGroups(rec, out _, out _);
            GeoVector3 normal = PointOf(b, 14, new GeoPoint3(0.0, 0.0, 1.0)).ToVector();
            var insert = new DxfBlockReference(BlockPosition, block) { ScaleFactors = BlockScale, Rotation = BlockRotation };

            if (normal.Length > 0.0)
            {
                insert.Normal = normal;
            }

            insert.Layer = layer;
            Colour(insert, Text(b, 93) ?? ByBlock);

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfAttributeDefinition definition && definition.Handle != null && AttributeAt(rec.After, definition.Handle) >= 0)
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                    attribute.TextString = Text(rec.After, 302, AttributeAt(rec.After, definition.Handle)) ?? string.Empty;
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            parts.Add(insert);
        }

        /// <summary>
        /// The block of a handle the record names, in the drawing given: null where it has none, or the handle is nought, as for a
        /// multileader of a drawing.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown, for a copy in no file, where the drawing it was copied from has
        /// erased the block of the handle since: what it would be drawn with is gone.</exception>
        private DxfBlockTableRecord Block(DxfDatabase database, string handle, string what)
        {
            string trimmed = handle.Trim();
            DxfBlockTableRecord block = BlockByHandle(database, trimmed);

            if (block == null && !IsResident && Item.File.IsDetached && Item.File.NamingDrawing is DxfFile origin && origin.WasRemoved(trimmed))
            {
                throw new InvalidOperationException("The MULTILEADER, a copy in no drawing, draws " + what + " with the block of handle " + trimmed
                    + ", which the drawing it was copied from has erased since: append the copy to a drawing that has the block, or explode the original.");
            }

            return block;
        }

        /// <summary>The arrow blocks of the leader lines of their own (94 the index of a line, 345 the block), by the index.</summary>
        private static Dictionary<int, string> Arrows(List<KeyValuePair<int, string>> after)
        {
            var arrows = new Dictionary<int, string>();

            for (int i = 0; i + 1 < after.Count; i++)
            {
                if (after[i].Key == 94 && after[i + 1].Key == 345)
                {
                    arrows[Whole(after[i].Value, 0)] = after[i + 1].Value.Trim();
                }
            }

            return arrows;
        }

        /// <summary>
        /// An arrowhead at a point, pointing away from a direction, of a size: an INSERT of its block on the normal, turned so,
        /// or, of no block, AutoCAD's closed filled arrow as a SOLID a third as wide as long, as a straight LEADER's.
        /// </summary>
        private static DxfEntity Arrowhead(GeoPoint3 tip, GeoVector3 direction, double size, GeoVector3 normal, DxfBlockTableRecord block)
        {
            if (block == null)
            {
                GeoPoint3 back = tip.Add(direction.Multiply(size));
                GeoVector3 side = DxfOcsTransform.Normalized(normal.CrossProduct(direction)).Multiply(size / 6.0);
                return new DxfSolid(tip, back.Add(side), back.Add(side.Negate())) { Normal = normal };
            }

            OcsAxes(normal, out GeoVector3 x, out GeoVector3 y);
            GeoVector3 away = direction.Negate();
            return new DxfBlockReference(tip, block) { Normal = normal, ScaleFactors = new GeoVector3(size, size, size), Rotation = Math.Atan2(away.DotProduct(y), away.DotProduct(x)) };
        }

        /// <summary>Gives a part a colour as the multileader writes one (a raw colour), BYBLOCK, BYLAYER, of the index or true; one an entity cannot hold left as it is.</summary>
        private static void Colour(DxfEntity part, string raw)
        {
            DxfColor colour = DxfMLeaderColors.Of(raw);

            if (colour.ColorMethod == DxfColorMethod.ByBlock || colour.ColorMethod == DxfColorMethod.ByLayer || colour.ColorMethod == DxfColorMethod.ByAci || colour.ColorMethod == DxfColorMethod.ByColor)
            {
                part.Color = colour;
            }
        }
    }
}
