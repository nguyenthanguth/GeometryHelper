using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>A clip a PDF page sets: a ring in page points, inside a parent clip where one was set before it.</summary>
    internal sealed class PdfClip
    {
        public PdfClip(double[] ring, PdfClip parent)
        {
            Ring = ring;
            Parent = parent;
            MinX = double.PositiveInfinity;
            MinY = double.PositiveInfinity;
            MaxX = double.NegativeInfinity;
            MaxY = double.NegativeInfinity;

            for (int i = 0; i + 1 < ring.Length; i += 2)
            {
                MinX = Math.Min(MinX, ring[i]);
                MaxX = Math.Max(MaxX, ring[i]);
                MinY = Math.Min(MinY, ring[i + 1]);
                MaxY = Math.Max(MaxY, ring[i + 1]);
            }

            IsBox = ring.Length == 8 && IsAxisRectangle(ring);
        }

        /// <summary>Gets the ring as x and y in turn, closed implicitly.</summary>
        public double[] Ring { get; }

        public PdfClip Parent { get; }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        /// <summary>Gets whether the ring is a rectangle along x and y, which a box test settles.</summary>
        public bool IsBox { get; }

        private static bool IsAxisRectangle(double[] r)
        {
            const double Eps = 1E-6;
            bool a =
                Math.Abs(r[0] - r[2]) < Eps
                && Math.Abs(r[3] - r[5]) < Eps
                && Math.Abs(r[4] - r[6]) < Eps
                && Math.Abs(r[7] - r[1]) < Eps;
            bool b =
                Math.Abs(r[1] - r[3]) < Eps
                && Math.Abs(r[2] - r[4]) < Eps
                && Math.Abs(r[5] - r[7]) < Eps
                && Math.Abs(r[6] - r[0]) < Eps;
            return a || b;
        }
    }

    /// <summary>The optional content a thing belongs to: layers of the PDF it is shown with, by the policy of the group.</summary>
    internal sealed class PdfOc
    {
        public PdfOc(int[] groups, string policy, PdfOc parent)
        {
            Groups = groups;
            Policy = policy ?? "AnyOn";
            Parent = parent;
        }

        /// <summary>Gets the numbers of the optional content groups (layers).</summary>
        public int[] Groups { get; }

        /// <summary>Gets the policy: AnyOn, AllOn, AnyOff or AllOff.</summary>
        public string Policy { get; }

        /// <summary>Gets the content this one is nested in; it is shown only where that is.</summary>
        public PdfOc Parent { get; }

        /// <summary>Whether the content is shown, given the layers that are on.</summary>
        public bool IsShown(Func<int, bool> isOn)
        {
            if (Parent != null && !Parent.IsShown(isOn))
            {
                return false;
            }

            if (Groups.Length == 0)
            {
                return true;
            }

            int on = 0;

            foreach (int group in Groups)
            {
                if (isOn(group))
                {
                    on++;
                }
            }

            switch (Policy)
            {
                case "AllOn":
                    return on == Groups.Length;
                case "AnyOff":
                    return on < Groups.Length;
                case "AllOff":
                    return on == 0;
                default:
                    return on > 0;
            }
        }
    }

    /// <summary>A thing a page draws, in page points from the page's lower left corner, y up.</summary>
    internal abstract class PdfItem
    {
        /// <summary>Gets or sets the clip it is drawn through; null where there is none.</summary>
        public PdfClip Clip { get; set; }

        /// <summary>Gets or sets the layers it belongs to; null where it is on none.</summary>
        public PdfOc Oc { get; set; }
    }

    /// <summary>A line or run of lines a page strokes.</summary>
    internal sealed class PdfStroke : PdfItem
    {
        /// <summary>Gets or sets the pieces: each a run of points as x and y in turn.</summary>
        public List<double[]> Lines { get; set; } = new List<double[]>();

        /// <summary>Gets or sets which of the lines are closed.</summary>
        public List<bool> Closed { get; set; } = new List<bool>();

        public int Rgb { get; set; }

        /// <summary>Gets or sets the width in page points.</summary>
        public double Width { get; set; }

        public double[] Dash { get; set; }

        public double Phase { get; set; }
    }

    /// <summary>An area a page fills, its rings by the even-odd rule or the nonzero one.</summary>
    internal sealed class PdfFill : PdfItem
    {
        public List<double[]> Rings { get; set; } = new List<double[]>();

        public int Rgb { get; set; }

        public bool EvenOdd { get; set; }
    }

    /// <summary>Words a page shows in a font that has no outlines to draw: written by the machine in the font of that name.</summary>
    internal sealed class PdfWords : PdfItem
    {
        public string Text { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        /// <summary>Gets or sets how far the capitals reach above the baseline, in points.</summary>
        public double Height { get; set; }

        public double Rotation { get; set; }

        /// <summary>Gets or sets the width the words take on the page, in points, from the widths the PDF gives.</summary>
        public double Width { get; set; }

        public string Family { get; set; }

        public int Rgb { get; set; }
    }

    /// <summary>A picture a page draws on the unit square of a matrix.</summary>
    internal sealed class PdfPicture : PdfItem
    {
        /// <summary>Gets or sets the matrix (a, b, c, d, e, f) that takes the unit square to the page; the picture's top is at y = 1.</summary>
        public double[] Matrix { get; set; }

        /// <summary>Gets or sets the picture as a file (PNG or JPEG) the canvas decodes.</summary>
        public byte[] Bytes { get; set; }

        /// <summary>Gets or sets what tells these bytes from any others.</summary>
        public string Key { get; set; }
    }

    /// <summary>One page of a PDF as it draws: its size and its things, with what the reader left out.</summary>
    internal sealed class PdfScene
    {
        /// <summary>Gets the width of the page in points, as it is shown (turned by the page's rotation).</summary>
        public double Width { get; set; }

        /// <summary>Gets the height of the page in points.</summary>
        public double Height { get; set; }

        /// <summary>Gets the things of the page in the order they are painted.</summary>
        public List<PdfItem> Items { get; } = new List<PdfItem>();

        /// <summary>Gets what the reader left out, each reason once, such as <c>shadings</c> or <c>Type 3 fonts</c>.</summary>
        public SortedSet<string> Dropped { get; } = new SortedSet<string>(StringComparer.Ordinal);
    }
}
