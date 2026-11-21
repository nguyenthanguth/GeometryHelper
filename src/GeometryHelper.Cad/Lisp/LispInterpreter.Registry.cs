using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private readonly LispRegistryStore _registryMemory = new LispRegistryStore();

        /// <summary>
        /// Gets or sets the file <c>vl-registry-read</c>, <c>vl-registry-write</c>, <c>vl-registry-delete</c> and
        /// <c>vl-registry-descendents</c> keep their keys and values in: the presenter keeps a store of its own rather than writing
        /// the Windows registry. Interpreters given the same file share the values. Null, the default, keeps them in memory for
        /// this interpreter alone.
        /// </summary>
        public string RegistryFile { get; set; }

        private void RegisterRegistry()
        {
            Define("VL-REGISTRY-READ", 1, 2, a =>
            {
                string path = RegistryKey(a[0]);
                string name = a.Length > 1 ? RegistryName(a[1]) : "";
                return WithRegistry(false, store => store.Read(path, name));
            });
            Define("VL-REGISTRY-WRITE", 2, 3, a =>
            {
                string key = NameOfKeyArgument(a[0]);
                string name = a.Length > 2 ? RegistryName(a[1]) : "";
                object value = a[a.Length - 1];
                string path = RegistryPath(key);

                if (!(value is string || value is int))
                {
                    throw new LispException("bad argument value: unsupported registry value: " + Print(name, true));
                }

                return WithRegistry(true, store =>
                {
                    store.Write(path, name, value);
                    return value;
                });
            });
            Define("VL-REGISTRY-DELETE", 1, 2, a =>
            {
                string path = RegistryKey(a[0]);
                string name = a.Length > 1 && a[1] != null ? RegistryName(a[1]) : null;
                return WithRegistry(true, store =>
                {
                    if (name != null)
                    {
                        store.DeleteValue(path, name);
                        return _true;
                    }

                    return Bool(store.DeleteKey(path));
                });
            });
            Define("VL-REGISTRY-DESCENDENTS", 1, 2, a =>
            {
                string path = RegistryKey(a[0]);
                bool values = a.Length > 1 && a[1] != null;
                return WithRegistry(false, store =>
                {
                    List<string> names = values ? store.ValueNames(path) : store.SubKeys(path);
                    return ListOf(names.ConvertAll(n => (object)n));
                });
            });
        }

        /// <summary>
        /// Runs work on the store: this interpreter's own in memory, or the file's, read fresh and written back after a change
        /// under the one lock of the process. A file that cannot be written makes the function give nil.
        /// </summary>
        private object WithRegistry(bool changes, Func<LispRegistryStore, object> work)
        {
            string file = RegistryFile;

            if (file == null)
            {
                return work(_registryMemory);
            }

            lock (LispRegistryStore.Lock)
            {
                LispRegistryStore store = LispRegistryStore.Load(file);
                object result = work(store);

                if (changes && store.Changed)
                {
                    try
                    {
                        store.Save(file);
                    }
                    catch (Exception exception)
                        when (exception is IOException || exception is UnauthorizedAccessException)
                    {
                        return null;
                    }
                }

                return result;
            }
        }

        /// <summary>Takes a key or a value name an argument gives: a string, or a symbol for its name.</summary>
        private static string NameOfKeyArgument(object value)
        {
            if (value is string text)
            {
                return text;
            }

            return value is LispSymbol symbol
                ? symbol.Name
                : throw TypeError("(or stringp symbolp)", value, colon: true);
        }

        private static string RegistryName(object value) => value == null ? "" : NameOfKeyArgument(value);

        private static string RegistryKey(object value) => RegistryPath(NameOfKeyArgument(value));

        private static string RegistryPath(string key) =>
            LispRegistryStore.TryPath(key, out string path)
                ? path
                : throw new LispException("bad registry key name: " + Print(key, true));
    }
}
