namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The registry entries of DWGPROPS, HYPERLINK, -HYPERLINK, GROUPEDIT and ATTREDEF; each command is in its own file
    /// (MEASUREGEOM's Quick option is part of MEASUREGEOM, in CadCommands.Utilities.cs).
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddMisc2To(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("DWGPROPS", DrawingProperties));
            AddHyperlinksTo(registry);
            registry.Add(new CadCommand("GROUPEDIT", GroupEdit));
            registry.Add(new CadCommand("ATTREDEF", AttRedef));
        }
    }
}
