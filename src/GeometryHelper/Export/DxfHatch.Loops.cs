using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Export.DxfLeaderGroups;

namespace GeometryHelper.Export
{
    public sealed partial class DxfHatch
    {
        #region The loops

        /// <summary>Gets how many loops the HATCH has, as AutoCAD's <c>NumberOfLoops</c>: the paths after its group 91.</summary>
        public int NumberOfLoops => Snap().Loops.Count;

        /// <summary>Gets what a loop is (its group 92), as AutoCAD's <c>LoopTypeAt</c>.</summary>
        /// <param name="loopIndex">The index of the loop, from nought.</param>
        /// <returns>The flags of the loop: <see cref="DxfHatchLoopTypes.Polyline"/> for a loop of a polyline.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no loop of the index.</exception>
        public DxfHatchLoopTypes LoopTypeAt(int loopIndex) => (DxfHatchLoopTypes)Whole(SnapLoop(loopIndex)[0].Value, 0);

        /// <summary>
        /// Gets a loop, as AutoCAD's <c>GetLoopAt</c>: what it is and its shape in the world, as <see cref="DxfHatchLoop"/> says.
        /// A loop of a polyline is a <see cref="GeoPolygonArc3"/> of its vertices and bulges, a polyline of two vertices joined by
        /// an arc given a vertex at the middle of the arc, as <see cref="DxfReader"/> gives a LWPOLYLINE so; a loop of edges is
        /// its lines (<see cref="GeoLine3"/>), arcs (<see cref="GeoArc3"/>, a <see cref="GeoCircle3"/> for a whole turn),
        /// ellipses (<see cref="GeoEllipse3"/> for a whole one, the <see cref="GeoSpline3"/> that is exactly the part of one) and
        /// splines (<see cref="GeoSpline3"/>), each running the way the loop runs; an arc edge whose two angles (50 and 51) are the
        /// same is a whole circle.
        /// </summary>
        /// <param name="loopIndex">The index of the loop, from nought.</param>
        /// <returns>The loop.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no loop of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an edge of the loop is no shape: an arc or an ellipse of no
        /// size, a spline with no control points or with knots that do not agree with them.</exception>
        public DxfHatchLoop GetLoopAt(int loopIndex)
        {
            List<KeyValuePair<int, string>> groups = SnapLoop(loopIndex);
            LoopData loop = LoopData.Read(groups);
            Snapshot snapshot = Snap();
            GeoVector3 normal = snapshot.Normal;
            double elevation = snapshot.Elevation;
            var flags = (DxfHatchLoopTypes)loop.Flags;

            try
            {
                if (loop.IsPolyline)
                {
                    return new DxfHatchLoop(flags, PolylineShape(loop, normal, elevation), new object[0], groups, normal, elevation);
                }

                var curves = new List<object>(loop.Edges.Count);

                foreach (Edge edge in loop.Edges)
                {
                    curves.Add(edge.Shape(normal, elevation));
                }

                return new DxfHatchLoop(flags, null, curves.AsReadOnly(), groups, normal, elevation);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("The loop " + loopIndex.ToString(CultureInfo.InvariantCulture) + " of the HATCH has an edge that is no shape: " + e.Message, e);
            }
        }

        /// <summary>
        /// Adds a loop after the others, as AutoCAD's <c>AppendLoop</c> of a <c>HatchLoop</c>: written as
        /// <see cref="InsertLoopAt(int, DxfHatchLoop)"/> writes one.
        /// </summary>
        /// <param name="hatchLoop">The loop.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the loop does not lie in the plane of the HATCH, or its edges do not
        /// run end to end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void AppendLoop(DxfHatchLoop hatchLoop) => InsertLoopAt(NumberOfLoops, hatchLoop);

        /// <summary>
        /// Adds a loop of a polyline after the others, as AutoCAD's <c>AppendLoop</c> of vertices and bulges: in the system of the
        /// normal, as given, closed, its type with <see cref="DxfHatchLoopTypes.Polyline"/>. A HATCH that was associative is so no
        /// longer, as <see cref="InsertLoopAt(int, DxfHatchLoop)"/> says.
        /// </summary>
        /// <param name="loopType">What the loop is.</param>
        /// <param name="vertexCollection">The vertices, two or more, in the system of the normal; a last on the first is kept, as AutoCAD keeps it.</param>
        /// <param name="bulgeCollection">The bulge of the segment that leaves each vertex, as many as the vertices; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vertices are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are fewer than two vertices, a coordinate or bulge is not a finite
        /// number, or the bulges are not as many as the vertices.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void AppendLoop(DxfHatchLoopTypes loopType, IReadOnlyList<GeoPoint2> vertexCollection, IReadOnlyList<double> bulgeCollection)
        {
            if (vertexCollection == null)
            {
                throw new ArgumentNullException(nameof(vertexCollection));
            }

            if (vertexCollection.Count < 2)
            {
                throw new ArgumentException("A loop of a polyline has two vertices or more.", nameof(vertexCollection));
            }

            if (bulgeCollection != null && bulgeCollection.Count != vertexCollection.Count)
            {
                throw new ArgumentException("A loop of a polyline has a bulge for each vertex.", nameof(bulgeCollection));
            }

            var vertices = new List<double[]>(vertexCollection.Count);

            for (int i = 0; i < vertexCollection.Count; i++)
            {
                GeoPoint2 p = vertexCollection[i];
                double bulge = bulgeCollection == null ? 0.0 : bulgeCollection[i];

                if (!p.IsValid || !Guard.IsFinite(bulge))
                {
                    throw new ArgumentException("A vertex and its bulge have to be finite numbers: " + p + " and " + bulge.ToString("R", CultureInfo.InvariantCulture) + " are not.", nameof(vertexCollection));
                }

                vertices.Add(new[] { p.X, p.Y, bulge });
            }

            List<KeyValuePair<int, string>> made = PolylineGroups((int)(loopType | DxfHatchLoopTypes.Polyline), vertices, true);

            if (SpliceLoops(NumberOfLoops, 0, made))
            {
                return;
            }

            Parts record = Record();
            List<List<KeyValuePair<int, string>>> loops = Loops(record);
            loops.Add(made);
            ChangeLoops(record, loops);
        }

