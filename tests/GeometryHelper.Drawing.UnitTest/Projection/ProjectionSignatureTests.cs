using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    /// <summary>
    /// The views of the test bodies, the frame of 1 000 members and 200 seeded random cases, each signed to the bit: the
    /// signatures were taken from the projection as it was before it was made faster, and a change that keeps them keeps
    /// every line of every view.
    /// </summary>
    public class ProjectionSignatureTests
    {
        // Taken on 2026-11-08 under .NET Framework 4.8, x64, from the projection as it stood before it was made faster
        // (the code agent projection left), before any line of it was changed: each a view's Signature, its SHA-256 cut
        // to 24 digits. The test writes what it takes in the folder proj\signatures of the run (TestRun).
        private const string Bodies =
            ""
            + "box top 5d624ae514d29c8c0ab7cd45\n"
            + "box bottom 752cdedb17584ca1962bb26e\n"
            + "box front 691f8a5e030c04f2c15e5434\n"
            + "box back ed62fa42b8969e7af1824c13\n"
            + "box left 4bdd9781a8e4f291a6ece126\n"
            + "box right db119175697d10fa1a210084\n"
            + "box iso 729dfb14c09d8dfe3aa476ef\n"
            + "plate top 07631fe8b4cc59cdb7005611\n"
            + "plate bottom 2abf7ddc74e7ea05af77cec1\n"
            + "plate front a3045c79b8c52cdf13ac96f6\n"
            + "plate back b0a645e4db189cde9be0626a\n"
            + "plate left 6a4ced24559e29d2f60f0db0\n"
            + "plate right ab4943b3bd75f1bf78f09e5f\n"
            + "plate iso 04a5ebbc0d13c946edd03ec4\n"
            + "angle top 2e8d6c5d034cad765e9b3682\n"
            + "angle bottom a7bd026507fc71da46a12e74\n"
            + "angle front b8cc52fcabec49e897a24e45\n"
            + "angle back ea361ce6276c6a8af2df0ac5\n"
            + "angle left f41331bdca9bc604acd815b1\n"
            + "angle right 8fd967836057a1db36938f12\n"
            + "angle iso 1f3ea654600ab9a598111b0e\n"
            + "tube top edd1d5220000ef2e02a25261\n"
            + "tube bottom edd1d5220000ef2e02a25261\n"
            + "tube front edd1d5220000ef2e02a25261\n"
            + "tube back 8a53ea85e4064fdc622ef262\n"
            + "tube left 42fd7fae46bf662b0078ea2e\n"
            + "tube right 352f5e5ba5be362ea517ba16\n"
            + "tube iso 9a9cf3599e777b9219fa7ebc\n"
            + "beam top 7223afa63f979c778db3059b\n"
            + "beam bottom 7223afa63f979c778db3059b\n"
            + "beam front 86e5787d3621ffc8932f7e3f\n"
            + "beam back 42b202728de630382048b66c\n"
            + "beam left e4b31f30b9e8a7bd4c3a60a7\n"
            + "beam right aebfc4cbf5ccfc3f002b0ce6\n"
            + "beam iso 14c393f2e730187c904a0b61\n"
            + "tc1 top bf2d416d4943ac814c02b6a1\n"
            + "tc1 bottom 21216b351d05b41c351e0a50\n"
            + "tc1 front 92e3025b9aced01b918c5e54\n"
            + "tc1 back 7fe6122b48334721b834dfa8\n"
            + "tc1 left 8e3dc960cdda9805699f6a3a\n"
            + "tc1 right 32460d060b677a57bddfe00b\n"
            + "tc1 iso 361bf493d04fd73f09d33da6\n"
            + "tc2 top b5a3bc6c27161d1c531a5c44\n"
            + "tc2 bottom b5a3bc6c27161d1c531a5c44\n"
            + "tc2 front c33331a347b6fef3e8c4aa8a\n"
            + "tc2 back 1918c60095db1f8307522d8e\n"
            + "tc2 left 45fbf5bf15fc4aeff397bd72\n"
            + "tc2 right 55be0b54cf70afeb303d7c8b\n"
            + "tc2 iso 02323393c2147b3949862f6f\n"
            + "tc3 top 7743965d8dd908eb5bbe4edc\n"
            + "tc3 bottom 7743965d8dd908eb5bbe4edc\n"
            + "tc3 front 7743965d8dd908eb5bbe4edc\n"
            + "tc3 back a358e495e9a0d843bff4f711\n"
            + "tc3 left f7a6805f81e0239aa0c31943\n"
            + "tc3 right 742e20001881943a40eb8933\n"
            + "tc3 iso a69c27ec3745f5b7732bb051\n"
            + "tc4 top 58b5e348142a217695ae3392\n"
            + "tc4 bottom 86320e1c1a988c69bca33c0e\n"
            + "tc4 front 3ec2d0d8272a9aa60237c103\n"
            + "tc4 back 2f06625ee72990aa9c6b9311\n"
            + "tc4 left 1aea8965d88efbfeca47119b\n"
            + "tc4 right b11a7ca5b215eab8e505688f\n"
            + "tc4 iso 48c46ffc54f0c5f19e3de9af\n"
            + "tc5 top 11b7e5b921e20326d606bfb7\n"
            + "tc5 bottom 3ba80bab8d34abed5fe7a98f\n"
            + "tc5 front 22d833bc117199f4946137f2\n"
            + "tc5 back a84825d2c22f97c18d7d9bd5\n"
            + "tc5 left 6f28b39f3029617663d5a05e\n"
            + "tc5 right 211ce7385604b9100cbc9ad0\n"
            + "tc5 iso 77adea0e65359da0f459c894\n"
            + "tc6 top 115bef1ec467a8014d2421d5\n"
            + "tc6 bottom befe063d804af074d9aad6dc\n"
            + "tc6 front 548ec32ebe88321d2587b7e2\n"
            + "tc6 back 03e69ac34e7e2ff45d396c75\n"
            + "tc6 left b714a05c08d6e3efe6746789\n"
            + "tc6 right 4f6c9033a9ec0a668125b3af\n"
            + "tc6 iso ecf5b51b899b46fcfffd7942\n"
            + "tc7 top 4f0f73d2524c52e03a825458\n"
            + "tc7 bottom a7724df2993227de5611053e\n"
            + "tc7 front 8d9f37ad04e9fc9bedd20688\n"
            + "tc7 back 5d53e8ddcde2814871f7cec8\n"
            + "tc7 left 988309d1449cc30c710ae705\n"
            + "tc7 right da98123d67895be5ed5ebcd3\n"
            + "tc7 iso c478a0b3d81f1d9eddf50458\n"
            + "tc8 top 7170294f8b48750b8fc58abb\n"
            + "tc8 bottom 9fa0aa78c8f58db08d3f24f2\n"
            + "tc8 front 777812662029c40f98563f04\n"
            + "tc8 back 8b818c7050ae9412e00855bb\n"
            + "tc8 left 8047e4d9177a7a8f015234a9\n"
            + "tc8 right 7b8c5f8b265fd6173af59cbc\n"
            + "tc8 iso cfd2a733fa3bed945e2e9135\n"
            + "tc9 top 4f9010dacd77de061f2ed5a6\n"
            + "tc9 bottom b33555f9e51ea6ef1b5eaa45\n"
            + "tc9 front 583edba07f326a7dc504bb1f\n"
            + "tc9 back 83e1081a12c86b25e5dd8ccf\n"
            + "tc9 left b3b270ad704dd9ecdea38f92\n"
            + "tc9 right 192c5bdfe63723de6bd40b0b\n"
            + "tc9 iso bce82a7084f50b61811ac04b\n"
            + "tc10 top ff63cf3e76e1d81338711484\n"
            + "tc10 bottom 0377d65a9f83e64159157298\n"
            + "tc10 front 6413d41c13fe1ca119799a3f\n"
            + "tc10 back 246ca56dcaa928a963262f29\n"
            + "tc10 left 5f9cf8f6313b1cd52761145f\n"
            + "tc10 right 978f17b9f1f00414d22a8346\n"
            + "tc10 iso 1716d10ee0ada61600bfd211\n"
            + "beam section 2192c91c5e6cfd5982d94aef\n"
            + "tc1 section 2ae209af96797e11fc6c13c0\n"
            + "tc2 section dea3e631b4bf0a0dc57efa71\n"
            + "tc5 section 45d905ce3431ae3579f14136\n"
            + "tc7 section 0e95682bcdff90ad3c99e6d7\n";

        private const string Frame =
            ""
            + "frame top 9ea316ed18125782f4d6822c\n"
            + "frame bottom 26862078fd0be8286c2c2b85\n"
            + "frame front eaa753789f3eb8d31a8fc734\n"
            + "frame back b8bc54999cbb3e0ada80080e\n"
            + "frame left 59c2e637333f455ac8afb6ae\n"
            + "frame right 4ec1e38a1a7659ca15375919\n"
            + "frame iso 82a139228dec73c8c258a346\n";

        private const string Random200 = "182446040d84c44012f41f01";

        /// <summary>The six views along the world's axes and the view from a corner.</summary>
        private static readonly (string Name, GeoCoordinateSystem3 Frame)[] Views =
        {
            ("top", Top),
            ("bottom", new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis.Negate())),
            ("front", Front),
            ("back", new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis.Negate(), GeoVector3.ZAxis)),
            ("left", new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis.Negate(), GeoVector3.ZAxis)),
            ("right", Right),
            ("iso", Iso),
        };

        [Fact]
        public void TheTestBodiesInTheSixViewsAndFromACornerAreDrawnAsTheyWereBeforeTheSpeedUp()
        {
            var lines = new StringBuilder();

            foreach ((string name, ProjectionInput[] inputs) in TestInputs())
            {
                foreach ((string view, GeoCoordinateSystem3 frame) in Views)
                {
                    lines
                        .Append(name)
                        .Append(' ')
                        .Append(view)
                        .Append(' ')
                        .Append(Hash(ViewProjector.Project(inputs, frame, Tol)))
                        .Append('\n');
                }
            }

            foreach ((string name, ProjectionInput[] inputs, GeoCoordinateSystem3 frame, GeoPlane3 plane) in Sections())
            {
                ProjectedView2 view = ViewProjector.Project(
                    inputs,
                    frame,
                    new ProjectionOptions(sectionPlane: plane),
                    Tol
                );
                lines.Append(name).Append(" section ").Append(Hash(view)).Append('\n');
            }

            Assert.Equal(Bodies, Kept("bodies", lines.ToString()));
        }

        [Fact]
        public void TheFrameOfAThousandMembersIsDrawnAsItWasBeforeTheSpeedUp()
        {
            ProjectionInput[] frame = ProjectionSpeedTests.Frame();
            var lines = new StringBuilder();

            foreach ((string view, GeoCoordinateSystem3 at) in Views)
            {
                lines
                    .Append("frame ")
                    .Append(view)
                    .Append(' ')
                    .Append(Hash(ViewProjector.Project(frame, at, Tol)))
                    .Append('\n');
            }

            Assert.Equal(Frame, Kept("frame", lines.ToString()));
        }

        [Fact]
        public void TwoHundredSeededRandomCasesAreDrawnAsTheyWereBeforeTheSpeedUp()
        {
            var lines = new StringBuilder();

            for (int seed = 0; seed < 200; seed++)
            {
                var random = new Random(seed);
                ProjectionInput[] inputs = ProjectionFuzzTests.Bodies(random);
                GeoCoordinateSystem3 frame = ProjectionFuzzTests.Frame(random);
                ProjectionOptions options =
                    random.Next(5) == 0
                        ? new ProjectionOptions(
                            sectionPlane: new GeoPlane3(new GeoPoint3(random.Next(0, 200), 0, 0), frame.ZAxis)
                        )
                        : ProjectionOptions.Default;

                lines.Append(Signature(ViewProjector.Project(inputs, frame, options, Tol)));
            }

            Assert.Equal(Random200, Kept("random200", Hash(lines.ToString())));
        }

        private static IEnumerable<(string Name, ProjectionInput[] Inputs)> TestInputs()
        {
            yield return ("box", new[] { new ProjectionInput("box", Box(0, 0, 0, 300, 200, 100)) });
            yield return (
                "plate",
                new[] { new ProjectionInput("plate", PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100))) }
            );
            yield return ("angle", new[] { new ProjectionInput("angle", Angle(100, 10, 1000)) });
            yield return ("tube", new[] { new ProjectionInput("tube", Tube(168.3, 6, 1000, 32)) });
            yield return ("beam", BeamWithEndPlates());
            yield return ("tc1", Inputs(TestBodies.TC1()));
            yield return ("tc2", Inputs(TestBodies.TC2()));
            yield return ("tc3", Inputs(TestBodies.TC3()));
            yield return ("tc4", Inputs(TestBodies.TC4()));
            yield return ("tc5", Inputs(TestBodies.TC5()));
            yield return ("tc6", Inputs(TestBodies.TC6()));
            yield return ("tc7", Inputs(TestBodies.TC7()));
            yield return ("tc8", Inputs(TestBodies.TC8()));
            yield return ("tc9", Inputs(TestBodies.TC9()));
            yield return ("tc10", Inputs(TestBodies.TC10()));
        }

        private static IEnumerable<(
            string Name,
            ProjectionInput[] Inputs,
            GeoCoordinateSystem3 Frame,
            GeoPlane3 Plane
        )> Sections()
        {
            yield return (
                "beam",
                BeamWithEndPlates(),
                Right,
                new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis)
            );
            yield return (
                "tc1",
                Inputs(TestBodies.TC1()),
                Right,
                new GeoPlane3(new GeoPoint3(50, 0, 0), GeoVector3.XAxis)
            );
            yield return (
                "tc2",
                Inputs(TestBodies.TC2()),
                Right,
                new GeoPlane3(new GeoPoint3(1500, 0, 0), GeoVector3.XAxis)
            );
            yield return ("tc5", Inputs(TestBodies.TC5()), Front, new GeoPlane3(GeoPoint3.Origin, GeoVector3.YAxis));
            yield return (
                "tc7",
                Inputs(TestBodies.TC7()),
                Right,
                new GeoPlane3(new GeoPoint3(1000, 0, 0), GeoVector3.XAxis)
            );
        }

        private static ProjectionInput[] Inputs(TestBody body) =>
            body.Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)).ToArray();

        /// <summary>
        /// Writes the signatures taken in the folder proj\signatures of the run (<see cref="TestRun"/>) and gives them.
        /// </summary>
        private static string Kept(string name, string signatures)
        {
            string folder = TestRun.Folder("proj", "signatures");
            File.WriteAllText(Path.Combine(folder, name + ".txt"), signatures);
            return signatures;
        }

        private static string Hash(ProjectedView2 view) => Hash(Signature(view));

        private static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(text))
                        .Take(12)
                        .Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))
                );
            }
        }
    }
}
