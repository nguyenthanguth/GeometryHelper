using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What is made for the graphics card from a cut scene at a render origin, made once and shared by every view that has that scene, that cut and that origin: a window with
    /// four views opens a scene with one build, not four, and the four elements of an item hold the one geometry object. It is kept by the window and used on its thread; what is
    /// made may be made on any other.
    /// </summary>
    /// <typeparam name="T">What is made: the geometry of every item of the scene.</typeparam>
    public sealed class GeometryCache<T>
    {
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Gets how many times something has been made, which a test reads to see that four views took one.
        /// </summary>
        public int Builds { get; private set; }

        /// <summary>
        /// Gets how many sets the cache holds: those a view holds, and none that no view does.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Takes what is made for a scene cut by a set of planes at a render origin, making it if no view has it: on the thread that asks when it is wanted now, on another thread
        /// when it is not. A view that wants it while it is being made on another thread waits for that one and makes none of its own, unless it is wanted now.
        /// </summary>
        /// <param name="generation">Which scene is shown.</param>
        /// <param name="clip">The cut of the scene; the key is the object, since a new cut is a new object.</param>
        /// <param name="origin">The render origin that what is made is relative to.</param>
        /// <param name="make">Makes it; run once however many views ask.</param>
        /// <param name="now">Whether it is wanted at once on this thread; a set under way on another thread is not waited for then, and one is made.</param>
        /// <returns>The set, which the caller holds until it gives it back with <see cref="Release"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when what makes it is null.</exception>
        public Entry Take(int generation, ClipResult clip, Vec3 origin, Func<T> make, bool now)
        {
            if (make == null)
            {
                throw new ArgumentNullException(nameof(make));
            }

            foreach (Entry entry in _entries)
            {
                if (entry.Generation == generation && ReferenceEquals(entry.Clip, clip) && entry.Origin.Equals(origin) && (!now || entry.Made.Status == TaskStatus.RanToCompletion))
                {
                    entry.Users++;
                    return entry;
                }
            }

            Builds++;
            var made = new Entry(generation, clip, origin, now ? Task.FromResult(make()) : Task.Run(make)) { Users = 1 };
            _entries.Add(made);
            return made;
        }

        /// <summary>
        /// Lets go of a set a view no longer holds; it is dropped when no view holds it.
        /// </summary>
        /// <param name="entry">The set, or null for none.</param>
        public void Release(Entry entry)
        {
            if (entry != null && --entry.Users <= 0)
            {
                _entries.Remove(entry);
            }
        }

        /// <summary>
        /// A set that has been made or is being made, and how many views hold it.
        /// </summary>
        public sealed class Entry
        {
            internal Entry(int generation, ClipResult clip, Vec3 origin, Task<T> made)
            {
                Generation = generation;
                Clip = clip;
                Origin = origin;
                Made = made;
            }

            /// <summary>
            /// Gets which scene it is for.
            /// </summary>
            public int Generation { get; }

            /// <summary>
            /// Gets the cut of the scene it is for.
            /// </summary>
            public ClipResult Clip { get; }

            /// <summary>
            /// Gets the render origin it is relative to.
            /// </summary>
            public Vec3 Origin { get; }

            /// <summary>
            /// Gets what is made, or is being made.
            /// </summary>
            public Task<T> Made { get; }

            /// <summary>
            /// Gets how many views hold it.
            /// </summary>
            public int Users { get; internal set; }
        }
    }
}
