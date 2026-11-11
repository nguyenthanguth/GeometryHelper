using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfDynamicBlockD27Tests;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The constraint leftovers of D27, each as AutoCAD 2021 worked it (plugin D27b): the anonymous block of an INSERT set
    /// holds a copy of the block's associative network at the values set, through which alone AutoCAD sets the INSERT again;
    /// a value set is taken to twelve significant digits; geometry the constraints leave free keeps the parameter's base
    /// point; a group of R2018 is read; construction lines, angles of three points and functions of expressions are solved;
    /// and a constraint parameter a table to match holds reads as read only. The block of these tests is D27's rectangle 100
    /// by 50 of four LINEs, its bottom left corner fixed, its Width and its Height constraint parameters, its network owned by
    /// a network of the drawing as AutoCAD's are.
    /// </summary>
    public class DxfDynamicBlockD27bTests
    {
        private const string Lines150 = "(0,0)-(150,0) (150,0)-(150,50) (150,50)-(0,50) (0,50)-(0,0)";

        /// <summary>The network of a block or of an anonymous block, under ACAD_ASSOCNETWORK of its extension dictionary; null where it has none.</summary>
        private static DxfItem NetworkOf(DxfBlockTableRecord block)
        {
            if (block.ExtensionDictionary == null || !block.ExtensionDictionary.Contains("ACAD_ASSOCNETWORK"))
            {
                return null;
            }

            var entry = (DxfDBDictionary)block.ExtensionDictionary.GetAt("ACAD_ASSOCNETWORK");
            return entry.Contains("ACAD_ASSOCNETWORK") ? entry.GetAt("ACAD_ASSOCNETWORK").Item : null;
        }

        /// <summary>The network the block's network is an action of.</summary>
        private static DxfItem TopOf(DxfItem network) =>
            network.File.ObjectItem(Section(network, "AcDbAssocAction")[2].Value.Trim());

        /// <summary>The groups of a subclass of an object.</summary>
        private static List<DxfTypedValue> Section(DxfItem item, string marker)
        {
            List<DxfTypedValue> groups = item.Groups.ToList();
            int at = groups.FindIndex(g => g.TypeCode == 100 && g.Value.Trim() == marker);
            int end = groups.FindIndex(at + 1, g => g.TypeCode == 100 || g.TypeCode == 1001);
            return groups.GetRange(at + 1, (end < 0 ? groups.Count : end) - at - 1);
        }

        /// <summary>A network and every object it owns (360), in the order reached.</summary>
        private static List<DxfItem> Tree(DxfItem network)
        {
            var tree = new List<DxfItem>();
            var queue = new Queue<DxfItem>(new[] { network });

            while (queue.Count > 0)
            {
                DxfItem item = queue.Dequeue();
                tree.Add(item);

                foreach (DxfTypedValue group in item.Groups.Where(g => g.TypeCode == 360 && g.Value.Trim() != "0"))
                {
                    DxfItem owned = item.File.ObjectItem(group.Value.Trim());

                    if (owned != null && !tree.Contains(owned) && !queue.Contains(owned))
                    {
                        queue.Enqueue(owned);
                    }
                }
            }

            return tree;
        }

        /// <summary>The object a dependency depends on (its first 330 after its marker).</summary>
        private static string DependsOn(DxfItem dependency) =>
            Section(dependency, "AcDbAssocDependency")[7].Value.Trim();

        /// <summary>The expression and the value of a variable of a network, by name, as "expression = code:value".</summary>
        private static string VariableOf(DxfItem network, string name)
        {
            DxfItem variable = Tree(network)
                .First(i => i.Type == "ACDBASSOCVARIABLE" && Section(i, "AcDbAssocVariable")[1].Value.Trim() == name);
            List<DxfTypedValue> held = Section(variable, "AcDbAssocVariable");
            return held[2].Value.Trim()
                + " = "
                + held[5].TypeCode.ToString(CultureInfo.InvariantCulture)
                + ":"
                + held[5].Value.Trim();
        }

        /// <summary>
        /// The rectangle of D27 with its network made an action of a network of the drawing, under ACAD_ASSOCNETWORK of the
        /// named objects, as AutoCAD keeps every block's network.
        /// </summary>
        private static DxfDatabase Networked(DxfDatabase db)
        {
            DxfItem network = NetworkOf(db.BlockTable["RECT"]);
            DxfItem entry = db.NamedObjectsDictionary.Add("ACAD_ASSOCNETWORK", "DICTIONARY", b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(281, "1");
            });
            DxfItem top = ((DxfDBDictionary)DxfDBObject.OfObject(entry)).Add(
                "ACAD_ASSOCNETWORK",
                "ACDBASSOCNETWORK",
                b =>
                {
                    b.Group(100, "AcDbAssocAction");
                    Array.ForEach(new[] { "90:1", "90:0", "330:0", "360:0", "90:0", "90:0", "90:0" }, s =>
                        b.Group(int.Parse(s.Split(':')[0], CultureInfo.InvariantCulture), s.Split(':')[1])
                    );
                    b.Group(100, "AcDbAssocNetwork");
                    Array.ForEach(new[] { "90:0", "90:1", "90:1", "330:" + network.Handle, "90:0" }, s =>
                        b.Group(int.Parse(s.Split(':')[0], CultureInfo.InvariantCulture), s.Split(':')[1])
                    );
                }
            );
            Edit(network, groups =>
            {
                int at = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbAssocAction");
                groups[at + 3] = new KeyValuePair<int, string>(330, top.Handle);
            });
            return DxfDatabase.Parse(db.ToText());
        }

        /// <summary>The constraint group of the rectangle's network.</summary>
        private static DxfItem GroupOf(DxfDatabase db) =>
            Tree(NetworkOf(db.BlockTable["RECT"])).First(i => i.Type == "ACDBASSOC2DCONSTRAINTGROUP");

        /// <summary>Lists objects among the dependencies of the group (360 before its node count), its count of them taken on.</summary>
        private static void Owns(DxfItem group, params string[] handles)
        {
            Edit(group, groups =>
            {
                int last = groups.FindLastIndex(g => g.Key == 360);
                int count = last;

                while (groups[count].Key == 360)
                {
                    count--;
                }

                groups[count] = new KeyValuePair<int, string>(
                    90,
                    (int.Parse(groups[count].Value, CultureInfo.InvariantCulture) + handles.Length).ToString(
                        CultureInfo.InvariantCulture
                    )
                );
                groups.InsertRange(last + 1, handles.Select(h => new KeyValuePair<int, string>(360, h)));
            });
        }

        /// <summary>Lists a variable among the actions of the network, its count taken on.</summary>
        private static void Lists(DxfItem network, string variable)
        {
            Edit(network, groups =>
            {
                int at = groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbAssocNetwork");
                int count = int.Parse(groups[at + 3].Value, CultureInfo.InvariantCulture);
                groups[at + 3] = new KeyValuePair<int, string>(90, (count + 1).ToString(CultureInfo.InvariantCulture));
                groups.Insert(at + 4 + count, new KeyValuePair<int, string>(360, variable));
            });
        }

        /// <summary>Appends nodes to the group, each as its builder writes it.</summary>
        private static void Append(DxfItem group, params Action<Groups>[] nodes)
        {
            var written = new Groups();
            Array.ForEach(nodes, n => n(written));
            Edit(group, groups => groups.AddRange(written));
        }

        /// <summary>
        /// The rectangle with a dimension of its Width's distance the constraints drive by a variable w1 of the expression
        /// Width, as AutoCAD's Block Editor keeps a dimensional constraint of no parameter: a hidden rotated dimension of the
        /// block, tied by a dependency whose body (ASSOCDIMDEPENDENCYBODY) shows the value.
        /// </summary>
        private static DxfDatabase Dimensioned()
        {
            DxfDatabase db = Networked(Rectangle());
            DxfBlockTableRecord block = db.BlockTable["RECT"];
            var picture = new DxfBlockTableRecord { Name = "W1DIM", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(picture);
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(50, -20, 0),
                "100",
                null
            )
            {
                DimBlock = picture,
                Visible = false,
            };
            block.AppendEntity(dimension);
            DxfItem network = NetworkOf(block);
            DxfItem group = GroupOf(db);
            DxfFile file = network.File;
            DxfItem w1 = Variable(file, network.Handle, 4, "w1", "Width", 100);
            Lists(network, w1.Handle);
            string value = Value(file, group.Handle, w1.Handle, 100);
            DxfItem tie = Add(file, "ACDBASSOCDEPENDENCY", group.Handle, b => Dependency(b, dimension.Handle, "0"));
            DxfItem body = Add(file, "ASSOCDIMDEPENDENCYBODY", tie.Handle, b =>
            {
                b.Group(100, "AcDbAssocDependencyBody");
                b.Group(90, "1");
                b.Group(100, "AcDbImpAssocDimDependencyBodyBase");
                b.Group(90, "1");
                b.Group(1, "100");
                b.Group(100, "AcDbAssocDimDependencyBody");
                b.Group(90, "1");
            });
            Edit(tie, groups =>
            {
                int at = groups.FindLastIndex(g => g.Key == 360);
                groups[at] = new KeyValuePair<int, string>(360, body.Handle);
            });
            Owns(group, value, tie.Handle);
            Append(group, b =>
            {
                Rule(b, "AcDistanceConstraint", group.Handle, 31, 2, 3);
                Distance(b, value, tie.Handle, 1, 0);
            });
            return DxfDatabase.Parse(db.ToText());
        }

        // ---- 1. the anonymous block's copy of the network --------------------------------------------------------------------

        [Fact]
        public void AnInsertSet_ItsAnonymousBlockHoldsACopyOfTheNetwork_AtTheValuesSet_AsAutoCadWritesItsOwn()
        {
            // AutoCAD 2021 set Height 600 of its Fluorescent sample and wrote a copy of the block's network into the *U13 it
            // made: its geometry dependencies on *U13's entities, Height's expression 600, and the copy listed by the drawing's
            // network (plugin D27b); it set no INSERT again whose anonymous block held none (eKeyNotFound).
            DxfDatabase db = Networked(Rectangle());
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            foreach (DxfDatabase drawing in new[] { db, Reopened(db) })
            {
                DxfBlockReference read = Insert(drawing);
                DxfItem copy = NetworkOf(read.BlockTableRecord);
                Assert.NotNull(copy);
                List<DxfItem> tree = Tree(copy);
                List<string> lines = read
                    .BlockTableRecord.OfType<DxfLine>()
                    .Select(l => l.Handle)
                    .OrderBy(h => h, StringComparer.Ordinal)
                    .ToList();
                Assert.Equal(
                    lines,
                    tree.Where(i => i.Type == "ACDBASSOCGEOMDEPENDENCY")
                        .Select(DependsOn)
                        .OrderBy(h => h, StringComparer.Ordinal)
                        .ToList()
                );
                Assert.Equal("150 = 90:150", VariableOf(copy, "Width"));
                Assert.Equal(" = 40:50", VariableOf(copy, "Height"));
                Assert.Equal(Lines150, Drawn(read));

                // Each line of the anonymous block names its dependency among its reactors.
                foreach (DxfItem dependency in tree.Where(i => i.Type == "ACDBASSOCGEOMDEPENDENCY"))
                {
                    DxfEntity line = read
                        .BlockTableRecord.OfType<DxfLine>()
                        .Single(l => l.Handle == DependsOn(dependency));
                    Assert.Contains(line.Item.Groups, g => g.TypeCode == 330 && g.Value.Trim() == dependency.Handle);
                }

                // The drawing's network lists the block's network and the copy, the copy given the next index.
                DxfItem top = TopOf(NetworkOf(drawing.BlockTable["RECT"]));
                List<DxfTypedValue> listed = Section(top, "AcDbAssocNetwork");
                Assert.Equal("2", listed[1].Value.Trim());
                Assert.Equal("2", listed[2].Value.Trim());
                Assert.Equal(copy.Handle, listed[4].Value.Trim());
                Assert.Equal("2", Section(copy, "AcDbAssocAction")[4].Value.Trim());
            }
        }

        [Fact]
        public void TheCopy_PutsItsDependencyOnWidthLastInTheParametersChain_AsAutoCadChainedItsOwn()
        {
            // AutoCAD 2021's copy of its Hex Head Bolt's network named the block's dependency on Length before its own (330 of
            // slot 9), the block's naming the copy after it (slot 10), and Length listed both among its reactors (plugin D27b).
            DxfDatabase db = Networked(Rectangle());
            DxfBlockReference insert = Insert(db);
            DxfItem width = Node(db.BlockTable["RECT"], "BLOCKHORIZONTALCONSTRAINTPARAMETER", "Width");

            Property(insert, "Width").Value = 150.0;

            DxfItem own = Tree(NetworkOf(db.BlockTable["RECT"]))
                .Single(i => i.Type == "ACDBASSOCDEPENDENCY" && DependsOn(i) == width.Handle);
            DxfItem copied = Tree(NetworkOf(insert.BlockTableRecord))
                .Single(i => i.Type == "ACDBASSOCDEPENDENCY" && DependsOn(i) == width.Handle);
            Assert.Equal(copied.Handle, Section(own, "AcDbAssocDependency")[10].Value.Trim());
            Assert.Equal(own.Handle, Section(copied, "AcDbAssocDependency")[9].Value.Trim());
            Assert.Contains(width.File.ObjectItem(width.Handle).Groups, g =>
                g.TypeCode == 330 && g.Value.Trim() == copied.Handle
            );
        }

        [Fact]
        public void AnInsertSetAgain_ItsAnonymousBlockHoldsOneCopy_TheOneBeforeTakenOutOfTheDrawingAndTheChain()
        {
            DxfDatabase db = Networked(Rectangle());
            DxfBlockReference insert = Insert(db);
            DxfItem width = Node(db.BlockTable["RECT"], "BLOCKHORIZONTALCONSTRAINTPARAMETER", "Width");
            Property(insert, "Width").Value = 150.0;
            List<string> first = Tree(NetworkOf(insert.BlockTableRecord)).Select(i => i.Handle).ToList();

            Property(insert, "Height").Value = 80.0;

            DxfItem copy = NetworkOf(insert.BlockTableRecord);
            Assert.DoesNotContain(copy.Handle, first);
            Assert.All(first, h => Assert.True(width.File.ObjectItem(h) == null || width.File.ObjectItem(h).IsRemoved));
            Assert.Equal("80 = 90:80", VariableOf(copy, "Height"));
            Assert.Equal("150 = 90:150", VariableOf(copy, "Width"));
            List<DxfTypedValue> listed = Section(TopOf(NetworkOf(db.BlockTable["RECT"])), "AcDbAssocNetwork");
            Assert.Equal("2", listed[2].Value.Trim());
            Assert.Equal(copy.Handle, listed[4].Value.Trim());

            DxfItem own = Tree(NetworkOf(db.BlockTable["RECT"]))
                .Single(i => i.Type == "ACDBASSOCDEPENDENCY" && DependsOn(i) == width.Handle);
            DxfItem copied = Tree(copy).Single(i => i.Type == "ACDBASSOCDEPENDENCY" && DependsOn(i) == width.Handle);
            Assert.Equal(copied.Handle, Section(own, "AcDbAssocDependency")[10].Value.Trim());
            IReadOnlyList<DxfTypedValue> reactors = width.File.ObjectItem(width.Handle).Groups;
            Assert.Contains(reactors, g => g.TypeCode == 330 && g.Value.Trim() == copied.Handle);
            Assert.DoesNotContain(reactors, g => g.TypeCode == 330 && first.Contains(g.Value.Trim()));
            Assert.Equal("(0,0)-(150,0) (150,0)-(150,80) (150,80)-(0,80) (0,80)-(0,0)", Drawn(insert));
        }

        [Fact]
        public void TheCopy_MovesTheDimensionOfAConstraintAndShowsItsValue_AsAutoCadMovedItsSamplesHiddenDimensions()
        {
            // AutoCAD 2021 set its Concrete Pipe again from the library's copy as from its own once the copies of the
            // constraints' dimensions measured what the copy said (plugin D27b c3-c6): the dimension of w1 = Width goes to the
            // new right end, its text the value as the drawing writes lengths.
            DxfDatabase db = Dimensioned();
            db.Dimzin = 8;
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            DxfRotatedDimension dimension = insert.BlockTableRecord.OfType<DxfRotatedDimension>().Single();
            Assert.Equal(150.0, dimension.XLine2Point.X, 12);
            Assert.Equal(0.0, dimension.XLine1Point.X, 12);
            Assert.Equal(150.0, dimension.DimLinePoint.X, 12);
            Assert.Equal(-20.0, dimension.DimLinePoint.Y, 12);
            Assert.Equal("150", dimension.DimensionText);
            Assert.Equal("Width = 90:150", VariableOf(NetworkOf(insert.BlockTableRecord), "w1"));
            DxfItem body = Tree(NetworkOf(insert.BlockTableRecord)).Single(i => i.Type == "ASSOCDIMDEPENDENCYBODY");
            Assert.Equal("150", Section(body, "AcDbImpAssocDimDependencyBodyBase")[1].Value.Trim());
        }

        // ---- 2. values ---------------------------------------------------------------------------------------------------------

        [Fact]
        public void AWidthOfMoreThanTwelveDigits_IsSolvedAtTwelve_AsAutoCadSolvedHeight612Point3456789012345()
        {
            // AutoCAD 2021 wrote Height 612.3456789012345 of its Fluorescent sample as 612.345678901 into the variable and
            // drew the rectangle at that (plugin D27b a1).
            DxfDatabase db = Networked(Rectangle());
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.12345678901234;

            DxfLine bottom = Lines(insert).First();
            Assert.Equal(150.123456789, bottom.EndPoint.X, 12);
            Assert.Equal("150.123456789 = 40:150.123456789", VariableOf(NetworkOf(insert.BlockTableRecord), "Width"));
        }

        [Fact]
        public void Functions_AreEvaluatedInDegrees_WholeOrRealAsAutoCadKeptThem()
        {
            // AutoCAD 2021 read width*cos(60) of its Fluorescent sample as half the width, height-h3*2*tan(45) as
            // height-h3*2, width-W3*2*asin(1)/90 as width-W3*2, kept sqrt(625) real and abs(-25) and round(99.6) whole, and
            // Length-30 whole and DIA/2 real (plugin D27b f1, a1).
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["Width"] = 1000,
                ["Length"] = 50,
                ["DIA"] = 10,
            };
            Func<string, double> read = name => values[name];
            Func<string, bool> whole = name => true;

            Assert.Equal(500.0, DxfConstraintExpression.Evaluate("width*cos(60)", read, whole, out bool cosine), 12);
            Assert.False(cosine);
            Assert.Equal(0.5, DxfConstraintExpression.Evaluate("sin(30)", read), 12);
            Assert.Equal(1.0, DxfConstraintExpression.Evaluate("tan(45)", read), 12);
            Assert.Equal(90.0, DxfConstraintExpression.Evaluate("asin(1)", read), 12);
            Assert.Equal(60.0, DxfConstraintExpression.Evaluate("acos(0.5)", read), 12);
            Assert.Equal(45.0, DxfConstraintExpression.Evaluate("atan(1)", read), 12);
            Assert.Equal(25.0, DxfConstraintExpression.Evaluate("sqrt(625)", read, whole, out bool rooted));
            Assert.False(rooted);
            Assert.Equal(25.0, DxfConstraintExpression.Evaluate("abs(-25)", read, whole, out bool absolute));
            Assert.True(absolute);
            Assert.Equal(100.0, DxfConstraintExpression.Evaluate("round(99.6)", read, whole, out bool rounded));
            Assert.True(rounded);
            Assert.Equal(20.0, DxfConstraintExpression.Evaluate("Length-30", read, whole, out bool left));
            Assert.True(left);
            Assert.Equal(5.0, DxfConstraintExpression.Evaluate("DIA/2", read, whole, out bool half));
            Assert.False(half);
            Assert.Throws<NotSupportedException>(() => DxfConstraintExpression.Evaluate("log10(1)", read));
        }

        [Fact]
        public void AConstraintParameterATableToMatchHolds_ReadsAsReadOnly_AndIsSetAllTheSame_AsAutoCadReadAndSetFluorescentsWidth()
        {
            // AutoCAD 2021 read Width and Height of its Fluorescent sample, whose table its properties must match, as read
            // only, and set Width 1200 all the same (plugin D27 p1, D27b a1).
            DxfDatabase db = Rectangle(table: true);
            DxfItem table = Nodes(db.BlockTable["RECT"]).Single(n => n.Type == "BLOCKPROPERTIESTABLE");
            Edit(table, groups =>
            {
                int count = groups.FindLastIndex(g => g.Key == 93);
                int must = groups.FindIndex(count, g => g.Key == 290);
                groups[must] = new KeyValuePair<int, string>(290, "1");
            });
            db = Reopened(db);
            DxfBlockReference insert = Insert(db);
            Assert.True(db.BlockTable["RECT"].BlockPropertiesTable.MustMatch);

            Assert.True(Property(insert, "Width").ReadOnly);
            Assert.True(Property(insert, "Height").ReadOnly);

            Property(insert, "Width").Value = 150.0;
            Assert.Equal(Lines150, Drawn(insert));
        }

        // ---- 3. geometry the constraints leave free ------------------------------------------------------------------------

        [Fact]
        public void ARectangleFreeToSlide_HeightSet_KeepsItsBottom_TheBasePointOfHeight()
        {
            // AutoCAD 2021 kept its Door Elevation's bottom at a new Height as its left side at a new Width (plugin D27 p2,
            // D27b): each parameter's base point stays where it is along its distance.
            DxfDatabase db = Rectangle(fixedCorner: false);
            DxfBlockReference insert = Insert(db);

            Property(insert, "Height").Value = 80.0;

            Assert.Equal("(0,0)-(100,0) (100,0)-(100,80) (100,80)-(0,80) (0,80)-(0,0)", Drawn(insert));
        }

        // ---- 4. what the library reads ----------------------------------------------------------------------------------------

        [Fact]
        public void AGroupOfR2018_IsRead_AsAutoCad2021SavedItsSamples()
        {
            // AutoCAD 2021 saved its samples' groups as DXF of R2018 with the classes and ids of the nodes listed first and
            // each node opened by its id (plugin D27b); the library refused every constraint parameter of them before.
            DxfDatabase db = Rectangle();
            Edit(GroupOfPlain(db), ToR2018);
            db = Reopened(db);
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal(Lines150, Drawn(insert));
        }

        [Fact]
        public void AConstructionLineAndAnAngleOfThreePoints_AreSolved_AsAutoCadSolvedItsImperialHexHeadBolt()
        {
            // AutoCAD 2021 held each fillet of its Hex Head Bolt - Imperial at 90 degrees from its start to its end through
            // lines of no entity (AcConstrained2PointsConstructionLine, Ac3PointAngleConstraint), and the library drew Length 3
            // and DIA 0.75 as it did, to 7E-15 (plugin D27b r3): here a line through the bottom's ends and the angle at the
            // corner from the bottom's end to the left side's start, 90 degrees.
            DxfDatabase db = Rectangle();
            DxfBlockTableRecord block = db.BlockTable["RECT"];
            DxfItem network = NetworkOf(block);
            DxfItem group = GroupOfPlain(db);
            DxfFile file = network.File;
            DxfItem angle = Variable(file, network.Handle, 4, "ang", "90", 90);
            Lists(network, angle.Handle);
            string value = Value(file, group.Handle, angle.Handle, 90);
            DxfItem tie = Add(file, "ACDBASSOCDEPENDENCY", group.Handle, b => Dependency(b, "0", "0"));
            Owns(group, value, tie.Handle);
            Append(
                group,
                b =>
                {
                    Head(b, "AcConstrained2PointsConstructionLine", group.Handle, 31, 32, 33);
                    b.Group(330, "0");
                    b.Group(90, "0");
                    Point(b, 10, 0, 0);
                    Point(b, 10, 100, 0);
                },
                b => Rule(b, "AcPointCurveConstraint", group.Handle, 32, 2, 31),
                b => Rule(b, "AcPointCurveConstraint", group.Handle, 33, 3, 31),
                b =>
                {
                    Rule(b, "Ac3PointAngleConstraint", group.Handle, 34, 2, 3, 11);
                    b.Group(340, value);
                    b.Group(340, tie.Handle);
                    b.Group(280, "0");
                }
            );
            db = Reopened(db);
            DxfBlockReference insert = Insert(db);

            Property(insert, "Width").Value = 150.0;

            Assert.Equal(Lines150, Drawn(insert));
        }

        /// <summary>The constraint group of the rectangle, its network owned by none.</summary>
        private static DxfItem GroupOfPlain(DxfDatabase db) =>
            Tree(NetworkOf(db.BlockTable["RECT"])).First(i => i.Type == "ACDBASSOC2DCONSTRAINTGROUP");

        /// <summary>
        /// Rewrites a group of R2010 as R2018 writes it: the next id and the count of nodes (the two 90s before the first node)
        /// as 90, 90 0, 90 0, 290 0 and 90, the classes and ids listed (a 1 and a 90 each), then each node as its id, its count
        /// and ids of its ties, its flags (70) and the groups of its class.
        /// </summary>
        private static void ToR2018(List<KeyValuePair<int, string>> groups)
        {
            int first = groups.FindIndex(g => g.Key == 1);
            var head = groups.GetRange(0, first - 2);
            head.Add(groups[first - 2]);
            head.Add(new KeyValuePair<int, string>(90, "0"));
            head.Add(new KeyValuePair<int, string>(90, "0"));
            head.Add(new KeyValuePair<int, string>(290, "0"));
            head.Add(groups[first - 1]);
            var listed = new List<KeyValuePair<int, string>>();
            var records = new List<KeyValuePair<int, string>>();

            for (int i = first; i < groups.Count; )
            {
                int next = groups.FindIndex(i + 1, g => g.Key == 1);
                next = next < 0 ? groups.Count : next;

                // 1 class, 330 group, 90 id, 70 flags, 90 count, ids, the rest.
                string id = groups[i + 2].Value;
                int count = int.Parse(groups[i + 4].Value, CultureInfo.InvariantCulture);
                listed.Add(groups[i]);
                listed.Add(new KeyValuePair<int, string>(90, id));
                records.Add(new KeyValuePair<int, string>(90, id));
                records.Add(groups[i + 4]);
                records.AddRange(groups.GetRange(i + 5, count));
                records.Add(groups[i + 3]);
                records.AddRange(groups.GetRange(i + 5 + count, next - i - 5 - count));
                i = next;
            }

            groups.Clear();
            groups.AddRange(head);
            groups.AddRange(listed);
            groups.AddRange(records);
        }
    }
}
