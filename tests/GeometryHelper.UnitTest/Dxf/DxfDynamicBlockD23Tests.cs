using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The dynamic properties of an INSERT of a dynamic block, read as AutoCAD saved them (D23): each parameter of the block's
    /// evaluation graph gives its properties, their values those the INSERT's representation data holds, or the block's own
    /// where it holds none. Each drawing is the R2018 file of ezdxf with a dynamic block PANEL written into it in the form
    /// AutoCAD writes one, its names and numbers made up, read as a file is.
    /// </summary>
    public class DxfDynamicBlockD23Tests
    {
        private const double Close = 1E-9;

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static string P(int code, double x, double y) => G(code, x) + G(code + 10, y) + G(code + 20, 0.0);

        /// <summary>A node of the evaluation graph: its id and the record of its object, handle and all.</summary>
        private sealed class Node
        {
            internal Node(string handle, int id, string record)
            {
                Handle = handle;
                Id = id;
                Record = record;
            }

            internal string Handle { get; }

            internal int Id { get; }

            internal string Record { get; }
        }

        /// <summary>The groups every block element begins with: its object's head, its node id and its name.</summary>
        private static string Element(string type, string handle, int id, string name)
            => G(0, type) + G(5, handle) + G(330, "1030") + G(100, "AcDbEvalExpr") + G(90, id.ToString(CultureInfo.InvariantCulture)) + G(98, "33") + G(99, "329")
               + G(100, "AcDbBlockElement") + G(300, name) + G(98, "33") + G(99, "329") + G(1071, "0");

        private static string Parameter() => G(100, "AcDbBlockParameter") + G(280, "1") + G(281, "0");

        private static string TwoPoints(double bx, double by, double ex, double ey)
            => G(100, "AcDbBlock2PtParameter") + P(1010, bx, by) + P(1011, ex, ey) + G(170, "4") + G(91, "0") + G(91, "0") + G(91, "0") + G(91, "0")
               + G(171, "0") + G(172, "0") + G(173, "0") + G(174, "0") + G(177, "0");

        private static string OnePoint(double x, double y) => G(100, "AcDbBlock1PtParameter") + P(1010, x, y) + G(93, "0") + G(170, "0") + G(171, "0");

        private static string ValueSet(int descCode, int flagsCode, int minCode, int countCode, double[] values)
        {
            var text = new StringBuilder(G(descCode, string.Empty) + G(flagsCode, "1") + G(minCode, 0.0) + G(minCode + 1, 0.0) + G(minCode + 2, 0.0)
                + G(countCode, values.Length.ToString(CultureInfo.InvariantCulture)));

            foreach (double value in values)
            {
                text.Append(G(minCode + 3, value));
            }

            return text.ToString();
        }

        private static Node Linear(string handle, int id, string name, string description, double bx, double by, double ex, double ey, params double[] list)
            => new Node(handle, id, Element("BLOCKLINEARPARAMETER", handle, id, "Linear") + Parameter() + TwoPoints(bx, by, ex, ey)
                + G(100, "AcDbBlockLinearParameter") + G(305, name) + G(306, description) + G(140, 7.5) + ValueSet(307, 96, 141, 175, list));

        private static Node Rotation(string handle, int id, string name, double ex, double ey, double ax, double ay)
            => new Node(handle, id, Element("BLOCKROTATIONPARAMETER", handle, id, "Rotation") + Parameter() + TwoPoints(0, 0, ex, ey)
                + G(100, "AcDbBlockRotationParameter") + G(305, name) + G(306, "Turn of the panel") + P(1011, ax, ay) + G(140, 1.0) + ValueSet(307, 96, 141, 175, new double[0]));

        private static Node Polar(string handle, int id, double bx, double by, double ex, double ey)
            => new Node(handle, id, Element("BLOCKPOLARPARAMETER", handle, id, "Polar") + Parameter() + TwoPoints(bx, by, ex, ey)
                + G(100, "AcDbBlockPolarParameter") + G(305, "Reach") + G(306, "How far it reaches") + G(307, "Swing") + G(308, "Which way it reaches") + G(140, 1.0)
                + ValueSet(309, 96, 141, 175, new double[0]) + ValueSet(410, 97, 145, 176, new double[0]));

        private static Node Xy(string handle, int id, double ex, double ey)
            => new Node(handle, id, Element("BLOCKXYPARAMETER", handle, id, "XY") + Parameter() + TwoPoints(0, 0, ex, ey)
                + G(100, "AcDbBlockXYParameter") + G(305, "Rise") + G(306, "Run") + G(307, "Height of the rise") + G(308, "Length of the run") + G(141, 1.0) + G(140, 1.0)
                + ValueSet(309, 97, 146, 176, new double[0]) + ValueSet(410, 96, 142, 175, new[] { 20.0, 40.0, 60.0 }));

        private static Node Point(string handle, int id, double x, double y)
            => new Node(handle, id, Element("BLOCKPOINTPARAMETER", handle, id, "Point") + Parameter() + OnePoint(x, y)
                + G(100, "AcDbBlockPointParameter") + G(303, "Anchor") + G(304, "Where the panel hangs") + P(1011, x + 5, y + 5));

        private static Node Flip(string handle, int id)
            => new Node(handle, id, Element("BLOCKFLIPPARAMETER", handle, id, "Flip") + Parameter() + TwoPoints(0, 0, 0, 100)
                + G(100, "AcDbBlockFlipParameter") + G(305, "Hand") + G(306, "Which hand the panel opens to") + G(307, "Left") + G(308, "Right")
                + P(1012, 10, 50) + G(309, "UpdatedFlip") + G(96, "2"));

        private static Node Visibility(string handle, int id)
            => new Node(handle, id, Element("BLOCKVISIBILITYPARAMETER", handle, id, "Visibility State") + Parameter() + OnePoint(5, 5)
                + G(100, "AcDbBlockVisibilityParameter") + G(281, "1") + G(301, "Look") + G(302, "How the panel looks") + G(91, "0")
                + G(93, "1") + G(331, "1004") + G(92, "3")
                + G(303, "Open") + G(94, "1") + G(332, "1004") + G(95, "0")
                + G(303, "Closed") + G(94, "0") + G(95, "0")
                + G(303, "Hidden") + G(94, "0") + G(95, "0"));

        private static Node Lookup(string handle, int id, int action)
            => new Node(handle, id, Element("BLOCKLOOKUPPARAMETER", handle, id, "Lookup") + Parameter() + OnePoint(600, 10)
                + G(100, "AcDbBlockLookUpParameter") + G(303, "Size") + G(304, "Size of the panel") + G(94, action.ToString(CultureInfo.InvariantCulture)));

        /// <summary>The lookup table of three rows: the width (node 7) to look up from, and the size (node 20) it gives.</summary>
        private static Node LookupAction(string handle, int id, bool reverse)
            => new Node(handle, id, Element("BLOCKLOOKUPACTION", handle, id, "Lookup1") + G(100, "AcDbBlockAction") + G(70, "0") + G(71, "0") + P(1010, 600, 0)
                + G(100, "AcDbBlockLookupAction") + G(92, "3") + G(93, "2") + G(301, string.Empty)
                + G(302, "500") + G(302, "Small") + G(302, "750") + G(302, "Medium") + G(302, "1000") + G(302, "Large")
                + G(303, string.Empty) + G(94, "7") + G(95, "40") + G(96, "2") + G(282, "0") + G(305, "Custom") + G(281, "0") + G(304, "UpdatedDistance")
                + G(303, string.Empty) + G(94, "20") + G(95, "1") + G(96, "0") + G(282, "1") + G(305, "Custom") + G(281, reverse ? "1" : "0") + G(304, "lookupString")
                + G(280, "1"));

        /// <summary>The data an INSERT keeps of a node: the head AutoCAD writes, then the node's own groups.</summary>
        private static KeyValuePair<int, string> Cache(int id, string groups)
            => new KeyValuePair<int, string>(id, G(1071, "18597260") + G(1071, "25303744") + G(70, "25") + G(70, "104") + groups);

        private static string Two(double bx, double by, double ex, double ey) => P(10, bx, by) + P(10, ex, ey) + G(10, 0.0) + G(20, 0.0) + G(30, -1.0);

        /// <summary>
        /// The R2018 drawing of ezdxf with the dynamic block PANEL, its graph the nodes given, a line in it; an INSERT of PANEL
        /// itself at the origin (handle 1020), and one (handle 1021) of the anonymous block *U8 standing for it, scaled as
        /// given, its representation data holding the caches given.
        /// </summary>
        private static DxfDatabase Drawing(Node[] nodes, KeyValuePair<int, string>[] caches, double sx = 1, double sy = 1, double sz = 1)
        {
            string records =
                G(0, "BLOCK_RECORD") + G(5, "1001") + G(102, "{ACAD_XDICTIONARY") + G(360, "1002") + G(102, "}") + G(330, "9") + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbBlockTableRecord") + G(2, "PANEL") + G(340, "0") + G(70, "4") + G(280, "1") + G(281, "0")
                + G(1001, "AcDbDynamicBlockTrueName") + G(1000, "PANEL") + G(1001, "AcDbDynamicBlockGUID") + G(1000, "{0D23D23D-0000-4000-8000-00000000D023}")
                + G(0, "BLOCK_RECORD") + G(5, "1010") + G(330, "9") + G(100, "AcDbSymbolTableRecord") + G(100, "AcDbBlockTableRecord") + G(2, "*U8") + G(340, "0")
                + G(70, "0") + G(280, "1") + G(281, "0") + G(1001, "AcDbBlockRepBTag") + G(1070, "1") + G(1005, "1001");
            string blocks =
                G(0, "BLOCK") + G(5, "1003") + G(330, "1001") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbBlockBegin") + G(2, "PANEL") + G(70, "0") + P(10, 0, 0) + G(3, "PANEL") + G(1, string.Empty)
                + G(0, "LINE") + G(5, "1004") + G(330, "1001") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbLine") + P(10, 0, 0) + P(11, 500, 0)
                + G(0, "ENDBLK") + G(5, "1005") + G(330, "1001") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbBlockEnd")
                + G(0, "BLOCK") + G(5, "1011") + G(330, "1010") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbBlockBegin") + G(2, "*U8") + G(70, "1") + P(10, 0, 0) + G(3, "*U8") + G(1, string.Empty)
                + G(0, "LINE") + G(5, "1012") + G(330, "1010") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbLine") + P(10, 0, 0) + P(11, 750, 0)
                + G(0, "ENDBLK") + G(5, "1013") + G(330, "1010") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbBlockEnd");
            string inserts =
                G(0, "INSERT") + G(5, "1020") + G(330, "17") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbBlockReference") + G(2, "PANEL") + P(10, 0, 0)
                + G(0, "INSERT") + G(5, "1021") + G(102, "{ACAD_XDICTIONARY") + G(360, "1022") + G(102, "}") + G(330, "17") + G(100, "AcDbEntity") + G(8, "0")
                + G(100, "AcDbBlockReference") + G(2, "*U8") + P(10, 1000, 0) + G(41, sx) + G(42, sy) + G(43, sz);
            var objects = new StringBuilder();
            objects.Append(G(0, "DICTIONARY") + G(5, "1002") + G(330, "1001") + G(100, "AcDbDictionary") + G(280, "1") + G(281, "1") + G(3, "ACAD_ENHANCEDBLOCK") + G(360, "1030"));
            objects.Append(G(0, "ACAD_EVALUATION_GRAPH") + G(5, "1030") + G(330, "1002") + G(100, "AcDbEvalGraph") + G(96, "99") + G(97, "99"));

            for (int i = 0; i < nodes.Length; i++)
            {
                objects.Append(G(91, i.ToString(CultureInfo.InvariantCulture)) + G(93, "32") + G(95, nodes[i].Id.ToString(CultureInfo.InvariantCulture)) + G(360, nodes[i].Handle)
                    + G(92, "-1") + G(92, "-1") + G(92, "-1") + G(92, "-1"));
            }

            foreach (Node node in nodes)
            {
                objects.Append(node.Record);
            }

            objects.Append(G(0, "DICTIONARY") + G(5, "1022") + G(330, "1021") + G(100, "AcDbDictionary") + G(280, "1") + G(281, "1") + G(3, "AcDbBlockRepresentation") + G(360, "1023"));
            objects.Append(G(0, "DICTIONARY") + G(5, "1023") + G(330, "1022") + G(100, "AcDbDictionary") + G(280, "1") + G(281, "1") + G(3, "AcDbRepData") + G(360, "1024")
                + G(3, "AppDataCache") + G(360, "1025"));
            objects.Append(G(0, "ACDB_BLOCKREPRESENTATION_DATA") + G(5, "1024") + G(330, "1023") + G(100, "AcDbBlockRepresentationData") + G(70, "1") + G(340, "1001"));
            objects.Append(G(0, "DICTIONARY") + G(5, "1025") + G(330, "1023") + G(100, "AcDbDictionary") + G(280, "1") + G(281, "1") + G(3, "ACAD_ENHANCEDBLOCKDATA") + G(360, "1026"));
            objects.Append(G(0, "DICTIONARY") + G(5, "1026") + G(330, "1025") + G(100, "AcDbDictionary") + G(280, "1") + G(281, "1"));

            for (int i = 0; i < caches.Length; i++)
            {
                objects.Append(G(3, caches[i].Key.ToString(CultureInfo.InvariantCulture)) + G(360, (0x1040 + i).ToString("X", CultureInfo.InvariantCulture)));
            }

            for (int i = 0; i < caches.Length; i++)
            {
                objects.Append(G(0, "XRECORD") + G(5, (0x1040 + i).ToString("X", CultureInfo.InvariantCulture)) + G(330, "1026") + G(100, "AcDbXrecord") + G(280, "1") + caches[i].Value);
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\n2000\n")
                .Replace("  0\nENDTAB\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n", records + "  0\nENDTAB\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n", blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", inserts + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n");
            return DxfDatabase.Parse(text);
        }

        private static DxfBlockReference Insert(DxfDatabase db, string handle) => db.ModelSpace.OfType<DxfBlockReference>().Single(r => r.Handle == handle);

        private static DxfDynamicBlockReferenceProperty Named(DxfBlockReference insert, string name)
            => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        private static double Real(DxfDynamicBlockReferenceProperty property) => Assert.IsType<double>(property.Value);

        [Fact]
        public void ALinearParameterGivesTheDistanceTheInsertSaved_AndTheBlocksOwnWhereItSavedNone()
        {
            DxfDatabase db = Drawing(new[] { Linear("1031", 7, "Width", "Width of the panel", 0, 0, 500, 0) }, new[] { Cache(7, Two(0, 0, 750, 0)) });

            DxfDynamicBlockReferenceProperty saved = Assert.Single(Insert(db, "1021").DynamicBlockReferencePropertyCollection);
            Assert.Equal("Width", saved.PropertyName);
            Assert.Equal(750.0, Real(saved), Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Distance, saved.UnitsType);
            Assert.Equal(1, saved.PropertyTypeCode);
            Assert.False(saved.ReadOnly);
            Assert.Equal("Width of the panel", saved.Description);
            Assert.Empty(saved.GetAllowedValues());

            DxfDynamicBlockReferenceProperty own = Assert.Single(Insert(db, "1020").DynamicBlockReferencePropertyCollection);
            Assert.Equal(500.0, Real(own), Close);
        }

        [Fact]
        public void AnInsertScaledAlikeScalesItsDistances_AndOneScaledUnlikeHasNoProperties()
        {
            Node[] nodes = { Linear("1031", 7, "Width", string.Empty, 0, 0, 500, 0, 250, 500, 750) };
            KeyValuePair<int, string>[] caches = { Cache(7, Two(0, 0, 750, 0)) };

            DxfDynamicBlockReferenceProperty doubled = Named(Insert(Drawing(nodes, caches, 2, 2, 2), "1021"), "Width");
            Assert.Equal(1500.0, Real(doubled), Close);
            Assert.Equal(new object[] { 500.0, 1000.0, 1500.0 }, doubled.GetAllowedValues());

            Assert.Equal(1500.0, Real(Named(Insert(Drawing(nodes, caches, -2, 2, 2), "1021"), "Width")), Close);
            Assert.Empty(Insert(Drawing(nodes, caches, 2, 3, 2), "1021").DynamicBlockReferencePropertyCollection);
        }

        [Fact]
        public void AValueListIsGivenAsTheAllowedValues()
        {
            DxfDatabase db = Drawing(new[] { Linear("1031", 7, "Width", string.Empty, 0, 0, 500, 0, 250, 500, 750) }, new KeyValuePair<int, string>[0]);

            Assert.Equal(new object[] { 250.0, 500.0, 750.0 }, Named(Insert(db, "1020"), "Width").GetAllowedValues());
            Assert.Equal(new object[] { 250.0, 500.0, 750.0 }, Named(Insert(db, "1021"), "Width").GetAllowedValues());
        }

        [Fact]
        public void AVisibilityParameterGivesTheSavedState_AndTheFirstStateWhereNoneIsSaved()
        {
            DxfDatabase db = Drawing(new[] { Visibility("1031", 1) }, new[] { Cache(1, P(10, 5, 5) + G(1, "Closed")) });

            DxfDynamicBlockReferenceProperty saved = Named(Insert(db, "1021"), "Look");
            Assert.Equal("Closed", saved.Value);
            Assert.Equal(5, saved.PropertyTypeCode);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.NoUnits, saved.UnitsType);
            Assert.Equal("How the panel looks", saved.Description);
            Assert.Equal(new object[] { "Open", "Closed", "Hidden" }, saved.GetAllowedValues());
            Assert.Equal("Open", Named(Insert(db, "1020"), "Look").Value);
        }

        [Fact]
        public void AFlipParameterGivesItsStateAsAShort()
        {
            DxfDatabase db = Drawing(new[] { Flip("1031", 3) }, new[] { Cache(3, Two(0, 0, 0, 100) + G(70, "1")) });

            DxfDynamicBlockReferenceProperty saved = Named(Insert(db, "1021"), "Hand");
            Assert.Equal((short)1, saved.Value);
            Assert.Equal(3, saved.PropertyTypeCode);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.NoUnits, saved.UnitsType);
            Assert.Equal("Which hand the panel opens to", saved.Description);
            Assert.Equal(new object[] { (short)0, (short)1 }, saved.GetAllowedValues());
            Assert.Equal((short)0, Named(Insert(db, "1020"), "Hand").Value);
        }

        [Fact]
        public void ARotationParameterGivesTheAngleFromItsBaseAngle_InRadians()
        {
            double c = 50 / Math.Sqrt(2);
            DxfDatabase db = Drawing(new[] { Rotation("1031", 4, "Turn", 0, 50, 50, 0) }, new[] { Cache(4, Two(0, 0, c, c) + P(10, 50, 0)) });

            DxfDynamicBlockReferenceProperty saved = Named(Insert(db, "1021"), "Turn");
            Assert.Equal(Math.PI / 4, Real(saved), Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Angular, saved.UnitsType);
            Assert.Equal("Turn of the panel", saved.Description);
            Assert.Equal(Math.PI / 2, Real(Named(Insert(db, "1020"), "Turn")), Close);

            DxfDatabase back = Drawing(new[] { Rotation("1031", 4, "Turn", 0, 50, 50, 0) }, new[] { Cache(4, Two(0, 0, c, -c) + P(10, 50, 0)) });
            Assert.Equal(7 * Math.PI / 4, Real(Named(Insert(back, "1021"), "Turn")), Close);
        }

        [Fact]
        public void APointParameterGivesItsPositionAsAnXAndAY()
        {
            DxfDatabase db = Drawing(new[] { Point("1031", 5, 10, 20) }, new[] { Cache(5, P(10, 15, -5)) });

            Assert.Equal(new[] { "Anchor X", "Anchor Y" }, Insert(db, "1021").DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(15.0, Real(Named(Insert(db, "1021"), "Anchor X")), Close);
            Assert.Equal(-5.0, Real(Named(Insert(db, "1021"), "Anchor Y")), Close);
            Assert.Equal("Where the panel hangs", Named(Insert(db, "1021"), "Anchor Y").Description);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Distance, Named(Insert(db, "1021"), "Anchor X").UnitsType);
            Assert.Equal(10.0, Real(Named(Insert(db, "1020"), "Anchor X")), Close);
            Assert.Equal(20.0, Real(Named(Insert(db, "1020"), "Anchor Y")), Close);
        }

        [Fact]
        public void AnXYParameterGivesItsRunAndItsRise_EachWithItsOwnList()
        {
            DxfDatabase db = Drawing(new[] { Xy("1031", 6, 40, 30) }, new[] { Cache(6, Two(0, 0, 60, -25)) });

            DxfBlockReference insert = Insert(db, "1021");
            Assert.Equal(new[] { "Run", "Rise" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(60.0, Real(Named(insert, "Run")), Close);
            Assert.Equal(25.0, Real(Named(insert, "Rise")), Close);
            Assert.Equal("Length of the run", Named(insert, "Run").Description);
            Assert.Equal("Height of the rise", Named(insert, "Rise").Description);
            Assert.Equal(new object[] { 20.0, 40.0, 60.0 }, Named(insert, "Run").GetAllowedValues());
            Assert.Empty(Named(insert, "Rise").GetAllowedValues());
            Assert.Equal(40.0, Real(Named(Insert(db, "1020"), "Run")), Close);
            Assert.Equal(30.0, Real(Named(Insert(db, "1020"), "Rise")), Close);
        }

        [Fact]
        public void APolarParameterGivesItsDistanceAndItsAngle()
        {
            DxfDatabase db = Drawing(new[] { Polar("1031", 8, 0, 0, 30, 40) }, new[] { Cache(8, Two(10, 10, 10, 30) + P(10, 20, 10)) });

            DxfBlockReference insert = Insert(db, "1021");
            Assert.Equal(new[] { "Reach", "Swing" }, insert.DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(20.0, Real(Named(insert, "Reach")), Close);
            Assert.Equal(Math.PI / 2, Real(Named(insert, "Swing")), Close);
            Assert.Equal(DxfDynamicBlockReferencePropertyUnitsType.Angular, Named(insert, "Swing").UnitsType);
            Assert.Equal("Which way it reaches", Named(insert, "Swing").Description);
            Assert.Equal(50.0, Real(Named(Insert(db, "1020"), "Reach")), Close);
            Assert.Equal(Math.Atan2(40, 30), Real(Named(Insert(db, "1020"), "Swing")), Close);
        }

        [Fact]
        public void ALookupParameterGivesTheSavedValue_TheValuesOfItsColumn_AndIsReadOnlyWithoutReverseLookup()
        {
            Node[] nodes = { Linear("1031", 7, "Width", string.Empty, 0, 0, 500, 0), Lookup("1032", 20, 24), LookupAction("1033", 24, true) };
            DxfDatabase db = Drawing(nodes, new[] { Cache(7, Two(0, 0, 750, 0)), Cache(20, P(10, 600, 10) + G(1, "Medium")) });

            DxfDynamicBlockReferenceProperty size = Named(Insert(db, "1021"), "Size");
            Assert.Equal("Medium", size.Value);
            Assert.Equal(5, size.PropertyTypeCode);
            Assert.Equal("Size of the panel", size.Description);
            Assert.False(size.ReadOnly);
            Assert.Equal(new object[] { "Small", "Medium", "Large" }, size.GetAllowedValues());

            DxfDynamicBlockReferenceProperty unmatched = Named(Insert(db, "1020"), "Size");
            Assert.Equal("Custom", unmatched.Value);
            Assert.Equal(new object[] { "Small", "Medium", "Large", "Custom" }, unmatched.GetAllowedValues());

            nodes[2] = LookupAction("1033", 24, false);
            Assert.True(Named(Insert(Drawing(nodes, new KeyValuePair<int, string>[0]), "1020"), "Size").ReadOnly);
        }

        [Fact]
        public void ThePropertiesComeInTheOrderOfTheGraph_AndAnythingElseInItGivesNone()
        {
            Node move = new Node("1039", 30, Element("BLOCKMOVEACTION", "1039", 30, "Move") + G(100, "AcDbBlockAction") + G(70, "0") + G(71, "0") + P(1010, 0, 0)
                + G(100, "AcDbBlockMoveAction") + G(92, "5") + G(301, "XDelta") + G(93, "5") + G(302, "YDelta") + G(140, 1.0) + G(141, 0.0) + G(280, "0"));
            Node basePoint = new Node("103A", 31, Element("BLOCKBASEPOINTPARAMETER", "103A", 31, "Base Point") + Parameter() + OnePoint(0, 0)
                + G(100, "AcDbBlockBasepointParameter") + P(1011, 0, 0) + P(1012, 0, 0));
            Node[] nodes =
            {
                Visibility("1031", 1), Flip("1032", 3), move, Rotation("1033", 4, "Turn", 0, 50, 50, 0), Point("1034", 5, 10, 20), basePoint, Xy("1035", 6, 40, 30),
                Linear("1036", 7, "Width", string.Empty, 0, 0, 500, 0), Polar("1037", 8, 0, 0, 30, 40), Lookup("1038", 20, 24), LookupAction("103B", 24, true),
            };

            DxfDatabase db = Drawing(nodes, new KeyValuePair<int, string>[0]);

            Assert.Equal(new[] { "Look", "Hand", "Turn", "Anchor X", "Anchor Y", "Run", "Rise", "Width", "Reach", "Swing", "Size" },
                Insert(db, "1020").DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
            Assert.Equal(new[] { "Look", "Hand", "Turn", "Anchor X", "Anchor Y", "Run", "Rise", "Width", "Reach", "Swing", "Size" },
                Insert(db, "1021").DynamicBlockReferencePropertyCollection.Select(p => p.PropertyName));
        }

        [Fact]
        public void SavedDataTooShortToReadGivesTheBlocksOwnValue()
        {
            DxfDatabase db = Drawing(new[] { Linear("1031", 7, "Width", string.Empty, 0, 0, 500, 0), Visibility("1032", 1) },
                new[] { Cache(7, P(10, 0, 0)), Cache(1, P(10, 5, 5)) });

            Assert.Equal(500.0, Real(Named(Insert(db, "1021"), "Width")), Close);
            Assert.Equal("Open", Named(Insert(db, "1021"), "Look").Value);
        }

        [Fact]
        public void AnInsertOfNoDynamicBlockHasNoProperties()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            foreach (DxfBlockReference insert in db.ModelSpace.OfType<DxfBlockReference>())
            {
                Assert.Empty(insert.DynamicBlockReferencePropertyCollection);
            }

            Assert.Empty(new DxfBlockReference(new Geometry.GeoPoint3(0, 0, 0), db.BlockTable["BOLT"]).DynamicBlockReferencePropertyCollection);

            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            bolt.XData = new[] { new DxfTypedValue(1001, "AcDbDynamicBlockGUID"), new DxfTypedValue(1000, "{0D23D23D-0000-4000-8000-00000000D024}") };
            var dynamic = new DxfBlockReference(new Geometry.GeoPoint3(0, 0, 0), bolt);
            db.ModelSpace.AppendEntity(dynamic);
            Assert.True(dynamic.IsDynamicBlock);
            Assert.Empty(dynamic.DynamicBlockReferencePropertyCollection);
        }
    }
}
