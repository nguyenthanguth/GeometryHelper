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
[assembly: Guid("a0058f32-efe7-4459-aebe-065f45a24b58")]
