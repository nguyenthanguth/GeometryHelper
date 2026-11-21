using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using GeometryHelper.Fixture.Other;

namespace GeometryHelper.Fixture
{
    /// <summary>One of every kind of member.</summary>
    public class Members
    {
        public Members() { }

        /// <summary>Makes it with a count.</summary>
        public Members(int count) { }

        public Members(int count, string name) { }

        public int Count { get; set; }
        public string Label { get; }
        public static int Total { get; set; }
        public static string Version { get; }
        public double Sink
        {
            set { }
        }
        public int Field;
        public const int Limit = 5;
        public static readonly string Tag = "t";
        public int True;

        public int this[int index]
        {
            get => 0;
            set { }
        }

        public string this[string key] => key;

        public event EventHandler Changed;
        public event EventHandler<DataArgs> DataChanged;

        public int Twice(int x) => x * 2;

        public static int Twice(int x, int y) => x * y;

        public void Nothing() { }

        public bool TryFind(string key, out int index)
        {
            index = 0;
            return true;
        }

        public void Swap(ref int a, ref int b) { }

        public bool TryBoth(out int a, out string b)
        {
            a = 0;
            b = "";
            return true;
        }

        public bool TryScale(string key, out int index, double tolerance)
        {
            index = 0;
            return true;
        }

        public bool TryScale(string key, out int index, double tolerance, out string note)
        {
            index = 0;
            note = "";
            return true;
        }

        public bool TryDivide(int number, out int quotient, out int remainder, double tolerance, double limit)
        {
            quotient = 0;
            remainder = 0;
            return true;
        }

        public void Fill(int count = 3, string name = "n") { }

        public int Sum(params int[] values) => values.Length;

        public void Range(int from, int with, int other) { }

        public static T Pick<T>(T a, T b) => a;

        public Gadget Another(Gadget gadget, Other.Shape shape, Shape own) => gadget;

        [CompilerGenerated]
        public void Hidden() { }
    }

    /// <summary>One method for each type of the table.</summary>
    public class Table
    {
        public object Scalars(
            bool a,
            byte b,
            sbyte c,
            short d,
            ushort e,
            int f,
            uint g,
            long h,
            ulong i,
            float j,
            double k,
            decimal l,
            string m,
            char n,
            object o
        ) => o;

        public int? Maybe(int? a) => a;

        public double[] Doubles(double[] a) => a;

        public int[][] Jagged(int[][] a) => a;

        public double[,] Grid() => null;

        public IEnumerable<Spot> Spots(IEnumerable<Spot> spots) => spots;

        public IReadOnlyList<Spot> Listed(IReadOnlyList<Spot> spots) => spots;

        public List<int> Lists(List<int> values) => values;

        public IDictionary<string, int> Dictionaries(Dictionary<string, int> map) => map;

        public KeyValuePair<string, int> Pair(KeyValuePair<string, int> pair) => pair;

        public void Callbacks(Action a, Action<int> b, Func<int, string> c) { }

        public Action<int> Callback() => null;

        public Stream Open(DateTime when, TimeSpan span) => null;

        public IEnumerator<int> Walk() => null;

        public Guid Unknown(Guid id) => id;

        public EventArgs Args() => null;

        public IDisposable Disposable() => null;
    }

    /// <summary>A partial class, summarized twice.</summary>
    public partial class Parted { }

    /// <summary>The second part.</summary>
    public partial class Parted { }

    /// <summary>The words of the documentation comments.</summary>
    public class Documented
    {
        /// <summary>Makes a <see cref="Spot"/> from <paramref name="x"/>, or <see langword="null"/>; <c>nothing</c> else.</summary>
        public Spot Make(double x) => new Spot(x, x);

        /// <summary>
        /// First paragraph.
        /// <para>Second "quoted" paragraph, with a back\slash and """ three quotes.</para>
        /// </summary>
        public void Paragraphs() { }

        public void NoSummary() { }

        /// <summary>See <see cref="M:GeometryHelper.Fixture.Spot.Scale(System.Double)"/>, <see cref="P:GeometryHelper.Fixture.Members.Count"/>, <see cref="T:System.Collections.Generic.List{System.Int32}"/> and <see cref="M:GeometryHelper.Fixture.Members.#ctor(System.Int32)"/>.</summary>
        public void Crefs() { }

        /// <summary>This summary is longer than one line of a stub can hold, so it is wrapped at the width of the stubs, which is a hundred and sixteen columns counting the indent of the member it belongs to.</summary>
        public void Long() { }

        /// <summary>Ends with a quote "</summary>
        public void EndsWithQuote() { }
    }
}

namespace GeometryHelper.Fixture.Other
{
    /// <summary>A gadget of another namespace.</summary>
    public class Gadget
    {
        /// <summary>Gets the spot the gadget stands on, a type of the first namespace.</summary>
        public Spot Position { get; set; }
    }

    /// <summary>A shape of another namespace that has the name of a shape of the first.</summary>
    public class Shape { }
}
