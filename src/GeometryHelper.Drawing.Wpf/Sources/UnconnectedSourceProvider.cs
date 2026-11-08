using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// A source the app knows of but cannot reach: listed in the New drawing dialog, greyed, with what it needs. Tekla
    /// Structures and IFC files are listed this way until their bridges are plugged in; this project holds no Tekla or xBIM code.
    /// </summary>
    public sealed class UnconnectedSourceProvider : IDrawingSourceProvider
    {
        /// <summary>Makes a source that is not connected.</summary>
        /// <param name="name">The name shown.</param>
        /// <param name="description">The line under the name.</param>
        /// <param name="availabilityText">Why it cannot give objects now.</param>
        /// <exception cref="ArgumentNullException">Thrown when any of them is null.</exception>
        public UnconnectedSourceProvider(string name, string description, string availabilityText)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            AvailabilityText = availabilityText ?? throw new ArgumentNullException(nameof(availabilityText));
        }

        /// <summary>Gets Tekla Structures, not connected, as the design names it.</summary>
        public static UnconnectedSourceProvider TeklaStructures => new UnconnectedSourceProvider(
            "Tekla Structures",
            "Pick the objects in Tekla; their parts, bolts and welds are read through TeklaConvert",
            "Not connected · no Tekla Structures bridge in this app yet");

        /// <summary>Gets an IFC file, not connected, as the design names it.</summary>
        public static UnconnectedSourceProvider IfcFile => new UnconnectedSourceProvider(
            "IFC file…",
            "Choose an .ifc file; its objects are read through IfcConvert",
            "Not connected · no IFC bridge in this app yet");

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <summary>Gets false: the source cannot give objects.</summary>
        public bool IsAvailable => false;

        /// <inheritdoc/>
        public string AvailabilityText { get; }

        /// <summary>Gives no objects, the source not being connected.</summary>
        /// <returns>Never returns.</returns>
        /// <exception cref="InvalidOperationException">Always: the source is not connected.</exception>
        public IReadOnlyList<DrawingObject> CreateObjects() => throw new InvalidOperationException(Name + " is not connected: " + AvailabilityText + ".");
    }
}
