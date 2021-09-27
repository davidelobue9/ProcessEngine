using System;
using System.Collections.Generic;

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