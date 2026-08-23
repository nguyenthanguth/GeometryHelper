using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("CommonGeometry")]
[assembly: AssemblyDescription("Types shared by PlaneGeometry and SolidGeometry: tolerance, angle, and the enumerations that describe where a point sits.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Nguyen Thang")]
[assembly: AssemblyProduct("CommonGeometry")]
[assembly: AssemblyCopyright("Copyright © Nguyen Thang 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6b1d9e04-9f8a-4c1e-93d5-2f7a0c58b311")]

// Tolerance.EqualAngleSin is the sine of EqualAngleRad, precomputed once so that parallelism and
// intersection tests can compare against it without calling Math.Sin on every comparison. It is
// derived data rather than a threshold the caller sets, so it stays internal instead of becoming
// public API that has to be kept compatible forever. The two geometry libraries built on this one
// need to read it, which is what these declarations grant.
[assembly: InternalsVisibleTo("PlaneGeometry")]
[assembly: InternalsVisibleTo("SolidGeometry")]
[assembly: InternalsVisibleTo("CommonGeometry.UnitTest")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]