        /// <summary>
        /// Puts a loop in before the loop of an index, as AutoCAD's <c>InsertLoopAt</c>: one read from a HATCH of the same normal
        /// and elevation written as it was read; any other from its shapes, taken into the system of the normal, a polyline as a
        /// loop of a polyline, the edges as a loop of edges, the arcs and ellipses that run clockwise about the normal flagged so,
        /// each edge to start where the one before it ends, within <see cref="Tolerance.Default"/>. A loop read is written with the
        /// count of fit points of each spline edge (97) as the file's version has it: from R2010 a spline edge with none writes
        /// <c>97 0</c>, before it none. A HATCH that was associative is so no longer, as AutoCAD's is not once its loops are changed: its group 71 nought and the
        /// handles of the objects its loops followed taken out.
        /// </summary>
        /// <param name="loopIndex">Where it goes, from nought to <see cref="NumberOfLoops"/>.</param>
        /// <param name="hatchLoop">The loop.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or above the count.</exception>
        /// <exception cref="ArgumentException">Thrown when the loop does not lie in the plane of the HATCH, within
        /// <see cref="Tolerance.Default"/>, or its edges do not run end to end.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void InsertLoopAt(int loopIndex, DxfHatchLoop hatchLoop)
        {
            if (hatchLoop == null)
            {
                throw new ArgumentNullException(nameof(hatchLoop));
            }

            Snapshot snapshot = Snap();
            int count = snapshot.Loops.Count;

            if (loopIndex < 0 || loopIndex > count)
            {
                throw new ArgumentOutOfRangeException(nameof(loopIndex), loopIndex, "A loop goes in from nought to the count of the loops, " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            List<KeyValuePair<int, string>> made = Encoded(hatchLoop, snapshot.Normal, snapshot.Elevation, Item.File.Supports(1024));

            if (SpliceLoops(loopIndex, 0, made))
            {
                return;
            }

            Parts record = Record();
            List<List<KeyValuePair<int, string>>> loops = Loops(record);
            loops.Insert(loopIndex, made);
            ChangeLoops(record, loops);
        }

        /// <summary>
        /// Takes a loop out, as AutoCAD's <c>RemoveLoopAt</c>. A HATCH that was associative is so no longer, as
        /// <see cref="InsertLoopAt(int, DxfHatchLoop)"/> says.
        /// </summary>
        /// <param name="loopIndex">The index of the loop, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no loop of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void RemoveLoopAt(int loopIndex)
        {
            SnapLoop(loopIndex);

            if (SpliceLoops(loopIndex, 1, null))
            {
                return;
            }

            Parts record = Record();
            List<List<KeyValuePair<int, string>>> loops = Loops(record);
            LoopAt(record, loopIndex);
            loops.RemoveAt(loopIndex);
            ChangeLoops(record, loops);
        }

        /// <summary>
        /// Gets or sets whether the HATCH follows the objects its loops were made of (group 71), as AutoCAD's <c>Associative</c>:
        /// their handles are in its loops (groups 330), and AutoCAD makes its loops anew when they change.
        /// </summary>
        /// <value>
        /// Set true, group 71 is written 1; set false, it is written nought and the handles of the objects are taken out of the
        /// loops, each loop's count of them (97) nought, as AutoCAD's <c>RemoveAssociatedObjectIds</c> takes them out, and the HATCH
        /// taken out of the reactors of the objects (102 <c>{ACAD_REACTORS</c>), as AutoCAD takes it out.
        /// </value>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public bool Associative
        {
            get => Whole(Record().Text(71), 0) != 0;

            set
            {
                if (value == Associative)
                {
                    return;
                }

                Parts record = Record();

                if (value)
                {
                    record.Set(71, "1");
                    Write(record);
                    return;
                }

                IReadOnlyList<DxfEntity> followed = GetAssociatedObjects();
                Unlinked(record);
                Write(record);
                Unreact(followed);
            }
        }

        /// <summary>
        /// Gets the objects the loops follow, as AutoCAD's <c>GetAssociatedObjectIds</c> gives their ids: the entities of the
        /// handles of every loop (groups 330), each once, in the order the loops name them; a handle the drawing has no entity of
        /// is passed over. None for a HATCH in no drawing.
        /// </summary>
        /// <returns>The entities.</returns>
        public IReadOnlyList<DxfEntity> GetAssociatedObjects()
        {
            var handles = new List<string>();

            foreach (List<KeyValuePair<int, string>> loop in Snap().Loops)
            {
                handles.AddRange(LoopData.Read(loop).Sources);
            }

            return Entities(handles);
        }

        /// <summary>
        /// Gets the objects one loop follows, as AutoCAD's <c>GetAssociatedObjectIdsAt</c> gives their ids, as
        /// <see cref="GetAssociatedObjects"/> gives them.
        /// </summary>
        /// <param name="loopIndex">The index of the loop, from nought.</param>
        /// <returns>The entities.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no loop of the index.</exception>
        public IReadOnlyList<DxfEntity> GetAssociatedObjectsAt(int loopIndex) => Entities(LoopData.Read(SnapLoop(loopIndex)).Sources);

        /// <summary>
        /// Takes the objects the loops follow away, as AutoCAD's <c>RemoveAssociatedObjectIds</c>: the handles of every loop out,
        /// each loop's count of them nought, and the HATCH no longer associative, as <see cref="Associative"/> set false says.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void RemoveAssociatedObjects()
        {
            IReadOnlyList<DxfEntity> followed = GetAssociatedObjects();
            Parts record = Record();
            Unlinked(record);
            Write(record);
            Unreact(followed);
        }

        /// <summary>
        /// Gets the area the HATCH fills, as AutoCAD's <c>Area</c>: that of each loop, worked out exactly on its lines, arcs,
        /// ellipses and splines (a spline by Gauss's rule, sixteen points to each span of its knots), added or taken off by how
        /// deep the loop lies among the others and the <see cref="HatchStyle"/>: in the normal style every other one, from the
        /// outermost in; in the outer style the outermost less those just inside them; ignoring islands, the outermost alone. A
        /// square 30 by 20 round a circle of radius 5 fills 521.46 in the normal style.
        /// </summary>
        /// <remarks>
        /// A loop lies within another where more of the points it is drawn through, and the middles between them, lie inside the
        /// other than outside it, by the even-odd rule, those within <see cref="Tolerance.Default"/> of the other's edge not
        /// counted; where all of them are on that edge, a point inside the loop decides. So an island that touches the outer loop,
        /// a triangle in a corner of a square or a notch on its side, is taken off: the square 10 by 10 with the triangle (10, 5),
        /// (5, 10), (5, 5) fills 87.5. Loops that cross each other are not cut where they cross: each is within the other or not
        /// as most of its points are, so two squares 10 by 10 that overlap by a quarter fill 200, where AutoCAD fills 150. Two loops
        /// each within the other, as one loop given twice, count the later within the earlier, as the even-odd rule counts them:
        /// a square 10 by 10 given twice fills nothing, and the same round an island given twice fills 100.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when a loop has an edge that is no shape, as <see cref="GetLoopAt"/> says.</exception>
        public double Area
        {
            get
            {
                Snapshot snapshot = Snap();
                List<List<KeyValuePair<int, string>>> loops = snapshot.Loops;

                if (snapshot.Areas == null)
                {
                    var areas = new double[loops.Count];
                    var outlines = new Outline[loops.Count];

                    for (int i = 0; i < loops.Count; i++)
                    {
                        LoopData loop = LoopData.Read(loops[i]);

                        try
                        {
                            areas[i] = Math.Abs(loop.SignedArea());
                            outlines[i] = new Outline(loop.Outline());
                        }
                        catch (ArgumentException e)
                        {
                            throw new InvalidOperationException("The loop " + i.ToString(CultureInfo.InvariantCulture) + " of the HATCH has an edge that is no shape: " + e.Message, e);
                        }
                    }

                    snapshot.Outlines = outlines;
                    snapshot.Areas = areas;
                }

                DxfHatchStyle style = HatchStyle;
                double tolerance = Tolerance.Default.EqualPoint;
                double area = 0.0;

                for (int i = 0; i < loops.Count; i++)
                {
                    int depth = 0;

                    for (int j = 0; j < loops.Count; j++)
                    {
                        // Two loops each within the other, as one loop given twice, count once: the later within the earlier.
                        if (j != i && Within(snapshot.Outlines[i], snapshot.Outlines[j], tolerance)
                            && (j < i || !Within(snapshot.Outlines[j], snapshot.Outlines[i], tolerance)))
                        {
                            depth++;
                        }
                    }

                    if (style == DxfHatchStyle.Normal)
                    {
                        area += depth % 2 == 0 ? snapshot.Areas[i] : -snapshot.Areas[i];
                    }
                    else if (depth == 0)
                    {
                        area += snapshot.Areas[i];
                    }
                    else if (depth == 1 && style == DxfHatchStyle.Outer)
                    {
                        area -= snapshot.Areas[i];
                    }
                }

                return area;
            }
        }

        /// <summary>The loops of the HATCH as last read, with what is worked out of them, kept while its record is the one read.</summary>
        private Snapshot _snapshot;

        /// <summary>What is read of the record once for many loops: the loops, the elevation, the normal, and the outlines and areas of the loops when asked for.</summary>
        private sealed class Snapshot
        {
            internal byte[] Bytes { get; set; }

            internal int Start { get; set; }

            internal int End { get; set; }

            internal List<List<KeyValuePair<int, string>>> Loops { get; set; }

            internal double Elevation { get; set; }

            internal GeoVector3 Normal { get; set; }

            internal bool Associative { get; set; }

            /// <summary>Where each loop begins in the bytes of the record, then where the last ends; null where they are not one run of bytes.</summary>
            internal List<int> Starts { get; set; }

            internal int CountFrom { get; set; }

            internal int CountTo { get; set; }

            internal Outline[] Outlines { get; set; }

            internal double[] Areas { get; set; }
        }

        /// <summary>
        /// The loops, elevation and normal of the record, read once while it is the record they were read from, so that walking a thousand
        /// loops reads it once: not to be changed.
        /// </summary>
        private Snapshot Snap()
        {
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);
            Snapshot snapshot = _snapshot;

            if (snapshot != null && ReferenceEquals(record.Bytes, snapshot.Bytes) && record.Start == snapshot.Start && record.End == snapshot.End)
            {
                return snapshot;
            }

            var places = new List<int>();
            IReadOnlyList<DxfTypedValue> own = Item.File.OwnGroupsOf(Item, 0, places);
            List<KeyValuePair<int, string>> run = Run(own);
            var parts = new Parts(run);
            snapshot = new Snapshot
            {
                Bytes = record.Bytes, Start = record.Start, End = record.End, Loops = Loops(parts), Elevation = Num(parts.Text(30), 0.0),
                Normal = GetNormal(), Associative = Whole(parts.Text(71), 0) != 0,
            };
            Placed(snapshot, run, own.Count - run.Count, places, parts.Part(Kind.Paths));
            _snapshot = snapshot;
            return snapshot;
        }

        /// <summary>
        /// Keeps where the count of the loops (91) and each loop stand in the bytes of the record, for <see cref="SpliceLoops"/>:
        /// only where they are one run of bytes, each loop from its 92, as the loops were read.
        /// </summary>
        private static void Placed(Snapshot snapshot, List<KeyValuePair<int, string>> run, int skip, List<int> places, List<KeyValuePair<int, string>> paths)
        {
            int head = run.FindIndex(g => g.Key == 91);

            if (paths == null || head < 0)
            {
                return;
            }

            int last = head + paths.Count - 1;
            var starts = new List<int>();

            for (int i = head; i <= last; i++)
            {
                if (i < last && places[2 * (i + skip) + 1] != places[2 * (i + 1 + skip)])
                {
                    return;
                }

                if (i > head && run[i].Key == 92)
                {
                    starts.Add(places[2 * (i + skip)]);
                }
                else if (i == head + 1)
                {
                    return;
                }
            }

            if (starts.Count != snapshot.Loops.Count)
            {
                return;
            }

            starts.Add(places[2 * (last + skip) + 1]);
            snapshot.Starts = starts;
            snapshot.CountFrom = places[2 * (head + skip)];
            snapshot.CountTo = places[2 * (head + skip) + 1];
        }

        /// <summary>
        /// Puts a loop in before the loop of an index, or takes out loops from it, by splicing the bytes of the record: those of
        /// every other loop and group kept, the count of the loops (91) written anew, so that a thousand loops appended one at a
        /// time cost a copy of the bytes each, not a reading and writing of every group. False, with nothing written, where the
        /// HATCH is associative, whose loops all change, or its loops are not one run of bytes.
        /// </summary>
        private bool SpliceLoops(int index, int remove, List<KeyValuePair<int, string>> made)
        {
            Snapshot snapshot = Snap();
            List<int> starts = snapshot.Starts;

            if (starts == null || snapshot.Associative)
            {
                return false;
            }

            int from = starts[index];
            int to = starts[index + remove];
            int expected = snapshot.End - snapshot.Start - (to - from);
            int written = Item.File.SpliceRecord(Item, 0, from, to, made ?? new List<KeyValuePair<int, string>>(), true);
            expected += written;
            starts.RemoveRange(index + 1, remove);
            snapshot.Loops.RemoveRange(index, remove);

            if (made != null)
            {
                starts.Insert(index + 1, from + written);
                snapshot.Loops.Insert(index, made);
            }

            for (int i = made != null ? index + 2 : index + 1; i < starts.Count; i++)
            {
                starts[i] += written - (to - from);
            }

            // The count stands before the loops: written anew, it moves them all.
            int length = Item.File.SpliceRecord(Item, 0, snapshot.CountFrom, snapshot.CountTo, new[] { G(91, snapshot.Loops.Count) }, false);
            int by = length - (snapshot.CountTo - snapshot.CountFrom);

            for (int i = 0; i < starts.Count; i++)
            {
                starts[i] += by;
            }

            expected += by;
            snapshot.CountTo = snapshot.CountFrom + length;
            snapshot.Outlines = null;
            snapshot.Areas = null;

            // A record that came out other than spliced is read again.
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);
            snapshot.Bytes = record.Bytes;
            snapshot.Start = record.Start;
            snapshot.End = record.End;

            if (record.End - record.Start != expected)
            {
                _snapshot = null;
            }

            return true;
        }

