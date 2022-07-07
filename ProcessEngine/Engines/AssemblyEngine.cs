#nullable enable
using ProcessEngine.DTOs;
using System;
using System.Collections.Generic;

namespace ProcessEngine.Engines
{
    public class AssemblyEngine
    {        
        private readonly MemoryEngine _memoryEngine;
        private readonly ThreadsEngine _threadsEngine;

        internal AssemblyEngine(MemoryEngine memoryEngine, ThreadsEngine threadsEngine)
        {
            _memoryEngine = memoryEngine;
            _threadsEngine = threadsEngine;
        }

        public void CallFunction(IntPtr functionPointer)
        {
            if (functionPointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(functionPointer)} argument is Zero");
            }

            _threadsEngine.CreateAndExecute(functionPointer);
        }

        public void CallFunction(IntPtr functionPointer, RegisterCallingConventionParameters callingParams)
        {
            if (functionPointer.Equals(IntPtr.Zero))
            {
                throw new Exception($"{nameof(functionPointer)} argument is Zero");
            }
            if (callingParams is null)
            {
                throw new ArgumentNullException(nameof(callingParams));
            }

            byte[] operationCodes = GenerateCallFunctionOperationCodes(functionPointer, callingParams);
            InjectAndExecute(operationCodes);
        }

        public byte[] GenerateCallFunctionOperationCodes(
            IntPtr functionPointer,
            RegisterCallingConventionParameters? callingParams = null)
        {
            List<byte> operationCodes = new();

            if (callingParams is not null)
            {
                if (callingParams.EAX is not null)
                {
                    operationCodes.Add(0xB8); // mov eax, ...
                    operationCodes.AddRange(callingParams.EAX.Value.GetBytes());
                }
                if (callingParams.ECX is not null)
                {
                    operationCodes.Add(0xB9); // mov ecx, ...
                    operationCodes.AddRange(callingParams.ECX.Value.GetBytes());
                }
                if (callingParams.EDX is not null)
                {
                    operationCodes.Add(0xBA); // mov edx, ...
                    operationCodes.AddRange(callingParams.EDX.Value.GetBytes());
                }
                if (callingParams.LTRStack is not null)
                {
                    foreach (var arg in callingParams.LTRStack)
                    {
                        operationCodes.AddRange(new byte[] { 0x6A, arg }); // push, ...
                    }
                }
            }

            byte[] functionPointerBytes = BitConverter.GetBytes(functionPointer.ToInt32());
            operationCodes.AddRange(new byte[]
            {
                0xBF, functionPointerBytes[0], functionPointerBytes[1], functionPointerBytes[2], functionPointerBytes[3], // mov edi, function
                0xFF, 0xD7,                                                                                               // call edi
                0xC3                                                                                                      // ret
            });

            return operationCodes.ToArray();
        }

        public IntPtr Inject(byte[] operationCodes)
        {
            if (operationCodes is null)
            {
                throw new ArgumentNullException(nameof(operationCodes));
            }

            IntPtr functionPointer = _memoryEngine.Allocate(operationCodes.Length);
            _memoryEngine.WriteArray<byte>(functionPointer, operationCodes);

            return functionPointer;
        }

        public void InjectAndExecute(byte[] operationCodes)
        {
            if (operationCodes is null)
            {
                throw new ArgumentNullException(nameof(operationCodes));
            }

            IntPtr functionPointer = Inject(operationCodes);

            CallFunction(functionPointer);
            
            _memoryEngine.Release(functionPointer);
        }
    }
}