using System.Collections.Generic;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// Where the objects of a model drawing come from, as the New drawing dialog lists it: a name, a line saying what it is,
    /// whether it can give objects now and why not, and the objects themselves. Sources are converters outside the core
    /// (A7): a bridge to Tekla Structures or IFC gives its objects through one of these, and the app's own test bodies do.
    /// </summary>
    /// <remarks>
    /// A source that is not connected is still listed, greyed, with what it needs: "Tekla Structures, not connected: no Tekla
    /// Structures bridge in this app yet". <see cref="CreateObjects"/> is called off the UI thread and gives new objects each
    /// time, in model millimetres.
    /// </remarks>
    public interface IDrawingSourceProvider
    {
        /// <summary>Gets the name shown, as "Test bodies" or "Tekla Structures".</summary>
        string Name { get; }

        /// <summary>Gets the line under the name, saying what the source gives.</summary>
        string Description { get; }

        /// <summary>Gets whether the source can give objects now.</summary>
        bool IsAvailable { get; }

        /// <summary>Gets what the source's state is, in words: "Built in · 3 assemblies, 16 objects" or "Not connected · ...".</summary>
        string AvailabilityText { get; }

        /// <summary>Gives the objects of the model as they are now.</summary>
        /// <returns>The objects, each with an id unique in the source and the same from one call to the next.</returns>
        IReadOnlyList<DrawingObject> CreateObjects();
    }
}
