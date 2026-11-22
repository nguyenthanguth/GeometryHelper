using System;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>vlax-dump-object</c> against the full AutoCAD 2021 (probes gd1 and gd2 of the C4a measurements): the lines it writes
    /// for a line, a circle, a text, a layer, the model space and the other interfaces, the methods that the second argument
    /// asks for, and its errors. AutoCAD lists every member of an interface and the presenter those its object model has, so
    /// the rows here are the lines of members both have, and the order and layout of the lot.
    /// </summary>
    public sealed class LispDumpObjectTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Drawn()
        {
            CadCommandSession session = Session(Blank());
            session.Feed("(setvar \"CMDECHO\" 0)");
            session.Feed("(vl-load-com)");
            session.Feed("(command \"_.LINE\" \"0,0\" \"10,5\" \"\")");
            session.Feed("(setq l (vlax-ename->vla-object (entlast)))");
            session.Feed("(command \"_.CIRCLE\" \"5,5\" \"3\")");
            session.Feed("(setq c (vlax-ename->vla-object (entlast)))");
            session.Feed("(command \"_.TEXT\" \"0,0\" \"2.5\" \"0\" \"hello\")");
            session.Feed("(setq tx (vlax-ename->vla-object (entlast)))");
            session.Feed("(setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))");
            return session;
        }

        private static string[] Dump(CadCommandSession session, string form)
        {
            string[] lines = session.Lisp.Run(form).Output.Split('\n');
            return lines.Select(l => l.TrimEnd()).ToArray();
        }

        #region What it writes

        [Fact]
        public void ALine_IsDumpedWithItsInterfaceItsPropertiesByNameAndTheValueT()
        {
            // AutoCAD 2021 (gd1): ; IAcadLine: AutoCAD Line Interface, ; Property values:, the properties in alphabetical order
            // with (RO) on those that cannot be set, and T after the lines.
            string[] lines = Dump(Drawn(), "(vlax-dump-object l)");

            Assert.Equal("; IAcadLine: AutoCAD Line Interface", lines[0]);
            Assert.Equal("; Property values:", lines[1]);
            Assert.Equal("T", lines[lines.Length - 1]);
            Assert.Contains(";   Angle (RO) = 0.463648", lines);
            Assert.Contains(";   Delta (RO) = (10.0 5.0 0.0)", lines);
            Assert.Contains(";   EndPoint = (10.0 5.0 0.0)", lines);
            Assert.Contains(";   Layer = \"0\"", lines);
            Assert.Contains(";   Length (RO) = 11.1803", lines);
            Assert.Contains(";   LinetypeScale = 1.0", lines);
            Assert.Contains(";   Lineweight = -1", lines);
            Assert.Contains(";   ObjectName (RO) = \"AcDbLine\"", lines);
            Assert.Contains(";   StartPoint = (0.0 0.0 0.0)", lines);
            Assert.Contains(";   Thickness = 0.0", lines);
            Assert.Contains(";   Visible = -1", lines);
            Assert.StartsWith(
                ";   Application (RO) = #<VLA-OBJECT IAcadApplication ",
                lines.Single(l => l.StartsWith(";   Application"))
            );

            string[] names = lines
                .Where(l => l.StartsWith(";   ", StringComparison.Ordinal))
                .Select(l => l.Substring(4).Split(' ')[0])
                .ToArray();
            Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(), names);
            Assert.DoesNotContain("; Methods supported:", lines);
        }

        [Fact]
        public void ACircleAndATextAreDumpedUnderTheirOwnInterfaces()
        {
            // AutoCAD 2021 (gd1): ; IAcadCircle: AutoCAD Circle Interface with Radius = 3.0, Area = 28.2743, Center = (5.0 5.0 0.0)
            // and ; IAcadText: AutoCAD Text Interface with TextString = "hello", Height = 2.5.
            CadCommandSession session = Drawn();

            string[] circle = Dump(session, "(vlax-dump-object c)");
            Assert.Equal("; IAcadCircle: AutoCAD Circle Interface", circle[0]);
            Assert.Contains(";   Radius = 3.0", circle);
            Assert.Contains(";   Area = 28.2743", circle);
            Assert.Contains(";   Center = (5.0 5.0 0.0)", circle);
            Assert.Contains(";   Circumference = 18.8496", circle);

            string[] text = Dump(session, "(vlax-dump-object tx)");
            Assert.Equal("; IAcadText: AutoCAD Text Interface", text[0]);
            Assert.Contains(";   TextString = \"hello\"", text);
            Assert.Contains(";   Height = 2.5", text);
            Assert.Contains(";   InsertionPoint = (0.0 0.0 0.0)", text);
        }

        [Fact]
        public void TheSecondArgument_AsksForTheMethodsAndNilDoesNot()
        {
            // AutoCAD 2021 (gd1): (vlax-dump-object l T) adds ; Methods supported: and ;   Name (parameters) for each, by name;
            // (vlax-dump-object l nil) and with a number other than nil: nil leaves them out, a number puts them in.
            CadCommandSession session = Drawn();

            string[] with = Dump(session, "(vlax-dump-object l T)");
            int at = Array.IndexOf(with, "; Methods supported:");
            Assert.True(at > 0);
            Assert.Contains(";   Delete (0)", with);
            Assert.Contains(";   GetBoundingBox (2)", with);
            Assert.Contains(";   Highlight (1)", with);
            Assert.Contains(";   Update (0)", with);
            string[] methods = with.Skip(at + 1)
                .Where(l => l.StartsWith(";   ", StringComparison.Ordinal))
                .Select(l => l.Substring(4).Split(' ')[0])
                .ToArray();
            Assert.Equal(methods.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray(), methods);

            Assert.DoesNotContain("; Methods supported:", Dump(session, "(vlax-dump-object l nil)"));
            Assert.Contains("; Methods supported:", Dump(session, "(vlax-dump-object l 5)"));
        }

        [Fact]
        public void TheOtherInterfaces_HaveTheDescriptionsOfAutoCads()
        {
            // AutoCAD 2021 (gd1, gd2): the line after the interface's name.
            CadCommandSession session = Drawn();

            Assert.Equal(
                "; IAcadLayer: A logical grouping of data, similar to transparent acetate overlays on a drawing",
                Dump(session, "(vlax-dump-object (vla-get-ActiveLayer doc))")[0]
            );
            Assert.Equal(
                "; IAcadModelSpace: A special Block object containing all model space entities",
                Dump(session, "(vlax-dump-object (vla-get-ModelSpace doc))")[0]
            );
            Assert.Equal("; IAcadDocument: An AutoCAD drawing", Dump(session, "(vlax-dump-object doc)")[0]);
            Assert.Equal(
                "; IAcadApplication: An instance of the AutoCAD application",
                Dump(session, "(vlax-dump-object (vlax-get-acad-object))")[0]
            );
            Assert.Equal(
                "; IAcadLayers: The collection of all layers in the drawing",
                Dump(session, "(vlax-dump-object (vla-get-Layers doc))")[0]
            );
            Assert.Equal(
                "; IAcadTextStyles: The collection of all text styles in the drawing",
                Dump(session, "(vlax-dump-object (vla-get-TextStyles doc))")[0]
            );
        }

        [Fact]
        public void AnInterfaceWithNoDescriptionOfItsOwn_IsWordedAsMostEntitiesAre()
        {
            // AutoCAD 2021 (gd2): IAcadArc "AutoCAD Arc Interface", IAcadEllipse "AutoCAD Ellipse Interface".
            CadCommandSession session = Drawn();
            session.Feed("(entmake '((0 . \"ARC\") (10 0.0 0.0 0.0) (40 . 5.0) (50 . 0.0) (51 . 1.5)))");

            Assert.Equal(
                "; IAcadArc: AutoCAD Arc Interface",
                Dump(session, "(vlax-dump-object (vlax-ename->vla-object (entlast)))")[0]
            );
        }

        #endregion

        #region What it refuses

        [Theory]
        [InlineData("(vlax-dump-object 5)", "; error: bad argument type: VLA-OBJECT 5")] // AutoCAD 2021 (gd1)
        [InlineData("(vlax-dump-object nil)", "; error: bad argument type: VLA-OBJECT nil")] // AutoCAD 2021 (gd1)
        [InlineData("(vlax-dump-object)", "; error: too few arguments")] // AutoCAD 2021 (gd1)
        [InlineData("(vlax-dump-object l T 1)", "; error: too many arguments")] // AutoCAD 2021 (gd1)
        [InlineData(
            "(vlax-dump-object (vlax-make-variant 5))",
            "; error: bad argument type: VLA-OBJECT #<variant 3 5>"
        )] // AutoCAD 2021 (gd1)
        public void WrongArguments_GiveAutoCadsErrors(string form, string output) =>
            Assert.Equal(output, Drawn().Lisp.Run(form).Output);

        [Fact]
        public void AReleasedObject_IsTheErrorOfAnyOtherFunction_AndBeforeVlLoadComTheFunctionIsNotThere()
        {
            CadCommandSession session = Drawn();
            session.Feed("(vlax-release-object l)");

            Assert.StartsWith("; error: null interface pointer", session.Lisp.Run("(vlax-dump-object l)").Output);
            Assert.Equal(
                "; error: no function definition: VLAX-DUMP-OBJECT",
                new LispInterpreter().Run("(vlax-dump-object 5)").Output
            );
        }

        #endregion
    }
}
