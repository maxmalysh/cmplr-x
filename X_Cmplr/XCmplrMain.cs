using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NS_Compiler;

namespace NS_XCmplr
{
    class COptions
    {
        static public string SrcFile
        {
            get
            {
                return "C:\\Temp\\CMPLR_X\\CTests\\ex01.c";
            }
        }

        static public bool DumpLexer
        {
            get
            {
                return true;
            }
        }

        static public bool DumpSyntaxParser
        {
            get
            {
                return true;
            }
        }

        static public bool DumpIrAfterSyntaxParser
        {
            get
            {
                return true;
            }
        }

        static public bool DelayAfterCompile
        {
            get
            {
                return true;
            }
        }

        static public int AlignStaticData
        {
            get 
            {
                return 1;
            }
        }

        static public int AlignStackData
        {
            get 
            {
                return 1;
            }
        }

        static public int AlignStructField
        {
            get 
            {
                return 1;
            }
        }

        static public int AlignArrayItem
        {
            get
            {
                return 1;
            }
        }

        static public int AlignCode
        {
            get 
            {
                return 1;
            }
        }


    }

    class XCmplrMain
    {

        class COptionParser
        {
            static public COptions Parse(string[] args)
            {
                return new COptions();
            }

            static public string ErrorMsg
            {
                get 
                {
                    return "";
                }
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("X compiler version 0.1\nCopyright (C) BMSTU 2014\n");

            COptions opts = COptionParser.Parse(args);

            Compiler cmplr = new Compiler(opts);
            cmplr.CompileIt();

            if (COptions.DelayAfterCompile)
            {
                Console.WriteLine("Delay after compilation. Press any key");
                Console.ReadLine();
            }
        }
    }
}
