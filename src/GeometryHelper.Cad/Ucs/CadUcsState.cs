using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The user coordinate systems of a drawing at one moment: the current system of model space and of paper space, the earlier
    /// ones kept of each for UCS Previous (oldest first) and the named ones in the order of their names. A step of undo that
    /// changes the systems holds two of these, before and after, and U and REDO put the systems back by them; the systems
    /// themselves never change, so two states are the same where they hold the same objects.
    /// </summary>
    internal sealed class CadUcsState
    {
        internal CadUcsState(
            CadUcs model,
            CadUcs paper,
            CadUcs[] modelPrevious,
            CadUcs[] paperPrevious,
            IDictionary<string, CadUcs> named
        )
        {
            Model = model;
            Paper = paper;
            ModelPrevious = modelPrevious;
            PaperPrevious = paperPrevious;
            Named = new CadUcs[named.Count];
            named.Values.CopyTo(Named, 0);
        }

        /// <summary>Gets the current system of model space.</summary>
        public CadUcs Model { get; }

        /// <summary>Gets the current system of paper space.</summary>
        public CadUcs Paper { get; }

        /// <summary>Gets the earlier systems of model space, the oldest first.</summary>
        public CadUcs[] ModelPrevious { get; }

        /// <summary>Gets the earlier systems of paper space, the oldest first.</summary>
        public CadUcs[] PaperPrevious { get; }

        /// <summary>Gets the named systems, in ordinal order of their names.</summary>
        public CadUcs[] Named { get; }

        /// <summary>Whether another state holds the same systems in the same places.</summary>
        public bool SameAs(CadUcsState other) =>
            ReferenceEquals(Model, other.Model)
            && ReferenceEquals(Paper, other.Paper)
            && SameList(ModelPrevious, other.ModelPrevious)
            && SameList(PaperPrevious, other.PaperPrevious)
            && SameList(Named, other.Named);

        /// <summary>The state in a few words, as the record of its step shows it: the current system of model space, and how many earlier ones are kept.</summary>
        public string Describe() =>
            Model
            + " ("
            + ModelPrevious.Length.ToString(CultureInfo.InvariantCulture)
            + " earlier, "
            + Named.Length.ToString(CultureInfo.InvariantCulture)
            + " named)";

        private static bool SameList(CadUcs[] a, CadUcs[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
