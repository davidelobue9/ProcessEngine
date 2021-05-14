using PlatformInvoke;
using System;
using System.Runtime.InteropServices;
using static PlatformInvoke.Kernel32;

namespace ProcessEngine.Engines
{
    public class ThreadsEngine
    {

        #region Environment variables

        private readonly IntPtr processHandle;

        #endregion 

        #region Properties
        #endregion

        #region Constructor/Destructor

        internal ThreadsEngine(IntPtr processHandle)
        {
            this.processHandle = processHandle;
        }
        ~ThreadsEngine()
        {
        }

        #endregion

        #region Methods
        public IntPtr Create(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags)
        {

            IntPtr lpThreadId = IntPtr.Zero;

            IntPtr returnValue = CreateRemoteThread(
                processHandle,
                lpThreadAttributes, //new IntPtr(),
                dwStackSize,
                lpStartAddress,
                lpParameter,
                dwCreationFlags,
                out lpThreadId
            );

            if (returnValue == null)
                throw new Kernel32Exception("Impossible to create thread.", Marshal.GetLastWin32Error());

            return returnValue;

        }

        public void CreateAndExecute(IntPtr lpStartAddress)
        {

            IntPtr hThread = Create(
                IntPtr.Zero,
                0,
                lpStartAddress,
                IntPtr.Zero,
                0
            );

            WaitForThreadToExit(hThread);

            if(!CloseHandle(hThread))
                throw new Kernel32Exception("Impossible to close thread.", Marshal.GetLastWin32Error());

        }

        static uint WaitForThreadToExit(IntPtr hThread)
        {

            uint waitForSingleObject = WaitForSingleObject(hThread, 0xAFAF);

            if (waitForSingleObject == (uint)WaitForSingleObjectFlags.Infinite)
                throw new Kernel32Exception("Operation time out.", Marshal.GetLastWin32Error());

            if (!GetExitCodeThread(hThread, out uint exitCode))
                throw new Kernel32Exception("Impossible to get the exit code of the thread.", Marshal.GetLastWin32Error());

            return exitCode;

        }
        #endregion

    }
}
