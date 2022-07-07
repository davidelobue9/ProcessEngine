using ProcessEngine.Structs;

namespace ProcessEngine.DTOs
{
    public class RegisterCallingConventionParameters
    {
        public CPURegister? EAX { get; set; }
        public CPURegister? ECX { get; set; }
        public CPURegister? EDX { get; set; }

        public byte[] LTRStack { get; set; }
    }
}
