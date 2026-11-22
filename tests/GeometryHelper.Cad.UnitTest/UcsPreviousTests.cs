using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// UCS Previous as AutoCAD 2021's accoreconsole gives it (probed with 3-point, World, View, Z, Move, NAmed Save and
    /// Restore): every UCS option keeps the current system, one that changes nothing included, UCS Previous gives them back
    /// newest first, the tenth gives the last, and the next says <c>No previous coordinate system saved.</c>.
    /// </summary>
    public sealed class UcsPreviousTests
    {
        private const string Nothing = "No previous coordinate system saved.";

        // A system turned the angle of the line (10, 5) from the origin: 26.565 degrees, the 3-point system of the probes.
        private static readonly double Turned = Math.Atan2(5.0, 10.0);

        private static void Rotated(CadCommandSession session) => Run(session, "UCS", "3", "0,0", "10,5", "0,10");

        private static void Previous(CadCommandSession session) => Run(session, "UCS", "P");

        private static void IsTurned(double angle, CadDocument document)
        {
            Assert.Equal(angle, document.Ucs.Current.Angle, 6);
        }

        [Fact]
        public void ARotatedUcsThenWorldAgain_GivesWorldToALispCommandOfWorldAndPrevious()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Rotated(session);
            IsTurned(Turned, document);
            Run(session, "UCS", "W");
            Assert.True(document.Ucs.Current.IsWorld);

            // The maintainer's routine: (command "UCS" "W") ... (command "UCS" "P") with the UCS World already.
            Value(session, "(command \"UCS\" \"W\")");
            Value(session, "(command \"UCS\" \"P\")");

            Assert.True(document.Ucs.Current.IsWorld);
            Assert.Equal(0.0, session.Ucs.Angle, 9);
        }

        [Fact]
        public void WorldTwiceThenPreviousFourTimes_GivesWorldThenTheRotatedOneThenWorldThenNothing()
        {
            // AutoCAD 2021: UCSXDIR after UCS 3, W, W, then P four times: (1 0 0), (0.894 0.447 0), (1 0 0), (1 0 0) and the refusal.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Rotated(session);
            Run(session, "UCS", "W");
            Run(session, "UCS", "W");

            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
            Previous(session);
            IsTurned(Turned, document);
            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void PreviousWithNothingKept_SaysSoAndChangesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Previous(session);

            Assert.Equal(Nothing, session.History.Last());
            Assert.True(document.Ucs.Current.IsWorld);
        }

        [Fact]
        public void ZViewAndMove_KeepEachSystemTheyReplace_ForPreviousToGiveBackNewestFirst()
        {
            // AutoCAD 2021: 3-point, Z 30, View, Move 5,5; Previous gives the View system, the Z 30 one, the 3-point one, World.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Rotated(session);
            Run(session, "UCS", "Z", "30");
            Run(session, "UCS", "V");
            Run(session, "UCS", "M", "5,5");
            Near(new GeoPoint3(5, 5, 0), document.Ucs.Current.Origin);

            Previous(session);
            Near(new GeoPoint3(0, 0, 0), document.Ucs.Current.Origin);
            IsTurned(0.0, document);
            Previous(session);
            IsTurned(Turned + Math.PI / 6.0, document);
            Previous(session);
            IsTurned(Turned, document);
            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
        }

        [Fact]
        public void SavingTheCurrentSystem_KeepsItUnnamedForPrevious()
        {
            // AutoCAD 2021: after 3-point and NAmed Save T1, Previous gives the same system with no name, then World.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Rotated(session);
            Run(session, "UCS", "NA", "S", "T1");
            Assert.Equal("T1", document.Ucs.Current.Name);

            Previous(session);
            Assert.Null(document.Ucs.Current.Name);
            IsTurned(Turned, document);
            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
        }

        [Fact]
        public void RestoringTheNamedSystemThatIsCurrent_KeepsItForPrevious()
        {
            // AutoCAD 2021: 3-point, Save T2, Restore T2; Previous gives T2, then the unnamed one, then World.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Rotated(session);
            Run(session, "UCS", "NA", "S", "T2");
            Run(session, "UCS", "NA", "R", "T2");

            Previous(session);
            Assert.Equal("T2", document.Ucs.Current.Name);
            Previous(session);
            Assert.Null(document.Ucs.Current.Name);
            IsTurned(Turned, document);
            Previous(session);
            Assert.True(document.Ucs.Current.IsWorld);
            Previous(session);
            Assert.Equal(Nothing, session.History.Last());
        }

        [Fact]
        public void TwelveTurnsOfTenDegrees_LeaveTenToGoBackThrough()
        {
            // AutoCAD 2021: after Z 10 twelve times, Previous gives 110 degrees down to 20 and then the refusal.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            for (int i = 0; i < 12; i++)
            {
                Run(session, "UCS", "Z", "10");
            }

            for (int degrees = 110; degrees >= 20; degrees -= 10)
            {
                Previous(session);
                IsTurned(degrees * Math.PI / 180.0, document);
            }

            Previous(session);
            Assert.Equal(Nothing, session.History.Last());
            IsTurned(20.0 * Math.PI / 180.0, document);
        }
    }
}
