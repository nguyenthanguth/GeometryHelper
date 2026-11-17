using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The rows of the selection box of the Properties palette: with several kinds of object selected, "All (7)" and one row
    /// for each kind with its count, whose objects alone give the properties shown and edited.
    /// </summary>
    public class SelectionKindTests
    {
        /// <summary>Five MTEXTs, two OLE frames and a line, the handles of each in the order they were added.</summary>
        private static CadDocument Mixed(out string[] texts, out string[] frames, out string line)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var textHandles = new string[5];

            for (int i = 0; i < textHandles.Length; i++)
            {
                var text = new DxfMText
                {
                    Location = new GeoPoint3(i * 100, 0, 0),
                    TextHeight = 2.5,
                    Contents = "T" + i,
                };
                database.ModelSpace.AppendEntity(text);
                textHandles[i] = text.Handle;
            }

            var frameHandles = new string[2];

            for (int i = 0; i < frameHandles.Length; i++)
            {
                var frame = new DxfOle2Frame(OleFrameDisplayTests.RedAndBlueBitmap());
                database.ModelSpace.AppendEntity(frame);
                frame.Position3d = new DxfRectangle3d(
                    new GeoPoint3(i * 100, 40, 0),
                    new GeoPoint3(i * 100 + 40, 40, 0),
                    new GeoPoint3(i * 100, 20, 0),
                    new GeoPoint3(i * 100 + 40, 20, 0)
                );
                frameHandles[i] = frame.Handle;
            }

            var segment = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            database.ModelSpace.AppendEntity(segment);
            texts = textHandles;
            frames = frameHandles;
            line = segment.Handle;
            return CadDocument.From(database, "kinds.dxf");
        }

        [Fact]
        public void SevenObjectsOfTwoKinds_GiveAllThenOneRowForEachKindWithItsCount()
        {
            CadDocument document = Mixed(out string[] texts, out string[] frames, out _);

            var kinds = CadProperties.Kinds(document, texts.Concat(frames).ToArray());

            Assert.Equal(new[] { "All (7)", "MText (5)", "OLE Object (2)" }, kinds.Select(k => k.Label));
            Assert.Equal(new string[] { null, "MText", "OLE Object" }, kinds.Select(k => k.Name));
            Assert.Equal(new[] { 7, 5, 2 }, kinds.Select(k => k.Count));
            Assert.Equal(texts.OrderBy(h => h, System.StringComparer.OrdinalIgnoreCase), kinds[1].Handles);
            Assert.Equal(frames.OrderBy(h => h, System.StringComparer.OrdinalIgnoreCase), kinds[2].Handles);
        }

        [Fact]
        public void TheKindRows_AreSortedByName_WhateverOrderTheHandlesAreGivenIn()
        {
            CadDocument document = Mixed(out string[] texts, out string[] frames, out string line);
            string[] everything = texts.Concat(frames).Concat(new[] { line }).ToArray();

            var forward = CadProperties.Kinds(document, everything);
            var backward = CadProperties.Kinds(document, everything.Reverse().ToArray());

            Assert.Equal(new[] { "All (8)", "Line (1)", "MText (5)", "OLE Object (2)" }, forward.Select(k => k.Label));
            Assert.Equal(forward.Select(k => k.Label), backward.Select(k => k.Label));
            Assert.Equal(forward.SelectMany(k => k.Handles), backward.SelectMany(k => k.Handles));
        }

        [Fact]
        public void OneKindOfObject_GivesOneRowThatStandsForTheWholeSelection()
        {
            CadDocument document = Mixed(out string[] texts, out _, out _);

            var kinds = CadProperties.Kinds(document, texts);

            CadSelectionKind only = Assert.Single(kinds);
            Assert.Equal("MText (5)", only.Label);
            Assert.Null(only.Name);
            Assert.Equal(5, only.Count);
        }

        [Fact]
        public void NothingSelected_GivesNoRows_AndAHandleOfNoObjectIsPassedOver()
        {
            CadDocument document = Mixed(out string[] texts, out _, out _);

            Assert.Empty(CadProperties.Kinds(document, null));
            Assert.Empty(CadProperties.Kinds(document, new string[0]));
            CadSelectionKind only = Assert.Single(CadProperties.Kinds(document, new[] { texts[0], "FFFFFF" }));
            Assert.Equal("MText (1)", only.Label);
        }

        [Fact]
        public void TheObjectsOfOneKind_AreDescribedAsIfOnlyTheyWereSelected()
        {
            CadDocument document = Mixed(out string[] texts, out string[] frames, out _);
            var kinds = CadProperties.Kinds(document, texts.Concat(frames).ToArray());

            CadSelectionInfo all = CadProperties.Describe(document, null, kinds[0].Handles);
            CadSelectionInfo chosen = CadProperties.Describe(document, null, kinds[1].Handles);

            Assert.Equal("All (7)", all.Label);
            Assert.Equal(new[] { "General" }, all.Groups.Select(g => g.Title));
            Assert.Equal(kinds[1].Label, chosen.Label);
            Assert.Contains(chosen.Groups, g => g.Title == "Text");
            Assert.Contains(chosen.Groups, g => g.Title == "Geometry");
        }
    }
}
