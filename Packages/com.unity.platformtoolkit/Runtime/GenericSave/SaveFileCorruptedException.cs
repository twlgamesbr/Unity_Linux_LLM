using System;

namespace Unity.PlatformToolkit
{
    internal class SaveFileCorruptedException : Exception
    {
        public SaveFileCorruptedException(string message)
            : base(message) { }
    }
}
