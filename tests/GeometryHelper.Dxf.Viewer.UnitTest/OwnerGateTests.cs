using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class OwnerGateTests
    {
        private sealed class Model : ICadEntityOwner
        {
            private readonly CadEditRule _rule;

            public Model(CadEditRule rule)
            {
                _rule = rule;
            }

            public string Name => "Model";

            public CadEditRule GetEditRule(DxfEntity entity) => _rule;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }

        private static CadDocument Owned(CadEditRule rule, out string handle)
        {
            DxfDatabase database = TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Line(0, 0, 100, 0, handle: "A1"),
                TestDrawings.Line(0, 50, 100, 50, handle: "A2")
            );
            DxfEntity line = database.ModelSpace.OfType<DxfLine>().Single(e => e.Handle == "A1");
            new CadOwnerTag("Model", "view-1").WriteTo(line);
            CadDocument document = CadDocument.From(database, "test.dxf");
            document.Owners.Register(new Model(rule));
            handle = "A1";
            return document;
        }

        [Fact]
        public void AMoveOnlyEntityCanBeMovedButNotRotatedOrErased()
        {
            CadDocument document = Owned(CadEditRule.MoveOnly, out string handle);
            var move = new CadEditRecord(CadEditRecordKind.Transformed, handle, "Move");
            var rotate = new CadEditRecord(CadEditRecordKind.Transformed, handle, "Rotate");
            var erase = new CadEditRecord(CadEditRecordKind.Erased, handle);
            DxfEntity entity = document.FindEntity(handle);

            Assert.True(document.EditGate.Allows(document, entity, move, out _));
            Assert.False(document.EditGate.Allows(document, entity, rotate, out string why));
            Assert.Equal("This line belongs to the model and can only be moved.", why);
            Assert.False(document.EditGate.Allows(document, entity, erase, out _));
        }

        [Fact]
        public void AReadOnlyEntityRefusesAMoveThroughTheDocumentAndStaysWhereItWas()
        {
            CadDocument document = Owned(CadEditRule.ReadOnly, out string handle);
            string before = document.Database.ToText();
            var planned = new[] { new CadEditRecord(CadEditRecordKind.Transformed, handle, "Move") };

            CadEditResult result = document.ApplyEdit("MOVE", planned, s =>
                document
                    .FindEntity(handle)
                    .TransformBy(
                        GeometryHelper.Geometry.GeoTransform3.Translation(
                            new GeometryHelper.Geometry.GeoVector3(5, 5, 0)
                        )
                    )
            );

            Assert.True(result.Refused);
            Assert.Contains("belongs to the model", result.Message);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void AnEntityWithNoOwnerIsEditedFreely()
        {
            CadDocument document = Owned(CadEditRule.ReadOnly, out _);
            DxfEntity free = document.FindEntity("A2");

            Assert.True(
                document.EditGate.Allows(document, free, new CadEditRecord(CadEditRecordKind.Erased, "A2"), out _)
            );
        }

        [Fact]
        public void ACopyOfAnOwnedEntityCarriesNoOwnerTag()
        {
            CadDocument document = Owned(CadEditRule.Edit, out string handle);
            var session = new CadCommandSession(document);

            session.Start("COPY");
            session.FeedSelection(new[] { handle });
            session.Enter();
            session.Feed(new GeometryHelper.Geometry.GeoPoint2(0, 0));
            session.Feed(new GeometryHelper.Geometry.GeoPoint2(0, 10));
            session.Escape();

            int tagged = document.Database.ModelSpace.Count(e => CadOwnerTag.TryRead(e, out _));
            int lines = document.Database.ModelSpace.OfType<DxfLine>().Count();
            Assert.Equal(1, tagged);
            Assert.Equal(3, lines);
        }
    }
}
