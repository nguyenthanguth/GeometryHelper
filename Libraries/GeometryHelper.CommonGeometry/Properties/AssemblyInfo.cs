using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The SDK generates the assembly identity attributes — title, description, company, product, copyright
// and the three version attributes — from the MSBuild properties in the project file and in
// Directory.Build.props at the root of the repository. Declaring them here as well would be a second
// copy to keep in step, and the compiler would reject the duplicates outright.
//
// What is left is what the SDK does not generate.

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6b1d9e04-9f8a-4c1e-93d5-2f7a0c58b311")]

// Tolerance caches the sine of its angular threshold so that the parallelism and intersection tests can
// compare against it without calling Math.Sin on every comparison. It is derived data rather than a
// threshold the caller sets, so it stays internal instead of becoming public API that has to be kept
// compatible forever. The two geometry libraries built on this one need to read it, which is what these
// declarations grant.
[assembly: InternalsVisibleTo("GeometryHelper.PlaneGeometry")]
[assembly: InternalsVisibleTo("GeometryHelper.SolidGeometry")]
[assembly: InternalsVisibleTo("GeometryHelper.CommonGeometry.UnitTest")]
