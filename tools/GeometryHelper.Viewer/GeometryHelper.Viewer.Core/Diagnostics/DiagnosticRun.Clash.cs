using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;

namespace GeometryHelper.Viewer
{
    internal sealed partial class DiagnosticRun
    {
        /// <summary>
        /// The most faces of the shared volumes drawn for one pair.
        /// </summary>
        private const int MaxFacesDrawnPerPair = 400;

        /// <summary>
        /// A body of the scene as the clash check takes it: the library solid, prepared once, and the item it came from.
        /// </summary>
        private sealed class ClashBody
        {
            internal int Item;
            internal string Name;
            internal GeoSolid3 Solid;
            internal GeoPreparedSolid3 Prepared;
        }

        private void ClashCheck(List<int> targets)
        {
            // The clash is the library's reading alone: the viewer has none of its own to put in its place.
            if (!_options.UseLibrary)
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, targets.Count > 0 ? targets[0] : -1, null, "the clash check is the library's alone (Clash3.Find) and is not made with the library left out", "viewer", default(Vec3), 0.0, 0.0);
                return;
            }

            Tolerance tolerance = _scene.Tolerance;
            bool single = targets.Count == 1;
            int targetItem = single ? targets[0] : -1;

            // One item selected is checked against the rest of the scene; two or more, or none, against each other. Against the rest, only the items whose
            // boxes come within the tolerance of the one selected are rebuilt: the others cannot clash with it.
            IEnumerable<int> pool = single ? Enumerable.Range(0, _scene.Items.Count) : targets;
            Bounds3 targetBox = single && targetItem < _primitives.Items.Count ? _primitives.Items[targetItem].Bounds : default(Bounds3);
            var bodies = new List<ClashBody>();
            int notBodies = 0;

