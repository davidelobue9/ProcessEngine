using ProcessEngine.Engines;
using System.Diagnostics;

namespace ProcessEngine
{
    public interface IPEngine
    {
        AssemblyEngine Assembly { get; }
        DetouringEngine Detouring { get; }
        MemoryEngine Memory { get; }
        Process Process { get; }
        ThreadsEngine Threads { get; }
        WindowEngine[] Windows { get; }
    }
}