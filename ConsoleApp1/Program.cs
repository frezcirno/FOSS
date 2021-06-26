using System;
using System.Runtime.InteropServices;


namespace ConsoleApp1
{
    class Program
    {
        [DllImport("CppN.dll")]
        public static extern void Pr();
        
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Pr();
            Console.ReadLine();
        }
    }
}