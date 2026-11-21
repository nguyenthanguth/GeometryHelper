using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Plugins.UnitTest
{
    /// <summary>
    /// What one emitted application class does: it writes "init NAME" and "term NAME" lines to the log file of its assembly,
    /// adds the tabs and the snap given, and may throw. A plugin of the tests is made of these, in an assembly of its own, so
    /// that the other tests, which read this whole assembly as a plugin, never make an application class by accident.
    /// </summary>
    internal sealed class EmittedClass
    {
        public EmittedClass(string name)
        {
            Name = name;
        }

        /// <summary>The class's name; the order of the classes is the ordinal order of these.</summary>
        public string Name { get; }

        /// <summary>The ids of the tabs Initialize adds, each titled with its id.</summary>
        public string[] Tabs { get; set; } = new string[0];

        /// <summary>The keyword of the snap Initialize adds, as given (a lower case one is made capitals by the snap); null for none.</summary>
        public string SnapKeyword { get; set; }

        /// <summary>The message of the exception Initialize throws after it has added and logged; null for none.</summary>
        public string InitializeThrows { get; set; }

        /// <summary>The message of the exception Terminate throws after it has logged; null for none.</summary>
        public string TerminateThrows { get; set; }

        /// <summary>The name of a command of the class (a method that yields no prompt); null for none.</summary>
        public string Command { get; set; }
    }

    /// <summary>Builds assemblies of application classes with Reflection.Emit: in memory, or saved as a .dll for the manager to load.</summary>
    internal static class EmittedPlugin
    {
        private static int _made;

        /// <summary>A name no other assembly of this process has: .NET Framework gives a second .dll of a name the first one.</summary>
        public static string UniqueName(string stem) =>
            "GhEmitted"
            + stem
            + (++_made).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + Guid.NewGuid().ToString("N").Substring(0, 6);

        /// <summary>The assembly in memory, with the plugin API it states (null for none), for <see cref="CadPlugin.FromAssemblies"/>.</summary>
        public static Assembly InMemory(string name, string api, string logFile, params EmittedClass[] classes) =>
            Build(name, api, null, logFile, null, null, classes);

        /// <summary>The assembly saved as <c>folder\name.dll</c>, which the manager loads; the path.</summary>
        public static string ToFile(
            string folder,
            string name,
            string api,
            string logFile,
            params EmittedClass[] classes
        )
        {
            Directory.CreateDirectory(folder);
            Build(name, api, folder, logFile, null, null, classes);
            return Path.Combine(folder, name + ".dll");
        }

        /// <summary>An assembly saved to a file that embeds one resource and has no class, for the icon tests.</summary>
        public static string WithResource(string folder, string name, string resourceName, byte[] bytes)
        {
            Directory.CreateDirectory(folder);
            Build(name, null, folder, null, resourceName, bytes, new EmittedClass[0]);
            return Path.Combine(folder, name + ".dll");
        }

        /// <summary>The lines a plugin's classes wrote to the log file, in order; none where it has not been written.</summary>
        public static string[] Lines(string logFile) =>
            File.Exists(logFile) ? File.ReadAllLines(logFile) : new string[0];

        private static Assembly Build(
            string name,
            string api,
            string folder,
            string logFile,
            string resourceName,
            byte[] resource,
            EmittedClass[] classes
        )
        {
            AssemblyBuilder assembly =
                folder == null
                    ? AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run)
                    : AppDomain.CurrentDomain.DefineDynamicAssembly(
                        new AssemblyName(name),
                        AssemblyBuilderAccess.RunAndSave,
                        folder
                    );

            if (api != null)
            {
                assembly.SetCustomAttribute(
                    new CustomAttributeBuilder(
                        typeof(CadPluginApiAttribute).GetConstructor(new[] { typeof(string) }),
                        new object[] { api }
                    )
                );
            }

            ModuleBuilder module =
                folder == null ? assembly.DefineDynamicModule(name) : assembly.DefineDynamicModule(name, name + ".dll");

            if (resourceName != null)
            {
                module.DefineManifestResource(resourceName, new MemoryStream(resource), ResourceAttributes.Public);
            }

            foreach (EmittedClass spec in classes)
            {
                Define(module, name, spec, logFile);
            }

            if (folder != null)
            {
                assembly.Save(name + ".dll");
            }

            return assembly;
        }

        private static void Define(ModuleBuilder module, string assemblyName, EmittedClass spec, string logFile)
        {
            TypeBuilder type = module.DefineType(
                assemblyName + "." + spec.Name,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                typeof(object),
                new[] { typeof(ICadPluginApplication) }
            );
            type.DefineDefaultConstructor(MethodAttributes.Public);

            // A finder that gives no point, for the snap: a static method the delegate is made over.
            MethodBuilder finder = type.DefineMethod(
                "Find",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(IEnumerable<GeoPoint2>),
                new[] { typeof(CadSnapRequest) }
            );
            ILGenerator none = finder.GetILGenerator();
            none.Emit(OpCodes.Ldnull);
            none.Emit(OpCodes.Ret);

            MethodAttributes virtualPublic =
                MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final;

            MethodBuilder initialize = type.DefineMethod(
                "Initialize",
                virtualPublic,
                typeof(void),
                new[] { typeof(CadPluginHost) }
            );
            ILGenerator il = initialize.GetILGenerator();
            Log(il, logFile, "init " + spec.Name);

            foreach (string tab in spec.Tabs)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, tab);
                il.Emit(OpCodes.Ldstr, tab);
                il.Emit(OpCodes.Newobj, typeof(CadRibbonTab).GetConstructor(new[] { typeof(string), typeof(string) }));
                il.Emit(OpCodes.Callvirt, typeof(CadPluginHost).GetMethod(nameof(CadPluginHost.AddRibbonTab)));
            }

            if (spec.SnapKeyword != null)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldstr, "Emitted snap");
                il.Emit(OpCodes.Ldstr, spec.SnapKeyword);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ldftn, finder);
                il.Emit(OpCodes.Newobj, typeof(CadSnapFinder).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));
                il.Emit(
                    OpCodes.Newobj,
                    typeof(CadCustomSnap).GetConstructor(
                        new[] { typeof(string), typeof(string), typeof(CadSnapFinder) }
                    )
                );
                il.Emit(OpCodes.Callvirt, typeof(CadPluginHost).GetMethod(nameof(CadPluginHost.AddObjectSnap)));
            }

            Finish(il, spec.InitializeThrows);

            MethodBuilder terminate = type.DefineMethod("Terminate", virtualPublic, typeof(void), Type.EmptyTypes);
            il = terminate.GetILGenerator();
            Log(il, logFile, "term " + spec.Name);
            Finish(il, spec.TerminateThrows);

            if (spec.Command != null)
            {
                MethodBuilder command = type.DefineMethod(
                    "Run",
                    MethodAttributes.Public,
                    typeof(IEnumerable<CadPrompt>),
                    new[] { typeof(CadCommandContext) }
                );
                command.SetCustomAttribute(
                    new CustomAttributeBuilder(
                        typeof(CadCommandAttribute).GetConstructor(new[] { typeof(string) }),
                        new object[] { spec.Command }
                    )
                );
                il = command.GetILGenerator();
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Newarr, typeof(CadPrompt));
                il.Emit(OpCodes.Ret);
            }

            type.CreateType();
        }

        private static void Log(ILGenerator il, string logFile, string line)
        {
            if (logFile == null)
            {
                return;
            }

            il.Emit(OpCodes.Ldstr, logFile);
            il.Emit(OpCodes.Ldstr, line + "\n");
            il.Emit(
                OpCodes.Call,
                typeof(File).GetMethod(nameof(File.AppendAllText), new[] { typeof(string), typeof(string) })
            );
        }

        private static void Finish(ILGenerator il, string throws)
        {
            if (throws == null)
            {
                il.Emit(OpCodes.Ret);
                return;
            }

            il.Emit(OpCodes.Ldstr, throws);
            il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
            il.Emit(OpCodes.Throw);
        }
    }

    /// <summary>The sample plugin that adds to the window, as the build left it beside the tests.</summary>
    internal static class UiSample
    {
        public const string Name = "GeometryHelper.Cad.SampleUiPlugin";

        public static string Built =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleUiPlugin", Name + ".dll");

        /// <summary>A copy of it in a folder of its own, as <see cref="PluginFiles.Sample"/> makes one of the other sample.</summary>
        public static string Copy(PluginFiles files, string folder = "dev-ui")
        {
            string dir = Path.Combine(files.Root, folder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, Name + ".dll");
            File.Copy(Built, path, true);
            return path;
        }
    }
}
