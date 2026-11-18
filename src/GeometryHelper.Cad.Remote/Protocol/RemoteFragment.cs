namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// The small drawing objects travel in. A request or an answer holds the DXF text of one: the objects in its model space,
    /// each with the layers, styles and blocks it needs, and a name for each in the message (its handle in that small
    /// drawing), so that the same text carries any number of objects and an object keeps its place across the channel.
    /// </summary>
    internal static class RemoteFragment
    {
        /// <summary>The version of the small drawing, AutoCAD 2013's: every entity the library writes can be held in it.</summary>
        public const string Version = "AC1027";
    }
}
