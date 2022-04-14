using ProcessEngine.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessEngine.Models
{
    public class RegisterCallingConventionParameters
    {
        public CPURegister? EAX { get; set; }
        public CPURegister? ECX { get; set; }
        public CPURegister? EDX { get; set; }

        public byte[] LTRStack { get; set; }
    }
}
