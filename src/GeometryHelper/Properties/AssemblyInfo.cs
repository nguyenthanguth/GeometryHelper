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
[assembly: Guid("b146f992-0591-4769-9e3f-a153ac877482")]

// The test project reaches the internals this library keeps out of its public surface: the sine Tolerance
// caches for its angular threshold, the planar engine under Offset and Boolean, and the mesh helpers under
// the solid operations. They are implementation detail rather than API that has to stay compatible, so
// they are tested from inside rather than promoted to public.
[assembly: InternalsVisibleTo("GeometryHelper.UnitTest")]

// The AutoCAD sample is the visual half of the same testing: it draws what the headless tests measure,
// including Merge2.JoinBackup, the plain reading of joining that the fast one is held against.
[assembly: InternalsVisibleTo("GeometryHelper.ArrangeAlgorithms.CadTest")]
