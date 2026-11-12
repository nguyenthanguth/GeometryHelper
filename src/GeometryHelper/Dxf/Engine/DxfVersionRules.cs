using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The versions a DXF file is saved as and what each one added to it: the header variables, the groups of a record and
    /// the kinds of record that a file of an older version has not, as AutoCAD 2021 writes one drawing in each of its versions.
    /// </summary>
    /// <remarks>
    /// The groups come from comparing AutoCAD 2021's DXF of one drawing of 1 300 lines, 130 hatches and a table, saved as R2000
    /// (AC1015), R2004 (AC1018), R2007 (AC1021), R2010 (AC1024), R2013 (AC1027) and R2018 (AC1032): a group that stands in a
    /// subclass from one version on and in none before is listed with that version. Those few the comparison did not reach
    /// (a viewport's lighting, a dimension style's handles) are as ezdxf 1.4.5 gives them.
    /// </remarks>
    internal static class DxfVersions
    {
        /// <summary>The number of R12 (AC1009), which a drawing is saved as by rules of its own (<see cref="DxfVersionConverter"/>).</summary>
        internal const int R12 = 1009;

        /// <summary>The versions of R2000 and later a drawing is saved as, oldest first: R2000, R2004, R2007, R2010, R2013 and R2018.</summary>
        internal static readonly string[] Written = { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

        // The header variables a version brought, by variable: nought for one R2000 has.
        private static readonly Dictionary<string, int> HeaderSince = Build(
            1018,
            "$LASTSAVEDBY $SORTENTS $INDEXCTL $HIDETEXT $XCLIPFRAME $HALOGAP $OBSCOLOR $OBSLTYPE $INTERSECTIONDISPLAY"
                + " $INTERSECTIONCOLOR $DIMASSOC $PROJECTNAME",
            1021,
            "$DIMFXL $DIMFXLON $DIMJOGANG $DIMTFILL $DIMTFILLCLR $DIMARCSYM $DIMLTYPE $DIMLTEX1 $DIMLTEX2 $CAMERADISPLAY"
                + " $LENSLENGTH $CAMERAHEIGHT $STEPSPERSEC $STEPSIZE $3DDWFPREC $PSOLWIDTH $PSOLHEIGHT $LOFTANG1 $LOFTANG2"
                + " $LOFTMAG1 $LOFTMAG2 $LOFTPARAM $LOFTNORMALS $LATITUDE $LONGITUDE $NORTHDIRECTION $TIMEZONE"
                + " $LIGHTGLYPHDISPLAY $TILEMODELIGHTSYNCH $CMATERIAL $SOLIDHIST $SHOWHIST $DWFFRAME $DGNFRAME"
                + " $REALWORLDSCALE $INTERFERECOLOR $INTERFEREOBJVS $INTERFEREVPVS $CSHADOW $SHADOWPLANELOCATION",
            1024,
            "$DIMTXTDIRECTION",
            1027,
            "$REQUIREDVERSIONS"
        );

        // The groups a version brought, by subclass marker and then group.
        private static readonly Dictionary<string, Dictionary<int, int>> GroupSince = BuildGroups(
            "AcDbEntity 420 430 440:1018 284 347 348 380 390:1021",
            "AcDbLayerTableRecord 420 430 440:1018 347:1021 348:1027",
            "AcDbViewTableRecord 73 332 334 348:1021 361:1024",
            "AcDbViewportTableRecord 1 60 61 63 141 142 170 282 292 332 333 348 361 421:1021",
            "AcDbBlockTableRecord 70 280 281:1021",
            "AcDbViewport 170:1018 61 63 91 141 142 282 292 332 333 335 343 344 348 361 421 431:1021",
            "AcDbDimension 73 74 75:1021 280:1024",
            "AcDbMText 45 63 90 421 431 441:1018 46:1021",
            "AcDbHatch 63 421 450 451 452 453 460 461 462 463 470:1018",
            "AcDbRasterImage 290:1024",
            "AcDbWipeout 290:1024",
            "AcDbAttribute 280:1021 71:1032",
            "AcDbAttributeDefinition 280:1021 71:1032",
            "AcDbMLeader 270 271 272 273:1024",
            "AcDbMLeaderStyle 179 271 272 273:1024 298:1027",
            "AcDbTableStyle 1 90 91:1021",
            "AcDbTable 280:1024",
            "AcDbLinkedTableData 304:1021",
            "AcDbMaterial 93 94 148 468:1021",
            "AcDbVisualStyle 421 424 425:1018",
            "AcDbPlotSettings 76 77 78:1018",
            "AcDbColor 62 420 430:1018",
            "AcDbDetailViewStyle 62:1018",
            "AcDbSectionViewStyle 62:1018",
            "AcDbModelDocViewStyle 90 300:1032",
            "AcDbModelerGeometry 2 290:1027",
            "AcDb3dSolid 350:1021"
        );

        // The subclass markers a version brought: a subclass left with nothing in a file before it loses its marker too.
        private static readonly Dictionary<string, int> SubclassSince = new Dictionary<string, int>(
            StringComparer.Ordinal
        )
        {
            ["AcDb3dSolid"] = 1021,
        };

        /// <summary>
        /// The number a version name stands for, <c>1015</c> for <c>AC1015</c>; -1 where the name is not "AC" and four digits.
        /// </summary>
        internal static int Number(string version)
        {
            string named = (version ?? string.Empty).Trim();

            if (
                named.Length == 6
                && (named[0] == 'A' || named[0] == 'a')
                && (named[1] == 'C' || named[1] == 'c')
                && int.TryParse(named.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            )
            {
                return number;
            }

            return -1;
        }

        /// <summary>
        /// The maintenance version of a drawing's format (<c>$ACADMAINTVER</c>) as AutoCAD 2021 writes it of each version: 18 for
        /// R2000, 104, 50, 226, 125, and 174 for R2018.
        /// </summary>
        internal static int Maintenance(int version)
        {
            switch (version)
            {
                case 1015:
                    return 18;
                case 1018:
                    return 104;
                case 1021:
                    return 50;
                case 1024:
                    return 226;
                case 1027:
                    return 125;
                default:
                    return 174;
            }
        }

        /// <summary>The name of a version as it stands in <c>$ACADVER</c>, <c>AC1015</c> for 1015.</summary>
        internal static string Name(int number) => "AC" + number.ToString(CultureInfo.InvariantCulture);

        /// <summary>Whether a drawing is saved as the version: R12, or one of <see cref="Written"/>.</summary>
        internal static bool IsWritten(int number) => number == R12 || Array.IndexOf(Written, Name(number)) >= 0;

        /// <summary>The version that brought a header variable: nought for one R2000 has.</summary>
        internal static int SinceOfVariable(string variable) =>
            HeaderSince.TryGetValue(variable, out int since) ? since : 0;

        /// <summary>The version that brought a group of a subclass: nought for one R2000 has, or that is not listed.</summary>
        internal static int SinceOfGroup(string subclass, int code) =>
            GroupSince.TryGetValue(subclass, out Dictionary<int, int> groups) && groups.TryGetValue(code, out int since)
                ? since
                : 0;

        /// <summary>The version that brought a subclass marker: nought for one R2000 has, or that is not listed.</summary>
        internal static int SinceOfSubclass(string subclass) =>
            SubclassSince.TryGetValue(subclass, out int since) ? since : 0;

        /// <summary>The variables of a header, given as pairs of a version and the names it brought, separated by spaces.</summary>
        private static Dictionary<string, int> Build(params object[] versionsAndNames)
        {
            var made = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < versionsAndNames.Length; i += 2)
            {
                foreach (string name in ((string)versionsAndNames[i + 1]).Split(' '))
                {
                    made[name] = (int)versionsAndNames[i];
                }
            }

            return made;
        }

        /// <summary>
        /// The groups of subclasses, each given as the marker, then runs of codes each closed by a colon and the version that
        /// brought them: <c>"AcDbEntity 420 430 440:1018 284 347:1021"</c>.
        /// </summary>
        private static Dictionary<string, Dictionary<int, int>> BuildGroups(params string[] rows)
        {
            var made = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);

            foreach (string row in rows)
            {
                string[] words = row.Split(' ');
                var groups = new Dictionary<int, int>();
                var run = new List<int>();

                for (int i = 1; i < words.Length; i++)
                {
                    int colon = words[i].IndexOf(':');
                    string code = colon < 0 ? words[i] : words[i].Substring(0, colon);
                    run.Add(int.Parse(code, CultureInfo.InvariantCulture));

                    if (colon >= 0)
                    {
                        int since = int.Parse(words[i].Substring(colon + 1), CultureInfo.InvariantCulture);

                        foreach (int member in run)
                        {
                            groups[member] = since;
                        }

                        run.Clear();
                    }
                }

                made[words[0]] = groups;
            }

            return made;
        }
    }
}
