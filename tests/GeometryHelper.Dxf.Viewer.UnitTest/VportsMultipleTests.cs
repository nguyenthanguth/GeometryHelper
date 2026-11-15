using System.Collections.Generic;
using System.Linq;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// -VPORTS listing, deleting, restoring and saving the configurations against AutoCAD 2021's console run through standard
    /// input (15 November): <c>*Multiple</c>, which Toggle writes and writes over, is listed and deleted like any other, and the
    /// names that begin with an asterisk are AutoCAD's own.
    /// </summary>
    public class VportsMultipleTests
    {
        /// <summary>The lines -VPORTS ? with a pattern wrote, from "Current configuration:".</summary>
        private static List<string> Listed(CadCommandSession session, string pattern)
        {
            int before = session.History.Count;
            Run(session, "-VPORTS", "?", pattern);
            return session.History.Skip(before).SkipWhile(l => l != "Current configuration:").ToList();
        }

        [Fact]
        public void Toggle_KeepsTheTilesAsMultiple_AndListsThemInTheOrderOfTheNames_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.Equal(
                new[]
                {
                    "Current configuration:",
                    "id# 2",
                    "   corners: 0.0000,0.0000 1.0000,1.0000",
                    "No matching viewport configurations found.",
                },
                Listed(session, "*")
            );

            Run(session, "-VPORTS", "Save", "Z", "-VPORTS", "3", Enter, "-VPORTS", "Toggle", "-VPORTS", "Save", "A");

            // *Multiple stands first, as AutoCAD's own sort of the names has it, then A and Z.
            Assert.Equal(
                new[]
                {
                    "Current configuration:",
                    "id# 3",
                    "   corners: 0.0000,0.0000 1.0000,1.0000",
                    "Configuration \"*Multiple\":",
                    "    0.5000,0.0000 1.0000,1.0000",
                    "    0.0000,0.5000 0.5000,1.0000",
                    "    0.0000,0.0000 0.5000,0.5000",
                    "Configuration \"A\":",
                    "    0.0000,0.0000 1.0000,1.0000",
                    "Configuration \"Z\":",
                    "    0.0000,0.0000 1.0000,1.0000",
                },
                Listed(session, "*")
            );
            Assert.Equal(
                new[] { "Current configuration:", "id# 3", "   corners: 0.0000,0.0000 1.0000,1.0000" }.Concat(
                    new[]
                    {
                        "Configuration \"*Multiple\":",
                        "    0.5000,0.0000 1.0000,1.0000",
                        "    0.0000,0.5000 0.5000,1.0000",
                        "    0.0000,0.0000 0.5000,0.5000",
                    }
                ),
                Listed(session, "*M*")
            );
            Assert.Equal("No matching viewport configurations found.", Listed(session, "Q").Last());
        }

        [Fact]
        public void AToggleOfTwoTiles_WritesOverTheMultipleOfThreeTheOneBefore_AsAutoCad2021()
        {
            // Three tiles toggled to one and split into two, then toggled again: the two take the place of the three.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-VPORTS", "3", Enter, "-VPORTS", "Toggle", "-VPORTS", "2", Enter, "-VPORTS", "Toggle");

            Assert.Equal(
                new[]
                {
                    "Current configuration:",
                    "id# 2",
                    "   corners: 0.0000,0.0000 1.0000,1.0000",
                    "Configuration \"*Multiple\":",
                    "    0.5000,0.0000 1.0000,1.0000",
                    "    0.0000,0.0000 0.5000,1.0000",
                },
                Listed(session, "*")
            );

            Run(session, "-VPORTS", "Toggle");
            Assert.Equal(
                new[]
                {
                    "Current configuration:",
                    "id# 2",
                    "   corners: 0.5000,0.0000 1.0000,1.0000",
                    "id# 3",
                    "   corners: 0.0000,0.0000 0.5000,1.0000",
                    "Configuration \"*Multiple\":",
                    "    0.5000,0.0000 1.0000,1.0000",
                    "    0.0000,0.0000 0.5000,1.0000",
                },
                Listed(session, "*")
            );
        }

        [Fact]
        public void DeleteWithAnAsterisk_DeletesMultipleToo_AndRestoreOfItIsNothingAtAll()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-VPORTS", "Save", "Z", "-VPORTS", "3", Enter, "-VPORTS", "Toggle", "-VPORTS", "Save", "A");

            // AutoCAD 2021 ends Restore of *Multiple, or *Active, with nothing said and nothing changed.
            int before = session.History.Count;
            Run(session, "-VPORTS", "Restore", "*Multiple");
            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History.Skip(before), l =>
                l.StartsWith("Regenerating") || l.StartsWith("Cannot")
            );
            Assert.Equal("id# 3", Listed(session, "A")[1]);

            Run(session, "-VPORTS", "Delete", "*");
            Assert.Equal("Deleted 3 viewport configurations.", session.History.Last());
            Assert.Equal("No matching viewport configurations found.", Listed(session, "*").Last());

            Run(session, "-VPORTS", "Delete", "*Multiple");
            Assert.Equal("No matching viewport configurations found.", session.History.Last());
        }

        [Fact]
        public void ANameThatBeginsWithAnAsterisk_IsNotAValidName_ToSave()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "-VPORTS", "Save", "*Foo");
            Assert.Equal("Invalid viewport configuration name.", session.History.Last());
            Assert.Equal("Enter name for new viewport configuration or [?]:", session.PromptText);
            Run(session, "Foo");
            Assert.Equal("Configuration \"Foo\":", Listed(session, "*").Last(l => l.StartsWith("Config")));

            // An unknown name beginning with * is not found, where *Multiple and *Active end the command at once.
            Run(session, "-VPORTS", "Restore", "*Foo");
            Assert.Equal("Cannot find viewport configuration \"*Foo\".", session.History.Last());
        }
    }
}
