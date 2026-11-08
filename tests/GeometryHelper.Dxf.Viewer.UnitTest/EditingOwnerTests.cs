using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingOwnerTests
    {
        private const string OwnerName = "Test.Owner";

        /// <summary>
        /// A drawing of R2013 with four lines from (0, y) to (100, y): F at y = 0 free, M at 10 a mark (MoveOnly), E at 20 a
        /// dimension of the owner (Edit), N at 30 one the owner will not take (Edit, refused).
        /// </summary>
        private static CadDocument Owned(out FakeOwner owner, out Dictionary<string, string> handles)
        {
            var database = new DxfDatabase("AC1027");
            handles = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] ids = { null, "MOVE-1", "EDIT-1", "NOPE-1" };
            string[] names = { "F", "M", "E", "N" };

            for (int i = 0; i < ids.Length; i++)
            {
                var line = new DxfLine(new GeoPoint3(0, 10 * i, 0), new GeoPoint3(100, 10 * i, 0));
                database.ModelSpace.AppendEntity(line);

                if (ids[i] != null)
                {
                    new CadOwnerTag(OwnerName, ids[i]).WriteTo(line);
                }

                handles[names[i]] = line.Handle;
            }

            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            CadDocument document = CadDocument.From(database, "owned.dxf");
            owner = new FakeOwner();
            document.Owners.Register(owner);
            return document;
        }

        private static string Recorded(CadDocument document, string handle)
        {
            IReadOnlyList<DxfTypedValue> data = document.FindEntity(handle).GetXDataForApplication("GH_TESTREC");
            return data == null || data.Count < 2 ? null : data[1].Value;
        }

        [Fact]
        public void AMarkMovedByMove_GoesToItsOwnerAsAMoveOfItsDisplacement_AndMovesOnce()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "M", new[] { h["M"] }, Enter, P(0, 0), P(10, -2.5));

            CadEdit edit = Assert.Single(owner.Taken);
            Assert.Equal(CadEditKind.Move, edit.Kind);
            Assert.Equal("10,-2.5", edit.NewValue);
            Assert.Null(edit.OldValue);
            Near(new GeoPoint3(10, 7.5, 0), Entity<DxfLine>(document, h["M"]).StartPoint);
            Assert.Equal(edit.ToString(), Recorded(document, h["M"]));
            Assert.Equal("MOVE", document.NextUndo.Name);

            document.Undo();
            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TurnScaleAndMirrorOfAnEditEntity_GoToItsOwnerWithTheirBasePointsAndValues()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            CadCommandSession session = Session(document);

            Run(session, "RO", new[] { h["E"] }, Enter, P(0, 20), "90");
            Run(session, "SC", new[] { h["E"] }, Enter, P(0, 20), "2");
            Run(session, "MI", new[] { h["E"] }, Enter, P(0, 0), P(0, 50), "Y");

            Assert.Equal(new[] { CadEditKind.Rotate, CadEditKind.Scale, CadEditKind.Mirror }, owner.Taken.Select(e => e.Kind));
            Assert.Equal("0,20," + (Math.PI / 2.0).ToString("R", CultureInfo.InvariantCulture), owner.Taken[0].NewValue);
            Assert.Equal("0,20,2", owner.Taken[1].NewValue);
            Assert.Equal("0,0,0,50", owner.Taken[2].NewValue);
        }

        [Fact]
        public void AnEditEntityErased_GoesToItsOwner_AndUndoBringsItAndTheRecordBack()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "E", new[] { h["E"], h["F"] }, Enter);

            Assert.Equal(CadEditKind.Erase, Assert.Single(owner.Taken).Kind);
            Assert.Null(document.FindEntity(h["E"]));
            Assert.Null(document.FindEntity(h["F"]));
            document.Undo();
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void APropertySetOfAnEditEntity_GoesToItsOwnerAsTheRowsLabelAndTexts()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);

            Assert.True(CadPropertyEditor.Set(document, new[] { h["E"] }, "Layer", "Steel").Succeeded);

            CadEdit edit = Assert.Single(owner.Taken);
            Assert.Equal(CadEditKind.Property, edit.Kind);
            Assert.Equal("Layer", edit.Property);
            Assert.Equal("0", edit.OldValue);
            Assert.Equal("Steel", edit.NewValue);
            Assert.Equal("Steel", document.FindEntity(h["E"]).Layer);
        }

        [Fact]
        public void GripsGoToTheOwner_AReshapeAsAGripOfItsIndexAndPoints_AMiddleGripOfAMarkAsAMove()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);

            Assert.True(document.GripDrag(h["E"], 1, P(100, 40)).Succeeded);
            Assert.True(document.GripDrag(h["M"], 2, P(55, 15)).Succeeded);
            Assert.True(document.GripDrag(h["M"], 0, P(-5, 0)).Refused);

            Assert.Equal(2, owner.Taken.Count);
            Assert.Equal(new CadEdit(h["E"], CadEditKind.Grip, "1", "100,20", "100,40").ToString(), owner.Taken[0].ToString());
            Assert.Equal(new CadEdit(h["M"], CadEditKind.Move, null, null, "5,5").ToString(), owner.Taken[1].ToString());
            Near(new GeoPoint3(100, 40, 0), Entity<DxfLine>(document, h["E"]).EndPoint);
            Near(new GeoPoint3(5, 15, 0), Entity<DxfLine>(document, h["M"]).StartPoint);
        }

        [Fact]
        public void AFreeEntityNeverReachesTheOwner_AndCopiesOfAnOwnedOneAreFree()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            CadCommandSession session = Session(document);

            Run(session, "M", new[] { h["F"] }, Enter, P(0, 0), P(0, -5));
            Run(session, "CO", new[] { h["M"] }, Enter, P(0, 0), P(0, 100), Enter);

            Assert.Empty(owner.Taken);
            DxfLine copy = All<DxfLine>(document.ModelSpace).Last();
            Assert.Equal(110, copy.StartPoint.Y, 9);
            Assert.False(CadOwnerTag.TryRead(copy, out _));
            Assert.Equal(CadEditRule.Free, document.Owners.GetEditRule(copy));
        }

        [Fact]
        public void AnEditTheOwnerDoesNotTake_IsRefusedWhole_TheFreeEntitiesOfItUnmovedToo()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "M", new[] { h["F"], h["M"], h["N"] }, Enter, P(0, 0), P(10, 0));

            Assert.Equal("The model owner did not take this edit.", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
            Near(new GeoPoint3(0, 10, 0), Entity<DxfLine>(document, h["M"]).StartPoint);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AMovePlannedWithNoDisplacement_IsMeasuredForTheOwner()
        {
            CadDocument document = Owned(out FakeOwner owner, out Dictionary<string, string> h);
            string mark = h["M"];

            CadEditResult moved = document.ApplyEdit("MOVE", new[] { new CadEditRecord(CadEditRecordKind.Transformed, mark, "Move") }, s =>
                s.Document.FindEntity(mark).TransformBy(GeoTransform3.Translation(new GeoVector3(40, 0, 0))));

            Assert.True(moved.Succeeded);
            Assert.Equal("40,0", Assert.Single(owner.Taken).NewValue);
            Near(new GeoPoint3(40, 10, 0), Entity<DxfLine>(document, mark).StartPoint);
        }

        /// <summary>
        /// An owner as Part B's: MOVE- objects may only be moved, EDIT- objects take every edit, NOPE- objects are Edit but
        /// every edit is refused. An edit taken is applied by the owner itself in an edit of the document, the edit written into
        /// the entity's extended data (GH_TESTREC) as the owner's record of it.
        /// </summary>
        private sealed class FakeOwner : ICadEntityOwner
        {
            public List<CadEdit> Taken { get; } = new List<CadEdit>();

            public string Name => OwnerName;

            public CadEditRule GetEditRule(DxfEntity entity)
            {
                CadOwnerTag.TryRead(entity, out CadOwnerTag tag);
                return tag.ObjectId.StartsWith("MOVE", StringComparison.Ordinal) ? CadEditRule.MoveOnly : CadEditRule.Edit;
            }

            public bool TryApply(CadDocument document, CadEdit edit)
            {
                DxfEntity entity = document.FindEntity(edit.Handle);
                CadOwnerTag.TryRead(entity, out CadOwnerTag tag);

                if (tag.ObjectId.StartsWith("NOPE", StringComparison.Ordinal))
                {
                    return false;
                }

                // As Part B's owner plans it: a move as a move, an erase as an erase, anything else as a change of a property.
                var planned = edit.Kind == CadEditKind.Move ? new CadEditRecord(CadEditRecordKind.Transformed, edit.Handle, "Move", null, edit.NewValue)
                    : edit.Kind == CadEditKind.Erase ? new CadEditRecord(CadEditRecordKind.Erased, edit.Handle)
                    : new CadEditRecord(CadEditRecordKind.PropertyChanged, edit.Handle, "Owner " + edit.Kind);
                CadEditResult result = document.ApplyEdit("OWNER", new[] { planned }, s =>
                {
                    DxfEntity owned = s.Document.FindEntity(edit.Handle);
                    double[] v = edit.NewValue == null || edit.Kind == CadEditKind.Property ? new double[0] : edit.NewValue.Split(',').Select(t => double.Parse(t, CultureInfo.InvariantCulture)).ToArray();

                    switch (edit.Kind)
                    {
                        case CadEditKind.Move:
                            owned.TransformBy(GeoTransform3.Translation(new GeoVector3(v[0], v[1], 0)));
                            break;
                        case CadEditKind.Rotate:
                            owned.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(v[0], v[1], 0), new GeoVector3(0, 0, 1), v[2]));
                            break;
                        case CadEditKind.Scale:
                            owned.TransformBy(GeoTransform3.Scaling(new GeoPoint3(v[0], v[1], 0), v[2]));
                            break;
                        case CadEditKind.Mirror:
                            var normal = new GeoVector3(-(v[3] - v[1]), v[2] - v[0], 0).Normalize();
                            owned.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(v[0], v[1], 0), normal)));
                            break;
                        case CadEditKind.Property:
                            owned.Layer = edit.NewValue;
                            break;
                        case CadEditKind.Grip:
                            ((DxfLine)owned).EndPoint = new GeoPoint3(v[0], v[1], 0);
                            break;
                        case CadEditKind.Erase:
                            owned.Erase();
                            return;
                    }

                    owned.XData = new[] { new DxfTypedValue(1001, "GH_TESTREC"), new DxfTypedValue(1000, edit.ToString()) };
                });

                if (result.Succeeded)
                {
                    Taken.Add(edit);
                }

                return result.Succeeded;
            }
        }
    }
}
