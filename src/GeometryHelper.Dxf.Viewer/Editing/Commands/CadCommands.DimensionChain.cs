using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The dimensions made in a row: DIMCONTINUE, DIMBASELINE and QDIM.</summary>
    /// <remarks>
    /// Each dimension is made as the DIM commands make one, in the current dimension style, with its picture in a block of its
    /// own. DIMCONTINUE and DIMBASELINE go on from the last linear, ordinate or angular dimension of the space, or one selected:
    /// a row of 100 and 200 from (0, 0) along a dimension line 30 below gives, continued to (300, 0), a dimension of 200 on the
    /// same line; based on it, a dimension of 300 from (0, 0) on a line DIMDLI further out. Of an ordinate dimension, either
    /// gives another from the same origin along the same axis, its leader ending level with the last's. The whole run is one
    /// undo step, and Undo inside takes back the last dimension made, as AutoCAD's.
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>Where a row of dimensions goes on from: the line or the arc of the last, and the origin the next one starts at.</summary>
        private sealed class Chain
        {
            public bool Angular { get; set; }

            /// <summary>Whether the row is of ordinate dimensions, each from <see cref="Origin"/>, its leader ending level with the last's.</summary>
            public bool Ordinate { get; set; }

            /// <summary>Whether an ordinate row measures along the X axis of its system.</summary>
            public bool AlongX { get; set; }

            /// <summary>How far the system of an ordinate row is turned, in radians.</summary>
            public double Turn { get; set; }

            /// <summary>Where the leader of the last ordinate dimension ends.</summary>
            public GeoPoint2 LeaderEnd { get; set; }

            /// <summary>The direction measured along, in radians, of a linear row.</summary>
            public double Angle { get; set; }

            /// <summary>A point of the dimension line of a linear row.</summary>
            public GeoPoint2 Line { get; set; }

            /// <summary>The vertex of an angular row.</summary>
            public GeoPoint2 Vertex { get; set; }

            /// <summary>The radius of the arc of an angular row.</summary>
            public double Radius { get; set; }

            /// <summary>The origin the next dimension starts from: the last second origin, or the base of a baseline row.</summary>
            public GeoPoint2 Origin { get; set; }

            /// <summary>What <see cref="Origin"/> is tied to, as the first point of the next dimension; null where it is tied to nothing.</summary>
            public DxfOsnapPointRef OriginTie { get; set; }

            /// <summary>What the vertex of an angular row is tied to, as the third point of the next dimension; null for none.</summary>
            public DxfOsnapPointRef VertexTie { get; set; }

            public Chain Copy() => (Chain)MemberwiseClone();
        }

        private static IEnumerable<CadPrompt> DimContinue(CadCommandContext c) => DimChain(c, false);

        private static IEnumerable<CadPrompt> DimBaseline(CadCommandContext c) => DimChain(c, true);

        /// <summary>
        /// DIMCONTINUE and DIMBASELINE: from the last linear or angular dimension of the space, else one selected, a dimension
        /// to each second origin given, continued from the last one's second extension line on the same line, or from the base
        /// extension line on a line DIMDLI further out each time.
        /// </summary>
        private static IEnumerable<CadPrompt> DimChain(CadCommandContext c, bool baseline)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            DxfDimension last = null;

            foreach (DxfEntity entity in c.Space.Block)
            {
                if (
                    entity is DxfDimension dimension
                    && !dimension.IsErased
                    && (IsChained(dimension) || dimension is DxfOrdinateDimension)
                )
                {
                    last = dimension;
                }
            }

            Chain chain = last == null ? null : ChainOf(last, null, baseline);

            var made = new List<string>();
            var before = new List<Chain>();

            while (true)
            {
                if (chain == null)
                {
                    yield return CadPrompt.ForEntity(baseline ? "Select base dimension" : "Select continued dimension");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    DxfDimension picked = c.Document.FindEntity(c.Input.Handles[0]) as DxfDimension;
                    chain =
                        picked == null || !(IsChained(picked) || picked is DxfOrdinateDimension)
                            ? null
                            : ChainOf(picked, c.Input.Point, baseline);

                    if (chain == null)
                    {
                        c.Write("Dimension must be linear, ordinate, or angular.");
                    }

                    continue;
                }

                double spacing = baseline ? style.Dimdli * (Dimscale(c) > 0.0 ? Dimscale(c) : 1.0) : 0.0;
                Chain from = chain;

                // The dimension the point at the cursor would add to the row, as it is added.
                CadPreview shown = DimPreview(c, input =>
                {
                    Chain after = ChainNext(from, spacing, input.Point);
                    return from.Ordinate
                            ? OrdinateMade(
                                c,
                                style,
                                from.AlongX,
                                from.Origin,
                                from.Turn,
                                input.Point,
                                after.LeaderEnd,
                                null,
                                null,
                                out _,
                                out _
                            )
                        : from.Angular
                            ? AngularDimensionMade(
                                c,
                                style,
                                from.Vertex,
                                from.Origin,
                                input.Point,
                                after.Radius,
                                out _,
                                out _
                            )
                        : LinearDimensionMade(c, style, from.Angle, from.Origin, input.Point, after.Line, out _, out _);
                });
                yield return (
                    chain.Ordinate
                        ? CadPrompt.ForPoint("Specify feature location", null, new[] { "Select", "Undo" }, "Select")
                        : CadPrompt.ForPoint(
                            "Specify a second extension line origin",
                            chain.Origin,
                            new[] { "Select", "Undo" },
                            "Select"
                        )
                ).Previewing(shown);

                if (c.Input.IsPoint)
                {
                    Chain next = ChainNext(chain, spacing, c.Input.Point);
                    string handle;

                    // AutoCAD 2021 ties the new dimension's second point to what it was snapped to, and its first to what the
                    // origin it starts from was tied to: DIMCONTINUE the last one's second, DIMBASELINE the base's first.
                    DxfOsnapPointRef snapped =
                        chain.Ordinate || chain.Angular
                            ? CadDimAssociation.FromSnap(c.Document, 1, c.Input.Snap)
                            : CadDimAssociation.FromSnap(c.Document, 1, c.Input.Snap, chain.Origin, chain.OriginTie);

                    if (chain.Ordinate)
                    {
                        handle = OrdinateDimension(
                            c,
                            style,
                            chain.AlongX,
                            chain.Origin,
                            chain.Turn,
                            c.Input.Point,
                            next.LeaderEnd,
                            null,
                            null,
                            true
                        );
                    }
                    else if (chain.Angular)
                    {
                        handle = AngularDimension(
                            c,
                            style,
                            chain.Vertex,
                            chain.Origin,
                            c.Input.Point,
                            next.Radius,
                            true
                        );
                    }
                    else
                    {
                        handle = LinearDimension(c, style, chain.Angle, chain.Origin, c.Input.Point, next.Line, true);
                    }

                    if (handle != null)
                    {
                        CadDimAssociation.Tie(
                            c,
                            handle,
                            chain.Ordinate
                                ? new[] { snapped }
                                : new[] { chain.OriginTie, snapped, chain.Angular ? chain.VertexTie : null }
                        );

                        if (!baseline && !chain.Ordinate)
                        {
                            next.Origin = c.Input.Point;
                            next.OriginTie = CadDimAssociation.Renumbered(snapped, 0);
                        }

                        made.Add(handle);
                        before.Add(chain);
                        chain = next;
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (made.Count == 0)
                    {
                        c.Write("Nothing to undo.");
                        continue;
                    }

                    EraseAll(c, new[] { made[made.Count - 1] });
                    made.RemoveAt(made.Count - 1);
                    chain = before[before.Count - 1];
                    before.RemoveAt(before.Count - 1);
                }
                else if (c.Input.Is("Select"))
                {
                    chain = null;
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// The row after a dimension to a point: an ordinate one's leader ending level with the last's, an angular one's arc and
        /// a linear one's line the spacing farther out (nought for a continued row).
        /// </summary>
        private static Chain ChainNext(Chain chain, double spacing, GeoPoint2 point)
        {
            Chain next = chain.Copy();

            if (chain.Ordinate)
            {
                // Continued or based alike, an ordinate dimension measures from the same origin, its leader ending level with the last's.
                GeoVector2 square = chain.AlongX
                    ? new GeoVector2(-Math.Sin(chain.Turn), Math.Cos(chain.Turn))
                    : new GeoVector2(Math.Cos(chain.Turn), Math.Sin(chain.Turn));
                double across = (chain.LeaderEnd - point).X * square.X + (chain.LeaderEnd - point).Y * square.Y;
                next.LeaderEnd = point + square * across;
            }
            else if (chain.Angular)
            {
                next.Radius = chain.Radius + spacing;
            }
            else
            {
                var along = new GeoVector2(Math.Cos(chain.Angle), Math.Sin(chain.Angle));
                var square = new GeoVector2(-along.Y, along.X);
                double side = (chain.Line - chain.Origin).X * square.X + (chain.Line - chain.Origin).Y * square.Y;
                next.Line = chain.Line + square * (side < 0.0 ? -spacing : spacing);
            }

            return next;
        }

        /// <summary>Whether a dimension is one a row goes on from: a linear (rotated or aligned) or an angular one.</summary>
        private static bool IsChained(DxfDimension dimension) =>
            dimension is DxfRotatedDimension
            || dimension is DxfAlignedDimension
            || dimension is DxfPoint3AngularDimension
            || dimension is DxfLineAngularDimension2;

        /// <summary>
        /// The row a dimension starts: its line or arc, and the origin the next dimension starts from, its second (continued) or
        /// first (baseline) extension line's, or the one nearer the point it was picked at; null where it cannot be read.
        /// </summary>
        private static Chain ChainOf(DxfDimension dimension, GeoPoint2? pick, bool baseline)
        {
            GeoPoint2 first;
            GeoPoint2 second;
            var chain = new Chain();

            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                    first = P2(rotated.XLine1Point);
                    second = P2(rotated.XLine2Point);
                    chain.Angle = rotated.Rotation;
                    chain.Line = P2(rotated.DimLinePoint);
                    break;
                case DxfAlignedDimension aligned:
                    first = P2(aligned.XLine1Point);
                    second = P2(aligned.XLine2Point);

                    if ((second - first).Length <= 1E-12)
                    {
                        return null;
                    }

                    chain.Angle = Math.Atan2(second.Y - first.Y, second.X - first.X);
                    chain.Line = P2(aligned.DimLinePoint);
                    break;
                case DxfOrdinateDimension ordinate:
                    chain.Ordinate = true;
                    chain.AlongX = ordinate.UsingXAxis;
                    chain.Turn = -ordinate.HorizontalRotation;
                    chain.Origin = P2(ordinate.Origin);
                    chain.LeaderEnd = P2(ordinate.LeaderEndPoint);
                    return chain;
                default:
                    if (!AngularRays(dimension, out GeoPoint2 vertex, out first, out second, out double radius))
                    {
                        return null;
                    }

                    chain.Angular = true;
                    chain.Vertex = vertex;
                    chain.Radius = radius;
                    break;
            }

            if (pick.HasValue)
            {
                chain.Origin = (pick.Value - first).Length < (pick.Value - second).Length ? first : second;
            }
            else
            {
                chain.Origin = baseline ? first : second;
            }

            chain.OriginTie = CadDimAssociation.TiedAt(dimension, chain.Origin, 0);
            chain.VertexTie = chain.Angular ? CadDimAssociation.TiedAt(dimension, chain.Vertex, 2) : null;
            return chain;
        }

        /// <summary>
        /// The vertex of an angular dimension, a point on each ray of the angle it measures (the first counter-clockwise from
        /// the second) and the radius of its arc; false where it measures nothing.
        /// </summary>
        private static bool AngularRays(
            DxfDimension dimension,
            out GeoPoint2 vertex,
            out GeoPoint2 first,
            out GeoPoint2 second,
            out double radius
        )
        {
            vertex = default;
            first = default;
            second = default;
            radius = 0.0;
            DxfLine line1 = null;
            DxfLine line2 = null;
            var points = new GeoPoint2[2];
            GeoPoint2 arc;

            switch (dimension)
            {
                case DxfPoint3AngularDimension three:
                    vertex = P2(three.CenterPoint);
                    points[0] = P2(three.XLine1Point);
                    points[1] = P2(three.XLine2Point);
                    arc = P2(three.ArcPoint);
                    break;
                case DxfLineAngularDimension2 two:
                    line1 = new DxfLine(two.XLine1Start, two.XLine1End);
                    line2 = new DxfLine(two.XLine2Start, two.XLine2End);

                    if (
                        line1.StartPoint.DistanceTo(line1.EndPoint) <= 1E-12
                        || line2.StartPoint.DistanceTo(line2.EndPoint) <= 1E-12
                        || !Meet(line1, line2, new Tolerance(1E-9, 1E-9), out vertex)
                    )
                    {
                        return false;
                    }

                    arc = P2(two.ArcPoint);
                    break;
                default:
                    return false;
            }

            radius = (arc - vertex).Length;

            if (
                radius <= 1E-12
                || (line1 == null && ((points[0] - vertex).Length <= 1E-12 || (points[1] - vertex).Length <= 1E-12))
            )
            {
                return false;
            }

            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            AngularSector(line1, line2, vertex, points, arc, rays, near, far, out _, out double sweep);

            if (sweep <= 1E-12)
            {
                return false;
            }

            first = vertex + rays[0] * (far[0] > 0.0 ? far[0] : radius);
            second = vertex + rays[1] * (far[1] > 0.0 ? far[1] : radius);
            return true;
        }

        /// <summary>
        /// Makes a rotated dimension from one origin to another along a direction, its line through a point, as DIMLINEAR makes
        /// one; "Dimension text = ..." written where <paramref name="echo"/> says. The handle of the dimension, or null where it
        /// measures nothing or was not made.
        /// </summary>
        private static string LinearDimension(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            double angle,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 onLine,
            bool echo
        )
        {
            List<DxfEntity> picture = LinearDimensionMade(
                c,
                style,
                angle,
                p1,
                p2,
                onLine,
                out DxfDimension made,
                out string words
            );

            if (picture == null)
            {
                if (echo)
                {
                    c.Write("*Invalid* The dimension measures nothing.");
                }

                return null;
            }

            if (echo)
            {
                c.Write("Dimension text = " + words);
            }

            return AddedHandle(Place(c, style, TextHeight(c, style), made, picture));
        }

        /// <summary>The picture of the rotated dimension <see cref="LinearDimension"/> makes, and the dimension; null where it measures nothing.</summary>
        private static List<DxfEntity> LinearDimensionMade(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            double angle,
            GeoPoint2 p1,
            GeoPoint2 p2,
            GeoPoint2 onLine,
            out DxfDimension made,
            out string words
        )
        {
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);
            made = null;
            words = null;

            if (measured <= c.Session.Tolerance.EqualPoint)
            {
                return null;
            }

            GeoPoint2 q1 = Foot(p1, onLine, along, 0.0);
            GeoPoint2 q2 = Foot(p2, onLine, along, 0.0);
            made = new DxfRotatedDimension(angle, P3(p1), P3(p2), P3(q2), string.Empty, null);
            words = Words(style, made, measured, null, string.Empty);
            double textHeight = TextHeight(c, style);
            return Linear(style, Dimscale(c), textHeight, p1, p2, q1, q2, words);
        }

        /// <summary>
        /// Makes a three-point angular dimension about a vertex between two points, of the smaller of the two angles they make,
        /// its arc at a radius; the handle of the dimension, or null where it measures nothing.
        /// </summary>
        private static string AngularDimension(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            GeoPoint2 vertex,
            GeoPoint2 a,
            GeoPoint2 b,
            double radius,
            bool echo
        )
        {
            List<DxfEntity> picture = AngularDimensionMade(
                c,
                style,
                vertex,
                a,
                b,
                radius,
                out DxfDimension made,
                out string words
            );

            if (picture == null)
            {
                if (echo)
                {
                    c.Write("*Invalid* The angle measures nothing.");
                }

                return null;
            }

            if (echo)
            {
                c.Write("Dimension text = " + words);
            }

            return AddedHandle(Place(c, style, TextHeight(c, style), made, picture));
        }

        /// <summary>The picture of the angular dimension <see cref="AngularDimension"/> makes, and the dimension; null where it measures nothing.</summary>
        private static List<DxfEntity> AngularDimensionMade(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            GeoPoint2 vertex,
            GeoPoint2 a,
            GeoPoint2 b,
            double radius,
            out DxfDimension made,
            out string words
        )
        {
            made = null;
            words = null;

            if (
                (a - vertex).Length <= c.Session.Tolerance.EqualPoint
                || (b - vertex).Length <= c.Session.Tolerance.EqualPoint
                || radius <= c.Session.Tolerance.EqualPoint
            )
            {
                return null;
            }

            double a1 = Math.Atan2(a.Y - vertex.Y, a.X - vertex.X);
            double a2 = Math.Atan2(b.Y - vertex.Y, b.X - vertex.X);
            double turn = CadGrips.Sweep(a1, a2);
            double middle = turn <= Math.PI ? a1 + turn / 2.0 : a2 + (2.0 * Math.PI - turn) / 2.0;
            GeoPoint2 arc = vertex + new GeoVector2(Math.Cos(middle), Math.Sin(middle)) * radius;
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            AngularSector(null, null, vertex, new[] { a, b }, arc, rays, near, far, out double from, out double sweep);

            if (sweep <= c.Session.Tolerance.EqualAngleRad / 1000.0)
            {
                return null;
            }

            made = new DxfPoint3AngularDimension(P3(vertex), P3(a), P3(b), P3(arc), string.Empty, null);
            words = Words(style, made, sweep, null, string.Empty);
            double textHeight = TextHeight(c, style);
            return Angular(style, Dimscale(c), textHeight, vertex, radius, from, sweep, near, far, words);
        }

        /// <summary>
        /// QDIM: dimensions of the geometry selected, made at once where the dimension line is put: Continuous, a row end to
        /// end; Staggered, pairs from the outside in, nested DIMDLI apart; Baseline, each from the first point (or the datum);
        /// Ordinate, an ordinate dimension of each from the datum (or the origin of the UCS), its leader ending on the line;
        /// Radius and Diameter, one of each circle and arc. The points are the ends of lines, the vertices of polylines and the
        /// centres of circles and arcs, measured level or upright by where the line is put, as DIMLINEAR measures.
        /// </summary>
        private static IEnumerable<CadPrompt> QuickDimension(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            yield return CadPrompt.ForSelection("Select geometry to dimension");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            var points = new List<GeoPoint2>();
            var round = new List<DxfCurve>();

            // What each point is tied to, as AutoCAD 2021's QDIM ties it: the ends of lines, the vertices of polylines, the
            // centres of circles and arcs.
            var ties = new Dictionary<GeoPoint2, DxfOsnapPointRef>();

            void Add(GeoPoint2 point, DxfOsnapPointRef tie)
            {
                points.Add(point);

                if (!ties.ContainsKey(point))
                {
                    ties[point] = tie;
                }
            }

            foreach (string handle in c.Input.Handles)
            {
                switch (c.Document.FindEntity(handle))
                {
                    case DxfLine line:
                        DxfOsnapPointRef[] ends = CadDimAssociation.LineEnds(line);
                        Add(P2(line.StartPoint), ends[0]);
                        Add(P2(line.EndPoint), ends[1]);
                        break;
                    case DxfPolyline polyline:
                        for (int i = 0; i < polyline.NumberOfVertices; i++)
                        {
                            Add(polyline.GetPoint2dAt(i), CadDimAssociation.VertexOf(polyline, i));
                        }

                        break;
                    case DxfCircle circle:
                        Add(P2(circle.Center), CadDimAssociation.CenterOf(circle)[0]);
                        round.Add(circle);
                        break;
                    case DxfArc arc:
                        Add(P2(arc.Center), CadDimAssociation.CenterOf(arc)[0]);
                        round.Add(arc);
                        break;
                }
            }

            if (points.Count == 0)
            {
                c.Write("No geometry selected to dimension.");
                yield break;
            }

            string mode = "Continuous";
            GeoPoint2? datum = null;
            string[] keywords =
            {
                "Continuous",
                "Staggered",
                "Baseline",
                "Ordinate",
                "Radius",
                "Diameter",
                "datumPoint",
                "Edit",
                "seTtings",
            };

            // The dimensions put at a point, made; or, given a list, their pictures added to it, as the row previews at the cursor.
            int Dimensioned(GeoPoint2 location, List<DxfEntity> preview)
            {
                if (mode != "Radius" && mode != "Diameter")
                {
                    return QuickLinear(c, style, points, ties, datum, location, mode, preview);
                }

                int count = 0;

                foreach (DxfCurve curve in round)
                {
                    count += RadialOf(c, style, curve, location, mode == "Diameter", preview) ? 1 : 0;
                }

                return count;
            }

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify dimension line position,", null, keywords, mode)
                    .Previewing(DimPreview(c, input =>
                        {
                            var preview = new List<DxfEntity>();
                            return Dimensioned(input.Point, preview) > 0 ? preview : null;
                        }));

                if (c.Input.IsPoint)
                {
                    break;
                }

                if (
                    c.Input.Is("Continuous")
                    || c.Input.Is("Staggered")
                    || c.Input.Is("Baseline")
                    || c.Input.Is("Ordinate")
                    || c.Input.Is("Radius")
                    || c.Input.Is("Diameter")
                )
                {
                    mode = c.Input.Text;
                }
                else if (c.Input.Is("datumPoint"))
                {
                    yield return CadPrompt.ForPoint("Select new datum point");

                    if (c.Input.IsPoint)
                    {
                        datum = c.Input.Point;
                    }
                }
                else if (c.Input.Is("Edit"))
                {
                    bool removing = true;

                    while (true)
                    {
                        yield return removing
                            ? CadPrompt.ForPoint(
                                "Indicate dimension point to remove,",
                                null,
                                new[] { "Add", "eXit" },
                                "eXit"
                            )
                            : CadPrompt.ForPoint(
                                "Indicate dimension point to add,",
                                null,
                                new[] { "Remove", "eXit" },
                                "eXit"
                            );

                        if (c.Input.IsPoint && removing)
                        {
                            int nearest = Nearest(points, c.Input.Point);

                            if (
                                nearest >= 0
                                && (points[nearest] - c.Input.Point).Length
                                    <= Math.Max(c.Session.PickAperture, c.Session.Tolerance.EqualPoint)
                            )
                            {
                                GeoPoint2 gone = points[nearest];
                                points.RemoveAll(p => (p - gone).Length <= c.Session.Tolerance.EqualPoint);
                                c.Write("One dimension point removed.");
                            }
                            else
                            {
                                c.Write("No dimension point there.");
                            }
                        }
                        else if (c.Input.IsPoint)
                        {
                            points.Add(c.Input.Point);
                            c.Write("One dimension point added.");
                        }
                        else if (c.Input.Is("Add") || c.Input.Is("Remove"))
                        {
                            removing = c.Input.Is("Remove");
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else if (c.Input.Is("seTtings"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Associative dimension priority",
                        new[] { "Endpoint", "Intersection" },
                        "Endpoint"
                    );
                }
                else
                {
                    yield break;
                }
            }

            if (Dimensioned(c.Input.Point, null) == 0)
            {
                c.Write("No dimensions made: the geometry selected has nothing to dimension that way.");
            }
        }

        private static int Nearest(List<GeoPoint2> points, GeoPoint2 to)
        {
            int nearest = -1;

            for (int i = 0; i < points.Count; i++)
            {
                if (nearest < 0 || (points[i] - to).Length < (points[nearest] - to).Length)
                {
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The linear dimensions of QDIM: the points ordered along the axis the line is put across (level where it lies above
        /// or below them, upright where to a side), those within the tolerance of one another along it taken as one, then
        /// dimensioned as the mode says. The number made; given a list to preview into, the number whose pictures were added to
        /// it instead.
        /// </summary>
        private static int QuickLinear(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            List<GeoPoint2> points,
            Dictionary<GeoPoint2, DxfOsnapPointRef> ties,
            GeoPoint2? datum,
            GeoPoint2 location,
            string mode,
            List<DxfEntity> preview = null
        )
        {
            CadUcs ucs = c.Session.Ucs;
            var local = new List<GeoPoint2>();
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (GeoPoint2 point in points)
            {
                GeoPoint2 u = ucs.ToUcs(point);
                local.Add(u);
                minX = Math.Min(minX, u.X);
                minY = Math.Min(minY, u.Y);
                maxX = Math.Max(maxX, u.X);
                maxY = Math.Max(maxY, u.Y);
            }

            GeoPoint2 at = ucs.ToUcs(location);
            bool level = LinearAngle(new GeoPoint2(minX, minY), new GeoPoint2(maxX, maxY), at) == 0.0;
            double angle = ucs.AngleToWorld(level ? 0.0 : Math.PI / 2.0);

            // Ordered along the axis measured, then across it, then as given; one point kept of those the tolerance makes one.
            var order = new List<int>();

            for (int i = 0; i < local.Count; i++)
            {
                order.Add(i);
            }

            order.Sort(
                (i, j) =>
                {
                    int byAlong = (level ? local[i].X : local[i].Y).CompareTo(level ? local[j].X : local[j].Y);

                    if (byAlong != 0)
                    {
                        return byAlong;
                    }

                    int byAcross = (level ? local[i].Y : local[i].X).CompareTo(level ? local[j].Y : local[j].X);
                    return byAcross != 0 ? byAcross : i.CompareTo(j);
                }
            );

            var row = new List<GeoPoint2>();
            double lastAlong = double.NaN;

            foreach (int i in order)
            {
                double along = level ? local[i].X : local[i].Y;

                if (row.Count == 0 || along - lastAlong > c.Session.Tolerance.EqualPoint)
                {
                    row.Add(points[i]);
                    lastAlong = along;
                }
            }

            // Lines further out are put away from the points: up or right where the line is put above or to the right of their middle.
            double across = level ? at.Y : at.X;
            double middle = level ? (minY + maxY) / 2.0 : (minX + maxX) / 2.0;
            GeoVector2 square = new GeoVector2(-Math.Sin(angle), Math.Cos(angle)) * (across >= middle ? 1.0 : -1.0);
            double spacing = style.Dimdli * (Dimscale(c) > 0.0 ? Dimscale(c) : 1.0);
            int made = 0;

            // Ties a dimension made to what its points are tied to, numbered from the first given; false where none was made.
            bool Tied(string handle, int first, params GeoPoint2[] tied)
            {
                if (handle == null)
                {
                    return false;
                }

                var references = new DxfOsnapPointRef[tied.Length];

                for (int i = 0; i < tied.Length; i++)
                {
                    references[i] = ties.TryGetValue(tied[i], out DxfOsnapPointRef tie)
                        ? CadDimAssociation.Renumbered(tie, first + i)
                        : null;
                }

                CadDimAssociation.Tie(c, handle, references);
                return true;
            }

            // One rotated dimension of the row, made and tied; or, for a preview, its picture added to the list.
            bool Measured(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 onLine)
            {
                if (preview == null)
                {
                    return Tied(LinearDimension(c, style, angle, p1, p2, onLine, false), 0, p1, p2);
                }

                List<DxfEntity> picture = LinearDimensionMade(c, style, angle, p1, p2, onLine, out _, out _);
                preview.AddRange(picture ?? new List<DxfEntity>());
                return picture != null;
            }

            switch (mode)
            {
                case "Ordinate":
                    // From the datum, else the origin of the UCS: along X where the line is put above or below the points, each
                    // leader ending on it; else along Y.
                    GeoPoint2 zero = datum ?? new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);

                    foreach (GeoPoint2 point in row)
                    {
                        GeoPoint2 u = ucs.ToUcs(point);
                        GeoPoint2 end = ucs.ToWorld(level ? new GeoPoint2(u.X, at.Y) : new GeoPoint2(at.X, u.Y));
                        if (preview == null)
                        {
                            made += Tied(
                                OrdinateDimension(c, style, level, zero, ucs.Angle, point, end, null, null, false),
                                1,
                                point
                            )
                                ? 1
                                : 0;
                        }
                        else if ((end - point).Length > c.Session.Tolerance.EqualPoint)
                        {
                            preview.AddRange(
                                OrdinateMade(c, style, level, zero, ucs.Angle, point, end, null, null, out _, out _)
                            );
                            made++;
                        }
                    }

                    break;
                case "Baseline":
                    GeoPoint2 origin = datum ?? row[0];
                    int step = 0;

                    foreach (GeoPoint2 point in row)
                    {
                        if (Measured(origin, point, location + square * (spacing * step)))
                        {
                            made++;
                            step++;
                        }
                    }

                    break;
                case "Staggered":
                    for (int i = row.Count / 2 - 1, step2 = 0; i >= 0; i--)
                    {
                        if (Measured(row[i], row[row.Count - 1 - i], location + square * (spacing * step2)))
                        {
                            made++;
                            step2++;
                        }
                    }

                    break;
                default:
                    for (int i = 0; i + 1 < row.Count; i++)
                    {
                        made += Measured(row[i], row[i + 1], location) ? 1 : 0;
                    }

                    break;
            }

            return made;
        }

        /// <summary>
        /// A radius or a diameter of a circle or an arc for QDIM, its line toward the point the dimensions are put at; given a
        /// list to preview into, its picture added to it instead.
        /// </summary>
        private static bool RadialOf(
            CadCommandContext c,
            DxfDimStyleTableRecord style,
            DxfCurve curve,
            GeoPoint2 location,
            bool diameter,
            List<DxfEntity> preview = null
        )
        {
            GeoPoint2 center;
            double radius;

            if (curve is DxfCircle circle)
            {
                center = P2(circle.Center);
                radius = circle.Radius;
            }
            else
            {
                var arc = (DxfArc)curve;
                center = P2(arc.Center);
                radius = arc.Radius;
            }

            GeoVector2 toward = location - center;

            if (toward.Length <= c.Session.Tolerance.EqualPoint)
            {
                toward = new GeoVector2(1.0, 0.0);
            }

            GeoVector2 unit = toward / toward.Length;
            double textHeight = TextHeight(c, style);
            double scale = Dimscale(c) > 0.0 ? Dimscale(c) : 1.0;
            GeoPoint2 chord = center + unit * radius;
            GeoPoint2 far = center - unit * radius;
            double leader = 2.0 * style.Dimasz * scale;
            string prefix = diameter ? "%%c" : "R";
            DxfDimension made = diameter
                ? (DxfDimension)new DxfDiametricDimension(P3(chord), P3(far), leader, string.Empty, null)
                : new DxfRadialDimension(P3(center), P3(chord), leader, string.Empty, null);
            string words = Words(style, made, diameter ? 2.0 * radius : radius, null, prefix);
            List<DxfEntity> picture = Radial(
                style,
                Dimscale(c),
                textHeight,
                center,
                radius,
                chord,
                far,
                chord + unit * leader,
                diameter,
                words
            );

            if (preview != null)
            {
                preview.AddRange(picture);
                return true;
            }

            return AddedHandle(Place(c, style, textHeight, made, picture)) != null;
        }
    }
}
