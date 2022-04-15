using PlatformInvoke;
using ProcessEngine.DTOs;
using ProcessEngine.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PlatformInvoke.Kernel32;

namespace ProcessEngine.Engines
{
    public class MemoryEngine
    {
        private readonly Mapper _mapper;
        private readonly IntPtr _processHandle;
        
        private readonly List<IntPtr> _allocatedMemoryBlockPtrs = new();

        internal MemoryEngine(IntPtr processHandle)
        {
            _processHandle = processHandle;
            _mapper = new Mapper(this);
        }

        public IntPtr[] AllocatedMemoryBlockPtrs { get => _allocatedMemoryBlockPtrs.ToArray(); }

        public IntPtr Allocate(int size)
        {
            IntPtr allocatedMemoryBlockPtr = VirtualAllocEx(
                _processHandle,
                IntPtr.Zero,
                size,
                AllocationType.Commit,
                MemoryProtection.ExecuteReadWrite
            );

            if (allocatedMemoryBlockPtr.Equals(IntPtr.Zero))
            {
                throw new Kernel32Exception("Impossible to allocate memory.", Marshal.GetLastWin32Error());
            }

            _allocatedMemoryBlockPtrs.Add(allocatedMemoryBlockPtr);

            return allocatedMemoryBlockPtr;
        }

        public IntPtr GetPointer(IntPtr basePointer, int[] offsets)
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            IntPtr pointer = Read<IntPtr>(basePointer);

            List<int> offsetsList = offsets.ToList();
            int lastOffset = offsetsList.Last();
            offsetsList.RemoveAt(offsetsList.Count - 1);

            foreach (var offset in offsetsList)
                pointer = Read<IntPtr>(IntPtr.Add(pointer, offset));

            return IntPtr.Add(pointer, lastOffset);
        }

        public Task<IntPtr> GetPointerAsync(IntPtr baseAddress, int[] offsets)
        {
            if (baseAddress.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(baseAddress)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            return Task.Run(
                () => GetPointer(baseAddress, offsets));
        }

        public T Read<T>(IntPtr pointer) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            int memoryAreaSize = Marshal.SizeOf<T>();

            byte[] buffer = new byte[memoryAreaSize];

            if (!ReadProcessMemory(
                _processHandle,
                pointer,
                buffer,
                memoryAreaSize,
                out var _
                ))
            {
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());
            }

