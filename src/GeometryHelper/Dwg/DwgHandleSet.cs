using System;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// A set of the handles written so far, made for the number of objects a DWG holds: its table is made once for them
    /// (two slots to a handle, in one array), where a <see cref="System.Collections.Generic.HashSet{T}"/> grown by adding
    /// would make the array of every size between, each one a large object that makes the collector look at the whole heap.
    /// A handle is looked for by a multiplication and a mask, and the table doubles where it is more than half full.
    /// </summary>
    internal sealed class DwgHandleSet
    {
        private ulong[] _slots;
        private bool _hasZero;

        /// <summary>A set with room for <paramref name="capacity"/> handles before it doubles.</summary>
        public DwgHandleSet(int capacity)
        {
            int size = 16;

            while (size < (long)capacity * 2)
            {
                size <<= 1;
            }

            _slots = new ulong[size];
        }

        /// <summary>The handles in the set.</summary>
        public int Count { get; private set; }

        /// <summary>Puts a handle in the set; false where it was in it already.</summary>
        public bool Add(ulong handle)
        {
            if (handle == 0)
            {
                if (_hasZero)
                {
                    return false;
                }

                _hasZero = true;
                Count++;
                return true;
            }

            if ((Count + 1) * 2 > _slots.Length)
            {
                Grow();
            }

            int at = SlotOf(_slots, handle);

            if (_slots[at] == handle)
            {
                return false;
            }

            _slots[at] = handle;
            Count++;
            return true;
        }

        /// <summary>Whether a handle is in the set.</summary>
        public bool Contains(ulong handle) => handle == 0 ? _hasZero : _slots[SlotOf(_slots, handle)] == handle;

        /// <summary>The slot that holds <paramref name="handle"/> or the empty one it would go in.</summary>
        private static int SlotOf(ulong[] slots, ulong handle)
        {
            int mask = slots.Length - 1;
            int at = (int)((handle * 0x9E3779B97F4A7C15UL) >> 32) & mask;

            while (slots[at] != 0 && slots[at] != handle)
            {
                at = (at + 1) & mask;
            }

            return at;
        }

        private void Grow()
        {
            var bigger = new ulong[_slots.Length * 2];

            foreach (ulong handle in _slots)
            {
                if (handle != 0)
                {
                    bigger[SlotOf(bigger, handle)] = handle;
                }
            }

            _slots = bigger;
        }
    }
}
