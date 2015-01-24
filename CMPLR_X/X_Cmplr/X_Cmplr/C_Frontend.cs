using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using NS_XCmplr;
using NS_SyntaxTableBuilder;
using NS_Compiler;
using NM_DirectedGraphLibrary.NM_DirectedGraphLibraryInternals;

namespace NS_C_Frontend
{

    class Message
    {
        public bool IsError;
        public string Text;

        public override string ToString()
        {
            return (IsError ? "ERROR: " : "WARNING: ") + Text;
        }
    }

    class Fragment
    {
        public Position Start, End = null;

        public override string ToString()
        {
            return Start.ToString() + (End != null ? "-" + End.ToString() : "");
        }
    }

    class Position : IComparable<Position>
    {
        string program = String.Empty;
        public int Index { get; private set; }
        public int Line { get; private set; }
        public int Pos { get; private set; }
        public int Current
        {
            get
            {
                return (Index == program.Length) ? -1
                    : Char.ConvertToUtf32(program, Index);
            }
        }
        public bool IsNewLine
        {
            get
            {
                if (Index == program.Length) return true;
                if (program[Index] == '\r' && Index + 1 < program.Length)
                    return program[Index + 1] == '\n';

                return program[Index] == '\n';
            }
        }

        public bool IsWhiteSpace
        {
            get
            {
                return Index != program.Length && Char.IsWhiteSpace(program, Index);
            }
        }

        public bool IsLetter
        {
            get
            {
                return Index != program.Length && (Char.IsLetter(program, Index) || Current == '_');
            }
        }

        public bool IsLetterOrDigit
        {
            get
            {
                return Index != program.Length && (Char.IsLetterOrDigit(program, Index) || Current == '_');
            }
        }

        public bool IsDecimalDigit
        {
            get
            {
                return Index != program.Length && program[Index] >= '0' && program[Index] <= '9';
            }
        }

        private Position() { }

        public Position(string program)
        {
            this.program = program;
            Line = Pos = 1;
            Index = 0;
        }

        public Position Copy()
        {
            Position result = new Position()
            {
                Line = this.Line,
                Pos = this.Pos,
                Index = this.Index,
                program = this.program
            };
            return result;
        }

        public override string ToString()
        { return "(" + Line + "," + Pos + ")"; }

        public static Position operator ++(Position p)
        {
            if (p.Index < p.program.Length)
            {
                if (p.IsNewLine)
                {
                    if (p.program[p.Index] == '\r') p.Index++;
                    p.Line++;
                    p.Pos = 1;
                }
                else if (p.program[p.Index] == '\t')
                {
                    if (Char.IsHighSurrogate(p.program[p.Index])) p.Index++;
                    p.Pos += 4;
                }
                else
                {
                    if (Char.IsHighSurrogate(p.program[p.Index])) p.Index++;
                    p.Pos++;
                }
                p.Index++;
            }
            return p;
        }

        public int CompareTo(Position other)
        { return Index.CompareTo(other.Index); }
    }

    abstract class Token
    {
        public Lexem Tag { get; set; }
        public Fragment Coords { get; set; }

        public override String ToString()
        { return Tag + ":" + Coords; }
    }

    class IdentToken : Token
    {
        public int Code { get; set; }
        public IdentToken() : base() { Tag = Lexem.IDENT; }

        public override String ToString()
        {
            return base.ToString().Split(':')[0]
                    + "[" + Code + "]:" +
                    base.ToString().Split(':')[1];
        }
    }

    class INumberToken : Token
    {
        public long Value { get; set; }
        public INumberToken() : base() { Tag = Lexem.INT_CONST; }

        public override String ToString()
        {
            return base.ToString().Split(':')[0]
                    + "[" + Value + "]:" +
                    base.ToString().Split(':')[1];
        }
    }

    class FNumberToken : Token
    {
        public double Value { get; set; }
        public FNumberToken() : base() { Tag = Lexem.FLOAT_CONST; }

        public override String ToString()
        {
            return base.ToString().Split(':')[0]
                    + "[" + Value + "]:" +
                    base.ToString().Split(':')[1];
        }
    }

    class StringToken : Token
    {
        public string Value { get; set; }
        public StringToken() : base() { Tag = Lexem.STRING_LITERAL; }