        /// <summary>
        /// Writes a HATCH copied from a drawing of another version in the form of the drawing it is added to, as AutoCAD's
        /// SAVEAS writes one: each spline edge's count of fit points (97) as <see cref="InsertLoopAt"/> says, and before R2004,
        /// which has no gradient, the gradient taken out, as SAVEAS to such a version drops it, the HATCH left filled solid.
        /// Nothing where the drawing cannot hold a HATCH, which refuses it.
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            DxfFile file = owner.File;

            if (file == null || !file.Supports(1015))
            {
                return;
            }

            Parts record = Record();
            List<List<KeyValuePair<int, string>>> loops = Loops(record);
            bool fitCount = file.Supports(1024);
            bool changed = false;

            for (int i = 0; i < loops.Count; i++)
            {
                int body = LoopData.Body(loops[i]);
                List<KeyValuePair<int, string>> made = FitCounts(loops[i].GetRange(0, body), fitCount);
                made.AddRange(loops[i].GetRange(body, loops[i].Count - body));

                if (!SameGroups(made, loops[i]))
                {
                    loops[i] = made;
                    changed = true;
                }
            }

            if (changed)
            {
                record.SetPart(Kind.Paths, JoinLoops(loops));
            }

            if (!file.Supports(1018) && record.Part(Kind.Gradient) != null)
            {
                record.SetPart(Kind.Gradient, null);
                changed = true;
            }

