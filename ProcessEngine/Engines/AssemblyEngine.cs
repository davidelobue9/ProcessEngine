using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessEngine.Engines
{
    public class AssemblyEngine
    {
        private readonly IntPtr _processHandle;
        private readonly MemoryEngine _memoryEngine;
        private readonly ThreadsEngine _threadsEngine;

        internal AssemblyEngine(IntPtr processHandle, MemoryEngine memoryEngine, ThreadsEngine threadsEngine)
        {
            _processHandle = processHandle;
            _memoryEngine = memoryEngine;
            _threadsEngine = threadsEngine;
        }

        public void DetourAttach(
            IntPtr targetFunctionPtr,
            int targetFunctionReplaceableLenght,
            IntPtr detourFunctionPtr,
            int? eax = null,
            int? edx = null,
            params byte[] args)
        {
            if (targetFunctionReplaceableLenght < 5)
            {
                throw new Exception("You need to replace at least 5 bytes to detour a function");
            }

            byte[] targetFunctionReplacedBytes = _memoryEngine.ReadArray<byte>(targetFunctionPtr, targetFunctionReplaceableLenght);
            IntPtr detourStubFunctionPtr = DetourInjectStubFunction(targetFunctionPtr, targetFunctionReplacedBytes, detourFunctionPtr, eax, edx, args);
            
            List<byte> targetReplaceBytes = new();

            int detourStubFnRelativeOffset = detourStubFunctionPtr.ToInt32() - targetFunctionPtr.ToInt32() - 5;
            byte[] detourStubFnRelativeOffsetBytes = BitConverter.GetBytes(detourStubFnRelativeOffset);
            targetReplaceBytes.AddRange(new byte[]
            {
                0xe9, detourStubFnRelativeOffsetBytes[0], detourStubFnRelativeOffsetBytes[1], detourStubFnRelativeOffsetBytes[2], detourStubFnRelativeOffsetBytes[3], // jmp detourStubFnRelativeOffsetBytes
            });
            targetReplaceBytes.AddRange(
                Enumerable.Repeat<byte>(0x90, targetFunctionReplaceableLenght - 5));

            _memoryEngine.WriteArray<byte>(targetFunctionPtr, targetReplaceBytes.ToArray());
        }

        private IntPtr DetourInjectStubFunction(
            IntPtr targetFunctionPtr, 
            byte[] targetFunctionReplacedBytes,
            IntPtr detourFunctionPtr,
            int? eax = null, 
            int? edx = null, 
            params byte[] args)
        {
            if (targetFunctionReplacedBytes.Length < 5)
            {
                throw new Exception("You need to replace at least 5 bytes to detour a function");
            }

            byte[] detourFnPtrBytes = BitConverter.GetBytes(detourFunctionPtr.ToInt32());
            List<byte> detourStubOpCodes = new()
            {
                0x60, // pusha
                0x9c // pushd
            };
            if (eax is not null)
            {
                var eaxTmp = BitConverter.GetBytes(eax.Value);
                detourStubOpCodes.AddRange(new byte[] { 0xB8, eaxTmp[0], eaxTmp[1], eaxTmp[2], eaxTmp[3] }); // mov eax, ...
            }
            if (edx is not null)
            {
                var edxTmp = BitConverter.GetBytes(edx.Value);
                detourStubOpCodes.AddRange(new byte[] { 0xBA, edxTmp[0], edxTmp[1], edxTmp[2], edxTmp[3] }); // mov edx, ...
            }
            foreach (var arg in args)
            {
                detourStubOpCodes.AddRange(new byte[] { 0x6A, arg }); // push, ...
            }
            detourStubOpCodes.AddRange(new byte[]
            {
                0xBF, detourFnPtrBytes[0], detourFnPtrBytes[1], detourFnPtrBytes[2], detourFnPtrBytes[3], // mov edi, detourFnPtrBytes
                0xFF, 0xD7, // call edi
                0x9D, // popf
                0x61, // popa
            });
            detourStubOpCodes.AddRange(targetFunctionReplacedBytes); // replaced original function op codes

            byte[] targetFunctionPtrBytes = BitConverter.GetBytes(targetFunctionPtr.ToInt32() + 5);
            detourStubOpCodes.AddRange(new byte[]
            {
                0x68, targetFunctionPtrBytes[0], targetFunctionPtrBytes[1], targetFunctionPtrBytes[2], targetFunctionPtrBytes[3], // push targetFunctionPtr
                0xc3 // ret
            });

            return InjectFunction(detourStubOpCodes.ToArray());
        }

        public IntPtr InjectFunction(byte[] operationCodes)
        {
            if (operationCodes is null)
            {
                throw new ArgumentNullException(nameof(operationCodes));
            }

            IntPtr functionPointer = _memoryEngine.Allocate(operationCodes.Length);

            _memoryEngine.WriteArray<byte>(functionPointer, operationCodes);

            return functionPointer;
        }

        public void ExecuteFunction(IntPtr functionPointer)
        {
            if (functionPointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(functionPointer)} argument is Zero");
            }

            _threadsEngine.CreateAndExecute(functionPointer);
        }

        public void ExecuteFunction(byte[] operationCodes)
        {
            if (operationCodes is null)
            {
                throw new ArgumentNullException(nameof(operationCodes));
            }

            IntPtr functionPointer = InjectFunction(operationCodes);
            ExecuteFunction(functionPointer);
            _memoryEngine.Release(functionPointer);
        }

        public unsafe void ExecuteFunction(IntPtr functionPointer, int? eax = null, int? ecx = null, int? edx = null, params byte[] args)
        {
            if (functionPointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(functionPointer)} argument is Zero");
            }

            var opCodes = new List<byte>();

            opCodes.AddRange(
                new byte[] {
                    0x60, // pusha
                    0x9c // pushd
                });

            if (eax != null)
            {
                var eaxTmp = BitConverter.GetBytes(eax.Value);
                opCodes.AddRange(new byte[] { 0xB8, eaxTmp[0], eaxTmp[1], eaxTmp[2], eaxTmp[3] }); // mov eax, ...
            }
            if (ecx != null)
            {
                var ecxTmp = BitConverter.GetBytes(ecx.Value);
                opCodes.AddRange(new byte[] { 0xB9, ecxTmp[0], ecxTmp[1], ecxTmp[2], ecxTmp[3] }); // mov ecx, ...
            }
            if (edx != null)
            {
                var edxTmp = BitConverter.GetBytes(edx.Value);
                opCodes.AddRange(new byte[] { 0xBA, edxTmp[0], edxTmp[1], edxTmp[2], edxTmp[3] }); // mov edx, ...
            }

            foreach (var arg in args)
            {
                opCodes.AddRange(new byte[] { 0x6A, arg }); // push, ...
            }

            var functionTmp = BitConverter.GetBytes(functionPointer.ToInt32());

            opCodes.AddRange(
                new byte[] {
                    0xBF, functionTmp[0], functionTmp[1], functionTmp[2], functionTmp[3], // mov edi, function
                    0xFF, 0xD7, // call edi
                    0x9D, // popf
                    0x61, // popa
                    0xC3 // ret
                });

            ExecuteFunction(opCodes.ToArray());
        }
    }
}