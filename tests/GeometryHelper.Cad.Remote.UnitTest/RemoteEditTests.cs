using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteEditTests
    {
        private const double Millimetre = 1e-9;

        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        /// <summary>A drawing with the block TAG: a line 10 long and the attribute MARK, inserted at (100, 100) with the value B-12.</summary>
        private static CadDocument WithTag(string name)
        {
            var db = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "TAG" };
            db.BlockTable.Add(block);
            block.AppendEntity(Line(0, 0, 10, 0));
            var definition = new DxfAttributeDefinition
            {
                Tag = "MARK",
                Prompt = "Mark",
                TextString = "?",
                Position = new GeoPoint3(0, 5, 0),
                Height = 2.5,
            };
            block.AppendEntity(definition);
            var insert = new DxfBlockReference(new GeoPoint3(100, 100, 0), block);
            db.ModelSpace.AppendEntity(insert);
            var attribute = new DxfAttributeReference();
            attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
            attribute.TextString = "B-12";
            insert.AttributeCollection.AppendAttribute(attribute);
            return CadDocument.From(db, name);
        }

        private static (Harness harness, TestHost host, CadDocument document, CadRemoteDocument remote) Start(
            CadDocument document
        )
        {
            var harness = new Harness();
            var host = new TestHost();
            host.Add(document);
            harness.Serve(host);
            CadRemoteDocument remote = harness.Connect().Documents.Single();
            return (harness, host, document, remote);
        }

        private static CadDocument Blank() => CadDocument.From(new DxfDatabase("AC1027"), "A-101.dxf");

        [Fact]
        public void ALineInserted_ComesBackByValue_AndModifyKeepsItsHandle_AndDeleteTakesItOut()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                string ticket = remote.Insert(Line(1, 2, 301, 2), null);
                Assert.Empty(document.Database.ModelSpace);
                Assert.StartsWith("~", ticket, StringComparison.Ordinal);

                string handle = Assert.Single(remote.CommitChanges());

                Assert.Equal(handle, document.Database.ModelSpace.Single().Handle);
                var read = (DxfLine)remote.GetObject(ticket);
                Assert.Equal(1.0, read.StartPoint.X, 9);
                Assert.Equal(301.0, read.EndPoint.X, 9);
                Assert.Equal(handle, remote.HandleOf(read));

                read.EndPoint = new GeoPoint3(401, 2, 0);
                remote.Modify(read);
                Assert.Equal(301.0, ((DxfLine)document.FindEntity(handle)).EndPoint.X, 9);
                Assert.Equal(new string[0], remote.CommitChanges());

                Assert.Equal(handle, document.Database.ModelSpace.Single().Handle);
                Assert.Equal(401.0, ((DxfLine)remote.GetObject(handle)).EndPoint.X, 9);

                remote.Delete(handle);
                remote.CommitChanges();

                Assert.DoesNotContain(document.Database.ModelSpace, e => !e.IsErased);
                Assert.Null(remote.GetObject(handle));
            }
        }

        [Fact]
        public void APolylineWithABulge_ComesBackByValue()
        {
            var (harness, _, _, remote) = Start(Blank());
            using (harness)
            {
                var polyline = new DxfPolyline();
                polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
                polyline.AddVertexAt(1, new GeoPoint2(100, 0), 0.5, 0.0, 0.0);
                polyline.AddVertexAt(2, new GeoPoint2(100, 50), 0.0, 0.0, 0.0);
                polyline.Closed = true;
                polyline.Layer = "Steel";

                remote.Insert(polyline);
                string handle = remote.CommitChanges().Single();
                var back = (DxfPolyline)remote.GetObject(handle);

                Assert.Equal(3, back.NumberOfVertices);
                Assert.True(back.Closed);
                Assert.Equal(0.5, back.GetBulgeAt(1), 9);
                Assert.Equal(100.0, back.GetPoint2dAt(2).X, 9);
                Assert.Equal("Steel", back.Layer);
            }
        }

        [Fact]
        public void AnMTextInserted_AndItsWordsChanged_KeepTheHandle()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                var text = new DxfMText
                {
                    Contents = "Plate PL10 x 200",
                    Location = new GeoPoint3(10, 20, 0),
                    TextHeight = 5.0,
                };

                remote.Insert(text);
                string handle = remote.CommitChanges().Single();
                var back = (DxfMText)remote.GetObject(handle);
                Assert.Equal("Plate PL10 x 200", back.Contents);
                Assert.Equal(5.0, back.TextHeight, 9);

                back.Contents = "Plate PL12 x 200";
                remote.Modify(back);
                remote.CommitChanges();

                Assert.Equal("Plate PL12 x 200", ((DxfMText)document.FindEntity(handle)).Contents);
                Assert.Single(document.Database.ModelSpace, e => !e.IsErased);
            }
        }

        [Fact]
        public void ADimensionMadeInCode_IsDrawnByTheApp_AndRedrawnWhenItsPointMoves()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                var dimension = new DxfRotatedDimension(
                    0.0,
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(100, 20, 0),
                    string.Empty,
                    null
                );

                remote.Insert(dimension);
                string handle = remote.CommitChanges().Single();
                var back = (DxfRotatedDimension)remote.GetObject(handle);

                Assert.Equal(100.0, back.Measurement, 6);
                int picture = back.DimBlock.Count(e => !e.IsErased);
                Assert.True(picture >= 4, "the picture holds the lines, arrows and words: " + picture);
                string block = ((DxfDimension)document.FindEntity(handle)).DimBlock.Name;
                int blocks = document.Database.BlockTable.Count();

                back.XLine2Point = new GeoPoint3(150, 0, 0);
                remote.Modify(back);
                remote.CommitChanges();

                var moved = (DxfRotatedDimension)document.FindEntity(handle);
                Assert.Equal(150.0, moved.Measurement, 6);
                Assert.Equal(block, moved.DimBlock.Name);
                Assert.Equal(blocks, document.Database.BlockTable.Count());
                var words = moved.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased);
                Assert.Contains("150", words.Contents, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ABlockReferenceWithAttributes_ComesBackByValue_ItsValueChanged_AndCopiedAgain()
        {
            var (harness, _, document, remote) = Start(WithTag("A-101.dxf"));
            using (harness)
            {
                var insert = (DxfBlockReference)
                    remote.GetObjects(new CadObjectFilter { Kind = typeof(DxfBlockReference) }).Single();
                string handle = remote.HandleOf(insert);
                Assert.Equal("TAG", insert.Name);
                Assert.Equal("B-12", insert.AttributeCollection.Single().TextString);

                insert.AttributeCollection.Single().TextString = "C-7";
                remote.Modify(insert);
                remote.CommitChanges();

                var changed = (DxfBlockReference)document.FindEntity(handle);
                Assert.Equal("C-7", changed.AttributeCollection.Single().TextString);
                Assert.Equal("MARK", changed.AttributeCollection.Single().Tag);
                Assert.Equal(100.0, changed.Position.X, 9);

                // The copy of the INSERT read, moved: a second INSERT of TAG with the same attribute.
                var again = (DxfBlockReference)remote.GetObject(handle);
                again.Position = new GeoPoint3(300, 100, 0);
                string copied = remote.Insert(again);
                string second = remote.CommitChanges().Single();

                Assert.NotEqual(handle, second);
                var made = (DxfBlockReference)document.FindEntity(second);
                Assert.Equal("TAG", made.Name);
                Assert.Equal(300.0, made.Position.X, 9);
                Assert.Equal("C-7", made.AttributeCollection.Single().TextString);
                Assert.Equal(1, document.Database.BlockTable.Count(b => b.Name == "TAG"));
                Assert.Equal(second, remote.HandleOf(remote.GetObject(copied)));
            }
        }

        [Fact]
        public void AnInsertOfABlockTheDrawingHasNot_ReadFromAnotherDrawing_BringsTheBlockWithIt()
        {
            var (harness, host, plain, remote) = Start(Blank());
            using (harness)
            {
                CadDocument tagged = host.Add(WithTag("B-202.dxf"));
                CadApplication app = harness.Connect();
                var source = app.Documents.Get("B-202.dxf");
                var insert = (DxfBlockReference)
                    source.GetObjects(new CadObjectFilter { Kind = typeof(DxfBlockReference) }).Single();

                remote = app.Documents.Get("A-101.dxf");
                remote.Insert(Line(0, 0, 5, 5));
                remote.Insert(insert);
                IReadOnlyList<string> handles = remote.CommitChanges();

                // The commit carried the definition of TAG, a line and its attribute, which the drawing had not.
                Assert.Equal(2, handles.Count);
                Assert.Equal(2, plain.Database.BlockTable["TAG"].Count(e => !e.IsErased));
                var made = (DxfBlockReference)plain.FindEntity(handles[1]);
                Assert.Equal("TAG", made.Name);
                Assert.Equal("B-12", made.AttributeCollection.Single().TextString);
                Assert.Equal("REMOTE", plain.NextUndo.Name);
                Assert.NotNull(tagged);
            }
        }

        [Fact]
        public void AHatchInserted_ComesBackByValue_AndItsPatternScaleChanged()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                var hatch = new DxfHatch();
                hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
                hatch.PatternScale = 2.0;
                hatch.AppendLoop(
                    DxfHatchLoopTypes.External,
                    new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) },
                    null
                );

                remote.Insert(hatch);
                string handle = remote.CommitChanges().Single();
                var back = (DxfHatch)remote.GetObject(handle);

                Assert.Equal("ANSI31", back.PatternName);
                Assert.Equal(2.0, back.PatternScale, 9);
                Assert.Equal(1, back.NumberOfLoops);
                Assert.Equal(600.0, back.Area, 6);

                back.PatternScale = 5.0;
                remote.Modify(back);
                remote.CommitChanges();

                var changed = (DxfHatch)document.FindEntity(handle);
                Assert.Equal(5.0, changed.PatternScale, 9);
                Assert.Equal("ANSI31", changed.PatternName);
                Assert.Equal(600.0, changed.Area, 6);
            }
        }

        [Fact]
        public void ALayerTheDrawingHasNot_IsMade_AndTheLayerBoxIsTold()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                var changes = new List<CadDocumentChangedEventArgs>();
                document.Changed += (s, e) => changes.Add(e);

                remote.Insert(Line(0, 0, 1, 1).WithLayer("Steel"));
                string handle = remote.CommitChanges().Single();

                Assert.True(document.Database.LayerTable.Has("Steel"));
                Assert.Equal("Steel", document.FindEntity(handle).Layer);
                CadDocumentChangedEventArgs seen = Assert.Single(changes);
                Assert.True(seen.TablesChanged);
            }
        }

        [Fact]
        public void ManyChanges_AreOneUndoStep_OneRedraw_AndUndoTakesThemAllBack()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                remote.Insert(Line(0, 0, 10, 0));
                remote.Insert(Line(0, 5, 10, 5));
                IReadOnlyList<string> first = remote.CommitChanges();
                string start = document.Database.ToText();
                var changes = new List<CadDocumentChangedEventArgs>();
                document.Changed += (s, e) => changes.Add(e);

                var moved = (DxfLine)remote.GetObject(first[0]);
                moved.StartPoint = new GeoPoint3(-5, 0, 0);
                remote.Modify(moved);
                remote.Delete(first[1]);
                remote.Insert(Line(1, 1, 2, 2));
                remote.Insert(Line(3, 3, 4, 4));
                IReadOnlyList<string> made = remote.CommitChanges();

                Assert.Equal(2, made.Count);
                CadDocumentChangedEventArgs seen = Assert.Single(changes, c => c.StepName == "REMOTE");
                Assert.Equal(CadChangeKind.Edit, seen.Kind);
                Assert.Contains(made[0], seen.Handles);
                Assert.Equal("REMOTE", document.NextUndo.Name);

                Assert.True(document.Undo());

                Assert.Equal(start, document.Database.ToText());
            }
        }

        [Fact]
        public void AnOwnerThatRefusesOneChange_RefusesAll_NothingIsApplied_AndTheMessageIsTheOwners()
        {
            var db = new DxfDatabase("AC1027");
            var mark = Line(0, 0, 100, 0);
            db.ModelSpace.AppendEntity(mark);
            new CadOwnerTag("Test.Owner", "MARK-1").WriteTo(mark);
            CadDocument document = CadDocument.From(db, "A-101.dxf");
            document.Owners.Register(new ReadOnlyOwner());
            var (harness, _, _, remote) = Start(document);
            using (harness)
            {
                string handle = mark.Handle;
                string start = document.Database.ToText();
                var read = (DxfLine)remote.GetObject(handle);
                read.EndPoint = new GeoPoint3(500, 0, 0);

                remote.Insert(Line(1, 1, 2, 2));
                remote.Modify(read);
                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());

                Assert.Contains("belongs to the model", refused.Message, StringComparison.Ordinal);
                Assert.Equal(start, document.Database.ToText());
                Assert.False(document.CanUndo);

                remote.Delete(handle);
                Assert.Contains(
                    "belongs to the model",
                    Assert.Throws<CadRemoteException>(() => remote.CommitChanges()).Message,
                    StringComparison.Ordinal
                );
                Assert.Equal(start, document.Database.ToText());
            }
        }

        [Fact]
        public void AnObjectOfAnOwnerWhoTakesEdits_IsChangedByTheOwner_NotByTheCommit()
        {
            var db = new DxfDatabase("AC1027");
            var mark = Line(0, 0, 100, 0);
            db.ModelSpace.AppendEntity(mark);
            new CadOwnerTag("Test.Owner", "EDIT-1").WriteTo(mark);
            CadDocument document = CadDocument.From(db, "A-101.dxf");
            var owner = new TakingOwner();
            document.Owners.Register(owner);
            var (harness, _, _, remote) = Start(document);
            using (harness)
            {
                var read = (DxfLine)remote.GetObject(mark.Handle);
                read.EndPoint = new GeoPoint3(500, 0, 0);

                remote.Modify(read);
                remote.CommitChanges();

                Assert.Single(owner.Taken);
                Assert.Equal(mark.Handle, owner.Taken[0].Handle);
                Assert.Equal(100.0, ((DxfLine)document.FindEntity(mark.Handle)).EndPoint.X, 9);
            }
        }

        [Fact]
        public void TheObjectsAsked_AreFilteredByKindLayerAndLayout_InTheOrderTheyWereMade()
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(Line(0, 0, 1, 0).WithLayer("Steel"));
            db.ModelSpace.AppendEntity(Line(0, 1, 1, 1).WithLayer("Concrete"));
            db.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(5, 5, 0), new GeoVector3(0, 0, 1), 2).WithLayer("Steel")
            );
            db.ModelSpace.AppendEntity(Line(0, 2, 1, 2).WithLayer("steel"));
            CadDocument document = CadDocument.From(db, "A-101.dxf");
            var (harness, _, _, remote) = Start(document);
            using (harness)
            {
                Assert.Equal(4, remote.GetObjects().Count);
                Assert.Equal(3, remote.GetObjects(new CadObjectFilter { Layer = "STEEL" }).Count);
                Assert.Equal(
                    new[] { 0.0, 2.0 },
                    remote
                        .GetObjects(new CadObjectFilter { Kind = typeof(DxfLine), Layer = "Steel" })
                        .Cast<DxfLine>()
                        .Select(l => l.StartPoint.Y)
                );
                Assert.Single(remote.GetObjects(new CadObjectFilter { Kind = typeof(DxfCircle) }));
                Assert.Equal(4, remote.GetObjects(new CadObjectFilter { Layout = "Model" }).Count);
                Assert.Empty(remote.GetObjects(new CadObjectFilter { Layout = "Layout1" }));
                Assert.Contains(
                    "no layout named Sheet9",
                    Assert
                        .Throws<CadRemoteException>(() => remote.GetObjects(new CadObjectFilter { Layout = "Sheet9" }))
                        .Message,
                    StringComparison.Ordinal
                );
                Assert.Throws<ArgumentException>(() => new CadObjectFilter { Kind = typeof(string) });
            }
        }

        [Fact]
        public void AnObjectInsertedIntoALayout_IsInThatLayoutAndNotInModelSpace()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                string layout = document.Spaces.First(s => !s.IsModel).Name;

                remote.Insert(Line(0, 0, 50, 0), layout);
                string handle = remote.CommitChanges().Single();

                Assert.Equal(layout, document.FindSpace(handle).Name);
                Assert.Empty(remote.GetObjects(new CadObjectFilter { Layout = "Model" }));
                Assert.Single(remote.GetObjects(new CadObjectFilter { Layout = layout.ToUpperInvariant() }));
                remote.Insert(Line(0, 0, 50, 0), "Sheet9");
                Assert.Contains(
                    "no layout named Sheet9",
                    Assert.Throws<CadRemoteException>(() => remote.CommitChanges()).Message,
                    StringComparison.Ordinal
                );
            }
        }

        [Fact]
        public void TheSelection_IsReadAndSetThroughHandles()
        {
            var (harness, _, document, remote) = Start(Blank());
            using (harness)
            {
                remote.Insert(Line(0, 0, 1, 0));
                remote.Insert(Line(0, 1, 1, 1));
                remote.Insert(Line(0, 2, 1, 2));
                var handles = remote.CommitChanges();
                Assert.Empty(remote.GetSelectedObjects());

                remote.Select(new[] { handles[2], handles[0] });

                Assert.Equal(
                    new[] { handles[0], handles[2] },
                    document.Selection.Handles.OrderBy(h => h, StringComparer.Ordinal)
                );
                Assert.Equal(new[] { handles[0], handles[2] }, remote.GetSelectedObjects().Select(remote.HandleOf));
                Assert.Contains(
                    "has no object ZZZ",
                    Assert.Throws<CadRemoteException>(() => remote.Select(new[] { "ZZZ" })).Message,
                    StringComparison.Ordinal
                );
                remote.Select(new string[0]);
                Assert.Empty(remote.GetSelectedObjects());
            }
        }

        [Fact]
        public void ATicketOfAnObjectNotCommitted_IsRefusedWhereAHandleIsNeeded_AndAnObjectNotReadCannotBeModified()
        {
            var (harness, _, _, remote) = Start(Blank());
            using (harness)
            {
                string ticket = remote.Insert(Line(0, 0, 1, 1));

                Assert.Contains(
                    "call CommitChanges first",
                    Assert.Throws<CadRemoteException>(() => remote.Delete(ticket)).Message,
                    StringComparison.Ordinal
                );
                Assert.Throws<ArgumentException>(() => remote.Modify(Line(0, 0, 1, 1)));
                Assert.Throws<ArgumentNullException>(() => remote.Insert(null));
                Assert.Null(remote.HandleOf(Line(0, 0, 1, 1)));
            }
        }

        [Fact]
        public void AnObjectReadFromAnotherDrawing_CanBeInsertedHere_ButNotModifiedHere()
        {
            var (harness, host, first, remote) = Start(Blank());
            using (harness)
            {
                CadDocument second = host.Add(CadDocument.From(new DxfDatabase("AC1027"), "B-202.dxf"));
                CadApplication app = harness.Connect();
                CadRemoteDocument target = app.Documents.Get("A-101.dxf");
                CadRemoteDocument other = app.Documents.Get("B-202.dxf");
                other.Insert(Line(7, 7, 8, 8).WithLayer("Imported"));
                other.CommitChanges();
                var read = other.GetObjects().Single();

                target.Insert(read);
                target.CommitChanges();

                Assert.Equal(7.0, ((DxfLine)first.Database.ModelSpace.Single()).StartPoint.X, 9);
                Assert.True(first.Database.LayerTable.Has("Imported"));
                Assert.Throws<ArgumentException>(() => target.Modify(read));
                Assert.Single(second.Database.ModelSpace);
            }
        }

        [Fact]
        public void ACommitWhileACommandRuns_IsRefused_UntilTheCommandEnds()
        {
            var (harness, host, document, remote) = Start(Blank());
            using (harness)
            {
                remote.SendCommand("LINE 0,0", TimeSpan.Zero);
                remote.Insert(Line(0, 0, 1, 1));

                CadRemoteException refused = Assert.Throws<CadRemoteException>(() => remote.CommitChanges());

                Assert.Contains("busy with the command", refused.Message, StringComparison.Ordinal);
                Assert.Empty(document.Database.ModelSpace);

                host.SessionOf(document).Escape();
                remote.Insert(Line(0, 0, 1, 1));
                Assert.Single(remote.CommitChanges());
            }
        }

        private sealed class ReadOnlyOwner : ICadEntityOwner
        {
            public string Name => "Test.Owner";

            public CadEditRule GetEditRule(DxfEntity entity) => CadEditRule.ReadOnly;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }

        private sealed class TakingOwner : ICadEntityOwner
        {
            public List<CadEdit> Taken { get; } = new List<CadEdit>();

            public string Name => "Test.Owner";

            public CadEditRule GetEditRule(DxfEntity entity) => CadEditRule.Edit;

            public bool TryApply(CadDocument document, CadEdit edit)
            {
                Taken.Add(edit);
                return true;
            }
        }
    }

    internal static class EntityExtensions
    {
        public static T WithLayer<T>(this T entity, string layer)
            where T : DxfEntity
        {
            entity.Layer = layer;
            return entity;
        }
    }
}
