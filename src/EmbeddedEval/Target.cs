using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrueEval
{
    internal class Target
    {
        public void SomeMethod()
        {
            
            Patch.Eval(@"Console.WriteLine(""nuts"");");

            Console.WriteLine("deez");

            Patch.Eval(@"int i = 5;");
            // does not separate local variables
            Patch.Eval(@"int j = 10;");

            // error invalid c# code
            Patch.Eval(@"System.Console.WriteLine(i);");


        }
    }


}
