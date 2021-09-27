using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static PlatformInvoke.User32;

namespace ProcessEngine.Engines
{
    public class WindowEngine
    {
        public IntPtr Handle { get; }

        internal WindowEngine(IntPtr handleWindow)
        {
            Handle = handleWindow;
        }

        public string GetTitle()
        {
            string title = "";
            IntPtr titleTmp;
            int length;

            length = SendMessageA(Handle, WindowMessages.GetTextLenght, IntPtr.Zero, IntPtr.Zero) + 1;
            titleTmp = Marshal.AllocHGlobal(length);
            SendMessageA(Handle, WindowMessages.GetText, new IntPtr(length), titleTmp);

            for (int i = 0; i < length - 1; i++)
            {
                title += (char)Marshal.ReadByte(titleTmp, i);
            }

            Marshal.FreeHGlobal(titleTmp);

            return title;
        }

        public void SendKey(byte key, bool isCaseSensitive = false, int delay = 75)
        {
            var canPushCapslk = false;

            if (isCaseSensitive)
            {
                var isCapslk = Control.IsKeyLocked(Keys.CapsLock);

                canPushCapslk =
                    isCapslk && key >= 'a' && key <= 'z' || !isCapslk && key >= 'A' && key <= 'Z';
            }

            key = (byte)VkKeyScan((char)key);

            var vKey = MapVirtualKey(key, 0);
            var lParam = (vKey * 0x10000) | (0xF);

            if (canPushCapslk)
            {
                PushCapslock();
            }

            PostMessage(Handle, WindowMessages.KeyDown, new IntPtr(key), new IntPtr(lParam));
            Thread.Sleep(delay);
            PostMessage(Handle, WindowMessages.KeyUp, new IntPtr(key), new IntPtr(lParam));

            Application.DoEvents();

            if (canPushCapslk)
            {
                PushCapslock();
            }

            Thread.Sleep(50);

            static void PushCapslock()
            {
                var capslkInitState = Control.IsKeyLocked(Keys.CapsLock);

                keybd_event(0x14, 0x45, 0X1, 0);
                keybd_event(0x14, 0x45, 0x1 | 0x2, 0);

                Application.DoEvents();

                do
                {
                } while (capslkInitState == Control.IsKeyLocked(Keys.CapsLock));
            }
        }

        public void SendString(string text, bool isCaseSensitive = true)
        {
            foreach (byte c in text)
            {
                SendKey(c, isCaseSensitive);
            }
        }

        public void SendClick(int x, int y, bool isDoubleClick = false)
        {
            var lParam = (uint)((y << 0x10) | (x & 0xFFFF));

            SendMessageA(Handle, WindowMessages.MouseMove, new IntPtr(0), new IntPtr(lParam));
            SendMessageA(Handle, WindowMessages.LButtonDown, new IntPtr(1), new IntPtr(lParam));
            SendMessageA(Handle, WindowMessages.LButtonUp, new IntPtr(0), new IntPtr(lParam));

            if (isDoubleClick)
            {
                SendMessageA(Handle, WindowMessages.LButtonDBClick, new IntPtr(0), new IntPtr(lParam));
                SendMessageA(Handle, WindowMessages.LButtonUp, new IntPtr(0), new IntPtr(lParam));
            }
        }

        public Task SendKeyAsync(byte key, bool isCaseSensitive = false, int delay = 75)
        {
            return Task.Run(() =>
            {
                SendKey(key, isCaseSensitive, delay);
            });
        }

        public Task SendStringAsync(string text, bool isCaseSensitive = true)
        {
            return Task.Run(() =>
            {
                SendString(text, isCaseSensitive);
            });
        }

        public Task SendClickAsync(int x, int y, bool isDoubleClick = false)
        {
            return Task.Run(() =>
            {
                SendClick(x, y, isDoubleClick);
            });
        }

        internal static WindowEngine[] GetWindows(Process process)
        {
            if (process is null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            var windows = new List<WindowEngine>();

            foreach (ProcessThread thread in process.Threads)
                EnumThreadWindows(
                    thread.Id,
                    (IntPtr, lParam) =>
                    {
                        windows.Add(
                            new WindowEngine(IntPtr)
                            );
                        return true;
                    },
                    IntPtr.Zero
                );

            return windows.ToArray();
        }

        internal static Task<WindowEngine[]> GetWindowsAsync(Process process)
        {
            if (process is null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            return Task.Run(() =>
            {
                return GetWindows(process);
            });
        }
    }
}