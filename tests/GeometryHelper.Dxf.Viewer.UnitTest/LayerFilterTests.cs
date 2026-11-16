using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The layer filters of a drawing as AutoCAD 2021 keeps and reads them (an XRECORD of ACLYDICTIONARY under the layer table's
    /// extension dictionary: group 1 AcLyLayerFilter or AcLyLayerGroup, 90, the name 300, the expression 301 or the layers 330,
    /// group 290 on the current one, the filters nested in it in an ACLYDICTIONARY of its own XRECORD's extension dictionary), the
    /// expressions (<c>NAME=="A*" AND COLOR=="1"</c>) tested against the layers as AutoCAD 2021's <c>LayerFilter.Filter</c>
    /// tested the same layers, and the five columns of a layer the Layer manager adds (plot, transparency, plot style, description).
    /// A drawing of ours was read back by AutoCAD 2021's <c>Database.LayerFilters</c> with the same tree.
    /// </summary>
    public class LayerFilterTests
    {
        /// <summary>
        /// The drawing of the probe: layers 0, A1 red, A2 green and off with lineweight 0.25, B1 blue, frozen, locked and frozen in new
        /// viewports, Wall of colour 30 not plotted, 50 per cent transparent, 0.50 wide and described "hello world", and T1 of the
        /// true colour 10, 20, 30.
        /// </summary>
        private static CadDocument Probe()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "A1", ColorIndex = 1 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "A2", ColorIndex = 3 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "B1", ColorIndex = 5 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Wall", ColorIndex = 30 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "T1", ColorIndex = 7 });
            db.LayerTable["A2"].IsOff = true;
            db.LayerTable["A2"].LineWeight = DxfLineWeight.LineWeight025;
            db.LayerTable["B1"].IsFrozen = true;
            db.LayerTable["B1"].IsLocked = true;
            db.LayerTable["B1"].ViewportVisibilityDefault = true;
            db.LayerTable["Wall"].IsPlottable = false;
            db.LayerTable["Wall"].Transparency = new DxfTransparency(127);
            db.LayerTable["Wall"].LineWeight = DxfLineWeight.LineWeight050;
            db.LayerTable["Wall"].Description = "hello world";
            db.LayerTable["T1"].Color = DxfColor.FromRgb(10, 20, 30);
            return CadDocument.From(db, "probe.dxf");
        }

        private static string Pass(CadDocument document, string expression)
        {
            Assert.True(CadLayerExpression.TryParse(expression, out CadLayerExpression parsed), expression);
            var context = new CadLayerFilterContext(document.Database);
            return string.Join(
                ",",
                document.Database.LayerTable.Where(l => parsed.Matches(l, context)).Select(l => l.Name).OrderBy(n => n)
            );
        }

        [Theory]
        [InlineData("NAME==\"A*\"", "A1,A2")]
        [InlineData("NAME==\"a*\"", "A1,A2")]
        [InlineData("NAME==\"~A*\"", "0,B1,T1,Wall")]
        [InlineData("NAME==\"A?\"", "A1,A2")]
        [InlineData("NAME==\"[AB]1\"", "A1,B1")]
        [InlineData("NAME==\"A*,Wall\"", "")]
        [InlineData("COLOR==\"1\"", "A1")]
        [InlineData("COLOR==\"30\"", "Wall")]
        [InlineData("COLOR==\"10,20,30\"", "T1")]
        [InlineData("COLOR==\"1*\"", "A1,T1")]
        [InlineData("COLOR==\"red\"", "")]
        [InlineData("LINETYPE==\"CONTINUOUS\"", "0,A1,A2,B1,T1,Wall")]
        [InlineData("LINEWEIGHT==\"0.25\"", "A2")]
        [InlineData("LINEWEIGHT==\"0.50\"", "Wall")]
        [InlineData("LINEWEIGHT==\"Default\"", "0,A1,B1,T1")]
        [InlineData("TRANSPARENCY==\"50\"", "Wall")]
        [InlineData("TRANSPARENCY==\"0\"", "0,A1,A2,B1,T1")]
        [InlineData("TRANSPARENCY==\"49\"", "")]
        [InlineData("PLOTTABLE==\"False\"", "Wall")]
        [InlineData("PLOTTABLE==\"True\"", "0,A1,A2,B1,T1")]
        [InlineData("NEWVPFROZEN==\"True\"", "B1")]
        [InlineData("DESCRIPTION==\"hello*\"", "Wall")]
        [InlineData("DESCRIPTION==\"\"", "0,A1,A2,B1,T1,Wall")]
        [InlineData("OFF==\"True\"", "A2")]
        [InlineData("OFF==\"False\"", "0,A1,B1,T1,Wall")]
        [InlineData("FROZEN==\"True\"", "B1")]
        [InlineData("LOCKED==\"True\"", "B1")]
        [InlineData("VPFROZEN==\"False\"", "0,A1,A2,B1,T1,Wall")]
        [InlineData("ON==\"True\"", "")]
        [InlineData("NAME==\"A*\" AND COLOR==\"1\"", "A1")]
        [InlineData("NAME==\"A*\" OR NAME==\"B*\"", "A1,A2,B1")]
        [InlineData("NAME == \"A*\" and OFF == \"FALSE\"", "A1")]
        [InlineData("( NAME== \"A*\" )", "A1,A2")]
        [InlineData("NAME==\"A*\" AND (COLOR==\"1\" OR COLOR==\"3\")", "A1,A2")]
        [InlineData("NAME!=\"A*\"", "0,B1,T1,Wall")]
        public void AnExpression_PassesTheLayersAutoCad2021PassedForIt(string expression, string passes)
        {
            Assert.Equal(passes, Pass(Probe(), expression));
        }

        [Theory]
        [InlineData("OFF == \"FALSE\" and FROZEN == \"FALSE\" and VPFROZEN == \"FALSE\"", 1, 3)]
        [InlineData("NAME==\"*dim*\" OR NAME==\"*annot*\" OR NAME==\"*text*\" OR NAME==\"*txt*\"", 4, 1)]
        [InlineData("NAME==\"*|*dim*\" OR NAME==\"*|*annot*\"", 2, 1)]
        [InlineData("( COLOR== \"1\" ) and FROZEN == \"FALSE\"", 1, 2)]
        [InlineData("(NAME==\"A*\" OR NAME==\"B*\") AND OFF==\"False\"", 2, 2)]
        [InlineData("", 0, 0)]
        public void AnExpressionAutoCadWrote_IsReadAsAlternativesOfRules(string text, int alternatives, int rules)
        {
            Assert.True(CadLayerExpression.TryParse(text, out CadLayerExpression expression));
            Assert.Equal(alternatives, expression.Alternatives.Count);
            Assert.Equal(rules, expression.Alternatives.Select(a => a.Count).DefaultIfEmpty(0).Max());
        }

        [Theory]
        [InlineData("NAME=\"A*\"")]
        [InlineData("NAME~=\"A*\"")]
        [InlineData("NAME==A*")]
        [InlineData("NAME==\"A*\" AND")]
        [InlineData("( NAME==\"A*\"")]
        [InlineData("NAME==\"A*\" NAME==\"B*\"")]
        [InlineData("()")]
        public void AnExpressionThatIsNotOne_IsNotRead(string text)
        {
            Assert.False(CadLayerExpression.TryParse(text, out _));
        }

        [Fact]
        public void AnExpression_IsWrittenAsAutoCadsDialogWritesIt_ANDInsideAnAlternativeORBetween()
        {
            Assert.True(
                CadLayerExpression.TryParse(
                    "name == \"A*\" and color == \"1\" or (NAME==\"B*\" AND OFF==\"false\")",
                    out CadLayerExpression expression
                )
            );
            Assert.Equal("NAME==\"A*\" AND COLOR==\"1\" OR NAME==\"B*\" AND OFF==\"false\"", expression.ToString());
        }

        [Fact]
        public void APropertyFilter_IsKeptAsAnXrecordOfAcLyDictionary_AsAutoCad2021KeepsIt_AndReadBack()
        {
            CadDocument document = Probe();
            Assert.True(CadLayerFilters.AddProperty(document, null, "  Names A ", "NAME==\"A*\"").Succeeded);

            DxfDBDictionary kept = (DxfDBDictionary)
                document.Database.LayerTable.ExtensionDictionary.GetAt("ACLYDICTIONARY");
            DxfXrecord record = (DxfXrecord)kept.Single().Value;
            Assert.Equal("*A1", kept.Single().Key);
            Assert.Equal(
                "1=AcLyLayerFilter 90=1 300=Names A 301=NAME==\"A*\"",
                string.Join(" ", record.Data.Select(g => g.TypeCode + "=" + g.Value))
            );
            AssertReopens(document);

            CadLayerFilter read = CadLayerFilters.Read(DxfDatabase.Parse(document.Database.ToText())).Single();
            Assert.Equal("Names A", read.Name);
            Assert.Equal(CadLayerFilterKind.Property, read.Kind);
            Assert.Equal("NAME==\"A*\"", read.Expression);
            Assert.False(read.IsCurrent);
            Assert.True(document.Undo());
            Assert.Empty(CadLayerFilters.Read(document.Database));
        }

        [Fact]
        public void AGroupFilter_KeepsItsLayersByHandle_AndTheFilterNestedInAFilter_LivesInThatFiltersOwnAcLyDictionary()
        {
            CadDocument document = Probe();
            Assert.True(CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"").Succeeded);
            Assert.True(CadLayerFilters.AddProperty(document, new[] { "Names A" }, "Red", "COLOR==\"1\"").Succeeded);
            Assert.True(
                CadLayerFilters.AddGroup(document, null, "Grp", new[] { "A1", "wall", "Nope", "A1" }).Succeeded
            );

            DxfLayerTableRecord a1 = document.Database.LayerTable["A1"];
            DxfLayerTableRecord wall = document.Database.LayerTable["Wall"];
            DxfDBDictionary kept = (DxfDBDictionary)
                document.Database.LayerTable.ExtensionDictionary.GetAt("ACLYDICTIONARY");
            DxfXrecord group = kept.Select(e => (DxfXrecord)e.Value).Single(r => r.Data.Any(g => g.Value == "Grp"));
            Assert.Equal(
                "1=AcLyLayerGroup 90=1 300=Grp 330=" + a1.Handle + " 330=" + wall.Handle,
                string.Join(" ", group.Data.Select(g => g.TypeCode + "=" + g.Value))
            );

            DxfXrecord names = kept.Select(e => (DxfXrecord)e.Value).Single(r => r.Data.Any(g => g.Value == "Names A"));
            DxfDBDictionary inside = (DxfDBDictionary)names.ExtensionDictionary.GetAt("ACLYDICTIONARY");
            Assert.Equal("*A1", inside.Single().Key);
            Assert.Equal(
                "1=AcLyLayerFilter 90=1 300=Red 301=COLOR==\"1\"",
                string.Join(" ", ((DxfXrecord)inside.Single().Value).Data.Select(g => g.TypeCode + "=" + g.Value))
            );
            AssertReopens(document);

            IReadOnlyList<CadLayerFilter> read = CadLayerFilters.Read(DxfDatabase.Parse(document.Database.ToText()));
            Assert.Equal(new[] { "Grp", "Names A" }, read.Select(f => f.Name));
            Assert.Equal(new[] { "A1", "Wall" }, read[0].LayerNames);
            Assert.Equal("Names A/Red", read[1].Children.Single().ToString());
        }

        [Fact]
        public void AFilterOfANameASiblingHasAlready_IsRefused_EitherCase_AsAreTheNamesAutoCadMakes_AndABadName_AndABadExpression()
        {
            CadDocument document = Probe();
            Assert.True(CadLayerFilters.AddProperty(document, null, "Steel", "NAME==\"S*\"").Succeeded);
            Assert.True(CadLayerFilters.AddProperty(document, new[] { "Steel" }, "Steel", "NAME==\"S*\"").Succeeded);

            Assert.Equal(
                "A layer filter named \"steel\" already exists.",
                CadLayerFilters.AddGroup(document, null, "steel", null).Message
            );
            Assert.Equal(
                "A layer filter named \"all used layers\" already exists.",
                CadLayerFilters.AddGroup(document, null, "all used layers", null).Message
            );
            Assert.False(CadLayerFilters.AddGroup(document, null, "  ", null).Succeeded);
            Assert.False(CadLayerFilters.AddGroup(document, null, "a*b", null).Succeeded);
            Assert.Equal(
                "The filter definition is not valid: NAME=A",
                CadLayerFilters.AddProperty(document, null, "Bad", "NAME=A").Message
            );
            Assert.Equal(
                "The drawing has no layer filter \"Nowhere\".",
                CadLayerFilters.AddGroup(document, new[] { "Nowhere" }, "X", null).Message
            );
            Assert.Equal(2, CadLayerFilters.All(CadLayerFilters.Read(document.Database)).Count());
        }

        [Fact]
        public void ARenamedFilter_KeepsItsOwnPlace_AndItsChildren_ADeletedOne_TakesItsChildrenWithIt_EachOneUndoStep()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"");
            CadLayerFilters.AddProperty(document, new[] { "Names A" }, "Red", "COLOR==\"1\"");
            CadLayerFilters.AddProperty(document, null, "Other", "NAME==\"B*\"");

            Assert.False(CadLayerFilters.Rename(document, new[] { "Names A" }, "other").Succeeded);
            Assert.True(CadLayerFilters.Rename(document, new[] { "Names A" }, " A layers ").Succeeded);
            Assert.Equal(
                new[] { "A layers", "A layers/Red", "Other" },
                CadLayerFilters.All(CadLayerFilters.Read(document.Database)).Select(f => f.ToString())
            );
            Assert.True(CadLayerFilters.SetExpression(document, new[] { "A layers" }, "NAME==\"A1\"").Succeeded);
            Assert.Equal("NAME==\"A1\"", CadLayerFilters.Read(document.Database)[0].Expression);

            Assert.True(CadLayerFilters.Delete(document, new[] { "A layers" }).Succeeded);
            Assert.Equal(
                new[] { "Other" },
                CadLayerFilters.All(CadLayerFilters.Read(document.Database)).Select(f => f.ToString())
            );
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(
                new[] { "A layers", "A layers/Red", "Other" },
                CadLayerFilters.All(CadLayerFilters.Read(document.Database)).Select(f => f.ToString())
            );
            Assert.False(CadLayerFilters.Delete(document, new[] { "Nowhere" }).Succeeded);
        }

        [Fact]
        public void LayersAddedToAGroupFilter_AreAddedOrReplaced_AAPropertyFilterTakesNone_AndConvertToGroupKeepsTheLayersItShows()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddGroup(document, null, "Grp", new[] { "A1" });
            CadLayerFilters.AddProperty(document, null, "Prop", "NAME==\"A*\"");
            Assert.True(
                CadLayerFilters.SetLayers(document, new[] { "Grp" }, new[] { "A2", "Wall", "A1" }, false).Succeeded
            );
            Assert.Equal(new[] { "A1", "A2", "Wall" }, CadLayerFilters.Read(document.Database)[0].LayerNames);
            Assert.True(CadLayerFilters.SetLayers(document, new[] { "Grp" }, new[] { "B1" }, true).Succeeded);
            Assert.Equal(new[] { "B1" }, CadLayerFilters.Read(document.Database)[0].LayerNames);

            Assert.Equal(
                "\"Prop\" is a property filter: layers are added to a group filter.",
                CadLayerFilters.SetLayers(document, new[] { "Prop" }, new[] { "A1" }, false).Message
            );
            Assert.Equal(
                "\"Grp\" is a group filter: it has layers, not a definition.",
                CadLayerFilters.SetExpression(document, new[] { "Grp" }, "NAME==\"A*\"").Message
            );

            Assert.True(CadLayerFilters.ConvertToGroup(document, new[] { "Prop" }).Succeeded);
            CadLayerFilter converted = CadLayerFilters.Read(document.Database)[1];
            Assert.Equal(CadLayerFilterKind.Group, converted.Kind);
            Assert.Equal(new[] { "A1", "A2" }, converted.LayerNames);
            Assert.False(CadLayerFilters.ConvertToGroup(document, new[] { "Prop" }).Succeeded);
            AssertReopens(document);
        }

        [Fact]
        public void TheCurrentFilter_IsMarkedByGroup290OfItsXrecordAlone_AndAFilterAutoCadMakesMarksNone()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "One", "NAME==\"A*\"");
            CadLayerFilters.AddProperty(document, null, "Two", "NAME==\"B*\"");

            Assert.True(CadLayerFilters.SetCurrent(document, new[] { "Two" }).Succeeded);
            Assert.Equal(new[] { false, true }, CadLayerFilters.Read(document.Database).Select(f => f.IsCurrent));
            Assert.Contains("\n290\n1\n  1\nAcLyLayerFilter\n", document.Database.ToText().Replace("\r\n", "\n"));

            Assert.True(CadLayerFilters.SetCurrent(document, new[] { "One" }).Succeeded);
            Assert.Equal(new[] { true, false }, CadLayerFilters.Read(document.Database).Select(f => f.IsCurrent));
            Assert.True(CadLayerFilters.SetCurrent(document, Array.Empty<string>()).Succeeded);
            Assert.DoesNotContain(true, CadLayerFilters.Read(document.Database).Select(f => f.IsCurrent));
            Assert.False(CadLayerFilters.SetCurrent(document, new[] { "Nowhere" }).Succeeded);
        }

        [Fact]
        public void AFilterOnlyAcadLayerFiltersKeeps_IsListed_AndWhenChangedMovesToAcLyDictionaryAndLeavesTheOldRecord()
        {
            CadDocument document = Probe();
            DxfLayerTable table = document.Database.LayerTable;
            table.CreateExtensionDictionary();
            table.ExtensionDictionary.SetAt("ACAD_LAYERFILTERS", new DxfDBDictionary());
            DxfDBDictionary old = (DxfDBDictionary)table.ExtensionDictionary.GetAt("ACAD_LAYERFILTERS");
            old.SetAt("Dim", new DxfXrecord());
            old.GetAt("Dim").XData = new[]
            {
                new DxfTypedValue(1001, "ACAD"),
                new DxfTypedValue(1000, "( NAME== \"*dim\" )"),
            };

            CadLayerFilter dim = CadLayerFilters.Read(document.Database).Single();
            Assert.Equal("Dim", dim.Name);
            Assert.True(dim.IsLegacy);
            Assert.Equal("( NAME== \"*dim\" )", dim.Expression);

            Assert.True(CadLayerFilters.SetExpression(document, new[] { "Dim" }, "NAME==\"*dim*\"").Succeeded);

            CadLayerFilter now = CadLayerFilters.Read(document.Database).Single();
            Assert.False(now.IsLegacy);
            Assert.Equal("NAME==\"*dim*\"", now.Expression);
            Assert.Empty(old);
        }

        [Fact]
        public void TheLayersAFilterLists_AreThoseItAndItsAncestorsPass_TheOtherOnesWhenInverted_AndAllWhereTheNameIsNone()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"");
            CadLayerFilters.AddProperty(document, new[] { "Names A" }, "Red", "COLOR==\"1\"");
            CadLayerFilters.AddGroup(document, null, "Grp", new[] { "Wall", "B1" });
            DxfDatabase db = document.Database;

            Assert.Equal(6, CadLayerFilters.Visible(db, Array.Empty<string>(), false, null).Count);
            Assert.Equal(new[] { "A1", "A2" }, Sorted(CadLayerFilters.Visible(db, new[] { "names a" }, false, null)));
            Assert.Equal(new[] { "A1" }, Sorted(CadLayerFilters.Visible(db, new[] { "Names A", "Red" }, false, null)));
            Assert.Equal(new[] { "B1", "Wall" }, Sorted(CadLayerFilters.Visible(db, new[] { "Grp" }, false, null)));
            Assert.Equal(
                new[] { "0", "A2", "B1", "T1", "Wall" },
                Sorted(CadLayerFilters.Visible(db, new[] { "Names A", "Red" }, true, null))
            );
            Assert.Equal(6, CadLayerFilters.Visible(db, new[] { "Missing" }, false, null).Count);
            Assert.Empty(CadLayerFilters.Visible(db, Array.Empty<string>(), true, null));
        }

        [Fact]
        public void TheFiltersAutoCadMakes_AreAllUsedLayers_NoUnreconciledOnes_AndTheLayersThatHaveViewportOverrides()
        {
            CadDocument document = Probe();
            document.Database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "A1" }
            );
            DxfDatabase db = document.Database;

            // Layer 0 and the current layer are always in use; A1 has a line on it.
            Assert.Equal(
                new[] { "0", "A1" },
                Sorted(CadLayerFilters.Visible(db, new[] { "All Used Layers" }, false, null))
            );
            Assert.Empty(CadLayerFilters.Visible(db, new[] { "Unreconciled New Layers" }, false, null));
            Assert.Empty(CadLayerFilters.Visible(db, new[] { "Viewport Overrides" }, false, null));
            Assert.Equal(
                new[] { "A2", "B1", "T1", "Wall" },
                Sorted(CadLayerFilters.Visible(db, new[] { "all used layers" }, true, null))
            );
        }

        private static string[] Sorted(IEnumerable<string> names) =>
            names.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        [Fact]
        public void TheLayerBox_ListsOnlyTheLayersOfTheChosenFilter_WhereTheFilterIsAppliedToIt_AndTheCurrentLayerAlways()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"");
            Assert.Equal(6, document.ToolbarLayers().Count);

            int raised = 0;
            document.LayerFilterChanged += (s, e) => raised++;
            document.LayerFilterPath = new[] { "Names A" };
            Assert.Equal(6, document.ToolbarLayers().Count);
            document.ApplyLayerFilterToToolbar = true;
            Assert.Equal(new[] { "0", "A1", "A2" }, Sorted(document.ToolbarLayers().Select(l => l.Name)));

            document.InvertLayerFilter = true;
            Assert.Equal(new[] { "0", "B1", "T1", "Wall" }, Sorted(document.ToolbarLayers().Select(l => l.Name)));
            Assert.Equal(3, raised);

            document.LayerFilterPath = new[] { "Names A" };
            Assert.Equal(3, raised);
        }

        [Fact]
        public void TheFilterTheDrawingMarksAsCurrent_IsTheOneTheDocumentLooksThrough_AndKeepingItWritesItBack()
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"");
            CadLayerFilters.AddProperty(document, new[] { "Names A" }, "Red", "COLOR==\"1\"");
            CadLayerFilters.SetCurrent(document, new[] { "Names A", "Red" });

            CadDocument reopened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");
            Assert.Equal(new[] { "Names A", "Red" }, reopened.LayerFilterPath);

            reopened.LayerFilterPath = new[] { "Names A" };
            Assert.True(reopened.KeepLayerFilter().Succeeded);
            Assert.Equal(
                new[] { "Names A" },
                CadLayerFilters.All(CadLayerFilters.Read(reopened.Database)).Single(f => f.IsCurrent).Path
            );

            // A filter AutoCAD makes is not written; All marks none.
            reopened.LayerFilterPath = new[] { "All Used Layers" };
            Assert.True(reopened.KeepLayerFilter().Succeeded);
            Assert.DoesNotContain(
                true,
                CadLayerFilters.All(CadLayerFilters.Read(reopened.Database)).Select(f => f.IsCurrent)
            );
            Assert.Equal(new[] { "All Used Layers" }, reopened.LayerFilterPath);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheFiltersOfADrawing_SurviveItsDwgOfEveryVersionFromR2000(string version)
        {
            CadDocument document = Probe();
            CadLayerFilters.AddProperty(document, null, "Names A", "NAME==\"A*\"");
            CadLayerFilters.AddProperty(document, new[] { "Names A" }, "Red", "COLOR==\"1\"");
            CadLayerFilters.AddGroup(document, null, "Grp", new[] { "A1", "Wall" });
            CadLayerFilters.SetCurrent(document, new[] { "Names A", "Red" });

            using (var stream = new System.IO.MemoryStream())
            {
                GeometryHelper.Dwg.DwgWriter.Write(document.Database, stream, version);
                stream.Position = 0;
                DxfDatabase back = GeometryHelper.Dwg.DwgReader.Open(stream);
                IReadOnlyList<CadLayerFilter> read = CadLayerFilters.Read(back);
                Assert.Equal(
                    new[] { "Grp", "Names A", "Names A/Red" },
                    CadLayerFilters.All(read).Select(f => f.ToString())
                );
                Assert.Equal(new[] { "A1", "Wall" }, read[0].LayerNames);
                Assert.Equal("COLOR==\"1\"", read[1].Children.Single().Expression);
                Assert.True(read[1].Children.Single().IsCurrent);
            }
        }

        /// <summary>A drawing of named plot styles (<c>$PSTYLEMODE</c> 0) whose plot style names are Normal and Style 1, with the layers Steel and Other.</summary>
        private static CadDocument NamedStyles()
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Other" });
            string text = db.ToText().Replace("\r\n", "\n");
            string dictionary = db.PlotStyleNameDictionary.Handle;
            string normal = "  3\nNormal\n350\n" + db.PlotStyleNameDictionary.GetAt("Normal").Handle + "\n";
            text = text.Replace(normal, normal + "  3\nStyle 1\n350\nFF01\n");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Insert(
                end,
                "  0\nACDBPLACEHOLDER\n  5\nFF01\n102\n{ACAD_REACTORS\n330\n"
                    + dictionary
                    + "\n102\n}\n330\n"
                    + dictionary
                    + "\n"
            );
            return CadDocument.From(DxfDatabase.Parse(text), "named.dxf");
        }

        [Fact]
        public void LayerPStyle_IsOfferedOnlyInADrawingOfNamedPlotStyles_AsksTheNameThenTheLayers_AndAnUnknownNameIsAskedAgain()
        {
            CadCommandSession byColour = Run(Session(Blank()), "-LAYER");
            Assert.DoesNotContain("PStyle", byColour.PromptText);

            CadDocument document = NamedStyles();
            CadCommandSession session = Run(Session(document), "-LAYER");
            Assert.Contains("Plot/PStyle/Freeze", session.PromptText);

            Run(session, "PStyle");
            Assert.Equal("Enter Plot Style Name or [?] <Normal>:", session.PromptText);
            Run(session, "?");
            Assert.Contains("Plot Style Names:", session.History);
            Assert.Contains("Normal", session.History);
            Assert.Contains("Style 1", session.History);
            Run(session, "Bogus");
            Assert.Contains("Unknown plot style.", session.History);
            Assert.Equal("Enter Plot Style Name or [?] <Normal>:", session.PromptText);

            Run(session, "style 1");
            Assert.Equal("Enter name list of layer(s) for plot style name Style 1 <0>:", session.PromptText);
            Run(session, "Steel,Nope");
            Assert.Equal("Style 1", document.Database.LayerTable["Steel"].PlotStyleName);
            Assert.Equal("Normal", document.Database.LayerTable["Other"].PlotStyleName);
            Assert.Equal("Normal", document.Database.LayerTable["0"].PlotStyleName);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            // Enter at the name goes back to the options.
            Run(session, "-LAYER", "PStyle", Enter);
            Assert.Contains("Plot/PStyle/Freeze", session.PromptText);
            Run(session, Enter);
            Assert.True(document.Undo());
            Assert.Equal("Normal", document.Database.LayerTable["Steel"].PlotStyleName);
        }

        [Fact]
        public void ALayersPlotStyle_IsSetByNameInADrawingOfNamedPlotStyles_AsOneUndoStep_AndRefusedWhereThePlotStylesFollowTheColour()
        {
            CadDocument byColour = Blank();
            Assert.Equal(
                "The drawing plots by colour: a layer has no plot style of its own to set.",
                CadLayerEditor.SetPlotStyle(byColour, "0", "Normal").Message
            );

            var db = new DxfDatabase("AC1027");
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel" });
            CadDocument named = CadDocument.From(db, "named.dxf");
            Assert.Equal("Normal", db.LayerTable["Steel"].PlotStyleName);
            Assert.True(CadLayerEditor.SetPlotStyle(named, "steel", "normal").Succeeded);
            Assert.Equal(
                "The drawing has no plot style named \"Style 9\".",
                CadLayerEditor.SetPlotStyle(named, "Steel", "Style 9").Message
            );
            Assert.Equal("Cannot find layer \"Nope\".", CadLayerEditor.SetPlotStyle(named, "Nope", "Normal").Message);
        }
    }
}
