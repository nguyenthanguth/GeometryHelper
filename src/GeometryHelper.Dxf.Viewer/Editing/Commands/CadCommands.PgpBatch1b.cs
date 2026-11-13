namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The commands left of batch 1 of the acad.pgp plan: CHANGE, RENAME and -RENAME.</summary>
    internal static partial class CadCommands
    {
        private static void AddPgpBatch1bTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CHANGE", Change, true));
            registry.Add(new CadCommand("RENAME", RenameDialog));
            registry.Add(new CadCommand("-RENAME", RenameTyped));
        }
    }
}
