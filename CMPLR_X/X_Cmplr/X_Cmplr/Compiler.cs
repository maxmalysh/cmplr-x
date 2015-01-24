using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using NS_XCmplr;
using NS_C_Frontend;

namespace NS_Compiler
{
    class Compiler
    {
        private COptions opts;
        private SortedList<Position, Message> messages;
        private Dictionary<string, int> nameCodes;
        private List<string> names;

        static public readonly string PHASE_SEPARATOR = "####################################";

        public Compiler(COptions opts)
        {
            this.opts = opts;
            clean();
        }

        public bool CompileIt()
        {
            string srcCode = File.ReadAllText(COptions.SrcFile);

            var lexer = new Lexer(opts, this);
            var parser = new Parser();

            parser.parse(lexer.Tokens());

            return false;
        }

        #region Internal Utils

        internal int AddName(string name)
        {
            if (nameCodes.ContainsKey(name))
                return nameCodes[name];
            else
            {
                int code = names.Count;
                names.Add(name);
                nameCodes[name] = code;
                return code;
            }
        }
        internal string GetName(int code)
        {
            return names[code];
        }

        internal void AddMessage(Position pos, Message msg)
        {
            messages[pos] = msg;
        }

        #endregion

        private void clean()
        {
            messages = new SortedList<Position, Message>();
            nameCodes = new Dictionary<string, int>();
            names = new List<string>();
        }
    }
}