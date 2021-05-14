using PlatformInvoke;
using ProcessEngine.Engines;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static PlatformInvoke.Kernel32;

namespace ProcessEngine
{
    public class PEngine : IPEngine
    {

        #region Environment variables

        private readonly IntPtr _processHandle;

        #endregion

        #region Properties

        public Process Process { get; }
        public AssemblyEngine Assembly { get; private set; }
        public MemoryEngine Memory { get; private set; }
        public ThreadsEngine Threads { get; private set; }
        public WindowEngine[] Windows { get; private set; }

        #endregion

        #region Constructors/Destructor

        private PEngine(Process process)
        {

            _processHandle = OpenProcess(ProcessAccessFlags.All, false, Process.Id);
            if (_processHandle == null)
                throw new Kernel32Exception("Impossible to open process.", Marshal.GetLastWin32Error());

            Process = process;

        }
        ~PEngine()
        {

            if (!CloseHandle(_processHandle))
                throw new Kernel32Exception("Impossible to close process handle.", Marshal.GetLastWin32Error());

        }

        private async Task<PEngine> InitializeAsync()
        {

            var getWindowsTask = WindowEngine.GetWindowsAsync(Process);
            Memory = new MemoryEngine(_processHandle);
            Threads = new ThreadsEngine(_processHandle);
            Assembly = new AssemblyEngine(_processHandle, Memory, Threads);

            Windows = await getWindowsTask;

            return this;

        }

        public static async Task<PEngine> BuildAsync(Process process)
        {

            return await new PEngine(process).InitializeAsync();

        }

        #endregion

    }
}
