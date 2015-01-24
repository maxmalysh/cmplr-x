using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NS_SyntaxTableBuilder
{
    public enum Lexem
    {
        /*(0)       idents  */
        IDENT = 0,
        /*(1-3)     values  */
        INT_CONST = 1,
        FLOAT_CONST = 2,
        STRING_LITERAL = 3,
        /*(4-11)    types   */
        CHAR = 4,
        DOUBLE = 5,
        FLOAT = 6,
        INT = 7,
        LONG = 8,
        SHORT = 9,
        VOID = 10,
        BYTE = 11,
        /*(12-14)   prefix  */
        CONST = 12,
        SIGNED = 13,
        UNSIGNED = 14,
        /*(15-16)   if  */
        IF = 15,
        ELSE = 16,
        /*(17-19)   switch  */
        SWITCH = 17,
        CASE = 18,
        DEFAULT = 19,
        /*(20-24)   loop  */
        FOR = 20,
        DO = 21,
        WHILE = 22,
        BREAK = 23,
        CONTINUE = 24,
        /*(25-27)   ( ) return */
        FUNC_ARG_LEFT = 25,
        FUNC_ARG_RIGHT = 26,
        RETURN = 27,
        /*(28-32)   + - / * % */
        OP_ADD = 28,
        OP_SUB = 29,
        OP_DIV = 30,
        OP_MUL = 31,
        OP_PROCENT = 32,
        /*(33-38)   = +=... */
        ASSIGN = 33,
        ASSIGN_ADD = 34,
        ASSIGN_SUB = 35,
        ASSIGN_MUL = 36,
        ASSIGN_DIV = 37,
        ASSIGN_PROCENT = 38,
        /*(39-43)   &=... */
        ASSIGN_AND = 39,
        ASSIGN_OR = 40,
        ASSIGN_XOR = 41,
        ASSIGN_RIGHT = 42,
        ASSIGN_LEFT = 43,
        /*(44-45)   ++, -- */
        INC = 44,
        DEC = 45,
        /*(46-51)   &, |, ^, >>, << !*/
        OP_AND = 46,
        OP_OR = 47,
        OP_XOR = 48,
        OP_RIGHT = 49,
        OP_LEFT = 50,
        OP_NOT = 51,
        /*(52-59)   ==,!=,>=,<=,&&,||, <, >*/
        LOGIC_EQ = 52,
        LOGIC_NOT_EQ = 53,
        LOGIC_NOT_S = 54,
        LOGIC_NOT_L = 55,
        LOGIC_AND = 56,
        LOGIC_OR = 57,
        LOGIC_S = 58,
        LOGIC_L = 59,
        /*(60-62)   [], -> */
        PTR_MASS_LEFT = 60,
        PTR_MASS_RIGHT = 61,
        PTR_LINK = 62,
        /*(63-68)   Utils { } ; , . : */
        BLOCK_LEFT = 63,
        BLOCK_RIGHT = 64,
        SEMICOLON = 65,
        COMMA = 66,
        POINT = 67,
        DOUBLE_POINT = 68,
        /*(69-71)   Sub Lexem */
        START = 69,
        ERROR = 70,
        END = 71
    }

    public class TableBuilder
    {
        string grammar;
        readonly string startName = "::Start::";

        string startRule = String.Empty;
        Lexem[] terms = (Lexem[])Enum.GetValues(typeof(Lexem));
        List<Rule> rules = new List<Rule>();
        
        Tuple<Actions, int, LRItem>[,] tableAction;
        int[,] tableGoto;

        string GrammarName = String.Empty;

        /// <summary>
        /// Токены берутся из enum Lexem.
        /// </summary>
        /// <param name="grammar">Входная грамматика. Первое правило - начальное правило! Первая строка: %grammarName</param>
        public TableBuilder(string grammar)
        {
            this.grammar = grammar;

            GrammarName = grammar.Split(new char[] { '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0].Substring(1);

            try 
            {
                var grammarTables = Directory.GetFiles("Grammar\\" + GrammarName + "\\");
                if (grammarTables.Count() != 3)
                    throw new IOException();
                Array.Sort(grammarTables);
                Console.WriteLine("Grammar " + GrammarName + " exists. Start to load tables...");

                foreach (var tableName in grammarTables) 
                {
                    switch (tableName.Substring(tableName.LastIndexOf('.')))
                    { 
                        case ".action":
                            Console.Write("Start loading action table...");
                            tableAction = JsonConvert.DeserializeObject<Tuple<Actions, int, LRItem>[,]>(File.ReadAllText(tableName));
                            Console.WriteLine("finished.");
                            break;
                        case ".goto":
                            Console.Write("Loading goto table...");
                            tableGoto = JsonConvert.DeserializeObject<int[,]>(File.ReadAllText(tableName));
                            Console.WriteLine("finished.");
                            break;
                        case ".rules":
                            Console.Write("Loading rules...");
                            rules = JsonConvert.DeserializeObject<Rule[]>(File.ReadAllText(tableName)).ToList();
                            Console.WriteLine("finished.");
                            break;
                    }
                    
                }
                startRule = rules[0].name;
                Console.WriteLine("Grammar loading finished!");
                return;
            }
            catch(Exception e)
            {
                if (!(e is DirectoryNotFoundException || e is IOException))
                    throw;
            }

            Console.WriteLine("Grammar "+ GrammarName+" does not exist. Initialize rools.");
            grammar = grammar.Substring(grammar.IndexOf('\n')+1);
            // Парсим список правил
            Rule current = null;
            List<string> lineRule = null;

            foreach (var word in grammar.Split(new char[] { '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (word)
                {
                    case ":":
                        lineRule = new List<string>();
                        break;
                    case "|":
                        current.vars.Add(lineRule);
                        lineRule = new List<string>();
                        break;
                    case ";":
                        current.vars.Add(lineRule);
                        rules.Add(current);
                        current = null;
                        break;
                    default:
                        if (current == null)
                            current = new Rule() { vars = new List<List<string>>(), name = word };
                        else
                            lineRule.Add(word);
                        break;
                }
            }

            if (rules.Count == 0)
                throw new ArgumentException("В грамматике нет правил");
            else
                startRule = rules[0].name;

            rules.Add(new Rule()
            {
                name = startName,
                vars = new List<List<string>>() { 
                    new List<string>() { startRule } 
                },
                follow = new List<Lexem>() { Lexem.END }
            });


        }


        /// <summary>
        /// Строит таблицы
        /// </summary>
        /// <param name="tableAction">перенос.свёртка.допуск.ошибка</param>
        /// <param name="tableGoto">int - номер, куда состоится переход</param>
        public void getTables(out List<Rule> rules,
                                out Tuple<Actions, int, LRItem>[,] tableAction,
                                out int[,] tableGoto)
        {
            if (this.tableAction != null)
            {
                rules = this.rules;
                tableAction = this.tableAction;
                tableGoto = this.tableGoto;
                return;
            }

            Console.WriteLine("Start to build state machine...");
            //First & Follow
            rules = this.rules;
            rules.ForEach(rule => rule.first = First(rule));

            bool loop = true;
            while (loop)
            {
                loop = false;
                rules.ForEach(rule => { loop |= Follow(rule); });
            }
            //////////////////////////////////////////////////////////////
            var Q = Items();
            Console.WriteLine("State machine builded!\nStart to build tables...");
            var lexems = Enum.GetNames(typeof(Lexem));

            var action = new Tuple<Actions, int,  LRItem>[Q.Count,lexems.Count()];
            var GOTO = new int[Q.Count, rules.Count];

            for (int i = 0; i < Q.Count; i++ )
            {
                var q = Q[i];
                for(int j = 0; j < lexems.Count(); j++)
                {
                    var a = lexems[j];
                    List<LRItem> nextQ = null;

                    q.Where(item => item.cursor != item.var.Count && item.var[item.cursor].Equals(a)).ToList().ForEach(item =>
                    {
                        if (nextQ == null)
                            nextQ = Goto(q, a);

                        var index = Q.FindIndex(x => CompareStates(nextQ, x));
                       
                        if (index != -1)
                        {
                            if (action[i,j] != null)
                            {
                                if (action[i,j].Item1 == Actions.REDUCE)
                                    Console.WriteLine(
                                                "Conflict SHIFT/"
                                                + action[i,j].Item1.ToString()
                                                + "\nLRItem: " + item.ToString()
                                                + "\n\t" + action[i,j].Item3.ToString()
                                                + ". Priority: REDUCE");
                                else if (action[i,j].Item1 == Actions.SHIFT) 
                                {
                                    if (action[i,j].Item2 != index)
                                    {
                                            throw new Exception("FATAL Conflict numbers SHIFT/"
                                                + action[i,j].Item1.ToString()
                                                + "\nLRItem: " + item.ToString()
                                                + "\n\t" + action[i,j].Item3.ToString()
                                                + ".");
                                    }
                                }
                            }
                            else
                            {
                                action[i,j] = new Tuple<Actions, int, LRItem>(Actions.SHIFT, index, item);
                            }
                        }
                        else
                        {
                            throw new IndexOutOfRangeException("index bad!!");
                        }
                    });

                    q.Where(item => item.cursor == item.var.Count && item.prefix.ToString().Equals(a)).ToList().ForEach( item =>
                    {
                        var newTuple = new Tuple<Actions, int, LRItem>(Actions.REDUCE, -1, item);

                        if (action[i,j] != null
                                && !action[i,j].Equals(newTuple))
                        {
                            if (action[i,j] != null && action[i,j].Item1 != Actions.ACCEPT)
                                Console.WriteLine(
                                                "Conflict REDUCE/"
                                                + action[i,j].Item1.ToString()
                                                + "\nLRItem: " + item.ToString()
                                                + "\n\t&\n" + action[i,j].Item3.ToString()
                                                + ". Priority: SECOND");
                        }
                        else 
                            action[i,j] = newTuple;
                        
                    });
                    if (q.Exists(item => item.holder.Equals(startName) && item.cursor == item.var.Count))
                        action[i,(int)Lexem.END] = new Tuple<Actions, int, LRItem>(Actions.ACCEPT, -1, null);
                }
                for (int j = 0; j < rules.Count; j++) 
                {
                    var A = rules[j].name;
                    var next = q.Find(item => item.var.Count != item.cursor && item.var[item.cursor].Equals(A));
                    if (next != null)
                    {
                        var nextQ = Goto(q, A);
                        var index = Q.FindIndex(x => CompareStates(nextQ, x));
                        if (index == -1)
                        {
                            throw new IndexOutOfRangeException();
                        }

                        GOTO[i,j] = index;
                    }
                    else
                        GOTO[i,j] = -1;
                }
            }

            tableAction = new Tuple<Actions, int, LRItem>[Q.Count, lexems.Count()];
            tableGoto = new int[Q.Count, rules.Count];
            for (int i = 0; i < Q.Count; i++)
            {
                for (int j = 0; j < lexems.Count(); j++ )
                    tableAction[i,j] = action[i, j];

                for (int j = 0; j < rules.Count; j++ )
                    tableGoto[i, j] = GOTO[i, j];
            }
            
            Console.WriteLine("Tables builded. Save them:)");

            Directory.CreateDirectory("Grammar\\" + GrammarName);

            var writeStream = File.CreateText("Grammar\\" + GrammarName + "\\" + GrammarName + ".action");
            writeStream.Write(JsonConvert.SerializeObject(action));
            writeStream.Close();
            writeStream = File.CreateText("Grammar\\" + GrammarName + "\\" + GrammarName + ".goto");
            writeStream.Write(JsonConvert.SerializeObject(GOTO));
            writeStream.Close();
            writeStream = File.CreateText("Grammar\\" + GrammarName + "\\" + GrammarName + ".rules");
            writeStream.Write(JsonConvert.SerializeObject(rules));
            writeStream.Close();

            Console.WriteLine("All tables Saved!");
        }

        private List<LRItem> Closure(List<LRItem> mass)
        {
            var result = new List<LRItem>(mass); // I

            for (int i = 0; i < result.Count; i++)
            {
                //для каждой ситуации [A -> alpha .B beta, a] из result
                var item = result[i];
                if (item.cursor >= item.var.Count || !rules.Exists(x => x.name.Equals(item.var[item.cursor])))
                    continue;

                // для каждого правила вывода B -> gamma из rules
                rules.Find(x => x.name.Equals(item.var[item.cursor])).vars.ForEach(var =>
                {
                    var subQuery = new List<string>();
                    for (int j = item.cursor + 1; j < item.var.Count; j++)
                        subQuery.Add(item.var[j]);

                    subQuery.Add(item.prefix.ToString());

                    // для каждого терминала b из FIRST(beta a)
                    FindFirst(subQuery).ForEach(terminal =>
                    {
                        // если [B ->.gamma, b] нет в result
                        if (!result.Exists(x =>
                                            x.prefix == terminal
                                            && x.cursor == 0
                                            && x.var.Equals(var)
                                            && x.holder.Equals(item.var[item.cursor])
                            ))
                        {
                            result.Add(new LRItem()
                            {
                                holder = item.var[item.cursor],
                                cursor = 0,
                                var = var,
                                prefix = terminal
                            });
                        }

                    });
                });
            }

            return result;
        }

        private List<LRItem> Goto(List<LRItem> mass, string symbol)
        {
            
            return Closure(
                    mass.Where(x => 
                            x.cursor < x.var.Count 
                            && x.var[x.cursor].Equals(symbol))
                        .Select(x => x.IncCursor()).ToList()
                        );
        }

        private List<List<LRItem>> Items()
        {
            var G = new List<LRItem>();
            G.Add(new LRItem()
            {
                cursor = 0,
                prefix = Lexem.END,
                holder = rules.Last().name,
                var = rules.Last().vars[0]
            });
            
            var result = new List<List<LRItem>>();
            result.Add(Closure(G));
            var symbols = Enum.GetNames(typeof(Lexem)).Union(rules.Select(x => x.name));
            for (int i = 0; i < result.Count; i++)
            {
                var vertex = result[i];
                symbols
                    .Select(l => new { fgt = Goto(vertex, l) , sbl = l })
                        .Where( v => v.fgt.Count != 0 && !result.Exists(x => CompareStates(x, v.fgt)))
                        .ToList()
                        .ForEach(
                        newVertex => result.Add(newVertex.fgt));

            }
            
            return result;
        }

        private bool CompareStates(List<LRItem> state1, List<LRItem> state2)
        {
            if (state1.Count != state2.Count)
                return false;

            for (int i = 0; i < state2.Count; i++)
                if (!state1.Exists(x => x.Equals(state2[i])))
                    return false;

            return true;
        }

        private bool Follow(Rule rule)
        {
            bool loop = false;

            rules.ForEach(x =>
            {
                foreach (List<string> var in x.vars.Where(var => var.Contains(rule.name)))
                {
                    int next = var.IndexOf(rule.name) + 1;
                    Lexem lexem;

                    if (next == var.Count)
                    {
                        foreach (Lexem item in x.follow)
                        {
                            if (!rule.follow.Contains(item))
                            {
                                rule.follow.Add(item);
                                loop = true;
                            }
                        }
                    }
                    else if (Enum.TryParse(var[next], out lexem))
                    {
                        if (!rule.follow.Contains(lexem))
                        {
                            rule.follow.Add(lexem);
                            loop = true;
                        }
                    }
                    else
                    {
                        Rule A = rules.Find(i => i.name.Equals(var[next]));
                        if (A == null)
                            throw new Exception("name: " + var[next]);
                        foreach (Lexem item in A.first)
                        {
                            if (item == Lexem.ERROR)
                            {
                                foreach (Lexem item1 in A.follow)
                                {
                                    if (!rule.follow.Contains(item1))
                                    {
                                        rule.follow.Add(item1);
                                        loop = true;
                                    }
                                }
                            }
                            else if (!rule.follow.Contains(item))
                            {
                                rule.follow.Add(item);
                                loop = true;
                            }
                        }
                    }
                }
            });

            return loop;
        }

        private List<Lexem> FindFirst(List<string> var)
        {
            List<Lexem> result;
            Lexem lexem;

            if (Enum.TryParse(var[0], out lexem))
            {
                result = new List<Lexem>();
                result.Add(lexem);
            }
            else
            {
                result = rules.Find(x => x.name == var[0]).first;
                if (result.Contains(Lexem.ERROR))
                    result.AddRange(
                        FindFirst(
                            var.GetRange(1, var.Count - 1)
                        )
                    );
            }

            return result;
        }

        private List<Lexem> First(Rule rule)
        {
            if (rule == null)
                return null;

            var result = rule.first;

            rule.vars.ForEach(var =>
            {
                if (var.Count == 0)
                {
                    //Добавление epsilon к списку first
                    if (result.IndexOf(Lexem.ERROR) == -1) result.Add(Lexem.ERROR);
                }
                else
                {
                    bool IsEmpty = false;
                    foreach (string item in var)
                    {
                        if (item.Equals(rule.name))
                            break;

                        Lexem lexem;
                        if (Enum.TryParse<Lexem>(item, out lexem))
                        {
                            if (result.IndexOf(lexem) == -1)
                                result.Add(lexem);
                            break;
                        }
                        var newRule = rules.Find(x => x.name.Equals(item));
                        if (newRule == null)
                            throw new ArgumentException("Не существует токена " + item +
                                                        " в правиле " + rule.name);
                        var newF = newRule.first.Count == 0 ? First(newRule) : newRule.first;

                        if (newF == null)
                            throw new Exception("Unknow name: " + item);
                        foreach (Lexem l in newF)
                        {
                            if (!result.Contains(l))
                                result.Add(l);
                            else if (l == Lexem.ERROR)
                                IsEmpty = true;
                        }

                        if (!IsEmpty)
                            break;
                    }
                }

            });

            return result;
        }

    }

    /*
     *  UTILS
     */
    public enum Actions { SHIFT, REDUCE, ACCEPT, ERROR }

    public class Rule
    {
        public string name;
        public List<List<string>> vars;
        public List<Lexem> first = new List<Lexem>(), follow = new List<Lexem>();
    }

    [Serializable()]
    public class LRItem
    {
        public string holder;
        public List<string> var;
        public int cursor;
        public Lexem prefix;

        public override bool Equals(object obj)
        {
            if (obj is LRItem)
            {
                var other = (LRItem)obj;
                return
                    holder.Equals(other.holder)
                    && cursor == other.cursor
                    && prefix == other.prefix
                    && var == other.var;
            }
            else
                return base.Equals(obj);
        }

        public override string ToString()
        {
            var result =  holder + " ->";
            for(int i = 0; i < var.Count;i++)
                result += (i == cursor ? " * " : " ") + var[i];
            if (cursor == var.Count)
                result += " *";
            return result + ", " + prefix ;
        }

        public LRItem IncCursor()
        {
            return new LRItem()
            {
                holder = holder,
                var = var,
                cursor = cursor + 1,
                prefix = prefix
            };
        }
    }
}