            if (changed)
            {
                Write(record);
            }
        }

        /// <summary>Whether two runs of groups are the same, code for code and text for text.</summary>
        private static bool SameGroups(List<KeyValuePair<int, string>> a, List<KeyValuePair<int, string>> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Key != b[i].Key || !string.Equals(a[i].Value, b[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The groups of the loop of an index, as last read: not to be changed.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no loop of the index.</exception>
        private List<KeyValuePair<int, string>> SnapLoop(int index)
        {
            List<List<KeyValuePair<int, string>>> loops = Snap().Loops;

            if (index < 0 || index >= loops.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The HATCH has " + loops.Count.ToString(CultureInfo.InvariantCulture) + " loops, from nought.");
            }

            return loops[index];
        }

        /// <summary>The loops, each its groups from its 92 on.</summary>
        private static List<List<KeyValuePair<int, string>>> Loops(Parts record)
        {
            var loops = new List<List<KeyValuePair<int, string>>>();
            List<KeyValuePair<int, string>> paths = record.Part(Kind.Paths);

            if (paths == null)
            {
                return loops;
            }

            for (int i = 1; i < paths.Count; i++)
            {
                if (paths[i].Key == 92 || loops.Count == 0)
                {
                    loops.Add(new List<KeyValuePair<int, string>>());
                }

                loops[loops.Count - 1].Add(paths[i]);
            }

            return loops;
        }

        /// <summary>The groups of the loops: their count (91), then each.</summary>
        private static List<KeyValuePair<int, string>> JoinLoops(List<List<KeyValuePair<int, string>>> loops)
        {
            var groups = new List<KeyValuePair<int, string>> { G(91, loops.Count) };

            foreach (List<KeyValuePair<int, string>> loop in loops)
            {
                groups.AddRange(loop);
            }

            return groups;
        }

        /// <summary>The groups of the loop of an index.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no loop of the index.</exception>
        private static List<KeyValuePair<int, string>> LoopAt(Parts record, int index)
        {
            List<List<KeyValuePair<int, string>>> loops = Loops(record);

            if (index < 0 || index >= loops.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The HATCH has " + loops.Count.ToString(CultureInfo.InvariantCulture) + " loops, from nought.");
            }

            return loops[index];
        }

        /// <summary>Writes the loops anew, the association taken away where the HATCH had one.</summary>
        private void ChangeLoops(Parts record, List<List<KeyValuePair<int, string>>> loops)
        {
            record.SetPart(Kind.Paths, JoinLoops(loops));
            IReadOnlyList<DxfEntity> followed = null;

            if (Whole(record.Text(71), 0) != 0)
            {
                followed = GetAssociatedObjects();
                Unlinked(record);
            }

            Write(record);

            if (followed != null)
            {
                Unreact(followed);
            }
        }

        /// <summary>Takes the HATCH out of the reactors of the entities it followed (102 <c>{ACAD_REACTORS</c>), as AutoCAD takes it out once it follows them no more.</summary>
        private void Unreact(IReadOnlyList<DxfEntity> followed)
        {
            foreach (DxfEntity entity in followed)
            {
                Item.File.SetReactor(entity.Item, Handle, false);
            }
        }

        /// <summary>Takes the association away: 71 nought, and each loop's handles out with its count of them nought.</summary>
        private static void Unlinked(Parts record)
        {
            if (record.Text(71) != null)
            {
                record.Set(71, "0");
            }

            List<List<KeyValuePair<int, string>>> loops = Loops(record);
            bool changed = false;

            for (int i = 0; i < loops.Count; i++)
            {
                int body = LoopData.Body(loops[i]);

                if (body < loops[i].Count && (loops[i].Count - body != 1 || Whole(loops[i][body].Value, 0) != 0))
                {
                    loops[i] = loops[i].GetRange(0, body);
                    loops[i].Add(G(97, "0"));
                    changed = true;
                }
            }

            if (changed)
            {
                record.SetPart(Kind.Paths, JoinLoops(loops));
            }
        }

        /// <summary>The entities of handles, each once, those the drawing has none of passed over.</summary>
        private IReadOnlyList<DxfEntity> Entities(List<string> handles)
        {
            var found = new List<DxfEntity>();
            DxfDatabase database = IsResident ? Item.File.Database : null;

            if (database == null)
            {
                return found.AsReadOnly();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in handles)
            {
                if (!seen.Add(handle.Trim()))
                {
                    continue;
                }

                foreach (DxfBlockTableRecord block in database.BlockTable)
                {
                    DxfEntity entity = block.EntityByHandle(handle);

                    if (entity != null)
                    {
                        found.Add(entity);
                        break;
                    }
                }
            }

            return found.AsReadOnly();
        }

        /// <summary>The points a loop is drawn through in its plane, with the middles between them and its box.</summary>
        private sealed class Outline
        {
            internal Outline(List<GeoPoint2> points)
            {
                Points = points;
                Samples = new List<GeoPoint2>(2 * points.Count);
                MinX = MinY = double.PositiveInfinity;
                MaxX = MaxY = double.NegativeInfinity;

                for (int i = 0; i < points.Count; i++)
                {
                    GeoPoint2 a = points[i];
                    GeoPoint2 b = points[(i + 1) % points.Count];
                    Samples.Add(a);
                    Samples.Add(new GeoPoint2((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5));
                    MinX = Math.Min(MinX, a.X);
                    MinY = Math.Min(MinY, a.Y);
                    MaxX = Math.Max(MaxX, a.X);
                    MaxY = Math.Max(MaxY, a.Y);
                }
            }

            internal List<GeoPoint2> Points { get; }

            internal List<GeoPoint2> Samples { get; }

            internal double MinX { get; }

            internal double MinY { get; }

            internal double MaxX { get; }

            internal double MaxY { get; }
        }

        /// <summary>
        /// Whether one loop lies within another: its box within the other's, and more of its points and middles inside the other
        /// than outside it, those on the other's edge within the tolerance not counted; where every one is on that edge, a point
        /// inside the loop decides.
        /// </summary>
        private static bool Within(Outline inner, Outline outer, double tolerance)
        {
            if (outer.Points.Count < 3 || inner.Points.Count == 0 || inner.MinX < outer.MinX - tolerance || inner.MinY < outer.MinY - tolerance
                || inner.MaxX > outer.MaxX + tolerance || inner.MaxY > outer.MaxY + tolerance)
            {
                return false;
            }

            int inside = 0;
            int outside = 0;

            foreach (GeoPoint2 p in inner.Samples)
            {
                if (OnEdge(p, outer.Points, tolerance))
                {
                    continue;
                }

                if (EvenOdd(p, outer.Points))
                {
                    inside++;
                }
                else
                {
                    outside++;
                }
            }

            if (inside + outside > 0)
            {
                return inside > outside;
            }

            GeoPoint2? point = InteriorPoint(inner);
            return point.HasValue && !OnEdge(point.Value, outer.Points, tolerance) && EvenOdd(point.Value, outer.Points);
        }

        /// <summary>Whether a point lies inside a polygon by the even-odd rule.</summary>
        private static bool EvenOdd(GeoPoint2 p, List<GeoPoint2> polygon)
        {
            bool odd = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                GeoPoint2 a = polygon[i];
                GeoPoint2 b = polygon[j];

                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    odd = !odd;
                }
            }

            return odd;
        }

        /// <summary>Whether a point lies within the tolerance of an edge of a polygon.</summary>
        private static bool OnEdge(GeoPoint2 p, List<GeoPoint2> polygon, double tolerance)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                GeoPoint2 a = polygon[i];
                GeoPoint2 b = polygon[(i + 1) % polygon.Count];
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double length = dx * dx + dy * dy;
                double t = length > 0.0 ? Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length)) : 0.0;
                double ex = a.X + t * dx - p.X;
                double ey = a.Y + t * dy - p.Y;

