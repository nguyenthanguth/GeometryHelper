using System.Collections.Generic;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        /// <summary>
        /// Replaces the context data of the record (the groups between <c>CONTEXT_DATA{</c> and its closing brace) by the groups
        /// of the context of a scale that has become the default, as AutoCAD 2021 rewrites the record when the default scale is
        /// deleted; the groups before and after it are kept.
        /// </summary>
        internal void SetContextData(List<KeyValuePair<int, string>> inner)
        {
            Rec rec = Read();
            var run = new List<KeyValuePair<int, string>>(rec.Before) { G(300, rec.Context?.Open ?? "CONTEXT_DATA{") };
            run.AddRange(inner);
            run.Add(G(301, rec.Context?.Close ?? "}"));
            run.AddRange(rec.After);
            Write(Rec.Read(run));
        }
    }
}
