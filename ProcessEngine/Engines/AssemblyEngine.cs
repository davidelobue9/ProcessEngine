using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ProcessEngine.Engines
{
    public class AssemblyEngine
    {

        #region Environment variables

        private readonly IntPtr processHandle;
        private readonly MemoryEngine memoryEngine;
        private readonly ThreadsEngine threadsEngine;

        #endregion

        #region Constructor/Destructor

        internal AssemblyEngine(IntPtr processHandle, MemoryEngine memoryEngine, ThreadsEngine threadsEngine)
        {
            this.processHandle = processHandle;
            this.memoryEngine = memoryEngine;
            this.threadsEngine = threadsEngine;
        }
        ~AssemblyEngine()
        { 
        }

        #endregion

        #region Methods
        
        public IntPtr InjectFunction(byte[] operationCodes)
        {
         
            IntPtr functionPointer = memoryEngine.Allocate(operationCodes.Length);

            memoryEngine.Write<byte>(functionPointer, operationCodes);

            return functionPointer;
        
        }

        public void ExecuteFunction(IntPtr functionPointer)
        {
            threadsEngine.CreateAndExecute(functionPointer);
        }

        public void ExecuteFunction(byte[] operationCodes)
        {

            IntPtr functionPointer = InjectFunction(operationCodes);
            ExecuteFunction(functionPointer);
            memoryEngine.Release(functionPointer);

        }

        public unsafe void ExecuteFunction(IntPtr functionPointer, int? eax = null, int? ecx = null, int? edx = null, params byte[] args)
        {

            var opCodes = new List<byte>();

            opCodes.AddRange(
                new byte[] {
                    0x60, // pusha
                    0x9c // pushd
                });

            if (eax != null)
            {
                var eaxTmp = BitConverter.GetBytes(eax.Value);
                opCodes.AddRange( new byte[] { 0xB8, eaxTmp[0], eaxTmp[1], eaxTmp[2], eaxTmp[3] } ); // mov eax, ...
            }
            if (ecx != null)
            {
                var ecxTmp = BitConverter.GetBytes(ecx.Value);
                opCodes.AddRange( new byte[] { 0xB9, ecxTmp[0], ecxTmp[1], ecxTmp[2], ecxTmp[3] } ); // mov ecx, ...
            }
            if (edx != null)
            {
                var edxTmp = BitConverter.GetBytes(edx.Value);
                opCodes.AddRange( new byte[] { 0xBA, edxTmp[0], edxTmp[1], edxTmp[2], edxTmp[3] } ); // mov edx, ...
            }

            foreach (var arg in args)
            { 
                opCodes.AddRange( new byte[] { 0x6A, arg } ); // push, ...
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

        #endregion

    }
}
