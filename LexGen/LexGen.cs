using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LexGen
{
    class LexGen
    {
        public static void Main(String[] args)
        {
            string text = File.ReadAllText("C:\\Temp\\CMPLR_X\\GenInputs\\keywords.lexem");
            string[] lexems = text.Split(new char[] { '\t', '\n', ',', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Array.Sort(lexems);

            var states = new List<Dictionary<char, int>>();
            states.Add(new Dictionary<char, int>());
            int curState;
            int prevState;
            for (int i = 0; i < lexems.Count(); i++)
            {
                var lexem = lexems[i];
                curState = 0;
                prevState = 0;
                foreach (char c in lexem)
                {
                    if (states[curState].ContainsKey(c))
                    {
                        prevState = curState;
                        curState = states[curState][c];
                    }
                    else
                    {
                        states[curState][c] = states.Count;
                        prevState = curState;
                        curState = states.Count;
                        states.Add(new Dictionary<char, int>());
                    }
                }

                states[prevState][(char)0] = -i;
            }
            states.Add(new Dictionary<char, int>());


            Console.WriteLine("Replace table_of_letters in Lexer.cs:\n");
            byte[,] tableOfLetters = new byte[states.Count, 26 + 26 + 1];
            var letters = "new byte[][] {\n                                        /*";
            for (char i = 'a'; i <= 'z'; i++)
                letters += Char.ToString(i) + ", ";
            for (char i = 'A'; i <= 'Z'; i++)
                letters += Char.ToString(i) + ", ";
            for (char i = '0'; i <= '9'; i++)
                letters += Char.ToString(i) + ", ";
            letters += "_*/";
            letters += "\n                                        /*";
            for (int i = 0; i < 63; i++)
                letters += (i < 9 ? i + ", " : i + ",");
            letters += "*/\n";
            for (int i = 0; i < states.Count; i++)
            {
                letters += "               /*state " + i + "*/new byte[]    {";
                for (char c = 'a'; c <= 'z'; c++)
                {
                    if (states[i].ContainsKey(c))
                        letters += (states[i][c] < 10 ? " " + states[i][c] + "," : states[i][c] + ",");
                    else
                        letters += (states.Count - 1 < 10 ? " " + (states.Count - 1) + "," : (states.Count - 1) + ",");
                }
                for (char c = 'A'; c <= 'Z'; c++)
                {
                    if (states[i].ContainsKey(c))
                        letters += (states[i][c] < 10 ? " " + states[i][c] + "," : states[i][c] + ",");
                    else
                        letters += (states.Count < 10 ? " " + (states.Count - 1) + "," : (states.Count - 1) + ",");
                }
                for (char c = '0'; c <= '9'; c++)
                {
                    if (states[i].ContainsKey(c))
                        letters += (states[i][c] < 10 ? " " + states[i][c] + "," : states[i][c] + ",");
                    else
                        letters += (states.Count < 10 ? " " + (states.Count - 1) + "," : (states.Count - 1) + ",");
                }
                if (states[i].ContainsKey('_'))
                    letters += (states[i]['_'] < 10 ? " " + states[i]['_'] + "}" : states[i]['_'] + "}");

                else
                    letters += (states.Count < 10 ? " " + (states.Count - 1) + "}" : (states.Count - 1) + "}");

                if (i != states.Count - 1)
                    letters += ",\n";
                else
                    letters += "\n\t\t};";

            }
            Console.WriteLine(letters);
            Console.WriteLine("Replace state_letters in Lexer.cs:\n");
            var stateLet = "{\n                Lexem.START";
            foreach (var state in states)
            {
                if (state.ContainsKey((char)0))
                {
                    stateLet += ",\n\t\tLexem." + lexems[-state[(char)0]].ToUpper();
                }
                else
                {
                    stateLet += ",\n\t\tLexem.IDENT";
                }
            }
            stateLet += "\n\t};";
            Console.WriteLine(stateLet);
            Console.WriteLine("Add to Lexems in Lexem.cs:\npublic enum Lexem\n{\n...\nEND=49\n");
            int mean = 50;
            foreach (string lexem in lexems)
            {
                Console.WriteLine(", " + lexem.ToUpper() + "=" + mean++.ToString());
            }

            Console.WriteLine("Replace method \"InitDict()\" body in Lexem.cs:\n");
            var initDict = "\t\tprivate void InitDict() {\n\t";
            for (char i = 'a'; i <= 'z'; i++)
            {
                initDict += "\tdictionaryForIdents.Add(\'" + i + "\'," + (int)(i - 'a') + ");\n\t";
            }
            for (char i = 'A'; i <= 'Z'; i++)
            {
                initDict += "\tdictionaryForIdents.Add(\'" + i + "\'," + (int)(i - 'A' + 26) + ");\n\t";
            }
            for (char i = '0'; i <= '9'; i++)
            {
                initDict += "\tdictionaryForIdents.Add(\'" + i + "\'," + (int)(i - '0' + 52) + ");\n\t";
            }
            initDict += "\tdictionaryForIdents.Add(\'_\'," + 62 + ");\n\t}";
            Console.WriteLine(initDict);

            Console.ReadLine();
        }
    }
}