                if (ex * ex + ey * ey <= tolerance * tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A point inside a loop: the middle of the first span inside it along a level line across its box; null where there is none.</summary>
        private static GeoPoint2? InteriorPoint(Outline outline)
        {
            double y = outline.MinY + (outline.MaxY - outline.MinY) * 0.5123;
            var crossings = new List<double>();
            List<GeoPoint2> points = outline.Points;

            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                GeoPoint2 a = points[i];
                GeoPoint2 b = points[j];

                if ((a.Y > y) != (b.Y > y))
                {
                    crossings.Add((b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X);
                }
            }

            crossings.Sort();
            return crossings.Count < 2 ? (GeoPoint2?)null : new GeoPoint2((crossings[0] + crossings[1]) * 0.5, y);
        }

        /// <summary>
        /// The groups of a loop of edges read from a file, each spline edge's count of fit points (97) as a file of the version
        /// writes it: <c>97 0</c> after its control points where it has none from R2010, none of it before; one with fit points kept.
        /// A loop of a polyline as it is.
        /// </summary>
        private static List<KeyValuePair<int, string>> FitCounts(List<KeyValuePair<int, string>> groups, bool fitCount)
        {
            if ((Whole(groups[0].Value, 0) & 2) != 0)
            {
                return groups;
            }

            var made = new List<KeyValuePair<int, string>>(groups.Count + 4);
            int i = 0;

            while (i < groups.Count)
            {
                if (groups[i].Key != 72 || Whole(groups[i].Value, 0) != 4)
                {
                    made.Add(groups[i++]);
                    continue;
                }

                int end = i + 1;

                while (end < groups.Count && groups[end].Key != 72)
                {
                    end++;
                }

                List<KeyValuePair<int, string>> edge = groups.GetRange(i, end - i);
                bool fitted = edge.Exists(g => g.Key == 11);

                if (!fitted)
                {
                    edge.RemoveAll(g => g.Key == 97);

                    if (fitCount)
                    {
                        int tangent = edge.FindIndex(g => g.Key == 12 || g.Key == 13);
                        edge.Insert(tangent < 0 ? edge.Count : tangent, G(97, "0"));
                    }
                }

                made.AddRange(edge);
                i = end;
            }

            return made;
        }

        /// <summary>Refuses the groups of edges that do not run end to end, each starting within the tolerance of where the one before it ends.</summary>
        /// <exception cref="ArgumentException">Thrown when an edge starts away from the end of the one before it.</exception>
        private static void Chained(List<KeyValuePair<int, string>> groups)
        {
            var edges = new List<Edge>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Key != 72)
                {
                    continue;
                }

                int end = i + 1;

                while (end < groups.Count && groups[end].Key != 72)
                {
                    end++;
                }

                edges.Add(Edge.Read(groups.GetRange(i, end - i)));
            }

