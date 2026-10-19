using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What a scene is drawn with once a set of clip planes has cut it, and the section lines the planes leave on its bodies and meshes, and where asked the caps
    /// that fill the closed ones. It is made in one pass, on any thread, since the cutting is done on the shapes themselves and holds no screen type.
    /// </summary>
    public sealed class ClipResult
    {
        private static readonly IReadOnlyList<DrawPolyline> NoLines = new DrawPolyline[0];
        private static readonly IReadOnlyList<DrawTriangle> NoTriangles = new DrawTriangle[0];

        private ClipResult(ClipSet clips, Primitives clipped, IReadOnlyList<IReadOnlyList<DrawPolyline>> sections, IReadOnlyList<IReadOnlyList<DrawTriangle>> caps, int capsRefused)
        {
            Clips = clips;
            Clipped = clipped;
            Sections = sections;
            Caps = caps;
            CapsRefused = capsRefused;
        }

        /// <summary>
        /// Gets the set of planes that cut.
        /// </summary>
        public ClipSet Clips { get; }

        /// <summary>
        /// Gets what is left to draw: the primitives of the scene themselves where no plane is on, otherwise a cut copy with the same box round it.
        /// </summary>
        public Primitives Clipped { get; }

        /// <summary>
        /// Gets the section lines of each item, in the order of the items: where the planes meet its triangles, cut by the other planes so that
        /// only what lies inside the clipped region is left. An item with no triangles, or that no plane meets, has none.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<DrawPolyline>> Sections { get; }

        /// <summary>
        /// Gets the triangles that fill the closed section lines of each item, in the order of the items, cut by the other planes like the lines; none for an item
        /// that has no closed line, and none at all where the caps were not asked for.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<DrawTriangle>> Caps { get; }

        /// <summary>
        /// Gets how many outlines of closed section lines the library refused to cut into a cap, which are left unfilled.
        /// </summary>
        public int CapsRefused { get; }

        /// <summary>
        /// Cuts a scene.
        /// </summary>
        /// <param name="source">What the scene is drawn with.</param>
        /// <param name="clips">The planes.</param>
        /// <param name="cancel">Stops the work between items.</param>
        /// <returns>The result, with no caps.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the planes are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static ClipResult Build(Primitives source, ClipSet clips, CancellationToken cancel = default(CancellationToken)) => Build(source, clips, false, cancel);

        /// <summary>
        /// Cuts a scene, and fills the closed section lines where asked.
        /// </summary>
        /// <param name="source">What the scene is drawn with.</param>
        /// <param name="clips">The planes.</param>
        /// <param name="caps">Whether to fill the closed lines in which the planes cut closed bodies (<see cref="ItemPrimitives.IsClosedBody"/>; a face and a body with a hole in its skin are left as lines) with triangles, cut by the other planes, in the tolerance the scene was drawn at (<see cref="Primitives.Tolerance"/>).</param>
        /// <param name="cancel">Stops the work between items.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the planes are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static ClipResult Build(Primitives source, ClipSet clips, bool caps, CancellationToken cancel = default(CancellationToken)) => Build(source, clips, caps, true, cancel);

        /// <summary>
        /// Cuts a scene for a look while a plane is being dragged: what is left to draw, with no section lines and no caps, which is the part of the work that a person watching the plane move can do without.
        /// </summary>
        /// <param name="source">What the scene is drawn with.</param>
        /// <param name="clips">The planes.</param>
        /// <param name="cancel">Stops the work between items.</param>
        /// <returns>The result, with no section lines and no caps.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the planes are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static ClipResult BuildPreview(Primitives source, ClipSet clips, CancellationToken cancel = default(CancellationToken)) => Build(source, clips, false, false, cancel);

        private static ClipResult Build(Primitives source, ClipSet clips, bool caps, bool wantSections, CancellationToken cancel)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (clips == null)
            {
                throw new ArgumentNullException(nameof(clips));
            }

            // The scene is cut with its own slack, whatever the set was made with: the one rule that the drawing, the picking, the snaps and the box select use.
            clips = clips.ForScene(source.Bounds);
            var sections = new IReadOnlyList<DrawPolyline>[source.Items.Count];
            var fills = new IReadOnlyList<DrawTriangle>[source.Items.Count];

            for (int i = 0; i < sections.Length; i++)
            {
                sections[i] = NoLines;
                fills[i] = NoTriangles;
            }

            if (!clips.IsActive)
            {
                return new ClipResult(clips, source, sections, fills, 0);
            }

            var cut = new Primitives { Bounds = source.Bounds, Tolerance = source.Tolerance };
            cut.Warnings.AddRange(source.Warnings);
            int refused = 0;

            for (int i = 0; i < sections.Length; i++)
            {
                cancel.ThrowIfCancellationRequested();
                ItemPrimitives item = source.Items[i];
                cut.Items.Add(clips.Apply(item, cancel));

                if (!wantSections || item.Triangles.Count == 0)
                {
                    continue;
                }

                // A vertex is on a plane, and two points are one, within the slack; a scene with no box has no slack, and the size of the item does.
                double weld = clips.Slack > 0.0 ? clips.Slack : 1E-9 * Math.Max(1.0, item.Bounds.IsEmpty ? 1.0 : item.Bounds.Size.Length);
                var lines = new List<DrawPolyline>();
                var filled = new List<DrawTriangle>();

                for (int p = 0; p < clips.Planes.Count; p++)
                {
                    ClipPlane plane = clips.Planes[p];

                    if (!plane.Enabled)
                    {
                        continue;
                    }

                    // A plane that the box round the item lies wholly on one side of, by more than the weld, meets none of its triangles.
                    if (item.BoundsCoverAll && !item.Bounds.IsEmpty && !Meets(plane, item.Bounds, weld))
                    {
                        continue;
                    }

                    ClipSet others = clips.Without(p);
                    List<DrawPolyline> chains = SectionBuilder.Chains(item, plane, weld, cancel);

                    foreach (DrawPolyline chain in chains)
                    {
                        if (others.IsActive)
                        {
                            others.ClipPolyline(chain, lines);
                        }
                        else
                        {
                            lines.Add(chain);
                        }
                    }

                    if (caps && item.IsClosedBody && chains.Count > 0)
                    {
                        List<DrawTriangle> fill = SectionCaps.Build(chains, plane, source.Tolerance, out int refusedHere, cancel);
                        refused += refusedHere;

                        if (fill.Count > 0)
                        {
                            if (others.IsActive)
                            {
                                var piece = new ItemPrimitives { Bounds = item.Bounds };
                                piece.Triangles.AddRange(fill);
                                filled.AddRange(others.Apply(piece, cancel).Triangles);
                            }
                            else
                            {
                                filled.AddRange(fill);
                            }
                        }
                    }
                }

                if (lines.Count > 0)
                {
                    sections[i] = lines;
                }

                if (filled.Count > 0)
                {
                    fills[i] = filled;
                }
            }

            return new ClipResult(clips, cut, sections, fills, refused);
        }

        /// <summary>
        /// Gets whether a plane may meet something inside a box: false where the box lies wholly on one side of it by more than the weld and a margin of rounding.
        /// </summary>
        private static bool Meets(ClipPlane plane, Bounds3 box, double weld)
        {
            double largest = Math.Max(
                Math.Max(Math.Max(Math.Abs(box.Min.X), Math.Abs(box.Min.Y)), Math.Abs(box.Min.Z)),
                Math.Max(Math.Max(Math.Abs(box.Max.X), Math.Abs(box.Max.Y)), Math.Abs(box.Max.Z)));
            double margin = weld + 1E-9 * (1.0 + largest + Math.Abs(plane.Point.X) + Math.Abs(plane.Point.Y) + Math.Abs(plane.Point.Z));
            double lowest = double.PositiveInfinity;
            double highest = double.NegativeInfinity;

            for (int i = 0; i < 8; i++)
            {
                double d = plane.SignedDistance(new Vec3((i & 1) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z));
                lowest = Math.Min(lowest, d);
                highest = Math.Max(highest, d);
            }

            return !(lowest > margin || highest < -margin);
        }
    }
}
