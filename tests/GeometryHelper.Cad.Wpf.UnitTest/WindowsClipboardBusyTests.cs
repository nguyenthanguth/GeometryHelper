using System;
using System.Runtime.InteropServices;
using GeometryHelper.Cad.Wpf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The clipboard calls of the window when another app holds the clipboard: Windows answers CLIPBRD_E_CANT_OPEN
    /// (0x800401D0) as a COMException, which the Copy of the text window once let through as an error box.
    /// </summary>
    public class WindowsClipboardBusyTests
    {
        private const int ClipboardCantOpen = unchecked((int)0x800401D0);

        [Fact]
        public void AClipboardBusyTwice_IsTriedAgainAndTheCallGoesThrough()
        {
            int calls = 0;

            bool done = CadWindowsClipboard.Retry(() =>
            {
                calls++;

                if (calls < 3)
                {
                    throw new COMException("OpenClipboard Failed", ClipboardCantOpen);
                }
            });

            Assert.True(done);
            Assert.Equal(3, calls);
        }

        [Fact]
        public void AClipboardThatStaysBusy_IsGivenUpWithoutAnError()
        {
            int calls = 0;

            bool done = CadWindowsClipboard.Retry(() =>
            {
                calls++;
                throw new COMException("OpenClipboard Failed", ClipboardCantOpen);
            });

            Assert.False(done);
            Assert.Equal(5, calls);
        }

        [Fact]
        public void AnErrorThatIsNotTheClipboards_StillComesThrough()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CadWindowsClipboard.Retry(() => throw new InvalidOperationException("not the clipboard"))
            );
        }
    }
}
