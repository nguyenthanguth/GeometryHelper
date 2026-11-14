using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>HATCHSETORIGIN, the hatch origin set by a picked point, as AutoCAD 2021's Set Origin of the hatch menu runs it.</summary>
    internal static partial class CadCommands
    {
        internal static void AddHatchOriginTo(CadCommandRegistry registry) =>
            registry.Add(new CadCommand("HATCHSETORIGIN", HatchSetOrigin, true));

        /// <summary>
        /// HATCHSETORIGIN: "Select hatch objects:" (the hatches selected before it are taken instead; whatever else is picked is
        /// left out), then "Select new hatch origin:", a point; every hatch not on a locked layer has its pattern laid from the
        /// point, one undo step, nothing said when it is done. A hatch whose owner allows it no change is refused in the owner's
        /// words and the others are laid. Enter with no hatch selected ends the command. A solid fill and a gradient are left as
        /// they are: they have no lines to lay.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchSetOrigin(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select hatch objects", e => e is DxfHatch))
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Select new hatch origin");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 origin = c.Input.Point;
            var planned = new List<CadEditRecord>();

            foreach (string handle in c.Selected)
            {
                var record = new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Hatch origin");

                if (c.Document.EditGate.Allows(c.Document, c.Document.FindEntity(handle), record, out string reason))
                {
                    planned.Add(record);
                }
                else
                {
                    c.Write(reason ?? "The hatch " + handle + " may not be changed.");
                }
            }

            if (planned.Count == 0)
            {
                yield break;
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    CadHatchOrigin.MoveTo((DxfHatch)s.Document.FindEntity(record.Handle), origin);
                }
            });

            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }
    }
}
