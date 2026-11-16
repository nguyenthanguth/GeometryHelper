using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The order of what an action lists. AutoCAD 2021 keeps the entities a stretch action binds and the actions a group of
    /// constraints owns in an order of its own, which is no order of their handles: reading its DWG of the 13 dynamic block
    /// samples and writing the 74 stretch actions and 34 groups again gives its own bytes only where that order is kept (a
    /// handle order, which the reader and the writer once put in, matched 5 of the 34 groups of its R2018 files' handle streams
    /// and none of the 13 stretch actions that list them in another order).
    /// </summary>
    public class DwgOrderOfWhatAnActionOwnsTests
    {
        private static string Object(string type, string handle, string owner, string groups)
        {
            var text = new StringBuilder("  0\n" + type + "\n  5\n" + handle + "\n330\n" + owner + "\n");

            foreach (string group in groups.Split('|'))
            {
                int equals = group.IndexOf('=');
                text.Append(group.Substring(0, equals).PadLeft(3))
                    .Append('\n')
                    .Append(group.Substring(equals + 1))
                    .Append('\n');
            }

            return text.ToString();
        }

        private static string Handle(DwgAnswerKey.Record record) => record.Key.Substring(record.Key.IndexOf(':') + 1);

        private static List<string> Pointers(byte[] dwg, string type, string handle, int code, int after = 0)
        {
            DwgAnswerKey.Record record = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Single(r => Handle(r) == handle);
            Assert.Equal(type, record.Type);
            return record.Groups.Skip(after).Where(g => g.Key == code).Select(g => g.Value.Trim()).ToList();
        }

        #region A stretch action

        private static string StretchDrawing(out string action, out string[] edges)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                string.Empty,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -10,
                null
            );
            block.AddStretchAction(width, new GeoPoint2(90, -5), new GeoPoint2(110, 55), lines);
            string text = db.ToText().Replace("\r\n", "\n");
            DwgAnswerKey.Record stretch = DwgAnswerKey
                .Parse(Encoding.UTF8.GetBytes(text))
                .Single(r => r.Type == "BLOCKSTRETCHACTION");
            action = Handle(stretch);
            edges = stretch.Groups.Where(g => g.Key == 331).Select(g => g.Value.Trim()).ToArray();
            return text;
        }

        /// <summary>The text with the bindings of the stretch action (the 331, the 74 and the 94s of each) in the reverse order.</summary>
        private static string Reversed(string text)
        {
            int record = text.IndexOf("\nBLOCKSTRETCHACTION\n", StringComparison.Ordinal);
            var binding = new Regex(@"331\n[0-9A-Fa-f]+\n 74\n *\d+\n( 94\n *\d+\n)*");
            Match first = binding.Match(text, record);
            int end = first.Index;
            var all = new List<string>();

            for (Match m = first; m.Success && m.Index == end; m = binding.Match(text, end))
            {
                all.Add(m.Value);
                end = m.Index + m.Length;
            }

            all.Reverse();
            return text.Substring(0, first.Index) + string.Concat(all) + text.Substring(end);
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1024")]
        public void TheEntitiesOfAStretchActionOutOfHandleOrder_AreReadBackInTheOrderTheFileKeeps(string version)
        {
            string text = Reversed(StretchDrawing(out string action, out string[] edges));
            byte[] dwg = DwgWriter.ToDwg(text, version);
            List<string> back = Pointers(dwg, "BLOCKSTRETCHACTION", action, 331);

            Assert.Equal(3, edges.Length);
            Assert.NotEqual(edges.OrderBy(e => Convert.ToUInt64(e, 16)), edges.Reverse());
            Assert.Equal(edges.Reverse(), back);
        }

        #endregion

        #region A group of constraints

        private static string Group(string dictionary, string owned) =>
            Object(
                "ACDBASSOCNETWORK",
                "F300",
                dictionary,
                "100=AcDbAssocAction|90=1|90=0|330=0|360=0|90=1|90=0|90=0|100=AcDbAssocNetwork|90=0|90=12|90=1|360=F301|90=0"
            )
            + Object(
                "ACDBASSOC2DCONSTRAINTGROUP",
                "F301",
                "F300",
                "100=AcDbAssocAction|90=1|90=0|330=F300|360=0|90=3|90=7|90=0"
                    + "|90=2|70=0|10=0.0|20=0.0|30=0.0|10=1.0|20=0.0|30=0.0|10=0.0|20=1.0|30=0.0"
                    + "|360=0|90=3"
                    + owned
                    + "|90=8|90=2"
                    + "|1=AcConstrainedImplicitPoint|330=F301|90=2|70=0|90=1|90=3|330=0|90=0|280=1|90=-1|90=1"
                    + "|1=AcFixedConstraint|330=F301|90=5|70=0|90=1|90=1|90=0|290=0|290=1"
            )
            + string.Concat(
                new[] { "F303", "F304", "F305" }.Select(h =>
                    Object(
                        "ACDBASSOCDEPENDENCY",
                        h,
                        "F301",
                        "100=AcDbAssocDependency|90=2|90=0|290=1|290=0|290=1|290=1|90=0|330=0|290=0|330=0|330=0|360=0|90=15"
                    )
                )
            );

        private static string GroupDrawing(string owned)
        {
            string text = StretchDrawing(out _, out _);
            const string classes =
                "  0\nCLASS\n  1\nACDBASSOCNETWORK\n  2\nAcDbAssocNetwork\n  3\nObjectDBX Classes\n 90\n1025\n280\n0\n281\n0\n"
                + "  0\nCLASS\n  1\nACDBASSOC2DCONSTRAINTGROUP\n  2\nAcDbAssoc2dConstraintGroup\n  3\nObjectDBX Classes\n 90\n1025\n280\n0\n281\n0\n"
                + "  0\nCLASS\n  1\nACDBASSOCDEPENDENCY\n  2\nAcDbAssocDependency\n  3\nObjectDBX Classes\n 90\n1025\n280\n0\n281\n0\n";
            int at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("CLASSES", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Insert(at, classes);
            at = text.IndexOf(
                "  0\nENDSEC",
                text.IndexOf("  2\nOBJECTS", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return text.Insert(
                at,
                Group(
                    Handle(DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(text)).First(r => r.Type == "DICTIONARY")),
                    owned
                )
            );
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1024")]
        public void TheActionsAGroupOwns_AreWrittenInTheOrderTheGroupsGive(string version)
        {
            // The three dependencies it owns, given with their handles neither ascending nor descending.
            string text = GroupDrawing("|360=F304|360=F305|360=F303");
            DwgDrawing drawing = DwgDrawing.Parse(text, null);
            byte[] dwg = DwgFileWriter.Write(drawing, version, out DwgFileWriter writer);

            Assert.DoesNotContain("ACDBASSOC2DCONSTRAINTGROUP", writer.LeftOut.Keys);
            Assert.Equal(
                new[] { "F304", "F305", "F303" },
                DwgAnswerKey
                    .Parse(DwgReader.ToDxf(dwg))
                    .Single(r => Handle(r) == "F301")
                    .Groups.Where(g => g.Key == 360 && g.Value.Trim().StartsWith("F30", StringComparison.Ordinal))
                    .Select(g => g.Value.Trim())
            );
        }

        #endregion
    }
}
