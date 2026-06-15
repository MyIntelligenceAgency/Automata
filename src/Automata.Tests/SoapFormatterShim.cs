// Shim: System.Runtime.Serialization.Formatters.Soap.SoapFormatter was a .NET Framework-only
// type (never ported to .NET Core/5+). Several legacy serialization tests in this project use it
// for automaton round-trip coverage. Those tests are OUT OF SCOPE for #2979 (the surface-operator
// &/~ parser patch touches regex/automata conversion, not serialization). This shim lets the test
// project compile against net8.0; the Soap-using tests throw NotImplementedException at runtime
// (rather than being surgically #if-guarded across ~24 call sites in 4 files).
namespace System.Runtime.Serialization.Formatters.Soap
{
    internal sealed class SoapFormatter : System.Runtime.Serialization.IFormatter
    {
        public System.Runtime.Serialization.SerializationBinder Binder
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public System.Runtime.Serialization.StreamingContext Context
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public System.Runtime.Serialization.ISurrogateSelector SurrogateSelector
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object Deserialize(System.IO.Stream serializationStream) => throw new NotImplementedException();
        public void Serialize(System.IO.Stream serializationStream, object graph) => throw new NotImplementedException();
    }
}

// Shim: System.Web.UI.ObjectStateFormatter (ASP.NET viewstate formatter) is also Framework-only.
// Same rationale as SoapFormatter above — used by the SerializationTests *_osf helpers.
namespace System.Web.UI
{
    internal sealed class ObjectStateFormatter : System.Runtime.Serialization.IFormatter
    {
        public System.Runtime.Serialization.SerializationBinder Binder
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public System.Runtime.Serialization.StreamingContext Context
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public System.Runtime.Serialization.ISurrogateSelector SurrogateSelector
        { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object Deserialize(System.IO.Stream serializationStream) => throw new NotImplementedException();
        public void Serialize(System.IO.Stream serializationStream, object graph) => throw new NotImplementedException();
    }
}