            double tolerance = Tolerance.Default.EqualPoint;

            for (int i = 0; i < edges.Count; i++)
            {
                edges[i].Ends(out GeoPoint2 _, out GeoPoint2 last);
                edges[(i + 1) % edges.Count].Ends(out GeoPoint2 next, out GeoPoint2 _);

                if (Math.Sqrt((last.X - next.X) * (last.X - next.X) + (last.Y - next.Y) * (last.Y - next.Y)) > tolerance)
                {
                    throw new ArgumentException("The edges of a loop run end to end: the edge " + i.ToString(CultureInfo.InvariantCulture) + " ends at " + last + " in the plane of the HATCH, and the next starts at " + next + ".", "hatchLoop");
                }
            }
        }

        #endregion

        #region A loop as the file has it

        /// <summary>The shape of a loop of a polyline in the world: a loop of its vertices and bulges, else the chain of them.</summary>
        private static object PolylineShape(LoopData loop, GeoVector3 normal, double elevation)
        {
            Tolerance tolerance = Tolerance.Default;
            var points = new List<GeoPoint2>();
            var bulges = new List<double>();

            foreach (double[] v in loop.Vertices)
            {
                points.Add(new GeoPoint2(v[0], v[1]));
                bulges.Add(v[2]);
            }

            if (loop.Closed && points.Count == 2)
            {
                int arc = bulges[0] != 0.0 ? 0 : bulges[1] != 0.0 ? 1 : -1;

                if (arc >= 0)
                {
                    GeoPoint2 a = points[arc];
                    GeoPoint2 b = points[1 - arc];
                    double half = bulges[arc] / (1.0 + Math.Sqrt(1.0 + bulges[arc] * bulges[arc]));
                    points.Insert(arc + 1, Middle(a, b, bulges[arc]));
                    bulges[arc] = half;
                    bulges.Insert(arc + 1, half);
                }
            }

            var world = new List<GeoPoint3>(points.Count);
            var normals = new List<GeoVector3>(points.Count);

            foreach (GeoPoint2 p in points)
            {
                world.Add(OcsToWorld(normal, new GeoPoint3(p.X, p.Y, elevation)));
                normals.Add(normal);
            }

            if (loop.Closed)
            {
                try
                {
                    return new GeoPolygonArc3(world, bulges, normals, tolerance);
                }
                catch (ArgumentException)
                {
                    // A loop that encloses nothing the library takes is given as the chain round it.
                }

                world.Add(world[0]);
                bulges.Add(0.0);
                normals.Add(normal);
            }

            bulges[bulges.Count - 1] = 0.0;
            return new GeoPolylineArc3(world, bulges, normals, tolerance);
        }

        /// <summary>The middle of the arc of a bulge from one point to another, in their plane.</summary>
        private static GeoPoint2 Middle(GeoPoint2 a, GeoPoint2 b, double bulge)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double sagitta = bulge * 0.5;
            return new GeoPoint2((a.X + b.X) * 0.5 + sagitta * dy, (a.Y + b.Y) * 0.5 - sagitta * dx);
        }

        /// <summary>
        /// The groups of a loop: those it was read with where it is written into a HATCH of the same normal and elevation, to the
        /// bit, its handles left out; else those of its shapes, in the system of the normal.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a shape does not lie in the plane of the HATCH.</exception>
        private static List<KeyValuePair<int, string>> Encoded(DxfHatchLoop loop, GeoVector3 normal, double elevation, bool fitCount)
        {
            if (loop.Groups != null && Same(loop.Normal.X, normal.X) && Same(loop.Normal.Y, normal.Y) && Same(loop.Normal.Z, normal.Z) && Same(loop.Elevation, elevation))
            {
                List<KeyValuePair<int, string>> kept = FitCounts(loop.Groups.GetRange(0, LoopData.Body(loop.Groups)), fitCount);
                kept.Add(G(97, "0"));
                return kept;
            }

            var plane = new LoopPlane(normal, elevation);

            switch (loop.Polyline)
            {
                case GeoPolygonArc3 polygon:
                    return PolylineGroups((int)loop.LoopType, plane.Vertices(polygon.Vertices, polygon.Bulges, polygon.Normals), true);

                case GeoPolylineArc3 chain:
                    List<double[]> vertices = plane.Vertices(chain.Vertices, chain.Bulges, chain.Normals);
                    bool closed = vertices.Count > 2 && chain.Vertices[0].IsEqualTo(chain.Vertices[chain.Vertices.Count - 1], Tolerance.Default);

                    if (closed)
                    {
                        vertices.RemoveAt(vertices.Count - 1);
                    }

                    return PolylineGroups((int)loop.LoopType, vertices, closed);
            }

            var edges = new List<KeyValuePair<int, string>>();
            int count = 0;

            foreach (object curve in loop.Curves)
            {
                count += plane.Edges(curve, edges, fitCount);
            }

            Chained(edges);
            var groups = new List<KeyValuePair<int, string>> { G(92, (int)loop.LoopType), G(93, count) };
            groups.AddRange(edges);
            groups.Add(G(97, "0"));
            return groups;
        }

        /// <summary>The groups of a loop of a polyline, as AutoCAD writes one: its flags, whether it has bulges, whether it is closed, its count, each vertex with its bulge where it has any, and no handles.</summary>
        private static List<KeyValuePair<int, string>> PolylineGroups(int flags, List<double[]> vertices, bool closed)
        {
            bool bulged = vertices.Exists(v => v[2] != 0.0);
            var groups = new List<KeyValuePair<int, string>> { G(92, flags | 2), G(72, bulged ? "1" : "0"), G(73, closed ? "1" : "0"), G(93, vertices.Count) };

            foreach (double[] v in vertices)
            {
                groups.Add(G(10, v[0]));
                groups.Add(G(20, v[1]));

                if (bulged)
                {
                    groups.Add(G(42, v[2]));
                }
            }

            groups.Add(G(97, "0"));
            return groups;
        }

        /// <summary>
        /// A loop moved by a transformation seen from the system of the HATCH, as ezdxf moves one: each vertex, end, centre and
        /// control point taken through it at the elevation, each radius and axis scaled and turned, the angles of the arcs turned,
        /// those of the ellipses measured anew from their turned axes; the handles kept.
        /// </summary>
        private static List<KeyValuePair<int, string>> MovedLoop(List<KeyValuePair<int, string>> groups, DxfOcsTransform ocs, double elevation, bool conformal)
        {
            var moved = new List<KeyValuePair<int, string>>(groups.Count);
            LoopData loop = LoopData.Read(groups);
            int body = LoopData.Body(groups);

            if (loop.IsPolyline)
            {
                foreach (KeyValuePair<int, string> group in groups.GetRange(0, body))
                {
                    moved.Add(group);
                }

                for (int i = 0; i < moved.Count; i++)
                {
                    if (moved[i].Key == 10 && i + 1 < moved.Count && moved[i + 1].Key == 20)
                    {
                        GeoPoint3 p = ocs.TransformVertex(new GeoPoint3(Num(moved[i].Value, 0.0), Num(moved[i + 1].Value, 0.0), elevation));
                        moved[i] = G(10, p.X);
                        moved[i + 1] = G(20, p.Y);
                    }
                }
            }
            else
            {
                int first = groups.FindIndex(g => g.Key == 72);
                moved.AddRange(groups.GetRange(0, first < 0 || first > body ? body : first));

                foreach (Edge edge in loop.Edges)
                {
                    moved.AddRange(edge.Moved(ocs, elevation, conformal));
                }
            }

            moved.AddRange(groups.GetRange(body, groups.Count - body));
            return moved;
        }

        /// <summary>A loop as the file has it, in the system of the HATCH's normal.</summary>
        private sealed class LoopData
        {
            internal int Flags { get; private set; }

            internal bool IsPolyline => (Flags & 2) != 0;

            internal bool Closed { get; private set; } = true;

            /// <summary>The vertices of a loop of a polyline: x, y and the bulge of the segment that leaves it.</summary>
            internal List<double[]> Vertices { get; } = new List<double[]>();

            internal List<Edge> Edges { get; } = new List<Edge>();

            /// <summary>The handles of the objects the loop follows (330).</summary>
            internal List<string> Sources { get; } = new List<string>();

            /// <summary>Where the handles of the objects the loop follows begin, its 97 at the end with the 330s after it, as ezdxf takes them; the count of its groups where it has none.</summary>
            internal static int Body(List<KeyValuePair<int, string>> groups)
            {
                int i = groups.Count;

                while (i > 1 && groups[i - 1].Key == 330)
                {
                    i--;
                }

                return i > 1 && groups[i - 1].Key == 97 ? i - 1 : groups.Count;
            }

            internal static LoopData Read(List<KeyValuePair<int, string>> groups)
            {
                var loop = new LoopData { Flags = Whole(groups[0].Value, 0) };
                int body = Body(groups);

                for (int i = body + 1; i < groups.Count; i++)
                {
                    loop.Sources.Add(groups[i].Value.Trim());
                }

                if (loop.IsPolyline)
                {
                    double[] vertex = null;

                    for (int i = 1; i < body; i++)
                    {
                        KeyValuePair<int, string> group = groups[i];

                        switch (group.Key)
                        {
                            case 73:
                                loop.Closed = Whole(group.Value, 1) != 0;
                                break;
                            case 10:
                                vertex = new[] { Num(group.Value, 0.0), 0.0, 0.0 };
                                loop.Vertices.Add(vertex);
                                break;
                            case 20:
                                if (vertex != null)
                                {
                                    vertex[1] = Num(group.Value, 0.0);
                                }

                                break;
                            case 42:
                                if (vertex != null)
                                {
                                    vertex[2] = Num(group.Value, 0.0);
                                }

                                break;
                        }
                    }

                    return loop;
                }

                List<KeyValuePair<int, string>> edge = null;

                for (int i = 1; i < body; i++)
                {
                    if (groups[i].Key == 72)
                    {
                        if (edge != null)
                        {
                            loop.Add(edge);
                        }

                        edge = new List<KeyValuePair<int, string>>();
                    }

                    edge?.Add(groups[i]);
                }

                if (edge != null)
                {
                    loop.Add(edge);
                }

                return loop;
            }

            /// <summary>The area the loop closes, above nought counter-clockwise about the normal, by Green's theorem on its own pieces.</summary>
            internal double SignedArea()
            {
                double area = 0.0;

                if (IsPolyline)
                {
                    int n = Vertices.Count;

                    for (int i = 0; i < n && n > 1; i++)
                    {
                        if (!Closed && i == n - 1)
                        {
                            break;
                        }

                        double[] a = Vertices[i];
                        double[] b = Vertices[(i + 1) % n];
                        area += 0.5 * (a[0] * b[1] - b[0] * a[1]) + Segment(a, b);
                    }

                    if (!Closed && n > 1)
                    {
                        double[] a = Vertices[n - 1];
                        double[] b = Vertices[0];
                        area += 0.5 * (a[0] * b[1] - b[0] * a[1]);
                    }

                    return area;
                }

                foreach (Edge edge in Edges)
                {
                    area += edge.Green();
                }

                return area;
            }

            /// <summary>Points the loop is drawn through, in its plane, to tell what lies within it.</summary>
            internal List<GeoPoint2> Outline()
            {
                var points = new List<GeoPoint2>();

                if (IsPolyline)
                {
                    int n = Vertices.Count;

                    for (int i = 0; i < n; i++)
                    {
                        double[] a = Vertices[i];
                        double[] b = Vertices[(i + 1) % n];
                        points.Add(new GeoPoint2(a[0], a[1]));

                        if (a[2] != 0.0 && (Closed || i < n - 1))
                        {
                            for (int k = 1; k < 16; k++)
                            {
                                points.Add(OnBulge(a, b, k / 16.0));
                            }
                        }
                    }

                    return points;
                }

                foreach (Edge edge in Edges)
                {
                    edge.Sample(points);
                }

                return points;
            }

            private void Add(List<KeyValuePair<int, string>> groups)
            {
                Edge edge = Edge.Read(groups);

                if (edge != null)
                {
                    Edges.Add(edge);
                }
            }

            /// <summary>The area between the chord of a bulge and its arc, above nought where it bulges to the right of its way.</summary>
            private static double Segment(double[] a, double[] b)
            {
                double bulge = a[2];

                if (bulge == 0.0)
                {
                    return 0.0;
                }

                double chord = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
                double angle = 4.0 * Math.Atan(bulge);
                double sine = Math.Sin(angle * 0.5);

                if (chord == 0.0 || sine == 0.0)
                {
                    return 0.0;
                }

                double radius = chord / (2.0 * sine);
                return 0.5 * radius * radius * (angle - Math.Sin(angle));
            }

            /// <summary>A point of the arc of a bulge, a share of the way along it.</summary>
            private static GeoPoint2 OnBulge(double[] a, double[] b, double share)
            {
                double angle = 4.0 * Math.Atan(a[2]);
                double dx = b[0] - a[0];
                double dy = b[1] - a[1];
                double chord = Math.Sqrt(dx * dx + dy * dy);
                double sine = Math.Sin(angle * 0.5);

                if (chord == 0.0 || sine == 0.0)
                {
                    return new GeoPoint2(a[0] + dx * share, a[1] + dy * share);
                }

                double radius = chord / (2.0 * sine);
                double toCentre = radius * Math.Cos(angle * 0.5);
                double cx = (a[0] + b[0]) * 0.5 - dy / chord * toCentre;
                double cy = (a[1] + b[1]) * 0.5 + dx / chord * toCentre;
                double start = Math.Atan2(a[1] - cy, a[0] - cx);
                double at = start + angle * share;
                double r = Math.Abs(radius);
                return new GeoPoint2(cx + r * Math.Cos(at), cy + r * Math.Sin(at));
            }
        }

        #endregion
    }
}
