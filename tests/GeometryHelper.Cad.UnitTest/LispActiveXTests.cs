using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The object model of <c>vla-</c> and <c>vlax-</c> (part O1 of L4): the application, the document, the collections, the
    /// blocks, the table records, the entities' common members, the selection sets, the undo marks and the refusals, typed at
    /// the command line of a headless session on a blank drawing with a few entities (a1 a line on layer Steel,
    /// a2 a line, a3 a circle, a4 an arc, a5 a polyline, a6 a text, a7 an INSERT of BOX, a8 a line on the locked layer Locked).
    /// accoreconsole has no object model (probe a1), so the object rows are those the full AutoCAD 2021 gave (probes g2 and
    /// g3 of the L4 spec, section 2b): marked <c>AutoCAD 2021 (GUI g2)</c>; a row the probes do not decide is marked
    /// <c>ours: D2nn</c>.
    /// </summary>
    public sealed class LispActiveXTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        // The drawing of the tests: a blank drawing (with its handles, tables and two layouts), the layers Steel, Locked (locked)
        // and Off (off), the block BOX of four lines, and in model space, in this order, named a1 to a8 in LISP: a1 a line on Steel,
        // a2 a line, a3 a circle, a4 an arc, a5 a polyline, a6 a text, a7 an INSERT of BOX, a8 a line on Locked.
        private static CadDocument Drawing(string name = "blank.dxf")
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"), name);
            DxfDatabase database = document.Database;
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Locked", ColorIndex = 3 });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Off", ColorIndex = 3 });
            database.LayerTable["Locked"].IsLocked = true;
            database.LayerTable["Off"].IsOff = true;
            var box = new DxfBlockTableRecord { Name = "BOX" };
            database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            box.AppendEntity(new DxfLine(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)));
            box.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)));
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(0, 0, 0)));
            return document;
        }

        // The drawing with the ActiveX functions loaded, its entities made by entmake and the application and the document read.
        private static CadCommandSession Loaded(CadDocument document = null)
        {
            CadCommandSession session = Session(document ?? Drawing());
            Value(session, "(vl-load-com)");
            StandIns(session);
            Value(session, "(entmake '((0 . \"LINE\") (8 . \"Steel\") (10 0.0 0.0 0.0) (11 100.0 0.0 0.0)))");
            Value(session, "(setq a1 (entlast))");
            Value(session, "(entmake '((0 . \"LINE\") (8 . \"0\") (10 0.0 0.0 0.0) (11 0.0 50.0 0.0)))");
            Value(session, "(setq a2 (entlast))");
            Value(session, "(entmake '((0 . \"CIRCLE\") (8 . \"0\") (10 200.0 0.0 0.0) (40 . 10.0)))");
            Value(session, "(setq a3 (entlast))");
            Value(
                session,
                "(entmake '((0 . \"ARC\") (8 . \"0\") (10 300.0 0.0 0.0) (40 . 20.0) (50 . 0.0) (51 . 1.5708)))"
            );
            Value(session, "(setq a4 (entlast))");
            Value(
                session,
                "(entmake '((0 . \"LWPOLYLINE\") (8 . \"0\") (90 . 3) (70 . 0) (10 0.0 100.0) (10 100.0 100.0) (10 100.0 150.0)))"
            );
            Value(session, "(setq a5 (entlast))");
            Value(session, "(entmake '((0 . \"TEXT\") (8 . \"0\") (10 0.0 200.0 0.0) (40 . 5.0) (1 . \"NOTE\")))");
            Value(session, "(setq a6 (entlast))");
            Value(session, "(entmake '((0 . \"INSERT\") (8 . \"0\") (2 . \"BOX\") (10 500.0 0.0 0.0)))");
            Value(session, "(setq a7 (entlast))");
            Value(session, "(entmake '((0 . \"LINE\") (8 . \"Locked\") (10 0.0 -50.0 0.0) (11 100.0 -50.0 0.0)))");
            Value(session, "(setq a8 (entlast))");
            Value(session, "(setq app (vlax-get-acad-object) doc (vla-get-ActiveDocument app))");
            return session;
        }

        // What a form gives, as the lines after it, the made-up addresses of the objects written @ and handles of names *.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form.Length > 0 && form[0] != '(' && form[0] != '!' ? "!" + form : form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            text = Regex.Replace(text, @"(#<VLA-OBJECT \w+) [0-9a-f]{16}>", "$1 @>");
            return Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
        }

        // The variant and safearray functions are part V's; where they are not defined yet (this part is built on its own) the
        // few the tests of objects need are defined here, as small stand-ins, so that the tests run before and after the merge.
        private static void StandIns(CadCommandSession session)
        {
            LispInterpreter lisp = session.Lisp;

            void Define(string name, int min, int max, Func<object[], object> body)
            {
                if (lisp.GetValue(name) == null)
                {
                    lisp.Define(name, min, max, body);
                }
            }

            double Real(object value) => value is int whole ? whole : (double)value;

            object Plain(object item) => item is LispVariant variant ? variant.Value : item;

            Define("VLAX-3D-POINT", 2, 3, a =>
                LispVariant.Point(Real(a[0]), Real(a[1]), a.Length > 2 ? Real(a[2]) : 0.0)
            );
            Define("VLAX-VARIANT-VALUE", 1, 1, a => ((LispVariant)a[0]).Value);
            Define("VLAX-MAKE-VARIANT", 1, 1, a => a[0] is LispSafeArray array ? LispVariant.OfArray(array) : a[0]);
            Define("VLAX-SAFEARRAY->LIST", 1, 1, a =>
            {
                var array = (LispSafeArray)Plain(a[0]);
                return LispInterpreter.ListOf(array.Items.Select(Plain).ToList());
            });
            Define("VLAX-MAKE-SAFEARRAY", 2, 2, a =>
            {
                var bounds = (LispCons)a[1];
                return new LispSafeArray((int)a[0], new[] { (int)bounds.Car }, new[] { (int)bounds.Cdr });
            });
            Define("VLAX-SAFEARRAY-FILL", 2, 2, a =>
            {
                var array = (LispSafeArray)a[0];
                var items = LispInterpreter.ItemsOf(a[1]);

                for (int i = 0; i < items.Count; i++)
                {
                    object item = items[i];
                    array.Items[i] =
                        array.ElementType == 12 && item is string text ? new LispVariant(8, text, text)
                        : array.ElementType == 5 ? (object)Real(item)
                        : item;
                }

                return array;
            });

            foreach (
                var constant in new[]
                {
                    ("VLAX-VBINTEGER", 2),
                    ("VLAX-VBDOUBLE", 5),
                    ("VLAX-VBOBJECT", 9),
                    ("VLAX-VBVARIANT", 12),
                }
            )
            {
                if (lisp.GetValue(constant.Item1) == null)
                {
                    lisp.SetValue(constant.Item1, constant.Item2);
                }
            }
        }

        // The handle of an entity, as entget's group 5 gives it.
        private static string HandleOf(CadCommandSession session, string name) =>
            Value(session, "(cdr (assoc 5 (entget " + name + ")))").Trim('"');

        private static string Error(CadCommandSession session, string form)
        {
            string text = Value(session, form);
            Assert.StartsWith("; error: ", text);
            return text.Substring("; error: ".Length);
        }

        private static void Object(CadCommandSession session, string name, string handle) =>
            Value(session, "(setq " + name + " (vlax-ename->vla-object " + handle.ToLowerInvariant() + "))");

        [Fact]
        public void BeforeVlLoadCom_OnlyVlaxForExists()
        {
            CadCommandSession session = Session(Drawing());

            // AutoCAD 2021 (a3, a4): only vlax-for is a SUBR before (vl-load-com).
            Assert.Equal("SUBR", Value(session, "(type vlax-for)"));
            Assert.Equal(
                "#<SUBR @000000000000 VLAX-FOR>",
                Regex.Replace(Value(session, "!vlax-for"), "@[0-9a-f]+", "@000000000000")
            );
            Assert.Equal("nil", Value(session, "(type vlax-get-acad-object)"));
            Assert.Equal("nil", Value(session, "(type vla-get-Name)"));
            Assert.Equal("nil", Value(session, "(type vlax-ename->vla-object)"));
            Assert.Equal("no function definition: VLAX-GET-ACAD-OBJECT", Error(session, "(vlax-get-acad-object)"));
        }

        [Fact]
        public void AfterVlLoadCom_TheObjectFunctionsAndMembersAreSubrs()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (a2): (type vla-get-Name) SUBR, printed with the case of the reference; vlax- names upper case.
            Assert.Equal("SUBR", Value(session, "(type vla-get-Name)"));
            Assert.Equal("SUBR", Value(session, "(type vla-StartUndoMark)"));
            Assert.Equal("SUBR", Value(session, "(type vlax-get-property)"));
            Assert.Equal("nil", Value(session, "(type vla-get-nosuchprop)"));
            Assert.Matches(@"^#<SUBR @[0-9a-f]+ vla-get-Name>$", Value(session, "!vla-get-Name"));
            Assert.Matches(@"^#<SUBR @[0-9a-f]+ VLAX-ENAME->VLA-OBJECT>$", Value(session, "!vlax-ename->vla-object"));
        }

        #region Non-object arguments (a2)

        [Theory]
        [InlineData("(vla-get-Name 5)", "bad argument type: VLA-OBJECT 5")]
        [InlineData("(vla-get-Name nil nil)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-get-property 5 'Name)", "bad argument type: VLA-OBJECT 5")]
        [InlineData("(vlax-get-property nil \"Name\")", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-invoke-method nil 'Delete)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-object-released-p nil)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-release-object nil)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-property-available-p nil 'Name)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-method-applicable-p nil 'Delete)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-typeinfo-available-p nil)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-map-collection nil 'princ)", "bad argument type: VLA-OBJECT nil")]
        [InlineData("(vlax-for x nil (princ x))", "bad argument type: VLA-object collection: nil")]
        [InlineData("(vlax-erased-p nil)", "unable to get ObjectID: nil")]
        [InlineData("(vlax-read-enabled-p nil)", "unable to get ObjectID: nil")]
        [InlineData("(vlax-write-enabled-p nil)", "unable to get ObjectID: nil")]
        [InlineData("(vlax-ename->vla-object 5)", "bad argument type: lentityp 5")]
        [InlineData("(vlax-ename->vla-object nil)", "bad argument type: lentityp nil")]
        [InlineData("(vla-get-Name)", "too few arguments")]
        [InlineData("(vlax-for)", "syntax error")]
        [InlineData("(vlax-for x)", "syntax error")]
        [InlineData("(vlax-for \"x\" nil 1)", "syntax error")]
        public void AnArgumentThatIsNoObject_IsTheErrorOfAutoCad(string form, string error)
        {
            // AutoCAD 2021 (a2, a4): every row.
            Assert.Equal(error, Error(Loaded(), form));
        }

        #endregion

        #region Application and document

        [Fact]
        public void TheApplicationAndTheDocument_PrintWithTheirInterface()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): #<VLA-OBJECT IAcadApplication 00007ff7ac47b910>, #<VLA-OBJECT IAcadDocument ...>
            Assert.Equal("#<VLA-OBJECT IAcadApplication @>", Value(session, "app"));
            Assert.Equal("#<VLA-OBJECT IAcadDocument @>", Value(session, "doc"));
            Assert.Equal("VLA-OBJECT", Value(session, "(type app)"));
            Assert.Equal("#<VLA-OBJECT IAcadApplication @>", Value(session, "(vla-get-Application doc)"));
            Assert.Equal("#<VLA-OBJECT IAcadDocument @>", Value(session, "(vla-get-ActiveDocument app)"));
        }

        [Fact]
        public void TheAddressOfAValue_IsSixteenLowerCaseHexDigitsAndStaysTheSame()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): 16 lower-case hex digits; ours: a value keeps its address.
            session.Feed("!doc");
            string first = session.History[session.History.Count - 1];
            session.Feed("!doc");
            Assert.Matches(@"^#<VLA-OBJECT IAcadDocument [0-9a-f]{16}>$", first);
            Assert.Equal(first, session.History[session.History.Count - 1]);
        }

        [Fact]
        public void EveryReadOfAnObject_IsANewValueThatIsEqualButNotEq()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (eq doc (vla-get-ActiveDocument app)) nil, (equal ...) T, also for an entity and the application.
            Assert.Equal("nil", Value(session, "(eq doc (vla-get-ActiveDocument app))"));
            Assert.Equal("T", Value(session, "(equal doc (vla-get-ActiveDocument app))"));
            Assert.Equal("nil", Value(session, "(eq app (vlax-get-acad-object))"));
            Assert.Equal("T", Value(session, "(equal app (vlax-get-acad-object))"));
            Assert.Equal("nil", Value(session, "(eq o (vlax-ename->vla-object a1))"));
            Assert.Equal("T", Value(session, "(equal o (vlax-ename->vla-object a1))"));
            Assert.Equal("nil", Value(session, "(equal o (vlax-ename->vla-object a2))"));
            Assert.Equal("T", Value(session, "(eq o o)"));
        }

        [Fact]
        public void TheNameOfTheDocument_IsItsFileWithTheFolderSplitOff()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): Name "g1.dxf", FullName the full path, Path the folder without a trailing separator;
            // ours: the test drawing is named blank.dxf and has no folder.
            Assert.Equal("\"blank.dxf\"", Value(session, "(vla-get-Name doc)"));
            Assert.Equal("\"blank.dxf\"", Value(session, "(vla-get-FullName doc)"));
            Assert.Equal("\"\"", Value(session, "(vla-get-Path doc)"));
        }

        [Fact]
        public void ADrawingInAFolder_HasAPathWithoutATrailingSeparator()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ghax" + Guid.NewGuid().ToString("N"));
            CadDocument document = Drawing(System.IO.Path.Combine(folder, "g1.dxf"));
            CadCommandSession session = Loaded(document);

            // AutoCAD 2021 (GUI g2): Name "g1.dxf", Path the folder without a trailing separator.
            Assert.Equal("\"g1.dxf\"", Value(session, "(vla-get-Name doc)"));
            Assert.Equal("\"" + folder.Replace("\\", "\\\\") + "\"", Value(session, "(vla-get-Path doc)"));
        }

        [Fact]
        public void ADrawingNeverSaved_IsNamedDrawing1Dwg()
        {
            CadDocument document = CadDocument.From(new DxfDatabase("AC1027"));
            CadCommandSession session = Loaded(document);

            // ours: D219 (Drawing1.dwg as the window titles a drawing never saved).
            Assert.Equal("\"Drawing1.dwg\"", Value(session, "(vla-get-Name doc)"));
            Assert.Equal("\"\"", Value(session, "(vla-get-FullName doc)"));
        }

        [Fact]
        public void ActiveSpace_IsOneInModelSpaceAndZeroInALayout()
        {
            CadDocument document = Drawing();
            CadCommandSession session = Loaded(document);

            // AutoCAD 2021 (GUI g2): 1 in model space; reference: acPaperSpace 0, acModelSpace 1.
            Assert.Equal("1", Value(session, "(vla-get-ActiveSpace doc)"));
            document.CurrentSpace = document.Spaces[1];
            Assert.Equal("0", Value(session, "(vla-get-ActiveSpace doc)"));
            Assert.Equal("\"ModelSpace\"", Value(session, "(nth 1 '(\"PaperSpace\" \"ModelSpace\"))"));
        }

        [Fact]
        public void ModelSpaceAndPaperSpace_AreBlocksNamedAsTheTableNamesThem()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): IAcadModelSpace "*Model_Space", IAcadPaperSpace "*Paper_Space".
            Assert.Equal("#<VLA-OBJECT IAcadModelSpace @>", Value(session, "(setq ms (vla-get-ModelSpace doc))"));
            Assert.Equal("\"*Model_Space\"", Value(session, "(vla-get-Name ms)"));
            Assert.Equal("\"AcDbBlockTableRecord\"", Value(session, "(vla-get-ObjectName ms)"));
            Assert.Equal("#<VLA-OBJECT IAcadPaperSpace @>", Value(session, "(setq ps (vla-get-PaperSpace doc))"));
            Assert.StartsWith("\"*Paper_Space", Value(session, "(vla-get-Name ps)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-IsLayout ms)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-IsLayout ps)"));
        }

        [Fact]
        public void TheDocument_HasNoObjectNameAndNoHandle()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): ObjectName, Handle and Layer of a document are unknown names, as the caller spells them.
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: ObjectName",
                Error(session, "(vla-get-ObjectName doc)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Handle",
                Error(session, "(vla-get-Handle doc)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Layer",
                Error(session, "(vla-get-Layer doc)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: LAYER",
                Error(session, "(vlax-get-property doc 'LAYER)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Layer",
                Error(session, "(vlax-get-property doc \"Layer\")")
            );
        }

        [Fact]
        public void ActiveLayer_ReadsAndSetsTheCurrentLayer()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): ActiveLayer a layer named "0"; put sets CLAYER.
            Assert.Equal("#<VLA-OBJECT IAcadLayer @>", Value(session, "(setq aly (vla-get-ActiveLayer doc))"));
            Assert.Equal("\"0\"", Value(session, "(vla-get-Name aly)"));
            Value(session, "(vla-put-ActiveLayer doc (vla-Item (vla-get-Layers doc) \"Steel\"))");
            Assert.Equal("\"Steel\"", Value(session, "(getvar \"CLAYER\")"));
            Value(session, "(vla-put-ActiveLayer doc (vla-Item (vla-get-Layers doc) \"0\"))");
            Assert.Equal("\"0\"", Value(session, "(getvar \"CLAYER\")"));
        }

        #endregion

        #region Collections

        [Fact]
        public void Blocks_AreWalkedInTheOrderOfTheTable()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq blks (vla-get-Blocks doc) names nil)");
            Value(session, "(vlax-for b blks (setq names (cons (vla-get-Name b) names)))");

            // AutoCAD 2021 (GUI g2): *Model_Space, *Paper_Space, then the definitions in the order made.
            Assert.StartsWith("(\"*Model_Space\" \"*Paper_Space", Value(session, "(reverse names)"));
            Assert.Contains("\"BOX\"", Value(session, "names"));
            Assert.Equal("\"AcDbBlockTable\"", Value(session, "(vla-get-ObjectName blks)"));
            Assert.Equal("#<VLA-OBJECT IAcadBlocks @>", Value(session, "blks"));
        }

        [Fact]
        public void ABlock_HasANameACountItsFlagsAndAnOrigin()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq box (vla-Item (vla-get-Blocks doc) \"box\"))");

            // AutoCAD 2021 (GUI g2): Item by name in any case an IAcadBlock, ObjectName AcDbBlockTableRecord, IsLayout, IsXRef
            // and IsDynamicBlock :vlax-false, Count of its entities, Origin a variant.
            Assert.Equal("#<VLA-OBJECT IAcadBlock @>", Value(session, "box"));
            Assert.Equal("\"BOX\"", Value(session, "(vla-get-Name box)"));
            Assert.Equal("\"AcDbBlockTableRecord\"", Value(session, "(vla-get-ObjectName box)"));
            Assert.Equal("4", Value(session, "(vla-get-Count box)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-IsLayout box)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-IsXRef box)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-IsDynamicBlock box)"));
            Assert.Equal("#<variant 8197 ...>", Value(session, "(vla-get-Origin box)"));
            Assert.Equal(
                "(0.0 0.0 0.0)",
                Value(session, "(vlax-safearray->list (vlax-variant-value (vla-get-Origin box)))")
            );
        }

        [Fact]
        public void AnEntityOfABlock_IsReadByItemAndByVlaxFor()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq box (vla-Item (vla-get-Blocks doc) \"BOX\") kinds nil)");
            Value(session, "(vlax-for e box (setq kinds (cons (vla-get-ObjectName e) kinds)))");

            // AutoCAD 2021 (GUI g2): a block is walked in the order made; Item 0 is its first entity.
            Assert.Equal("(\"AcDbLine\" \"AcDbLine\" \"AcDbLine\" \"AcDbLine\")", Value(session, "kinds"));
            Assert.Equal("#<VLA-OBJECT IAcadLine @>", Value(session, "(vla-Item box 0)"));
            Assert.Matches("^\"[0-9A-F]+\"$", Value(session, "(vla-get-Handle (vla-Item box 0))"));
        }

        [Fact]
        public void ModelSpaceCount_IsTheNumberOfItsEntities()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): (vla-get-Count ms) 12 for a drawing of 12 entities; the Plan drawing has 8.
            Assert.Equal("8", Value(session, "(vla-get-Count (vla-get-ModelSpace doc))"));
            Assert.Equal("#<VLA-OBJECT IAcadLine @>", Value(session, "(vla-Item (vla-get-ModelSpace doc) 0)"));
        }

        [Theory]
        [InlineData("(vla-Item blks \"NOSUCH\")", "Automation Error. Key not found")]
        [InlineData("(vla-Item blks 99)", "Automation Error. Description was not provided.")]
        [InlineData("(vla-Item blks -1)", "Automation Error. Description was not provided.")]
        [InlineData("(vla-Item (vla-get-Layers doc) \"NOSUCH\")", "Automation Error. Key not found")]
        [InlineData("(vla-Item (vla-get-SelectionSets doc) 0)", "Automation Error. Invalid argument index in Item")]
        [InlineData("(vla-Item (vla-get-SelectionSets doc) \"NOSUCH\")", "Automation Error. Key not found")]
        [InlineData("(vla-Item (vla-get-ModelSpace doc) 99)", "Automation Error. Description was not provided.")]
        [InlineData("(vla-Item (vla-get-ModelSpace doc) \"1\")", "Automation Error. Key not found")]
        public void ItemPastTheEnd_IsTheErrorOfAutoCad(string form, string error)
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq blks (vla-get-Blocks doc))");

            // AutoCAD 2021 (GUI g2, g3): the errors of Item; a string index into a block is parked, ours: Key not found (D241).
            Assert.Equal(error, Error(session, form));
        }

        [Fact]
        public void ItemTakesARealAndAVariantIndexAndANameInAnyCase()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq blks (vla-get-Blocks doc))");

            // AutoCAD 2021 (GUI g2): (vla-Item BLKS 1.0) and (vla-Item BLKS (vlax-make-variant 1)) the second block.
            Assert.Equal("#<VLA-OBJECT IAcadModelSpace @>", Value(session, "(vla-Item blks 0)"));
            Assert.Equal("#<VLA-OBJECT IAcadPaperSpace @>", Value(session, "(vla-Item blks 1.0)"));
            Assert.Equal("#<VLA-OBJECT IAcadPaperSpace @>", Value(session, "(vla-Item blks (vlax-make-variant 1))"));
            Assert.Equal("#<VLA-OBJECT IAcadBlock @>", Value(session, "(vla-Item blks \"bOx\")"));
        }

        [Fact]
        public void Layers_AreWalkedInTableOrderWithTheirMembers()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq lays (vla-get-Layers doc) names nil)");
            Value(session, "(vlax-for l lays (setq names (cons (vla-get-Name l) names)))");

            // AutoCAD 2021 (GUI g2): table order; Count; ObjectName AcDbLayerTable; a collection has no Name.
            Assert.Equal("(\"0\" \"Steel\" \"Locked\" \"Off\")", Value(session, "(reverse names)"));
            Assert.Equal("4", Value(session, "(vla-get-Count lays)"));
            Assert.Equal("\"AcDbLayerTable\"", Value(session, "(vla-get-ObjectName lays)"));
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Name",
                Error(session, "(vla-get-Name lays)")
            );
        }

        [Fact]
        public void ATableCollection_HasTheHandleOfItsTable()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): (vla-get-Handle blks) "1" for the block table of a blank drawing.
            string handle = Value(session, "(vla-get-Handle (vla-get-Blocks doc))");
            Assert.Matches("^\"[0-9A-F]+\"$", handle);
            Assert.NotEqual(Value(session, "(vla-get-Handle (vla-get-Layers doc))"), handle);
            Assert.Equal("\"AcDbTextStyleTable\"", Value(session, "(vla-get-ObjectName (vla-get-TextStyles doc))"));
            Assert.Equal("\"AcDbLinetypeTable\"", Value(session, "(vla-get-ObjectName (vla-get-Linetypes doc))"));
            Assert.Equal("\"AcDbDimStyleTable\"", Value(session, "(vla-get-ObjectName (vla-get-DimStyles doc))"));
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: ObjectName",
                Error(session, "(vla-get-ObjectName (vla-get-SelectionSets doc))")
            );
        }

        [Fact]
        public void ALayer_ReadsItsProperties()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq lays (vla-get-Layers doc))");

            // AutoCAD 2021 (GUI g2): Color 7, TrueColor an AcCmColor with ColorIndex 7, Linetype Continuous, Lineweight -3,
            // LayerOn, Freeze, Lock, Plottable, Description "", an off layer's Color positive (3).
            Assert.Equal("\"AcDbLayerTableRecord\"", Value(session, "(vla-get-ObjectName (vla-Item lays \"0\"))"));
            Assert.Equal("7", Value(session, "(vla-get-Color (vla-Item lays \"0\"))"));
            Assert.Equal("#<VLA-OBJECT IAcadAcCmColor @>", Value(session, "(vla-get-TrueColor (vla-Item lays \"0\"))"));
            Assert.Equal("7", Value(session, "(vla-get-ColorIndex (vla-get-TrueColor (vla-Item lays \"0\")))"));
            Assert.Equal("\"Continuous\"", Value(session, "(vla-get-Linetype (vla-Item lays \"0\"))"));
            Assert.Equal("-3", Value(session, "(vla-get-Lineweight (vla-Item lays \"0\"))"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-LayerOn (vla-Item lays \"0\"))"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Freeze (vla-Item lays \"0\"))"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Lock (vla-Item lays \"0\"))"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Plottable (vla-Item lays \"0\"))"));
            Assert.Equal("\"\"", Value(session, "(vla-get-Description (vla-Item lays \"0\"))"));
            Assert.Equal("3", Value(session, "(vla-get-Color (vla-Item lays \"Off\"))"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-LayerOn (vla-Item lays \"Off\"))"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Lock (vla-Item lays \"Locked\"))"));
        }

        [Fact]
        public void ALayerIsReadOnly_AndAPutOfAnEntityPropertyOnItIsRefused()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq lay (vla-Item (vla-get-Layers doc) \"Steel\"))");

            // ours: D225 (the Part C rule: refused, never ignored); the record-only setters are not defined (D229).
            Assert.Equal(
                "vla-put-Color: changing a table record is not supported by GeometryHelper.Cad yet",
                Error(session, "(vla-put-Color lay 5)")
            );
            Assert.Equal("nil", Value(session, "(type vla-put-LayerOn)"));
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vlax-put-property lay 'LayerOn :vlax-false)")
            );
        }

        [Fact]
        public void ATextStyleAndALinetype_ReadTheirProperties()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (GUI g2): Standard: ObjectName AcDbTextStyleTableRecord, Height 0.0, Width 1.0, ObliqueAngle 0.0;
            // Continuous: Description "Solid line", ObjectName AcDbLinetypeTableRecord; a dimension style's ObjectName.
            Value(session, "(setq st (vla-Item (vla-get-TextStyles doc) \"Standard\"))");
            Assert.Equal("\"AcDbTextStyleTableRecord\"", Value(session, "(vla-get-ObjectName st)"));
            Assert.Equal("\"Standard\"", Value(session, "(vla-get-Name st)"));
            Assert.Equal("0.0", Value(session, "(vla-get-ObliqueAngle st)"));
            Assert.Equal("1.0", Value(session, "(vla-get-Width st)"));
            Assert.Equal("STR", Value(session, "(type (vla-get-FontFile st))"));
            Value(session, "(setq lt (vla-Item (vla-get-Linetypes doc) \"Continuous\"))");
            Assert.Equal("\"AcDbLinetypeTableRecord\"", Value(session, "(vla-get-ObjectName lt)"));
            Assert.Equal("\"Solid line\"", Value(session, "(vla-get-Description lt)"));
            Assert.Equal(
                "\"AcDbDimStyleTableRecord\"",
                Value(session, "(vla-get-ObjectName (vla-Item (vla-get-DimStyles doc) 0))")
            );
        }

        [Fact]
        public void MapCollection_GivesTheCollectionAndCallsTheFunctionOnEachMember()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq lays (vla-get-Layers doc) n 0)");

            // AutoCAD 2021 (GUI g2): (vlax-map-Collection LAYS 'vla-get-Name) gives the collection.
            Assert.Equal(
                "#<VLA-OBJECT IAcadLayers @>",
                Value(session, "(vlax-map-collection lays (function (lambda (x) (setq n (1+ n)))))")
            );
            Assert.Equal("4", Value(session, "n"));
            Assert.Equal("#<VLA-OBJECT IAcadLayers @>", Value(session, "(vlax-map-collection lays 'vla-get-Name)"));
        }

        [Fact]
        public void VlaxForAndMapCollectionOfANonCollection_AreTheErrorsOfAutoCad()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): bad argument type: VLA-object collection: #<VLA-OBJECT IAcadLine ...>, and for the map
            // not a collection: #<VLA-OBJECT IAcadLine ...>.
            Assert.Equal(
                "bad argument type: VLA-object collection: #<VLA-OBJECT IAcadLine @>",
                Error(session, "(vlax-for x o (princ x))")
            );
            Assert.Equal(
                "not a collection: #<VLA-OBJECT IAcadLine @>",
                Error(session, "(vlax-map-collection o 'princ)")
            );
        }

        [Fact]
        public void VlaxFor_GivesTheSymbolItsOldValueAfterAndNil()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq x 'before)");

            // ours: D213 (the variable is bound for the loop only; the value of the form is nil).
            Assert.Equal("nil", Value(session, "(vlax-for x (vla-get-Layers doc) (princ))"));
            Assert.Equal("BEFORE", Value(session, "x"));
        }

        #endregion

        #region Entities

        [Fact]
        public void ALine_ReadsTheMembersEveryEntityHas()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): ObjectName AcDbLine, Handle as entget's 5, Layer, Color 256 (ByLayer), LinetypeScale 1.0,
            // Lineweight -1, Visible :vlax-true; Linetype "ByLayer" when absent (ours: D223; AutoCAD's drawing had Continuous).
            Assert.Equal("#<VLA-OBJECT IAcadLine @>", Value(session, "o"));
            Assert.Equal("\"AcDbLine\"", Value(session, "(vla-get-ObjectName o)"));
            Assert.Equal("\"" + HandleOf(session, "a1") + "\"", Value(session, "(vla-get-Handle o)"));
            Assert.Equal("\"Steel\"", Value(session, "(vla-get-Layer o)"));
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
            Assert.Equal("1.0", Value(session, "(vla-get-LinetypeScale o)"));
            Assert.Equal("-1", Value(session, "(vla-get-Lineweight o)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("\"ByLayer\"", Value(session, "(vla-get-Linetype o)"));
            Assert.Equal("#<VLA-OBJECT IAcadDocument @>", Value(session, "(vla-get-Document o)"));
            Assert.Equal("#<VLA-OBJECT IAcadApplication @>", Value(session, "(vla-get-Application o)"));
        }

        [Fact]
        public void ObjectIdAndOwnerId_AreTheValuesOfTheHandles()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // ours: D223 (AutoCAD's ObjectID is a small integer, 43 in one drawing; ours is the handle's value: the handle A1 is 161).
            Assert.Equal(
                Convert.ToInt64(HandleOf(session, "a1"), 16).ToString(),
                Value(session, "(vla-get-ObjectID o)")
            );
            Assert.Equal("INT", Value(session, "(type (vla-get-ObjectID o))"));
            Assert.Matches(@"^[0-9]+$", Value(session, "(vla-get-OwnerID o)"));
            Assert.NotEqual("0", Value(session, "(vla-get-OwnerID o)"));
        }

        [Theory]
        [InlineData("A1", "IAcadLine", "AcDbLine")]
        [InlineData("A3", "IAcadCircle", "AcDbCircle")]
        [InlineData("A4", "IAcadArc", "AcDbArc")]
        [InlineData("A5", "IAcadLWPolyline", "AcDbPolyline")]
        [InlineData("A6", "IAcadText", "AcDbText")]
        [InlineData("A7", "IAcadBlockReference", "AcDbBlockReference")]
        public void TheInterfaceAndTheObjectNameOfAnEntity_FollowItsType(string handle, string face, string objectName)
        {
            CadCommandSession session = Loaded();
            Object(session, "o", handle);

            // AutoCAD 2021 (GUI g2): IAcadLine "AcDbLine", IAcadBlockReference, IAcadText; reference: D212 for the rest.
            Assert.Equal("#<VLA-OBJECT " + face + " @>", Value(session, "o"));
            Assert.Equal("\"" + objectName + "\"", Value(session, "(vla-get-ObjectName o)"));
        }

        [Fact]
        public void AnEntityWithoutAMember_IsTheUnknownNameError()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vla-get-Name O) of a line, (vlax-get-property O 'FOO), (vlax-get-property O "Foo").
            Assert.Equal("ActiveX Server returned the error: unknown name: Name", Error(session, "(vla-get-Name o)"));
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: FOO",
                Error(session, "(vlax-get-property o 'FOO)")
            );
            Assert.Equal(
                "ActiveX Server returned the error: unknown name: Foo",
                Error(session, "(vlax-get-property o \"Foo\")")
            );
        }

        [Fact]
        public void PropertyAvailableAndMethodApplicable_TellWhatAnObjectHas()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vlax-property-available-p O 'LAYER T) T, of EFFECTIVENAME nil, (vlax-method-applicable-p O
            // 'DELETE) T, (vlax-typeinfo-available-p O) T; ours: the Layer of a layer is read only here, Length not an O1 member.
            Assert.Equal("T", Value(session, "(vlax-property-available-p o 'LAYER)"));
            Assert.Equal("T", Value(session, "(vlax-property-available-p o \"layer\" T)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p o 'EFFECTIVENAME)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p doc 'LAYER)"));
            Assert.Equal("nil", Value(session, "(vlax-property-available-p doc 'NAME T)"));
            Assert.Equal("T", Value(session, "(vlax-method-applicable-p o 'DELETE)"));
            Assert.Equal("nil", Value(session, "(vlax-method-applicable-p o 'GETATTRIBUTES)"));
            Assert.Equal("T", Value(session, "(vlax-typeinfo-available-p o)"));
        }

        [Fact]
        public void TheOldFunctions_GiveBooleansAsMinusOneAndOneAndPropertiesAsLists()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vlax-get O 'VISIBLE) -1, (vlax-get-property O 'VISIBLE) :vlax-true,
            // (vlax-get-property O "visible") :vlax-true.
            Assert.Equal("-1", Value(session, "(vlax-get o 'VISIBLE)"));
            Assert.Equal(":vlax-true", Value(session, "(vlax-get-property o 'VISIBLE)"));
            Assert.Equal(":vlax-true", Value(session, "(vlax-get-property o \"visible\")"));
            Assert.Equal("\"Steel\"", Value(session, "(vlax-get o \"Layer\")"));
        }

        [Fact]
        public void GetBoundingBox_SetsTwoSymbolsToSafearraysOfTheCorners()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vla-GetBoundingBox O 'LO 'HI) nil, (type LO) safearray, LO (0 0 0), HI (10 0 0).
            Assert.Equal("nil", Value(session, "(vla-GetBoundingBox o 'lo 'hi)"));
            Assert.Equal("safearray", Value(session, "(type lo)"));
            Assert.Equal("#<safearray...>", Value(session, "lo"));
            Assert.Equal("(0.0 0.0 0.0)", Value(session, "(vlax-safearray->list lo)"));
            Assert.Equal("(100.0 0.0 0.0)", Value(session, "(vlax-safearray->list hi)"));
            Assert.Equal("nil", Value(session, "(vlax-invoke-method o 'GETBOUNDINGBOX 'lo2 'hi2)"));
            Assert.Equal("safearray", Value(session, "(type lo2)"));
        }

        [Fact]
        public void TheOldVlaxInvokeOfGetBoundingBox_GivesNilAndSetsNothing()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vlax-invoke O 'GETBOUNDINGBOX 'LO3 'HI3) nil, LO3 stays nil; a non-symbol gives nil too.
            Assert.Equal("nil", Value(session, "(vlax-invoke o 'GETBOUNDINGBOX 'lo3 'hi3)"));
            Assert.Equal("nil", Value(session, "lo3"));
            Assert.Equal("nil", Value(session, "(vla-GetBoundingBox o 5 'hi4)"));
        }

        [Fact]
        public void TheBoundingBoxOfAText_StartsAtItsInsertionPoint()
        {
            CadCommandSession session = Loaded();
            Object(session, "t", "A6");

            // AutoCAD 2021 (GUI g2): a TEXT box is its textbox box moved to the insertion point (abc, height 2.5, font txt: (0 20 0) to
            // (6.66667 22.5 0)); ours: D216, the extents the drawing model gives, which start at the insertion point; the width of the
            // words needs the display fonts, which a headless test drawing has not (open: part O2 or the lead).
            Value(session, "(vla-GetBoundingBox t 'lo 'hi)");
            Assert.Equal("(0.0 200.0 0.0)", Value(session, "(vlax-safearray->list lo)"));
        }

        [Fact]
        public void AnEntityOfABlockDefinition_IsChangedThroughItsObject()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq box (vla-Item (vla-get-Blocks doc) \"BOX\") e (vla-Item box 0))");

            // ours: D221, D223 (an entity of a block definition is read and written as one of a space; vla-Delete of it is refused).
            Assert.Equal("nil", Value(session, "(vla-put-Color e 5)"));
            Assert.Equal("5", Value(session, "(vla-get-Color (vla-Item box 0))"));
            Assert.Equal("nil", Value(session, "(vla-put-Layer e \"Steel\")"));
            Assert.Equal("\"Steel\"", Value(session, "(vla-get-Layer e)"));
            Assert.Equal(
                "vla-Delete: deleting an object of a block definition is not supported by GeometryHelper.Cad yet",
                Error(session, "(vla-Delete e)")
            );
        }

        [Fact]
        public void ALayerHasAHandleAndAnOwner()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq lay (vla-Item (vla-get-Layers doc) \"Steel\"))");

            // AutoCAD 2021 (GUI g2): a layer's Handle is the one of its record ("10" for layer 0), its OwnerID the table's; ours:
            // OwnerID is the handle's value (D223).
            Assert.Equal(
                Value(session, "(cdr (assoc 5 (entget (tblobjname \"LAYER\" \"Steel\"))))"),
                Value(session, "(vla-get-Handle lay)")
            );
            Assert.Matches("^[0-9]+$", Value(session, "(vla-get-OwnerID lay)"));
            Assert.Equal(
                Value(session, "(vla-get-Handle (vla-get-Layers doc))"),
                "\"" + Convert.ToInt64(Value(session, "(vla-get-OwnerID lay)"), 10).ToString("X") + "\""
            );
        }

        [Fact]
        public void UpdateAndHighlight_GiveNilAndChangeNothing()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A1");

            // AutoCAD 2021 (GUI g2): (vla-Update O) nil, (vla-Highlight O :vlax-false) nil.
            Assert.Equal("nil", Value(session, "(vla-Update o)"));
            Assert.Equal("nil", Value(session, "(vla-Highlight o :vlax-false)"));
        }

        #endregion

        #region Writing

        [Fact]
        public void PuttingTheCommonMembers_ChangesTheEntityAsEntgetSeesIt()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vla-put-Color O 1) group 62 1; (vla-put-LinetypeScale O 2) 2.0; (vla-put-Visible O 0)
            // false, -1 true; (vla-put-Layer O "ghoff") sets the layer as the table spells it.
            Assert.Equal("nil", Value(session, "(vla-put-Color o 1)"));
            Assert.Equal("1", Value(session, "(cdr (assoc 62 (entget a2)))"));
            Assert.Equal("nil", Value(session, "(vla-put-LinetypeScale o 2)"));
            Assert.Equal("2.0", Value(session, "(vla-get-LinetypeScale o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Visible o 0)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Visible o -1)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Visible o :vlax-false)"));
            Assert.Equal(":vlax-false", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Visible o T)"));
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Layer o \"STEEL\")"));
            Assert.Equal("\"Steel\"", Value(session, "(vla-get-Layer o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Lineweight o 50)"));
            Assert.Equal("50", Value(session, "(vla-get-Lineweight o)"));
        }

        [Fact]
        public void ColorByLayer_TakesGroup62Out()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");
            Value(session, "(vla-put-Color o 1)");

            // AutoCAD 2021 (GUI g2): (vla-put-Color O 256) leaves no group 62.
            Assert.Equal("nil", Value(session, "(vla-put-Color o 256)"));
            Assert.Equal("nil", Value(session, "(assoc 62 (entget a2))"));
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void AnIntegerProperty_TakesARealAndANumericString()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vla-put-Color O 1.5) nil, (vla-put-Color O "1") nil; D204: half to even (ours for the
            // value set: 1.5 gives 2, 2.5 gives 2).
            Assert.Equal("nil", Value(session, "(vla-put-Color o 1.5)"));
            Assert.Equal("2", Value(session, "(vla-get-Color o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Color o 2.5)"));
            Assert.Equal("2", Value(session, "(vla-get-Color o)"));
            Assert.Equal("nil", Value(session, "(vla-put-Color o \"3\")"));
            Assert.Equal("3", Value(session, "(vla-get-Color o)"));
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vla-put-Color o \"abc\")")
            );
        }

        [Fact]
        public void AColorOutOfRange_IsTheErrorOfAutoCad()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vla-put-Color O 300) Automation Error. Invalid argument color in IAcadEntity::put_Color.
            Assert.Equal(
                "Automation Error. Invalid argument color in IAcadEntity::put_Color",
                Error(session, "(vla-put-Color o 300)")
            );
            Assert.Equal(
                "Automation Error. Invalid argument color in IAcadEntity::put_Color",
                Error(session, "(vla-put-Color o -1)")
            );
        }

        [Fact]
        public void ABooleanIsGivenAsTAMinusOneZeroOrTheSymbols_AndNilIsRefused()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vla-put-Visible O nil) ActiveX Server returned an error: Parameter not optional.
            Assert.Equal(
                "ActiveX Server returned an error: Parameter not optional",
                Error(session, "(vla-put-Visible o nil)")
            );
            Assert.Equal(":vlax-true", Value(session, "(vla-get-Visible o)"));
            Assert.Equal("ActiveX Server returned an error: Type mismatch", Error(session, "(vla-put-Visible o 5)"));
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vla-put-Visible o \"yes\")")
            );
        }

        [Fact]
        public void VlaxPutOfAScalar_Works_AndOfAPointFails()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2, g3): (vlax-put O 'Color 3) works, (vlax-put O 'Visible :vlax-true) works; of a point
            // property it fails with Exception occurred whatever it is given. No O1 property is a point, so the case is
            // SelectAtPoint's argument: a list gives the coercion error.
            Assert.Equal("nil", Value(session, "(vlax-put o 'Color 3)"));
            Assert.Equal("3", Value(session, "(vlax-get o 'Color)"));
            Assert.Equal("nil", Value(session, "(vlax-put o 'Visible :vlax-true)"));
            Assert.Equal("nil", Value(session, "(vlax-put-property o \"Color\" 4)"));
            Assert.Equal("4", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void AReadOnlyPropertyWritten_IsATypeMismatch()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vlax-put-property O 'LENGTH 5.0) ActiveX Server returned an error: Type mismatch.
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vlax-put-property o 'Handle \"1\")")
            );
            Assert.Equal(
                "ActiveX Server returned an error: Type mismatch",
                Error(session, "(vlax-put-property o 'ObjectName \"1\")")
            );
        }

        [Fact]
        public void PutLayerOfAMissingLayer_IsKeyNotFound_AndALayerIsFoundInAnyCase()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vla-put-Layer O "NOSUCH") Automation Error. Key not found, "ghoff" sets GHOFF; the same
            // for a linetype.
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-Layer o \"NOSUCH\")"));
            Assert.Equal("nil", Value(session, "(tblsearch \"LAYER\" \"NOSUCH\")"));
            Assert.Equal("nil", Value(session, "(vla-put-Layer o \"oFF\")"));
            Assert.Equal("\"Off\"", Value(session, "(vla-get-Layer o)"));
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-put-Linetype o \"NOSUCHLT\")"));
            Assert.Equal("nil", Value(session, "(vla-put-Linetype o \"bylayer\")"));
            Assert.Equal("\"ByLayer\"", Value(session, "(vla-get-Linetype o)"));
        }

        [Fact]
        public void ALinetypeScaleOfZero_IsRefused()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // ours: D243 (a scale is above nought; the error is the one of an out-of-range value).
            Assert.Equal("Automation Error. Invalid argument", Error(session, "(vla-put-LinetypeScale o 0)"));
            Assert.Equal("1.0", Value(session, "(vla-get-LinetypeScale o)"));
        }

        [Fact]
        public void TrueColor_IsACopyThatChangesTheEntityOnlyWhenPutBack()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");
            Value(session, "(setq tc (vla-get-TrueColor o))");

            // AutoCAD 2021 (GUI g2): TrueColor ColorIndex 256, ColorMethod 192, Red 0, EntityColor -1073741824; a new value at
            // each read; (vla-put-ColorIndex tc 5) leaves Color 256 until (vla-put-TrueColor o tc), then 5.
            Assert.Equal("#<VLA-OBJECT IAcadAcCmColor @>", Value(session, "tc"));
            Assert.Equal("nil", Value(session, "(eq tc (vla-get-TrueColor o))"));
            Assert.Equal("256", Value(session, "(vla-get-ColorIndex tc)"));
            Assert.Equal("192", Value(session, "(vla-get-ColorMethod tc)"));
            Assert.Equal("0", Value(session, "(vla-get-Red tc)"));
            Assert.Equal("-1073741824", Value(session, "(vla-get-EntityColor tc)"));
            Assert.Equal("nil", Value(session, "(vla-put-ColorIndex tc 5)"));
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
            Assert.Equal("nil", Value(session, "(vla-put-TrueColor o tc)"));
            Assert.Equal("5", Value(session, "(vla-get-Color o)"));
            Assert.Equal("195", Value(session, "(vla-get-ColorMethod tc)"));
            Assert.Equal("-1023410171", Value(session, "(vla-get-EntityColor tc)"));
            Assert.Equal("0", Value(session, "(vla-get-Red tc)"));
        }

        [Fact]
        public void ARgbColor_HasItsComponentsAndEntityColor()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");
            Value(session, "(setq tc (vla-get-TrueColor o))");

            // AutoCAD 2021 (GUI g2): RGB -1040187392 + r*65536 + g*256 + b (ours beyond ByLayer, D243); reference: SetRGB,
            // ColorMethod 194 (acColorMethodByRGB).
            Assert.Equal("nil", Value(session, "(vla-SetRGB tc 255 128 0)"));
            Assert.Equal("194", Value(session, "(vla-get-ColorMethod tc)"));
            Assert.Equal("255", Value(session, "(vla-get-Red tc)"));
            Assert.Equal("128", Value(session, "(vla-get-Green tc)"));
            Assert.Equal("0", Value(session, "(vla-get-Blue tc)"));
            Assert.Equal(
                (-1040187392 + 255 * 65536 + 128 * 256).ToString(),
                Value(session, "(vla-get-EntityColor tc)")
            );
            Assert.Equal("nil", Value(session, "(vla-put-TrueColor o tc)"));
            Assert.Equal("(420 . 16744448)", Value(session, "(assoc 420 (entget a2))"));
        }

        [Fact]
        public void PointsAreGivenAsVariantsOfThreeDoubles()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq ss (vla-Add (vla-get-SelectionSets doc) \"P\"))");

            // AutoCAD 2021 (GUI g2): a list point is the coercion error (two spaces); a safearray of two doubles Automation
            // Error. Incorrect number of elements in SafeArray; a variant of three is taken.
            Assert.Equal(
                "lisp value has no coercion to VARIANT with this type:  (50 0 0)",
                Error(session, "(vla-SelectAtPoint ss '(50 0 0))")
            );
            Assert.Equal(
                "Automation Error. Incorrect number of elements in SafeArray",
                Error(
                    session,
                    "(vla-SelectAtPoint ss (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(50 0)))"
                )
            );
            Assert.Equal("nil", Value(session, "(vla-SelectAtPoint ss (vlax-3d-point 50 0 0))"));
            Assert.Equal("1", Value(session, "(vla-get-Count ss)"));
        }

        #endregion

        #region Argument counts

        [Fact]
        public void TooManyAndTooFewArguments_AreTheErrorsOfAutoCad()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2, g3): (vla-get-Color o o) Too many actual parameters, (vla-get-Layers doc 5) the same,
            // (vla-put-Color o) too few actual parameters, (vlax-invoke O 'DELETE 5) Invalid number of parameters.
            Assert.Equal("Too many actual parameters", Error(session, "(vla-get-Color o o)"));
            Assert.Equal("Too many actual parameters", Error(session, "(vla-get-Layers doc 5)"));
            Assert.Equal("too few actual parameters", Error(session, "(vla-put-Color o)"));
            Assert.Equal("Too many actual parameters", Error(session, "(vla-put-Color o 1 2)"));
            Assert.Equal("Invalid number of parameters", Error(session, "(vlax-invoke o 'DELETE 5)"));
            Assert.Equal("Too many actual parameters", Error(session, "(vla-Delete o 5)"));
            Assert.Equal("too few actual parameters", Error(session, "(vla-Item (vla-get-Layers doc))"));
        }

        #endregion

        #region Erased, released, locked

        [Fact]
        public void AnErasedEntity_IsErasedForEveryFunctionAndWorksAgainWhenEntdelBringsItBack()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");
            Value(session, "(setq e a2)");

            // AutoCAD 2021 (GUI g2): after (vla-Delete O2): get and put Automation Error. Object was erased, vlax-erased-p T,
            // read and write enabled nil, vlax-vla-object->ename nil, (vlax-ename->vla-object e) nil; (entdel e) brings it
            // back and the old value works again; the value still prints as before.
            Assert.Equal("nil", Value(session, "(vla-Delete o)"));
            Assert.Equal("nil", Value(session, "(entget e)"));
            Assert.Equal("Automation Error. Object was erased", Error(session, "(vla-get-Layer o)"));
            Assert.Equal("Automation Error. Object was erased", Error(session, "(vla-put-Layer o \"0\")"));
            Assert.Equal("T", Value(session, "(vlax-erased-p o)"));
            Assert.Equal("nil", Value(session, "(vlax-read-enabled-p o)"));
            Assert.Equal("nil", Value(session, "(vlax-write-enabled-p o)"));
            Assert.Equal("nil", Value(session, "(vlax-vla-object->ename o)"));
            Assert.Equal("nil", Value(session, "(vlax-ename->vla-object e)"));
            Assert.Equal("#<VLA-OBJECT IAcadLine @>", Value(session, "o"));
            Assert.Equal("Automation Error. Object was erased", Error(session, "(vla-Delete o)"));
            Assert.Equal("<Entity name: *>", Value(session, "(entdel e)"));
            Assert.Equal("\"0\"", Value(session, "(vla-get-Layer o)"));
            Assert.Equal("nil", Value(session, "(vlax-erased-p o)"));
            Assert.Equal("T", Value(session, "(vlax-read-enabled-p o)"));
        }

        [Fact]
        public void AnEntityErasedByEntdel_IsErasedToTheObjectToo()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (ENTDEL e) then the object is erased.
            Value(session, "(entdel a2)");
            Assert.Equal("T", Value(session, "(vlax-erased-p o)"));
            Assert.Equal("Automation Error. Object was erased", Error(session, "(vla-get-Layer o)"));
        }

        [Fact]
        public void VlaxErasedPOfTheNilAnErasedNameGave_IsTheNonObjectError()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq e a2)");
            Value(session, "(entdel e)");

            // AutoCAD 2021 (GUI g3): (vlax-ename->vla-object GHL2) nil, then (vlax-erased-p nil) unable to get ObjectID: nil.
            Assert.Equal("nil", Value(session, "(vlax-ename->vla-object e)"));
            Assert.Equal("unable to get ObjectID: nil", Error(session, "(vlax-erased-p (vlax-ename->vla-object e))"));
        }

        [Fact]
        public void ReleasingAValue_MakesItPrintAsZerosAndRefusesEveryUse()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (vlax-release-object O3) 0, released-p T, O3 prints #<VLA-OBJECT 0000000000000000>, a get
            // gives null interface pointer: #<VLA-OBJECT 0000000000000000>, vlax-vla-object->ename nil, vlax-erased-p unable to
            // get ObjectID: ..., a second release the null interface pointer error, (eq O3 (vlax-ename->vla-object e)) nil.
            Assert.Equal("nil", Value(session, "(vlax-object-released-p o)"));
            Assert.Equal("0", Value(session, "(vlax-release-object o)"));
            Assert.Equal("T", Value(session, "(vlax-object-released-p o)"));
            Assert.Equal("#<VLA-OBJECT 0000000000000000>", Value(session, "o"));
            Assert.Equal("null interface pointer: #<VLA-OBJECT 0000000000000000>", Error(session, "(vla-get-Layer o)"));
            Assert.Equal(
                "null interface pointer: #<VLA-OBJECT 0000000000000000>",
                Error(session, "(vla-put-Layer o \"0\")")
            );
            Assert.Equal("nil", Value(session, "(vlax-vla-object->ename o)"));
            Assert.Equal("unable to get ObjectID: #<VLA-OBJECT 0000000000000000>", Error(session, "(vlax-erased-p o)"));
            Assert.Equal(
                "null interface pointer: #<VLA-OBJECT 0000000000000000>",
                Error(session, "(vlax-release-object o)")
            );
            Assert.Equal("nil", Value(session, "(eq o (vlax-ename->vla-object a2))"));
            Assert.Equal("\"0\"", Value(session, "(vla-get-Layer (vlax-ename->vla-object a2))"));
        }

        [Fact]
        public void AnEntityOnALockedLayer_CannotBeChangedOrDeleted_ButCanBeRead()
        {
            CadCommandSession session = Loaded();
            Object(session, "ok", "A8");

            // AutoCAD 2021 (GUI g2): write-enabled nil, read-enabled T, a get works, a put of Color, a put of Layer and Delete
            // are Automation Error. On locked layer, vlax-erased-p nil.
            Assert.Equal("nil", Value(session, "(vlax-write-enabled-p ok)"));
            Assert.Equal("T", Value(session, "(vlax-read-enabled-p ok)"));
            Assert.Equal("256", Value(session, "(vla-get-Color ok)"));
            Assert.Equal("Automation Error. On locked layer", Error(session, "(vla-put-Color ok 2)"));
            Assert.Equal("Automation Error. On locked layer", Error(session, "(vla-put-Layer ok \"0\")"));
            Assert.Equal("Automation Error. On locked layer", Error(session, "(vla-Delete ok)"));
            Assert.Equal("nil", Value(session, "(vlax-erased-p ok)"));
            Assert.Equal("\"Locked\"", Value(session, "(vla-get-Layer ok)"));
        }

        #endregion

        #region Selection sets

        [Fact]
        public void ASelectionSet_IsMadeFilledAndCounted()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq sss (vla-get-SelectionSets doc))");

            // AutoCAD 2021 (GUI g2): Count 0, Add "GHSS" an IAcadSelectionSet, Name, Count 0, SelectAtPoint adds the object
            // under the point once, Item 0 the line, a point off everything adds nothing.
            Assert.Equal("0", Value(session, "(vla-get-Count sss)"));
            Assert.Equal("#<VLA-OBJECT IAcadSelectionSet @>", Value(session, "(setq ss (vla-Add sss \"GHSS\"))"));
            Assert.Equal("\"GHSS\"", Value(session, "(vla-get-Name ss)"));
            Assert.Equal("0", Value(session, "(vla-get-Count ss)"));
            Assert.Equal("nil", Value(session, "(vla-SelectAtPoint ss (vlax-3d-point 50 0 0))"));
            Assert.Equal("1", Value(session, "(vla-get-Count ss)"));
            Assert.Equal("#<VLA-OBJECT IAcadLine @>", Value(session, "(vla-Item ss 0)"));
            Assert.Equal("\"" + HandleOf(session, "a1") + "\"", Value(session, "(vla-get-Handle (vla-Item ss 0))"));
            Assert.Equal("nil", Value(session, "(vla-SelectAtPoint ss (vlax-3d-point 50 0 0))"));
            Assert.Equal("1", Value(session, "(vla-get-Count ss)"));
            Assert.Equal("nil", Value(session, "(vla-SelectAtPoint ss (vlax-3d-point 500 500 0))"));
            Assert.Equal("1", Value(session, "(vla-get-Count ss)"));
            Assert.Equal("1", Value(session, "(vla-get-Count sss)"));
            Assert.Equal("\"AcDbLine\"", Value(session, "(vla-get-ObjectName (vla-Item ss 0))"));
        }

        [Fact]
        public void SelectionSetNames_AreCaseSensitiveAndOneNameIsOneSet()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq sss (vla-get-SelectionSets doc) ss (vla-Add sss \"GHSS\"))");

            // AutoCAD 2021 (GUI g2): (vla-Add SSS "GHSS") again Automation Error. The named selection set exists, "ghss"
            // succeeds beside "GHSS", (eq SS (vla-Item SSS "GHSS")) nil, (vla-Item SSS "NOSUCH") Key not found.
            Assert.Equal("Automation Error. The named selection set exists", Error(session, "(vla-Add sss \"GHSS\")"));
            Assert.Equal("#<VLA-OBJECT IAcadSelectionSet @>", Value(session, "(vla-Add sss \"ghss\")"));
            Assert.Equal("2", Value(session, "(vla-get-Count sss)"));
            Assert.Equal("nil", Value(session, "(eq ss (vla-Item sss \"GHSS\"))"));
            Assert.Equal("T", Value(session, "(equal ss (vla-Item sss \"GHSS\"))"));
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-Item sss \"NOSUCH\")"));
            Assert.Equal("\"ghss\"", Value(session, "(vla-get-Name (vla-Item sss \"ghss\"))"));
        }

        [Fact]
        public void SelectAll_WindowAndFilter_SelectLikeSsget()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq ss (vla-Add (vla-get-SelectionSets doc) \"S\"))");

            // AutoCAD 2021 (GUI g2): (vla-Select SS acSelectionSetAll) all, a window with two 3D points one line, a filter of
            // an integer safearray (0) and a variant safearray ("LINE") only the lines; ours: all is all of the current space.
            Assert.Equal("nil", Value(session, "(vla-Select ss 5)"));
            Assert.Equal("8", Value(session, "(vla-get-Count ss)"));
            Value(session, "(vla-Clear ss)");
            Assert.Equal("0", Value(session, "(vla-get-Count ss)"));
            Value(session, "(vla-Select ss 0 (vlax-3d-point -1 -1 0) (vlax-3d-point 101 1 0))");
            Assert.Equal("1", Value(session, "(vla-get-Count ss)"));
            Value(session, "(vla-Clear ss)");
            Value(
                session,
                "(vla-Select ss 5 nil nil (vlax-safearray-fill (vlax-make-safearray vlax-vbInteger '(0 . 0)) '(0)) (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 0)) '(\"LINE\")))"
            );
            Assert.Equal("3", Value(session, "(vla-get-Count ss)"));
            Value(session, "(setq kinds nil)");
            Value(session, "(vlax-for e ss (setq kinds (cons (vla-get-ObjectName e) kinds)))");
            Assert.Equal("(\"AcDbLine\" \"AcDbLine\" \"AcDbLine\")", Value(session, "kinds"));
        }

        [Fact]
        public void AddItemsAndRemoveItems_TakeSafearraysOfObjects()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq ss (vla-Add (vla-get-SelectionSets doc) \"S\"))");
            Object(session, "o1", "A1");
            Object(session, "o2", "A2");
            Value(
                session,
                "(vla-Select ss 5 nil nil (vlax-safearray-fill (vlax-make-safearray vlax-vbInteger '(0 . 0)) '(0)) (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 0)) '(\"LINE\")))"
            );

            // AutoCAD 2021 (GUI g2): RemoveItems a safearray of objects (3 to 2), AddItems a variant of one (2 to 3).
            Assert.Equal(
                "nil",
                Value(
                    session,
                    "(vla-RemoveItems ss (vlax-safearray-fill (vlax-make-safearray vlax-vbObject '(0 . 0)) (list o1)))"
                )
            );
            Assert.Equal("2", Value(session, "(vla-get-Count ss)"));
            Assert.Equal(
                "nil",
                Value(
                    session,
                    "(vla-AddItems ss (vlax-make-variant (vlax-safearray-fill (vlax-make-safearray vlax-vbObject '(0 . 0)) (list o1))))"
                )
            );
            Assert.Equal("3", Value(session, "(vla-get-Count ss)"));
            Assert.Equal("nil", Value(session, "(vla-Highlight ss :vlax-false)"));
        }

        [Fact]
        public void ADeletedSelectionSet_IsTheErrorOfAutoCadAndLeavesTheCollection()
        {
            CadCommandSession session = Loaded();
            Value(
                session,
                "(setq sss (vla-get-SelectionSets doc) ss (vla-Add sss \"GHSS\") other (vla-Add sss \"X\"))"
            );

            // AutoCAD 2021 (GUI g2): (vla-Delete SS) nil, then Count and Name give Automation Error. Selection Set was deleted,
            // (vlax-object-released-p SS) nil, Count of the collection one less, (vla-Item SSS "GHSS") Key not found.
            Assert.Equal("nil", Value(session, "(vla-Delete ss)"));
            Assert.Equal("Automation Error. Selection Set was deleted", Error(session, "(vla-get-Count ss)"));
            Assert.Equal("Automation Error. Selection Set was deleted", Error(session, "(vla-get-Name ss)"));
            Assert.Equal("nil", Value(session, "(vlax-object-released-p ss)"));
            Assert.Equal("1", Value(session, "(vla-get-Count sss)"));
            Assert.Equal("Automation Error. Key not found", Error(session, "(vla-Item sss \"GHSS\")"));
            Assert.Equal("#<VLA-OBJECT IAcadSelectionSet @>", Value(session, "(vla-Add sss \"GHSS\")"));
        }

        [Fact]
        public void ASelectionSetFromAnotherEvaluationStillHoldsItsObjects()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq ss (vla-Add (vla-get-SelectionSets doc) \"S\"))");
            Value(session, "(vla-SelectAtPoint ss (vlax-3d-point 50 0 0))");
            Value(session, "(vla-Delete (vlax-ename->vla-object a1))");

            // ours: D222 (a set holds handles; an erased object leaves it when counted).
            Assert.Equal("0", Value(session, "(vla-get-Count ss)"));
        }

        [Fact]
        public void ActiveSelectionSet_IsTheSetCurrentWithTheLastSsget()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq sss (vla-get-SelectionSets doc))");
            Value(session, "(setq x (ssget \"_X\"))");

            // AutoCAD 2021 (GUI g2): after (ssget "_X") the active set is named "CURRENT" and holds its objects, each read gives
            // a new value, reading it adds it to the collection (Count 1), then after a window of one object the count is 1.
            Value(session, "(setq ass (vla-get-ActiveSelectionSet doc))");
            Assert.Equal("\"CURRENT\"", Value(session, "(vla-get-Name ass)"));
            Assert.Equal("8", Value(session, "(vla-get-Count ass)"));
            Assert.Equal("nil", Value(session, "(eq ass (vla-get-ActiveSelectionSet doc))"));
            Assert.Equal("1", Value(session, "(vla-get-Count sss)"));
            Value(session, "(setq w (ssget \"_W\" '(-1 -1) '(101 1)))");
            Assert.Equal("1", Value(session, "(vla-get-Count (vla-get-ActiveSelectionSet doc))"));
            Assert.Equal("1", Value(session, "(vla-get-Count sss)"));
        }

        [Fact]
        public void PickfirstSelectionSet_IsNamedPickfirstAndJoinsTheCollection()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq sss (vla-get-SelectionSets doc) ss (vla-Add sss \"GHSS\"))");

            // AutoCAD 2021 (GUI g2): named "PICKFIRST", Count 0, then 1 after (sssetfirst nil (ssadd ename)); reading it adds it to
            // SelectionSets, and vlax-for lists the sets in the order they were made ("ghss" "CURRENT" "PICKFIRST").
            Assert.Equal("\"PICKFIRST\"", Value(session, "(vla-get-Name (vla-get-PickfirstSelectionSet doc))"));
            Assert.Equal("0", Value(session, "(vla-get-Count (vla-get-PickfirstSelectionSet doc))"));
            Value(session, "(sssetfirst nil (ssadd a3))");
            Assert.Equal("1", Value(session, "(vla-get-Count (vla-get-PickfirstSelectionSet doc))"));
            Value(session, "(setq x (ssget \"_X\"))");
            Value(session, "(vla-get-ActiveSelectionSet doc)");
            Value(session, "(setq names nil)");
            Value(session, "(vlax-for s sss (setq names (cons (vla-get-Name s) names)))");
            Assert.Equal("(\"GHSS\" \"PICKFIRST\" \"CURRENT\")", Value(session, "(reverse names)"));
            Assert.Equal("3", Value(session, "(vla-get-Count sss)"));
        }

        [Fact]
        public void DeletingTheCurrentSet_DoesNoHarm()
        {
            CadCommandSession session = Loaded();
            Value(session, "(setq x (ssget \"_X\") w (ssget \"_W\" '(-1 -1) '(101 1)))");

            // AutoCAD 2021 (GUI g2): (vla-Delete (vla-get-ActiveSelectionSet doc)) nil; read again it holds the previous selection.
            Assert.Equal("nil", Value(session, "(vla-Delete (vla-get-ActiveSelectionSet doc))"));
            Assert.Equal("1", Value(session, "(vla-get-Count (vla-get-ActiveSelectionSet doc))"));
        }

        #endregion

        #region Undo

        [Fact]
        public void TypedPutsWithNoCommandBetween_AreOneUndoStep()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g3 A): (vla-put-Color OU 3) and (vla-put-Color OU 4) typed one after the other, one U takes both.
            Value(session, "(vla-put-Color o 3)");
            Value(session, "(vla-put-Color o 4)");
            Assert.Equal("4", Value(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void APutAfterACommand_IsAStepOfItsOwn()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g3 D): REGEN, put 2, U gives the color before the put.
            Run(session, "REGEN");
            Value(session, "(vla-put-Color o 2)");
            Assert.Equal("2", Value(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void TwoPutsOfARoutineThatIsACommand_AreOneUndoStep()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");
            Value(session, "(defun c:ghp () (vla-put-Color o 1) (vla-put-Color o 2) (princ))");

            // AutoCAD 2021 (GUI g2): a c: command's two puts are one step.
            Run(session, "GHP");
            Assert.Equal("2", Value(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void TwoFormsInOnePutsAreOneUndoStep()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): (progn (vla-put-Color OU 5) (vla-put-Color OU 6)) then U goes back to the first color.
            Value(session, "(progn (vla-put-Color o 5) (vla-put-Color o 6))");
            Run(session, "U");
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void UndoMarks_GroupTheStepsBetweenThem()
        {
            CadCommandSession session = Loaded();
            Object(session, "o", "A2");

            // AutoCAD 2021 (GUI g2): StartUndoMark, two puts, EndUndoMark, then U returned to the color before the marks; an
            // EndUndoMark with none open gives nil. Ours: a command between the puts makes two steps, which the marks join.
            Assert.Equal("nil", Value(session, "(vla-EndUndoMark doc)"));
            Value(session, "(vla-StartUndoMark doc)");
            Value(session, "(vla-put-Color o 7)");
            Run(session, "REGEN");
            Value(session, "(vla-put-Color o 8)");
            Value(session, "(vla-EndUndoMark doc)");
            Assert.Equal("8", Value(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal("256", Value(session, "(vla-get-Color o)"));
        }

        #endregion

        #region COM to other programs

        [Theory]
        [InlineData(
            "(vlax-get-or-create-object \"Excel.Application\")",
            "vlax-get-or-create-object: COM to other programs is not supported by GeometryHelper.Cad: \"Excel.Application\""
        )]
        [InlineData(
            "(vlax-create-object \"Excel.Application\")",
            "vlax-create-object: COM to other programs is not supported by GeometryHelper.Cad: \"Excel.Application\""
        )]
        [InlineData(
            "(vlax-get-object \"Excel.Application\")",
            "vlax-get-object: COM to other programs is not supported by GeometryHelper.Cad: \"Excel.Application\""
        )]
        [InlineData(
            "(vlax-get-or-create-object \"GhNoSuch.Application\")",
            "vlax-get-or-create-object: COM to other programs is not supported by GeometryHelper.Cad: \"GhNoSuch.Application\""
        )]
        [InlineData(
            "(vlax-get-or-create-object 'GhProg)",
            "vlax-get-or-create-object: COM to other programs is not supported by GeometryHelper.Cad: \"GHPROG\""
        )]
        [InlineData("(vlax-get-or-create-object 5)", "bad argument type: (or stringp symbolp): 5")]
        [InlineData(
            "(vlax-import-type-library :tlb-filename \"x.tlb\")",
            "vlax-import-type-library: type libraries are not supported by GeometryHelper.Cad"
        )]
        public void AnyProgIdOfAnotherProgram_IsRefusedWithAMessage(string form, string error)
        {
            // AutoCAD 2021 (a2, GUI g2): nil for a ProgID it cannot find, and for 5 bad argument type: (or stringp symbolp): 5;
            // ours: D228, D246 (an error for every ProgID, so that the routine's *error* runs and says why; Excel is never started).
            Assert.Equal(error, Error(Loaded(), form));
        }

        [Fact]
        public void TheRefusal_CanBeCaughtAndReachesTheErrorHandler()
        {
            CadCommandSession session = Loaded();

            // ours: D228 (the routine of the maintainer that starts Excel stops at the first call, its *error* runs).
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(vl-catch-all-error-p (vl-catch-all-apply 'vlax-get-or-create-object '(\"Excel.Application\")))"
                )
            );
            Assert.Equal(
                "\"vlax-get-or-create-object: COM to other programs is not supported by GeometryHelper.Cad: \\\"Excel.Application\\\"\"",
                Value(
                    session,
                    "(vl-catch-all-error-message (vl-catch-all-apply 'vlax-get-or-create-object '(\"Excel.Application\")))"
                )
            );
            Value(session, "(defun *error* (m) (setq got m))");
            Value(session, "(vlax-get-or-create-object \"Excel.Application\")");
            Assert.StartsWith("\"vlax-get-or-create-object: COM to other programs", Value(session, "got"));
        }

        #endregion
    }
}
