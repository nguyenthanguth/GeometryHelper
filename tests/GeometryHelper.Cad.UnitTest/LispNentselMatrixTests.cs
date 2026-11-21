using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The matrix <c>nentsel</c> and <c>nentselp</c> give for an object inside a block reference (L5 part F, item 3): AutoCAD
    /// 2021 in its own window (L5 X2, probes pick2 to pick4) gave a 4 by 4 matrix row by row with the translation in the fourth
    /// column, not the 4 by 3 of its documentation. The drawing is the probes': a block with a line from (20,5) to (30,5), a
    /// reference at (100,0) and one at (200,0) at scale 2 turned 90 degrees.
    /// </summary>
    public sealed class LispNentselMatrixTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Probe()
        {
            CadDocument document = Blank();
            var block = new DxfBlockTableRecord { Name = "GHB" };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(20, 5, 0), new GeoPoint3(30, 5, 0)));
            document.ApplyEdit("T", null, s =>
            {
                s.Append(document.ModelSpace, new DxfBlockReference(new GeoPoint3(100, 0, 0), block));
                var turned = new DxfBlockReference(new GeoPoint3(200, 0, 0), block);
                s.Append(document.ModelSpace, turned);
                turned.ScaleFactors = new GeoVector3(2, 2, 2);
                turned.Rotation = Math.PI / 2;
            });
            return Session(document);
        }

        [Fact]
        public void NentselpOnTheLineOfAReference_GivesTheFourByFourMatrixWithTheTranslationInTheFourthColumn()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, X2 pick2): (nentselp '(125 5 0)) gives (<line> (125.0 5.0 0.0) ((1.0 0.0 0.0 100.0)
            // (0.0 1.0 0.0 0.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0)) (<insert>))
            Value(session, "(setq r (nentselp '(125 5 0)))");

            Assert.Equal("4", Value(session, "(length r)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (car r))))"));
            Assert.Equal("(125.0 5.0 0.0)", Value(session, "(cadr r)"));
            Assert.Equal(
                "((1.0 0.0 0.0 100.0) (0.0 1.0 0.0 0.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))",
                Value(session, "(caddr r)")
            );
            Assert.Equal("1", Value(session, "(length (cadddr r))"));
            Assert.Equal("\"INSERT\"", Value(session, "(cdr (assoc 0 (entget (car (cadddr r)))))"));
        }

        [Fact]
        public void NentselpOnTheLineOfATurnedScaledReference_GivesAutoCadsMatrix()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, X2 pick2): (nentselp '(190 50 0)) gives the matrix ((-4.44089e-16 -2.0 0.0 200.0)
            // (2.0 -4.44089e-16 0.0 0.0) (0.0 0.0 2.0 0.0) (0.0 0.0 0.0 1.0)); the zeros here are the cosine of a quarter turn
            Value(session, "(setq r (nentselp '(190 50 0)))");

            Assert.Equal("4", Value(session, "(length r)"));
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (caddr r) '((0.0 -2.0 0.0 200.0) (2.0 0.0 0.0 0.0) (0.0 0.0 2.0 0.0) (0.0 0.0 0.0 1.0)) 1e-9)"
                )
            );
            Assert.Equal("1", Value(session, "(length (cadddr r))"));
        }

        [Fact]
        public void NentselOnTheSameLine_GivesTheSameMatrixAsNentselp()
        {
            CadCommandSession session = Probe();

            Value(session, "(setq n (nentselp '(190 50 0)))");
            Value(session, "(setq m (nentsel))");
            Run(session, "190,50");

            Assert.Equal("T", Value(session, "(equal n m)"));
        }

        [Fact]
        public void ALineInAReferenceInAReference_GivesTheComposedMatrixAndBothReferencesInnermostFirst()
        {
            // ours: L2 D66 (the matrix carries the innermost block's coordinates into the world, the owners come innermost out);
            // the shape of each is AutoCAD 2021's (GUI, X2 pick2)
            CadDocument document = Blank();
            var inner = new DxfBlockTableRecord { Name = "GHIN" };
            var outer = new DxfBlockTableRecord { Name = "GHOUT" };
            document.Database.BlockTable.Add(inner);
            document.Database.BlockTable.Add(outer);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            outer.AppendEntity(new DxfBlockReference(new GeoPoint3(10, 5, 0), inner));
            document.ApplyEdit("T", null, s =>
                s.Append(document.ModelSpace, new DxfBlockReference(new GeoPoint3(100, 50, 0), outer))
            );
            CadCommandSession session = Session(document);

            Value(session, "(setq r (nentselp '(115 55 0)))");

            Assert.Equal("4", Value(session, "(length r)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (car r))))"));
            Assert.Equal(
                "((1.0 0.0 0.0 110.0) (0.0 1.0 0.0 55.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))",
                Value(session, "(caddr r)")
            );
            Assert.Equal("2", Value(session, "(length (cadddr r))"));
            Assert.Equal(
                "(\"GHIN\" \"GHOUT\")",
                Value(session, "(mapcar '(lambda (e) (cdr (assoc 2 (entget e)))) (cadddr r))")
            );
        }
    }
}
