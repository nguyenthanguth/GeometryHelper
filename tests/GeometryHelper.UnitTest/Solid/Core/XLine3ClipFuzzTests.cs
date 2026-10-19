using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// <see cref="GeoXLine3.ClipTo(GeoAabb3)"/> on random boxes and lines, held to the places the line pierces the six faces of the
    /// box, worked out by the oracle of the fuzz of the line (<see cref="XLineSpecimen"/>), which shares no code with the library.
    /// </summary>
    /// <remarks>
    /// A line through the box is clipped to its first and last piercing, running the way the line runs; one that misses it by
    /// more than twice the point tolerance gives nothing, and one that comes within half of it gives a segment, within the
    /// tolerance of the box. The run draws 20 000 boxes and lines, half of them through a point of a face, the rest anywhere and
    /// along the axes, and every second one starting a million along.
    /// </remarks>
    public class XLine3ClipFuzzTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static int Cases => int.TryParse(Environment.GetEnvironmentVariable("XLINE3_FUZZ"), out int n) ? Math.Max(n, 20000) : 20000;

        [Fact]
        public void ALineClippedToABox_IsTheStretchBetweenItsFirstAndLastPiercingOfTheFaces()
        {
            var random = new Random(77);
            var failures = new List<string>();
            int clipped = 0;
            int missed = 0;

            for (int i = 0; i < Cases; i++)
            {
                XLineSpecimen specimen = XLine3Gen.Make("Aabb3", random);
                var box = (GeoAabb3)specimen.Shape;
                var layout = i % 4 == 3 ? XLine3Gen.Layout.Axis : i % 4 == 2 ? XLine3Gen.Layout.Random : XLine3Gen.Layout.Through;
                GeoXLine3 line = XLine3Gen.MakeLine(random, specimen, layout);

                if (i % 2 == 1)
                {
                    line = new GeoXLine3(line.GetPointAtDistance(1E6), line.Direction);
                }

                try
                {
                    var oracleLine = new Ln(line);
                    double distance = specimen.Distance(oracleLine);
                    GeoLine3? cut = line.ClipTo(box, Strict);
                    Vec[] piercings = specimen.Meetings(oracleLine);

                    if (distance > 2 * Strict.EqualPoint)
                    {
                        missed++;
                        Assert.False(cut.HasValue, "a segment for a line " + distance + " from the box");
                    }
                    else if (distance <= 0.5 * Strict.EqualPoint)
                    {
                        clipped++;
                        Assert.True(cut.HasValue, "no segment for a line " + distance + " from the box");

                        // Its ends are on the box to within the tolerance, it runs the way the line runs, and its middle is in the box.
                        GeoVector3 along = cut.Value.StartPoint.GetVectorTo(cut.Value.EndPoint);

                        Assert.True(along.DotProduct(line.Direction) >= -1E-9, "runs against the line");
                        Assert.True(oracleLine.Perp(Vec.Of(cut.Value.StartPoint)).Length <= 1E-6 && oracleLine.Perp(Vec.Of(cut.Value.EndPoint)).Length <= 1E-6, "off the line");
                        Assert.True(box.DistanceTo(cut.Value.MidPoint) <= 1E-6, "the middle is outside the box");

                        // Piercing it, the ends are the first and the last of the places the line pierces the faces, or the same place.
                        if (piercings.Length >= 2 && !specimen.Delicate(oracleLine))
                        {
                            Assert.True(Vec.Of(cut.Value.StartPoint).DistanceTo(piercings[0]) <= 1E-5, "starts at " + cut.Value.StartPoint + ", not " + piercings[0].ToPoint());
                            Assert.True(Vec.Of(cut.Value.EndPoint).DistanceTo(piercings[piercings.Length - 1]) <= 1E-5, "ends at " + cut.Value.EndPoint + ", not " + piercings[piercings.Length - 1].ToPoint());
                        }
                    }
                }
                catch (Exception e)
                {
                    failures.Add("#" + i + " " + layout + ": " + e.Message + "\n   box " + box + "\n   line " + line);
                }
            }

            Assert.True(failures.Count == 0, failures.Count + " of " + Cases + " went wrong:\n" + string.Join("\n", failures.Take(8)));
            Assert.True(clipped > Cases / 4 && missed > Cases / 20, clipped + " clipped and " + missed + " missed: the run did not meet both");
        }
    }
}
