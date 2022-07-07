#nullable enable
using ProcessEngine.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessEngine.Engines
{
    public class DetouringEngine
    {
        private readonly MemoryEngine _memoryEngine;
        private readonly AssemblyEngine _assemblyEngine;

        public DetouringEngine(MemoryEngine memoryEngine, AssemblyEngine assemblyEngine)
        {
            _memoryEngine = memoryEngine;
            _assemblyEngine = assemblyEngine;
        }

        public Dictionary<IntPtr, byte[]> TargetReplaceableBytesByTargetPtr { get; private set; } = new();

        public void Attach(
            IntPtr targetPtr,
            int targetReplaceableBytesCount,
            IntPtr detourFunctionPtr,
            RegisterCallingConventionParameters? detourFunctionCallingParams = null)
        {
            if (targetPtr.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(targetPtr)} argument is Zero");
            }
            if (targetReplaceableBytesCount < 6)
            {
                throw new Exception("You need more replaceable bytes to detour a function");
            }
            if (detourFunctionPtr.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(detourFunctionPtr)} argument is Zero");
            }

            byte[] targetReplaceableBytes = _memoryEngine.ReadArray<byte>(targetPtr, targetReplaceableBytesCount);
            byte[] stubFunctionOperationCodes = GenerateStubFunctionOperationCodes(targetPtr, targetReplaceableBytes, detourFunctionPtr, detourFunctionCallingParams);
            IntPtr stubFunctionPtr = _assemblyEngine.Inject(stubFunctionOperationCodes);

            byte[] targetNewBytes = new byte[]
            { 0x68 }.Concat(BitConverter.GetBytes(stubFunctionPtr.ToInt32()))       // push stubFunctionPtr
            .Concat(new byte[] { 0xc3 })                                            // ret
            .Concat(Enumerable.Repeat<byte>(0x90, targetReplaceableBytesCount - 6)) // ...nop
            .ToArray();

            _memoryEngine.WriteArray<byte>(targetPtr, targetNewBytes);
            TargetReplaceableBytesByTargetPtr.Add(targetPtr, targetReplaceableBytes);
        }

        public void Detach(IntPtr targetPtr)
        {
            _memoryEngine.WriteArray<byte>(targetPtr, TargetReplaceableBytesByTargetPtr[targetPtr]);
            TargetReplaceableBytesByTargetPtr.Remove(targetPtr);
        }

        public void DetachAll()
        {
            IntPtr[] targetPtrs = TargetReplaceableBytesByTargetPtr.Keys.ToArray();

            foreach (IntPtr targetPtr in targetPtrs)
            {
                Detach(targetPtr);
            }
        }

        private byte[] GenerateStubFunctionOperationCodes(
            IntPtr targetPtr,
            byte[] targetReplaceableBytes,
            IntPtr detourFunctionPtr,
            RegisterCallingConventionParameters? detourFunctionCallingParams = null)
        {
            if (targetReplaceableBytes is null)
            {
                throw new ArgumentNullException(nameof(targetReplaceableBytes));
            }
            if (targetPtr.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(targetPtr)} argument is Zero");
            }
            if (detourFunctionPtr.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(detourFunctionPtr)} argument is Zero");
            }

            byte[] detourFunctionCallerOpCodes = _assemblyEngine.GenerateCallFunctionOperationCodes(detourFunctionPtr, detourFunctionCallingParams);
            IntPtr detourFunctionCallerPtr = _assemblyEngine.Inject(detourFunctionCallerOpCodes);

            byte[] operationCodes = new byte[]
            {
                0x60,                                                                                     // pusha
                0x9c,                                                                                     // pushd
            }
            .Concat(new byte[] { 0xBF }.Concat(BitConverter.GetBytes(detourFunctionCallerPtr.ToInt32()))) // mov edi, detourFunctionCallerPtrBytes
            .Concat(new byte[]
            {
                0xFF, 0xD7,                                                                               // call edi
                0x9D,                                                                                     // popf
                0x61,                                                                                     // popa
            })
            .Concat(targetReplaceableBytes)                                                               // ...targetReplaceableBytes
            .Concat(new byte[] { 0x68 }.Concat(BitConverter.GetBytes(targetPtr.ToInt32() + 6)))           // push targetPtr
            .Concat(new byte[] 
            {
                0xc3                                                                                      // ret  
            })                                                                                                                            
            .ToArray();

            return operationCodes;
        }
    }
}
