using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pacednl;

//pacednc compiles source files to Library objects (implemented in pacednl)

namespace pacednc
{
    public static class Info
    {
        public static string Version = "pacednc experimental 0.1.2";
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

        static Token[] Tokens;
        static StatementPattern[] StatementPatterns;
        static List<Library> LibrariesInUse = new List<Library>();
        static Library Library;
        static Profile Profile;

        public static Library Compile(string Source, string LocalPath)
        {
            //Tokenize, see Tokenize function below
            Tokens = Tokenize(Source, new List<(Regex, TokenType)>
            {
                //keywords
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

                //braces
                (new Regex(@"^\("), TokenType.PrimaryOpen),
                (new Regex(@"^\)"), TokenType.PrimaryClose),
                (new Regex(@"^\["), TokenType.SecondaryOpen),
                (new Regex(@"^]"), TokenType.SecondaryClose),
                (new Regex(@"^\{"), TokenType.TertiaryOpen),
                (new Regex(@"^}"), TokenType.TertiaryClose),

                //reserved operators
                (new Regex(@"^\."), TokenType.Period),
                (new Regex(@"^;"), TokenType.Semicolon),
                (new Regex(@"^,"), TokenType.Comma),
                (new Regex(@"^="), TokenType.Equals),
                (new Regex(@"^:"), TokenType.Colon),
                (new Regex(@"^=>"), TokenType.Lambda),
                (new Regex(@"^@"), TokenType.At),
                (new Regex(@"^\?"), TokenType.QuestionMark),
                (new Regex(@"^#"), TokenType.Hash),

                //custom operators
                (new Regex(@"^" + LowOperatorCharMatch + LowOperatorCharMatch + @"?"), TokenType.LowOperator),
                (new Regex(@"^" + HighOperatorCharMatch + HighOperatorCharMatch + @"?"), TokenType.HighOperator),

                //numbers
                (new Regex(@"^[0-9]+"), TokenType.DecInteger),
                (new Regex(@"^[0-9]+\.[0-9]+"), TokenType.DecNonInteger),
                (new Regex(@"^0[bB][01]+"), TokenType.BinInteger),
                (new Regex(@"^0[xX][0-9a-fA-F]+"), TokenType.HexInteger),

                //word
                (new Regex(@"^" + IdentifierCharMatch + @"*"), TokenType.Word),

                //misc
                (new Regex("^global::"), TokenType.Global)
            });

            //!!! optional !!! print tokens to console

            //for (int i = 0; i < Tokens.Length; i++)
            //{
            //    Console.WriteLine($"{$"{Tokens[i].Place.Line} : {Tokens[i].Place.Index}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
            //}

            //!!! end optional !!!

            //Parse, see StatementPattern struct and NextStatement function
            StatementPatterns = new StatementPattern[]
            {
                //keyword blocks
                new StatementPattern("t|=s", StatementType.Main, new[]{ TokenType.MainWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Element, new[]{ TokenType.ElementWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Struct, new[]{ TokenType.StructWord }, null, null, null),
                new StatementPattern("t|=n", StatementType.Enum, new[]{ TokenType.EnumWord }, null, null, null),

                //control flow statements
                new StatementPattern("=b", StatementType.Scope, null, null, null, null),
                new StatementPattern("t|!=i|=s", StatementType.Label, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, null),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, null),
                new StatementPattern("t|!t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, new[]{ "(", ")" }, null),
                new StatementPattern("t|=s", StatementType.Else, new[]{ TokenType.ElseWord }, null, null, null),

                //field/variable decleration
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Set }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PrivateWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.PublicWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set | VariableFlags.SetPublic }),
                new StatementPattern("t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.AliasWord }, null, null, new object[] { VariableFlags.Get | VariableFlags.GetPublic | VariableFlags.Set }),
                new StatementPattern("t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.GetWord }, null, null, new object[] { VariableFlags.Get }),
                new StatementPattern("t|=n|=n|!e", StatementType.Declaration, new[] { TokenType.SetWord }, null, null, new object[] { VariableFlags.Set }),

                new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, null, null),

                //convertions
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ExplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ImplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.AutomaticWord, TokenType.ToWord, TokenType.Equals }, null, null, null),

