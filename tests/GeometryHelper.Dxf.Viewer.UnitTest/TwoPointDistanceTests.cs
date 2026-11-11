using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A distance or an angle asked with no base point and given by two points, as AutoCAD 2021's console takes them: a point
    /// at "Enter new value for FILLETRAD &lt;10.0000&gt;:" asks "Specify second point:", and the distance between the two is
    /// the value; @dx,dy at the second is measured from the first, its refusal is the prompt's, a keyword is the prompt's, Enter
    /// its default, and the last point is left as it was. SETVAR's points take a number as a distance from the last point.
    /// </summary>
    public class TwoPointDistanceTests
    {
        private static CadCommandSession Start(CadDocument document, CadCommandRegistry registry = null)
        {
            var session = new CadCommandSession(document, registry)
            {
                PickAperture = 1.0,
                Tolerance = Tolerance.Default,
            };
            return session;
        }

        [Fact]
        public void APointAtADistanceWithNoBasePoint_AsksTheSecondPoint_AndTakesTheDistanceBetween()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "FILLETRAD", "0,0");
            Assert.Equal("Enter new value for FILLETRAD <10.0000>: 0,0", session.History.Last());
            Assert.Equal("Specify second point:", session.PromptText);
            Assert.Equal(new GeoPoint2(0, 0), session.Prompt.BasePoint);
            Run(session, "3,4");

            Assert.False(session.IsRunning);
            Assert.Equal("Specify second point: 3,4", session.History.Last());
            Assert.Equal(5.0, document.Database.Filletrad);
        }

        [Fact]
        public void TheSecondPointTyped_IsMeasuredFromTheFirst_AndTheLastPointIsLeftAsItWas()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Run(session, "ID", "100,100");
            Assert.Equal(new GeoPoint2(100, 100), session.LastPoint);

            Run(session, "FILLETRAD", "1,1", "@3,4");
            Assert.Equal(5.0, document.Database.Filletrad);
            Assert.Equal(new GeoPoint2(100, 100), session.LastPoint);

            // @ alone is the last point, and the second is measured from it.
            Run(session, "FILLETRAD", "@", "@6,8");
            Assert.Equal(10.0, document.Database.Filletrad);
            Assert.Equal(new GeoPoint2(100, 100), session.LastPoint);
        }

        [Fact]
        public void PointsPicked_GiveTheDistanceToo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "FILLETRAD", new GeoPoint2(10, 10));
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, new GeoPoint2(10, 22));

            Assert.False(session.IsRunning);
            Assert.Equal(12.0, document.Database.Filletrad);
        }

        [Fact]
        public void TextAtTheSecondPoint_IsRefusedInThePromptsWords_AndEnterKeepsTheValue()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "FILLETRAD", "0,0", "abc");
            Assert.Equal("Requires numeric distance or two points.", session.History.Last());
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, "20,0");
            Assert.Equal(20.0, document.Database.Filletrad);

            Run(session, "FILLETRAD", "0,0", Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(20.0, document.Database.Filletrad);
        }

        [Fact]
        public void TheSamePointTwice_IsNought()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "FILLETRAD", "5,5", "5,5");

            Assert.Equal(0.0, document.Database.Filletrad);
        }

        [Fact]
        public void FromAtEitherPoint_GivesThatPoint()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            // As AutoCAD 2021: 0,0 then FROM 10,0 @0,5 is 11.1803; FROM 10,0 @0,5 then 10,0 is 5.
            Run(session, "FILLETRAD", "0,0", "FROM", "10,0", "@0,5");
            Assert.Equal(Math.Sqrt(125.0), document.Database.Filletrad, 12);

            Run(session, "FILLETRAD", "FROM", "10,0", "@0,5");
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, "10,0");
            Assert.Equal(5.0, document.Database.Filletrad, 12);
        }

        [Fact]
        public void TheSecondPoint_TakesThePromptsKeywords_AndEnterItsDefault()
        {
            var answers = new List<string>();
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            registry.Add(new CadCommand("GHOFFSET", c => Distance(c, answers)));
            CadCommandSession session = Start(Blank(), registry);

            // As OFFSET's in AutoCAD 2021: T at the second point is Through, Enter there the default Through.
            Run(session, "GHOFFSET", "0,0", "abc");
            Assert.Equal("Requires numeric distance, two points, or option keyword.", session.History.Last());
            Run(session, "T");
            Run(session, "GHOFFSET", "0,0", Enter);
            Run(session, "GHOFFSET", "0,0", "0,2");

            Assert.Equal(new[] { "Keyword Through", "Keyword Through", "Number 2" }, answers);
        }

        private static IEnumerable<CadPrompt> Distance(CadCommandContext c, List<string> answers)
        {
            yield return CadPrompt.ForDistance(
                "Specify offset distance",
                null,
                new[] { "Through", "Erase", "Layer" },
                "Through"
            );
            answers.Add(c.Input.Kind + " " + c.Input);
        }

        [Fact]
        public void AnAngleWithNoBasePoint_TakesTwoPointsToo_AndRefusesTextInAutoCadsWords()
        {
            double kept = 0.0;
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            registry.AddVariable(CadSystemVariable.Angle("GHANGLE", () => kept, v => kept = v));
            CadCommandSession session = Start(Blank(), registry);

            Run(session, "GHANGLE", "abc");
            Assert.Equal("Requires valid numeric angle or two points.", session.History.Last());
            Run(session, "0,0");
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, "1,1");

            Assert.False(session.IsRunning);
            Assert.Equal(Math.PI / 4.0, kept, 12);

            Run(session, "GHANGLE", new GeoPoint2(0, 0), Enter);
            Assert.Equal(Math.PI / 4.0, kept, 12);
        }

        [Fact]
        public void APointVariable_TakesANumberAsADistanceFromTheLastPoint_TowardsTheCursor()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Run(session, "ID", "10,20");

            // As AutoCAD 2021's INSBASE 5 and GRIDUNIT 5: 5 from the last point, the way the cursor lies.
            session.Cursor = new GeoPoint2(13, 24);
            Run(session, "INSBASE", "5");
            Assert.False(session.IsRunning);
            Assert.Equal(13.0, document.Database.Insbase.X, 12);
            Assert.Equal(24.0, document.Database.Insbase.Y, 12);

            // Below nought it goes the other way.
            Run(session, "ID", "10,20");
            Run(session, "INSBASE", "-5");
            Assert.Equal(7.0, document.Database.Insbase.X, 12);
            Assert.Equal(16.0, document.Database.Insbase.Y, 12);
        }
    }
}
