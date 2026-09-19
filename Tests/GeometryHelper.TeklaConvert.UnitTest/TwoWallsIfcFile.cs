using System;
using System.IO;
using GeometryHelper.IfcConvert.Core;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// A small IFC file written to the temp folder: two 1 x 0.5 x 2 m walls in metres, Wall_A at the origin and
    /// Wall_B at (10, 0, 5) m. Disposing it drops the file from the global cache and deletes it.
    /// </summary>
    internal sealed class TwoWallsIfcFile : IDisposable
    {
        internal const string WallA = "0000000000000000000002";
        internal const string WallB = "0000000000000000000003";

        private const string Step = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('two_walls.ifc','2026-09-18T00:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('0000000000000000000001',$,'TwoWalls',$,$,$,$,$,#2);
#2=IFCUNITASSIGNMENT((#3));
#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);
#4=IFCCARTESIANPOINT((0.,0.));
#5=IFCAXIS2PLACEMENT2D(#4,$);
#6=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCAXIS2PLACEMENT3D(#7,$,$);
#9=IFCDIRECTION((0.,0.,1.));
#10=IFCEXTRUDEDAREASOLID(#6,#8,#9,2.);
#11=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.0001,#8,$);
#12=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#10));
#13=IFCPRODUCTDEFINITIONSHAPE($,$,(#12));
#14=IFCLOCALPLACEMENT($,#8);
#15=IFCWALL('0000000000000000000002',$,'Wall_A',$,$,#14,#13,$,$);
#16=IFCCARTESIANPOINT((10.,0.,5.));
#17=IFCAXIS2PLACEMENT3D(#16,$,$);
#18=IFCLOCALPLACEMENT($,#17);
#19=IFCWALL('0000000000000000000003',$,'Wall_B',$,$,#18,#13,$,$);
ENDSEC;
END-ISO-10303-21;
";

        internal TwoWallsIfcFile()
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"teklaconvert_{Guid.NewGuid():N}.ifc");
            File.WriteAllText(FilePath, Step);
        }

        internal string FilePath { get; }

        public void Dispose()
        {
            IfcStoreCache.ClearGlobalCache(FilePath);

            try
            {
                File.Delete(FilePath);
            }
            catch (IOException)
            {
            }
        }
    }
}