        public override String ToString()
        {
            return base.ToString().Split(':')[0]
                    + "[" + Value + "]:" +
                    base.ToString().Split(':')[1];
        }
    }

    class SpecToken : Token { }

    class Lexer
    {
        string program = String.Empty;
        COptions opts;
        Compiler compiler;
        Dictionary<char, int> dictionaryForIdents;
        Dictionary<char, int> dictionaryForDigits;

        bool afterIdent = false;
        bool afterValue = false;
        Token bufToken = null;

        #region Таблицы переходов и т.п.
        readonly byte[][] table_of_letters = new byte[][] { 
                                        /*0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10,11,12,13,14,15,16,17,18,19,20*/
                                        /*a, b, c, d, e, f, g, h, i, k, l, n, o, r, s, t, u, v, w, y, qpjzxm_A-Z0-9 */
               
               /*state 0*/ new byte[]   { 1, 2, 3, 4, 5, 6, 1, 1, 7, 1, 8, 1, 1, 9,10, 1,11,12,13, 1, 1  },
               /*state 1*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 2*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,14, 1, 1, 1, 1, 1,18, 1  },
               /*state 3*/ new byte[]   {21, 1, 1, 1, 1, 1, 1,33, 1, 1, 1, 1,24, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 4*/ new byte[]   { 1, 1, 1, 1,36, 1, 1, 1, 1, 1, 1, 1,42, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 5*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,46, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 6*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,51, 1,49, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 7*/ new byte[]   { 1, 1, 1, 1, 1,55, 1, 1, 1, 1, 1,56, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 8*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,58, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state 9*/ new byte[]   { 1, 1, 1, 1,61, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state10*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1,66,75, 1, 1, 1, 1, 1, 1, 1, 1, 1,70, 1, 1  },
               /*state11*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,80, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state12*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,87, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state13*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1,90, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state14*/ new byte[]   { 1, 1, 1, 1,15, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state15*/ new byte[]   {16, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state16*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1,17, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state17*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state18*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,19, 1, 1, 1, 1, 1  },
               /*state19*/ new byte[]   { 1, 1, 1, 1,20, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state20*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state21*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,22, 1, 1, 1, 1, 1, 1  },
               /*state22*/ new byte[]   { 1, 1, 1, 1,23, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state23*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state24*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,25, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state25*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,31,26, 1, 1, 1, 1, 1  },
               /*state26*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1,27, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state27*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,28, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state28*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,29, 1, 1, 1, 1  },
               /*state29*/ new byte[]   { 1, 1, 1, 1,30, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state30*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state31*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,32, 1, 1, 1, 1, 1  },
               /*state32*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state33*/ new byte[]   {34, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state34*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,35, 1, 1, 1, 1, 1, 1, 1  },
               /*state35*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state36*/ new byte[]   { 1, 1, 1, 1, 1,37, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state37*/ new byte[]   {38, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state38*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,39, 1, 1, 1, 1  },
               /*state39*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,40, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state40*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,41, 1, 1, 1, 1, 1  },
               /*state41*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state42*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,93, 1, 1, 1, 1  },
               /*state43*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,44, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state44*/ new byte[]   { 1, 1, 1, 1,45, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state45*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state46*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,47, 1, 1, 1, 1, 1, 1  },
               /*state47*/ new byte[]   { 1, 1, 1, 1,48, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state48*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state49*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,50, 1, 1, 1, 1, 1, 1, 1  },
               /*state50*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state51*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,52, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state52*/ new byte[]   {53, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state53*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,54, 1, 1, 1, 1, 1  },
               /*state54*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state55*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state56*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,57, 1, 1, 1, 1, 1  },
               /*state57*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state58*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,59, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state59*/ new byte[]   { 1, 1, 1, 1, 1, 1,60, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state60*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state61*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,62, 1, 1, 1, 1, 1  },
               /*state62*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,63, 1, 1, 1, 1  },
               /*state63*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,64, 1, 1, 1, 1, 1, 1, 1  },
               /*state64*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,65, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state65*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state66*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,67, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state67*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,68, 1, 1, 1, 1, 1, 1, 1  },
               /*state68*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,69, 1, 1, 1, 1, 1  },
               /*state69*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state70*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1,71, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state71*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,72, 1, 1, 1, 1, 1  },
               /*state72*/ new byte[]   { 1, 1,73, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state73*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1,74, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state74*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state75*/ new byte[]   { 1, 1, 1, 1, 1, 1,76, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state76*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,77, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state77*/ new byte[]   { 1, 1, 1, 1,78, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state78*/ new byte[]   { 1, 1, 1,79, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state79*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state80*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,81, 1, 1, 1, 1, 1, 1  },
               /*state81*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1,82, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state82*/ new byte[]   { 1, 1, 1, 1, 1, 1,83, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state83*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,84, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state84*/ new byte[]   { 1, 1, 1, 1,85, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state85*/ new byte[]   { 1, 1, 1,86, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state86*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state87*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1,88, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state88*/ new byte[]   { 1, 1, 1,89, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state89*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state90*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1,91, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state91*/ new byte[]   { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,92, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state92*/ new byte[]   { 1, 1, 1, 1,93, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
               /*state93*/ new byte[]   { 1,43, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },
                                        /*a, b, c, d, e, f, g, h, i, k, l, n, o, r, s, t, u, v, w, y, qpjzxm_A-Z0-9 */
            };

        readonly Lexem[] states_letters = {
                Lexem.START,    /* state 0  = START */
                Lexem.IDENT,    /* state 1  = DEFAULT IDENT */
                Lexem.IDENT,    /* state 2  = b|reak b|yte */
                Lexem.IDENT,    /* state 3  = c|ase c|ontinue c|har c|onst */
                Lexem.IDENT,    /* state 4  = d|ouble  d|efault d|o */
                Lexem.IDENT,    /* state 5  = e|lse */
                Lexem.IDENT,    /* state 6  = f|or f|loat*/
                Lexem.IDENT,    /* state 7  = i|f i|nt */
                Lexem.IDENT,    /* state 8  = l|ong */
                Lexem.IDENT,    /* state 9  = r|eturn */
                Lexem.IDENT,    /* state 10 = s|witch s|hort s|igned */
                Lexem.IDENT,    /* state 11 = u|nsigned */
                Lexem.IDENT,    /* state 12 = v|oid */
                Lexem.IDENT,    /* state 13 = w|hile */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.BREAK,                /* state 14,15,16,17 = break */
                Lexem.IDENT,Lexem.IDENT,Lexem.BYTE,                             /* state 18,19,20 = byte */
                Lexem.IDENT,Lexem.IDENT,Lexem.CASE,                             /* state 21,22,23 = case */
                Lexem.IDENT,Lexem.IDENT,                                        /* state 24,25 = con|st con|tinue */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.CONTINUE, /* state 26,27,28,29,30 = continue */
                Lexem.IDENT,Lexem.CONST,                                        /* state 31,32 = const */
                Lexem.IDENT,Lexem.IDENT,Lexem.CHAR,                             /* state 33,34,35 = char */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.DEFAULT,      /* state 36,37,38,39,40,41 = default */
                Lexem.DO,                                                       /* state 42 = do do|ble */
                Lexem.IDENT,Lexem.IDENT,Lexem.DOUBLE,                           /* state 43,44,45 = double */
                Lexem.IDENT,Lexem.IDENT,Lexem.ELSE,                             /* state 46,47,48 = else */
                Lexem.IDENT,Lexem.FOR,                                          /* state 49,50 = for */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.FLOAT,                /* state 51,52,53,54 = float */
                Lexem.IF,                                                       /* state 55 = if */
                Lexem.IDENT,Lexem.INT,                                          /* state 56,57 = int */
                Lexem.IDENT,Lexem.IDENT,Lexem.LONG,                             /* state 58,59,60 = long */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.RETURN,              /* state 61,62,63,64,65 = break */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.SHORT,                /* state 66,67,68,69 = short */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.SWITCH,   /* state 70,71,72,73,74 = switch */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.SIGNED,   /* state 75,76,77,78,79 = signed */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.UNSIGNED, /* state 80,81,82,83,84,85,86 = unsigned */
                Lexem.IDENT,Lexem.IDENT,Lexem.VOID,                             /* state 87,88,89 = long */
                Lexem.IDENT,Lexem.IDENT,Lexem.IDENT,Lexem.WHILE,                /* state 90,91,92,93 = while */
                Lexem.IDENT                                                     /* state U for double(Forget it)*/
            };
        readonly byte[][] table_of_numbers = new byte[][] { 
               /*state 9  Exit*/        /*-+, 0,19, ., E, other*/
               /*state 0*/ new byte[]    { 8, 1, 4, 8, 8, 8  },
               /*state 1*/ new byte[]    { 9, 8, 8, 2, 8, 8  },
               /*state 2*/ new byte[]    { 8, 3, 3, 8, 8, 8  },
               /*state 3*/ new byte[]    { 9, 3, 3, 8, 5, 8  },
               /*state 4*/ new byte[]    { 9, 4, 4, 2, 5, 8  },
               /*state 5*/ new byte[]    { 6, 8, 7, 8, 8, 8  },
               /*state 6*/ new byte[]    { 8, 8, 7, 8, 8, 8  },
               /*state 7*/ new byte[]    { 9, 7, 7, 8, 8, 8  },
               /*state 8*/ new byte[]    { 8, 8, 8, 8, 8, 8  }, /*ERROR*/

            };
        readonly Lexem[] states_numbers = {
                Lexem.START,        /* state 0  = START             */
                Lexem.INT_CONST,    /* state 1  = final     0       */
                Lexem.ERROR,        /* state 2  = Non-final 1.      */
                Lexem.FLOAT_CONST,  /* state 3  = final     1.2     */
                Lexem.INT_CONST,    /* state 4  = final     1       */
                Lexem.ERROR,        /* state 5  = Non-final 1.2E    */
                Lexem.ERROR,        /* state 6  = Non-final 1.2E-   */
                Lexem.FLOAT_CONST,  /* state 7  = final     1.2E-3  */
                Lexem.ERROR
            };
        #endregion

        #region Сканеры
        private Token scan(Position cursor)
        {
            Token result = null;
            if (bufToken != null)
            {
                result = bufToken;
                bufToken = null;
                return result;
            }

            while (cursor.IsWhiteSpace)         //WhiteSpaces
                cursor++;

            if (cursor.Current == -1)
                return new SpecToken()
                {
                    Tag = Lexem.END,
                    Coords = new Fragment() { Start = cursor.Copy() }
                };

            if (cursor.IsLetter)                //Idents and keys
                result = FindIdent(cursor);
            else if (cursor.IsDecimalDigit)     //Numbers
                result = FindNumber(cursor);
            else if (cursor.Current == '\"')    //Strings
                result = FindStr(cursor);
            else if (cursor.Current == '\'')    //Chars
                result = FindChar(cursor);
            else                                //Other
                result = FindOther(cursor);


            return result ?? scan(cursor);
        }

        private Token FindIdent(Position cursor)
        {
            if (afterIdent || afterValue)
            {
                compiler.AddMessage(cursor.Copy(), new Message()
                {
                    IsError = true,
                    Text = "Unexpected identifier after identifier or value"
                });
                cursor++;
                afterIdent = afterValue = false;
                return null;
            }


            int state = 0;
            Position start = cursor.Copy();
            List<char> attr = new List<char>();

            while (cursor.IsLetterOrDigit)
            {
                int mChar;
                dictionaryForIdents.TryGetValue((char)cursor.Current, out mChar);

                state = table_of_letters[state][mChar];
                attr.Add((char)cursor.Current);

                cursor++;
            }

            afterIdent = states_letters[state] == Lexem.IDENT;
            afterValue = false;

            if (afterIdent) return new IdentToken()
            {
                Code = compiler.AddName(new String(attr.ToArray())),
                Coords = new Fragment() { Start = start, End = cursor.Copy() }
            };
            else return new SpecToken()
            {
                Tag = states_letters[state],
                Coords = new Fragment() { Start = start, End = cursor.Copy() }
            };
        }

        private Token FindNumber(Position cursor)
        {
            if (afterIdent || afterValue)
            {
                compiler.AddMessage(cursor.Copy(), new Message()
                {
                    IsError = true,
                    Text = "Unexpected identifier after identifier or value"
                });
                cursor++;
                afterIdent = afterValue = false;
                return null;
            }

            int state = 0;
            Position start = cursor.Copy();
            List<char> attr = new List<char>();

            while (cursor.IsDecimalDigit
                || cursor.Current == '.'
                || cursor.Current == '+'
                || cursor.Current == '-'
                || cursor.Current == 'E'
                || cursor.Current == 'e')
            {
                if (state == 8) break;

                int mChar;
                dictionaryForDigits.TryGetValue((char)cursor.Current, out mChar);

                if (table_of_numbers[state][mChar] == 9)
                { //Exit
                    break;
                }
                state = table_of_numbers[state][mChar];

                attr.Add(cursor.Current == '.' ? ',' : (char)cursor.Current);
                cursor++;
            }

            afterValue = states_numbers[state] != Lexem.ERROR;
            afterIdent = false;


            string value = new String(attr.ToArray());

            long iValue;
            double fValue;
            if (afterValue && long.TryParse(value, out iValue)) return new INumberToken()
            {
                Coords = new Fragment() { Start = start, End = cursor.Copy() },
                Value = iValue
            };
            else if (afterValue && double.TryParse(value, out fValue)) return new FNumberToken()
            {
                Coords = new Fragment() { Start = start, End = cursor.Copy() },
                Value = fValue
            };


            string errorText = afterValue ? "Constant is too large: " + value : "Bad constant";
            compiler.AddMessage(start, new Message()
            {
                IsError = true,
                Text = errorText
            });

            return null;
        }

        private Token FindStr(Position cursor)
        {
            cursor++;

            if (afterIdent || afterValue)
            {
                compiler.AddMessage(cursor.Copy(), new Message()
                {
                    IsError = true,
                    Text = "Unexpected value after identifier or value"
                });
                afterIdent = afterValue = false;
                return null;
            }

            Position start = cursor.Copy();
            List<char> attr = new List<char>();
            bool correctStr = false;

            while (cursor.Current != -1 && !cursor.IsNewLine)
            {
                if (cursor.Current == '\"')
                {
                    correctStr = true;
                    break;
                }
                else if (cursor.Current == '\\')
                {
                    char? escape = TryParseEscape(cursor);
                    if (escape == null)
                    {
                        compiler.AddMessage(cursor.Copy(), new Message()
                        {
                            IsError = true,
                            Text = "Bad escape \\" + (char)cursor.Current
                        });
                        attr.Add('\\');
                        attr.Add((char)cursor++.Current);
                    }
                    else
                        attr.Add((char)escape);
                }
                else
                {
                    attr.Add((char)cursor.Current);
                    cursor++;
                }
            }
            afterValue = correctStr;
            afterIdent = false;


            if (!correctStr)
            {
                compiler.AddMessage(cursor++.Copy(), new Message()
                {
                    IsError = true,
                    Text = "\" expected"
                });

                return null;
            }

            return new StringToken()
            {
                Value = new string(attr.ToArray()),
                Coords = new Fragment() { Start = start, End = cursor++.Copy() }
            };
        }

        private Token FindChar(Position cursor)
        {
            cursor++;
            if (afterIdent || afterValue)
            {
                compiler.AddMessage(cursor.Copy(), new Message()
                {
                    IsError = true,
                    Text = "Unexpected value after identifier or value"
                });
                afterIdent = afterValue = false;
                return null;
            }

            /*
             * Первый символ или escape-последовательность
             */
            char? codePoint = null;

            if (cursor.Current == '\\')
            {
                codePoint = TryParseEscape(cursor);
            }
            else if (!cursor.IsNewLine)
            {
                codePoint = (char)cursor.Current;
                cursor++;
            }


            /*
             * Закрывающаяся кавычка
             * Итог
             */
            afterIdent = false;
            afterValue = codePoint != null && cursor.Current == '\'';

            if (afterValue) return new INumberToken()
            {
                Coords = new Fragment() { Start = cursor++.Copy() },
                Value = (char)codePoint
            };


            string errorText = cursor.Current != '\'' ? "\' expected" : "Bad escape";
            compiler.AddMessage(cursor++, new Message()
            {
                IsError = true,
                Text = errorText
            });

            return null;
        }

        private char? TryParseEscape(Position cursor)
        {
            char? result = null;
            if (cursor.Current != '\\')
            {
                return null;
            }
            switch ((++cursor).Current)
            {
                case '\'':
                case '\"':
                case '\\':
                    result = (char)cursor.Current;
                    break;
                case 'a':
                    result = '\a';
                    break;
                case 'b':
                    result = '\b';
                    break;
                case 'f':
                    result = '\f';
                    break;
                case 'n':
                    result = '\n';
                    break;
                case 'r':
                    result = '\r';
                    break;
                case 't':
                    result = '\t';
                    break;
                case 'v':
                    result = '\v';
                    break;
                default:
                    break;
            }
            cursor++;
            return result;
        }

        private Token FindOther(Position cursor)
        {
            Lexem tag = Lexem.ERROR;
            Position start = cursor.Copy();
            switch (cursor.Current)
            {
                case '>':
                    if ((++cursor).Current == '>')
                        if ((++cursor).Current == '=')
                        {
                            tag = Lexem.ASSIGN_RIGHT;
                            cursor++;
                        }
                        else
                            tag = Lexem.OP_RIGHT;
                    else if (cursor.Current == '=')
                    {
                        tag = Lexem.LOGIC_NOT_S;
                        cursor++;
                    }
                    else
                        tag = Lexem.LOGIC_L;
                    break;
                case '<':
                    if ((++cursor).Current == '<')
                        if ((++cursor).Current == '=')
                        {
                            tag = Lexem.ASSIGN_LEFT;
                            cursor++;
                        }
                        else
                            tag = Lexem.OP_LEFT;
                    else if (cursor.Current == '=')
                    {
                        tag = Lexem.LOGIC_NOT_L;
                        cursor++;
                    }
                    else
                        tag = Lexem.LOGIC_S;
                    break;
                case '+':
                    if ((++cursor).Current == '+')
                        if ((++cursor).Current == '+')
                            if (afterIdent)
                            {
                                tag = Lexem.INC;
                                cursor++;
                                bufToken = new SpecToken()
                                {
                                    Tag = Lexem.OP_ADD,
                                    Coords = new Fragment() { Start = cursor++ }
                                };
                            }
                            else
                                tag = Lexem.INC;
                        else
                            tag = Lexem.INC;
                    else if (cursor.Current == '=')
                    {
                        tag = Lexem.ASSIGN_ADD;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_ADD;
                    break;
                case '-':
                    if ((++cursor).Current == '-')
                        if ((++cursor).Current == '-')
                            if (afterIdent)
                            {
                                tag = Lexem.DEC;
                                cursor++;
                                bufToken = new SpecToken()
                                {
                                    Tag = Lexem.OP_SUB,
                                    Coords = new Fragment() { Start = cursor++ }
                                };
                            }
                            else
                                tag = Lexem.DEC;
                        else
                            tag = Lexem.DEC;
                    else if (cursor.Current == '=')
                    {
                        tag = Lexem.ASSIGN_SUB;
                        cursor++;
                    }
                    else if (cursor.Current == '>')
                    {
                        tag = Lexem.PTR_LINK;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_SUB;
                    break;
                case '*':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_MUL;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_MUL;
                    break;
                case '/':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_DIV;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_DIV;
                    break;
                case '%':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_PROCENT;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_PROCENT;
                    break;
                case '&':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_AND;
                        cursor++;
                    }
                    else if (cursor.Current == '&')
                    {
                        tag = Lexem.LOGIC_AND;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_AND;
                    break;
                case '^':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_XOR;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_XOR;
                    break;
                case '|':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.ASSIGN_OR;
                        cursor++;
                    }
                    else if (cursor.Current == '|')
                    {
                        tag = Lexem.LOGIC_OR;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_OR;
                    break;
                case '!':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.LOGIC_NOT_EQ;
                        cursor++;
                    }
                    else
                        tag = Lexem.OP_NOT;
                    break;
                case '=':
                    if ((++cursor).Current == '=')
                    {
                        tag = Lexem.LOGIC_EQ;
                        cursor++;
                    }
                    else
                        tag = Lexem.ASSIGN;
                    break;
                case '[':
                    tag = Lexem.PTR_MASS_LEFT;
                    cursor++;
                    break;
                case ']':
                    tag = Lexem.PTR_MASS_RIGHT;
                    cursor++;
                    break;
                case '{':
                    tag = Lexem.BLOCK_LEFT;
                    cursor++;
                    break;
                case '}':
                    tag = Lexem.BLOCK_RIGHT;
                    cursor++;
                    break;
                case '(':
                    tag = Lexem.FUNC_ARG_LEFT;
                    cursor++;
                    break;
                case ')':
                    tag = Lexem.FUNC_ARG_RIGHT;
                    cursor++;
                    break;
                case ';':
                    tag = Lexem.SEMICOLON;
                    cursor++;
                    break;
                case ',':
                    tag = Lexem.COMMA;
                    cursor++;
                    break;
                case '.':
                    tag = Lexem.POINT;
                    cursor++;
                    break;
                case ':':
                    tag = Lexem.DOUBLE_POINT;
                    cursor++;
                    break;
                default:
                    break;
            }
            afterIdent = afterValue = false;

            if (tag == Lexem.ERROR)
            {
                compiler.AddMessage(cursor.Copy(), new Message()
                {
                    IsError = true,
                    Text = "Unexpected character " + (char)cursor.Current
                });
                cursor++;
                return null;
            }
            return new SpecToken()
            {
                Tag = tag,
                Coords = new Fragment() { Start = start }
            };
        }
        #endregion

        public Lexer(COptions opts, Compiler compiler)
        {
            this.opts = opts;
            this.compiler = compiler;

            dictionaryForIdents = new Dictionary<char, int> { };
            dictionaryForDigits = new Dictionary<char, int> { };

            /*
             * Заглавные буквы
             */
            for (char i = 'A'; i <= 'Z'; i++)
                dictionaryForIdents.Add(i, 20);

            dictionaryForDigits.Add('E', 4);

            /*
             *  Цифры
             */
            dictionaryForIdents.Add('0', 20);
            dictionaryForDigits.Add('0', 1);

            for (char i = '1'; i <= '9'; i++)
            {
                dictionaryForIdents.Add(i, 20);
                dictionaryForDigits.Add(i, 2);
            }

            /*
             * маленькие буквы
             */

            dictionaryForDigits.Add('e', 4);

            int point = 'a';
            for (char i = 'a'; i <= 'i'; i++)
                dictionaryForIdents.Add(i, i - point);

            dictionaryForIdents.Add('j', 20); point++;
            dictionaryForIdents.Add('k', 'k' - point);
            dictionaryForIdents.Add('l', 'l' - point);
            dictionaryForIdents.Add('m', 20); point++;
            dictionaryForIdents.Add('n', 'n' - point);
            dictionaryForIdents.Add('o', 'o' - point);
            dictionaryForIdents.Add('p', 20); point++;
            dictionaryForIdents.Add('q', 20); point++;

            for (char i = 'r'; i <= 'w'; i++)
                dictionaryForIdents.Add(i, i - point);

            dictionaryForIdents.Add('x', 20); point++;
            dictionaryForIdents.Add('y', 'y' - point);
            dictionaryForIdents.Add('z', 20); point++;

            /*
             * Спец символы
             */
            dictionaryForIdents.Add('_', 20);
            dictionaryForDigits.Add('.', 3);
            dictionaryForDigits.Add('+', 0);
            dictionaryForDigits.Add('-', 0);

            this.program = File.ReadAllText(COptions.SrcFile);
        }

        public IEnumerable<Token> Tokens()
        {
            Position cursor = new Position(program);
            Token t = new SpecToken()
            {
                Tag = Lexem.START,
                Coords = new Fragment() { Start = cursor }
            };

            if (COptions.DumpLexer)
            {
                Console.WriteLine(Compiler.PHASE_SEPARATOR);
                Console.WriteLine(t.ToString());
            }
            yield return t;

            while (t.Tag != Lexem.END)
            {
                t = scan(cursor);
                if (COptions.DumpLexer)
                    Console.WriteLine(t.ToString());
                yield return t;
            }
        }

        public List<Token> AllTokens()
        {
            Position cursor = new Position(program);
            List<Token> tokens = new List<Token>();

            tokens.Add(
                new SpecToken()
                {
                    Tag = Lexem.START,
                    Coords = new Fragment() { Start = cursor.Copy() }
                }
            );

            while (tokens.Last().Tag != Lexem.END)
                tokens.Add(scan(cursor));

            return tokens;
        }
    }

    public class CDirAdjListGraph<
                    EProperties,
                    VProperties>
        : CDirGraph<EProperties, VProperties>
    {
        public CDirAdjListGraph()
            : base(new CDirectedAdjListStorage<EProperties, VProperties>())
        { }
    }

    class CAstEdge { }

    class CAstNode
    {
        public string name;
        public List<CAstNode> dauthers;
        public CAstNode parent;

        public CAstNode(string name)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return name;
        }

        public void Print(int level)
        {
            for (int i = 0; i < level; i++)
                Console.Write("|\t");
            Console.WriteLine(">" + name);
            if (dauthers != null)
                dauthers.ForEach(x => x.Print(level + 1));
        }
    }

    public class SyntaxTree
    {
        private CDirAdjListGraph<CAstNode, CAstEdge> tree;

        public SyntaxTree()
        {
            tree = new CDirAdjListGraph<CAstNode, CAstEdge>();
        }

    }

    class Parser
    {
        public void parse(IEnumerable<Token> program)
        {
            var tb = new TableBuilder(File.ReadAllText("c.grammar"));

            /*
             * Таблицы GoTo & Action
             */
            List<Rule> rules;
            Tuple<Actions, int, LRItem>[,] tableAction;
            int[,] tableGoto;
            tb.getTables(out rules, out tableAction, out tableGoto);


            /*
             * Initial
             */
            var resultStack = new Stack<CAstNode>();
            var stackStates = new Stack<int>();
            var stackTokens = new Stack<Token>();

            var loop = true;
            var pos = program.GetEnumerator();
            var lexems = Enum.GetNames(typeof(Lexem));
            /*
             * Start Option
             */
            pos.MoveNext();
            stackStates.Push(0);


            if (pos.Current.Tag == Lexem.START)
            {
                stackTokens.Push(pos.Current);
                pos.MoveNext();
            }


            while (stackTokens.Count != 0 && loop)
            {
                var a = pos.Current;
                var q = stackStates.First();
                var currentAction = tableAction[q, (int)a.Tag];
                switch (currentAction != null ? currentAction.Item1 : Actions.ERROR)
                {
                    case Actions.SHIFT:
                        stackTokens.Push(a);
                        if (!pos.MoveNext())
                            throw new Exception("Не хватает символов!!!");
                        stackStates.Push(currentAction.Item2);
                        break;
                    case Actions.REDUCE:
                        foreach (var s in currentAction.Item3.var)
                            stackStates.Pop();
                        int newState = stackStates.First();

                        var newPush = tableGoto[newState, rules.FindIndex(x => x.name.Equals(currentAction.Item3.holder))];
                        stackStates.Push(newPush);
                        /*
                         * Добавление
                         */
                        if (currentAction.Item3.var.Count == 1)
                        {
                            if (lexems.Contains(currentAction.Item3.var[0]))
                                resultStack.Push(new CAstNode(stackTokens.Pop().ToString()));
                        }
                        else
                        {
                            var dauthers = new List<CAstNode>();
                            var x = new List<string>(currentAction.Item3.var);
                            x.Reverse();
                            foreach (var tok in x)
                            {
                                if (lexems.Contains(tok))
                                    dauthers.Add(new CAstNode(stackTokens.Pop().ToString()));
                                else
                                    dauthers.Add(resultStack.Pop());
                            }
                            dauthers.Reverse();
                            resultStack.Push(new CAstNode(currentAction.Item3.holder) { dauthers = dauthers });
                        }

                        break;
                    case Actions.ACCEPT:
                        loop = false;
                        break;
                    case Actions.ERROR:
                        throw new Exception("Syntax Error at " + pos.Current.Coords.ToString());
                }

            }
            if (pos.MoveNext())
                throw new Exception("Файл не закончился((");
            if (resultStack.Count != 1)
            {
                throw new ArgumentException("Error Optimize. Stack size =" + resultStack.Count);
            }
            else
            {
                var tree = resultStack.Pop();
                tree.name = "Program";
                tree.Print(0);
            }
        }
    }


}