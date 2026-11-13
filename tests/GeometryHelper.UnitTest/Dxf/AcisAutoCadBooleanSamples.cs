namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ACIS data of regions AutoCAD 2021 made by booleans, moved, turned and mirrored, as its <c>DxfOut</c> writes it, by the
    /// same decoding as <see cref="AcisAutoCadSamples"/>: a lens of two circles of radius 10 (the first at (0, 0), the second at
    /// (10, 0)), a box 40 by 30 less a circle of radius 10 centred on its right edge (the notch), that notch moved by (100, 50),
    /// mirrored across the line x = 50 and turned 30 degrees about the origin, a box 40 by 30 united with a circle of radius 10
    /// at (100, 15) (two faces, which AutoCAD keeps as two lumps), and a box 60 by 60 less a box 40 by 40 united with a circle of
    /// radius 8 at its centre (a frame and an island), and an ellipse of half axes 30 and 10 about (20, 15) cut by the box (a line on
    /// each side and two elliptical arcs, whose loop runs up the right side against its edge). AutoCAD's data of a boolean differs from that of a plain REGION: an arc is
    /// the ellipse of its whole turn cut by the vertices of its edge in R2000, and each face carries colour attributes.
    /// </summary>
    internal static class AcisAutoCadBooleanSamples
    {
        internal static readonly string[] Lens2000 =
        {
            "400 19 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $4 $-1 $5 $2 $-1 $6 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $3 257 #",
            "loop $-1 $-1 $7 $3 #",
            "plane-surface $-1 0 0 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $8 $8 $-1 $9 reversed $5 $-1 #",
            "coedge $-1 $7 $7 $-1 $10 forward $5 $-1 #",
            "edge $11 $12 $13 $7 $14 forward #",
            "edge $15 $12 $13 $8 $16 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $9 257 #",
            "vertex $-1 $10 $17 #",
            "vertex $-1 $9 $18 #",
            "ellipse-curve $-1 10 0 0 -0 -0 -1 -10 -0 0 1 F -3.141592653589793116 F 3.141592653589793116 #",
            "color-adesk-attrib $-1 $-1 $-1 $10 257 #",
            "ellipse-curve $-1 0 0 0 0 0 1 -10 -0 0 1 I I #",
            "point $-1 5 -8.660254037844387298 0 #",
            "point $-1 5 8.660254037844387298 0 #",
        };

        internal static readonly string[] Notch2000 =
        {
            "400 42 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $4 $-1 $5 $2 $-1 $6 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $3 257 #",
            "loop $-1 $-1 $7 $3 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $8 $9 $-1 $10 forward $5 $-1 #",
            "coedge $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 $7 $13 $-1 $14 forward $5 $-1 #",
            "edge $15 $16 $17 $7 $18 forward #",
            "coedge $-1 $19 $8 $-1 $20 forward $5 $-1 #",
            "edge $21 $17 $22 $8 $23 forward #",
            "coedge $-1 $9 $19 $-1 $24 forward $5 $-1 #",
            "edge $25 $26 $16 $9 $23 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $10 257 #",
            "vertex $-1 $10 $27 #",
            "vertex $-1 $12 $28 #",
            "ellipse-curve $-1 40 15 0 -0 -0 -1 -10 -0 0 1 F -3.141592653589793116 F 3.141592653589793116 #",
            "coedge $-1 $13 $11 $-1 $29 forward $5 $-1 #",
            "edge $30 $22 $31 $11 $32 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $12 257 #",
            "vertex $-1 $12 $33 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "edge $34 $35 $26 $13 $36 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $14 257 #",
            "vertex $-1 $24 $37 #",
            "point $-1 40 5 0 #",
            "point $-1 40 25 0 #",
            "edge $38 $31 $35 $19 $39 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $20 257 #",
            "vertex $-1 $20 $40 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $24 257 #",
            "vertex $-1 $24 $41 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 40 0 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $29 257 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 30 0 #",
            "point $-1 0 0 0 #",
        };

        internal static readonly string[] NotchMirrored2000 =
        {
            "400 43 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $2 #",
            "lump $-1 $-1 $3 $0 #",
            "transform $-1 -1 0 -1.224646799147353207e-16 0 1 0 1.224646799147353207e-16 0 -1 100 0 6.123233995736766085e-15 1 rotate no_reflect no_shear #",
            "shell $-1 $-1 $-1 $4 $-1 $1 #",
            "face $5 $-1 $6 $3 $-1 $7 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $4 257 #",
            "loop $-1 $-1 $8 $4 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $9 $10 $-1 $11 forward $6 $-1 #",
            "coedge $-1 $12 $8 $-1 $13 forward $6 $-1 #",
            "coedge $-1 $8 $14 $-1 $15 forward $6 $-1 #",
            "edge $16 $17 $18 $8 $19 forward #",
            "coedge $-1 $20 $9 $-1 $21 forward $6 $-1 #",
            "edge $22 $18 $23 $9 $24 forward #",
            "coedge $-1 $10 $20 $-1 $25 forward $6 $-1 #",
            "edge $26 $27 $17 $10 $24 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $11 257 #",
            "vertex $-1 $11 $28 #",
            "vertex $-1 $13 $29 #",
            "ellipse-curve $-1 40 15 0 -0 -0 -1 -10 -0 0 1 F -3.141592653589793116 F 3.141592653589793116 #",
            "coedge $-1 $14 $12 $-1 $30 forward $6 $-1 #",
            "edge $31 $23 $32 $12 $33 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $13 257 #",
            "vertex $-1 $13 $34 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "edge $35 $36 $27 $14 $37 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $15 257 #",
            "vertex $-1 $25 $38 #",
            "point $-1 40 5 0 #",
            "point $-1 40 25 0 #",
            "edge $39 $32 $36 $20 $40 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $21 257 #",
            "vertex $-1 $21 $41 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $25 257 #",
            "vertex $-1 $25 $42 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 40 0 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $30 257 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 30 0 #",
            "point $-1 0 0 0 #",
        };

        internal static readonly string[] NotchMoved2000 =
        {
            "400 43 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $2 #",
            "lump $-1 $-1 $3 $0 #",
            "transform $-1 1 0 0 0 1 0 0 0 1 100 50 0 1 no_rotate no_reflect no_shear #",
            "shell $-1 $-1 $-1 $4 $-1 $1 #",
            "face $5 $-1 $6 $3 $-1 $7 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $4 257 #",
            "loop $-1 $-1 $8 $4 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $9 $10 $-1 $11 forward $6 $-1 #",
            "coedge $-1 $12 $8 $-1 $13 forward $6 $-1 #",
            "coedge $-1 $8 $14 $-1 $15 forward $6 $-1 #",
            "edge $16 $17 $18 $8 $19 forward #",
            "coedge $-1 $20 $9 $-1 $21 forward $6 $-1 #",
            "edge $22 $18 $23 $9 $24 forward #",
            "coedge $-1 $10 $20 $-1 $25 forward $6 $-1 #",
            "edge $26 $27 $17 $10 $24 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $11 257 #",
            "vertex $-1 $11 $28 #",
            "vertex $-1 $13 $29 #",
            "ellipse-curve $-1 40 15 0 -0 -0 -1 -10 -0 0 1 F -3.141592653589793116 F 3.141592653589793116 #",
            "coedge $-1 $14 $12 $-1 $30 forward $6 $-1 #",
            "edge $31 $23 $32 $12 $33 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $13 257 #",
            "vertex $-1 $13 $34 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "edge $35 $36 $27 $14 $37 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $15 257 #",
            "vertex $-1 $25 $38 #",
            "point $-1 40 5 0 #",
            "point $-1 40 25 0 #",
            "edge $39 $32 $36 $20 $40 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $21 257 #",
            "vertex $-1 $21 $41 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $25 257 #",
            "vertex $-1 $25 $42 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 40 0 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $30 257 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 30 0 #",
            "point $-1 0 0 0 #",
        };

        internal static readonly string[] NotchRotated2004 =
        {
            "20800 44 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $3 #",
            "lump $-1 -1 $-1 $-1 $4 $1 #",
            "transform $-1 -1 0.8660254037844387076 0.4999999999999999445 0 -0.4999999999999999445 0.8660254037844387076 0 0 -0 1 0 0 0 1 rotate no_reflect no_shear #",
            "shell $-1 -1 $-1 $-1 $-1 $5 $-1 $2 #",
            "face $6 -1 $-1 $-1 $7 $4 $-1 $8 forward double out #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $5 257 #",
            "loop $-1 -1 $-1 $-1 $9 $5 #",
            "plane-surface $-1 -1 $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 -1 $-1 $10 $11 $-1 $12 forward $7 $-1 #",
            "coedge $-1 -1 $-1 $13 $9 $-1 $14 forward $7 $-1 #",
            "coedge $-1 -1 $-1 $9 $15 $-1 $16 forward $7 $-1 #",
            "edge $17 -1 $-1 $18 -1.570796326794896558 $19 1.570796326794896558 $9 $20 forward @7 unknown #",
            "coedge $-1 -1 $-1 $21 $10 $-1 $22 forward $7 $-1 #",
            "edge $23 -1 $-1 $19 25 $24 30 $10 $25 forward @7 unknown #",
            "coedge $-1 -1 $-1 $11 $21 $-1 $26 forward $7 $-1 #",
            "edge $27 -1 $-1 $28 0 $18 5 $11 $25 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $12 257 #",
            "vertex $-1 -1 $-1 $12 $29 #",
            "vertex $-1 -1 $-1 $14 $30 #",
            "ellipse-curve $-1 -1 $-1 40 15 0 -0 -0 -1 -10 -0 0 1 F -3.141592653589793116 F 3.141592653589793116 #",
            "coedge $-1 -1 $-1 $15 $13 $-1 $31 forward $7 $-1 #",
            "edge $32 -1 $-1 $24 0 $33 40 $13 $34 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $14 257 #",
            "vertex $-1 -1 $-1 $14 $35 #",
            "straight-curve $-1 -1 $-1 40 0 0 0 1 0 F 0 F 30 #",
            "edge $36 -1 $-1 $37 0 $28 40 $15 $38 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $16 257 #",
            "vertex $-1 -1 $-1 $26 $39 #",
            "point $-1 -1 $-1 40 5 0 #",
            "point $-1 -1 $-1 40 25 0 #",
            "edge $40 -1 $-1 $33 0 $37 30 $21 $41 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $22 257 #",
            "vertex $-1 -1 $-1 $22 $42 #",
            "straight-curve $-1 -1 $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 30 0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $26 257 #",
            "vertex $-1 -1 $-1 $26 $43 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 40 #",
            "point $-1 -1 $-1 40 0 0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $31 257 #",
            "straight-curve $-1 -1 $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 -1 $-1 0 30 0 #",
            "point $-1 -1 $-1 0 0 0 #",
        };

        internal static readonly string[] Disjoint2000 =
        {
            "400 43 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $2 $3 $0 #",
            "lump $-1 $-1 $4 $0 #",
            "shell $-1 $-1 $-1 $5 $-1 $1 #",
            "shell $-1 $-1 $-1 $6 $-1 $2 #",
            "face $7 $-1 $8 $3 $-1 $9 forward double out #",
            "face $10 $-1 $11 $4 $-1 $12 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $5 257 #",
            "loop $-1 $-1 $13 $5 #",
            "plane-surface $-1 20 14.99999999999999822 0 0 0 1 1 0 0 forward_v I I I I #",
            "color-adesk-attrib $-1 $-1 $-1 $6 257 #",
            "loop $-1 $-1 $14 $6 #",
            "plane-surface $-1 100 15 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $15 $16 $-1 $17 forward $8 $-1 #",
            "coedge $-1 $14 $14 $-1 $18 forward $11 $-1 #",
            "coedge $-1 $19 $13 $-1 $20 forward $8 $-1 #",
            "coedge $-1 $13 $19 $-1 $21 forward $8 $-1 #",
            "edge $22 $23 $24 $13 $25 forward #",
            "edge $26 $27 $27 $14 $28 forward #",
            "coedge $-1 $16 $15 $-1 $29 forward $8 $-1 #",
            "edge $30 $24 $31 $15 $32 forward #",
            "edge $33 $34 $23 $16 $35 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $17 257 #",
            "vertex $-1 $17 $36 #",
            "vertex $-1 $17 $37 #",
            "straight-curve $-1 0 0 0 1 0 0 F 0 F 40 #",
            "color-adesk-attrib $-1 $-1 $-1 $18 257 #",
            "vertex $-1 $18 $38 #",
            "ellipse-curve $-1 100 15 0 0 0 1 -10 -0 0 1 I I #",
            "edge $39 $31 $34 $19 $40 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $20 257 #",
            "vertex $-1 $20 $41 #",
            "straight-curve $-1 40 0 0 0 1 0 F 0 F 30 #",
            "color-adesk-attrib $-1 $-1 $-1 $21 257 #",
            "vertex $-1 $29 $42 #",
            "straight-curve $-1 0 30 0 0 -1 0 F 0 F 30 #",
            "point $-1 0 0 0 #",
            "point $-1 40 0 0 #",
            "point $-1 110 15.00000000000000178 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $29 257 #",
            "straight-curve $-1 40 30 0 -1 0 0 F 0 F 40 #",
            "point $-1 40 30 0 #",
            "point $-1 0 30 0 #",
        };

        internal static readonly string[] Island2004 =
        {
            "20800 69 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "asmheader $-1 -1 @13 226.0.0.65535 #",
            "body $-1 -1 $-1 $2 $-1 $-1 #",
            "lump $-1 -1 $-1 $3 $4 $1 #",
            "lump $-1 -1 $-1 $-1 $5 $1 #",
            "shell $-1 -1 $-1 $-1 $-1 $6 $-1 $2 #",
            "shell $-1 -1 $-1 $-1 $-1 $7 $-1 $3 #",
            "face $8 -1 $-1 $-1 $9 $4 $-1 $10 forward double out #",
            "face $11 -1 $-1 $-1 $12 $5 $-1 $13 forward double out #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $6 257 #",
            "loop $-1 -1 $-1 $14 $15 $6 #",
            "plane-surface $-1 -1 $-1 30 30 0 0 0 1 1 0 0 forward_v I I I I #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $7 257 #",
            "loop $-1 -1 $-1 $-1 $16 $7 #",
            "plane-surface $-1 -1 $-1 30 30 0 0 0 1 1 0 0 forward_v I I I I #",
            "loop $-1 -1 $-1 $-1 $17 $6 #",
            "coedge $-1 -1 $-1 $18 $19 $-1 $20 forward $9 $-1 #",
            "coedge $-1 -1 $-1 $16 $16 $-1 $21 forward $12 $-1 #",
            "coedge $-1 -1 $-1 $22 $23 $-1 $24 forward $14 $-1 #",
            "coedge $-1 -1 $-1 $25 $15 $-1 $26 forward $9 $-1 #",
            "coedge $-1 -1 $-1 $15 $25 $-1 $27 forward $9 $-1 #",
            "edge $28 -1 $-1 $29 -40 $30 -0 $15 $31 forward @7 unknown #",
            "edge $32 -1 $-1 $33 -3.141592653589793116 $33 3.141592653589793116 $16 $34 forward @7 unknown #",
            "coedge $-1 -1 $-1 $35 $17 $-1 $36 forward $14 $-1 #",
            "coedge $-1 -1 $-1 $17 $35 $-1 $37 forward $14 $-1 #",
            "edge $38 -1 $-1 $39 0 $40 60 $17 $41 forward @7 unknown #",
            "coedge $-1 -1 $-1 $19 $18 $-1 $42 forward $9 $-1 #",
            "edge $43 -1 $-1 $30 -40 $44 0 $18 $45 forward @7 unknown #",
            "edge $46 -1 $-1 $47 -40 $29 -0 $19 $48 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $20 257 #",
            "vertex $-1 -1 $-1 $20 $49 #",
            "vertex $-1 -1 $-1 $20 $50 #",
            "straight-curve $-1 -1 $-1 10 10 0 -1 -0 -0 F -40 F -0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $21 257 #",
            "vertex $-1 -1 $-1 $21 $51 #",
            "ellipse-curve $-1 -1 $-1 30 30 0 0 0 1 -8 -0 0 1 I I #",
            "coedge $-1 -1 $-1 $23 $22 $-1 $52 forward $14 $-1 #",
            "edge $53 -1 $-1 $40 0 $54 60 $22 $55 forward @7 unknown #",
            "edge $56 -1 $-1 $57 0 $39 60 $23 $58 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $24 257 #",
            "vertex $-1 -1 $-1 $24 $59 #",
            "vertex $-1 -1 $-1 $24 $60 #",
            "straight-curve $-1 -1 $-1 0 0 0 1 0 0 F 0 F 60 #",
            "edge $61 -1 $-1 $44 -40 $47 0 $25 $62 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $26 257 #",
            "vertex $-1 -1 $-1 $42 $63 #",
            "straight-curve $-1 -1 $-1 10 50 0 -0 1 -0 F -40 F -0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $27 257 #",
            "vertex $-1 -1 $-1 $27 $64 #",
            "straight-curve $-1 -1 $-1 50 10 0 -0 -1 -0 F -40 F -0 #",
            "point $-1 -1 $-1 50 10 0 #",
            "point $-1 -1 $-1 10 10 0 #",
            "point $-1 -1 $-1 38 30 0 #",
            "edge $65 -1 $-1 $54 0 $57 60 $35 $66 forward @7 unknown #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $36 257 #",
            "vertex $-1 -1 $-1 $36 $67 #",
            "straight-curve $-1 -1 $-1 60 0 0 0 1 0 F 0 F 60 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $37 257 #",
            "vertex $-1 -1 $-1 $52 $68 #",
            "straight-curve $-1 -1 $-1 0 60 0 0 -1 0 F 0 F 60 #",
            "point $-1 -1 $-1 0 0 0 #",
            "point $-1 -1 $-1 60 0 0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $42 257 #",
            "straight-curve $-1 -1 $-1 50 50 0 1 -0 -0 F -40 F -0 #",
            "point $-1 -1 $-1 10 50 0 #",
            "point $-1 -1 $-1 50 50 0 #",
            "color-adesk-attrib $-1 -1 $-1 $-1 $52 257 #",
            "straight-curve $-1 -1 $-1 60 60 0 -1 0 0 F 0 F 60 #",
            "point $-1 -1 $-1 60 60 0 #",
            "point $-1 -1 $-1 0 60 0 #",
        };

        internal const string NotchMirrored2018 =
            ""
            + "414349532042696E61727946696C652855000000000000020000000C00000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030393A35313A30392032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0C03000000110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C040000000C01000000110D097472616E73666F726D0CFFFFFF"
            + "FF04FFFFFFFF14000000000000F0BF0000000000000000075C143326A6A1BC140000000000000000000000000000F03F0000"
            + "00000000000014075C143326A6A13C0000000000000000000000000000F0BF1400000000000059400000000000000000CBCF"
            + "CFAF9B93FB3C06000000000000F03F0A0B0B110D057368656C6C0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0CFFFFFF"
            + "FF0C050000000CFFFFFFFF0C02000000110D04666163650C0600000004FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C070000000C04"
            + "0000000CFFFFFFFF0C080000000B0A0B110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0C"
            + "FFFFFFFF0CFFFFFFFF0C050000000401010000110D046C6F6F700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C090000"
            + "000C05000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000003440FFFFFF"
            + "FFFFFF2D4000000000000000001400000000000000000000000000000000000000000000F03F14000000000000F03F000000"
            + "000000000000000000000000000B0B0B0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0A0000000C0B00"
            + "00000CFFFFFFFF0C0C0000000B0C0700000004000000000CFFFFFFFF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFF"
            + "FFFF0C0D0000000C090000000CFFFFFFFF0C0E0000000B0C0700000004000000000CFFFFFFFF110D06636F656467650CFFFF"
            + "FFFF04FFFFFFFF0CFFFFFFFF0C090000000C0F0000000CFFFFFFFF0C100000000B0C0700000004000000000CFFFFFFFF110D"
            + "04656467650C1100000004FFFFFFFF0CFFFFFFFF0C1200000006182D4454FB21F9BF0C1300000006182D4454FB21F93F0C09"
            + "0000000C140000000B0707756E6B6E6F776E110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C150000000C0A00"
            + "00000CFFFFFFFF0C160000000B0C0700000004000000000CFFFFFFFF110D04656467650C1700000004FFFFFFFF0CFFFFFFFF"
            + "0C130000000600000000000039400C18000000060000000000003E400C0A0000000C190000000B0707756E6B6E6F776E110D"
            + "06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0B0000000C150000000CFFFFFFFF0C1A0000000B0C070000000400"
            + "0000000CFFFFFFFF110D04656467650C1B00000004FFFFFFFF0CFFFFFFFF0C1C0000000600000000000000000C1200000006"
            + "00000000000014400C0B0000000C190000000B0707756E6B6E6F776E110E05636F6C6F720E05616465736B0D066174747269"
            + "620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C0C0000000401010000110D067665727465780CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C0C00000004000000000C1D000000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0E00000004"
            + "000000000C1E000000110E07656C6C697073650D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000004440"
            + "0000000000002E4000000000000000001400000000000000800000000000000080000000000000F0BF1400000000000024C0"
            + "0000000000000080000000000000000006000000000000F03F0A06182D4454FB2109C00A06182D4454FB210940110D06636F"
            + "656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0F0000000C0D0000000CFFFFFFFF0C1F0000000B0C070000000400000000"
            + "0CFFFFFFFF110D04656467650C2000000004FFFFFFFF0CFFFFFFFF0C180000000600000000000000000C2100000006000000"
            + "00000044400C0D0000000C220000000B0707756E6B6E6F776E110E05636F6C6F720E05616465736B0D066174747269620CFF"
            + "FFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C0E0000000401010000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFF"
            + "FFFF0C0E00000004010000000C23000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF"
            + "13000000000000444000000000000000000000000000000000140000000000000000000000000000F03F0000000000000000"
            + "0A0600000000000000000A060000000000003E40110D04656467650C2400000004FFFFFFFF0CFFFFFFFF0C25000000060000"
            + "0000000000000C1C0000000600000000000044400C0F0000000C260000000B0707756E6B6E6F776E110E05636F6C6F720E05"
            + "616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C100000000401010000110D0676657274"
            + "65780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1A00000004010000000C27000000110D05706F696E740CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF13000000000000444000000000000014400000000000000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFF"
            + "FFFFFF13000000000000444000000000000039400000000000000000110D04656467650C2800000004FFFFFFFF0CFFFFFFFF"
            + "0C210000000600000000000000000C25000000060000000000003E400C150000000C290000000B0707756E6B6E6F776E110E"
            + "05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C1600000004010100"
            + "00110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1600000004010000000C2A000000110E0873747261696768"
            + "740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000044400000000000003E4000000000000000001400"
            + "0000000000F0BF000000000000000000000000000000000A0600000000000000000A060000000000004440110D05706F696E"
            + "740CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000044400000000000003E400000000000000000110E05636F6C6F720E"
            + "05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C1A0000000401010000110D06766572"
            + "7465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1A00000004000000000C2B000000110E0873747261696768740D0563757276"
            + "650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000000000000000000000000014000000000000F03F"
            + "000000000000000000000000000000000A0600000000000000000A060000000000004440110D05706F696E740CFFFFFFFF04"
            + "FFFFFFFF0CFFFFFFFF13000000000000444000000000000000000000000000000000110E05636F6C6F720E05616465736B0D"
            + "066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C1F0000000401010000110E0873747261696768740D05"
            + "63757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000003E4000000000000000001400000000"
            + "00000000000000000000F0BF00000000000000000A0600000000000000000A060000000000003E40110D05706F696E740CFF"
            + "FFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000003E400000000000000000110D05706F696E740CFFFFFF"
            + "FF04FFFFFFFF0CFFFFFFFF13000000000000000000000000000000000000000000000000110E03456E640E026F660E034153"
            + "4D0D0464617461";

        internal const string Disjoint2013 =
            ""
            + "414349532042696E61727946696C652855000000000000020000000C00000007104175746F6465736B204175746F43414407"
            + "1441534D203232362E302E302E3635353335204E540718467269204E6F762031332030393A34323A31392032303236066666"
            + "666666663940068DEDB5A0F7C6B03E06BBBDD7D9DF7CDB3D0D0961736D6865616465720CFFFFFFFF04FFFFFFFF070D323236"
            + "2E302E302E3635353335110D04626F64790CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C020000000CFFFFFFFF0CFFFFFFFF110D04"
            + "6C756D700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C030000000C040000000C01000000110D046C756D700CFFFFFFFF04FFFFFF"
            + "FF0CFFFFFFFF0CFFFFFFFF0C050000000C01000000110D057368656C6C0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C"
            + "FFFFFFFF0C060000000CFFFFFFFF0C02000000110D057368656C6C0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0CFFFF"
            + "FFFF0C070000000CFFFFFFFF0C03000000110D04666163650C0800000004FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C090000000C"
            + "040000000CFFFFFFFF0C0A0000000B0A0B110D04666163650C0B00000004FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C0C0000000C"
            + "050000000CFFFFFFFF0C0D0000000B0A0B110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0CFFFFFFFF0C060000000401010000110D046C6F6F700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C0E00"
            + "00000C06000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000003440FFFF"
            + "FFFFFFFF2D4000000000000000001400000000000000000000000000000000000000000000F03F14000000000000F03F0000"
            + "00000000000000000000000000000B0B0B0B0B110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFF"
            + "FFFF0CFFFFFFFF0CFFFFFFFF0C070000000401010000110D046C6F6F700CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C"
            + "0F0000000C07000000110E05706C616E650D07737572666163650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000005940"
            + "0000000000002E4000000000000000001400000000000000000000000000000000000000000000F03F14000000000000F03F"
            + "000000000000000000000000000000000B0B0B0B0B110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C10000000"
            + "0C110000000CFFFFFFFF0C120000000B0C0900000004000000000CFFFFFFFF110D06636F656467650CFFFFFFFF04FFFFFFFF"
            + "0CFFFFFFFF0C0F0000000C0F0000000CFFFFFFFF0C130000000B0C0C00000004000000000CFFFFFFFF110D06636F65646765"
            + "0CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C140000000C0E0000000CFFFFFFFF0C150000000B0C0900000004000000000CFFFFFF"
            + "FF110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C0E0000000C140000000CFFFFFFFF0C160000000B0C090000"
            + "0004000000000CFFFFFFFF110D04656467650C1700000004FFFFFFFF0CFFFFFFFF0C180000000600000000000000000C1900"
            + "00000600000000000044400C0E0000000C1A0000000B0707756E6B6E6F776E110D04656467650C1B00000004FFFFFFFF0CFF"
            + "FFFFFF0C1C00000006182D4454FB2109C00C1C00000006182D4454FB2109400C0F0000000C1D0000000B0707756E6B6E6F77"
            + "6E110D06636F656467650CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C110000000C100000000CFFFFFFFF0C1E0000000B0C090000"
            + "0004000000000CFFFFFFFF110D04656467650C1F00000004FFFFFFFF0CFFFFFFFF0C190000000600000000000000000C2000"
            + "0000060000000000003E400C100000000C210000000B0707756E6B6E6F776E110D04656467650C2200000004FFFFFFFF0CFF"
            + "FFFFFF0C230000000600000000000000000C18000000060000000000003E400C110000000C240000000B0707756E6B6E6F77"
            + "6E110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C1200000004"
            + "01010000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1200000004000000000C25000000110D0676657274"
            + "65780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1200000004010000000C26000000110E0873747261696768740D056375727665"
            + "0CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000000000000000000000000014000000000000F03F00"
            + "0000000000000000000000000000000A0600000000000000000A060000000000004440110E05636F6C6F720E05616465736B"
            + "0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C130000000401010000110D067665727465780CFFFF"
            + "FFFF04FFFFFFFF0CFFFFFFFF0C1300000004020000000C27000000110E07656C6C697073650D0563757276650CFFFFFFFF04"
            + "FFFFFFFF0CFFFFFFFF1300000000000059400000000000002E40000000000000000014000000000000000000000000000000"
            + "00000000000000F03F1400000000000024C00000000000000080000000000000000006000000000000F03F0B0B110D046564"
            + "67650C2800000004FFFFFFFF0CFFFFFFFF0C200000000600000000000000000C230000000600000000000044400C14000000"
            + "0C290000000B0707756E6B6E6F776E110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFF"
            + "FFFFFF0CFFFFFFFF0C150000000401010000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C15000000040100"
            + "00000C2A000000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF13000000000000444000"
            + "000000000000000000000000000000140000000000000000000000000000F03F00000000000000000A060000000000000000"
            + "0A060000000000003E40110E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFF"
            + "FFFFFF0C160000000401010000110D067665727465780CFFFFFFFF04FFFFFFFF0CFFFFFFFF0C1E00000004010000000C2B00"
            + "0000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000000000000000000000"
            + "3E400000000000000000140000000000000000000000000000F0BF00000000000000000A0600000000000000000A06000000"
            + "0000003E40110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF13000000000000000000000000000000000000000000"
            + "000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000004440000000000000000000000000000000"
            + "00110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000805B400100000000002E40000000000000000011"
            + "0E05636F6C6F720E05616465736B0D066174747269620CFFFFFFFF04FFFFFFFF0CFFFFFFFF0CFFFFFFFF0C1E000000040101"
            + "0000110E0873747261696768740D0563757276650CFFFFFFFF04FFFFFFFF0CFFFFFFFF130000000000004440000000000000"
            + "3E40000000000000000014000000000000F0BF000000000000000000000000000000000A0600000000000000000A06000000"
            + "0000004440110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000044400000000000003E400000000000"
            + "000000110D05706F696E740CFFFFFFFF04FFFFFFFF0CFFFFFFFF1300000000000000000000000000003E4000000000000000"
            + "00110E03456E640E026F660E0341534D0D0464617461";
        internal static readonly string[] EllipseInBox2000 =
        {
            "400 30 1 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
            "body $-1 $1 $-1 $-1 #",
            "lump $-1 $-1 $2 $0 #",
            "shell $-1 $-1 $-1 $3 $-1 $1 #",
            "face $4 $-1 $5 $2 $-1 $6 forward double out #",
            "color-adesk-attrib $-1 $-1 $-1 $3 257 #",
            "loop $-1 $-1 $7 $3 #",
            "plane-surface $-1 20 15 0 0 0 1 1 0 0 forward_v I I I I #",
            "coedge $-1 $8 $9 $-1 $10 reversed $5 $-1 #",
            "coedge $-1 $11 $7 $-1 $12 forward $5 $-1 #",
            "coedge $-1 $7 $11 $-1 $13 forward $5 $-1 #",
            "edge $14 $15 $16 $7 $17 forward #",
            "coedge $-1 $9 $8 $-1 $18 reversed $5 $-1 #",
            "edge $19 $15 $20 $8 $21 forward #",
            "edge $22 $23 $16 $9 $21 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $10 257 #",
            "vertex $-1 $12 $24 #",
            "vertex $-1 $10 $25 #",
            "straight-curve $-1 40 0 0 -0 -1 -0 F -30 F -0 #",
            "edge $26 $23 $20 $11 $27 forward #",
            "color-adesk-attrib $-1 $-1 $-1 $12 257 #",
            "vertex $-1 $18 $28 #",
            "ellipse-curve $-1 20 15 0 0 0 1 -30 -0 -0 0.3333333333333333148 I I #",
            "color-adesk-attrib $-1 $-1 $-1 $13 257 #",
            "vertex $-1 $13 $29 #",
            "point $-1 40 22.45355992499929698 0 #",
            "point $-1 40 7.546440075000701242 0 #",
            "color-adesk-attrib $-1 $-1 $-1 $18 257 #",
            "straight-curve $-1 0 30 0 -0 1 -0 F -30 F -0 #",
            "point $-1 0 22.45355992499929698 0 #",
            "point $-1 0 7.546440075000701242 0 #",
        };
    }
}
