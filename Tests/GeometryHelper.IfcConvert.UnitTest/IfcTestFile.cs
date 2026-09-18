using System;
using System.IO;
using GeometryHelper.IfcConvert.Core;

namespace GeometryHelper.IfcConvert.UnitTest
{
    /// <summary>
    /// Writes a synthetic IFC STEP file to the temp folder and opens it through <see cref="IfcStoreCache"/>.
    /// </summary>
    internal static class IfcTestFile
    {
        /// <summary>
        /// Shared entities #1-#11: project in <paramref name="lengthUnit"/>, 2D/3D origin placements,
        /// +Z direction and the 'Model' representation context. Test data should start at #20.
        /// </summary>
        public static string CommonHeader(string lengthUnit = "$,.METRE.") => $@"
#1=IFCPROJECT('0000000000000000000001',$,'TestProject',$,$,$,$,(#11),#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,{lengthUnit});
#4=IFCCARTESIANPOINT((0.,0.));
#5=IFCAXIS2PLACEMENT2D(#4,$);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCAXIS2PLACEMENT3D(#7,$,$);
#9=IFCDIRECTION((0.,0.,1.));
#11=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.0001,#8,$);
";

        /// <summary>
        /// Builds a full STEP file around <paramref name="data"/> and runs <paramref name="test"/> on it.
        /// </summary>
        public static void Run(string data, Action<IfcStoreCache> test, string schema = "IFC4")
        {
            string step = $@"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('test.ifc','2026-09-18T00:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('{schema}'));
ENDSEC;
DATA;
{data.Trim()}
ENDSEC;
END-ISO-10303-21;
";
            string tempFile = Path.Combine(Path.GetTempPath(), $"ifctest_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(tempFile, step);

            try
            {
                using (IfcStoreCache model = IfcStoreCache.Open(tempFile))
                {
                    test(model);
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
