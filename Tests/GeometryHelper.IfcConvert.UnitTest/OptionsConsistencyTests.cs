using System;
using System.Linq;
using System.Reflection;
using GeometryHelper.CommonGeometry;
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
            var options = new IfcConvertOptions(new[] { "Skip*" });
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
