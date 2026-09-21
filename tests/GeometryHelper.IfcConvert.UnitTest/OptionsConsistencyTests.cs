using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    public class OptionsConsistencyTests
    {
        // Every settable option gets a non-default value, so a property added later but forgotten in
        // Clone() or GetCacheKey() makes these tests fail.
        private static IfcConvertOptions NonDefaultOptions()
        {
            var options = new IfcConvertOptions().AddSkipNames("Skip*").AddOnlyNames("Only*");
            foreach (PropertyInfo property in typeof(IfcConvertOptions).GetProperties().Where(p => p.CanWrite))
            {
                property.SetValue(options, NonDefaultValue(property.PropertyType, property.GetValue(options)));
            }

            return options;
        }

        private static object NonDefaultValue(Type type, object current)
        {
            if (type == typeof(bool)) return !(bool)current;
            if (type == typeof(double)) return (double)current + 0.125;
            if (type.IsEnum) return Enum.GetValues(type).Cast<object>().First(v => !v.Equals(current));
            if (type == typeof(Tolerance)) return new Tolerance(2e-4, 3e-4, 0.02, 4e-4);
            throw new NotSupportedException("Add a non-default value for " + type.Name);
        }

        [Fact]
        public void Clone_CopiesEveryOption()
        {
            IfcConvertOptions original = NonDefaultOptions();
            IfcConvertOptions copy = original.Clone();

            foreach (PropertyInfo property in typeof(IfcConvertOptions).GetProperties().Where(p => p.CanWrite))
            {
                Assert.Equal(property.GetValue(original), property.GetValue(copy));
            }

            Assert.Equal(original.SkipNames.OrderBy(s => s), copy.SkipNames.OrderBy(s => s));
            Assert.NotSame(original.SkipNames, copy.SkipNames);
            Assert.Equal(original.OnlyNames.OrderBy(s => s), copy.OnlyNames.OrderBy(s => s));
            Assert.NotSame(original.OnlyNames, copy.OnlyNames);

            // The copies keep matching without regard to case, as the originals do.
            Assert.Contains("ONLY*", copy.OnlyNames);
            Assert.Contains("skip*", copy.SkipNames);
        }

        [Fact]
        public void Clone_KeepsTheNamesExactlyAsTheyAre()
        {
            // Names added to the sets directly are not trimmed; the copy must match what the original matches.
            var original = new IfcConvertOptions();
            original.SkipNames.Add("Bolt assembly ");
            original.OnlyNames.Add(" BEAM");

            IfcConvertOptions copy = original.Clone();

            Assert.Equal(new[] { "Bolt assembly " }, copy.SkipNames);
            Assert.Equal(new[] { " BEAM" }, copy.OnlyNames);
            Assert.Equal(original.GetCacheKey(), copy.GetCacheKey());
        }

        [Fact]
        public void OnlyTheParameterlessConstructor_IsPublic()
        {
            // Names are added through AddSkipNames / AddOnlyNames and the rest through properties, so that no
            // constructor argument can be read as the wrong list.
            ConstructorInfo constructor = Assert.Single(typeof(IfcConvertOptions).GetConstructors());
            Assert.Empty(constructor.GetParameters());
        }

        [Fact]
        public void AddSkipNamesAndAddOnlyNames_TrimIgnoreBlanksAndChain()
        {
            var options = new IfcConvertOptions();

            IfcConvertOptions returned = options
                .AddSkipNames(" Bolt*", "", null)
                .AddOnlyNames(new List<string> { "BEAM ", "  ", "PLATE" })
                .AddOnlyNames("GIRDER");

            Assert.Same(options, returned);
            Assert.Equal(new[] { "Bolt*" }, options.SkipNames);
            Assert.Equal(new[] { "BEAM", "GIRDER", "PLATE" }, options.OnlyNames.OrderBy(n => n));

            // The sets still match without regard to case.
            Assert.Contains("bolt*", options.SkipNames);
            Assert.Contains("beam", options.OnlyNames);
        }

        [Fact]
        public void AddNames_WithNothing_AddsNothing()
        {
            var options = new IfcConvertOptions()
                .AddSkipNames((string[])null)
                .AddSkipNames((IEnumerable<string>)null)
                .AddOnlyNames()
                .AddOnlyNames(new List<string>());

            Assert.Empty(options.SkipNames);
            Assert.Empty(options.OnlyNames);
            Assert.Equal(new IfcConvertOptions().GetCacheKey(), options.GetCacheKey());
        }

        [Fact]
        public void CacheKey_ChangesWithTheNameLists()
        {
            string defaultKey = new IfcConvertOptions().GetCacheKey();

            Assert.NotEqual(defaultKey, new IfcConvertOptions { SkipNames = { "Bolt assembly" } }.GetCacheKey());
            Assert.NotEqual(defaultKey, new IfcConvertOptions { OnlyNames = { "Bolt assembly" } }.GetCacheKey());
        }

        [Fact]
        public void Clone_IsPublic()
        {
            // GeometryHelper.TeklaConvert copies a caller's options before setting the unit and scale of each
            // reference model, so the copy has to be reachable from outside this assembly.
            MethodInfo clone = typeof(IfcConvertOptions).GetMethod(nameof(IfcConvertOptions.Clone), Type.EmptyTypes);

            Assert.NotNull(clone);
            Assert.True(clone.IsPublic);
        }

        [Fact]
        public void CacheKey_ChangesWithEveryGeometryOption()
        {
            string defaultKey = new IfcConvertOptions().GetCacheKey();

            // IncludeNonPhysicalProducts only filters model-wide queries; it never changes one product's geometry.
            foreach (PropertyInfo property in typeof(IfcConvertOptions).GetProperties()
                .Where(p => p.CanWrite && p.Name != nameof(IfcConvertOptions.IncludeNonPhysicalProducts)))
            {
                var options = new IfcConvertOptions();
                property.SetValue(options, NonDefaultValue(property.PropertyType, property.GetValue(options)));
                Assert.True(options.GetCacheKey() != defaultKey, property.Name + " is missing from the cache key");
            }
        }
    }
}
