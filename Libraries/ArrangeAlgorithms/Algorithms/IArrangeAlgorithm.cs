using System.Collections.Generic;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.Algorithms
{
    /// <summary>
    /// Common interface for label arrangement algorithms.
    /// </summary>
    internal interface IArrangeAlgorithm
    {
        /// <summary>
        /// Performs arrangement of the label list.
        /// </summary>
        /// <param name="arranges">List of labels to be arranged.</param>
        /// <param name="options">Configuration options controlling the algorithm.</param>
        /// <returns>List of translation GeoVectors corresponding to each label.</returns>
        List<GeoVector2> Arrange(List<Arrange> arranges, ArrangeOptions options);
    }
}
