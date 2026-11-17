using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The Table Cell Format dialog opened by the Format "..." button of the Modify Table Style dialog: the window made as its
    /// caller makes it, and every XAML file of the presenter checked for a Resources property written twice.
    /// </summary>
    public sealed class DataFormatWindowTests
    {
        [Fact]
        public void TheDataFormatWindow_IsMadeAsTheFormatButtonMakesIt_WithoutTheResourcesPropertySetTwice()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                var model = new DataFormatViewModel("Header", DxfDataType.Double, DxfUnitType.Unitless, string.Empty);

                var window = new DataFormatWindow(model);

                Assert.Same(model, window.DataContext);
                Assert.NotNull(window.FindResource("Show"));
                Assert.NotNull(window.FindResource("GhPanel"));
                window.Body.Measure(new Size(window.Width, window.Height));
                window.Body.Arrange(new Rect(0, 0, window.Width, window.Height));
                window.Body.UpdateLayout();
                Assert.True(window.Body.ActualWidth > 0);
                window.Close();
            });
        }

        [Fact]
        public void TheModifyTableStyleWindow_WiresTheFormatButtonToAnOpenableDataFormatWindow()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                var model = new MainViewModel();
                var document = CadDocument.From(CadTemplate.CreateMetric(), "format.dxf");
                model.OpenDocument(document);
                model.ShowInsertTable = dialog =>
                {
                    dialog.Ok();
                    return true;
                };
                model.Execute("TABLE");
                model.Execute("0,0");
                model.Escape();

                var modify = new TableStyleModifyViewModel(document, "Standard", false);
                DataFormatViewModel offered = null;
                modify.ShowDataFormat = format =>
                {
                    offered = format;
                    var window = new DataFormatWindow(format);
                    window.Close();
                    return false;
                };

                modify.CellStyle = "Header";
                modify.FormatCommand.Execute(null);

                Assert.NotNull(offered);
            });
        }

        [Fact]
        public void NoXamlFile_SetsAResourcesPropertyTwice_AsAResourceDictionarySourceBesideAnotherResource()
        {
            string folder = SourceFolder();
            var faults = new List<string>();

            foreach (string file in Directory.GetFiles(folder, "*.xaml", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                {
                    continue;
                }

                foreach (
                    XElement resources in XDocument
                        .Load(file)
                        .Descendants()
                        .Where(e => e.Name.LocalName.EndsWith(".Resources"))
                )
                {
                    List<XElement> children = resources.Elements().ToList();

                    if (children.Count > 1 && children.Any(c => c.Name.LocalName == "ResourceDictionary"))
                    {
                        faults.Add(Path.GetFileName(file) + " " + resources.Name.LocalName);
                    }
                }
            }

            Assert.Empty(faults);
        }

        [Fact]
        public void EveryWindowWhoseModelCanBeMadeFromADocument_LoadsItsXamlWithoutAParseError()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;
                var document = CadDocument.From(CadTemplate.CreateMetric(), "windows.dxf");
                var faults = new List<string>();
                int made = 0;

                foreach (
                    Type type in typeof(DataFormatWindow)
                        .Assembly.GetTypes()
                        .Where(t => typeof(Window).IsAssignableFrom(t) && !t.IsAbstract)
                        .OrderBy(t => t.Name, StringComparer.Ordinal)
                )
                {
                    foreach (ConstructorInfo constructor in type.GetConstructors())
                    {
                        object[] arguments = constructor
                            .GetParameters()
                            .Select(p => Make(p.ParameterType, document, 0))
                            .ToArray();

                        if (arguments.Any(a => a == null) && constructor.GetParameters().Any(p => !p.IsOptional))
                        {
                            continue;
                        }

                        try
                        {
                            var window = (Window)constructor.Invoke(arguments);
                            window.Close();
                            made++;
                        }
                        catch (TargetInvocationException ex) when (ex.InnerException is XamlParseException)
                        {
                            faults.Add(type.Name + ": " + ex.InnerException.Message);
                        }
                        catch (Exception)
                        {
                            // A dummy argument the window refuses is not a fault of its XAML.
                        }

                        break;
                    }
                }

                Assert.True(made >= 70, made.ToString());
                Assert.Empty(faults);
            });
        }

        /// <summary>An instance of a type for a constructor argument, made from the document where it asks for one, or null.</summary>
        private static object Make(Type type, CadDocument document, int depth)
        {
            if (type == typeof(CadDocument))
            {
                return document;
            }

            if (type == typeof(string))
            {
                return "Standard";
            }

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type.IsAbstract || type.IsInterface || typeof(Delegate).IsAssignableFrom(type) || depth > 1)
            {
                return null;
            }

            ConstructorInfo constructor = type.GetConstructors()
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                return null;
            }

            object[] arguments = constructor
                .GetParameters()
                .Select(p => Make(p.ParameterType, document, depth + 1))
                .ToArray();

            try
            {
                return constructor.Invoke(arguments);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string SourceFolder()
        {
            var bin = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            string folder = Path.Combine(
                bin.Parent.Parent.Parent.Parent.Parent.FullName,
                "src",
                "GeometryHelper.Cad.Wpf"
            );
            Assert.True(Directory.Exists(folder), folder);
            return folder;
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
