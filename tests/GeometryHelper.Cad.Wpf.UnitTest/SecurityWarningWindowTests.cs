using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;
using static GeometryHelper.Cad.Wpf.UnitTest.SecurityKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The two warnings of L6 as windows shown over an owner that is off the screen (D914, D915, D922): what each says, what
    /// each button answers, and that the way out of each (Esc, Alt+F4, the cross) is the safe answer. The window is shown
    /// with <see cref="LoadSecurityWindow.Ask"/> and <see cref="CadQuestionWindow.ConfirmWritableTrustedFolder"/>; a test
    /// presses a button the way a click does, or closes the window the way the cross does.
    /// </summary>
    public sealed class SecurityWarningWindowTests : IDisposable
    {
        private readonly TestFolder _folder = new TestFolder();

        public void Dispose() => _folder.Dispose();

        [Fact]
        public void TheLoadWarning_SaysAutoCADsWords_AndNamesTheFileItsFolderAndItsDate()
        {
            string path = Code(_folder, "tool.lsp");

            AskOver(
                owner => LoadSecurityWindow.Ask(owner, path),
                window =>
                {
                    var texts = Texts(window);
                    Assert.Contains("Security - Unsigned Executable File", texts);
                    Assert.Contains(
                        "The publisher of this executable file could not be verified and the file is not located in a trusted folder. What do you want to do?",
                        texts
                    );
                    Assert.Contains("Name:", texts);
                    Assert.Contains("tool.lsp", texts);
                    Assert.Contains("Publisher:", texts);
                    Assert.Contains("Unknown Publisher", texts);
                    Assert.Contains("Location:", texts);
                    Assert.Contains(_folder.FullPath, texts);
                    Assert.Contains("Issuer:", texts);
                    Assert.Contains("None", texts);
                    Assert.Contains("Date:", texts);
                    Assert.Contains(
                        "Make sure that this executable file comes from a trusted source and is located in a trusted folder.",
                        texts
                    );
                    Assert.Contains("How do I decide what files to load?", texts);
                }
            );
        }

        [Fact]
        public void TheLoadWarningOfAPyFile_NamesThatFile()
        {
            string path = Code(_folder, "circles.py", "print('hi')\n");

            AskOver(
                owner => LoadSecurityWindow.Ask(owner, path),
                window => Assert.Contains("circles.py", Texts(window))
            );
        }

        [Theory]
        [InlineData("Always Load", CadLoadAnswer.AlwaysLoad)]
        [InlineData("Load Once", CadLoadAnswer.LoadOnce)]
        [InlineData("Do Not Load", CadLoadAnswer.DoNotLoad)]
        public void EachButtonOfTheLoadWarning_ReturnsItsAnswer(string label, CadLoadAnswer expected)
        {
            string path = Code(_folder, "tool.lsp");

            CadLoadAnswer answer = AskOver(
                owner => LoadSecurityWindow.Ask(owner, path),
                window => ButtonLabelled(window, label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))
            );

            Assert.Equal(expected, answer);
        }

        [Fact]
        public void DoNotLoad_IsTheDefaultButtonAndTheCancelButton_AndTheOtherTwoAreNeither()
        {
            // Enter and Esc both answer Do Not Load (D914); the other two buttons are answered by a click or Tab and Space only.
            string path = Code(_folder, "tool.lsp");

            AskOver(
                owner => LoadSecurityWindow.Ask(owner, path),
                window =>
                {
                    Button doNot = ButtonLabelled(window, "Do Not Load");
                    Assert.True(doNot.IsDefault);
                    Assert.True(doNot.IsCancel);

                    foreach (string label in new[] { "Always Load", "Load Once" })
                    {
                        Button other = ButtonLabelled(window, label);
                        Assert.False(other.IsDefault);
                        Assert.False(other.IsCancel);
                    }
                }
            );
        }

        [Fact]
        public void TheLoadWarningClosedWithNoButton_AsAltF4DoesIt_AnswersDoNotLoad()
        {
            string path = Code(_folder, "tool.lsp");

            CadLoadAnswer answer = AskOver(owner => LoadSecurityWindow.Ask(owner, path), window => window.Close());

            Assert.Equal(CadLoadAnswer.DoNotLoad, answer);
        }

        [Fact]
        public void TheCrossInTheTitleBand_AnswersDoNotLoad()
        {
            string path = Code(_folder, "tool.lsp");

            CadLoadAnswer answer = AskOver(
                owner => LoadSecurityWindow.Ask(owner, path),
                window =>
                {
                    object style = window.FindResource("GhTitleCloseButton");
                    Button cross = Descendants(window).OfType<Button>().Single(b => ReferenceEquals(b.Style, style));
                    cross.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            );

            Assert.Equal(CadLoadAnswer.DoNotLoad, answer);
        }

        [Fact]
        public void TheWritableFolderWarning_SaysAutoCADsWords_AndNamesTheFolder()
        {
            AskOver(
                owner => CadQuestionWindow.ConfirmWritableTrustedFolder(owner, _folder.FullPath),
                window =>
                {
                    var texts = Texts(window);
                    Assert.Contains("Trusted File Search Path - Security Concern", texts);
                    Assert.Contains(
                        "The location specified is not read-only.  What do you want to do?\n"
                            + "Specifying a writable folder can leave your computer vulnerable to malicious code.",
                        texts
                    );
                    Assert.Contains(_folder.FullPath, texts);
                    Assert.NotNull(ButtonLabelled(window, "Continue"));
                    Assert.NotNull(ButtonLabelled(window, "Cancel"));
                }
            );
        }

        [Fact]
        public void Continue_ConfirmsTheWritableFolder()
        {
            bool confirmed = AskOver(
                owner => CadQuestionWindow.ConfirmWritableTrustedFolder(owner, _folder.FullPath),
                window => ButtonLabelled(window, "Continue").RaiseEvent(new RoutedEventArgs(Button.ClickEvent))
            );

            Assert.True(confirmed);
        }

        [Fact]
        public void Cancel_RefusesTheWritableFolder_AndSoDoesEscAndTheClosedWindow()
        {
            string folder = _folder.FullPath;

            bool pressed = AskOver(
                owner => CadQuestionWindow.ConfirmWritableTrustedFolder(owner, folder),
                window =>
                {
                    Button cancel = ButtonLabelled(window, "Cancel");
                    Assert.True(cancel.IsCancel);
                    cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            );
            bool closed = AskOver(
                owner => CadQuestionWindow.ConfirmWritableTrustedFolder(owner, folder),
                window => window.Close()
            );

            Assert.False(pressed);
            Assert.False(closed);
        }
    }
}
