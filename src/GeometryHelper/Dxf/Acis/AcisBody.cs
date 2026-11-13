using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf
{
    /// <summary>One loop of a planar face: its edges in order, counter-clockwise about the face's normal for an outer loop and clockwise for a hole.</summary>
    internal sealed class AcisLoop
    {
        internal AcisLoop(IReadOnlyList<AcisCurve> curves, bool isHole)
        {
            Curves = curves;
            IsHole = isHole;
        }

        internal IReadOnlyList<AcisCurve> Curves { get; }

        internal bool IsHole { get; }

        /// <summary>The perimeter of the loop.</summary>
        internal double Perimeter => Curves.Sum(c => c.Length);

        /// <summary>The area the loop encloses, positive, found along <paramref name="normal"/>.</summary>
        internal double Area(AcisVec normal)
        {
            AcisVec sum = default(AcisVec);

            foreach (AcisCurve curve in Curves)
            {
                sum += curve.AreaVector();
            }

            return Math.Abs(sum.Dot(normal));
        }
    }

    /// <summary>A planar face: the plane's normal and a point of it, and the loops, outer and holes.</summary>
    internal sealed class AcisFace
    {
        internal AcisFace(AcisVec origin, AcisVec normal, IReadOnlyList<AcisLoop> loops)
        {
            Origin = origin;
            Normal = normal;
            Loops = loops;
        }

        internal AcisVec Origin { get; }

        /// <summary>The unit normal of the plane, the outer loop running counter-clockwise about it.</summary>
        internal AcisVec Normal { get; }

        internal IReadOnlyList<AcisLoop> Loops { get; }

        internal double Area => Loops.Sum(l => l.IsHole ? -l.Area(Normal) : l.Area(Normal));

        internal double Perimeter => Loops.Sum(l => l.Perimeter);
    }

    /// <summary>
    /// The planar body of a REGION: its faces, each in a plane with its loops; made from the curves of closed loops, and turned
    /// into the records of ACIS data.
    /// </summary>
    internal sealed class AcisBody
    {
        internal AcisBody(IReadOnlyList<AcisFace> faces)
        {
            Faces = faces;
        }

        internal IReadOnlyList<AcisFace> Faces { get; }

        internal double Area => Faces.Sum(f => f.Area);

        internal double Perimeter => Faces.Sum(f => f.Perimeter);

        /// <summary>
        /// Makes the body of one face from the curves of an outer loop and of the holes in it, each a set of edges that join end
        /// to end in any order and direction. An outer loop is turned to run counter-clockwise about the normal of its plane,
        /// which is +Z where the loop lies flat on the XY plane, else the way the loop runs; a hole is turned clockwise.
        /// </summary>
        /// <param name="outer">The edges of the outer loop.</param>
        /// <param name="holes">The edges of each hole.</param>
        /// <param name="tolerance">The distance within which two ends meet and a point lies in the plane.</param>
        /// <exception cref="ArgumentException">Thrown when a loop does not close, has no area, or does not lie in one plane.</exception>
        internal static AcisBody Create(
            IReadOnlyList<AcisCurve> outer,
            IReadOnlyList<IReadOnlyList<AcisCurve>> holes,
            double tolerance
        )
        {
            List<AcisCurve> outerChain = Chain(outer, tolerance, "The outer loop");
            AcisVec turn = Turn(outerChain);

            if (turn.Length < tolerance * tolerance)
            {
                throw new ArgumentException("The outer loop encloses no area.", nameof(outer));
            }

            AcisVec normal = turn.Unit;
            double flat = 1E-9;

            if (Math.Abs(normal.X) < flat && Math.Abs(normal.Y) < flat)
            {
                normal = new AcisVec(0.0, 0.0, 1.0);

                if (turn.Z < 0.0)
                {
                    outerChain = Turned(outerChain);
                }
            }

            var loops = new List<AcisLoop>();

            for (int i = 0; i < holes.Count; i++)
            {
                List<AcisCurve> chain = Chain(holes[i], tolerance, "Hole " + (i + 1).ToString());

                if (Turn(chain).Dot(normal) > 0.0)
                {
                    chain = Turned(chain, true);
                }

                loops.Add(new AcisLoop(chain, true));
            }

            loops.Add(new AcisLoop(outerChain, false));

            foreach (AcisLoop loop in loops)
            {
                foreach (AcisCurve curve in loop.Curves)
                {
                    InPlane(curve, outerChain[0].Start, normal, tolerance);
                }
            }

            return new AcisBody(new[] { new AcisFace(Centre(outerChain), normal, loops) });
        }

        private static void InPlane(AcisCurve curve, AcisVec origin, AcisVec normal, double tolerance)
        {
            bool flat =
                Math.Abs((curve.Start - origin).Dot(normal)) <= tolerance
                && Math.Abs((curve.End - origin).Dot(normal)) <= tolerance;

            if (!curve.IsLine)
            {
                flat =
                    flat
                    && Math.Abs((curve.Center - origin).Dot(normal)) <= tolerance
                    && curve.Normal.Cross(normal).Length <= 1E-6;
            }

            if (!flat)
            {
                throw new ArgumentException("The loops do not lie in one plane.");
            }
        }

        /// <summary>The area vector of a closed loop, counter-clockwise positive along the direction it points.</summary>
        private static AcisVec Turn(IEnumerable<AcisCurve> chain)
        {
            AcisVec sum = default(AcisVec);

            foreach (AcisCurve curve in chain)
            {
                sum += curve.AreaVector();
            }

            return sum;
        }

        /// <summary>
        /// A loop run the other way: its edges in the opposite order, each turned over. A hole is run so from the edge it began
        /// with, as AutoCAD turns the loop of a region it cuts out: the first edge turned over stays first.
        /// </summary>
        private static List<AcisCurve> Turned(List<AcisCurve> chain, bool keepFirst = false)
        {
            var turned = new List<AcisCurve>(chain.Count);

            if (keepFirst)
            {
                turned.Add(chain[0].Reversed());
            }

            for (int i = chain.Count - 1; i >= (keepFirst ? 1 : 0); i--)
            {
                turned.Add(chain[i].Reversed());
            }

            return turned;
        }

        /// <summary>A point of the plane near the middle of a loop: that of the bounding box of its ends and, for a whole ellipse, its centre.</summary>
        private static AcisVec Centre(IReadOnlyList<AcisCurve> chain)
        {
            var points = new List<AcisVec>();

            foreach (AcisCurve curve in chain)
            {
                points.Add(curve.IsFull ? curve.Center : curve.Start);
            }

            return new AcisVec(
                (points.Min(p => p.X) + points.Max(p => p.X)) * 0.5,
                (points.Min(p => p.Y) + points.Max(p => p.Y)) * 0.5,
                (points.Min(p => p.Z) + points.Max(p => p.Z)) * 0.5
            );
        }

        /// <summary>
        /// Orders edges into one closed loop: from the first, each next edge is the one that starts (or, turned over, ends) where
        /// the last stopped, until the loop returns to its first point. A whole ellipse is a loop of itself.
        /// </summary>
        private static List<AcisCurve> Chain(IReadOnlyList<AcisCurve> curves, double tolerance, string what)
        {
            if (curves == null || curves.Count == 0)
            {
                throw new ArgumentException(what + " has no edges.");
            }

            var left = new List<AcisCurve>(curves);
            var chain = new List<AcisCurve> { left[0] };
            left.RemoveAt(0);

            if (chain[0].IsFull)
            {
                if (left.Count > 0)
                {
                    throw new ArgumentException(what + " has a whole ellipse and other edges: it closes with itself.");
                }

                return chain;
            }

            while (true)
            {
                AcisVec at = chain[chain.Count - 1].End;

                if (at.DistanceTo(chain[0].Start) <= tolerance)
                {
                    break;
                }

                int next = left.FindIndex(c => !c.IsFull && c.Start.DistanceTo(at) <= tolerance);
                bool turned = false;

                if (next < 0)
                {
                    next = left.FindIndex(c => !c.IsFull && c.End.DistanceTo(at) <= tolerance);
                    turned = true;
                }

                if (next < 0)
                {
                    throw new ArgumentException(what + " does not close: no edge leaves " + at.X + ", " + at.Y + ".");
                }

                chain.Add(turned ? left[next].Reversed() : left[next]);
                left.RemoveAt(next);
            }

            if (left.Count > 0)
            {
                throw new ArgumentException(what + " has edges left over after it closes.");
            }

            return chain;
        }

        /// <summary>
        /// The records of the body as AutoCAD writes a region: a header (from R2004), the body, a lump, a shell, the face on its
        /// plane, and for each loop its coedges, their edges and curves, and the vertices and points, numbered as ASM numbers
        /// them. A body of more than one face is not written.
        /// </summary>
        internal List<AcisRecord> Records(AcisFormat format)
        {
            if (Faces.Count != 1)
            {
                throw new NotSupportedException("A body of more than one face is not written.");
            }

            AcisFace face = Faces[0];
            var roots = new List<AcisRecord>();

            if (format.History)
            {
                roots.Add(new AcisRecord("asmheader").Pointer(null).Whole(-1).Text("226.0.0.65535"));
            }

            AcisRecord body = Start("body", format);
            AcisRecord lump = Start("lump", format);
            AcisRecord shell = Start("shell", format);
            AcisRecord faceRecord = Start("face", format);
            AcisRecord surface = Start("plane-surface", format);
            roots.Add(body);
            body.Pointer(lump).Pointer(null).Pointer(null);
            lump.Pointer(null).Pointer(shell).Pointer(body);
            shell.Pointer(null).Pointer(null).Pointer(faceRecord).Pointer(null).Pointer(lump);

            var loopRecords = face.Loops.Select(l => Start("loop", format)).ToList();
            faceRecord
                .Pointer(null)
                .Pointer(loopRecords[0])
                .Pointer(shell)
                .Pointer(null)
                .Pointer(surface)
                .Flag(false, "forward", "reversed")
                .Flag(true, "single", "double")
                .Flag(false, "out", "in");
            AcisVec x = ArbitraryX(face.Normal);
            surface
                .Position(face.Origin)
                .Direction(face.Normal)
                .Direction(x)
                .Flag(false, "forward_v", "reverse_v")
                .Bound(null)
                .Bound(null)
                .Bound(null)
                .Bound(null);

            var chained = new List<EdgeEnds>();

            for (int l = 0; l < face.Loops.Count; l++)
            {
                IReadOnlyList<AcisCurve> curves = face.Loops[l].Curves;
                int n = curves.Count;
                var coedges = new List<AcisRecord>();
                var edgeRecords = new List<AcisRecord>();
                var vertexRecords = new List<AcisRecord>();
                var loopEnds = new List<EdgeEnds>();

                for (int i = 0; i < n; i++)
                {
                    coedges.Add(Start("coedge", format));
                    edgeRecords.Add(Start("edge", format));
                    vertexRecords.Add(Start("vertex", format));
                }

                loopRecords[l]
                    .Pointer(l + 1 < loopRecords.Count ? loopRecords[l + 1] : null)
                    .Pointer(coedges[0])
                    .Pointer(faceRecord);

                for (int i = 0; i < n; i++)
                {
                    AcisCurve curve = curves[i];
                    AcisRecord coedge = coedges[i];
                    AcisRecord edge = edgeRecords[i];
                    coedge
                        .Pointer(coedges[(i + 1) % n])
                        .Pointer(coedges[(i + n - 1) % n])
                        .Pointer(null)
                        .Pointer(edge)
                        .Flag(false, "forward", "reversed")
                        .Pointer(loopRecords[l]);

                    if (format.CoedgeNumber)
                    {
                        coedge.Whole(0);
                    }

                    coedge.Pointer(null);
                    edge.Pointer(vertexRecords[i]);

                    // AutoCAD runs the line of a hole's edge from minus its length up to nought, its origin at the edge's end.
                    bool backwards = curve.IsLine && face.Loops[l].IsHole;
                    double length = curve.IsLine ? curve.Start.DistanceTo(curve.End) : 0.0;

                    if (format.History)
                    {
                        edge.Real(
                            curve.IsLine
                                ? backwards
                                    ? -length
                                    : 0.0
                                : curve.T0
                        );
                    }

                    edge.Pointer(vertexRecords[(i + 1) % n]);

                    if (format.History)
                    {
                        edge.Real(
                            curve.IsLine
                                ? backwards
                                    ? 0.0
                                    : length
                                : curve.T1
                        );
                    }

                    AcisRecord geometry = Start(curve.IsLine ? "straight-curve" : "ellipse-curve", format);
                    edge.Pointer(coedge).Pointer(geometry).Flag(false, "forward", "reversed");

                    if (format.History)
                    {
                        edge.Text("unknown");
                    }

                    if (curve.IsLine)
                    {
                        geometry
                            .Position(backwards ? curve.End : curve.Start)
                            .Direction((curve.End - curve.Start).Unit)
                            .Bound(backwards ? -length : 0.0)
                            .Bound(backwards ? 0.0 : length);
                    }
                    else
                    {
                        geometry
                            .Position(curve.Center)
                            .Direction(curve.Normal)
                            .Direction(curve.Major)
                            .Real(curve.Ratio);

                        if (curve.IsFull)
                        {
                            geometry.Bound(null).Bound(null);
                        }
                        else
                        {
                            geometry.Bound(curve.T0).Bound(curve.T1);
                        }
                    }
                }

                // The vertex that opens an edge points at that edge for now; the number it carries is set once they are numbered.
                for (int i = 0; i < n; i++)
                {
                    AcisRecord vertex = vertexRecords[i];
                    vertex.Pointer(edgeRecords[i]);

                    if (format.VertexNumber)
                    {
                        vertex.Whole(0);
                    }

                    AcisRecord point = Start("point", format);
                    vertex.Pointer(point);
                    point.Position(curves[i].Start);
                    loopEnds.Add(
                        new EdgeEnds(edgeRecords[i], vertexRecords[i], vertexRecords[(i + 1) % n], face.Loops[l].IsHole)
                    );
                }

                // A hole claims its vertices in the order of the loop it was turned from: the first edge, then the others from
                // the last back, each from its end.
                if (face.Loops[l].IsHole)
                {
                    chained.Add(loopEnds[0]);

                    for (int i = n - 1; i >= 1; i--)
                    {
                        chained.Add(loopEnds[i]);
                    }
                }
                else
                {
                    chained.AddRange(loopEnds);
                }
            }

            // A vertex belongs to the first edge, along the loops, that starts or ends at it, as AutoCAD sets them: the edge it
            // points at, and from R2007 a number, 0 for the start of that edge and 1 for the end. A whole circle or ellipse
            // starts and ends at its one vertex: 2 for the outer loop, as AutoCAD writes it, and 0 for a hole.
            var owned = new HashSet<AcisRecord>();
            int slot = format.History ? 3 : 1;

            foreach (EdgeEnds ends in chained)
            {
                foreach (
                    AcisRecord vertex in (
                        ends.Hole ? new[] { ends.Last, ends.First } : new[] { ends.First, ends.Last }
                    ).Distinct()
                )
                {
                    if (!owned.Add(vertex))
                    {
                        continue;
                    }

                    vertex.Fields[slot] = new AcisField('p', ends.Edge, 0, 0.0, default(AcisVec), null, null, null);

                    if (format.VertexNumber)
                    {
                        int number =
                            ends.First == ends.Last ? (ends.Hole ? 0 : 2)
                            : vertex == ends.First ? 0
                            : 1;
                        vertex.Fields[slot + 1] = new AcisField(
                            'i',
                            null,
                            number,
                            0.0,
                            default(AcisVec),
                            null,
                            null,
                            null
                        );
                    }
                }
            }

            return AcisWriter.Number(roots);
        }

        /// <summary>The two vertices of an edge, in the order the loops run, and whether its loop is a hole.</summary>
        private sealed class EdgeEnds
        {
            internal EdgeEnds(AcisRecord edge, AcisRecord first, AcisRecord last, bool hole)
            {
                Edge = edge;
                First = first;
                Last = last;
                Hole = hole;
            }

            internal AcisRecord Edge { get; }

            internal AcisRecord First { get; }

            internal AcisRecord Last { get; }

            internal bool Hole { get; }
        }

        /// <summary>A record with the fields every record opens with: its attribute pointer, and from R2004 its history number and pointer.</summary>
        private static AcisRecord Start(string name, AcisFormat format)
        {
            var record = new AcisRecord(name);
            record.Pointer(null);

            if (format.History)
            {
                record.Whole(-1).Pointer(null);
            }

            return record;
        }

        /// <summary>The x axis of the plane of a normal as AutoCAD's arbitrary axis algorithm gives it: Y x N, or Z x N where the normal is not within 1/64 of the Z axis.</summary>
        internal static AcisVec ArbitraryX(AcisVec normal)
        {
            AcisVec axis =
                Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0
                    ? new AcisVec(0.0, 1.0, 0.0).Cross(normal)
                    : new AcisVec(0.0, 0.0, 1.0).Cross(normal);
            return axis.Unit;
        }
    }
}
