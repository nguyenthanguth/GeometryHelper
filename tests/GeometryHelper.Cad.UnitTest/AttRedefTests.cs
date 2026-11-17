using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// ATTREDEF as AutoCAD 2021's Express Tools routine does it (checked against its own ATTREDEF.LSP run in accoreconsole): the
    /// block defined again from objects, each of its references put anew where it stood, an attribute of a tag the new block
    /// has keeping its value, a new one given its default, one the new block lacks dropped; the new references drawn on the
    /// current layer, as AutoCAD's are.
    /// </summary>
    public class AttRedefTests
    {
        private sealed class Fixture
        {
            public CadDocument Document;
            public string[] OldReferences;
            public string NewLine;
            public string NewTag2;
            public string NewTag3;
        }

        /// <summary>
        /// The block TB of a line, TAG1 (default d1) and TAG2 (default d2); two references of it, one at (50, 50) and one at (100, 100)
        /// scaled 2 and turned 30 degrees, both on layer WALLS, TAG1 and TAG2 of the first valued A1 and A2, of the second B1 and B2.
        /// In model space the new objects: a line from (0, 0) to (30, 0), TAG2 (default new2) and TAG3 (default d3).
        /// </summary>
        private static Fixture Build(bool newAttributes = true)
        {
            CadDocument source = CadDocument.From(CadTemplate.CreateMetric(), "blocks.dxf");
            DxfDatabase db = source.Database;
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            var block = new DxfBlockTableRecord { Name = "TB", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var tag1 = new DxfAttributeDefinition(new GeoPoint3(0, 5, 0), "d1", "TAG1", "One", null);
            var tag2 = new DxfAttributeDefinition(new GeoPoint3(0, 10, 0), "d2", "TAG2", "Two", null);
            block.AppendEntity(tag1);
            block.AppendEntity(tag2);

            foreach (
                var spec in new[]
                {
                    new
                    {
                        At = new GeoPoint3(50, 50, 0),
                        Scale = 1.0,
                        Turn = 0.0,
                        A = "A1",
                        B = "A2",
                    },
                    new
                    {
                        At = new GeoPoint3(100, 100, 0),
                        Scale = 2.0,
                        Turn = Math.PI / 6.0,
                        A = "B1",
                        B = "B2",
                    },
                }
            )
            {
                var reference = new DxfBlockReference(spec.At, block)
                {
                    Layer = "WALLS",
                    ScaleFactors = new GeoVector3(spec.Scale, spec.Scale, spec.Scale),
                    Rotation = spec.Turn,
                };
                db.ModelSpace.AppendEntity(reference);

                foreach (var pair in new[] { Tuple.Create(tag1, spec.A), Tuple.Create(tag2, spec.B) })
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(pair.Item1, reference.BlockTransform);
                    attribute.TextString = pair.Item2;
                    reference.AttributeCollection.AppendAttribute(attribute);
                }
            }

            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0)));
            db.ModelSpace.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, 5, 0), "new2", "TAG2", "Two", null));

            if (newAttributes)
            {
                db.ModelSpace.AppendEntity(
                    new DxfAttributeDefinition(new GeoPoint3(0, 10, 0), "d3", "TAG3", "Three", null)
                );
            }

            CadDocument document = CadDocument.From(DxfDatabase.Parse(db.ToText()), "blocks.dxf");
            return new Fixture
            {
                Document = document,
                OldReferences = All<DxfBlockReference>(document.ModelSpace).Select(r => r.Handle).ToArray(),
                NewLine = All<DxfLine>(document.ModelSpace).Single().Handle,
                NewTag2 = All<DxfAttributeDefinition>(document.ModelSpace).First(d => d.Tag == "TAG2").Handle,
                NewTag3 = newAttributes
                    ? All<DxfAttributeDefinition>(document.ModelSpace).First(d => d.Tag == "TAG3").Handle
                    : null,
            };
        }

        private static string[] NewObjects(Fixture f) =>
            new[] { f.NewLine, f.NewTag2, f.NewTag3 }.Where(h => h != null).ToArray();

        [Fact]
        public void TheBlockIsDefinedAgain_AndEachReferenceIsPutAnewKeepingItsTagsValues()
        {
            Fixture f = Build();
            CadCommandSession session = Session(f.Document);

            Run(session, "ATTREDEF");
            Assert.Equal("Enter name of the block you wish to redefine:", session.PromptText);
            Run(session, "tb");
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Contains("Select objects for new Block...", session.History);
            Run(session, NewObjects(f), Enter);
            Assert.Equal("Specify insertion base point of new Block:", session.PromptText);
            Run(session, P(0, 0));

            Assert.False(session.IsRunning);
            DxfBlockTableRecord block = f.Document.Database.BlockTable["TB"];
            Assert.Equal(
                new[] { "TAG2", "TAG3" },
                block.OfType<DxfAttributeDefinition>().Select(d => d.Tag).OrderBy(t => t, StringComparer.Ordinal)
            );
            Assert.Single(block.OfType<DxfLine>());

            // The objects were used up, as -BLOCK uses them.
            Assert.Empty(All<DxfLine>(f.Document.ModelSpace));
            Assert.Empty(All<DxfAttributeDefinition>(f.Document.ModelSpace));

            // The old references are gone; two new ones stand where they stood, at their scale and turn.
            Assert.All(f.OldReferences, h =>
                Assert.True(f.Document.FindEntity(h) == null || f.Document.FindEntity(h).IsErased)
            );
            var references = All<DxfBlockReference>(f.Document.ModelSpace).Where(r => !r.IsErased).ToList();
            Assert.Equal(2, references.Count);
            DxfBlockReference first = references.Single(r => (r.Position - new GeoPoint3(50, 50, 0)).Length < 1E-9);
            DxfBlockReference second = references.Single(r => (r.Position - new GeoPoint3(100, 100, 0)).Length < 1E-9);
            Assert.Equal(1.0, first.ScaleFactors.X, 9);
            Assert.Equal(2.0, second.ScaleFactors.X, 9);
            Assert.Equal(Math.PI / 6.0, second.Rotation, 9);

            // TAG2 keeps the value it had, TAG3 is new and takes the default, TAG1 is not in the new block and is dropped.
            Assert.Equal(
                new[] { "TAG2=A2", "TAG3=d3" },
                first
                    .AttributeCollection.Select(a => a.Tag + "=" + a.TextString)
                    .OrderBy(t => t, StringComparer.Ordinal)
            );
            Assert.Equal(
                new[] { "TAG2=B2", "TAG3=d3" },
                second
                    .AttributeCollection.Select(a => a.Tag + "=" + a.TextString)
                    .OrderBy(t => t, StringComparer.Ordinal)
            );
        }

        [Fact]
        public void TheNewReferences_AreOnTheCurrentLayer_AsAutoCadsAre()
        {
            Fixture f = Build();
            Run(Session(f.Document), "ATTREDEF", "TB", NewObjects(f), Enter, P(0, 0));

            foreach (
                DxfBlockReference reference in All<DxfBlockReference>(f.Document.ModelSpace).Where(r => !r.IsErased)
            )
            {
                Assert.Equal(f.Document.Database.Clayer, reference.Layer);
                Assert.NotEqual("WALLS", reference.Layer);
            }
        }

        [Fact]
        public void TheBasePointIsTheNewBlocksOrigin()
        {
            Fixture f = Build();

            Run(Session(f.Document), "ATTREDEF", "TB", NewObjects(f), Enter, P(5, 0));

            Near(new GeoPoint3(5, 0, 0), f.Document.Database.BlockTable["TB"].Origin);
        }

        [Fact]
        public void TheWholeRedefinitionIsOneUndoStep()
        {
            Fixture f = Build();
            CadCommandSession session = Session(f.Document);

            Run(session, "ATTREDEF", "TB", NewObjects(f), Enter, P(0, 0));
            Assert.Equal("ATTREDEF", f.Document.NextUndo.Name);
            Assert.True(f.Document.Undo());

            Assert.Equal(
                new[] { "TAG1", "TAG2" },
                f.Document.Database.BlockTable["TB"]
                    .OfType<DxfAttributeDefinition>()
                    .Select(d => d.Tag)
                    .OrderBy(t => t, StringComparer.Ordinal)
            );
            Assert.All(f.OldReferences, h => Assert.False(f.Document.FindEntity(h).IsErased));
            Assert.Equal(
                3,
                All<DxfLine>(f.Document.ModelSpace).Count + All<DxfAttributeDefinition>(f.Document.ModelSpace).Count
            );
            Assert.Null(f.Document.NextUndo);
        }

        [Fact]
        public void ANameThatIsNoBlock_IsToldSo_AndAskedAgain()
        {
            Fixture f = Build();
            CadCommandSession session = Session(f.Document);

            Run(session, "ATTREDEF", "nope");

            Assert.Contains("Block \"NOPE\" is not defined. Please try again.", session.History);
            Assert.Equal("Enter name of the block you wish to redefine:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ABlockWithNoReference_IsToldSo()
        {
            Fixture f = Build();
            foreach (string handle in f.OldReferences)
            {
                f.Document.FindEntity(handle).Erase();
            }

            CadCommandSession session = Session(f.Document);
            Run(session, "ATTREDEF", "TB");

            Assert.Contains("No insertions of block TB found to redefine.", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NewObjectsWithNoAttribute_AreRefused_AndTheBlockStaysAsItWas()
        {
            Fixture f = Build();
            CadCommandSession session = Session(f.Document);

            Run(session, "ATTREDEF", "TB", new[] { f.NewLine }, Enter);

            Assert.Contains("New block has no attributes.", session.History);
            Assert.False(session.IsRunning);
            Assert.Equal(2, f.Document.Database.BlockTable["TB"].OfType<DxfAttributeDefinition>().Count());
            Assert.False(f.Document.FindEntity(f.NewLine).IsErased);
            Assert.Null(f.Document.NextUndo);
        }

        [Fact]
        public void AReferenceOnALockedLayer_IsLeftAsItIs()
        {
            Fixture f = Build();
            DxfBlockReference locked = (DxfBlockReference)f.Document.FindEntity(f.OldReferences[0]);
            Assert.NotNull(locked);
            Assert.True(f.Document.Database.LayerTable.Has("WALLS"));
            f.Document.Database.LayerTable["WALLS"].IsLocked = true;
            f.Document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "OPEN" });
            DxfBlockReference open = (DxfBlockReference)f.Document.FindEntity(f.OldReferences[1]);
            Assert.NotNull(open);
            open.Layer = "OPEN";

            Run(Session(f.Document), "ATTREDEF", "TB", NewObjects(f), Enter, P(0, 0));

            Assert.False(locked.IsErased);
            Assert.True(open.IsErased);
        }

        [Fact]
        public void TheCommandIsRegistered()
        {
            Assert.Equal("ATTREDEF", CadCommandRegistry.CreateDefault().Find("ATTREDEF").Name);
        }
    }
}
