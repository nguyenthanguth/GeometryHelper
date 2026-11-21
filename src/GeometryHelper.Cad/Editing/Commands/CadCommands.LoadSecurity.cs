namespace GeometryHelper.Cad
{
    /// <summary>
    /// The three variables of the gate in front of files of code, as AutoCAD 2021 has them, each reading and setting the
    /// <see cref="CadCommandSession.LoadSecurity"/> of the session: SECURELOAD, 0 to 2 (3 is refused: <c>; error: AutoCAD
    /// variable setting rejected: "SECURELOAD" 3</c> from AutoLISP); TRUSTEDPATHS, the folders trusted for code, kept as typed
    /// with <c>%NAME%</c> expanded; LEGACYCODESEARCH, 0 or 1. They belong to the app, not to the drawing, so a change is not a
    /// step of undo. TRUSTEDDOMAINS, which AutoCAD keeps for web content, is not here: the presenter has none.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddLoadSecurityVariablesTo(CadCommandRegistry registry)
        {
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SECURELOAD",
                    false,
                    0,
                    2,
                    s => s.LoadSecurity.SecureLoad,
                    (c, v) => c.Session.LoadSecurity.SecureLoad = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "TRUSTEDPATHS",
                    false,
                    s => s.LoadSecurity.TrustedPaths,
                    (c, typed) =>
                    {
                        c.Session.LoadSecurity.TrustedPaths = typed;
                        return null;
                    }
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "LEGACYCODESEARCH",
                    false,
                    0,
                    1,
                    s => s.LoadSecurity.LegacyCodeSearch ? 1 : 0,
                    (c, v) => c.Session.LoadSecurity.LegacyCodeSearch = v == 1
                )
            );
        }
    }
}
