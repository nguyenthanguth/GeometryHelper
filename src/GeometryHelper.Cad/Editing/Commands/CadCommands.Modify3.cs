using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The registry entries of REVERSE, SETBYLAYER (and its variable SETBYLAYERMODE), CHPROP, BURST, TXTEXP and CHSPACE; each
    /// command is in its own file.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddModify3To(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("REVERSE", Reverse, true));
            registry.Add(new CadCommand("SETBYLAYER", SetByLayer, true));
            registry.Add(new CadCommand("CHPROP", ChProp, true));
            registry.Add(new CadCommand("BURST", Burst, true));
            registry.Add(new CadCommand("TXTEXP", TextExplode, true));
            registry.Add(new CadCommand("CHSPACE", ChangeSpace, true));

            // Which properties SETBYLAYER sets, and its two last questions' defaults: AutoCAD keeps it in the registry, 255 at first.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SETBYLAYERMODE",
                    false,
                    0,
                    255,
                    s => SetByLayerMode(s),
                    (c, v) => c.Remember("setbylayermode", v)
                )
            );
        }

        /// <summary>
        /// CHPROP, as AutoCAD 2021 asks it: the objects, then "Enter property to change [Color/LAyer/LType/ltScale/LWeight/
        /// Thickness/TRansparency/Material/Annotative]:" until Enter, the same questions as the Properties option of CHANGE
        /// less Elev, which CHANGE asks as well (<see cref="ChangeProperty"/> asks both).
        /// </summary>
        private static IEnumerable<CadPrompt> ChProp(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            foreach (CadPrompt prompt in ChangeProperty(c, false))
            {
                yield return prompt;
            }
        }
    }
}
