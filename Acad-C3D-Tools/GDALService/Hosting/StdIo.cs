using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Hosting
{
    internal static class StdIo
    {
        static StdIo()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        }

        public static void WriteOut(string line) { Console.WriteLine(line); Console.Out.Flush(); }
        public static void WriteErr(string line) { Console.Error.WriteLine(line); Console.Error.Flush(); }
    }
}
