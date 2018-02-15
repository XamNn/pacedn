using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pacednl;

//pacednc compiles source files to Library objects (implemented in pacednl)

namespace pacednc
{
    public static class Info
    {
        public static string Version = "pacednc 1N1";
    }
    static class Program
    {
        public static void Main(string[] args)
        {

        }
    }
    public static class Compiler
    {
        //convert a text file to a pace library

        static string IdentifierCharMatch = @"\w";
        static string LowOperatorCharMatch = @"[-+@!#¤%£$€´`<>~]";
        static string HighOperatorCharMatch = @"[*\/|&^]";

        static StatementPattern[] StatementPatterns = null;

        public static void Compile(string Source, string LocalPath)
        {
            //Tokenize
            Token[] Tokens = Tokenize(Source, new List<(Regex, TokenType)>
            {
                (new Regex(@"^element"), TokenType.ElementWord),
                (new Regex(@"^class"), TokenType.ClassWord),
                (new Regex(@"^struct"), TokenType.StructWord),
                (new Regex(@"^value"), TokenType.ValueWord),
                (new Regex(@"^null"), TokenType.NullWord),
                (new Regex(@"^object"), TokenType.ObjectWord),
                (new Regex(@"^enum"), TokenType.EnumWord),
                (new Regex(@"^get"), TokenType.GetWord),
                (new Regex(@"^set"), TokenType.SetWord),
                (new Regex(@"^alias"), TokenType.AliasWord),
                (new Regex(@"^if"), TokenType.IfWord),
                (new Regex(@"^else"), TokenType.ElseWord),
                (new Regex(@"^break"), TokenType.BreakWord),
                (new Regex(@"^continue"), TokenType.ContinueWord),
                (new Regex(@"^is"), TokenType.IsWord),
                (new Regex(@"^to"), TokenType.ToWord),
                (new Regex(@"^import"), TokenType.ImportWord),
                (new Regex(@"^use"), TokenType.UseWord),
                (new Regex(@"^main"), TokenType.MainWord),
                (new Regex(@"^func"), TokenType.FuncWord),
                (new Regex(@"^public"), TokenType.PublicWord),
                (new Regex(@"^private"), TokenType.PrivateWord),
                (new Regex(@"^visible"), TokenType.VisibleWord),
                (new Regex(@"^this"), TokenType.ThisWord),
                (new Regex(@"^initialize"), TokenType.InitializeWord),
                (new Regex(@"^finalize"), TokenType.FinalizeWord),
                (new Regex(@"^each"), TokenType.EachWord),
                (new Regex(@"^explicit"), TokenType.ExplicitWord),
                (new Regex(@"^implicit"), TokenType.ImplicitWord),
                (new Regex(@"^automatic"), TokenType.AutomaticWord),

                (new Regex(@"^\("), TokenType.PrimaryOpen),
                (new Regex(@"^\)"), TokenType.PrimaryClose),
                (new Regex(@"^\["), TokenType.SecondaryOpen),
                (new Regex(@"^]"), TokenType.SecondaryClose),
                (new Regex(@"^\{"), TokenType.TertiaryOpen),
                (new Regex(@"^}"), TokenType.TertiaryClose),

                (new Regex(@"^\."), TokenType.Period),
                (new Regex(@"^;"), TokenType.Semicolon),
                (new Regex(@"^,"), TokenType.Comma),
                (new Regex(@"^="), TokenType.Equals),
                (new Regex(@"^:"), TokenType.Colon),
                (new Regex(@"^=>"), TokenType.Lambda),
                (new Regex(@"^@"), TokenType.At),
                (new Regex(@"^\?"), TokenType.QuestionMark),
                (new Regex(@"^\$"), TokenType.Dollar),

                (new Regex(@"^" + LowOperatorCharMatch + LowOperatorCharMatch + @"?"), TokenType.LowOperator),
                (new Regex(@"^" + HighOperatorCharMatch + HighOperatorCharMatch + @"?"), TokenType.HighOperator),

                (new Regex(@"^[0-9]+"), TokenType.DecInteger),
                (new Regex(@"^[0-9]+\.[0-9]+"), TokenType.DecNonInteger),
                (new Regex(@"^0[bB][01]+"), TokenType.BinInteger),
                (new Regex(@"^0[xX][0-9a-fA-F]+"), TokenType.HexInteger),

                (new Regex(@"^" + IdentifierCharMatch + @"*"), TokenType.Word),

                (new Regex("^global::"), TokenType.Global)
            });

            for (int i = 0; i < Tokens.Length; i++)
            {
                Console.WriteLine($"{$"{Tokens[i].Place.Line} : {Tokens[i].Place.Index}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
            }

            //Parse
            StatementPatterns = new StatementPattern[]
            {
                new StatementPattern("t|=s", StatementType.Main, new[]{ TokenType.MainWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Element, new[]{ TokenType.ElementWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Struct, new[]{ TokenType.StructWord }, null, null, null),
                new StatementPattern("t|=n", StatementType.Enum, new[]{ TokenType.EnumWord }, null, null, null),

                new StatementPattern("t|!=i|=s", StatementType.Label, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, null),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, null),
                new StatementPattern("t|!t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, new[]{ "(", ")" }, null),
                new StatementPattern("t|=s", StatementType.Else, new[]{ TokenType.ElseWord }, null, null, null),

                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Set }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PrivateWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.PublicWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.AliasWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.GetWord }, null, null, new object[] { VariableFlags.Get }),
                new StatementPattern("t|=n|=n|!e", StatementType.Decleration, new[] { TokenType.SetWord }, null, null, new object[] { VariableFlags.Set }),

                new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, null, null),

                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ExplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ImplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.AutomaticWord, TokenType.ToWord, TokenType.Equals }, null, null, null),

                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, new object[] { false }),
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.UseWord }, null, null, new object[] { true }),
                new StatementPattern("t|!=i|!t|!=q|!t|!e", StatementType.TranslatorNote, new[]{ TokenType.Dollar, TokenType.PrimaryOpen, TokenType.String, TokenType.PrimaryClose }, null, new[]{ "$", "(", ")" }, null),

                new StatementPattern("=n|e", StatementType.Node, null, null, null, null),
                new StatementPattern("=n|=n|!e", StatementType.Decleration, null, null, null, new object[] { new VariableFlags() }),

            };

            List<Statement> Statements = new List<Statement>();
            {
                int i = 0;
                int lasti = Tokens.Length - 1;
                while (true)
                {
                    if (i == lasti) break;
                    Statements.Add(NextStatement(Tokens, ref i, StatementPatterns));
                }
            }
            ;

            Library Library = new Library { Name = "CompiledLibrary" };
            Project.Current.Libraries.Add(Library);
            Profile Profile = new Profile();
            Library.Profiles.Add(Profile);

            List<Library> UsedLibraries = new List<Library>();
            UsedLibraries.Add(Library);

            //dependencies
            for (int i = 0; i < Statements.Count; i++)
            {
                if (Statements[i].StatementType != StatementType.Import) break;
                string n = (string)Statements[i].Data[1];
                Profile.Dependencies.Add(n);
                var l = new Library();
                var or = l.Read(Config.FormatLibraryFilename(n, LocalPath, true));
                if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Statements[i].Place, or.Message);
                or = Project.Current.Import(l, true);
                if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Statements[i].Place, or.Message);
            }
        }

        //Errors
        static bool HasErrors = false;
        enum Error
        {
            Unknown0,
            InvalidCLArgs0,
            FileNotFound1,
            CharacterIllegal1,
            TokenExpected2,
            IdentifierExpected1,
            TokenIllegal1,
            StringExpected1,
            ItemDefiedMultipleLibraries1,
            ItemIllegalContext2,
            ExecutableNotInExecutableOrGlobal0,
            AccessModIllegalForItem0,
            MultipleDefined1,
            MultipleMain0,
            OperationResultError1,
        }
        static string GetErrorMessage(Error e, params string[] args)
        {
            switch (e)
            {
                case Error.Unknown0: return "Unknown error occured.";
                case Error.InvalidCLArgs0: return "Invalid command-line arguments.";
                case Error.FileNotFound1: return $"Dependency not found '{args[0]}'.";
                case Error.CharacterIllegal1: return $"The character '{args[0]}' does not match any token.";
                case Error.TokenExpected2: return $"Expected '{args[0]}', instead got '{args[1]}'.";
                case Error.IdentifierExpected1: return $"Expected identifer, instead got '{args[0]}'.";
                case Error.TokenIllegal1: return $"The token '{args[0]}' is not legal in this context.";
                case Error.StringExpected1: return $"String expected, instead got '{args[0]}'.";
                case Error.ItemDefiedMultipleLibraries1: return $"The item '{args[0]}' is defined in multiple libraries.";
                case Error.ItemIllegalContext2: return $"Items of type {args[0]} cannot appear in {args[0]} context.";
                case Error.ExecutableNotInExecutableOrGlobal0: return "Executable code may only appear in executable blocks.";
                case Error.AccessModIllegalForItem0: return "Access modifiers are not valid for this item.";
                case Error.MultipleDefined1: return $"'{args[0]}' is defined more than once.";
                case Error.MultipleMain0: return "Multiple entry points defined.";
                case Error.OperationResultError1: return $"Operation result error: {args[0]}.";
            }
            return "!!! Error message not defined !!!";
        }
        struct Place
        {
            public ushort Line;
            public ushort Index;
        }
        enum ThrowType
        {
            Fatal,
            Error,
            Warning,
            Message,
        }
        static void Throw(Error e, ThrowType tt, Place? p, params string[] args)
        {
            switch (tt)
            {
                case ThrowType.Fatal:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("Fatal error");
                    break;
                case ThrowType.Error:
                    HasErrors = true;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error");
                    break;
                case ThrowType.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Warning");
                    break;
                case ThrowType.Message:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Message");
                    break;
            }
            if (p != null)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("├ ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("At " + p.Value.Line + " : " + p.Value.Index);
            }
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("└ ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(GetErrorMessage(e, args));
            if(tt == ThrowType.Error)
                HasErrors = true;
            else if (tt == ThrowType.Fatal)
                Environment.Exit((int)e);
            Console.WriteLine();
        }

        //Tokens
        enum TokenType : byte
        {
            None,
            EndOfFile,

            //General
            Word,
            LowOperator,
            HighOperator,

            //Reserved operators
            Period,
            Semicolon,
            Comma,
            Equals,
            Colon,
            Lambda,
            At,
            QuestionMark,
            Dollar,

            //Braces
            PrimaryOpen,
            PrimaryClose,
            SecondaryOpen,
            SecondaryClose,
            TertiaryOpen,
            TertiaryClose,

            //Number literals
            DecInteger,
            DecNonInteger,
            BinInteger,
            HexInteger,

            //String literals
            String,

            //Reserved words
            ElementWord,
            ClassWord,
            StructWord,
            ValueWord,
            NullWord,
            ObjectWord,
            EnumWord,
            GetWord,
            SetWord,
            AliasWord,
            IfWord,
            ElseWord,
            BreakWord,
            ContinueWord,
            IsWord,
            ToWord,
            ImportWord,
            UseWord,
            MainWord,
            FuncWord,
            PublicWord,
            PrivateWord,
            VisibleWord,
            ThisWord,
            InitializeWord,
            FinalizeWord,
            EachWord,
            ExplicitWord,
            ImplicitWord,
            AutomaticWord,
            OperatorWord,

            //Other
            Global, //global::
        }
        struct Token
        {
            public TokenType TokenType;
            public string Match;
            public Place Place;
        }
        static Token[] Tokenize(string text, List<(Regex, TokenType)> matches)
        {
            StringBuilder sb = new StringBuilder(text);
            List<Token> l = new List<Token>();
            ushort index = 1;
            ushort line = 1;
            bool instringinsertion = false;
            ushort stringinsertions = 0;
            Place GetPlace() => new Place { Index = index, Line = line };
            while (true)
            {
                if (sb.Length == 0) break;
                char c = sb[0];
                if (c == ' ')
                {
                    index++;
                    sb.Remove(0, 1);
                    continue;
                }
                if (c == '\n')
                {
                    index = 1;
                    line++;
                    sb.Remove(0, 1);
                    continue;
                }
                if (c == '\t')
                {
                    index += 4;
                    sb.Remove(0, 1);
                    continue;
                }
                if (c == '\r')
                {
                    sb.Remove(0, 1);
                    continue;
                }
                if (c == '"' || (instringinsertion && c == '}'))
                {
                    if (c == '}')
                    {
                        stringinsertions--;
                        l.Add(new Token { Match = "+", TokenType = TokenType.LowOperator, Place = GetPlace() });
                    }
                    instringinsertion = false;
                    ushort startindex = index;
                    ushort startline = line;
                    if (instringinsertion)
                    {
                        instringinsertion = false;
                    }
                    int i = 0;
                    while (true)
                    {
                        i++;
                        index++;
                        if (i == sb.Length)
                        {
                            Throw(Error.TokenExpected2, ThrowType.Error, GetPlace(), "\"", "END OF FILE");
                            break;
                        }
                        if (sb[i] == '"' && sb[i - 1] != '\\')
                        {
                            index++;
                            i++;
                            break;
                        }
                        else if (sb[i] == '\n')
                        {
                            index = 1;
                            line++;
                        }
                        else if (sb[i] == '\t') index += 4;
                        else if (sb[i] == '{')
                        {
                            for (int x = 0; x < sb.Length; x++)
                            {
                                if (sb[x] == ' ') continue;
                                if (sb[x] == '}') break;
                                goto foolbl;
                            }
                            continue;
                            foolbl:;
                            i++;
                            index++;
                            instringinsertion = true;
                            stringinsertions++;
                            break;
                        }
                    }
                    l.Add(new Token { TokenType = TokenType.String, Match = sb.ToString(0, i), Place = new Place { Index = startindex, Line = startline } });
                    sb.Remove(0, i);
                    if (instringinsertion) l.Add(new Token { TokenType = TokenType.LowOperator, Match = "+", Place = GetPlace() });
                    else if (stringinsertions != 0) instringinsertion = true;
                    continue;
                }
                if (c == '/' && sb.Length != 1)
                {
                    if (sb[1] == '/')
                    {
                        int i = 2;
                        while (true)
                        {
                            if (i == sb.Length || sb[i] == '\n') break;
                            i++;
                            index++;
                        }
                        sb.Remove(0, i);
                        continue;
                    }
                    if (sb[1] == '*')
                    {
                        int i = 2;
                        while (true)
                        {
                            if (i == sb.Length) break;
                            if (i != sb.Length + 1 && sb[i] == '*' && sb[i + 1] == '/')
                            {
                                i += 2;
                                index += 2;
                                break;
                            }
                            if (i == '\n')
                            {
                                index = 1;
                                line++;
                            }
                            else index++;
                            i++;
                        }
                        sb.Remove(0, i);
                        continue;
                    }
                }
                string longest = string.Empty;
                TokenType tt = TokenType.None;
                foreach (var m in matches)
                {
                    var cm = m.Item1.Match(sb.ToString());
                    if (!cm.Success) continue;
                    if (cm.Length > longest.Length)
                    {
                        longest = cm.Value;
                        tt = m.Item2;
                    }
                }
                if (tt == TokenType.None)
                {
                    Throw(Error.CharacterIllegal1, ThrowType.Error, GetPlace(), sb.ToString(0, 1));
                    sb.Remove(0, 1);
                    index++;
                }
                else
                {
                    l.Add(new Token { TokenType = tt, Match = sb.ToString(0, longest.Length), Place = GetPlace() });
                    sb.Remove(0, longest.Length);
                    index += (ushort)longest.Length;
                }
            }
            l.Add(new Token { TokenType = TokenType.EndOfFile, Place = GetPlace(), Match = "END OF FILE" });
            return l.ToArray();
        }

        //Parsing
        enum StatementType : byte
        {
            //Specials
            Scope, //List<Statement>

            //Matched
            No_op,

            Element,
            Class,
            Struct,
            Enum,

            Convertion,

            Continue,
            Break,

            Label,

            Node,
            Decleration,
            Alias,

            EmptyFunction,
            UntypedFunction,
            NonreturningTypedFunction,
            ReturningTypedFunction,

            Main,
            Initialize,
            Finalize,

            If,
            Else,

            Each,

            TranslatorNote,
            SupportsTranslatorNote,
            Import,
        }
        [Flags]
        enum VariableFlags : byte
        {
            Get = 0b000001,
            Set = 0b000010,
            GetPublic = 0b000100,
            SetPublic = 0b001000,
        }
        class Statement
        {
            public StatementType StatementType;
            public Place Place;
            public object[] Data;
        }
        static void RefNodeList(Token[] l, ref int i, ref List<Node> nl, TokenType end, string endtokenmatch)
        {
            if (l[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add(NextNode(l, ref i));
                    if (l[i].TokenType == TokenType.Comma) i++;
                    else if (l[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, l[i].Place, endtokenmatch + "' or ',", l[i].Match); break; }
                }
        }
        static List<Node> NodeList(Token[] l, ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<Node>();
            RefNodeList(l, ref i, ref x, end, endtokenmatch);
            return x;
        }
        static void RefDualNodeList(Token[] l, ref int i, ref List<(Node, Node)> nl, TokenType end, string endtokenmatch)
        {
            if (l[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add((NextNode(l, ref i), NextNode(l, ref i)));
                    if (l[i].TokenType == TokenType.Comma) i++;
                    else if (l[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, l[i].Place, endtokenmatch + "' or ',", l[i].Match); break; }
                }
        }
        static List<(Node, Node)> DualNodeList(Token[] l, ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<(Node, Node)>();
            RefDualNodeList(l, ref i, ref x, end, endtokenmatch);
            return x;
        }
        static bool NodeListOrDualNodeList(Token[] l, ref int i, out List<Node> SingleNodeList, out List<(Node, Node)> DualNodeList, TokenType end, string endtokenmatch)
        {
            SingleNodeList = null;
            DualNodeList = null;
            if (l[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node>();
                return false;
            }
            var firstNode = NextNode(l, ref i);
            if (l[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node> { firstNode };
                return false;
            }
            if (l[i].TokenType == TokenType.Comma)
            {
                i++;
                SingleNodeList = new List<Node> { firstNode };
                RefNodeList(l, ref i, ref SingleNodeList, end, endtokenmatch);
                return false;
            }
            var secondNode = NextNode(l, ref i);
            DualNodeList = new List<(Node, Node)> { (firstNode, secondNode) };
            if (l[i].TokenType == TokenType.Comma) i++;
            else if (l[i].TokenType == end)
            {
                i++;
                return true;
            }
            else Throw(Error.TokenExpected2, ThrowType.Error, l[i].Place, endtokenmatch + "' or ',' expected", l[i].Match);
            RefDualNodeList(l, ref i, ref DualNodeList, end, endtokenmatch);
            return true;
        }
        static List<string> IdentifierList(Token[] l, ref int i, TokenType end, string endtokenmatch)
        {
            List<string> x = new List<string>();
            if (l[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (l[i].TokenType == TokenType.Word)
                    {
                        x.Add(l[i].Match);
                        i++;
                    }
                    else Throw(Error.IdentifierExpected1, ThrowType.Error, l[i].Place, l[i].Match);
                    if (l[i].TokenType == TokenType.Comma) i++;
                    else if (l[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, l[i].Place, endtokenmatch + "' or ',", l[i].Match); break; }
                }
            return x;
        }
        struct StatementPattern
        {
            public string[] Clauses;
            public StatementType StatementType;
            public TokenType[] TokenTypes;
            public string[] Matches;
            public string[] Misc;
            public object[] Data;
            public StatementPattern(string format, StatementType statementType, TokenType[] tokenTypes, string[] matches, string[] misc, object[] data)
            {
                Clauses = format.Split('|');
                StatementType = statementType;
                TokenTypes = tokenTypes;
                Matches = matches;
                Misc = misc;
                Data = data;
            }
        }
        static Statement NextStatement(Token[] l, ref int i, StatementPattern[] patterns)
        {
            Statement s = new Statement { Place = l[i].Place };
            Error currentError = Error.Unknown0;
            string[] currentErrorArgs = null;
            foreach (var p in patterns)
            {
                int tokentypeindex = 0;
                int matchindex = 0;
                int miscindex = 0;
                List<object> data = p.Data == null ? new List<object>() : p.Data.ToList();
                int originali = i;
                foreach (var c in p.Clauses)
                {
                    bool dothrow = false;
                    bool save = false;
                    int ci = 0;
                    while (true)
                    {
                        if (c[ci] == '!') dothrow = true;
                        else if (c[ci] == '=') save = true;
                        else break;
                        ci++;
                    }
                    switch (c[ci])
                    {
                        case 't':
                            if (l[i].TokenType != p.TokenTypes[tokentypeindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Error.TokenExpected2;
                                    currentErrorArgs = new[] { p.Misc[miscindex++], l[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            i++;
                            break;
                        case 'i':
                            if (l[i].TokenType != TokenType.Word)
                            {
                                if (dothrow)
                                {
                                    currentError = Error.IdentifierExpected1;
                                    currentErrorArgs = new[] { l[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            if (save) data.Add(l[i].Match);
                            i++;
                            break;
                        case 'a':
                            if (save) data.Add(l[i].Match);
                            i++;
                            break;
                        case 'n':
                            {
                                var n = NextNode(l, ref i);
                                if (save) data.Add(n);
                                break;
                            }
                        case 's':
                            {
                                var st = NextStatement(l, ref i, patterns);
                                if (save) data.Add(st);
                                break;
                            }
                        case 'b':
                            {
                                if (l[i].TokenType == TokenType.TertiaryOpen) i++;
                                else
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { "{", l[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                List<Statement> stl = new List<Statement>();
                                while (true)
                                {
                                    if (l[i].TokenType == TokenType.TertiaryClose) break;
                                    if (l[i].TokenType == TokenType.EndOfFile)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { "}", l[i].Match };
                                        goto throww;
                                    }
                                    stl.Add(NextStatement(l, ref i, patterns));
                                }
                                i++;
                                if (save) data.Add(stl);
                                break;
                            }
                        case 'e':
                            {
                                if (l[i].TokenType != TokenType.Semicolon)
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { ";", l[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                i++;
                                break;
                            }
                        case 'q':
                            {
                                if (l[i].TokenType != TokenType.String)
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.StringExpected1;
                                        currentErrorArgs = new[] { l[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                if (save) data.Add(l[i].Match);
                                i++;
                                break;
                            }
                    }
                }
                s.StatementType = p.StatementType;
                s.Data = data.ToArray();
                return s;
                nextpattern:
                i = originali;
            }
            throww:
            Throw(currentError, ThrowType.Error, l[i].Place, currentErrorArgs);
            s.StatementType = StatementType.No_op;
            return s;
        }
        enum PrimaryNodeType : byte
        {
            None,
            Parentheses, //Node
            Identifier,
            Literal,
            UnaryOperator, //Node
            BinaryOperator, //(Node, Node)
            Assignment, //(Node, Node)
            Record,
            Collection, //List<Node>
            Function,
            FunctionType, //(Node?, List<Node>)
            EmptyParameterList,
            UntypedParameterList, //List<Node>
            TypedParameterList, //List<(Node, Node)>
            Object,
            Null,
        }
        struct Node
        {
            public PrimaryNodeType NodeType;
            public object Data;
            public Box<SecondaryNode> Child;
            public Place Place;
        }
        enum SecondaryNodeType : byte
        {
            None,
            Call,
            Indexer,
            CollectionType,
            CollectionValue,
            TypedParameterList,
            BoxType,
        }
        struct SecondaryNode
        {
            public SecondaryNodeType NodeType;
            public object Data;
            public Box<SecondaryNode> Child;
            public Place Place;
        }
        static Node NextNode(Token[] l, ref int i)
        {
            var n1 = NextPrimaryNode(l, ref i);
            while (true)
            {
                switch (l[i].TokenType)
                {
                    default:
                        return n1;

                    case TokenType.LowOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(l, ref i)), NodeType = PrimaryNodeType.BinaryOperator, Place = l[i - 1].Place };
                        break;

                    case TokenType.HighOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(l, ref i)), NodeType = PrimaryNodeType.BinaryOperator, Place = l[i - 1].Place };
                        break;

                    case TokenType.Equals:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(l, ref i)), NodeType = PrimaryNodeType.Assignment, Place = l[i - 1].Place };
                        break;

                    case TokenType.Lambda:
                        {
                            Node? t = null;
                            List<(Node, Node)> tl = null;
                            if (n1.NodeType == PrimaryNodeType.EmptyParameterList) tl = new List<(Node, Node)>();
                            else if (n1.NodeType == PrimaryNodeType.TypedParameterList) tl = (List<(Node, Node)>)n1.Data;
                            else if (n1.NodeType == PrimaryNodeType.Identifier && n1.Child.Value.NodeType == SecondaryNodeType.TypedParameterList)
                            {
                                t = n1;
                                tl = (List<(Node, Node)>)n1.Child.Value.Data;
                            }
                            else if (n1.NodeType == PrimaryNodeType.Identifier && n1.Child.Value.NodeType == SecondaryNodeType.Call && ((List<Node>)n1.Child.Value.Data).Count == 0)
                            {
                                t = n1;
                                tl = new List<(Node, Node)>();
                            }
                            else
                            {
                                Throw(Error.TokenIllegal1, ThrowType.Error, l[i].Place, "=>");
                                i++;
                                break;
                            }
                            i++;
                            if (l[i].TokenType == TokenType.TertiaryOpen || l[i].TokenType == TokenType.At) n1 = new Node { Data = (t, tl, (object)NextStatement(l, ref i, StatementPatterns)), NodeType = PrimaryNodeType.Function, Place = l[i - 1].Place };
                            else n1 = new Node { Data = (t, tl, (object)NextNode(l, ref i)), NodeType = PrimaryNodeType.Function, Place = l[i - 1].Place };
                            break;
                        }
                }
            }
        }
        static Node NextPrimaryNode(Token[] l, ref int i)
        {
            Node n = new Node { Place = l[i].Place };
            bool couldbetype = true;
            switch (l[i].TokenType)
            {
                case TokenType.Word:
                    i++;
                    n.NodeType = PrimaryNodeType.Identifier;
                    break;

                case TokenType.String:
                case TokenType.DecInteger:
                case TokenType.DecNonInteger:
                case TokenType.BinInteger:
                case TokenType.HexInteger:
                    n.NodeType = PrimaryNodeType.Literal;
                    couldbetype = false;
                    i++;
                    break;

                case TokenType.PrimaryOpen:
                    {
                        i++;
                        couldbetype = false;
                        if (NodeListOrDualNodeList(l, ref i, out var sl, out var dl, TokenType.PrimaryClose, ")"))
                        {
                            n.NodeType = PrimaryNodeType.TypedParameterList;
                            n.Data = dl;
                        }
                        else
                        {
                            if (sl.Count == 0)
                            {
                                n.NodeType = PrimaryNodeType.EmptyParameterList;
                            }
                            else if (sl.Count == 1)
                            {
                                n.NodeType = PrimaryNodeType.Parentheses;
                                n.Data = sl[0];
                            }
                            else
                            {
                                n.NodeType = PrimaryNodeType.UntypedParameterList;
                                n.Data = sl;
                            }
                        }
                        break;
                    }

                case TokenType.SecondaryOpen:
                    n.NodeType = PrimaryNodeType.Record;
                    i++;
                    n.Data = NodeList(l, ref i, TokenType.SecondaryClose, "]");
                    break;

                case TokenType.TertiaryOpen:
                    n.NodeType = PrimaryNodeType.Collection;
                    couldbetype = false;
                    i++;
                    n.Data = NodeList(l, ref i, TokenType.TertiaryClose, "}");
                    break;

                case TokenType.LowOperator:
                case TokenType.HighOperator:
                    n.NodeType = PrimaryNodeType.UnaryOperator;
                    couldbetype = false;
                    i++;
                    n.Data = NextPrimaryNode(l, ref i);
                    return n;

                case TokenType.NullWord:
                    n.NodeType = PrimaryNodeType.Null;
                    i++;
                    break;

                case TokenType.ObjectWord:
                    n.NodeType = PrimaryNodeType.Object;
                    i++;
                    break;

                case TokenType.EndOfFile:
                    Throw(Error.TokenIllegal1, ThrowType.Error, l[i].Place, l[i].Match);
                    return n;

                default:
                    Throw(Error.TokenIllegal1, ThrowType.Error, l[i].Place, l[i].Match);
                    i++;
                    return NextPrimaryNode(l, ref i);
            }
            if (NextSecondaryNode(l, ref i, out var sn, couldbetype)) n.Child = sn;
            return n;
        }
        static bool NextSecondaryNode(Token[] l, ref int i, out SecondaryNode n, bool couldbetype)
        {
            n = new SecondaryNode { Place = l[i].Place };
            switch (l[i].TokenType)
            {
                default: return false;

                case TokenType.Period:
                    i++;
                    if (l[i].TokenType == TokenType.Word) i++;
                    else Throw(Error.IdentifierExpected1, ThrowType.Error, l[i].Place, l[i].Match);
                    break;

                case TokenType.PrimaryOpen:
                    {
                        couldbetype = false;
                        i++;
                        if (NodeListOrDualNodeList(l, ref i, out var sl, out var dl, TokenType.PrimaryClose, ")"))
                        {
                            n.NodeType = SecondaryNodeType.TypedParameterList;
                            n.Data = dl;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.Call;
                            n.Data = sl;
                        }
                        break;
                    }

                case TokenType.SecondaryOpen:
                    if (couldbetype && l[i].TokenType == TokenType.SecondaryClose)
                    {
                        n.NodeType = SecondaryNodeType.CollectionType;
                        i++;
                    }
                    else
                    {
                        i++;
                        couldbetype = false;
                        n.NodeType = SecondaryNodeType.Indexer;
                        n.Data = NodeList(l, ref i, TokenType.SecondaryClose, "]");
                    }
                    break;

                case TokenType.TertiaryOpen:
                    if (!couldbetype) return false;
                    i++;
                    n.NodeType = SecondaryNodeType.CollectionValue;
                    n.Data = NodeList(l, ref i, TokenType.TertiaryClose, "}");
                    break;

                case TokenType.QuestionMark:
                    n.NodeType = SecondaryNodeType.BoxType;
                    i++;
                    break;
            }
            if (NextSecondaryNode(l, ref i, out var sn, couldbetype)) n.Child = sn;
            return true;
        }

        //Structural analysis

        class Box<T>
        {
            public T Value;

            public static implicit operator Box<T>(T v)
            {
                return new Box<T> { Value = v };
            }
        }
    }
}
