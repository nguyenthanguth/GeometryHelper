using System;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A section a drawing is asked for: a cut square to the direction one of the views along the axes looks in, at a share of
    /// the depth of the objects, seen from that view's side with what lies in front of the cut taken away.
    /// </summary>
    /// <remarks>
    /// The share is counted from the side away from the viewer: a beam 3 000 long along the drawing frame's X, cut as the right
    /// end view sees it (from +X) at 0.5, is cut at 1 500 and shows its cross-section with the half from 0 to 1 500 behind
    /// it, as deep as <see cref="ViewSettings.SectionDepth"/> lets the view see. The section is named "SECTION A-A" for the
    /// name "A".
    /// </remarks>
    public sealed class SectionViewRequest
    {
        /// <summary>Makes the request for a section.</summary>
        /// <param name="name">The letter or name of the section, "A" for SECTION A-A.</param>
        /// <param name="lookingAs">The view along the axes whose direction the section looks in: front, top, bottom, left,
        /// right or back.</param>
        /// <param name="share">Where the cut is, as a share of the depth of the objects from the side away from the viewer:
        /// nought to one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, or the view is not one along the axes.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the share is not a number from nought to one.</exception>
        public SectionViewRequest(string name, ViewKind lookingAs, double share)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("A section needs a name.", nameof(name));
            }

            if (lookingAs != ViewKind.Front && lookingAs != ViewKind.Top && lookingAs != ViewKind.Bottom
                && lookingAs != ViewKind.Left && lookingAs != ViewKind.Right && lookingAs != ViewKind.Back)
            {
                throw new ArgumentException("A section looks as one of the views along the axes does.", nameof(lookingAs));
            }

            if (!(share >= 0.0) || !(share <= 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(share), share, "The share has to be a number from nought to one.");
            }

            Name = name;
            LookingAs = lookingAs;
            Share = share;
        }

        /// <summary>Gets the letter or name of the section.</summary>
        public string Name { get; }

        /// <summary>Gets the view along the axes whose direction the section looks in.</summary>
        public ViewKind LookingAs { get; }

        /// <summary>Gets where the cut is, as a share of the depth of the objects from the side away from the viewer.</summary>
        public double Share { get; }
    }
}
