using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What a scene is drawn with once a set of clip planes has cut it, and the section lines the planes leave on its bodies and meshes. It is
    /// made in one pass, on any thread, since the cutting is done on the shapes themselves and holds no screen type.
    /// </summary>
    public sealed class ClipResult
    {
        private static readonly IReadOnlyList<DrawPolyline> NoLines = new DrawPolyline[0];

        private ClipResult(ClipSet clips, Primitives clipped, IReadOnlyList<IReadOnlyList<DrawPolyline>> sections)
        {
            Clips = clips;
            Clipped = clipped;
            Sections = sections;
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
        /// Cuts a scene.
        /// </summary>
        /// <param name="source">What the scene is drawn with.</param>
        /// <param name="clips">The planes.</param>
        /// <param name="cancel">Stops the work between items.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the planes are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static ClipResult Build(Primitives source, ClipSet clips, CancellationToken cancel = default(CancellationToken))
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

            for (int i = 0; i < sections.Length; i++)
            {
                sections[i] = NoLines;
            }

            if (!clips.IsActive)
            {
                return new ClipResult(clips, source, sections);
            }

            var cut = new Primitives { Bounds = source.Bounds };
            cut.Warnings.AddRange(source.Warnings);

            for (int i = 0; i < sections.Length; i++)
            {
                cancel.ThrowIfCancellationRequested();
                ItemPrimitives item = source.Items[i];
                cut.Items.Add(clips.Apply(item, cancel));

                if (item.Triangles.Count == 0)
                {
                    continue;
                }

                // A vertex is on a plane, and two points are one, within the slack; a scene with no box has no slack, and the size of the item does.
                double weld = clips.Slack > 0.0 ? clips.Slack : 1E-9 * Math.Max(1.0, item.Bounds.IsEmpty ? 1.0 : item.Bounds.Size.Length);
                var lines = new List<DrawPolyline>();

                for (int p = 0; p < clips.Planes.Count; p++)
                {
                    ClipPlane plane = clips.Planes[p];

                    if (!plane.Enabled)
                    {
                        continue;
                    }

                    ClipSet others = clips.Without(p);

                    foreach (DrawPolyline chain in SectionBuilder.Chains(item, plane, weld, cancel))
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
                }

                if (lines.Count > 0)
                {
                    sections[i] = lines;
                }
            }

            return new ClipResult(clips, cut, sections);
        }
    }
}
