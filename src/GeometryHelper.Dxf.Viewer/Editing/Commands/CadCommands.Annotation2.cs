using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The annotation commands of the acad.pgp plan, batch 2: DIMOVERRIDE, TEXTALIGN, MLEADERALIGN, MLEADERCOLLECT,
    /// MLEADEREDIT, ATTIPEDIT and SPELL, each in its own file; this one registers them.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddAnnotation2To(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("DIMOVERRIDE", DimOverride));
            registry.Add(new CadCommand("MLEADEREDIT", MLeaderEdit));
            registry.Add(new CadCommand("TEXTALIGN", TextAlign));
            registry.Add(new CadCommand("MLEADERALIGN", MLeaderAlign));
            registry.Add(new CadCommand("ATTIPEDIT", AttributeInPlace));
            registry.Add(new CadCommand("SPELL", Spell));
        }
    }
}