                //importing
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, new object[] { false }),
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.UseWord }, null, null, new object[] { true }),

                //special stuff
                new StatementPattern("t|=q", StatementType.TranslatorNote, new[]{ TokenType.Hash }, null, new[]{ "#" }, null),
                new StatementPattern("t|!=i", StatementType.TranslatorSpecifier, new[]{ TokenType.Hash }, null, new[]{ "#" }, null),

                //nodes
                new StatementPattern("=n|e", StatementType.Node, null, null, null, null),
                new StatementPattern("=n|=n|!e", StatementType.Declaration, null, null, null, new object[] { new VariableFlags() }),

            };

            //match all tokens into statements
            List<Statement> Statements = new List<Statement>();
            {
                int i = 0;
                int lasti = Tokens.Length - 1;
                while (true)
                {
                    if (i == lasti) break;
                    Statements.Add(NextStatement(ref i, StatementPatterns));
                }
            }

            //create library and other objects
            Library = new Library { Name = "CompiledLibrary" };
            Project.Current.Libraries.Add(Library);
            Profile = new Profile();
            Library.Profiles.Add(Profile);

            //these libraries are "in use"
            LibrariesInUse.Add(Library);

            //import and translator specifier statements, must be before any other statements
            //this library depends on these files, 
            //they must exist when translating or importing or doing whatever with this library
            int StatementIndex = 0;
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                if (Statements[StatementIndex].StatementType == StatementType.Import)
                {
                    string n = (string)Statements[StatementIndex].Data[1];
                    Profile.Dependencies.Add(n);
                    var l = new Library();
                    var or = l.Read(Config.FormatLibraryFilename(n, LocalPath, true));
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                    or = Project.Current.Import(l, true);
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                }
                else if(Statements[StatementIndex].StatementType == StatementType.TranslatorSpecifier)
                {
                    if (Profile.Translator == null) Throw(Error.TranslatorSpecified0, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place);
                    else
                    {
                        Profile.Translator = (string)Statements[StatementIndex].Data[0];
                    }
                }
            }
            StatementIndex--;

            //analyze statements
            for(; StatementIndex < Statements.Count; StatementIndex++)
            {
                AnalyzeGlobal(Statements[StatementIndex]);
            }

            return Library;
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
            ItemIllegalContext2,
            ExecutableNotInExecutable0,
            AccessModIllegalForItem0,
            MultipleDefined1,
            MultipleMain0,
            OperationResultError1,
            FeatureNotImplemented0,
            NonAssignableValue0,
            IdentifiableNotDefined1,
            TranslatorSpecified0,
            StatementIllegal0,
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
                case Error.ExecutableNotInExecutable0: return "Executable code may only appear in executable blocks.";
                case Error.AccessModIllegalForItem0: return "Access modifiers are not valid for this item.";
                case Error.MultipleDefined1: return $"'{args[0]}' is defined more than once.";
                case Error.MultipleMain0: return "Entry point already defined.";
                case Error.OperationResultError1: return $"Operation result error: {args[0]}.";
                case Error.FeatureNotImplemented0: return "This feature is yet to be implemented.";
                case Error.NonAssignableValue0: return "This value is constant or read-only and cannot be assigned to.";
                case Error.IdentifiableNotDefined1: return $"The symbol '{args[0]}' is not defined.";
                case Error.TranslatorSpecified0: return "Translator already specified.";
                case Error.StatementIllegal0: return "This statement is illegal in this context.";
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
            ConsoleColor color = Console.ForegroundColor;
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
            Console.ForegroundColor = color;
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
            Hash,

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

        //This functions matches text with a regular expression
        //if a match is found, it generates a token with the according TokenType
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

                //special case for strings, because of string insertion
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

                //comments
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

                //the longest match
                string longest = string.Empty;

                //and its according TokenType
                TokenType tt = TokenType.None;

                //try to match if every regex
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

                //if no match, throw
                if (tt == TokenType.None)
                {
                    Throw(Error.CharacterIllegal1, ThrowType.Error, GetPlace(), sb.ToString(0, 1));
                    sb.Remove(0, 1);
                    index++;
                }

                //if match is found, generate token
                else
                {
                    l.Add(new Token { TokenType = tt, Match = sb.ToString(0, longest.Length), Place = GetPlace() });
                    sb.Remove(0, longest.Length);
                    index += (ushort)longest.Length;
                }
            }

            //add end of file token to the end
            l.Add(new Token { TokenType = TokenType.EndOfFile, Place = GetPlace(), Match = "END OF FILE" });
            return l.ToArray();
        }

        //Parsing
        
        //misc helper functions
        static void RefNodeList(ref int i, ref List<Node> nl, TokenType end, string endtokenmatch)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add(NextNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
                }
        }
        static List<Node> NodeList(ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<Node>();
            RefNodeList(ref i, ref x, end, endtokenmatch);
            return x;
        }
        static void RefDualNodeList(ref int i, ref List<(Node, Node)> nl, TokenType end, string endtokenmatch)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add((NextNode(ref i), NextNode(ref i)));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
                }
        }
        static List<(Node, Node)> DualNodeList(ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<(Node, Node)>();
            RefDualNodeList(ref i, ref x, end, endtokenmatch);
            return x;
        }
        static bool NodeListOrDualNodeList(ref int i, out List<Node> SingleNodeList, out List<(Node, Node)> DualNodeList, TokenType end, string endtokenmatch)
        {
            SingleNodeList = null;
            DualNodeList = null;
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node>();
                return false;
            }
            var firstNode = NextNode(ref i);
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node> { firstNode };
                return false;
            }
            if (Tokens[i].TokenType == TokenType.Comma)
            {
                i++;
                SingleNodeList = new List<Node> { firstNode };
                RefNodeList(ref i, ref SingleNodeList, end, endtokenmatch);
                return false;
            }
            var secondNode = NextNode(ref i);
            DualNodeList = new List<(Node, Node)> { (firstNode, secondNode) };
            if (Tokens[i].TokenType == TokenType.Comma) i++;
            else if (Tokens[i].TokenType == end)
            {
                i++;
                return true;
            }
            else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',' expected", Tokens[i].Match);
            RefDualNodeList(ref i, ref DualNodeList, end, endtokenmatch);
            return true;
        }
        static List<string> IdentifierList(ref int i, TokenType end, string endtokenmatch)
        {
            List<string> x = new List<string>();
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (Tokens[i].TokenType == TokenType.Word)
                    {
                        x.Add(Tokens[i].Match);
                        i++;
                    }
                    else Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
                }
            return x;
        }

        //statement parsing
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
            Declaration,
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
            TranslatorSpecifier,
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
            public int Token;
            public object[] Data; //!!! this array will be reffered as data in the comments below
        }

        //The NextStatement parsing function uses these to parse statements
        //StatementPattern tells the parsing function how to parse a statement of a certain StatementType
        struct StatementPattern
        {
            //Clauses from the format string
            public string[] Clauses;

            //The StatementType for statements that match this StatementPattern
            public StatementType StatementType;

            //'t' clauses will take the tokentype to match from this array in a numeric order
            //the length of this array must match the count of 't' clauses in the format string
            public TokenType[] TokenTypes; 

            //'m' clauses will take the total match from this array in a numeric order
            //the length of this array must match the count of 'm' clauses in the format string
            public string[] Matches;

            //contains the first argument for the error thrown from 't' clauses with the throw flag
            public string[] Misc;

            public object[] Data; //these objects will be added to the final statements data, before any data from pattern matching

            //The format string must constist of clauses separated by "|"
            //Clauses must be as follows: "!" (throw flag, throw a compiler error if not a match) "=" (save match to data) [any single character] (the clause
            //Examples of a clause: !t, =s, a
            //Example of a format string: !t|=s|a
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

        //Statement parsing function
        static Statement NextStatement(ref int i, StatementPattern[] patterns)
        {
            //the statement to return
            Statement s = new Statement { Token = i };

            //helper variables for throwing
            Error currentError = Error.Unknown0;
            string[] currentErrorArgs = null;

            //try to match all patterns
            foreach (var p in patterns)
            {
                //current position in the TokenTypes array
                int tokentypeindex = 0;

                //current position in the Misc array
                int miscindex = 0;

                //data of the final statement
                List<object> data = p.Data == null ? new List<object>() : p.Data.ToList();

                int originali = i;
                foreach (var c in p.Clauses)
                {
                    //flags come before the clause
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

                    //clause
                    switch (c[ci])
                    {
                        //token clause, tokentype from the Patterns array (see above)
                        case 't':
                            if (Tokens[i].TokenType != p.TokenTypes[tokentypeindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Error.TokenExpected2;
                                    currentErrorArgs = new[] { p.Misc[miscindex++], Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            i++;
                            break;

                        //identfier, matches a token with TokenType.Word
                        case 'i':
                            if (Tokens[i].TokenType != TokenType.Word)
                            {
                                if (dothrow)
                                {
                                    currentError = Error.IdentifierExpected1;
                                    currentErrorArgs = new[] { Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            if (save) data.Add(Tokens[i].Match);
                            i++;
                            break;

                        //any token
                        case 'a':
                            if (save) data.Add(Tokens[i].Match);
                            i++;
                            break;

                        //node, will always match and throw if invalid
                        case 'n':
                            {
                                var n = NextNode(ref i);
                                if (save) data.Add(n);
                                break;
                            }

                        //statement, will always match and throw if invalid
                        case 's':
                            {
                                var st = NextStatement(ref i, patterns);
                                if (save) data.Add(st);
                                break;
                            }

                        //block, TertiaryOpen and TertiaryClose with multiple Statements inside
                        case 'b':
                            {
                                if (Tokens[i].TokenType == TokenType.TertiaryOpen) i++;
                                else
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { "{", Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                List<Statement> stl = new List<Statement>();
                                while (true)
                                {
                                    if (Tokens[i].TokenType == TokenType.TertiaryClose) break;
                                    if (Tokens[i].TokenType == TokenType.EndOfFile)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { "}", Tokens[i].Match };
                                        goto throww;
                                    }
                                    stl.Add(NextStatement(ref i, patterns));
                                }
                                i++;
                                if (save) data.Add(stl);
                                break;
                            }

                        //matches a token with TokenType.Semicolon
                        case 'e':
                            {
                                if (Tokens[i].TokenType != TokenType.Semicolon)
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { ";", Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                i++;
                                break;
                            }

                        //matches a token with TokenType.String
                        case 'q':
                            {
                                if (Tokens[i].TokenType != TokenType.String)
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.StringExpected1;
                                        currentErrorArgs = new[] { Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                if (save) data.Add(Tokens[i].Match);
                                i++;
                                break;
                            }
                    }
                }
                //if this is executed it's a match!
                //the first match will be returned
                s.StatementType = p.StatementType;
                s.Data = data.ToArray();
                return s;
                nextpattern:
                i = originali;
            }
            //throw with the helper variables
            throww:
            Throw(currentError, ThrowType.Error, Tokens[i].Place, currentErrorArgs);
            s.StatementType = StatementType.No_op;
            return s;
        }

        //nodes 

        enum NodeType : byte
        {
            None,
            Parentheses, //Node                          //ex: (1 + 1)
            Identifier,                                  //ex: MyVariable
            Literal,                                     //ex: "hello" or 1
            UnaryOperator, //Node                        //ex: -1
            BinaryOperator, //(Node, Node)               //ex: 1 * 1
            Assignment, //(Node, Node)                   //ex: x = y
            Record,                                      //ex: [x = 1, y = 2]
            RecordType,                                  //ex: [int x, int y]
            Collection, //List<Node>                     //ex: {1, 2, 3}
            Function,                                    //ex: (x) => { print(x); }
            FunctionType, //(Node?, List<Node>)          //ex: func(int, int)
            EmptyParameterList,                          //ex: ()
            UntypedParameterList, //List<Node>           //ex: (x, y)
            TypedParameterList, //List<(Node, Node)>     //ex: (int x, int y)
            Object,                                      //ex: object
            Null,                                        //ex: null
        }
        class Node
        {
            public NodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
        }

        //SecondaryNodes are expressions that belong to the parent node (either Node or SecondaryNode)
        enum SecondaryNodeType : byte
        {
            None,
            Member,                       //ex: .MyField
            Call,                         //ex: (1, 2)
            Indexer,                      //ex: [1, 2]
            CollectionTypeSpecifier,      //ex: []
            CollectionValue,              //ex: {1, 2, 3}
            TypedParameterList,           //ex: (int x, int y)
            IndexerFunctionParameterList, //ex: [int index]
            BoxedSpecifier,               //ex: ?
        }
        class SecondaryNode
        {
            public SecondaryNodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
        }

        //Expression trees consist of Nodes and SecondaryNodes in a hierarchy

        //!!! Nodes, PrimaryNodes, SecondaryNode meaning explained
        //Nodes have Node type and can be any of NodeType
        //PrimaryNodes have Node type and can be any of NodeType enum except BinaryOperator, Assignment, and some Functions
        //SecondaryNodes have SecondaryNode type and are all of SecondaryNodeType

        //MyArray[1] + x.y

        //is parsed as (children indented): 

        //+         Node
        //  MyArray PrimaryNode
        //    []    SecondaryNode
        //      1   PrimaryNode
        //  x       PrimaryNode
        //    .y    SecondaryNode

        //The node parsing function
        //unlike the tokenize and statement parsing functions,
        static Node NextNode(ref int i)
        {
            //get next primary node
            var n1 = NextPrimaryNode(ref i, false);
            while (true)
            {
                switch (Tokens[i].TokenType)
                {
                    default:
                        return n1;

                    //When operators have low priority, they take the previous PrimaryNode (n1) and the next PrimaryNode
                    case TokenType.LowOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(ref i, false)), NodeType = NodeType.BinaryOperator, Token = i - 1 };
                        break;

                    //When operators have high priority, they take the previous PrimaryNode (n1) and the next Node
                    case TokenType.HighOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.BinaryOperator, Token = i - 1 };
                        break;

                    //Assignment operators take the previous PrimaryNode and the next Node
                    case TokenType.Equals:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Assignment, Token = i - 1 };
                        break;

                    //Lambda operators take the previous PrimaryNode (MUST be of some parameter list NodeType) and the next Node or statement if its of types StatementType.Scope or StatementType.Label
                    case TokenType.Lambda:
                        {
                            Node t = null;
                            List<(Node, Node)> tl = null;
                            if (n1.NodeType == NodeType.EmptyParameterList) tl = new List<(Node, Node)>();
                            else if (n1.NodeType == NodeType.TypedParameterList) tl = (List<(Node, Node)>)n1.Data;
                            else if (n1.NodeType == NodeType.Identifier && n1.Child.NodeType == SecondaryNodeType.TypedParameterList)
                            {
                                t = n1;
                                tl = (List<(Node, Node)>)n1.Child.Data;
                            }
                            else if (n1.NodeType == NodeType.Identifier && n1.Child.NodeType == SecondaryNodeType.Call && ((List<Node>)n1.Child.Data).Count == 0)
                            {
                                t = n1;
                                tl = new List<(Node, Node)>();
                            }
                            else
                            {
                                Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[i].Place, "=>");
                                i++;
                                break;
                            }
                            i++;
                            if (Tokens[i].TokenType == TokenType.TertiaryOpen || Tokens[i].TokenType == TokenType.At) n1 = new Node { Data = (t, tl, (object)NextStatement(ref i, StatementPatterns)), NodeType = NodeType.Function, Token = i - 1 };
                            else n1 = new Node { Data = (t, tl, (object)NextNode(ref i)), NodeType = NodeType.Function, Token = i - 1 };
                            break;
                        }
                }
            }
        }

        //The primary node parsing function
        //PrimaryNodes use the Node type
        static Node NextPrimaryNode(ref int i, bool onlytype)
        {
            //the value to return
            var n = new Node { Token = i };

            switch (Tokens[i].TokenType)
            {
                default:
                    Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    break;

                //Identifier
                case TokenType.Word:
                    n.NodeType = NodeType.Identifier;
                    i++;
                    break;

                //Parantheses and parameter lists
                case TokenType.PrimaryOpen:
                    {
                        if (onlytype) goto default;
                        i++;
                        if(Tokens[i].TokenType == TokenType.PrimaryClose)
                        {
                            n.NodeType = NodeType.EmptyParameterList;
                            break;
                        }

                        var nodes = NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")");
                        if(dual == null)
                        {
                            if(single.Count == 0)
                            {
                                n.NodeType = NodeType.Parentheses;
                                n.Data = single[0];
                            }
                            else
                            {
                                n.NodeType = NodeType.UntypedParameterList;
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.TypedParameterList;
                            n.Data = dual;
                        }
                        break;
                    }

                //literals
                case TokenType.DecInteger:
                case TokenType.DecNonInteger:
                case TokenType.String:
                case TokenType.HexInteger:
                case TokenType.BinInteger:
                    if (onlytype) goto default;
                    n.NodeType = NodeType.Literal;
                    i++;
                    break;

                //keywords
                case TokenType.NullWord:
                    if (onlytype) goto default;
                    n.NodeType = NodeType.Null;
                    i++;
                    break;
                case TokenType.ObjectWord:
                    n.NodeType = NodeType.Object;
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                    n.NodeType = NodeType.UnaryOperator;
                    i++;
                    n.Data = NextPrimaryNode(ref i, false);
                    break;

                //record
                case TokenType.SecondaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]");
                        if(dual == null)
                        {
                            n.NodeType = NodeType.Record;
                            n.Data = single;
                        }
                        else
                        {
                            n.NodeType = NodeType.RecordType;
                            n.Data = dual;
                        }
                        break;
                    }

                //collection
                case TokenType.TertiaryOpen:
                    {
                        n.NodeType = NodeType.Collection;
                        i++;
                        n.Data = NodeList(ref i, TokenType.TertiaryClose, "}");
                        break;
                    }

                //function type
                case TokenType.FuncWord:
                    {
                        n.NodeType = NodeType.FunctionType;
                        i++;
                        Node rettype = null;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        if (Tokens[i].TokenType == TokenType.Colon)
                        {
                            i++;
                            rettype = (NextNode(ref i));
                        }
                        else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(");
                        n.Data = (rettype, NextNode(ref i));
                        break;
                    }
            }

            //get child if there is
            if (!onlytype && NextSecondaryNode(ref i, out var sn, onlytype)) n.Child = sn;

            return n;
        }

        //SecondaryNode parse function
        static bool NextSecondaryNode(ref int i, out SecondaryNode n, bool onlytype)
        {
            n = new SecondaryNode { Token = i };

            switch (Tokens[i].TokenType)
            {
                default: return false;

                //member
                case TokenType.Period:
                    {
                        n.NodeType = SecondaryNodeType.Member;
                        i++;
                        if (Tokens[i].TokenType == TokenType.Word)
                        {
                            i++;
                            n.Data = Tokens[i].Match;
                        }
                        else
                        {
                            n.Data = "<unnamed member>";
                        }
                        break;
                    }

                //Parentheses and TypedParameterList
                case TokenType.PrimaryOpen:
                    {
                        if (onlytype) return false;
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")");
                        if(dual == null)
                        {
                            n.NodeType = SecondaryNodeType.Call;
                            n.Data = single;
                            break;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.TypedParameterList;
                            n.Data = dual;
                            break;
                        }
                    }

                //indexer and collection type
                case TokenType.SecondaryOpen:
                    {
                        if(Tokens[i + 1].TokenType == TokenType.SecondaryClose)
                        {
                            n.NodeType = SecondaryNodeType.CollectionTypeSpecifier;
                            i += 2;
                            onlytype = true;
                            break;
                        }
                        else if (onlytype) return false;
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]");
                        if(dual == null)
                        {
                            n.NodeType = SecondaryNodeType.Indexer;
                            n.Data = single;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.IndexerFunctionParameterList;
                            n.Data = dual;
                        }
                        break;
                    }

                //collection
                case TokenType.TertiaryOpen:
                    {
                        if (onlytype) return false;
                        n.NodeType = SecondaryNodeType.CollectionValue;
                        i++;
                        n.Data = NodeList(ref i, TokenType.TertiaryClose, "}");
                        break;
                    }
            }

            //get child if there is
            if (!onlytype && NextSecondaryNode(ref i, out var sn, onlytype)) n.Child = sn;

            return true;
        }

        //semantics

        static Value NodeToValue(Node n)
        {
            Value v = null;
            switch (n.NodeType)
            {
                default:
                    Throw(Error.FeatureNotImplemented0, ThrowType.Error, Tokens[n.Token].Place);
                    break;

                case NodeType.Literal:
                    {
                        if(n.Child != null)
                        {
                            Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[n.Token].Place, Tokens[n.Token].Match);
                        }
                        Token t = Tokens[n.Token];
                        switch (t.TokenType)
                        {
                            case TokenType.String: v = new LiteralValue { Type = LiteralValueType.String, Value = t.Match.Substring(1, t.Match.Length - 2) }; break;
                            case TokenType.DecInteger: v = new LiteralValue { Type = LiteralValueType.Integer, Value = t.Match }; break;
                            case TokenType.DecNonInteger: v = new LiteralValue { Type = LiteralValueType.Fractional, Value = t.Match }; break;
                            case TokenType.HexInteger:
                                {
                                    BigInteger x = BigInteger.Zero;
                                    for (int i = 2; i < t.Match.Length; i += 16)
                                    {
                                        x += Convert.ToUInt64(t.Match.Substring(i, Math.Min(16, t.Match.Length)), 16);
                                    }
                                    v = new LiteralValue { Type = LiteralValueType.Integer, Value = x.ToString() };
                                    break;
                                }
                            case TokenType.BinInteger:
                                {
                                    BigInteger x = BigInteger.Zero;
                                    for (int i = 2; i < t.Match.Length; i += 64)
                                    {
                                        x += Convert.ToUInt64(t.Match.Substring(i, Math.Min(64, t.Match.Length)), 2);
                                    }
                                    v = new LiteralValue { Type = LiteralValueType.Integer, Value = x.ToString() };
                                    break;
                                }
                        }
                        break;
                    }
                case NodeType.Identifier:
                    return new LocalValue { Name = Tokens[n.Token].Match };
            }
            return n.Child == null ? v : SecondaryNodeToValue(n.Child, v);
        }
        static Value SecondaryNodeToValue(SecondaryNode n, Value parent)
        {
            return parent;
        }

        static Procedure StatementToProcedure(Statement s, List<string> locals)
        {
            var p = new Procedure();
            p.Instructions.Add(StatementToInstruction(s, locals));
            return p;
        }
        static Instruction StatementToInstruction(Statement s, List<string> locals)
        {
            Instruction i = new Instruction();
            switch (s.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[s.Token].Place);
                    break;
                case StatementType.Node:
                    {
                        var n = (Node)s.Data[0];
                        switch (n.NodeType)
                        {
                            case NodeType.Assignment:
                                {
                                    var nodes = ((Node, Node))n.Data;
                                    var vals = (NodeToValue(nodes.Item1), NodeToValue(nodes.Item2));
                                    if (vals.Item1 != null && vals.Item2 != null)
                                    {
                                        if (!(vals.Item1 is LocalValue)) Throw(Error.NonAssignableValue0, ThrowType.Error, Tokens[nodes.Item1.Token].Place);
                                        else
                                        {
                                            if (vals.Item2 is LocalValue temp && !locals.Contains(temp.Name))
                                                Throw(Error.IdentifiableNotDefined1, ThrowType.Error, Tokens[nodes.Item2.Token].Place, Tokens[nodes.Item2.Token].Match);
                                            else
                                            {
                                                if (vals.Item1 is LocalValue && !locals.Contains(Tokens[nodes.Item1.Token].Match))
                                                {
                                                    locals.Add(Tokens[nodes.Item1.Token].Match);
                                                }

                                                i.Type = InstructionType.Assign;
                                                i.Data = (vals.Item1, vals.Item2);
                                            }
                                        }
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case StatementType.Scope:
                    {
                        i.Type = InstructionType.Scope;
                        var stl = (List<Statement>)s.Data[0];
                        var il = new List<Instruction>();
                        for (int x = 0; x < stl.Count; x++)
                        {
                            il.Add(StatementToInstruction(stl[x], locals));
                        }
                        i.Data = (string.Empty, il);
                        break;
                    }
                case StatementType.Label:
                    {
                        var name = (string)s.Data[0];
                        var st = (Statement)s.Data[1];
                        if(st.StatementType == StatementType.Scope)
                        {
                            i.Type = InstructionType.Scope;
                            var stl = (List<Statement>)st.Data[0];
                            var il = new List<Instruction>();
                            for (int x = 0; x < stl.Count; x++)
                            {
                                il.Add(StatementToInstruction(stl[0], locals));
                            }
                            i.Data = (name, il);
                        }
                        else if(st.StatementType == StatementType.Break)
                        {
                            i.Type = InstructionType.Break;
                            i.Data = name;
                        }
                        else if(st.StatementType == StatementType.Continue)
                        {
                            i.Type = InstructionType.Continue;
                            i.Data = name;
                        }
                        break;
                    }
                case StatementType.Break:
                    {
                        i.Type = InstructionType.Break;
                        i.Data = null;
                        break;
                    }
                case StatementType.Continue:
                    {
                        i.Type = InstructionType.Continue;
                        i.Data = null;
                        break;
                    }
                case StatementType.TranslatorNote:
                    {
                        i.Type = InstructionType.Special;
                        string x = (string)s.Data[0];
                        i.Data = x.Substring(1, x.Length - 2);
                        break;
                    }
            }
            return i;
        }

        //structural analysis
        static void AnalyzeGlobal(Statement s)
        {
            switch (s.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[s.Token].Place);
                    break;

                case StatementType.Main:
                    {
                        var p = StatementToProcedure((Statement)s.Data[0], new List<string>());
                        if (Project.Current.EntryPoint != null) Throw(Error.MultipleMain0, ThrowType.Error, Tokens[s.Token].Place);
                        else
                        {
                            Profile.EntryPoint = p;
                            Project.Current.EntryPoint = p;
                        }
                        break;
                    }
            }
        }
    }
}
