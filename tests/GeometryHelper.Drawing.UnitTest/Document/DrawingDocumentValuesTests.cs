using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    /// <summary>The values the application showing a drawing keeps with it in GH_DRAWING: the source, the preset, the whole assemblies.</summary>
    public class DrawingDocumentValuesTests
    {
        private static KeyValuePair<string, string> Value(string name, string value) => new KeyValuePair<string, string>(name, value);

        private static DxfDatabase SavedAndOpened(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                stream.Position = 0;
                return DxfDatabase.Open(stream);
            }
        }

        private static DxfDBDictionary Document(DxfDatabase database)
            => (DxfDBDictionary)database.NamedObjectsDictionary.GetAt(DrawingDocumentStore.DictionaryName);

        [Fact]
        public void ValuesSavedAndOpenedAgainComeBackInOrderAndTheDocumentIsTheSame()
        {
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, DrawingDocumentStoreTests.Beam());
            var values = new[] { Value("Source", "Test bodies"), Value("Preset", "Assembly"), Value("WholeAssembly", "B1"), Value("WholeAssembly", "C1"), Value("Note", null) };

            DrawingDocumentStore.WriteValues(database, values);
            DxfDatabase again = SavedAndOpened(database);

            Assert.Equal(values, DrawingDocumentStore.ReadValues(again));
            Assert.Equal(DrawingDocumentStoreTests.Beam(), DrawingDocumentStore.Read(again));

            // The drawing's own writes leave them.
            DrawingDocumentStore.Write(again, DrawingDocumentStoreTests.Beam());
            Assert.Equal(values, DrawingDocumentStore.ReadValues(again));
        }

        [Fact]
        public void ADocumentWrittenBeforeValuesWereKeptGivesNoneAndGivingItNoneChangesNoByte()
        {
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, DrawingDocumentStoreTests.Beam());
            string text = database.ToText();

            Assert.Empty(DrawingDocumentStore.ReadValues(database));

            DrawingDocumentStore.WriteValues(database, new KeyValuePair<string, string>[0]);

            Assert.Equal(text, database.ToText());
            Assert.Equal(new[] { "HEADER", "RECORDS" }, Names(Document(database)));
        }

        [Fact]
        public void ValuesWrittenAgainReplaceThoseThereAndNoneTakesThemOut()
        {
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, DrawingDocumentStoreTests.Beam());

            DrawingDocumentStore.WriteValues(database, new[] { Value("Source", "Test bodies"), Value("Preset", "Assembly") });
            DrawingDocumentStore.WriteValues(database, new[] { Value("Preset", "Changed") });

            Assert.Equal(new[] { Value("Preset", "Changed") }, DrawingDocumentStore.ReadValues(database));

            DrawingDocumentStore.WriteValues(database, new KeyValuePair<string, string>[0]);

            Assert.Empty(DrawingDocumentStore.ReadValues(database));
            Assert.Equal(new[] { "HEADER", "RECORDS" }, Names(Document(database)));
        }

        [Fact]
        public void ADrawingWithNoDocumentHasNoValuesAndTakesNone()
        {
            var database = new DxfDatabase("AC1027");

            Assert.Empty(DrawingDocumentStore.ReadValues(database));
            Assert.Throws<InvalidOperationException>(() => DrawingDocumentStore.WriteValues(database, new[] { Value("Source", "Test bodies") }));
            Assert.Throws<ArgumentException>(() => DrawingDocumentStore.WriteValues(database, new[] { Value(null, "Test bodies") }));
        }

        private static List<string> Names(DxfDBDictionary dictionary)
        {
            var names = new List<string>();

            foreach (DxfDBDictionaryEntry entry in dictionary)
            {
                names.Add(entry.Key);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
