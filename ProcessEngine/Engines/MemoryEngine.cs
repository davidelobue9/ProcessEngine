using PlatformInvoke;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using static PlatformInvoke.Kernel32;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace ProcessEngine.Engines
{
    public class MemoryEngine
    {

        #region Environment variables

        private readonly IntPtr processHandle;

        #endregion

        #region Properties
        #endregion

        #region Constructors/Destructor

        internal MemoryEngine(IntPtr processHandle)
        {

            this.processHandle = processHandle;

        }
        ~MemoryEngine()
        {
        }

        #endregion

        #region Methods

        public IntPtr Allocate(int size)
        {

            IntPtr returnValue = VirtualAllocEx(
                processHandle,
                IntPtr.Zero,
                size,
                AllocationType.Commit,
                MemoryProtection.ExecuteReadWrite
            );

            if (returnValue == null)
                throw new Kernel32Exception("Impossible to allocate memory.", Marshal.GetLastWin32Error());

            return returnValue;

        }

        public void Release(IntPtr address)
        {

            bool setFree = VirtualFreeEx(
                processHandle,
                address,
                0,
                AllocationType.Release
            );

            if (!setFree)
                throw new Kernel32Exception("Impossible to release the memory.", Marshal.GetLastWin32Error());

        }

        public IntPtr ScanPattern(ProcessModule module, byte[] pattern, string mask, int offset)
        {

            IntPtr baseAddress = module.BaseAddress;
            int memorySize = module.ModuleMemorySize;

            byte[] buffer = new byte[memorySize];
            bool isRead = ReadProcessMemory(processHandle, baseAddress, buffer, memorySize, out var bytesread);
            if (!isRead)
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());

            int patternCursor = 0;
            for (int i = 0; i <= (memorySize - mask.Length + 1); i++)
            {
                while (buffer[i + patternCursor] == pattern[patternCursor] || mask[patternCursor] == '?')
                {
                    if (patternCursor == mask.Length - 1) return (baseAddress + i + offset);
                    patternCursor++;
                }
                patternCursor = 0;
            }

            return IntPtr.Zero;

        }

        public IntPtr GetPointer(IntPtr baseAddress, int[] offsets)
        {

            IntPtr address = Read<IntPtr>(baseAddress);

            var offsetsCollection = offsets.ToList();

            var lastOffset = offsetsCollection.Last();

            offsetsCollection.RemoveAt(offsetsCollection.Count - 1);

            foreach (var offset in offsetsCollection)
                address = Read<IntPtr>(IntPtr.Add(address, offset));

            return IntPtr.Add(address, lastOffset);

        }

        public T Read<T>(IntPtr pointer) where T : struct
        {

            int memoryAreaSize;

            memoryAreaSize = Marshal.SizeOf<T>();

            byte[] buffer = new byte[memoryAreaSize];
            bool isRead = ReadProcessMemory(
                processHandle,
                pointer,
                buffer,
                memoryAreaSize,
                out var bytesRead
                );

            if (!isRead)
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());

            return BytesToStructure<T>(buffer);

        }

        public T Read<T>(IntPtr pointer, int[] offsets) where T : struct
        {

            return Read<T>(
                GetPointer(pointer, offsets)
                );
        
        }

        public unsafe T[] ReadArray<T>(IntPtr pointer, int arrayLength) where T : struct
        {

            T[] read = new T[arrayLength];

            int structSize = Marshal.SizeOf<T>();
            int memoryAreaSize = structSize * arrayLength;

            byte[] buffer = new byte[memoryAreaSize];
            bool isRead = ReadProcessMemory(
                processHandle,
                pointer,
                buffer,
                memoryAreaSize,
                out var bytesRead
                );

            if (!isRead)
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());

            for (int i = 0; i < arrayLength; i++)
            {
                fixed (byte* ptr = &buffer[i * structSize])
                {
                    read[i] = (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
                }
            }

            return read;

        }

        public T[] ReadArray<T>(IntPtr pointer, int[] offsets, int arrayLength) where T : struct
        {

            return ReadArray<T>(
                GetPointer(pointer, offsets),
                arrayLength
                );

        }

        public string ReadString(IntPtr pointer)
        {

            string readString = "";
            byte[] buffer = new byte[14];

            bool nullCharFound = false;
            
            for(int offset = 0; !nullCharFound; offset += 14) 
            { 
                
                bool isRead = ReadProcessMemory(
                    processHandle,
                    pointer + offset,
                    buffer,
                    14,
                    out var bytesRead
                    );

                if (!isRead)
                    throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());

                foreach (char c in buffer)
                {
                    if (c != '\0')
                    {
                        readString += c;
                    }
                    else 
                    {
                        nullCharFound = true;
                        break;
                    }
                }

            }

            return readString;

        }

        public string ReadString(IntPtr pointer, int length)
        {

            byte[] buffer = new byte[length];
            bool isRead = ReadProcessMemory(
                processHandle,
                pointer,
                buffer,
                length,
                out var bytesRead
                );

            if (!isRead)
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());

            return Encoding.Default.GetString(buffer);

        }

        public void Write(IntPtr pointer, byte[] array)
        {

            if (array is null) 
                throw new ArgumentNullException(nameof(array));

            var size = array.Length;

            bool isWrote = WriteProcessMemory(
                processHandle,
                pointer,
                array,
                size,
                out var bytesWritten
                );

            if (!isWrote)
                throw new Kernel32Exception("Impossible to write memory.", Marshal.GetLastWin32Error());

        }

        public void Write<T>(IntPtr pointer, T value) where T : struct
        {

            var bytes = StructureToBytes(value);
            Write(pointer, bytes);

        }

        public void Write<T>(IntPtr pointer, T[] array) where T : struct
        {

            var bytes = StructuresToBytes(array);
            Write(pointer, bytes);

        }

        public void Write<T>(IntPtr pointer, int[] offsets, T value) where T : struct
        {

            Write<T>(
                GetPointer(pointer, offsets),
                value
                );

        }

        public void Write<T>(IntPtr pointer, int[] offsets, T[] array) where T : struct
        {

            Write<T>(
                GetPointer(pointer, offsets),
                array
                );

        }

        public Task<IntPtr> ScanPatternAsync(ProcessModule module, byte[] pattern, string mask, int offset)
        {

            return Task.Run(() => {
                return ScanPattern(module, pattern, mask, offset);
            });

        }

        public Task<IntPtr> GetPointerAsync(IntPtr baseAddress, int[] offsets)
        {

            return Task.Run(() => {
                return GetPointer(baseAddress, offsets);
            });

        }

        public Task<T> ReadAsync<T>(IntPtr pointer) where T : struct
        {

            return Task.Run(() => {
                return Read<T>(pointer);
            });

        }
        
        public Task<T> ReadAsync<T>(IntPtr pointer, int[] offsets) where T : struct
        {

            return Task.Run(() => {
                return Read<T>(pointer, offsets);
            });

        }

        public unsafe Task<T[]> ReadArrayAsync<T>(IntPtr pointer, int arrayLength) where T : struct
        {

            return Task.Run(() => {
                return ReadArray<T>(pointer, arrayLength);
            });

        }

        public Task<T[]> ReadArrayAsync<T>(IntPtr pointer, int[] offsets, int arrayLength) where T : struct
        {
            return Task.Run(() => {
                return ReadArray<T>(pointer, offsets, arrayLength);
            });
        }

        public Task<string> ReadStringAsync(IntPtr pointer)
        {
            return Task.Run(() => {
                return ReadString(pointer);
            });
        }

        public Task<string> ReadStringAsync(IntPtr pointer, int length)
        {
            return Task.Run(() => {
                return ReadString(pointer, length);
            });
        }

        public Task WriteAsync(IntPtr pointer, byte[] array)
        {
            return Task.Run(() => {
                Write(pointer, array);
            });
        }

        public Task WriteAsync<T>(IntPtr pointer, T value) where T : struct
        {
            return Task.Run(() => {
                Write(pointer, value);
            });
        }

        public Task WriteAsync<T>(IntPtr pointer, T[] array) where T : struct
        {
            return Task.Run(() => {
                Write(pointer, array);
            });
        }

        public Task WriteAsync<T>(IntPtr pointer, int[] offsets, T value) where T : struct
        {
            return Task.Run(() => {
                Write(pointer, offsets, value);
            });
        }

        public Task WriteAsync<T>(IntPtr pointer, int[] offsets, T[] array) where T : struct
        {
            return Task.Run(() => {
                Write(pointer, offsets, array);
            });
        }

        private byte[] StructureToBytes<T>(T str) where T : struct
        {

            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
                
            return arr;
        
        }

        private byte[] StructuresToBytes<T>(T[] structArray) where T : struct
        {

            var bytes = new List<byte>();

            foreach (T element in structArray)
                bytes.AddRange(
                    StructureToBytes<T>(element)
                    );              
            
            return bytes.ToArray();

        }

        private unsafe T BytesToStructure<T>(byte[] bytes) where T : struct
        {
            fixed (byte* bytePtr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)bytePtr, typeof(T));
            }     
        }
        
        #endregion

    }
}

