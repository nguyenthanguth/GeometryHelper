using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Fixture
{
    /// <summary>A session that is closed with Dispose and can be walked through.</summary>
    public class Session : IDisposable, IEnumerable<Spot>
    {
        public void Dispose() { }

        public IEnumerator<Spot> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A session with a time limit, which is disposable through its base class.</summary>
    public class TimedSession : Session { }

    /// <summary>Takes and gives a System.Type in each place a type can stand.</summary>
    public class TypeBox
    {
        public Type Kind { get; set; }

        public void Accept(Type kind) { }

        public Type Current() => null;

        public void AcceptAll(IEnumerable<Type> kinds) { }
    }
}
