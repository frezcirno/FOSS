using System;
using System.Runtime.InteropServices;

namespace FileZipInterOp
{

    class Program
    {

        [DllImport("FileZip.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void AddFile(IntPtr search, IntPtr path);

        [DllImport("FileZip.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool CheckFile(IntPtr fileName, IntPtr path);

        [DllImport("FileZip.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void CopyToDir(IntPtr path, IntPtr dirName);

        [DllImport("FileZip.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void RemoveDir(IntPtr path, IntPtr dirName);

        [DllImport("FileZip.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void Greeting(IntPtr name);

        public static void Greeting(string name)
        {
            Greeting(Marshal.StringToBSTR(name));
        }

        [STAThread]
        static void Main(string[] args)
        {
            Greeting("World!");
        }
    }
}
