using System;
// using MD5LibLib;

namespace MD5InterOp
{
    class MD5Provider
    {
        public static string Digest(string password)
        {
            string res;
            // new MD5().digest(password, out res);
            return password;
        }

        static void Main(string[] args)
        {
            string md5 = Digest("Helloworld");
            Console.Out.WriteLine(md5);
            return;
        }
    }
}
