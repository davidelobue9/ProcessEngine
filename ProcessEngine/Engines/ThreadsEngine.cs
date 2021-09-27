using PlatformInvoke;
using System;
using System.Runtime.InteropServices;
using static PlatformInvoke.Kernel32;

namespace ProcessEngine.Engines
{
    public class ThreadsEngine
    {
        private readonly IntPtr processHandle;

        internal ThreadsEngine(IntPtr processHandle)
        {
            this.processHandle = processHandle;
        }

        public IntPtr Create(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags)
        {
            if (lpStartAddress.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(lpStartAddress)} argument is Zero");
            }

            IntPtr returnValue = CreateRemoteThread(
                processHandle,
                lpThreadAttributes, //new IntPtr(),
                dwStackSize,
                lpStartAddress,
                lpParameter,
                dwCreationFlags,
                out _
            );

            return returnValue.Equals(IntPtr.Zero)
                ? throw new Kernel32Exception("Impossible to create thread.", Marshal.GetLastWin32Error())
                : returnValue;
        }

        public void CreateAndExecute(IntPtr lpStartAddress)
        {
            if (lpStartAddress.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(lpStartAddress)} argument is Zero");
            }

            IntPtr hThread = Create(
                IntPtr.Zero,
                0,
                lpStartAddress,
                IntPtr.Zero,
                0
            );

            WaitForThreadToExit(hThread);

            if (!CloseHandle(hThread))
            {
                throw new Kernel32Exception("Impossible to close thread.", Marshal.GetLastWin32Error());
            }
        }

        private static uint WaitForThreadToExit(IntPtr hThread)
        {
            if (hThread.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(hThread)} argument is Zero");
            }

            uint waitForSingleObject = WaitForSingleObject(hThread, 0xAFAF);

            if (waitForSingleObject == (uint)WaitForSingleObjectFlags.Infinite)
            {
                throw new Kernel32Exception("Operation time out.", Marshal.GetLastWin32Error());
            }

            return !GetExitCodeThread(hThread, out uint exitCode)
                ? throw new Kernel32Exception("Impossible to get the exit code of the thread.", Marshal.GetLastWin32Error())
                : exitCode;
        }
    }
}