            return BytesToStructure<T>(buffer);
        }

        public T Read<T>(IntPtr basePointer, int[] offsets) where T : struct
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            return Read<T>(
                GetPointer(basePointer, offsets)
                );
        }

        public Task<T> ReadAsync<T>(IntPtr pointer) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => Read<T>(pointer));
        }

        public Task<T> ReadAsync<T>(IntPtr pointer, int[] offsets) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            return Task.Run(
                () => Read<T>(pointer, offsets));
        }

        public TDestination ReadAndMap<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            TDestination destinationObj = _mapper.ReadAndMap<TSource, TDestination>(pointer);

            return destinationObj;
        }

        public Task<TDestination> ReadAndMapAsync<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => ReadAndMap<TSource, TDestination>(pointer));
        }

        public T[] ReadArray<T>(IntPtr pointer, int arrayLength) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            int structSize = Marshal.SizeOf<T>();
            int memoryAreaSize = structSize * arrayLength;

            byte[] buffer = new byte[memoryAreaSize];
            if (!ReadProcessMemory(
                _processHandle,
                pointer,
                buffer,
                memoryAreaSize,
                out var _
                ))
            {
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());
            }

            T[] read = new T[arrayLength];
            Parallel.For(0, arrayLength, i =>
            {
                unsafe
                {
                    fixed (byte* ptr = &buffer[i * structSize])
                    {
                        read[i] = (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
                    }
                }
            });
            return read;
        }

        public T[] ReadArray<T>(IntPtr basePointer, int[] offsets, int arrayLength) where T : struct
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            return ReadArray<T>(
                GetPointer(basePointer, offsets),
                arrayLength
                );
        }

        public Task<T[]> ReadArrayAsync<T>(IntPtr pointer, int arrayLength) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => ReadArray<T>(pointer, arrayLength));
        }

        public Task<T[]> ReadArrayAsync<T>(IntPtr pointer, int[] offsets, int arrayLength) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            return Task.Run(() =>
                ReadArray<T>(pointer, offsets, arrayLength));
        }

        public TDestination[] ReadListAndMap<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            TDestination[] destinationObj = _mapper.ReadAndMapList<TSource, TDestination>(pointer);

            return destinationObj;
        }

        public Task<TDestination[]> ReadListAndMapAsync<TSource, TDestination>(IntPtr pointer)
            where TSource : struct
            where TDestination : StructWrapperDTO, new()
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => _mapper.ReadAndMapList<TSource, TDestination>(pointer));
        }

        public string ReadString(IntPtr pointer)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            byte[] buffer = new byte[14];
            string readString = "";
            bool nullCharFound = false;
            for (int offset = 0; !nullCharFound; offset += 14)
            {
                if (!ReadProcessMemory(
                    _processHandle,
                    pointer + offset,
                    buffer,
                    14,
                    out var _
                    ))
                {
                    throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());
                }

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
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            byte[] buffer = new byte[length];
            if (!ReadProcessMemory(
                _processHandle,
                pointer,
                buffer,
                length,
                out var _
                ))
            {
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());
            }

            return Encoding.Default.GetString(buffer);
        }

        public Task<string> ReadStringAsync(IntPtr pointer)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => ReadString(pointer));
        }

        public Task<string> ReadStringAsync(IntPtr pointer, int length)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => ReadString(pointer, length));
        }

        public void Release(IntPtr pointer)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            if (!VirtualFreeEx(_processHandle, pointer, 0, AllocationType.Release))
            {
                throw new Kernel32Exception("Impossible to release the memory.", Marshal.GetLastWin32Error());
            }
            _allocatedMemoryBlockPtrs.Remove(pointer);
        }

        public void ReleaseAll()
        {
            foreach (IntPtr allocatedMemoryBlockPtrPtr in AllocatedMemoryBlockPtrs)
            {
                Release(allocatedMemoryBlockPtrPtr);
            }
        }

        public IntPtr ScanPattern(IntPtr baseAddress, int memorySize, byte[] pattern, string mask, int offset)
        {
            if (baseAddress.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(baseAddress)} argument is Zero");
            }
            else if (pattern is null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            byte[] buffer = new byte[memorySize];
            if (!ReadProcessMemory(_processHandle, baseAddress, buffer, memorySize, out _))
            {
                throw new Kernel32Exception("Impossible to read memory.", Marshal.GetLastWin32Error());
            }

            int patternCursor = 0;
            for (int i = 0; i <= buffer.Length - mask.Length + 1; i++)
            {
                while (buffer[i + patternCursor] == pattern[patternCursor] || mask[patternCursor] is '?')
                {
                    if (patternCursor == mask.Length - 1)
                    {
                        return baseAddress + i + offset;
                    }

                    patternCursor++;
                }
                patternCursor = 0;
            }

            return IntPtr.Zero;
        }

        public IntPtr ScanPattern(ProcessModule module, byte[] pattern, string mask, int offset = 0)
        {
            if (module is null)
            {
                throw new ArgumentNullException(nameof(module));
            }
            else if (pattern is null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            IntPtr baseAddress = module.BaseAddress;
            int memorySize = module.ModuleMemorySize;

            return ScanPattern(baseAddress, memorySize, pattern, mask, offset);
        }

        public Task<IntPtr> ScanPatternAsync(IntPtr baseAddress, int memorySize, byte[] pattern, string mask, int offset)
        {
            if (baseAddress.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(baseAddress)} argument is Zero");
            }
            else if (pattern is null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            return Task.Run(
                () => ScanPattern(baseAddress, memorySize, pattern, mask, offset));
        }

        public Task<IntPtr> ScanPatternAsync(ProcessModule module, byte[] pattern, string mask, int offset)
        {
            if (module is null)
            {
                throw new ArgumentNullException(nameof(module));
            }
            else if (pattern is null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            IntPtr baseAddress = module.BaseAddress;
            int memorySize = module.ModuleMemorySize;

            return ScanPatternAsync(baseAddress, memorySize, pattern, mask, offset);
        }

        public void Write<T>(IntPtr pointer, T value) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            var bytes = StructureToBytes(value);
            WriteArray(pointer, bytes);
        }

        public void Write<T>(IntPtr basePointer, int[] offsets, T value) where T : struct
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }

            Write(
                GetPointer(basePointer, offsets),
                value);
        }

        public Task WriteAsync<T>(IntPtr pointer, T value) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => Write(pointer, value));
        }

        public Task WriteAsync<T>(IntPtr pointer, int[] offsets, T value) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }

            return Task.Run(
                () => Write(pointer, offsets, value));
        }

        public void WriteArray(IntPtr pointer, byte[] array)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (!WriteProcessMemory(
                _processHandle,
                pointer,
                array,
                array.Length,
                out var _
                ))
            {
                throw new Kernel32Exception("Impossible to write memory.", Marshal.GetLastWin32Error());
            }
        }

        public void WriteArray<T>(IntPtr pointer, T[] array) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            WriteArray(
                pointer,
                StructuresToBytes(array));
        }

        public void WriteArray<T>(IntPtr basePointer, int[] offsets, T[] array) where T : struct
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (offsets is null)
            {
                throw new ArgumentNullException(nameof(offsets));
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            WriteArray(
                GetPointer(basePointer, offsets),
                array);
        }

        public Task WriteArrayAsync(IntPtr pointer, byte[] array)
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            return Task.Run(
                () => WriteArray(pointer, array));
        }

        public Task WriteArrayAsync<T>(IntPtr pointer, T[] array) where T : struct
        {
            if (pointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(pointer)} argument is Zero");
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            return Task.Run(
                () => WriteArray(pointer, array));
        }

        public Task WriteArrayAsync<T>(IntPtr basePointer, int[] offsets, T[] array) where T : struct
        {
            if (basePointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(basePointer)} argument is Zero");
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            else if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            return Task.Run(
                () => WriteArray(basePointer, offsets, array));
        }

        private unsafe T BytesToStructure<T>(byte[] bytes) where T : struct
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            fixed (byte* bytePtr = &bytes[0])
            {
                return (T)Marshal.PtrToStructure((IntPtr)bytePtr, typeof(T));
            }
        }

        private byte[] StructuresToBytes<T>(T[] structArray) where T : struct
        {
            if (structArray is null)
            {
                throw new ArgumentNullException(nameof(structArray));
            }

            var bytes = new List<byte>();

            foreach (T element in structArray)
                bytes.AddRange(
                    StructureToBytes(element));

            return bytes.ToArray();
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
    }
}