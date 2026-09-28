using System;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.IfcConvert.Core;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// An IFC file written to the temp folder: walls of 1 x 0.5 x 2 m in metres, all sharing one body, wall k placed at
    /// (2k, 0, 0) m. Disposing it drops the file from the global cache and deletes it.
    /// </summary>
    internal sealed class ManyWallsIfcFile : IDisposable
    {
        private const string Head = @"ISO-10303-21;
HEADER;
FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');
FILE_NAME('many_walls.ifc','2026-09-28T00:00:00',('Tester'),('TestOrg'),'xBIM','xBIM','');
FILE_SCHEMA(('IFC4'));
ENDSEC;
DATA;
#1=IFCPROJECT('P000000000000000000001',$,'ManyWalls',$,$,$,$,$,#2);
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
";

        internal ManyWallsIfcFile(int count)
        {
            var step = new StringBuilder(Head);

            for (int k = 0; k < count; k++)
            {
                int id = 100 + 4 * k;
                step.AppendLine(string.Format(CultureInfo.InvariantCulture, "#{0}=IFCCARTESIANPOINT(({1}.,0.,0.));", id, 2 * k));
                step.AppendLine(string.Format(CultureInfo.InvariantCulture, "#{0}=IFCAXIS2PLACEMENT3D(#{1},$,$);", id + 1, id));
                step.AppendLine(string.Format(CultureInfo.InvariantCulture, "#{0}=IFCLOCALPLACEMENT($,#{1});", id + 2, id + 1));
                step.AppendLine(string.Format(CultureInfo.InvariantCulture, "#{0}=IFCWALL('{1}',$,'Wall_{2}',$,$,#{3},#13,$,$);", id + 3, Guid(k), k, id + 2));
            }

            step.AppendLine("ENDSEC;");
            step.AppendLine("END-ISO-10303-21;");

            FilePath = Path.Combine(Path.GetTempPath(), $"teklaconvert_walls_{System.Guid.NewGuid():N}.ifc");
            File.WriteAllText(FilePath, step.ToString());
        }

        internal string FilePath { get; }

        /// <summary>
        /// The GlobalId of wall k.
        /// </summary>
        internal static string Guid(int k) => "W" + k.ToString("D21", CultureInfo.InvariantCulture);

        public void Dispose()
        {
            IfcStoreCache.ClearGlobalCache(FilePath);

            try
            {
                File.Delete(FilePath);
            }
            catch (IOException)
            {
                // A file still open elsewhere is left for the temp folder to clear.
            }
        }
    }
}
