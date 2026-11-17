using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The parameters and actions of a dynamic block read back from its evaluation graph and changed in place, as the presenter's
    /// Block Editor does: the elements of a block of a linear, an XY and a polar parameter, a name, labels, descriptions, value
    /// sets, flags, an action's multiplier and offset and its place set in the records AutoCAD writes them in; a parameter erased
    /// with its grips and its actions; the visibility states added, renamed, moved, deleted and shown or hidden. AutoCAD 2021
    /// read the blocks the presenter made so (probe GhBa1, 26 files, AUDIT 0).
    /// </summary>
    public class DxfBlockAuthoringTests
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            DxfBlockTableRecord,
            DxfDatabase
        > Homes = new System.Runtime.CompilerServices.ConditionalWeakTable<DxfBlockTableRecord, DxfDatabase>();

        /// <summary>The drawing a block of these tests was made in.</summary>
        private static DxfDatabase Home(DxfBlockTableRecord block) =>
            Homes.TryGetValue(block, out DxfDatabase db) ? db : null;

        private static DxfBlockTableRecord Panel(out DxfLine[] lines)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            Homes.Add(block, db);
            lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            return block;
        }

        private static DxfBlockTableRecord WithWidthAndStretch(out DxfLine[] lines)
        {
            DxfBlockTableRecord block = Panel(out lines);
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return block;
        }

        [Fact]
        public void TheElementsOfABlock_AreItsParametersAndActionsInGraphOrder_WithTheirKindsLabelsPointsAndEntities()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out DxfLine[] lines);

            List<DxfBlockElement> elements = DxfBlockAuthoring.Elements(block);

            Assert.Equal(
                new[] { DxfBlockElementKind.Linear, DxfBlockElementKind.Stretch },
                elements.Select(e => e.Kind)
            );
            DxfBlockElement width = elements[0];
            Assert.True(width.IsParameter);
            Assert.Equal("Linear", width.Name);
            Assert.Equal("Width", Assert.Single(width.Labels));
            Assert.Equal("Overall width", Assert.Single(width.Descriptions));
            Assert.True(width.Base.Value.IsEqualTo(new GeoPoint3(0, 0, 0), Tolerance.Default));
            Assert.True(width.End.Value.IsEqualTo(new GeoPoint3(100, 0, 0), Tolerance.Default));
            Assert.True(width.At.Value.IsEqualTo(new GeoPoint3(50, -20, 0), Tolerance.Default));
            DxfBlockParameterValueSet set = Assert.Single(width.ValueSets);
            Assert.False(set.IsList);
            Assert.Equal(50.0, set.Minimum);
            Assert.Equal(300.0, set.Maximum);
            Assert.Equal(10.0, set.Increment);
            Assert.IsType<DxfBlockLinearParameter>(width.Parameter);

            DxfBlockElement stretch = elements[1];
            Assert.False(stretch.IsParameter);
            Assert.Equal("Stretch", stretch.Name);
            Assert.Equal(width.Node, stretch.ParameterNode);
            Assert.Equal(lines.Take(3).Select(l => l.Handle), stretch.Entities);
            Assert.Equal(1.0, stretch.Multiplier);
            Assert.Equal(new GeoPoint2(90, -10), stretch.Frame[0]);
            Assert.Equal(new GeoPoint2(110, 60), stretch.Frame[1]);
        }

        [Fact]
        public void ABlockWithNoGraph_HasNoElements()
        {
            DxfBlockTableRecord block = Panel(out _);

            Assert.Empty(DxfBlockAuthoring.Elements(block));
        }

        [Fact]
        public void ANameLabelAndDescriptionSet_AreWrittenInPlace_AndTheDynamicPropertyFollows()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out _);
            DxfBlockElement width = DxfBlockAuthoring.Elements(block)[0];

            DxfBlockAuthoring.Rename(width, "Span");
            DxfBlockAuthoring.SetLabel(width, 0, "Length");
            DxfBlockAuthoring.SetDescription(width, 0, "Edited");
            DxfBlockAuthoring.SetShowProperties(width, false);
            DxfBlockAuthoring.SetChainActions(width, true);

            DxfBlockElement again = DxfBlockAuthoring.Elements(block)[0];
            Assert.Equal("Span", again.Name);
            Assert.Equal("Length", again.Labels[0]);
            Assert.Equal("Edited", again.Descriptions[0]);
            Assert.False(again.ShowProperties);
            Assert.True(again.ChainActions);

            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            Home(block).ModelSpace.AppendEntity(insert);
            DxfDynamicBlockReferenceProperty property = insert.DynamicBlockReferencePropertyCollection.First(p =>
                p.PropertyName == "Length"
            );
            Assert.Equal("Edited", property.Description);
            Assert.False(property.Show);
        }

        [Fact]
        public void ANameOrLabelAnotherElementHas_IsRefused_AndNothingIsWritten()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out _);
            block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), -20, null);
            List<DxfBlockElement> elements = DxfBlockAuthoring.Elements(block);
            DxfBlockElement width = elements[0];
            DxfBlockElement height = elements.First(e => e.Labels.Contains("Height"));

            Assert.Throws<ArgumentException>(() => DxfBlockAuthoring.SetLabel(height, 0, "width"));
            Assert.Throws<ArgumentException>(() => DxfBlockAuthoring.Rename(height, "linear"));
            Assert.Throws<ArgumentException>(() => DxfBlockAuthoring.SetLabel(width, 0, " "));
            Assert.Throws<ArgumentException>(() => DxfBlockAuthoring.SetDescription(width, 0, "a\nb"));
            Assert.Equal("Height", DxfBlockAuthoring.Elements(block).First(e => e.Handle == height.Handle).Labels[0]);
        }

        [Fact]
        public void AValueSetChangedInPlace_IsWhatAnInsertSnapsTo_AsAListAndAsIncrementsAndNone()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out _);
            DxfBlockElement width = DxfBlockAuthoring.Elements(block)[0];

            DxfBlockAuthoring.SetValueSet(width, 0, DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 }));
            DxfBlockParameterValueSet list = DxfBlockAuthoring.Elements(block)[0].ValueSets[0];
            Assert.True(list.IsList);
            Assert.Equal(new[] { 100.0, 150, 200 }, list.Values);

            DxfBlockAuthoring.SetValueSet(width, 0, DxfBlockParameterValueSet.Increments(40, 400, 20));
            DxfBlockParameterValueSet steps = DxfBlockAuthoring.Elements(block)[0].ValueSets[0];
            Assert.False(steps.IsList);
            Assert.Equal(20.0, steps.Increment);

            DxfBlockAuthoring.SetValueSet(width, 0, null);
            Assert.Null(DxfBlockAuthoring.Elements(block)[0].ValueSets[0]);

            DxfBlockAuthoring.SetValueSet(width, 0, DxfBlockParameterValueSet.Increments(50, 300, 10));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            Home(block).ModelSpace.AppendEntity(insert);
            DxfDynamicBlockReferenceProperty property = insert.DynamicBlockReferencePropertyCollection.First(p =>
                p.PropertyName == "Width"
            );
            property.Value = 77.0;
            Assert.Equal(80.0, (double)property.Value, 9);
        }

        [Fact]
        public void AnActionsMultiplierOffsetAndLocation_AreWrittenInPlace_AndTheStretchFollowsTheMultiplier()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out _);
            DxfBlockElement stretch = DxfBlockAuthoring.Elements(block)[1];

            DxfBlockAuthoring.SetMultiplier(stretch, 0.5);
            DxfBlockAuthoring.SetAngleOffset(stretch, 0.0);
            DxfBlockAuthoring.SetLocation(stretch, new GeoPoint3(70, -30, 0));

            DxfBlockElement again = DxfBlockAuthoring.Elements(block)[1];
            Assert.Equal(0.5, again.Multiplier);
            Assert.True(again.At.Value.IsEqualTo(new GeoPoint3(70, -30, 0), Tolerance.Default));
            Assert.Throws<NotSupportedException>(() => DxfBlockAuthoring.SetSpacing(again, false, 10.0));

            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            Home(block).ModelSpace.AppendEntity(insert);
            insert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;
            Assert.Contains(insert.BlockTableRecord.OfType<DxfLine>(), l =>
                l.StartPoint.IsEqualTo(new GeoPoint3(150, 0, 0), Tolerance.Default)
            );
        }

        [Fact]
        public void AParametersLocation_IsItsLabelsPlace_AndAnAlignmentHasNone()
        {
            DxfBlockTableRecord block = Panel(out _);
            block.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0));
            DxfBlockElement alignment = DxfBlockAuthoring.Elements(block).Single();

            DxfBlockAuthoring.SetLocation(alignment, new GeoPoint3(10, 10, 0));

            Assert.Null(DxfBlockAuthoring.Elements(block).Single().At);
        }

        [Fact]
        public void AParameterErased_GoesWithItsGripsComponentsAndActions_AndAGraphLeftEmptyIsNoGraph()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out DxfLine[] lines);
            block.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), -20, null);
            DxfFile file = block.File;
            int before = file.ObjectCount();

            List<DxfBlockElement> elements = DxfBlockAuthoring.Elements(block);
            DxfDynamicBlockWriter.Erase(block, elements[0].Node);

            List<DxfBlockElement> left = DxfBlockAuthoring.Elements(block);
            Assert.Equal(new[] { "Height" }, left.Select(e => e.Labels.FirstOrDefault()));
            Assert.True(block.IsDynamicBlock);
            Assert.Equal(8, before - file.ObjectCount());

            DxfDynamicBlockWriter.Erase(block, left[0].Node);

            Assert.Empty(DxfBlockAuthoring.Elements(block));
            Assert.False(block.IsDynamicBlock);
            Assert.Equal(4, lines.Count(l => !l.IsErased));
        }

        [Fact]
        public void AnActionErased_LeavesItsParameter()
        {
            DxfBlockTableRecord block = WithWidthAndStretch(out _);

            DxfDynamicBlockWriter.Erase(block, DxfBlockAuthoring.Elements(block)[1].Node);

            Assert.Equal(DxfBlockElementKind.Linear, Assert.Single(DxfBlockAuthoring.Elements(block)).Kind);
        }

        private static DxfBlockTableRecord Sign(out DxfLine[] lines)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "SIGN", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            Homes.Add(block, db);
            lines = new[]
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0)),
                new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            block.AddVisibilityParameter(
                "Mark",
                null,
                new GeoPoint3(20, -10, 0),
                new[] { new DxfBlockVisibilityState("All", lines) }
            );
            return block;
        }

        [Fact]
        public void VisibilityStatesAdded_ShowNoneAllOrACopy_AndAreRenamedMovedAndDeleted()
        {
            DxfBlockTableRecord block = Sign(out DxfLine[] lines);

            DxfBlockVisibility.Add(block, "None", DxfBlockVisibility.NewState.HideAll, null);
            DxfBlockVisibility.Add(block, "Everything", DxfBlockVisibility.NewState.ShowAll, null);
            DxfBlockVisibility.SetVisible(block, new[] { lines[1] }, "All", false);
            DxfBlockVisibility.Add(block, "Copy", DxfBlockVisibility.NewState.Copy, "All");

            IReadOnlyList<DxfBlockVisibilityState> states = block.GetVisibilityStates();
            Assert.Equal(new[] { "All", "None", "Everything", "Copy" }, states.Select(s => s.Name));
            Assert.Equal(2, states[0].Entities.Count);
            Assert.Empty(states[1].Entities);
            Assert.Equal(3, states[2].Entities.Count);
            Assert.Equal(states[0].Entities.Select(e => e.Handle), states[3].Entities.Select(e => e.Handle));

            DxfBlockVisibility.Rename(block, "Copy", "Duplicate");
            DxfBlockVisibility.Move(block, "Duplicate", -3);
            Assert.Equal(
                new[] { "Duplicate", "All", "None", "Everything" },
                block.GetVisibilityStates().Select(s => s.Name)
            );

            DxfBlockVisibility.Delete(block, "None");
            Assert.Equal(new[] { "Duplicate", "All", "Everything" }, block.GetVisibilityStates().Select(s => s.Name));
            Assert.Throws<InvalidOperationException>(() =>
                DxfBlockVisibility.Add(block, "all", DxfBlockVisibility.NewState.ShowAll, null)
            );
            Assert.Throws<InvalidOperationException>(() => DxfBlockVisibility.Rename(block, "All", "everything"));
            Assert.Throws<InvalidOperationException>(() => DxfBlockVisibility.Delete(block, "Missing"));
        }

        [Fact]
        public void AnObjectHiddenInOneStateAlone_IsGovernedAndShownInTheOthers_AndHiddenInAllIsKeptInvisible()
        {
            DxfBlockTableRecord block = Sign(out DxfLine[] lines);
            DxfBlockVisibility.Add(block, "Second", DxfBlockVisibility.NewState.ShowAll, null);
            var added = new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(5, 5, 0));
            block.AppendEntity(added);
            Assert.DoesNotContain(added.Handle, DxfBlockVisibility.Governed(block));

            DxfBlockVisibility.SetVisible(block, new[] { added }, "All", false);

            IReadOnlyList<DxfBlockVisibilityState> states = block.GetVisibilityStates();
            Assert.DoesNotContain(added.Handle, states[0].Entities.Select(e => e.Handle));
            Assert.Contains(added.Handle, states[1].Entities.Select(e => e.Handle));
            Assert.Contains(added.Handle, DxfBlockVisibility.Governed(block));

            DxfBlockVisibility.SetVisible(block, new[] { lines[2] }, null, false);

            Assert.DoesNotContain(
                lines[2].Handle,
                block.GetVisibilityStates().SelectMany(s => s.Entities).Select(e => e.Handle)
            );
            Assert.Contains(lines[2].Handle, DxfBlockVisibility.Governed(block));
            Assert.False(lines[2].Visible);
            Assert.True(lines[0].Visible);

            DxfBlockVisibility.SetVisible(block, new[] { lines[2] }, "Second", true);
            Assert.Contains(lines[2].Handle, block.GetVisibilityStates()[1].Entities.Select(e => e.Handle));
            Assert.False(lines[2].Visible);
        }

        [Fact]
        public void TheVisibilityParameterErased_ShowsItsObjectsAgain_AndTakesItsNodeOutOfNoState()
        {
            DxfBlockTableRecord block = Sign(out DxfLine[] lines);
            DxfBlockVisibility.Add(block, "None", DxfBlockVisibility.NewState.HideAll, null);
            DxfBlockVisibility.Delete(block, "All");
            Assert.All(lines, l => Assert.False(l.Visible));

            DxfDynamicBlockWriter.Erase(
                block,
                DxfBlockAuthoring.Elements(block).First(e => e.Kind == DxfBlockElementKind.Visibility).Node
            );

            Assert.All(lines, l => Assert.True(l.Visible));
            Assert.Empty(block.GetVisibilityStates());
        }
    }
}
