using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.IfcConvert.Models;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// Reads the property sets and quantity sets of a product into xBIM-free models.
    /// <para>
    /// Sets attached to the product's type (IfcRelDefinesByType) are read first and the occurrence's own sets
    /// are laid over them, so an occurrence value overrides the type value of the same name, as IFC specifies.
    /// Units come from the value itself when given, otherwise from the project unit for the measure.
    /// </para>
    /// </summary>
    internal sealed class PropertyReader
    {
        private readonly IfcUnits _units;

        public PropertyReader(IfcUnits units)
        {
            _units = units;
        }

        public IReadOnlyDictionary<string, IfcPropertySet> Read(IIfcProduct product)
        {
            Dictionary<string, List<IfcPropertyValue>> sets = new Dictionary<string, List<IfcPropertyValue>>(StringComparer.OrdinalIgnoreCase);

            if (product == null)
            {
                return new Dictionary<string, IfcPropertySet>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (IIfcRelDefinesByType typeRel in product.IsTypedBy ?? Enumerable.Empty<IIfcRelDefinesByType>())
            {
                foreach (IIfcPropertySetDefinition definition in typeRel?.RelatingType?.HasPropertySets ?? Enumerable.Empty<IIfcPropertySetDefinition>())
                {
                    AddDefinition(definition, sets);
                }
            }

            foreach (IIfcRelDefinesByProperties rel in product.IsDefinedBy.OfType<IIfcRelDefinesByProperties>())
            {
                foreach (IIfcPropertySetDefinition definition in Definitions(rel.RelatingPropertyDefinition))
                {
                    AddDefinition(definition, sets);
                }
            }

            Dictionary<string, IfcPropertySet> result = new Dictionary<string, IfcPropertySet>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<IfcPropertyValue>> set in sets)
            {
                result[set.Key] = new IfcPropertySet(set.Key, set.Value);
            }

            return result;
        }

        // IFC4 allows one relationship to carry a set of definitions (IfcPropertySetDefinitionSet).
        private static IEnumerable<IIfcPropertySetDefinition> Definitions(IIfcPropertySetDefinitionSelect select)
        {
            if (select is IIfcPropertySetDefinition single)
            {
                return new[] { single };
            }

            if (select is IEnumerable<IIfcPropertySetDefinition> many)
            {
                return many;
            }

            return Enumerable.Empty<IIfcPropertySetDefinition>();
        }

        private void AddDefinition(IIfcPropertySetDefinition definition, Dictionary<string, List<IfcPropertyValue>> sets)
        {
            string name = definition?.Name?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            IEnumerable<IfcPropertyValue> values;
            if (definition is IIfcPropertySet pset)
            {
                values = pset.HasProperties.SelectMany(p => ReadProperty(p, string.Empty));
            }
            else if (definition is IIfcElementQuantity qset)
            {
                values = qset.Quantities.Select(ReadQuantity).Where(v => v != null);
            }
            else
            {
                return;
            }

            if (!sets.TryGetValue(name, out List<IfcPropertyValue> list))
            {
                list = new List<IfcPropertyValue>();
                sets[name] = list;
            }

            // Later entries win in IfcPropertySet, so occurrence values appended after type values override them.
            list.AddRange(values);
        }

        private IEnumerable<IfcPropertyValue> ReadProperty(IIfcProperty property, string namePrefix)
        {
            if (property == null)
            {
                yield break;
            }

            string name = namePrefix + property.Name;

            switch (property)
            {
                case IIfcPropertySingleValue single:
                    if (single.NominalValue == null)
                    {
                        yield return new IfcPropertyValue(name, null, "IfcPropertySingleValue", string.Empty);
                        yield break;
                    }

                    string measure = single.NominalValue.GetType().Name;
                    yield return new IfcPropertyValue(name, single.NominalValue.Value, measure, UnitFor(single.Unit, measure));
                    yield break;

                case IIfcPropertyEnumeratedValue enumerated:
                    string enumValue = string.Join(", ", enumerated.EnumerationValues.Select(v => v?.Value?.ToString() ?? string.Empty));
                    yield return new IfcPropertyValue(name, enumValue, "IfcLabel", string.Empty);
                    yield break;

                case IIfcPropertyListValue list:
                    string listValue = string.Join(", ", list.ListValues.Select(v => v?.Value?.ToString() ?? string.Empty));
                    string listMeasure = list.ListValues.FirstOrDefault()?.GetType().Name;
                    yield return new IfcPropertyValue(name, listValue, "IfcPropertyListValue", UnitFor(list.Unit, listMeasure));
                    yield break;

                case IIfcPropertyBoundedValue bounded:
                    string lower = bounded.LowerBoundValue?.Value?.ToString() ?? string.Empty;
                    string upper = bounded.UpperBoundValue?.Value?.ToString() ?? string.Empty;
                    string boundMeasure = (bounded.LowerBoundValue ?? bounded.UpperBoundValue)?.GetType().Name;
                    yield return new IfcPropertyValue(name, lower + ".." + upper, "IfcPropertyBoundedValue", UnitFor(bounded.Unit, boundMeasure));
                    yield break;

                case IIfcComplexProperty complex:
                    // Flatten nested properties as "Complex.Child" so they stay addressable by name.
                    foreach (IIfcProperty child in complex.HasProperties)
                    {
                        foreach (IfcPropertyValue value in ReadProperty(child, name + "."))
                        {
                            yield return value;
                        }
                    }

                    yield break;

                default:
                    // Table and reference values have no single scalar value.
                    yield return new IfcPropertyValue(name, null, property.GetType().Name, string.Empty);
                    yield break;
            }
        }

        private IfcPropertyValue ReadQuantity(IIfcPhysicalQuantity quantity)
        {
            if (quantity == null)
            {
                return null;
            }

            string name = quantity.Name.ToString();
            IIfcNamedUnit own = (quantity as IIfcPhysicalSimpleQuantity)?.Unit;

            switch (quantity)
            {
                case IIfcQuantityLength length:
                    return new IfcPropertyValue(name, (double)length.LengthValue, "IfcQuantityLength", UnitFor(own, "LENGTHUNIT"));
                case IIfcQuantityArea area:
                    return new IfcPropertyValue(name, (double)area.AreaValue, "IfcQuantityArea", UnitFor(own, "AREAUNIT"));
                case IIfcQuantityVolume volume:
                    return new IfcPropertyValue(name, (double)volume.VolumeValue, "IfcQuantityVolume", UnitFor(own, "VOLUMEUNIT"));
                case IIfcQuantityWeight weight:
                    return new IfcPropertyValue(name, (double)weight.WeightValue, "IfcQuantityWeight", UnitFor(own, "MASSUNIT"));
                case IIfcQuantityTime time:
                    return new IfcPropertyValue(name, (double)time.TimeValue, "IfcQuantityTime", UnitFor(own, "TIMEUNIT"));
                case IIfcQuantityCount count:
                    return new IfcPropertyValue(name, (double)count.CountValue, "IfcQuantityCount", string.Empty);
                default:
                    return new IfcPropertyValue(name, null, quantity.GetType().Name, string.Empty);
            }
        }

        // An explicit unit wins; otherwise the project unit for the measure type or unit type applies.
        private string UnitFor(IIfcUnit explicitUnit, string measureOrUnitType)
        {
            if (explicitUnit != null)
            {
                return IfcUnits.SymbolOf(explicitUnit);
            }

            if (string.IsNullOrEmpty(measureOrUnitType))
            {
                return string.Empty;
            }

            return measureOrUnitType.EndsWith("UNIT", StringComparison.Ordinal)
                ? _units.SymbolForUnitType(measureOrUnitType)
                : _units.SymbolForMeasure(measureOrUnitType);
        }
    }
}
