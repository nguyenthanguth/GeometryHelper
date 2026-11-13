namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ACIS data of regions as AutoCAD 2021 writes it in DXF, each a list of the lines of SAT with the characters decoded
    /// (a space after a caret and the cipher of 0x9F less each letter undone), the lines of a probe: regions made by the .NET
    /// API from a rectangle 40 by 30, a circle of radius 7, a polyline of a stadium 30 by 10 with half circles on its ends, a
    /// rectangle with a hole, two holes and an ellipse, written by <c>Database.DxfOut</c> for each version.
    /// </summary>
    internal static class AcisAutoCadSamples
    {
        internal static readonly string[] Rect2000 =
        {
            "400 26 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $-1 $6 $3 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $7 $8 $-1 $9 forward $4 $-1 #",
            "coedge $-1 $10 $6 $-1 $11 forward $4 $-1 #",
            "coedge $-1 $6 $10 $-1 $12 forward $4 $-1 #",
            "edge $-1 $13 $14 $6 $15 forward #",
            "coedge $-1 $8 $7 $-1 $16 forward $4 $-1 #",
            "edge $-1 $14 $17 $7 $18 forward #",
            "edge $-1 $19 $13 $8 $20 forward #",
            "vertex $-1 $9 $21 #",
            "vertex $-1 $9 $22 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "edge $-1 $17 $19 $10 $23 forward #",
            "vertex $-1 $11 $24 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 $16 $25 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 0 0 #",
            "point $-1 40 0 0 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "point $-1 0 30 0 #",
        };

        internal static readonly string[] Rect2004 =
        {
            "20800 27 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $11 $-1 $13 forward $5 $-1 #",
            "edge $-1 -1 $-1 $14 0 $15 40 $7 $16 forward @7 unknown #",
            "coedge $-1 -1 $-1 $9 $8 $-1 $17 forward $5 $-1 #",
            "edge $-1 -1 $-1 $15 0 $18 30 $8 $19 forward @7 unknown #",
            "edge $-1 -1 $-1 $20 0 $14 30 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $10 $22 #",
            "vertex $-1 -1 $-1 $10 $23 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "edge $-1 -1 $-1 $18 0 $20 40 $11 $24 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 $25 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 -1 $-1 $17 $26 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 40 0 0 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "point $-1 -1 $-1 0 30 0 #",
        };

        internal static readonly string[] Rect2010 =
        {
            "21500 27 2 24          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $11 $-1 $13 forward $5 $-1 #",
            "edge $-1 -1 $-1 $14 0 $15 40 $7 $16 forward @7 unknown #",
            "coedge $-1 -1 $-1 $9 $8 $-1 $17 forward $5 $-1 #",
            "edge $-1 -1 $-1 $15 0 $18 30 $8 $19 forward @7 unknown #",
            "edge $-1 -1 $-1 $20 0 $14 30 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $10 0 $22 #",
            "vertex $-1 -1 $-1 $10 1 $23 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "edge $-1 -1 $-1 $18 0 $20 40 $11 $24 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 1 $25 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 -1 $-1 $17 1 $26 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 40 0 0 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "point $-1 -1 $-1 0 30 0 #",
        };

        internal static readonly string[] Circle2000 =
        {
            "400 11 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $-1 $6 $3 #",
            "plane-surface $-1 0 0 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $6 $6 $-1 $7 forward $4 $-1 #",
            "edge $-1 $8 $8 $6 $9 forward #",
            "vertex $-1 $7 $10 #",
            "ellipse-curve $-1 0 0 0 0 0 1 -7 -0 0 1 I I #",
            "point $-1 7 8.572527594031472204e-16 0 #",
        };

        internal static readonly string[] Circle2010 =
        {
            "21500 12 2 24          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 0 0 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $7 $7 $-1 $8 forward $5 $-1 #",
            "edge $-1 -1 $-1 $9 -3.141592653589793116 $9 3.141592653589793116 $7 $10 forward @7 unknown #",
            "vertex $-1 -1 $-1 $8 2 $11 #",
            "ellipse-curve $-1 -1 $-1 0 0 0 0 0 1 -7 -0 0 1 I I #",
            "point $-1 -1 $-1 7 8.572527594031472204e-16 0 #",
        };

        internal static readonly string[] Stadium2007 =
        {
            "21200 27 2 26          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 15 5 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $11 $-1 $13 forward $5 $-1 #",
            "edge $-1 -1 $-1 $14 0 $15 30 $7 $16 forward @7 unknown #",
            "coedge $-1 -1 $-1 $9 $8 $-1 $17 forward $5 $-1 #",
            "edge $-1 -1 $-1 $15 0 $18 3.141592653589793116 $8 $19 forward @7 unknown #",
            "edge $-1 -1 $-1 $20 0 $14 3.141592653589793116 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $10 0 $22 #",
            "vertex $-1 -1 $-1 $10 1 $23 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 30 #",
            "edge $-1 -1 $-1 $18 0 $20 30 $11 $24 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 1 $25 #",
            "ellipse-curve $-1 -1 $-1 30 5 0 0 0 1 0 -5 0 1 F 0 F 3.141592653589793116 #",
            "vertex $-1 -1 $-1 $17 1 $26 #",
            "ellipse-curve $-1 -1 $-1 -8.881784197001252323e-16 5 0 -0 0 1 8.881784197001252323e-16 5 0 1 F 0 F 3.141592653589793116 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 30 0 0 #",
            "straight-curve $-1 -1 $-1 30 10 0 -1 0 0 F 0 F 30 #",
            "point $-1 -1 $-1 30 10 0 #",
            "point $-1 -1 $-1 0 10 0 #",
        };

        internal static readonly string[] RectHole2000 =
        {
            "400 32 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $6 $7 $3 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "loop $-1 $-1 $8 $3 #",
            "coedge $-1 $7 $7 $-1 $9 forward $4 $-1 #",
            "coedge $-1 $10 $11 $-1 $12 forward $6 $-1 #",
            "edge $-1 $13 $13 $7 $14 forward #",
            "coedge $-1 $15 $8 $-1 $16 forward $6 $-1 #",
            "coedge $-1 $8 $15 $-1 $17 forward $6 $-1 #",
            "edge $-1 $18 $19 $8 $20 forward #",
            "vertex $-1 $9 $21 #",
            "ellipse-curve $-1 20 15 0 -0 -0 -1 -5 -0 0 1 I I #",
            "coedge $-1 $11 $10 $-1 $22 forward $6 $-1 #",
            "edge $-1 $19 $23 $10 $24 forward #",
            "edge $-1 $25 $18 $11 $26 forward #",
            "vertex $-1 $12 $27 #",
            "vertex $-1 $12 $28 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 25 15 0 #",
            "edge $-1 $23 $25 $15 $29 forward #",
            "vertex $-1 $16 $30 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 $22 $31 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 0 0 #",
            "point $-1 40 0 0 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "point $-1 0 30 0 #",
        };

        internal static readonly string[] RectHole2007 =
        {
            "21200 33 2 26          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $7 $8 $4 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "loop $-1 -1 $-1 $-1 $9 $4 #",
            "coedge $-1 -1 $-1 $8 $8 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $12 $-1 $13 forward $7 $-1 #",
            "edge $-1 -1 $-1 $14 -3.141592653589793116 $14 3.141592653589793116 $8 $15 forward @7 unknown #",
            "coedge $-1 -1 $-1 $16 $9 $-1 $17 forward $7 $-1 #",
            "coedge $-1 -1 $-1 $9 $16 $-1 $18 forward $7 $-1 #",
            "edge $-1 -1 $-1 $19 0 $20 40 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $10 0 $22 #",
            "ellipse-curve $-1 -1 $-1 20 15 0 -0 -0 -1 -5 -0 0 1 I I #",
            "coedge $-1 -1 $-1 $12 $11 $-1 $23 forward $7 $-1 #",
            "edge $-1 -1 $-1 $20 0 $24 30 $11 $25 forward @7 unknown #",
            "edge $-1 -1 $-1 $26 0 $19 30 $12 $27 forward @7 unknown #",
            "vertex $-1 -1 $-1 $13 0 $28 #",
            "vertex $-1 -1 $-1 $13 1 $29 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 25 15 0 #",
            "edge $-1 -1 $-1 $24 0 $26 40 $16 $30 forward @7 unknown #",
            "vertex $-1 -1 $-1 $17 1 $31 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 -1 $-1 $23 1 $32 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 40 0 0 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "point $-1 -1 $-1 0 30 0 #",
        };

        internal static readonly string[] TwoHoles2010 =
        {
            "21500 39 2 24          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $7 $8 $4 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "loop $-1 -1 $-1 $9 $10 $4 #",
            "coedge $-1 -1 $-1 $8 $8 $-1 $11 forward $5 $-1 #",
            "loop $-1 -1 $-1 $-1 $12 $4 #",
            "coedge $-1 -1 $-1 $10 $10 $-1 $13 forward $7 $-1 #",
            "edge $-1 -1 $-1 $14 -3.141592653589793116 $14 3.141592653589793116 $8 $15 forward @7 unknown #",
            "coedge $-1 -1 $-1 $16 $17 $-1 $18 forward $9 $-1 #",
            "edge $-1 -1 $-1 $19 -3.141592653589793116 $19 3.141592653589793116 $10 $20 forward @7 unknown #",
            "vertex $-1 -1 $-1 $11 0 $21 #",
            "ellipse-curve $-1 -1 $-1 30 15 0 -0 -0 -1 -3 -0 0 1 I I #",
            "coedge $-1 -1 $-1 $22 $12 $-1 $23 forward $9 $-1 #",
            "coedge $-1 -1 $-1 $12 $22 $-1 $24 forward $9 $-1 #",
            "edge $-1 -1 $-1 $25 0 $26 40 $12 $27 forward @7 unknown #",
            "vertex $-1 -1 $-1 $13 0 $28 #",
            "ellipse-curve $-1 -1 $-1 10 15 0 -0 -0 -1 -4 -0 0 1 I I #",
            "point $-1 -1 $-1 33 15 0 #",
            "coedge $-1 -1 $-1 $17 $16 $-1 $29 forward $9 $-1 #",
            "edge $-1 -1 $-1 $26 0 $30 30 $16 $31 forward @7 unknown #",
            "edge $-1 -1 $-1 $32 0 $25 30 $17 $33 forward @7 unknown #",
            "vertex $-1 -1 $-1 $18 0 $34 #",
            "vertex $-1 -1 $-1 $18 1 $35 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 14 15 0 #",
            "edge $-1 -1 $-1 $30 0 $32 40 $22 $36 forward @7 unknown #",
            "vertex $-1 -1 $-1 $23 1 $37 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 -1 $-1 $29 1 $38 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 40 0 0 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "point $-1 -1 $-1 0 30 0 #",
        };

        internal static readonly string[] RectInRect2004 =
        {
            "20800 48 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $7 $8 $4 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "loop $-1 -1 $-1 $-1 $9 $4 #",
            "coedge $-1 -1 $-1 $10 $11 $-1 $12 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $13 $14 $-1 $15 forward $7 $-1 #",
            "coedge $-1 -1 $-1 $16 $8 $-1 $17 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $8 $16 $-1 $18 forward $5 $-1 #",
            "edge $-1 -1 $-1 $19 -8 $20 -0 $8 $21 forward @7 unknown #",
            "coedge $-1 -1 $-1 $22 $9 $-1 $23 forward $7 $-1 #",
            "coedge $-1 -1 $-1 $9 $22 $-1 $24 forward $7 $-1 #",
            "edge $-1 -1 $-1 $25 0 $26 40 $9 $27 forward @7 unknown #",
            "coedge $-1 -1 $-1 $11 $10 $-1 $28 forward $5 $-1 #",
            "edge $-1 -1 $-1 $20 -6 $29 0 $10 $30 forward @7 unknown #",
            "edge $-1 -1 $-1 $31 -6 $19 -0 $11 $32 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 $33 #",
            "vertex $-1 -1 $-1 $12 $34 #",
            "straight-curve $-1 -1 $-1 10 10 0 -1 -0 -0 F -8 F -0 #",
            "coedge $-1 -1 $-1 $14 $13 $-1 $35 forward $7 $-1 #",
            "edge $-1 -1 $-1 $26 0 $36 30 $13 $37 forward @7 unknown #",
            "edge $-1 -1 $-1 $38 0 $25 30 $14 $39 forward @7 unknown #",
            "vertex $-1 -1 $-1 $15 $40 #",
            "vertex $-1 -1 $-1 $15 $41 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "edge $-1 -1 $-1 $29 -8 $31 0 $16 $42 forward @7 unknown #",
            "vertex $-1 -1 $-1 $28 $43 #",
            "straight-curve $-1 -1 $-1 10 16 0 -0 1 -0 F -6 F -0 #",
            "vertex $-1 -1 $-1 $18 $44 #",
            "straight-curve $-1 -1 $-1 18 10 0 -0 -1 -0 F -6 F -0 #",
            "point $-1 -1 $-1 18 10 0 #",
            "point $-1 -1 $-1 10 10 0 #",
            "edge $-1 -1 $-1 $36 0 $38 40 $22 $45 forward @7 unknown #",
            "vertex $-1 -1 $-1 $23 $46 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "vertex $-1 -1 $-1 $35 $47 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 40 0 0 #",
            "straight-curve $-1 -1 $-1 18 16 0 1 -0 -0 F -8 F -0 #",
            "point $-1 -1 $-1 10 16 0 #",
            "point $-1 -1 $-1 18 16 0 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "point $-1 -1 $-1 0 30 0 #",
        };

        internal static readonly string[] Tilted2004 =
        {
            "20800 22 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 6.705406887010544992 3.794095225512603697 3.794095225512603697 -1.502607543985752744e-17 -0.7071067811865475727 0.7071067811865475727 1 -0 2.125007967628778252e-17 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $9 $7 $-1 $11 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $8 $-1 $12 forward $5 $-1 #",
            "edge $-1 -1 $-1 $13 0 $14 17.3205080756887746 $7 $15 forward @7 unknown #",
            "edge $-1 -1 $-1 $14 0 $16 10 $8 $17 forward @7 unknown #",
            "edge $-1 -1 $-1 $16 0 $13 14.14213562373095101 $9 $18 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 $19 #",
            "vertex $-1 -1 $-1 $11 $20 #",
            "straight-curve $-1 -1 $-1 10 10 10 -0.5773502691896257311 -0.5773502691896257311 -0.5773502691896257311 F 0 F 17.3205080756887746 #",
            "vertex $-1 -1 $-1 $11 $21 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 10 #",
            "straight-curve $-1 -1 $-1 10 0 0 0 0.7071067811865474617 0.7071067811865474617 F 0 F 14.14213562373095101 #",
            "point $-1 -1 $-1 10 10 10 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 10 0 0 #",
        };

        internal static readonly string[] RectClockwise2004 =
        {
            "20800 27 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 reversed double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 5 3 0 -0 -0 -1 -1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 reversed $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $7 $-1 $12 reversed $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $11 $-1 $13 reversed $5 $-1 #",
            "edge $-1 -1 $-1 $14 0 $15 6 $7 $16 forward @7 unknown #",
            "coedge $-1 -1 $-1 $9 $8 $-1 $17 reversed $5 $-1 #",
            "edge $-1 -1 $-1 $18 0 $14 10 $8 $19 forward @7 unknown #",
            "edge $-1 -1 $-1 $15 0 $20 10 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $10 $22 #",
            "vertex $-1 -1 $-1 $10 $23 #",
            "straight-curve $-1 -1 $-1 0 0 0 0 1 0 F 0 F 6 #",
            "edge $-1 -1 $-1 $20 0 $18 6 $11 $24 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 $25 #",
            "straight-curve $-1 -1 $-1 10 0 0 -1 0 0 F 0 F 10 #",
            "vertex $-1 -1 $-1 $17 $26 #",
            "straight-curve $-1 -1 $-1 0 6 0 1 0 0 F 0 F 10 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 0 6 0 #",
            "straight-curve $-1 -1 $-1 10 6 0 0 -1 0 F 0 F 6 #",
            "point $-1 -1 $-1 10 0 0 #",
            "point $-1 -1 $-1 10 6 0 #",
        };

        internal static readonly string[] DShape2010 =
        {
            "21500 27 2 24          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $-1 $3 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $4 $-1 $2 #",
            "face $-1 -1 $-1 $-1 $5 $3 $-1 $6 forward double out #",
            "loop $-1 -1 $-1 $-1 $7 $4 #",
            "plane-surface $-1 -1 $-1 13.22131600159529441 10 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 -1 $-1 $7 $11 $-1 $13 forward $5 $-1 #",
            "edge $-1 -1 $-1 $14 0 $15 20 $7 $16 forward @7 unknown #",
            "coedge $-1 -1 $-1 $9 $8 $-1 $17 forward $5 $-1 #",
            "edge $-1 -1 $-1 $15 0 $18 20 $8 $19 forward @7 unknown #",
            "edge $-1 -1 $-1 $20 0 $14 20 $9 $21 forward @7 unknown #",
            "vertex $-1 -1 $-1 $13 1 $22 #",
            "vertex $-1 -1 $-1 $12 0 $23 #",
            "straight-curve $-1 -1 $-1 0 20 0 0 -1 0 F 0 F 20 #",
            "edge $-1 -1 $-1 $18 0 $20 3.14159265358979356 $11 $24 forward @7 unknown #",
            "vertex $-1 -1 $-1 $12 1 $25 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 20 #",
            "vertex $-1 -1 $-1 $17 1 $26 #",
            "straight-curve $-1 -1 $-1 20 20 0 -1 0 0 F 0 F 20 #",
            "point $-1 -1 $-1 0 20 0 #",
            "point $-1 -1 $-1 0 0 0 #",
            "ellipse-curve $-1 -1 $-1 20 10 0 0 0 1 -3.552713678800500929e-15 -10 0 1 F 0 F 3.14159265358979356 #",
            "point $-1 -1 $-1 20 0 0 #",
            "point $-1 -1 $-1 20 20 0 #",
        };

        internal static readonly string[] Ellipse2000 =
        {
            "400 11 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $-1 $6 $3 #",
            "plane-surface $-1 5 5 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $6 $6 $-1 $7 forward $4 $-1 #",
            "edge $-1 $8 $8 $6 $9 forward #",
            "vertex $-1 $7 $10 #",
            "ellipse-curve $-1 5 5 0 0 0 1 -14 -0 -0 0.5 I I #",
            "point $-1 19 5.000000000000000888 0 #",
        };

        internal static readonly string[] HalfEllipse2000 =
        {
            "400 16 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $-1 $6 $3 #",
            "plane-surface $-1 2.653525527224599059e-16 1.57191584907003068 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $7 $7 $-1 $8 forward $4 $-1 #",
            "coedge $-1 $6 $6 $-1 $9 forward $4 $-1 #",
            "edge $-1 $10 $11 $6 $12 forward #",
            "edge $-1 $11 $10 $7 $13 forward #",
            "vertex $-1 $9 $14 #",
            "vertex $-1 $9 $15 #",
            "straight-curve $-1 -10 0 0 1 0 0 F 0 F 20 #",
            "ellipse-curve $-1 0 0 0 0 0 1 10 0 0 0.5 F 0 F 3.141592653589793116 #",
            "point $-1 -10 6.123233995736766282e-16 0 #",
            "point $-1 10 0 0 #",
        };

        internal static readonly string[] EllipseRotated2000 =
        {
            "400 11 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $-1 $-1 $4 $2 $-1 $5 forward double out #",
            "loop $-1 $-1 $6 $3 #",
            "plane-surface $-1 5 5 0 0 -0 1 1 0 -0 forward_v I I I I #",
            "coedge $-1 $6 $6 $-1 $7 forward $4 $-1 #",
            "edge $-1 $8 $8 $6 $9 forward #",
            "vertex $-1 $7 $10 #",
            "ellipse-curve $-1 5 5 0 0 -0 1 -10 -10 -0 0.4000000000000000222 I I #",
            "point $-1 15 15 0 #",
        };

        /// <summary>The SAB of the same regions as AutoCAD 2021 writes it into the data store of a DXF of R2018, as hex; its date is "Fri Nov 13 08:05:36 2026".</summary>
        internal const string Rect2018 =
            ""
            + "41534D2042696E61727946696C65341C57000000000000020000000400000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030383A30353A33362032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0CFFFFFFFF110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C030000000C01000000110D057368656C6C0CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0CFFFFFFFF0CFFFFFFFF0C040000000CFFFFFFFF0C02000000110D04666163650CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C050000000C030000000CFFFFFFFF0C060000000B0A0B110D046C6F6F700CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C070000000C04000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFF"
            + "FF130000000000003440FFFFFFFFFFFF2D4000000000000000001400000000000000000000000000000000000000000000F0"
            + "3F14000000000000F03F000000000000000000000000000000000B0B0B0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C080000000C090000000CFFFFFFFF0C0A0000000B0C0500000004000000000CFFFFFFFF110D06636F65646765"
            + "0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0B0000000C070000000CFFFFFFFF0C0C0000000B0C0500000004000000000CFFFFFF"
            + "FF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C070000000C0B0000000CFFFFFFFF0C0D0000000B0C050000"
            + "0004000000000CFFFFFFFF110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0E0000000600000000000000000C0F00"
            + "00000600000000000044400C070000000C100000000B0707756E6B6E6F776E110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C090000000C080000000CFFFFFFFF0C110000000B0C0500000004000000000CFFFFFFFF110D04656467650CFF"
            + "FFFFFF04FFFFFFFF0CFFFFFFFF0C0F0000000600000000000000000C12000000060000000000003E400C080000000C130000"
            + "000B0707756E6B6E6F776E110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C140000000600000000000000000C0E00"
            + "0000060000000000003E400C090000000C150000000B0707756E6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C0A00000004000000000C16000000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0A00000004"
            + "010000000C17000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000"
            + "000000000000000000000000000000000014000000000000F03F000000000000000000000000000000000A06000000000000"
            + "00000A060000000000004440110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C120000000600000000000000000C14"
            + "0000000600000000000044400C0B0000000C180000000B0707756E6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFF"
            + "FF0CFFFFFFFF0C0C00000004010000000C19000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF13000000000000444000000000000000000000000000000000140000000000000000000000000000F03F00000000"
            + "000000000A0600000000000000000A060000000000003E40110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C11"
            + "00000004010000000C1A000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF13000000"
            + "00000000000000000000003E400000000000000000140000000000000000000000000000F0BF00000000000000000A060000"
            + "0000000000000A060000000000003E40110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000"
            + "0000000000000000000000000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF13000000000000444000000000"
            + "000000000000000000000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000"
            + "000044400000000000003E40000000000000000014000000000000F0BF000000000000000000000000000000000A06000000"
            + "00000000000A060000000000004440110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000004440000000"
            + "0000003E400000000000000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000"
            + "003E400000000000000000110E03456E640E026F660E0341534D0D0464617461";

        internal const string Circle2018 =
            ""
            + "41534D2042696E61727946696C65341C57000000000000020000000400000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030383A30353A33362032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0CFFFFFFFF110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C030000000C01000000110D057368656C6C0CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0CFFFFFFFF0CFFFFFFFF0C040000000CFFFFFFFF0C02000000110D04666163650CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C050000000C030000000CFFFFFFFF0C060000000B0A0B110D046C6F6F700CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C070000000C04000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFF"
            + "FF130000000000000000000000000000000000000000000000001400000000000000000000000000000000000000000000F0"
            + "3F14000000000000F03F000000000000000000000000000000000B0B0B0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C070000000C070000000CFFFFFFFF0C080000000B0C0500000004000000000CFFFFFFFF110D04656467650CFF"
            + "FFFFFF04FFFFFFFF0CFFFFFFFF0C0900000006182D4454FB2109C00C0900000006182D4454FB2109400C070000000C0A0000"
            + "000B0707756E6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0800000004020000000C0B000000"
            + "110E07656C6C697073650D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000000000000000000000000000"
            + "000000000000001400000000000000000000000000000000000000000000F03F140000000000001CC0000000000000008000"
            + "0000000000000006000000000000F03F0B0B110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000001C40"
            + "0CA163D9C2E2CE3C0000000000000000110E03456E640E026F660E0341534D0D0464617461";

        internal const string RectHole2018 =
            ""
            + "41534D2042696E61727946696C65341C57000000000000020000000400000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030383A30353A33362032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0CFFFFFFFF110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C030000000C01000000110D057368656C6C0CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0CFFFFFFFF0CFFFFFFFF0C040000000CFFFFFFFF0C02000000110D04666163650CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C050000000C030000000CFFFFFFFF0C060000000B0A0B110D046C6F6F700CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0C070000000C080000000C04000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFF"
            + "FF130000000000003440FFFFFFFFFFFF2D4000000000000000001400000000000000000000000000000000000000000000F0"
            + "3F14000000000000F03F000000000000000000000000000000000B0B0B0B0B110D046C6F6F700CFFFFFFFF04FFFFFFFF0CFF"
            + "FFFFFF0CFFFFFFFF0C090000000C04000000110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C080000000C0800"
            + "00000CFFFFFFFF0C0A0000000B0C0500000004000000000CFFFFFFFF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFF"
            + "FFFF0C0B0000000C0C0000000CFFFFFFFF0C0D0000000B0C0700000004000000000CFFFFFFFF110D04656467650CFFFFFFFF"
            + "04FFFFFFFF0CFFFFFFFF0C0E00000006182D4454FB2109C00C0E00000006182D4454FB2109400C080000000C0F0000000B07"
            + "07756E6B6E6F776E110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C100000000C090000000CFFFFFFFF0C1100"
            + "00000B0C0700000004000000000CFFFFFFFF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C090000000C1000"
            + "00000CFFFFFFFF0C120000000B0C0700000004000000000CFFFFFFFF110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF"
            + "0C130000000600000000000000000C140000000600000000000044400C090000000C150000000B0707756E6B6E6F776E110D"
            + "067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0A00000004000000000C16000000110E07656C6C697073650D0563"
            + "757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000034400000000000002E400000000000000000140000000000"
            + "0000800000000000000080000000000000F0BF1400000000000014C000000000000000800000000000000000060000000000"
            + "00F03F0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0C0000000C0B0000000CFFFFFFFF0C170000000B"
            + "0C0700000004000000000CFFFFFFFF110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C140000000600000000000000"
            + "000C18000000060000000000003E400C0B0000000C190000000B0707756E6B6E6F776E110D04656467650CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0C1A0000000600000000000000000C13000000060000000000003E400C0C0000000C1B0000000B0707756E"
            + "6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0D00000004000000000C1C000000110D06766572"
            + "7465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0D00000004010000000C1D000000110E0873747261696768740D0563757276"
            + "650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000000000000000000000000014000000000000F03F"
            + "000000000000000000000000000000000A0600000000000000000A060000000000004440110D05706F696E740CFFFFFFFF04"
            + "FFFFFFFF0CFFFFFFFF1300000000000039400000000000002E400000000000000000110D04656467650CFFFFFFFF04FFFFFF"
            + "FF0CFFFFFFFF0C180000000600000000000000000C1A0000000600000000000044400C100000000C1E0000000B0707756E6B"
            + "6E6F776E110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1100000004010000000C1F000000110E0873747261"
            + "696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000004440000000000000000000000000000000"
            + "00140000000000000000000000000000F03F00000000000000000A0600000000000000000A060000000000003E40110D0676"
            + "65727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1700000004010000000C20000000110E0873747261696768740D056375"
            + "7276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000003E40000000000000000014000000000000"
            + "0000000000000000F0BF00000000000000000A0600000000000000000A060000000000003E40110D05706F696E740CFFFFFF"
            + "FF04FFFFFFFF0CFFFFFFFF13000000000000000000000000000000000000000000000000110D05706F696E740CFFFFFFFF04"
            + "FFFFFFFF0CFFFFFFFF13000000000000444000000000000000000000000000000000110E0873747261696768740D05637572"
            + "76650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000044400000000000003E40000000000000000014000000000000F0"
            + "BF000000000000000000000000000000000A0600000000000000000A060000000000004440110D05706F696E740CFFFFFFFF"
            + "04FFFFFFFF0CFFFFFFFF1300000000000044400000000000003E400000000000000000110D05706F696E740CFFFFFFFF04FF"
            + "FFFFFF0CFFFFFFFF1300000000000000000000000000003E400000000000000000110E03456E640E026F660E0341534D0D04"
            + "64617461";

        internal const string Stadium2013 =
            ""
            + "414349532042696E61727946696C652855000000000000020000000C00000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030383A30353A33372032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0CFFFFFFFF110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C030000000C01000000110D057368656C6C0CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0CFFFFFFFF0CFFFFFFFF0C040000000CFFFFFFFF0C02000000110D04666163650CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C050000000C030000000CFFFFFFFF0C060000000B0A0B110D046C6F6F700CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C070000000C04000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFF"
            + "FF130000000000002E40000000000000144000000000000000001400000000000000000000000000000000000000000000F0"
            + "3F14000000000000F03F000000000000000000000000000000000B0B0B0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C080000000C090000000CFFFFFFFF0C0A0000000B0C0500000004000000000CFFFFFFFF110D06636F65646765"
            + "0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0B0000000C070000000CFFFFFFFF0C0C0000000B0C0500000004000000000CFFFFFF"
            + "FF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C070000000C0B0000000CFFFFFFFF0C0D0000000B0C050000"
            + "0004000000000CFFFFFFFF110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0E0000000600000000000000000C0F00"
            + "0000060000000000003E400C070000000C100000000B0707756E6B6E6F776E110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C090000000C080000000CFFFFFFFF0C110000000B0C0500000004000000000CFFFFFFFF110D04656467650CFF"
            + "FFFFFF04FFFFFFFF0CFFFFFFFF0C0F0000000600000000000000000C1200000006182D4454FB2109400C080000000C130000"
            + "000B0707756E6B6E6F776E110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C140000000600000000000000000C0E00"
            + "000006182D4454FB2109400C090000000C150000000B0707756E6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C0A00000004000000000C16000000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0A00000004"
            + "010000000C17000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000"
            + "000000000000000000000000000000000014000000000000F03F000000000000000000000000000000000A06000000000000"
            + "00000A060000000000003E40110D04656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C120000000600000000000000000C14"
            + "000000060000000000003E400C0B0000000C180000000B0707756E6B6E6F776E110D067665727465780CFFFFFFFF04FFFFFF"
            + "FF0CFFFFFFFF0C0C00000004010000000C19000000110E07656C6C697073650D0563757276650CFFFFFFFF04FFFFFFFF0CFF"
            + "FFFFFF130000000000003E400000000000001440000000000000000014000000000000000000000000000000000000000000"
            + "00F03F14000000000000000000000000000014C0000000000000000006000000000000F03F0A0600000000000000000A0618"
            + "2D4454FB210940110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1100000004010000000C1A000000110E0765"
            + "6C6C697073650D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF13000000000000D0BC00000000000014400000000000"
            + "0000001400000000000000800000000000000000000000000000F03F14000000000000D03C00000000000014400000000000"
            + "00000006000000000000F03F0A0600000000000000000A06182D4454FB210940110D05706F696E740CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF13000000000000000000000000000000000000000000000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFF"
            + "FFFFFF130000000000003E4000000000000000000000000000000000110E0873747261696768740D0563757276650CFFFFFF"
            + "FF04FFFFFFFF0CFFFFFFFF130000000000003E400000000000002440000000000000000014000000000000F0BF0000000000"
            + "00000000000000000000000A0600000000000000000A060000000000003E40110D05706F696E740CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF130000000000003E4000000000000024400000000000000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFF"
            + "FFFF13000000000000000000000000000024400000000000000000110E03456E640E026F660E0341534D0D0464617461";
    }
}
