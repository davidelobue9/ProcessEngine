﻿using PlatformInvoke;
using ProcessEngine.Engines;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static PlatformInvoke.Kernel32;

namespace ProcessEngine
{
    public class PEngine : IPEngine
    {
        private readonly IntPtr _processHandle;

        public Process Process { get; }
        public AssemblyEngine Assembly { get; private set; }
        public MemoryEngine Memory { get; private set; }
        public ThreadsEngine Threads { get; private set; }
        public WindowEngine[] Windows { get; private set; }

        private PEngine(Process process)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            _processHandle = OpenProcess(ProcessAccessFlags.All, false, process.Id);
            if (_processHandle.Equals(IntPtr.Zero))
            {
                throw new Kernel32Exception("Impossible to open process.", Marshal.GetLastWin32Error());
            }
        }

        ~PEngine()
        {
            if (!CloseHandle(_processHandle))
            {
                throw new Kernel32Exception("Impossible to close process handle.", Marshal.GetLastWin32Error());
            }
        }

        public static async Task<PEngine> BuildAsync(Process process)
        {
            return await new PEngine(process).InitializeAsync();
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
    }
}