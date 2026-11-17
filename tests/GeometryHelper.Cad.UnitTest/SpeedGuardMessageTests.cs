using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using GeometryHelper.UnitTest;
using Xunit;
using Xunit.Sdk;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What a failed timing guard says: the time of every run it made, also where a run threw for want of memory or disk, so
    /// that a failure on a loaded machine shows how the runs went and not only the last of them.
    /// </summary>
    public class SpeedGuardMessageTests
    {
        [Fact]
        public void RunsAllOverTheLimit_AreAllListedInTheMessage()
        {
            XunitException failure = Assert.ThrowsAny<XunitException>(() =>
                SpeedGuard.AssertWithin(0.001, () => Thread.Sleep(20), 3)
            );

            Assert.Matches(
                @"at the best of 3 runs, the limit being 0\.00 s \(\d\.\d\d s, \d\.\d\d s, \d\.\d\d s\)",
                failure.Message
            );
        }

        [Fact]
        public void ARunThatThrowsAfterOthers_SaysWhatTheRunsBeforeItTook_AndKeepsTheException()
        {
            int run = 0;

            XunitException failure = Assert.ThrowsAny<XunitException>(() =>
                SpeedGuard.AssertWithin(
                    0.001,
                    () =>
                    {
                        Thread.Sleep(20);

                        if (++run == 3)
                        {
                            throw new IOException("There is not enough space on the disk.");
                        }
                    },
                    5
                )
            );

            Assert.Matches(
                @"^Run 3 of 5 threw IOException: There is not enough space on the disk\. The 2 runs before it took \d\.\d\d s, \d\.\d\d s ",
                failure.Message
            );
            Assert.IsType<IOException>(failure.InnerException);
        }

        [Fact]
        public void AFirstRunThatThrows_ThrowsItsOwnException()
        {
            Assert.Throws<OutOfMemoryException>(() =>
                SpeedGuard.AssertWithin(1.0, () => throw new OutOfMemoryException(), 3)
            );
        }

        [Fact]
        public void AnAssertionInsideTheWork_IsNotWrapped()
        {
            int run = 0;

            XunitException failure = Assert.ThrowsAny<XunitException>(() =>
                SpeedGuard.AssertWithin(
                    0.001,
                    () =>
                    {
                        Thread.Sleep(20);
                        Assert.True(++run < 2, "the second run was wrong");
                    },
                    3
                )
            );

            Assert.Equal("the second run was wrong", failure.Message.Split('\n')[0].Trim());
        }

        [Fact]
        public void DescribeWritesEachTimeInSeconds()
        {
            Assert.Equal("0.12 s, 0.05 s, 1.50 s", SpeedGuard.Describe(new[] { 120.0, 50.0, 1500.0 }));
        }
    }
}
