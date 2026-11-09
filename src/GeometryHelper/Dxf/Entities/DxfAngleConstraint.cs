namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The angle a segment of a leader of a multileader style is kept to, as AutoCAD's <c>AngleConstraint</c> numbers it: a
    /// step of 15 degrees, which the style holds in radians (groups 40 and 41 of an MLEADERSTYLE, 0.785398 for 45 degrees);
    /// <see cref="DegreesAny"/> where it gives none.
    /// </summary>
    public enum DxfAngleConstraint
    {
        /// <summary>Any angle, 0.</summary>
        DegreesAny = 0,

        /// <summary>A multiple of 15 degrees, 1.</summary>
        Degrees015 = 1,

        /// <summary>A multiple of 30 degrees, 2.</summary>
        Degrees030 = 2,

        /// <summary>A multiple of 45 degrees, 3.</summary>
        Degrees045 = 3,

        /// <summary>A multiple of 60 degrees, 4.</summary>
        Degrees060 = 4,

        /// <summary>A multiple of 90 degrees, 6.</summary>
        Degrees090 = 6,

        /// <summary>Level, 12.</summary>
        DegreesHorz = 12,
    }
}
