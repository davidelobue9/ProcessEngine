using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProcessEngine.Structs
{
    [StructLayout(LayoutKind.Explicit)]
    public struct CPURegister
    {
        [FieldOffset(0)] public byte Byte0;
        [FieldOffset(1)] public byte Byte1;
        [FieldOffset(2)] public byte Byte2;
        [FieldOffset(3)] public byte Byte3;

        [FieldOffset(0)] public int IntegerValue;



		public CPURegister(byte byte0, byte byte1, byte byte2, byte byte3) : this()
		{
			Byte0 = byte0;
			Byte1 = byte1;
			Byte2 = byte2;
			Byte3 = byte3;
		}

		public CPURegister(int integerValue) : this()
        {
            IntegerValue = integerValue;
        }

		public CPURegister(IntPtr pointer) : this(pointer.ToInt32())
		{
		}

		public byte[] GetBytes()
        {
            return new byte[] { Byte0, Byte1, Byte2, Byte3 };
        }
    }
}