using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// OOPS as AutoCAD's: the objects the last ERASE, BLOCK or WBLOCK took out of the drawing come back, as they were, with
    /// no question and no message. Only the last set comes back: a second OOPS does nothing, and one after an erase that was
    /// undone does nothing either, the objects being there.
    /// </summary>
    /// <remarks>
    /// The erased objects are kept as a small drawing of their own (<see cref="CopyToClipboard"/>) and put back where they
    /// stood, in the space they were erased from, one undo step. They come back as copies: a handle they had is gone, and so is
    /// a tie to an object that was not erased with them (a hatch to its boundary, a dimension to its points).
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>The objects a document's last erase took, with the handles they had and the space they stood in.</summary>
        private sealed class ErasedSet
        {
            public ErasedSet(CadClipboardData data, IReadOnlyList<string> handles, string space)
            {
                Data = data;
                Handles = handles;
                Space = space;
            }

            public CadClipboardData Data { get; }

            public IReadOnlyList<string> Handles { get; }

            public string Space { get; }
        }

        private static readonly ConditionalWeakTable<CadDocument, ErasedSet> LastErased =
            new ConditionalWeakTable<CadDocument, ErasedSet>();

        /// <summary>
        /// Keeps the objects about to be erased for OOPS, in place of those the last erase kept; called by ERASE and by the
        /// commands that take objects out into a block, before they erase them.
        /// </summary>
        internal static void RememberErased(CadCommandContext c, IReadOnlyList<string> handles)
        {
            LastErased.Remove(c.Document);

            if (handles.Count == 0)
            {
                return;
            }

            try
            {
                CadClipboardData data = CopyToClipboard(c.Document, handles, null);

                if (data != null)
                {
                    LastErased.Add(c.Document, new ErasedSet(data, handles.ToList(), c.Document.WorkingSpace.Name));
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("CadCommands: the objects erased could not be kept for OOPS.", exception);
            }
        }

        private static IEnumerable<CadPrompt> Oops(CadCommandContext c)
        {
            if (!LastErased.TryGetValue(c.Document, out ErasedSet set))
            {
                yield break;
            }

            LastErased.Remove(c.Document);

            // An erase undone has its objects back already: nothing is restored twice.
            if (set.Handles.Any(h => c.Document.FindEntity(h) != null))
            {
                yield break;
            }

            DxfDatabase fragment;

            try
            {
                fragment = DxfDatabase.Parse(set.Data.Drawing);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("CadCommands: the objects kept for OOPS could not be read.", exception);
                c.Write("The objects could not be restored: " + exception.Message);
                yield break;
            }

            List<DxfEntity> held = Held(fragment);
            CadSpace space = c.Document.Spaces.FirstOrDefault(s => s.Name == set.Space) ?? c.Space;

            c.Apply(null, s =>
            {
                foreach (DxfEntity copy in fragment.WblockCloneObjects(held, space.Block))
                {
                    if (!string.IsNullOrEmpty(copy.Handle) && !copy.IsErased)
                    {
                        s.Added(space, copy);
                    }
                }
            });
        }
    }
}
