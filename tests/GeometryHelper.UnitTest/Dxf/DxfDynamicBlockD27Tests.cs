using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The constraint parameters of D27, each as AutoCAD 2021 solved them once <c>AssocManager.EvaluateTopLevelNetwork</c> ran
    /// (plugin D27, AutoCAD's Dynamic Blocks samples set 36 times): a block whose geometric and dimensional constraints fix its
    /// geometry is solved from its own at the distance set, and one whose constraints leave geometry free that the distance
    /// would move is refused. The block of these tests is a rectangle 100 by 50 of four LINEs, its bottom left corner fixed,
    /// its sides held horizontal, perpendicular and parallel, its Width the horizontal distance of the bottom's ends and its
    /// Height the vertical distance from the bottom's start to the left side's start, as AutoCAD's Fluorescent sample holds
    /// its rectangles.
    /// </summary>
    public class DxfDynamicBlockD27Tests
    {
        internal const double Close = 1E-9;

        internal static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        internal static DxfBlockReference Insert(DxfDatabase db) => db.ModelSpace.OfType<DxfBlockReference>().Single();

        internal static DxfDynamicBlockReferenceProperty Property(DxfBlockReference insert, string name) => insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == name);

        internal static double Distance(DxfBlockReference insert, string name) => (double)Property(insert, name).Value;

        internal static List<DxfLine> Lines(DxfBlockReference insert) => insert.BlockTableRecord.OfType<DxfLine>().ToList();

        internal static string Real(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        internal static void Point(Groups b, int code, double x, double y)
        {
            b.Group(code, Real(x));
            b.Group(code + 10, Real(y));
            b.Group(code + 20, "0.0");
        }

        /// <summary>The objects of the graph's nodes of a block, in its order.</summary>
        internal static List<DxfItem> Nodes(DxfBlockTableRecord block)
        {
            DxfDBObject graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK");
            return graph.Item.Groups.Where(g => g.TypeCode == 360).Select(g => graph.Item.File.ObjectItem(g.Value)).ToList();
        }

        internal static DxfItem Node(DxfBlockTableRecord block, string type, string label)
            => Nodes(block).First(n => n.Type == type && n.Groups.Any(g => g.TypeCode == 305 && g.Value == label));

        internal static string IdOf(DxfItem node) => node.Groups.First(g => g.TypeCode == 90).Value.Trim();

        /// <summary>Rewrites the groups of an object by an edit of them all.</summary>
        internal static void Edit(DxfItem item, Action<List<KeyValuePair<int, string>>> edit)
        {
            DxfFile file = item.File;
            var groups = file.TailOf(item, 0).Select(g => new KeyValuePair<int, string>(g.TypeCode, g.Value)).ToList();
            edit(groups);
            file.ReplaceTail(item, 0, groups);
        }

        /// <summary>
        /// The rectangle RECT with Width and Height constraint parameters, as AutoCAD's Block Editor writes them: two linear
        /// parameters of the library made into a horizontal and a vertical constraint parameter, and the block's associative
        /// network holding the constraint group, the variables Width and Height and the dependencies that tie them. Leave the
        /// corner unfixed, or name the class of a constraint the library does not solve in place of the parallel one, where
        /// asked; give Height an expression of Width where asked, and a flip of the whole rectangle carrying Width where asked.
        /// </summary>
        internal static DxfDatabase Rectangle(bool fixedCorner = true, string parallel = "AcParallelConstraint", string heightExpression = "", bool flip = false, DxfBlockParameterValueSet widths = null, bool table = false)
        {
            var db = new DxfDatabase("AC1024");
            var block = new DxfBlockTableRecord { Name = "RECT", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };
            Array.ForEach(lines, l => block.AppendEntity(l));
            DxfItem widthNode;
            DxfItem heightNode;

            if (flip)
            {
                // The library writes a flip alone in its block, so the flip is written first and the constraint parameters
                // after it, as nodes of the graph written here.
                DxfBlockFlipParameter mirror = block.AddFlipParameter("Flip", string.Empty, new GeoPoint3(0, -20, 0), new GeoPoint3(0, 80, 0));
                block.AddFlipAction(mirror, lines);
                widthNode = Parameter(block, "BLOCKHORIZONTALCONSTRAINTPARAMETER", 100, "Width", 100, 0);
                heightNode = Parameter(block, "BLOCKVERTICALCONSTRAINTPARAMETER", 101, "Height", 0, 50);
            }
            else
            {
                DxfBlockLinearParameter width = block.AddLinearParameter("Width", string.Empty, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -10, widths);
                DxfBlockLinearParameter height = block.AddLinearParameter("Height", string.Empty, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 10, null);

                if (table)
                {
                    block.AddBlockPropertiesTable(new GeoPoint3(0, -30, 0), new[] { DxfBlockPropertiesTableColumn.Distance(width), DxfBlockPropertiesTableColumn.Distance(height) },
                        new[] { new DxfBlockPropertiesTableRow(100.0, 50.0), new DxfBlockPropertiesTableRow(200.0, 80.0) });
                }

                widthNode = Node(block, "BLOCKLINEARPARAMETER", "Width");
                heightNode = Node(block, "BLOCKLINEARPARAMETER", "Height");
            }

            DxfFile file = widthNode.File;

            if (flip)
            {
                // The flip carries Width (70 1, its id a 91) and holds it with its grips, as AutoCAD's Window sample's does.
                DxfItem action = Nodes(block).First(n => n.Type == "BLOCKFLIPACTION");
                string widthId = IdOf(widthNode);
                List<string> grips = widthNode.Groups.Where(g => g.TypeCode == 91).Select(g => g.Value.Trim()).Where(v => v != "0").ToList();
                List<string> gripHandles = Nodes(block).Where(n => n.Type == "BLOCKLINEARGRIP" && grips.Contains(IdOf(n))).Select(n => n.Handle).ToList();
                Edit(action, groups =>
                {
                    int at = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbBlockAction");
                    int count = groups.FindIndex(at, g => g.Key == 70);
                    groups[count] = new KeyValuePair<int, string>(70, "1");
                    groups.Insert(count + 1, new KeyValuePair<int, string>(91, widthId));
                    int held = groups.FindIndex(at, g => g.Key == 71);
                    int listed = int.Parse(groups[held].Value.Trim(), CultureInfo.InvariantCulture);
                    groups[held] = new KeyValuePair<int, string>(71, (listed + 1 + gripHandles.Count).ToString(CultureInfo.InvariantCulture));
                    groups.Insert(held + 1, new KeyValuePair<int, string>(330, widthNode.Handle));
                    groups.InsertRange(held + 2, gripHandles.Select(h => new KeyValuePair<int, string>(330, h)));
                });
            }

            // The network: its dictionary under the block's extension dictionary, the network, a group, variables, dependencies.
            DxfItem entry = block.ExtensionDictionary.Add("ACAD_ASSOCNETWORK", "DICTIONARY", b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(281, "1");
            });
            var entryDictionary = (DxfDBDictionary)DxfDBObject.OfObject(entry);
            DxfItem network = entryDictionary.Add("ACAD_ASSOCNETWORK", "ACDBASSOCNETWORK", b => b.Group(100, "AcDbAssocAction"));
            DxfItem group = Add(file, "ACDBASSOC2DCONSTRAINTGROUP", network.Handle, b => b.Group(100, "AcDbAssocAction"));
            DxfItem widthVariable = Variable(file, network.Handle, 2, "Width", string.Empty, 100);
            DxfItem heightVariable = Variable(file, network.Handle, 3, heightExpression.Length > 0 ? "H2" : "Height", heightExpression, 50);
            string[] geometry = lines.Select(l => Add(file, "ACDBASSOCGEOMDEPENDENCY", group.Handle, b =>
            {
                Dependency(b, l.Handle, "-10000");
                b.Group(100, "AcDbAssocGeomDependency");
                b.Group(90, "0");
                b.Group(290, "1");
                b.Group(1, "AcDbAssocSingleEdgePersSubentId");
                b.Group(290, "0");
            }).Handle).ToArray();
            string widthValue = Value(file, group.Handle, widthVariable.Handle, 100);
            string heightValue = Value(file, group.Handle, heightVariable.Handle, 50);
            string widthTie = Add(file, "ACDBASSOCDEPENDENCY", group.Handle, b => Dependency(b, widthNode.Handle, "0")).Handle;
            string heightTie = Add(file, "ACDBASSOCDEPENDENCY", group.Handle, b => Dependency(b, heightExpression.Length > 0 ? "0" : heightNode.Handle, "0")).Handle;

            Edit(network, groups =>
            {
                groups.AddRange(new[] { "90:1", "90:0", "330:0", "360:0", "90:1", "90:0", "90:0" }.Select(Pair));
                groups.Add(new KeyValuePair<int, string>(100, "AcDbAssocNetwork"));
                groups.AddRange(new[] { "90:0", "90:4", "90:3" }.Select(Pair));
                groups.Add(new KeyValuePair<int, string>(360, group.Handle));
                groups.Add(new KeyValuePair<int, string>(360, widthVariable.Handle));
                groups.Add(new KeyValuePair<int, string>(360, heightVariable.Handle));
                groups.Add(new KeyValuePair<int, string>(90, "0"));
            });

            // The nodes: the lines 1, 4, 7 and 10, each with its start and end after it; the datum line 13; the constraints.
            var nodes = new List<Action<Groups>>();

            for (int i = 0; i < 4; i++)
            {
                DxfLine line = lines[i];
                int id = 1 + 3 * i;
                string dependency = geometry[i];
                nodes.Add(b =>
                {
                    Head(b, "AcConstrainedBoundedLine", group.Handle, id);
                    b.Group(330, dependency);
                    b.Group(90, "0");
                    Point(b, 10, line.StartPoint.X, line.StartPoint.Y);
                    Point(b, 10, line.EndPoint.X - line.StartPoint.X, line.EndPoint.Y - line.StartPoint.Y);
                    b.Group(290, "0");
                    Point(b, 10, line.StartPoint.X, line.StartPoint.Y);
                    Point(b, 11, line.EndPoint.X, line.EndPoint.Y);
                });
                nodes.Add(b => Implicit(b, group.Handle, id + 1, 0, id));
                nodes.Add(b => Implicit(b, group.Handle, id + 2, 1, id));
            }

            nodes.Add(b =>
            {
                Head(b, "AcConstrainedDatumLine", group.Handle, 13);
                b.Group(330, "0");
                b.Group(90, "0");
                Point(b, 10, 0, 0);
                Point(b, 10, 1, 0);
            });
            nodes.Add(b => Rule(b, "AcPointCoincidenceConstraint", group.Handle, 20, 3, 5));
            nodes.Add(b => Rule(b, "AcPointCoincidenceConstraint", group.Handle, 21, 6, 8));
            nodes.Add(b => Rule(b, "AcPointCoincidenceConstraint", group.Handle, 22, 9, 11));
            nodes.Add(b => Rule(b, "AcPointCoincidenceConstraint", group.Handle, 23, 12, 2));
            nodes.Add(b =>
            {
                Rule(b, "AcHorizontalConstraint", group.Handle, 24, 1);
                b.Group(90, "13");
            });
            nodes.Add(b =>
            {
                Rule(b, "AcHorizontalConstraint", group.Handle, 25, 7);
                b.Group(90, "13");
            });
            nodes.Add(b => Rule(b, "AcPerpendicularConstraint", group.Handle, 26, 1, 4));
            nodes.Add(b => Rule(b, parallel, group.Handle, 27, 4, 10));

            if (fixedCorner)
            {
                nodes.Add(b => Rule(b, "AcFixedConstraint", group.Handle, 28, 2));
            }

            nodes.Add(b =>
            {
                Rule(b, "AcDistanceConstraint", group.Handle, 29, 2, 3);
                Distance(b, widthValue, widthTie, 1, 0);
            });
            nodes.Add(b =>
            {
                Rule(b, "AcDistanceConstraint", group.Handle, 30, 2, 11);
                Distance(b, heightValue, heightTie, 0, 1);
            });

            Edit(group, groups =>
            {
                groups.AddRange(new[] { "90:1", "90:0", "330:" + network.Handle, "360:0", "90:1", "90:31", "90:0", "90:2", "70:0" }.Select(Pair));
                groups.AddRange(new[] { "10:0.0", "20:0.0", "30:0.0", "10:1.0", "20:0.0", "30:0.0", "10:0.0", "20:1.0", "30:0.0", "360:0" }.Select(Pair));
                groups.Add(new KeyValuePair<int, string>(90, (geometry.Length + 4).ToString(CultureInfo.InvariantCulture)));
                groups.AddRange(geometry.Concat(new[] { widthValue, heightValue, widthTie, heightTie }).Select(h => new KeyValuePair<int, string>(360, h)));
                groups.Add(new KeyValuePair<int, string>(90, "31"));
                groups.Add(new KeyValuePair<int, string>(90, nodes.Count.ToString(CultureInfo.InvariantCulture)));
            });

            // The nodes are written after the group's head, each as its builder gives it.
            var written = new Groups();
            nodes.ForEach(n => n(written));
            Edit(group, groups => groups.AddRange(written));

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));

            // The two linear parameters made constraint parameters, of the same node ids and points.
            string text = db.ToText();

            if (!flip)
            {
                text = Retype(text, widthNode.Handle, "BLOCKHORIZONTALCONSTRAINTPARAMETER", widthTie);

                if (heightExpression.Length == 0)
                {
                    text = Retype(text, heightNode.Handle, "BLOCKVERTICALCONSTRAINTPARAMETER", heightTie);
                }
            }

            return DxfDatabase.Parse(text);
        }

        /// <summary>
        /// A constraint parameter of a type, an id and a name, from the origin to the point given, written as AutoCAD writes
        /// one and added to the block's graph as a node of its own.
        /// </summary>
        internal static DxfItem Parameter(DxfBlockTableRecord block, string type, int id, string name, double x, double y)
        {
            DxfItem graph = block.ExtensionDictionary.GetAt("ACAD_ENHANCEDBLOCK").Item;
            string ids = id.ToString(CultureInfo.InvariantCulture);
            DxfItem node = Add(graph.File, type, graph.Handle, b =>
            {
                b.Group(100, "AcDbEvalExpr");
                b.Group(90, ids);
                b.Group(98, "33");
                b.Group(99, "174");
                b.Group(100, "AcDbBlockElement");
                b.Group(300, name);
                b.Group(98, "33");
                b.Group(99, "174");
                b.Group(1071, "0");
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, 0, 0);
                Point(b, 1011, x, y);
                b.Group(170, "4");
                Array.ForEach(new[] { "91:0", "91:0", "91:0", "91:0", "171:0", "172:0", "173:0", "174:0", "177:0" }, s => b.Group(Pair(s).Key, Pair(s).Value));
                b.Group(100, "AcDbBlockConstraintParameter");
                b.Group(330, "0");
                b.Group(100, "AcDbBlockLinearConstraintParameter");
                b.Group(305, name);
                b.Group(306, string.Empty);
                Array.ForEach(new[] { "140:0.0", "307:", "96:0", "141:0.0", "142:0.0", "143:0.0", "175:0" }, s => b.Group(Pair(s).Key, Pair(s).Value));
                b.Group(100, type == "BLOCKHORIZONTALCONSTRAINTPARAMETER" ? "AcDbBlockHorizontalConstraintParameter" : "AcDbBlockVerticalConstraintParameter");
            });
            int count = graph.Groups.Count(g => g.TypeCode == 360);
            Edit(graph, groups => groups.AddRange(new[] { "91:" + count.ToString(CultureInfo.InvariantCulture), "93:32", "95:" + ids, "360:" + node.Handle, "92:-1", "92:-1", "92:-1", "92:-1" }.Select(Pair)));
            return node;
        }

        internal static KeyValuePair<int, string> Pair(string s)
        {
            int colon = s.IndexOf(':');
            return new KeyValuePair<int, string>(int.Parse(s.Substring(0, colon), CultureInfo.InvariantCulture), s.Substring(colon + 1));
        }

        /// <summary>The record of a parameter of the text made a constraint parameter of the type given: its type and its subclass marker.</summary>
        internal static string Retype(string text, string handle, string type, string tie)
        {
            string head = "  0\nBLOCKLINEARPARAMETER\n  5\n" + handle + "\n";
            int at = text.IndexOf(head, StringComparison.Ordinal);
            Assert.True(at >= 0);
            int end = text.IndexOf("\n  0\n", at + head.Length, StringComparison.Ordinal);
            string record = text.Substring(at, end - at).Replace("BLOCKLINEARPARAMETER", type)
                .Replace("AcDbBlockLinearParameter", "AcDbBlockConstraintParameter\n330\n" + tie + "\n100\nAcDbBlockLinearConstraintParameter");
            int data = record.IndexOf("\n1001\n", StringComparison.Ordinal);
            string marker = "\n100\n" + (type == "BLOCKHORIZONTALCONSTRAINTPARAMETER" ? "AcDbBlockHorizontalConstraintParameter" : "AcDbBlockVerticalConstraintParameter");
            record = data < 0 ? record + marker : record.Substring(0, data) + marker + record.Substring(data);
            return text.Substring(0, at) + record + text.Substring(end);
        }

        /// <summary>The groups of an object being written, in order.</summary>
        internal sealed class Groups : List<KeyValuePair<int, string>>
        {
            internal void Group(int code, string value) => Add(new KeyValuePair<int, string>(code, value));
        }

        /// <summary>Adds an object of the groups given at the end of the OBJECTS section, owned by the object given.</summary>
        internal static DxfItem Add(DxfFile file, string type, string owner, Action<Groups> fill)
        {
            var groups = new Groups();
            fill(groups);
            return file.AddObject(type, owner, b => groups.ForEach(g => b.Group(g.Key, g.Value)));
        }

        internal static DxfItem Variable(DxfFile file, string network, int id, string name, string expression, double value)
            => Add(file, "ACDBASSOCVARIABLE", network, b =>
            {
                b.Group(100, "AcDbAssocAction");
                Array.ForEach(new[] { "90:1", "90:0", "330:" + network, "360:0", "90:" + id.ToString(CultureInfo.InvariantCulture), "90:0", "90:0" }, s => b.Group(Pair(s).Key, Pair(s).Value));
                b.Group(100, "AcDbAssocVariable");
                b.Group(90, "2");
                b.Group(1, name);
                b.Group(1, expression);
                b.Group(1, "AcDbCalc:1.0");
                b.Group(1, string.Empty);
                b.Group(40, Real(value));
                b.Group(290, "0");
                b.Group(290, "0");
                b.Group(90, "0");
            });

        internal static void Dependency(Groups b, string target, string order)
        {
            b.Group(100, "AcDbAssocDependency");
            b.Group(90, "2");
            b.Group(90, "0");
            Array.ForEach(new[] { "1", "1", "1", "1" }, v => b.Group(290, v));
            b.Group(90, order);
            b.Group(330, target);
            b.Group(290, "0");
            b.Group(330, "0");
            b.Group(330, "0");
            b.Group(360, "0");
            b.Group(90, "1");
        }

        internal static string Value(DxfFile file, string group, string variable, double value)
            => Add(file, "ACDBASSOCVALUEDEPENDENCY", group, b =>
            {
                Dependency(b, variable, "0");
                b.Group(100, "AcDbAssocValueDependency");
                b.Group(90, "0");
                b.Group(1, string.Empty);
                b.Group(40, Real(value));
            }).Handle;

        internal static void Head(Groups b, string type, string group, int id, params int[] links)
        {
            b.Group(1, type);
            b.Group(330, group);
            b.Group(90, id.ToString(CultureInfo.InvariantCulture));
            b.Group(70, "4");
            b.Group(90, links.Length.ToString(CultureInfo.InvariantCulture));
            Array.ForEach(links, l => b.Group(90, l.ToString(CultureInfo.InvariantCulture)));
        }

        internal static void Implicit(Groups b, string group, int id, int type, int owner)
        {
            Head(b, "AcConstrainedImplicitPoint", group, id);
            b.Group(330, "0");
            b.Group(90, "0");
            b.Group(280, type.ToString(CultureInfo.InvariantCulture));
            b.Group(90, "-1");
            b.Group(90, owner.ToString(CultureInfo.InvariantCulture));
        }

        internal static void Rule(Groups b, string type, string group, int id, params int[] links)
        {
            Head(b, type, group, id, links);
            b.Group(90, "0");
            b.Group(290, "0");
            b.Group(290, "1");
        }

        internal static void Distance(Groups b, string value, string tie, double x, double y)
        {
            b.Group(340, value);
            b.Group(340, tie);
            b.Group(280, "1");
            Point(b, 10, x, y);
        }

        /// <summary>The lines of the INSERT's block, each its start and end, to the nearest 1E-9.</summary>
        internal static string Drawn(DxfBlockReference insert)
            => string.Join(" ", Lines(insert).Select(l => "(" + Round(l.StartPoint.X) + "," + Round(l.StartPoint.Y) + ")-(" + Round(l.EndPoint.X) + "," + Round(l.EndPoint.Y) + ")"));

        internal static string Round(double v) => Math.Round(v, 9).ToString("0.#########", CultureInfo.InvariantCulture);

        /// <summary>The two points of the record the INSERT's cache keeps of a node, and its normal's z.</summary>
        internal static string Cached(DxfBlockReference insert, string id)
        {
            var data = (DxfDBDictionary)((DxfDBDictionary)((DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation")).GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            var record = (DxfXrecord)data.GetAt(id);
            List<string> numbers = record.Data.Where(g => g.TypeCode == 10 || g.TypeCode == 20 || g.TypeCode == 30).Select(g => Round(double.Parse(g.Value, CultureInfo.InvariantCulture))).ToList();
            return string.Join(" ", numbers);
        }

        // ---- 1. solved ------------------------------------------------------------------------------------------------------

        [Fact]
        public void AFixedRectangle_WidthSet_MovesItsRightSideAlone_AsAutoCadSolvedItsFluorescentSample()
        {
            // AutoCAD 2021 drew its Fluorescent sample's rectangle 1000 by 500 at Width 1200 from (0, 0) to (1200, 500), the
            // fixed corner kept, to 3E-13 (plugin D27 p1); the library's rectangle the same way.
            DxfDatabase db = Rectangle();
            DxfBlockReference insert = Insert(db);
            Assert.Equal(100.0, Distance(insert, "Width"), Close);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal("(0,0)-(150,0) (150,0)-(150,50) (150,50)-(0,50) (0,50)-(0,0)", Drawn(insert));
            Assert.Equal(150.0, Distance(insert, "Width"), Close);
            Assert.Equal(50.0, Distance(insert, "Height"), Close);
            Assert.StartsWith("*U", insert.BlockTableRecord.Name, StringComparison.Ordinal);

            DxfBlockReference again = Insert(Reopened(db));
            Assert.Equal("(0,0)-(150,0) (150,0)-(150,50) (150,50)-(0,50) (0,50)-(0,0)", Drawn(again));
            Assert.Equal(150.0, Distance(again, "Width"), Close);
        }

        [Fact]
        public void AFixedRectangle_HeightSet_MovesItsTopAlone_AndTheCacheKeepsTheParametersPoints_AsAutoCadWroteThem()
        {
            // AutoCAD 2021 wrote the record of a vertical constraint parameter under its keys 758938381 and 1206874891, its
            // base point, its end point and the normal down (plugin D27 p1); the horizontal one's Width keeps its own points.
            DxfDatabase db = Rectangle();
            DxfBlockReference insert = Insert(db);
            string height = IdOf(Node(db.BlockTable["RECT"], "BLOCKVERTICALCONSTRAINTPARAMETER", "Height"));
            string width = IdOf(Node(db.BlockTable["RECT"], "BLOCKHORIZONTALCONSTRAINTPARAMETER", "Width"));

            Property(insert, "Height").Value = 80.0;

            Assert.Equal("(0,0)-(100,0) (100,0)-(100,80) (100,80)-(0,80) (0,80)-(0,0)", Drawn(insert));
            Assert.Equal("0 0 0 0 80 0 0 0 -1", Cached(insert, height));
            Assert.Equal("0 0 0 100 0 0 0 0 -1", Cached(insert, width));
            var data = (DxfDBDictionary)((DxfDBDictionary)((DxfDBDictionary)insert.ExtensionDictionary.GetAt("AcDbBlockRepresentation")).GetAt("AppDataCache")).GetAt("ACAD_ENHANCEDBLOCKDATA");
            Assert.Equal(new[] { "758938381", "1206874891" }, ((DxfXrecord)data.GetAt(height)).Data.Where(g => g.TypeCode == 1071).Select(g => g.Value.Trim()));
            Assert.Equal(new[] { "105960582", "676247459" }, ((DxfXrecord)data.GetAt(width)).Data.Where(g => g.TypeCode == 1071).Select(g => g.Value.Trim()));
        }

        [Fact]
        public void AWidthBelowItsValueSet_IsTakenToIt_AsAutoCadTookLength35OfItsHexHeadBoltTo50()
        {
            // AutoCAD 2021 took Length 35 of its Hex Head Bolt sample, of the minimum 50, to 50 (plugin D27 p2).
            DxfDatabase db = Rectangle(widths: DxfBlockParameterValueSet.Increments(60, 300, 10));
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 40.0;

            Assert.Equal(60.0, Distance(insert, "Width"), Close);
            Assert.Equal("(0,0)-(60,0) (60,0)-(60,50) (60,50)-(0,50) (0,50)-(0,0)", Drawn(insert));
        }

        [Fact]
        public void AVariableOfAnExpression_FollowsTheWidthSet_AsAutoCadWorkedOutItsHexHeadBoltsDiaOver2()
        {
            // AutoCAD 2021 worked d2 = DIA/2 of its Hex Head Bolt sample out at DIA 6 to 3 (plugin D27 p1); here H2 = Width/2.
            DxfDatabase db = Rectangle(heightExpression: "Width/2");
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal("(0,0)-(150,0) (150,0)-(150,75) (150,75)-(0,75) (0,75)-(0,0)", Drawn(insert));
        }

        [Fact]
        public void TheBlocksOwnWidthSet_LeavesItsGeometryAsTheBlockHasIt_BitForBit()
        {
            DxfDatabase db = Rectangle();
            DxfBlockReference insert = Insert(db);
            List<DxfLine> own = db.BlockTable["RECT"].OfType<DxfLine>().ToList();

            Property(insert, "Width").Value = 100.0;

            List<DxfLine> drawn = Lines(insert);
            Assert.Equal(own.Select(l => l.StartPoint), drawn.Select(l => l.StartPoint));
            Assert.Equal(own.Select(l => l.EndPoint), drawn.Select(l => l.EndPoint));
        }

        [Fact]
        public void AFlipCarryingWidth_MirrorsTheRectangleSolved_AndWidthsPoints_AsAutoCadFlippedItsWindowSample()
        {
            // AutoCAD 2021 drew its Window sample at DoorWidth 1200 the same whether set before Flip Window or after it, the
            // constraints solved first; and wrote DoorWidth's record mirrored, its normal up (plugin D27 p3).
            DxfDatabase db = Rectangle(flip: true);
            DxfBlockReference insert = Insert(db);
            string width = IdOf(Node(db.BlockTable["RECT"], "BLOCKHORIZONTALCONSTRAINTPARAMETER", "Width"));

            Property(insert, "Width").Value = 150.0;
            Property(insert, "Flip").Value = (short)1;

            Assert.Equal("(0,0)-(-150,0) (-150,0)-(-150,50) (-150,50)-(0,50) (0,50)-(0,0)", Drawn(insert));
            Assert.Equal(150.0, Distance(insert, "Width"), Close);
            Assert.Equal("0 0 0 -150 0 0 0 0 1", Cached(insert, width));

            DxfDatabase other = Rectangle(flip: true);
            DxfBlockReference first = Insert(other);
            Property(first, "Flip").Value = (short)1;
            Property(first, "Width").Value = 150.0;
            Assert.Equal(Drawn(insert), Drawn(first));
        }

        // ---- 2. refused -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ARectangleFreeToSlide_WidthSet_KeepsItsLeftSide_AsAutoCadKeptItsDoorElevationsLeftSide()
        {
            // AutoCAD 2021 drew its Door Elevation sample, whose constraints fix no point, at Width 800 with its left side kept
            // where the least change would move both sides (plugin D27 p2): Width's base point, on the left, stays where it is
            // (plugin D27b).
            DxfDatabase db = Rectangle(fixedCorner: false);
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal("(0,0)-(150,0) (150,0)-(150,50) (150,50)-(0,50) (0,50)-(0,0)", Drawn(insert));
            Assert.Equal(150.0, Distance(insert, "Width"), Close);
        }

        [Fact]
        public void AConstraintOfAClassTheLibraryDoesNotSolve_IsRefused()
        {
            DxfDatabase db = Rectangle(parallel: "AcSymmetricConstraint");
            DxfBlockReference insert = Insert(db);

            NotSupportedException refused = Assert.Throws<NotSupportedException>(() => Property(insert, "Width").Value = 150.0);
            Assert.Contains("AcSymmetricConstraint", refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowOfATableThatSetsAConstraintParameter_IsRefused_AsAutoCadLeftItsHexHeadBoltAsItWasAtARow()
        {
            // AutoCAD 2021 set DIA of its Hex Head Bolt sample from 10 to 6 by a row and left its constraints as they were
            // (plugin D27 p2), though it solved them at DIA 6 set alone.
            DxfDatabase db = Rectangle(table: true);
            DxfBlockReference insert = Insert(db);

            Assert.Throws<NotSupportedException>(() => Property(insert, "Block Table1").Value = (short)1);
            Assert.Equal(100.0, Distance(insert, "Width"), Close);

            Property(insert, "Width").Value = 150.0;
            Assert.Equal("(0,0)-(150,0) (150,0)-(150,50) (150,50)-(0,50) (0,50)-(0,0)", Drawn(insert));
        }

        // ---- 3. expressions -------------------------------------------------------------------------------------------------

        [Fact]
        public void AnExpression_IsEvaluated_AsAutoCadsParametersManagerWritesThem()
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Width"] = 1000, ["W3"] = 25, ["HdCr"] = 19.6 };
            Func<string, double> read = name => values[name];

            Assert.Equal(950.0, DxfConstraintExpression.Evaluate("width-W3*2", read), Close);
            Assert.Equal(4.9, DxfConstraintExpression.Evaluate("HdCr/4", read), Close);
            Assert.Equal(-4.0, DxfConstraintExpression.Evaluate("-2^2", read), Close);
            Assert.Equal(20.0, DxfConstraintExpression.Evaluate("(W3 - 5) * 1E0", read), Close);
            Assert.Throws<NotSupportedException>(() => DxfConstraintExpression.Evaluate("exp(1)", read));
            Assert.Throws<NotSupportedException>(() => DxfConstraintExpression.Evaluate("Width +", read));
        }
    }
}
