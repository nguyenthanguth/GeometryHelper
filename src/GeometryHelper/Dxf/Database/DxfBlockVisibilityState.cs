using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A visibility state of a dynamic block, as AutoCAD's Block Editor names one in its Visibility States: a name, and the
    /// entities of the block shown in it. Given to <see cref="DxfBlockTableRecord.AddVisibilityParameter"/>.
    /// </summary>
    /// <remarks>
    /// A sign of a frame, a cross and a tick given the states Cross (the cross), Tick (the tick) and Blank (nothing) shows the
    /// cross in Cross and hides the tick, shows the tick in Tick, and shows neither in Blank; the frame, named in no state, is
    /// shown in all three.
    /// </remarks>
    public sealed class DxfBlockVisibilityState
    {
        private readonly DxfEntity[] _entities;

        /// <summary>
        /// Makes a state of a name showing entities.
        /// </summary>
        /// <param name="name">The name of the state, the value its property takes, as Cross.</param>
        /// <param name="entities">The entities of the block shown in the state; none for a state that hides every entity of the
        /// other states.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name, the entities or one of them is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or holds a line break.</exception>
        public DxfBlockVisibilityState(string name, IEnumerable<DxfEntity> entities)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            if (name.Trim().Length == 0 || name.IndexOf('\r') >= 0 || name.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("A visibility state is named by a name of one line at least one letter long.", nameof(name));
            }

            var kept = new List<DxfEntity>();

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentNullException(nameof(entities), "An entity of the state " + name + " is null.");
                }

                kept.Add(entity);
            }

            Name = name.Trim();
            _entities = kept.ToArray();
        }

        /// <summary>Gets the name of the state, its ends trimmed.</summary>
        public string Name { get; }

        /// <summary>Gets the entities shown in the state, in the order given.</summary>
        public IReadOnlyList<DxfEntity> Entities => Array.AsReadOnly(_entities);
    }
}