            foreach (int index in pool)
            {
                _cancel.ThrowIfCancellationRequested();
                SceneItem item = _scene.Items[index];
                string name = item.Name ?? item.Kind;

                if (!IsBody(item.Kind))
                {
                    if (!single || index == targetItem)
                    {
                        notBodies++;
                    }

                    continue;
                }

                if (single && index != targetItem && targetBox.HasValue && index < _primitives.Items.Count)
                {
                    Bounds3 box = _primitives.Items[index].Bounds;

                    if (box.HasValue && !Meet(box, targetBox, tolerance.EqualPoint))
                    {
                        continue;
                    }
                }

                try
                {
                    List<GeoSolid3> solids = BodiesOf(item, index, name);

                    foreach (GeoSolid3 solid in solids)
                    {
                        bodies.Add(new ClashBody { Item = index, Name = name, Solid = solid });
                    }
                }
                catch (Exception e) when (!(e is OutOfMemoryException) && !(e is OperationCanceledException))
                {
                    Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, index, name, "left out of the clash check, it could not be rebuilt: " + e.GetType().Name + ": " + e.Message, "viewer", default(Vec3), 0.0, 0.0);
                }
            }

            if (notBodies > 0)
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, targets.Count > 0 ? targets[0] : -1, null, Count(notBodies, "item") + " " + (notBodies == 1 ? "is" : "are") + " not a body (a solid, a closed mesh or a box) and " + (notBodies == 1 ? "was" : "were") + " not checked", "viewer", default(Vec3), 0.0, 0.0);
            }

            ClashPrepare(bodies, tolerance);
            bodies = bodies.Where(b => b.Prepared != null && !b.Prepared.Box.IsEmpty).ToList();

            if (bodies.Count < 2 || (single && !bodies.Any(b => b.Item == targetItem)))
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, targets.Count > 0 ? targets[0] : -1, null, "the clash check needs two bodies; " + bodies.Count.ToString(CultureInfo.InvariantCulture) + (bodies.Count == 1 ? " body" : " bodies") + " could be checked", "viewer", default(Vec3), 0.0, 0.0);
                return;
            }

            // The pairs whose boxes come within the tolerance of each other, from a sweep along the axis with the fewest, and in the order of the scene.
            var lo = new double[3 * bodies.Count];
            var hi = new double[3 * bodies.Count];

            for (int i = 0; i < bodies.Count; i++)
            {
                GeoAabb3 box = bodies[i].Prepared.Box;
                lo[3 * i] = box.Min.X;
                lo[3 * i + 1] = box.Min.Y;
                lo[3 * i + 2] = box.Min.Z;
                hi[3 * i] = box.Max.X;
                hi[3 * i + 1] = box.Max.Y;
                hi[3 * i + 2] = box.Max.Z;
            }

            var pairs = new List<(int, int)>();
            Sweep.Pairs(lo, hi, bodies.Count, tolerance.EqualPoint, (i, j) =>
            {
                // Two pieces of one item are not a pair; and with one item selected, only the pairs it is in.
                if (bodies[i].Item != bodies[j].Item && (!single || bodies[i].Item == targetItem || bodies[j].Item == targetItem))
                {
                    pairs.Add((i, j));
                }
            }, _cancel);
            pairs.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));

            int cap = Math.Max(1, _options.MaxClashPairs);

            if (pairs.Count > cap)
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, bodies[pairs[cap].Item1].Item, bodies[pairs[cap].Item1].Name, (pairs.Count - cap).ToString(CultureInfo.InvariantCulture) + " of " + pairs.Count.ToString(CultureInfo.InvariantCulture) + " pairs of bodies whose boxes meet are not checked: only the first " + cap.ToString(CultureInfo.InvariantCulture) + " in the order of the scene are", "viewer", default(Vec3), 0.0, 0.0);
                pairs.RemoveRange(cap, pairs.Count - cap);
            }

            ClashResult[] results = ClashFind(bodies, pairs, tolerance);
            ClashReport(bodies, pairs, results);
        }

        private static bool Meet(Bounds3 a, Bounds3 b, double reach)
            => a.Min.X <= b.Max.X + reach && b.Min.X <= a.Max.X + reach
                && a.Min.Y <= b.Max.Y + reach && b.Min.Y <= a.Max.Y + reach
                && a.Min.Z <= b.Max.Z + reach && b.Min.Z <= a.Max.Z + reach;

        private static bool IsBody(string kind) => kind == "GeoSolid3" || kind == "GeoAabb3" || kind == "GeoObb3" || kind == "GeoMesh3";

        /// <summary>
        /// The library solids an item stands for: a solid as it is, a box as its six faces, a mesh as the bodies its faces close into, each with the
        /// voids inside it as openings. A mesh is cut into its bodies by its edges (see <see cref="MeshShells"/>), so that the numbers are the library's for the
        /// body as read from the file (its triangles).
        /// </summary>
        private List<GeoSolid3> BodiesOf(SceneItem item, int index, string name)
        {
            var solids = new List<GeoSolid3>();

            if (item.Kind == "GeoMesh3")
            {
                MeshBodies(item, index, name, solids);
                return solids;
            }

            object shape = _scene.Rebuild(item, out _);

            switch (shape)
            {
                case GeoSolid3 solid:
                    solids.Add(solid);
                    break;

                case GeoAabb3 box:
                    if (box.IsEmpty)
                    {
                        Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, index, name, "left out of the clash check: an empty box has no volume", "viewer", default(Vec3), 0.0, 0.0);
                    }
                    else
                    {
                        solids.Add(box.ToObb().ToSolid());
                    }

                    break;

                case GeoObb3 oriented:
                    solids.Add(oriented.ToSolid());
                    break;
            }

            return solids;
        }

        private void MeshBodies(SceneItem item, int index, string name, List<GeoSolid3> solids)
        {
            Vec3[] points = DataJson.Points(item.Data, "points", 3);
            List<int[]> faces = GeometryDecoder.Faces(item.Data).ToList();
            Tolerance tolerance = _scene.Tolerance;
            int open = 0;

            int insideOut = 0;

            foreach (ShellPiece piece in MeshShells.Split(points, faces, tolerance.EqualPoint, _cancel))
            {
                // A shell that runs inside out with no body round it is the void of nothing, and no body.
                if (piece.InsideOut)
                {
                    insideOut++;
                    continue;
                }

                // A piece the edges leave open may still be a body to the library (an edge cut at the middle on one side and whole on the other is closed to it):
                // it is built as it is and the library says whether it closes.
                try
                {
                    GeoFace3[] outer = piece.Outer.Select(f => Face(points, faces[f], false)).Where(f => f != null).ToArray();
                    GeoSolid3[] voids = piece.Voids.Select(shell => new GeoSolid3(shell.Select(f => Face(points, faces[f], true)).Where(f => f != null))).ToArray();
                    var solid = new GeoSolid3(outer, voids);

                    if (solid.IsClosed(tolerance))
                    {
                        solids.Add(solid);
                        continue;
                    }
                }
                catch (Exception e) when (!piece.Closed && (e is ArgumentException || e is InvalidOperationException))
                {
                    // A piece of loose faces the library will not take as a body is open.
                }

                open++;
            }

            if (insideOut > 0)
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, index, name, "left out of the clash check: " + (insideOut == 1 ? "an inside-out shell" : insideOut.ToString(CultureInfo.InvariantCulture) + " inside-out shells") + " with no body round " + (insideOut == 1 ? "it" : "them") + " (a void of nothing is no body)", "viewer", default(Vec3), 0.0, 0.0);
            }

            if (open > 0)
            {
                Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, index, name, "left out of the clash check: " + (open == 1 ? "the mesh is" : open.ToString(CultureInfo.InvariantCulture) + " pieces of the mesh are") + " not closed (the Edges check finds where it is open), so " + (open == 1 ? "it bounds" : "they bound") + " no volume", "viewer", default(Vec3), 0.0, 0.0);
            }
        }

        /// <summary>
        /// A face of a mesh as a library face, its corners that repeat the one before taken out; null where fewer than three are left. A void is read with its
        /// faces turned over, since a body's opening is a body of its own.
        /// </summary>
        private static GeoFace3 Face(Vec3[] points, int[] face, bool reverse)
        {
            var corners = new List<GeoPoint3>(face.Length);

            foreach (int i in face)
            {
                var p = new GeoPoint3(points[i].X, points[i].Y, points[i].Z);

                if (corners.Count == 0 || !new BitKey(points[i]).Equals(new BitKey(new Vec3(corners[corners.Count - 1].X, corners[corners.Count - 1].Y, corners[corners.Count - 1].Z))))
                {
                    corners.Add(p);
                }
            }

            if (corners.Count > 1 && new BitKey(new Vec3(corners[0].X, corners[0].Y, corners[0].Z)).Equals(new BitKey(new Vec3(corners[corners.Count - 1].X, corners[corners.Count - 1].Y, corners[corners.Count - 1].Z))))
            {
                corners.RemoveAt(corners.Count - 1);
            }

            if (corners.Count < 3)
            {
                return null;
            }

            if (reverse)
            {
                corners.Reverse();
            }

            return new GeoFace3(new GeoPolygon3(corners), null);
        }

        /// <summary>
        /// Prepares every body for the check once, on every processor; a body the library cannot prepare is left without, and said.
        /// </summary>
        private void ClashPrepare(List<ClashBody> bodies, Tolerance tolerance)
        {
            var failure = new string[bodies.Count];

            try
            {
                Parallel.For(0, bodies.Count, new ParallelOptions { CancellationToken = _cancel }, i =>
                {
                    try
                    {
                        using (Tolerance.Use(tolerance))
                        {
                            bodies[i].Prepared = new GeoPreparedSolid3(bodies[i].Solid, tolerance);
                        }
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException) && !(e is OperationCanceledException))
                    {
                        failure[i] = e.GetType().Name + ": " + e.Message;
                    }
                });
            }
            catch (AggregateException e)
            {
                Unwrap(e);
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                if (failure[i] != null)
                {
                    Add(DiagnosticChecks.Clash, DiagnosticKind.Note, false, bodies[i].Item, bodies[i].Name, "left out of the clash check, the library could not prepare it: " + failure[i], "GeoPreparedSolid3", default(Vec3), 0.0, 0.0);
                }
            }
        }

        /// <summary>
        /// Puts the findings of the clash check in their order, and keeps the first <see cref="DiagnosticOptions.MaxClashFindings"/> of those about a pair: one
        /// cap for the whole run, so that no pair is hidden by the item it is attached to, and the same pairs are kept in whatever order the scene is.
        /// </summary>
        private void FlushClash()
        {
            _pending.Sort(DiagnosticFinding.Compare);
            int cap = Math.Max(1, _options.MaxClashFindings);
            int kept = 0;
            int dropped = 0;

            foreach (DiagnosticFinding f in _pending)
            {
                if (f.OtherItemIndex < 0)
                {
                    _all.Add(f);
                }
                else if (kept < cap)
                {
                    _all.Add(f);
                    kept++;
                }
                else
                {
                    dropped++;
                }
            }

            if (dropped > 0)
            {
                _all.Add(new DiagnosticFinding(DiagnosticChecks.Clash, DiagnosticKind.Note, false, -1, null, dropped.ToString(CultureInfo.InvariantCulture) + " more pairs of bodies are not listed: only the first " + cap.ToString(CultureInfo.InvariantCulture) + " findings, clashes before touches, in order of place, are kept", "viewer", default(Vec3), 0.0, 20.0 * Math.Max(_tol, 1E-9)));
            }

            _pending.Clear();
        }

        /// <summary>
        /// Asks the library about each pair, on every processor, and gives the answers in the order of the pairs, whatever order the threads finish in.
        /// </summary>
        private ClashResult[] ClashFind(List<ClashBody> bodies, List<(int, int)> pairs, Tolerance tolerance)
        {
            var results = new ClashResult[pairs.Count];

            // One pair at a time to the library, which would use every processor on a pair; the processors are used across the pairs instead.
            var options = new ClashOptions(0.0, true, 1);

            try
            {
                Parallel.For(0, pairs.Count, new ParallelOptions { CancellationToken = _cancel }, k =>
                {
                    using (Tolerance.Use(tolerance))
                    {
                        ClashResult[] found = Clash3.Find(new[] { bodies[pairs[k].Item1].Prepared }, new[] { bodies[pairs[k].Item2].Prepared }, options, tolerance);
                        results[k] = found.Length > 0 ? found[0] : null;
                    }
                });
            }
            catch (AggregateException e)
            {
                Unwrap(e);
            }

            return results;
        }

        /// <summary>
        /// Rethrows the cancellation a parallel loop wraps, else the first fault it met.
        /// </summary>
        private static void Unwrap(AggregateException e)
        {
            AggregateException flat = e.Flatten();
            Exception cancelled = flat.InnerExceptions.FirstOrDefault(x => x is OperationCanceledException);

            if (cancelled != null)
            {
                throw new OperationCanceledException(cancelled.Message);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(flat.InnerExceptions[0]).Throw();
        }

        private void ClashReport(List<ClashBody> bodies, List<(int, int)> pairs, ClashResult[] results)
        {
            for (int k = 0; k < results.Length; k++)
            {
                ClashResult r = results[k];

                if (r == null)
                {
                    continue;
                }

                ClashBody a = bodies[pairs[k].Item1];
                ClashBody b = bodies[pairs[k].Item2];
                string who = "item " + Number(a.Item) + " '" + a.Name + "' and item " + Number(b.Item) + " '" + b.Name + "'";
                Vec3 at = new Vec3(r.Location.X, r.Location.Y, r.Location.Z);
                DiagnosticFinding f;

                switch (r.Kind)
                {
                    case ClashKind.Hard:
                    {
                        string text = who + " clash: they share a volume of " + N(r.Volume) + " in " + Count(r.Overlaps.Count, "piece") + ", " + N(r.Depth) + " deep, at " + Where(at);
                        double radius = 0.0;
                        var drawn = new DiagnosticOverlay();
                        int faces = 0;

                        foreach (GeoSolid3 overlap in r.Overlaps)
                        {
                            GeoAabb3 box = overlap.GetAabb();
                            radius = Math.Max(radius, new Vec3(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z).Length * 0.6);

                            foreach (GeoFace3 face in overlap.Faces)
                            {
                                if (faces++ >= MaxFacesDrawnPerPair)
                                {
                                    break;
                                }

                                ClashOutline(drawn, face, DiagnosticColors.Clash);
                            }
                        }

                        f = Add(DiagnosticChecks.Clash, DiagnosticKind.HardClash, true, a.Item, a.Name, text, "Clash3.Find: ClashResult.Volume, Depth, Overlaps", at, r.Volume, radius);
                        f.Drawing.Strokes.AddRange(drawn.Strokes);
                        f.Drawing.Dots.Add(new DiagnosticDot(at, DiagnosticColors.Clash, 9.0));
                        break;
                    }

                    case ClashKind.Touch:
                    {
                        string text = who + " touch " + (r.Contact.Count > 0 ? "face to face over " + N(r.ContactArea) : "along an edge or at a corner, sharing no volume") + (r.Overlaps.Count > 0 ? "; they share " + N(r.Volume) + " of volume, " + N(r.Depth) + " deep, too little to count" : string.Empty) + ", at " + Where(at);
                        var drawn = new DiagnosticOverlay();
                        double radius = 0.0;
                        int faces = 0;

                        foreach (GeoFace3 face in r.Contact)
                        {
                            GeoAabb3 box = face.GetAabb();
                            radius = Math.Max(radius, new Vec3(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z).Length * 0.6);

                            if (faces++ < MaxFacesDrawnPerPair)
                            {
                                ClashOutline(drawn, face, DiagnosticColors.Touch);
                            }
                        }

                        f = Add(DiagnosticChecks.Clash, DiagnosticKind.TouchingBodies, false, a.Item, a.Name, text, "Clash3.Find: ClashResult.Contact, ContactArea", at, r.ContactArea, radius);
                        f.Drawing.Strokes.AddRange(drawn.Strokes);
                        f.Drawing.Dots.Add(new DiagnosticDot(at, DiagnosticColors.Touch, 9.0));
                        break;
                    }

                    default:
                        f = Add(DiagnosticChecks.Clash, DiagnosticKind.UnresolvedClash, true, a.Item, a.Name, who + " could not be checked: " + (r.Error != null ? r.Error.GetType().Name + ": " + r.Error.Message : r.Kind.ToString()), "Clash3.Find: ClashResult.Error", at, 0.0, 0.0);
                        break;
                }

                f.OtherItemIndex = b.Item;
            }
        }

        /// <summary>
        /// Draws the outline of a face, and of each hole in it.
        /// </summary>
        private static void ClashOutline(DiagnosticOverlay overlay, GeoFace3 face, string color)
        {
            overlay.Loop(color, 3.0, face.Boundary.Vertices.Select(p => new Vec3(p.X, p.Y, p.Z)).ToArray());

            foreach (GeoPolygon3 hole in face.Holes)
            {
                overlay.Loop(color, 3.0, hole.Vertices.Select(p => new Vec3(p.X, p.Y, p.Z)).ToArray());
            }
        }
    }
}
