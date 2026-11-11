using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The sheet of a drawing (L4): its size, its frame, its title block filled in, its part list and where they stand, in
    /// paper millimetres from the sheet's lower left corner.
    /// </summary>
    /// <remarks>
    /// An A2 on its side is 594 by 420; with a margin of 10 its frame runs from (10, 10) to (584, 410), and its title block,
    /// 180 by 40, stands in the frame's lower right corner, from (404, 10) to (584, 50). The part list is packed with the
    /// views, as one more box.
    /// </remarks>
    public sealed class DrawingSheet
    {
        internal DrawingSheet(
            string sizeName,
            double width,
            double height,
            GeoRectangle2 frame,
            TitleBlock titleBlock,
            GeoRectangle2 titleBlockBox,
            IReadOnlyList<PartListRow> partList,
            GeoRectangle2? partListBox
        )
        {
            SizeName = sizeName;
            Width = width;
            Height = height;
            Frame = frame;
            TitleBlock = titleBlock;
            TitleBlockBox = titleBlockBox;
            PartList = partList;
            PartListBox = partListBox;
        }

        /// <summary>The height of a row of the part list on paper, in millimetres.</summary>
        public const double PartListRowHeight = 6.0;

        /// <summary>The width of the part list on paper, in millimetres.</summary>
        public const double PartListWidth = 160.0;

        /// <summary>Gets the name of the size: A0 to A4, or the width by the height for a sheet of its own, as "500x300".</summary>
        public string SizeName { get; }

        /// <summary>Gets the width of the sheet, in millimetres.</summary>
        public double Width { get; }

        /// <summary>Gets the height of the sheet, in millimetres.</summary>
        public double Height { get; }

        /// <summary>Gets the frame: the sheet less its margin.</summary>
        public GeoRectangle2 Frame { get; }

        /// <summary>Gets the texts of the title block, those left empty in the settings filled in from the source.</summary>
        public TitleBlock TitleBlock { get; }

        /// <summary>Gets where the title block stands: in the lower right corner of the frame.</summary>
        public GeoRectangle2 TitleBlockBox { get; }

        /// <summary>Gets the rows of the part list, in the order of their marks; empty where the sheet has none.</summary>
        public IReadOnlyList<PartListRow> PartList { get; }

        /// <summary>Gets where the part list stands; null where the sheet has none.</summary>
        public GeoRectangle2? PartListBox { get; }

        /// <summary>Gets the size of the part list on paper: its rows and its head, <see cref="PartListRowHeight"/> each.</summary>
        internal static double PartListHeight(int rows) => (rows + 1) * PartListRowHeight;

        /// <inheritdoc/>
        public override string ToString() => "Sheet " + SizeName;
    }
}
