using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
{
    /// <summary>
    /// Common interface for label arrangement algorithms.
    /// </summary>
    internal interface IArrangeAlgorithm
    {
        /// <summary>
        /// Performs arrangement of the label list.
        /// </summary>
        /// <param name="items">The labels to arrange; a null entry is passed over.</param>
        /// <param name="options">Configuration options controlling the algorithm.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options);
    }
}
