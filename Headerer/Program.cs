using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UtilsNS;

namespace Headerer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Extract headers of *.asf files in current folder");
            string dr = Directory.GetParent(Environment.GetCommandLineArgs()[0]).FullName+@"\";
            Console.WriteLine("-> "+dr);
            Console.WriteLine("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=");
            Console.WriteLine("");
            string[] fls = Directory.GetFiles(dr,"*.ahs");
            foreach (string fl in fls)
            {
                Console.WriteLine(fl); 
                List<string> ls = Utils.readList(fl,false);
                List<string> lt = new List<string>();
                foreach (string ln in ls)
                {
                    if (ln.Length.Equals(0)) continue;
                    if (ln[0].Equals('#'))
                        lt.Add(ln.Remove(0, 1));
                }
                Utils.writeList(Path.ChangeExtension(fl,".ahh"), lt);
            }
            Console.WriteLine("");
            Console.WriteLine("press any key to close");
            Console.ReadKey();
        }
    }
}
