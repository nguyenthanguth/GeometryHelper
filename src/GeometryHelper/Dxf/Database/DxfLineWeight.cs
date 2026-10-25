namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The weight a line is drawn with, as AutoCAD's <c>LineWeight</c> numbers it: in hundredths of a millimetre, group 370 of
    /// an entity or a layer, so <see cref="LineWeight025"/> is a line 0.25 mm wide; and the three that take it from elsewhere.
    /// </summary>
    public enum DxfLineWeight
    {
        /// <summary>The weight of the entity's layer, -1: what an entity that gives none has.</summary>
        ByLayer = -1,

        /// <summary>The weight of the INSERT that places the block the entity is in, -2.</summary>
        ByBlock = -2,

        /// <summary>The default weight of the drawing, -3: what a layer that gives none has.</summary>
        ByLineWeightDefault = -3,

        /// <summary>
        /// The weight in pixels of the device, -4, as AutoCAD names it; DXF writes no such weight, and the setters refuse it.
        /// </summary>
        ByDIPs = -4,

        /// <summary>0.00 mm, the thinnest line the device draws.</summary>
        LineWeight000 = 0,

        /// <summary>0.05 mm.</summary>
        LineWeight005 = 5,

        /// <summary>0.09 mm.</summary>
        LineWeight009 = 9,

        /// <summary>0.13 mm.</summary>
        LineWeight013 = 13,

        /// <summary>0.15 mm.</summary>
        LineWeight015 = 15,

        /// <summary>0.18 mm.</summary>
        LineWeight018 = 18,

        /// <summary>0.20 mm.</summary>
        LineWeight020 = 20,

        /// <summary>0.25 mm.</summary>
        LineWeight025 = 25,

        /// <summary>0.30 mm.</summary>
        LineWeight030 = 30,

        /// <summary>0.35 mm.</summary>
        LineWeight035 = 35,

        /// <summary>0.40 mm.</summary>
        LineWeight040 = 40,

        /// <summary>0.50 mm.</summary>
        LineWeight050 = 50,

        /// <summary>0.53 mm.</summary>
        LineWeight053 = 53,

        /// <summary>0.60 mm.</summary>
        LineWeight060 = 60,

        /// <summary>0.70 mm.</summary>
        LineWeight070 = 70,

        /// <summary>0.80 mm.</summary>
        LineWeight080 = 80,

        /// <summary>0.90 mm.</summary>
        LineWeight090 = 90,

        /// <summary>1.00 mm.</summary>
        LineWeight100 = 100,

        /// <summary>1.06 mm.</summary>
        LineWeight106 = 106,

        /// <summary>1.20 mm.</summary>
        LineWeight120 = 120,

        /// <summary>1.40 mm.</summary>
        LineWeight140 = 140,

        /// <summary>1.58 mm.</summary>
        LineWeight158 = 158,

        /// <summary>2.00 mm.</summary>
        LineWeight200 = 200,

        /// <summary>2.11 mm, the widest.</summary>
        LineWeight211 = 211,
    }
}
