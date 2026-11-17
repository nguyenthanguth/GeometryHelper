using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
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
            registry.Add(new CadCommand("TEXTALIGN", TextAlign, true));
            registry.Add(new CadCommand("MLEADERALIGN", MLeaderAlign, true));
            registry.Add(new CadCommand("MLEADERCOLLECT", MLeaderCollect, true));
            registry.Add(new CadCommand("ATTIPEDIT", AttributeInPlace));
            registry.Add(new CadCommand("SPELL", Spell));
            AddTextToolsTo(registry);
            registry.Add(new CadCommand("ATTDISP", AttributeDisplay));
            AddFieldsTo(registry);
        }
    }
}
