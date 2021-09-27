using System;

namespace ProcessEngine.DTOs
{
    public abstract class StructWrapperDTO
    {
        public IntPtr NativePtr { get; set; }
    }
}