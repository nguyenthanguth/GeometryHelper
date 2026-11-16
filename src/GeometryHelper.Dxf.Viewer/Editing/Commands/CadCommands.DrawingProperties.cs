using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DWGPROPS, as AutoCAD 2021 shows it: the Drawing Properties dialog, its General, Summary, Statistics and Custom tabs. OK
    /// writes the Summary texts and the custom properties to the drawing as one undo step (<see cref="DxfDatabase.SummaryInfo"/>:
    /// the header of a drawing of R2004 or later, the DWGPROPS record of an older one); Cancel writes nothing. With no window to
    /// show the dialog the command says so, as AutoCAD has no typed form of it.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> DrawingProperties(CadCommandContext c)
        {
            var request = new CadDrawingPropertiesRequest(c.Document);

            if (!c.Session.Request("DWGPROPS", c.Document, request))
            {
                c.Write("DWGPROPS is shown by the window.");
                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            if (!request.TryBuild(out DxfDatabaseSummaryInfo info, out string problem))
            {
                c.Write(problem);
                yield break;
            }

            if (info.Equals(c.Database.SummaryInfo))
            {
                yield break;
            }

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Drawing properties") }, s =>
                s.Database.SummaryInfo = info
            );
        }
    }
}
