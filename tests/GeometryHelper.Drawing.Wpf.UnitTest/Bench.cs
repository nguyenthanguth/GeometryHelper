using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>
    /// A presenter with the model drawing added, over the test bodies, the changed test bodies and the two sources not
    /// connected, with every dialog it asks for kept to be looked at; and helpers to make a drawing and find its entities.
    /// </summary>
    internal sealed class Bench
    {
        public static readonly Tolerance Tol = Tolerance.Default;

        public Bench(params IDrawingSourceProvider[] more)
        {
            Model = new MainViewModel();
            Original = new TestBodiesSourceProvider(Tol);
            Changed = new TestBodiesSourceProvider(Tol, changed: true);
            var sources = new List<IDrawingSourceProvider> { Original, Changed, UnconnectedSourceProvider.TeklaStructures, UnconnectedSourceProvider.IfcFile };
            sources.AddRange(more);
            Workspace = new DrawingWorkspace(Model, sources, Tol);
            Workspace.DialogRequested += (s, e) => Requests.Add(e);
        }

        public MainViewModel Model { get; }

        public DrawingWorkspace Workspace { get; }

        public TestBodiesSourceProvider Original { get; }

        public TestBodiesSourceProvider Changed { get; }

        public List<DrawingDialogRequestEventArgs> Requests { get; } = new List<DrawingDialogRequestEventArgs>();

        public IEnumerable<MessageViewModel> Messages => Requests.Select(r => r.Model).OfType<MessageViewModel>();

        /// <summary>Makes a drawing through the New drawing dialog: the first source, the preset's objects, Create.</summary>
        public async Task<ModelDrawing> MakeAsync(string preset = "Assembly", Action<NewDrawingViewModel> beforeCreate = null)
        {
            NewDrawingViewModel dialog = Workspace.ShowNewDrawing();
            await dialog.NextAsync();
            dialog.Preset = preset;
            await dialog.NextAsync();
            beforeCreate?.Invoke(dialog);
            ModelDrawing made = await dialog.NextAsync();
            Assert.NotNull(made);
            return made;
        }

        /// <summary>The handle of the first entity of a drawing that draws an element of a kind: View, Dimension, Mark.</summary>
        public static string FirstOf(ModelDrawing drawing, string kind)
        {
            var kinds = drawing.Drawing.Elements.ToDictionary(e => e.Id, e => e.Kind, StringComparer.Ordinal);

            foreach (DxfEntity entity in DrawingEntityTags.Find(drawing.Document.Database))
            {
                if (!entity.IsErased && DrawingEntityTags.TryRead(entity, out string id, out _) && kinds.TryGetValue(id, out string found) && found == kind)
                {
                    return entity.Handle;
                }
            }

            return null;
        }

        /// <summary>The handle of the first projected line of an object of the model.</summary>
        public static string LineOf(ModelDrawing drawing, string objectId)
        {
            foreach (DxfEntity entity in DrawingEntityTags.Find(drawing.Document.Database))
            {
                if (!entity.IsErased && entity is DxfLine && DrawingEntityTags.TryRead(entity, out string id, out _) && id == objectId)
                {
                    return entity.Handle;
                }
            }

            return null;
        }

        /// <summary>The id of the element an entity draws.</summary>
        public static string IdOf(ModelDrawing drawing, string handle)
        {
            DrawingEntityTags.TryRead(drawing.Document.FindEntity(handle), out string id, out _);
            return id;
        }

        /// <summary>A folder of its own under %TEMP%\gk\drawing\wpf-b-tests, deleted when the test ends.</summary>
        public static string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gk", "drawing", "wpf-b-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static void Delete(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>A source whose objects come only when the test lets them, or never, with an error.</summary>
    internal sealed class SlowSource : IDrawingSourceProvider
    {
        private readonly System.Threading.ManualResetEventSlim _go = new System.Threading.ManualResetEventSlim(false);
        private readonly bool _fails;

        public SlowSource(bool fails = false)
        {
            _fails = fails;
        }

        public string Name => _fails ? "Broken source" : "Slow source";

        public string Description => "A source for the tests";

        public bool IsAvailable => true;

        public string AvailabilityText => "Built in";

        public IReadOnlyList<DrawingObject> CreateObjects()
        {
            if (_fails)
            {
                throw new InvalidOperationException("The model is locked.");
            }

            _go.Wait(TimeSpan.FromSeconds(30));
            return new TestBodiesSourceProvider(Bench.Tol).CreateObjects();
        }

        public void Go() => _go.Set();
    }
}
