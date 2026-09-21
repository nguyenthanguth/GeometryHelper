using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// The project's unit assignment (IfcProject.UnitsInContext), read once per model.
    /// <para>
    /// IFC values without an explicit unit are expressed in the project unit for their kind, and a project may
    /// mix scales: Tekla, for example, exports lengths in millimetres but areas in m2 and volumes in m3.
    /// </para>
    /// </summary>
    internal sealed class IfcUnits
    {
        private static readonly Dictionary<string, string> PrefixSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "EXA", "E" }, { "PETA", "P" }, { "TERA", "T" }, { "GIGA", "G" }, { "MEGA", "M" }, { "KILO", "k" },
            { "HECTO", "h" }, { "DECA", "da" }, { "DECI", "d" }, { "CENTI", "c" }, { "MILLI", "m" },
            { "MICRO", "µ" }, { "NANO", "n" }, { "PICO", "p" }, { "FEMTO", "f" }, { "ATTO", "a" }
        };

        private static readonly Dictionary<string, string> SiNameSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "METRE", "m" }, { "SQUARE_METRE", "m2" }, { "CUBIC_METRE", "m3" }, { "GRAM", "g" }, { "SECOND", "s" },
            { "KELVIN", "K" }, { "DEGREE_CELSIUS", "°C" }, { "PASCAL", "Pa" }, { "NEWTON", "N" }, { "RADIAN", "rad" },
            { "STERADIAN", "sr" }, { "JOULE", "J" }, { "WATT", "W" }, { "AMPERE", "A" }, { "VOLT", "V" },
            { "HERTZ", "Hz" }, { "MOLE", "mol" }, { "CANDELA", "cd" }, { "LUMEN", "lm" }, { "LUX", "lx" },
            { "OHM", "Ω" }, { "COULOMB", "C" }, { "FARAD", "F" }, { "SIEMENS", "S" }, { "WEBER", "Wb" },
            { "TESLA", "T" }, { "HENRY", "H" }, { "BECQUEREL", "Bq" }, { "GRAY", "Gy" }, { "SIEVERT", "Sv" }
        };

        private static readonly Dictionary<string, string> ConversionBasedSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FOOT", "ft" }, { "INCH", "in" }, { "YARD", "yd" }, { "MILE", "mi" },
            { "SQUARE FOOT", "ft2" }, { "SQUARE INCH", "in2" }, { "CUBIC FOOT", "ft3" }, { "CUBIC INCH", "in3" },
            { "POUND", "lb" }, { "DEGREE", "°" }
        };

        // Measure type -> IfcUnitEnum member of the project unit that applies when the value carries no unit.
        private static readonly Dictionary<string, string> MeasureUnitTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "IfcLengthMeasure", "LENGTHUNIT" }, { "IfcPositiveLengthMeasure", "LENGTHUNIT" },
            { "IfcNonNegativeLengthMeasure", "LENGTHUNIT" }, { "IfcAreaMeasure", "AREAUNIT" },
            { "IfcVolumeMeasure", "VOLUMEUNIT" }, { "IfcMassMeasure", "MASSUNIT" },
            { "IfcPlaneAngleMeasure", "PLANEANGLEUNIT" }, { "IfcPositivePlaneAngleMeasure", "PLANEANGLEUNIT" },
            { "IfcTimeMeasure", "TIMEUNIT" }, { "IfcThermodynamicTemperatureMeasure", "THERMODYNAMICTEMPERATUREUNIT" },
            { "IfcForceMeasure", "FORCEUNIT" }, { "IfcPressureMeasure", "PRESSUREUNIT" },
            { "IfcEnergyMeasure", "ENERGYUNIT" }, { "IfcPowerMeasure", "POWERUNIT" }
        };

        private readonly Dictionary<string, IIfcNamedUnit> _byType = new Dictionary<string, IIfcNamedUnit>(StringComparer.OrdinalIgnoreCase);
        private readonly double _lengthToMetres;

        public IfcUnits(IModel model)
        {
            _lengthToMetres = model?.ModelFactors?.LengthToMetresConversionFactor ?? 1.0;

            IIfcProject project = model?.Instances.FirstOrDefault<IIfcProject>();
            if (project?.UnitsInContext?.Units == null)
            {
                return;
            }

            foreach (IIfcNamedUnit unit in project.UnitsInContext.Units.OfType<IIfcNamedUnit>())
            {
                string type = unit.UnitType.ToString();
                if (!_byType.ContainsKey(type))
                {
                    _byType[type] = unit;
                }
            }
        }

        /// <summary>
        /// Gets the full name of the project length unit (e.g. "MILLIMETRE", "METRE", "FOOT").
        /// </summary>
        public string LengthUnitName
        {
            get
            {
                if (_byType.TryGetValue("LENGTHUNIT", out IIfcNamedUnit unit))
                {
                    if (unit is IIfcSIUnit si)
                    {
                        return (si.Prefix.HasValue ? si.Prefix.Value.ToString() : string.Empty) + si.Name;
                    }

                    if (unit is IIfcConversionBasedUnit conversion)
                    {
                        return conversion.Name.ToString().ToUpperInvariant();
                    }
                }

                return NameFromFactor(_lengthToMetres);
            }
        }

        /// <summary>
        /// Gets the symbol of the project unit for an IfcUnitEnum member (e.g. "LENGTHUNIT" -> "mm"), or empty.
        /// </summary>
        public string SymbolForUnitType(string unitType)
        {
            return unitType != null && _byType.TryGetValue(unitType, out IIfcNamedUnit unit) ? SymbolOf(unit) : string.Empty;
        }

        /// <summary>
        /// Gets the symbol of the project unit that applies to a measure type (e.g. "IfcLengthMeasure" -> "mm"), or empty.
        /// </summary>
        public string SymbolForMeasure(string measureTypeName)
        {
            return measureTypeName != null && MeasureUnitTypes.TryGetValue(measureTypeName, out string unitType)
                ? SymbolForUnitType(unitType)
                : string.Empty;
        }

        /// <summary>
        /// Gets the symbol of an explicit IFC unit (e.g. MILLI METRE -> "mm", KILO GRAM -> "kg", FOOT -> "ft").
        /// </summary>
        public static string SymbolOf(IIfcUnit unit)
        {
            switch (unit)
            {
                case IIfcSIUnit si:
                    string prefix = si.Prefix.HasValue && PrefixSymbols.TryGetValue(si.Prefix.Value.ToString(), out string p) ? p : string.Empty;
                    string name = si.Name.ToString();
                    return prefix + (SiNameSymbols.TryGetValue(name, out string symbol) ? symbol : name.ToLowerInvariant());

                case IIfcConversionBasedUnit conversion:
                    string conversionName = conversion.Name.ToString();
                    return ConversionBasedSymbols.TryGetValue(conversionName, out string c) ? c : conversionName;

                default:
                    return string.Empty;
            }
        }

        private static string NameFromFactor(double metres)
        {
            if (Math.Abs(metres - 1.0) < 1e-9) return "METRE";
            if (Math.Abs(metres - 0.001) < 1e-12) return "MILLIMETRE";
            if (Math.Abs(metres - 0.01) < 1e-11) return "CENTIMETRE";
            if (Math.Abs(metres - 0.1) < 1e-10) return "DECIMETRE";
            if (Math.Abs(metres - 1000.0) < 1e-6) return "KILOMETRE";
            if (Math.Abs(metres - 0.3048) < 1e-9) return "FOOT";
            if (Math.Abs(metres - 0.0254) < 1e-10) return "INCH";
            return "METRE";
        }
    }
}
