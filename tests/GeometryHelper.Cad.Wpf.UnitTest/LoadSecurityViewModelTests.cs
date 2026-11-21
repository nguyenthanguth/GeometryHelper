using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The warning of SECURELOAD 1 as a view model (D914, <c>design/SecLoadWarning.png</c>): AutoCAD 2021's words, the rows
    /// read from the file (its name, its folder, when it was last written), and the three answers with Do Not Load the one
    /// nothing was pressed for. Assumed members of <see cref="LoadSecurityViewModel"/>: the constructor taking the file's
    /// path; <c>Title</c>, <c>Instruction</c>, <c>Advice</c>, <c>FootnoteLink</c>, <c>AlwaysLoadText</c>,
    /// <c>LoadOnceText</c>, <c>DoNotLoadText</c>, <c>Rows</c> (label and value pairs), <c>Answer</c>, and the commands
    /// <c>AlwaysLoadCommand</c>, <c>LoadOnceCommand</c> and <c>DoNotLoadCommand</c>.
    /// </summary>
    public sealed class LoadSecurityViewModelTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheTexts_AreAutoCADsWordForWord()
        {
            var model = new LoadSecurityViewModel(Code(_folder, "tool.lsp"));

            Assert.Equal("Security - Unsigned Executable File", model.Title);
            Assert.Equal(
                "The publisher of this executable file could not be verified and the file is not located in a trusted folder. What do you want to do?",
                model.Instruction
            );
            Assert.Equal(
                "Make sure that this executable file comes from a trusted source and is located in a trusted folder.",
                model.Advice
            );
            Assert.Equal("How do I decide what files to load?", model.FootnoteLink);
            Assert.Equal("Always Load", model.AlwaysLoadText);
            Assert.Equal("Load Once", model.LoadOnceText);
            Assert.Equal("Do Not Load", model.DoNotLoadText);
        }

        [Fact]
        public void TheRows_NameTheFile_ItsFolder_AndWhenItWasWritten_WithPublisherAndIssuerUnknown()
        {
            // The file is tool.lsp in the test's folder, written 7 March 2026 at 09:05; the unverified publisher and issuer are
            // AutoCAD's own texts for a file that carries no signature.
            string path = Code(_folder, "tool.lsp");
            IReadOnlyList<KeyValuePair<string, string>> rows = null;

            InCulture("en-US", () => rows = new LoadSecurityViewModel(path).Rows);

            Assert.Equal(
                new[]
                {
                    new KeyValuePair<string, string>("Name:", "tool.lsp"),
                    new KeyValuePair<string, string>("Publisher:", "Unknown Publisher"),
                    new KeyValuePair<string, string>("Location:", _folder.FullPath),
                    new KeyValuePair<string, string>("Issuer:", "None"),
                    new KeyValuePair<string, string>("Date:", "3/7/2026  09:05"),
                },
                rows
            );
        }

        [Theory]
        [InlineData("en-US", "3/7/2026  09:05")]
        [InlineData("de-DE", "07.03.2026  09:05")]
        public void TheDate_IsTheShortDateOfTheCurrentCulture_ThenTwoSpacesAndTheTimeOnA24HourClock(
            string culture,
            string expected
        )
        {
            string path = Code(_folder, "tool.lsp");
            string date = null;

            InCulture(culture, () =>
                date = Assert.Single(new LoadSecurityViewModel(path).Rows, r => r.Key == "Date:").Value
            );

            Assert.Equal(expected, date);
        }

        [Fact]
        public void APythonFile_HasTheSameTexts_AndItsOwnNameAndFolder()
        {
            string path = Code(_folder, "circles.py", "print('hi')\n");

            var model = new LoadSecurityViewModel(path);

            Assert.Equal("Security - Unsigned Executable File", model.Title);
            Assert.Contains(new KeyValuePair<string, string>("Name:", "circles.py"), model.Rows);
            Assert.Contains(new KeyValuePair<string, string>("Location:", _folder.FullPath), model.Rows);
        }

        [Fact]
        public void BeforeAnyButtonIsPressed_TheAnswerIsDoNotLoad_WhichIsWhatEscAndTheCrossLeave()
        {
            var model = new LoadSecurityViewModel(Code(_folder, "tool.lsp"));

            Assert.Equal(CadLoadAnswer.DoNotLoad, model.Answer);
        }

        [Theory]
        [InlineData(CadLoadAnswer.AlwaysLoad)]
        [InlineData(CadLoadAnswer.LoadOnce)]
        [InlineData(CadLoadAnswer.DoNotLoad)]
        public void EachButton_GivesItsAnswer(CadLoadAnswer answer)
        {
            var model = new LoadSecurityViewModel(Code(_folder, "tool.lsp"));
            ICommand button =
                answer == CadLoadAnswer.AlwaysLoad ? model.AlwaysLoadCommand
                : answer == CadLoadAnswer.LoadOnce ? model.LoadOnceCommand
                : model.DoNotLoadCommand;

            button.Execute(null);

            Assert.Equal(answer, model.Answer);
        }
    }
}
