using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pacednl;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)

using _Type = pacednl.Type;

//pacednc compiles source files to Library objects (implemented in pacednl)

namespace pacednc
{
    public static class Info
    {
        public static string Version = "pacednc experimental 0.2.0 (not functional)";
    }
    static class Program
    {
        public static void Main(string[] args)
        {

        }
    }
    public class Compiler
    {
        //convert a text file to a pace library

        static string IdentifierCharMatch = @"\w";
        static string LowOperatorChars = @"-+!#¤%£$€´`<>~:";
        static string HighOperatorChars = @"*/|&^";
        static string ReservedOperatorChars = @"=\@?";

        static string UnnamedPlaceholder = "<unnamed>";

        static StatementPattern[] StatementPatterns;

        Token[] Tokens;
        List<Library> LibrariesInUse = new List<Library>();
        Library Library;

        public Library Compile(string Source, string LocalPath)
        {

            //Tokenize, see Tokenize function below
            Tokens = Tokenize(Source, new List<(Regex, TokenType)>
            {
                //keywords
                (new Regex(@"^element"), TokenType.ElementWord),
                (new Regex(@"^class"), TokenType.ClassWord),
                (new Regex(@"^struct"), TokenType.StructWord),
                (new Regex(@"^value"), TokenType.ReturnWord),
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
                (new Regex(@"^initialize"), TokenType.InitWord),
                (new Regex(@"^finalize"), TokenType.CleanWord),
                (new Regex(@"^each"), TokenType.EachWord),
                (new Regex(@"^explicit"), TokenType.ExplicitWord),
                (new Regex(@"^implicit"), TokenType.ImplicitWord),
                (new Regex(@"^operator"), TokenType.OperatorWord),
                (new Regex(@"^true"), TokenType.TrueWord),
                (new Regex(@"^false"), TokenType.FalseWord),
                (new Regex(@"^not"), TokenType.NotWord),
                (new Regex(@"^and"), TokenType.AndWord),
                (new Regex(@"^or"), TokenType.OrWord),

                //braces
                (new Regex(@"^\("), TokenType.PrimaryOpen),
                (new Regex(@"^\)"), TokenType.PrimaryClose),
                (new Regex(@"^\["), TokenType.SecondaryOpen),
                (new Regex(@"^]"), TokenType.SecondaryClose),
                (new Regex(@"^\{"), TokenType.TertiaryOpen),
                (new Regex(@"^}"), TokenType.TertiaryClose),

                //custom operators
                (new Regex($@"^[{LowOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]?"), TokenType.LowOperator),
                (new Regex($@"^[{HighOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]?"), TokenType.HighOperator),
                (new Regex($@"^[{ReservedOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]"), TokenType.LowOperator),

                //reserved operators
                (new Regex(@"^\."), TokenType.Period),
                (new Regex(@"^;"), TokenType.Semicolon),
                (new Regex(@"^,"), TokenType.Comma),
                (new Regex(@"^="), TokenType.Equals),
                (new Regex(@"^\\"), TokenType.Backslash),
                (new Regex(@"^=>"), TokenType.Lambda),
                (new Regex(@"^@"), TokenType.At),
                (new Regex(@"^\?"), TokenType.QuestionMark),
                (new Regex(@"^#"), TokenType.Hash),

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

            for (int i = 0; i < Tokens.Length; i++)
            {
                Console.WriteLine($"{$"{Tokens[i].Place.Line} : {Tokens[i].Place.Index}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
            }

            //!!! end optional !!!

            //Parse, see StatementPattern struct and NextStatement function
            if (StatementPatterns == null) StatementPatterns = new StatementPattern[]
            {
                //keyword blocks
                new StatementPattern("t|=s", StatementType.Main, new[]{ TokenType.MainWord }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Element, new[]{ TokenType.ElementWord }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Struct, new[]{ TokenType.StructWord }, null, null, null),
                new StatementPattern("t|!=i|=n", StatementType.Enum, new[]{ TokenType.EnumWord }, null, null, null),

                //control flow statements
                new StatementPattern("=b", StatementType.Scope, null, null, null, null),
                new StatementPattern("t|!=i|=s", StatementType.Label, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, null),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, null),
                new StatementPattern("t|=n|!e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null, null),
                new StatementPattern("t|!t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, new[]{ "(", ")" }, null),
                new StatementPattern("t|=s", StatementType.Else, new[]{ TokenType.ElseWord }, null, null, null),

                //field/variable declaration
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PrivateWord | TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PrivateWord | TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PublicWord | TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PublicWord | TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=n|=i|=b", StatementType.PropertyDeclaration, new[] { TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.PublicWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=n|=n|!e", StatementType.VariableDeclaration, new[] { TokenType.VisibleWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),

                new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, null, null),

                //convertions
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ExplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.ImplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, null),
                new StatementPattern("=t|=n|!t|=n|!t|=n", StatementType.Convertion, new[] { TokenType.AutomaticWord, TokenType.ToWord, TokenType.Equals }, null, null, null),

                //importing
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, new object[] { false }),
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.UseWord }, null, null, new object[] { true }),

                //no-op
                new StatementPattern("t", StatementType.No_op, new[]{ TokenType.Semicolon }, null, null, null),

                //special stuff
                new StatementPattern("t|=i|!t", StatementType.TranslatorNote, new[]{ TokenType.SecondaryOpen, TokenType.SecondaryClose }, null, null, null),
                new StatementPattern("t|t|=i|!t", StatementType.TranslatorSpecifier, new[]{ TokenType.SecondaryOpen, TokenType.Hash, TokenType.SecondaryClose }, null, null, null),
            };

            //match all tokens into statements
            List<Statement> Statements = new List<Statement>();
            {
                int i = 0;
                int lasti = Tokens.Length - 1;
                while (true)
                {
                    if (i == lasti) break;
                    Statements.Add(NextStatement(ref i));
                }
            }

            //create library and other objects
            Library = new Library { Name = "CompiledLibrary" };
            SymbolStack = new Stack<List<Symbol>>();
            SymbolStack.Push(Library.Symbols);

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
                    Library.Dependencies.Add(n);
                    var l = new Library();
                    var or = l.Read(Config.FormatLibraryFilename(n, LocalPath, true));
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                    or = Project.Current.Import(l, true);
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                }
            }
            StatementIndex--;

            //analyze statements
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                AnalyzeGlobal(Statements[StatementIndex]);
            }

            if (HasErrors) return null;
            return Library;
        }

        //Errors
        static bool HasErrors = false;
        enum Error
        {
            Unknown0,
            CharacterIllegal1,
            TokenIllegal1,
            StatementIllegal0,
            TokenExpected2,
            IdentifierExpected1,
            StringExpected1,
            ValueExpected1,
            TypeExpected1,
            IdentifierNotDefined1,
            MemberNotDefined2,
            IdentifierDefined1,
            MemberDefined2,
            PropertyGetterDefined1,
            PropertySetterDefined1,
            ParameterCountIllegal1,
            GenericCountIllegal1,
            ValueNotInvokable1,
            TypeConvertionIllegal2,
            VariableNotGettable1,
            VariableNotSettable1,
            TooFewTypesInMultiType0,
            ControlOutsideScope0,
            MultipleMain0,
            OperationResultError1,
        }
        static string GetErrorMessage(Error e, params string[] args)
        {
            switch (e)
            {
                case Error.Unknown0: return "Unknown error occured.";
                case Error.CharacterIllegal1: return $"The character '{args[0]}' does not match any token.";
                case Error.TokenIllegal1: return $"The token '{args[0]}' is not legal in this context.";
                case Error.StatementIllegal0: return "This statement is illegal in this context.";
                case Error.TokenExpected2: return $"Expected '{args[0]}', instead got '{args[1]}'.";
                case Error.IdentifierExpected1: return $"Expected identifer, instead got '{args[0]}'.";
                case Error.StringExpected1: return $"String expected, instead got '{args[0]}'.";
                case Error.ValueExpected1: return $"'{args[0]}' is not a value.";
                case Error.TypeExpected1: return $"'{args[0]}' is not a type.";
                case Error.IdentifierNotDefined1: return $"'{args[0]}' is not defined.";
                case Error.MemberNotDefined2: return $"'{args[0]}' is not defined in '{args[1]}'.";
                case Error.IdentifierDefined1: return $"'{args[0]}' is already defined.";
                case Error.MemberDefined2: return $"'{args[0]}' is already defined in '{args[1]}'.";
                case Error.ParameterCountIllegal1: return $"Wrong amount of parameters, {args[0]} expected.";
                case Error.GenericCountIllegal1: return $"Wrong amount of generics, {args[0]} expected.";
                case Error.ValueNotInvokable1: return $"{args[0]} is not invokable.";
                case Error.TypeConvertionIllegal2: return $"Values of type '{args[0]}' cannot be converted to the type '{args[1]}'.";
                case Error.VariableNotGettable1: return $"Cannot get the value of '{args[0]}' in this context.";
                case Error.VariableNotSettable1: return $"Cannot set the value of '{args[1]}' in this context.";
                case Error.TooFewTypesInMultiType0: return "Multitypes must contain at least 2 types.";
                case Error.ControlOutsideScope0: return $"Control statements cannot appear outside scopes.";
                case Error.MultipleMain0: return "Entry point already defined.";
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
            if (tt == ThrowType.Error)
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
            Backslash,
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
            ReturnWord,
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
            InitWord,
            CleanWord,
            EachWord,
            ExplicitWord,
            ImplicitWord,
            AutomaticWord,
            OperatorWord,
            TrueWord,
            FalseWord,
            NotWord,
            AndWord,
            OrWord,

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
                            if (sb[i] == '\n')
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
        void RefNodeList(ref int i, ref List<Node> nl, TokenType end, string endtokenmatch)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add(NextNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
                }
        }
        List<Node> NodeList(ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<Node>();
            RefNodeList(ref i, ref x, end, endtokenmatch);
            return x;
        }
        void RefDualNodeList(ref int i, ref List<(Node, Node)> nl, TokenType end, string endtokenmatch)
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
        List<(Node, Node)> DualNodeList(ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<(Node, Node)>();
            RefDualNodeList(ref i, ref x, end, endtokenmatch);
            return x;
        }
        bool NodeListOrDualNodeList(ref int i, out List<Node> SingleNodeList, out List<(Node, Node)> DualNodeList, TokenType end, string endtokenmatch)
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
        List<string> IdentifierList(ref int i, TokenType end, string endtokenmatch)
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
            Node,
            VariableDeclaration,
            PropertyDeclaration,
            ReturningFunction,
            NonReturningFunction,

            Scope,
            No_op,

            Element,
            Class,
            Struct,
            Enum,

            Convertion,

            Continue,
            Break,
            Return,

            Label,

            Alias,

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
        Statement NextStatement(ref int i)
        {
            //the statement to return
            Statement s = new Statement { Token = i };

            //helper variables for throwing
            Error currentError = Error.Unknown0;
            string[] currentErrorArgs = null;

            //try to match all patterns
            foreach (var p in StatementPatterns)
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
                                var st = NextStatement(ref i);
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
                                    stl.Add(NextStatement(ref i));
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

            //if no match its a node, declaration, or some of the functions
            {
                Node n;
                while (true)
                {
                    int t = i;
                    n = NextNode(ref i);
                    if (t == i)
                    {
                        i++;
                        if (i == Tokens.Length - 1) return s;
                    }
                    else break;
                }
                if (Tokens[i].TokenType == TokenType.Semicolon)
                {
                    i++;
                    s.StatementType = StatementType.Node;
                    s.Data = new object[] { n };
                    return s;
                }
                if (n.NodeType == NodeType.Identifier && n.Child != null && n.Child.NodeType == SecondaryNodeType.ParameterList)
                {
                    s.StatementType = StatementType.NonReturningFunction;
                    s.Data = new object[] { NextStatement(ref i) };
                    return s;
                }
                Node n2 = NextNode(ref i);
                if (n2.NodeType == NodeType.Identifier && n2.Child != null && n2.Child.NodeType == SecondaryNodeType.ParameterList)
                {
                    s.StatementType = StatementType.NonReturningFunction;
                    s.Data = new object[] { NextStatement(ref i), NodeToType(n) };
                    return s;
                }
                if (Tokens[i].TokenType == TokenType.Semicolon) i++;
                else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, ";", Tokens[i].Match);
                s.StatementType = StatementType.VariableDeclaration;
                s.Data = new object[] { n, n2 };
                return s;
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
            Parentheses, //Node                                    //ex: (1 + 1)
            Identifier,                                            //ex: MyVariable
            Literal,                                               //ex: "hello" or 1
            UnaryOperator, //Node                                  //ex: -1
            BinaryOperator, //(Node, Node)                         //ex: 1 * 1
            Assignment, //(Node, Node)                             //ex: x = y
            Record,                                                //ex: [x = 1, y = 2]
            RecordType,                                            //ex: [int x, int y]
            Collection, //List<Node>                               //ex: {1, 2, 3}
            Function, //Node, List<Node, Node>, Statement or Node  //ex: int(int x, int y) => x - y
            FunctionType, //(Node?, List<Node>)                    //ex: func(int, int)
            MultiType, //List<Node>                                //ex: (x, y)
            ParameterList, //List<(Node, Node)>                    //ex: (int x, int y)
            Is, //(Node, Node)                                     //ex: x is y
            IsNot, //(Node, Node)                                  //ex: x is not y
            Not, //Node                                            //ex: not x
            And, //(Node, Node)                                    //ex: x and y
            Or, //(Node, Node)                                     //ex: x or y
            Xor, //(Node, Node)                                    //ex: x xor y
            Object,                                                //ex: object
            True,                                                  //ex: true
            False,                                                 //ex: false
            Null,                                                  //ex: null
            Global,                                                //ex: global::Element.Variable
        }
        class Node
        {
            public NodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
            public int EndToken;
        }
        string NodeToString(Node n)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = n.Token; i < n.EndToken; i++)
            {
                sb.Append(Tokens[i].Match);
            }
            return sb.ToString();
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
            Generics,                     //ex: \int, string/
            ParameterList,                //ex: (int x, int y)
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
        Node NextNode(ref int i)
        {
            //get next primary node
            var n1 = NextPrimaryNode(ref i);
            while (true)
            {
                switch (Tokens[i].TokenType)
                {
                    default:
                        return n1;

                    //When operators have low priority, they take the previous PrimaryNode (n1) and the next PrimaryNode
                    case TokenType.LowOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //When operators have high priority, they take the previous PrimaryNode (n1) and the next Node
                    case TokenType.HighOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //Assignment operators take the previous PrimaryNode and the next Node
                    case TokenType.Equals:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Assignment, Token = n1.Token, EndToken = i };
                        break;

                    //these work like a binary operators
                    case TokenType.IsWord:
                        i++;
                        if (Tokens[i].TokenType == TokenType.NotWord)
                        {
                            i++;
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.IsNot, Token = n1.Token, EndToken = i };
                        }
                        else
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Is, Token = n1.Token, EndToken = i };
                        }
                        break;
                    case TokenType.AndWord:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.And, Token = n1.Token, EndToken = i };
                        break;
                    case TokenType.OrWord:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Or, Token = n1.Token, EndToken = i };
                        break;

                    //Lambda operators take the previous PrimaryNode (node to must be ParameterList) and the next Node or statement if its of types TokenType.Scope or TokenType.Label
                    case TokenType.Lambda:
                        if (n1.NodeType == NodeType.ParameterList)
                        {
                            i++;
                            return new Node { NodeType = NodeType.Function, Data = Tokens[i].TokenType == TokenType.TertiaryOpen || Tokens[i].TokenType == TokenType.At ? NextStatement(ref i) : (object)NextNode(ref i), Token = n1.Token, EndToken = i };
                        }
                        else goto default;
                }
            }
        }

        //The primary node parsing function
        //PrimaryNodes use the Node type
        Node NextPrimaryNode(ref int i)
        {
            bool couldbetype = false;

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
                    couldbetype = true;
                    i++;
                    break;

                //Parantheses and parameter lists
                case TokenType.PrimaryOpen:
                    {
                        i++;
                        if (Tokens[i].TokenType == TokenType.PrimaryClose)
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = new List<(Node, Node)>(0);
                        }

                        var nodes = NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")");
                        if (dual == null)
                        {
                            if (single.Count == 0)
                            {
                                n.NodeType = NodeType.Parentheses;
                                n.Data = single[0];
                            }
                            else
                            {
                                n.NodeType = NodeType.MultiType;
                                couldbetype = true;
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = dual;
                        }
                        break;
                    }

                //not
                case TokenType.NotWord:
                    n.NodeType = NodeType.Not;
                    i++;
                    n.Data = NextPrimaryNode(ref i);
                    break;

                //literals
                case TokenType.DecInteger:
                case TokenType.DecNonInteger:
                case TokenType.String:
                case TokenType.HexInteger:
                case TokenType.BinInteger:
                    n.NodeType = NodeType.Literal;
                    i++;
                    break;

                //keywords
                case TokenType.NullWord:
                    n.NodeType = NodeType.Null;
                    i++;
                    break;
                case TokenType.TrueWord:
                    n.NodeType = NodeType.True;
                    i++;
                    break;
                case TokenType.FalseWord:
                    n.NodeType = NodeType.False;
                    i++;
                    break;
                case TokenType.ObjectWord:
                    n.NodeType = NodeType.Object;
                    couldbetype = true;
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                    n.NodeType = NodeType.UnaryOperator;
                    i++;
                    n.Data = NextPrimaryNode(ref i);
                    break;

                //record
                case TokenType.SecondaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]");
                        if (dual == null)
                        {
                            n.NodeType = NodeType.Record;
                            n.Data = single;
                        }
                        else
                        {
                            n.NodeType = NodeType.RecordType;
                            couldbetype = true;
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
                        couldbetype = true;
                        i++;
                        Node rettype = null;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else if (Tokens[i].TokenType == TokenType.Backslash)
                        {
                            i++;
                            rettype = (NextNode(ref i));
                            if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                            else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        }
                        else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        n.Data = (rettype, NodeList(ref i, TokenType.PrimaryClose, ")"));
                        break;
                    }

                case TokenType.Lambda:
                    {
                        i++;
                        return new Node { NodeType = NodeType.Function, Token = i - 1, Data = ((object)null, new List<(Node, Node)>(0), Tokens[i].TokenType == TokenType.TertiaryOpen || Tokens[i].TokenType == TokenType.At ? NextStatement(ref i) : (object)NextNode(ref i)) };
                    }

                //global context
                case TokenType.Global:
                    {
                        n.NodeType = NodeType.Global;
                        i++;
                        n.Data = NextPrimaryNode(ref i);
                        break;
                    }
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn, !couldbetype)) n.Child = sn;

            n.EndToken = i;
            return n;
        }

        //SecondaryNode parse function
        bool NextSecondaryNode(ref int i, out SecondaryNode n, bool nontype)
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
                            n.Data = UnnamedPlaceholder;
                        }
                        break;
                    }

                //Parentheses and TypedParameterList
                case TokenType.PrimaryOpen:
                    {
                        i++;
                        nontype = true;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")");
                        if (dual == null)
                        {
                            n.NodeType = SecondaryNodeType.Call;
                            n.Data = single;
                            break;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.ParameterList;
                            n.Data = dual;
                            break;
                        }
                    }

                //generic types
                case TokenType.Backslash:
                    {
                        n.NodeType = SecondaryNodeType.Generics;
                        i++;
                        List<Node> Types = new List<Node>(1);
                        n.Data = Types;
                        Types.Add(NextPrimaryNode(ref i));
                        if (Tokens[i].TokenType == TokenType.Comma)
                        {
                            i++;
                            while (true)
                            {
                                Types.Add(NextPrimaryNode(ref i));
                                if (Tokens[i].TokenType == TokenType.Comma) i++;
                                else if (Tokens[i].Match == "/")
                                {
                                    i++;
                                    break;
                                }
                                else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '/");
                            }
                        }
                        else if (Tokens[i].Match == "/") i++;
                        break;
                    }

                //indexer and collection type
                case TokenType.SecondaryOpen:
                    {
                        if (Tokens[i + 1].TokenType == TokenType.SecondaryClose)
                        {
                            n.NodeType = SecondaryNodeType.CollectionTypeSpecifier;
                            i += 2;
                            nontype = true;
                            break;
                        }
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]");
                        if (dual == null)
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
                        if (nontype) return false;
                        n.NodeType = SecondaryNodeType.CollectionValue;
                        i++;
                        n.Data = NodeList(ref i, TokenType.TertiaryClose, "}");
                        break;
                    }
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn, nontype)) n.Child = sn;

            return true;
        }

        //semantics

        static Stack<List<Symbol>> SymbolStack;

        //the symbol the code is currently 'in' and has access to private symbols inside it
        static Symbol Permission;

        bool CanGet(VariableSymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol x = Permission;
            while(x != null)
            {
                if (Project.Current.GetSymbol(s.Parent) == x) return true;
                x = Project.Current.GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanGet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol x = Permission;
            while (x != null)
            {
                if (Project.Current.GetSymbol(s.Parent) == x) return true;
                x = Project.Current.GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(VariableSymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol x = Permission;
            while (x != null)
            {
                if (Project.Current.GetSymbol(s.Parent) == x) return true;
                x = Project.Current.GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol x = Permission;
            while (x != null)
            {
                if (Project.Current.GetSymbol(s.Parent) == x) return true;
                x = Project.Current.GetSymbol(x.Parent);
            }
            return false;
        }

        object MatchIdentifier(string name)
        {
            foreach(var sl in SymbolStack)
            {
                for (int i = 0; i < sl.Count; i++)
                {
                    if (sl[i].Name == name) return sl[i];
                }
            }
            for (int i = 0; i < localAliases.Count; i++)
            {
                if (localAliases[i].Item1 == name) return localAliases[i].Item2;
            }
            for (int i = 0; i < LibrariesInUse.Count; i++)
            {
                if (LibrariesInUse[i].Aliases.ContainsKey(name))
                {
                    object o = LibrariesInUse[i].Aliases[name];
                    if (o is string s) return Project.Current.GetSymbol(s);
                    return o;
                }
            }
            return null;
        }

        static List<LocalValue> locals = new List<LocalValue>();
        static Stack<int> localSeparators = new Stack<int>();
        static List<(string, object)> localAliases = new List<(string, object)>();
        static Stack<int> localAliasSeparators = new Stack<int>();

        static void LocalsPush()
        {
            localSeparators.Push(locals.Count);
            localAliasSeparators.Push(localAliases.Count);
        }
        static void LocalsPop()
        {
            for (int i = localSeparators.Pop(); i < locals.Count; i++)
            {
                locals.RemoveAt(i);
            }
            for (int i = localAliasSeparators.Pop(); i < localAliases.Count; i++)
            {
                localAliases.RemoveAt(i);
            }
        }

        //will convert the value to the correctly typed value
        //type can be null, means that Value will be the values own type, or automatically converted
        //all values MUST be convertable to object
        Value ConvertValue(Value value, _Type type, bool dothrow = false, Place? place = null)
        {
            if (value.Type == type) return value;
            if(type == null)
            {
                for (int i = 0; i < LibrariesInUse.Count; i++)
                {
                    foreach (var x in LibrariesInUse[i].Convertions)
                    {
                        if(x.Value.Item2 == ConvertionType.Automatic && x.Key.Item1 == value.Type && x.Key.Item2 == type)
                        {
                            return new ConvertValue { Base = value, Type = type };
                        }
                    }
                }
                return value;
            }
            if (Project.Current.Convertions.ContainsKey((value.Type, type)))
            {
                return new ConvertValue { Base = value, Type = type };
            }
            if (dothrow)
            {
                Throw(Error.TypeConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
            }
            return NullValue.Value;
        }

        //This function returns the Symbol, Value, or _Type that the Node represents
        //typeContext specifies to what type values should be converted into
        object MatchNode(Node node, bool prioritizeSymbol, _Type typeContext = null)
        {
            Value value = null;
            _Type type = null;
            Symbol symbol = null;
            SecondaryNode childNode = node.Child;

            //initial
            switch (node.NodeType)
            {
                case NodeType.Global:
                    {
                        node = (Node)node.Data;
                        if (node.NodeType == NodeType.Identifier)
                        {
                            string name = Tokens[node.Token].Match;
                            for (int i = 0; i < Library.Symbols.Count; i++)
                            {
                                if (Library.Symbols[i].Name == name)
                                {
                                    symbol = Library.Symbols[i];
                                    goto symbolfound;
                                }
                            }
                            symbol = Project.Current.GetSymbol(name);
                            if (symbol != null) goto symbolfound;
                            Throw(Error.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, name);
                            return null;

                            symbolfound:
                            while (childNode != null && childNode.NodeType == SecondaryNodeType.Member)
                            {
                                name = Tokens[childNode.Token + 1].Match;
                                var s = symbol.Children.Find(x => x.Name == name);
                                if (symbol == null)
                                {
                                    Throw(Error.MemberDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, symbol.ToString());
                                    return null;
                                }
                                symbol = s;
                            }
                        }
                        else
                        {
                            Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                            return null;
                        }
                        break;
                    }
                case NodeType.Identifier:
                    {
                        object o = MatchIdentifier(Tokens[node.Token].Match);
                        if (o is Value vv) value = ConvertValue(vv, typeContext);
                        else if (o is _Type tt) type = tt;
                        else if (o is Symbol ss)
                        {
                            symbol = ss;
                            while(childNode != null && childNode.NodeType == SecondaryNodeType.Member && symbol is ElementSymbol)
                            {
                                var s = symbol.Children.Find(x => x.Name == Tokens[childNode.Token + 1].Match);
                                if (s == null)
                                {
                                    Throw(Error.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, Tokens[childNode.Token + 1].Match, symbol.ToString());
                                    return null;
                                }
                                symbol = s;
                                childNode = childNode.Child;
                            }
                        }
                        else
                        {
                            Throw(Error.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                            return null;
                        }
                        break;
                    }
                case NodeType.Literal:
                    {
                        Token t = Tokens[node.Token];
                        switch (t.TokenType)
                        {
                            case TokenType.String:
                                value = new LiteralValue { LiteralType = LiteralValueType.String, Value = t.Match.Substring(1, t.Match.Length - 2) };
                                break;
                            case TokenType.DecInteger:
                                value = new LiteralValue { LiteralType = LiteralValueType.Integer, Value = t.Match };
                                break;
                            case TokenType.DecNonInteger:
                                value = new LiteralValue { LiteralType = LiteralValueType.Fractional, Value = t.Match };
                                break;
                            case TokenType.HexInteger:
                                {
                                    BigInteger v = new BigInteger();
                                    BigInteger sixteen = 16;
                                    for (int i = 0; i < t.Match.Length; i++)
                                    {
                                        if (t.Match[i] != '0') v += BigInteger.Pow(sixteen, t.Match.Length - i + 1) * Convert.ToInt32(t.Match[i].ToString(), 16);
                                    }
                                    value = new LiteralValue { LiteralType = LiteralValueType.Integer, Value = v.ToString() };
                                    break;
                                }
                            case TokenType.BinInteger:
                                {
                                    BigInteger v = new BigInteger();
                                    BigInteger two = 2;
                                    for (int i = 0; i < t.Match.Length; i++)
                                    {
                                        if (t.Match[i] == '1') v += BigInteger.Pow(two, t.Match.Length - i + 1);
                                    }
                                    value = new LiteralValue { LiteralType = LiteralValueType.Integer, Value = v.ToString() };
                                    break;
                                }
                        }
                        break;
                    }
                case NodeType.Null:
                    value = NullValue.Value;
                    break;
                case NodeType.True:
                    value = LiteralValue.True;
                    break;
                case NodeType.False:
                    value = LiteralValue.False;
                    break;
                case NodeType.Object:
                    type = ObjectType.Value;
                    break;
                case NodeType.Record:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var record = new RecordValue();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            if (nodeList[i].NodeType == NodeType.Assignment)
                            {
                                var nodes = ((Node, Node))nodeList[i].Data;
                                string name;
                                if (nodes.Item1.NodeType == NodeType.Identifier)
                                {
                                    name = Tokens[nodes.Item1.Token].Match;
                                }
                                else
                                {
                                    name = UnnamedPlaceholder;
                                }
                                record.Values.Add((name, NodeToValue(nodes.Item2, null)));
                            }
                        }
                        break;
                    }
                case NodeType.RecordType:
                    {
                        var nodeList = (List<(Node, Node)>)node.Data;
                        var record = new RecordType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            string name;
                            if (nodeList[i].Item2.NodeType == NodeType.Identifier)
                            {
                                name = Tokens[nodeList[i].Item2.Token].Match;
                            }
                            else
                            {
                                name = UnnamedPlaceholder;
                            }
                            if (!record.Fields.Add((name, NodeToType(nodeList[i].Item2))))
                            {
                                Throw(Error.IdentifierDefined1, ThrowType.Error, Tokens[nodeList[i].Item2.Token].Place, name);
                            }
                        }
                        break;
                    }
                case NodeType.Collection:
                    {
                        var nodeList = (List<Node>)node.Data;
                        if (nodeList.Count == 0) return new CollectionValue { Type = new CollectionType { Base = ObjectType.Value } };
                        var collection = new CollectionValue();
                        collection.Values.Add(NodeToValue(nodeList[0], null));
                        _Type elementtype = collection.Values[0].Type;
                        for (int i = 1; i < nodeList.Count; i++)
                        {
                            collection.Values.Add(NodeToValue(nodeList[i], elementtype));
                        }
                        collection.Type = new CollectionType { Base = elementtype };
                        value = collection;
                        break;
                    }
                case NodeType.MultiType:
                    {
                        var nodelist = (List<Node>)node.Data;
                        if (nodelist.Count < 2)
                        {
                            Throw(Error.TooFewTypesInMultiType0, ThrowType.Error, Tokens[node.Token].Place);
                            if (nodelist.Count == 1)
                            {
                                return NodeToType(nodelist[0]);
                            }
                            return null;
                        }
                        var multi = new MultiType();
                        for (int i = 0; i < nodelist.Count; i++)
                        {
                            multi.Types.Add(NodeToType(nodelist[i]));
                        }
                        type = multi;
                        break;
                    }
            }

            //convert from symbol to value or type
            if(symbol != null)
            {
                if (prioritizeSymbol && childNode == null) return symbol;

                if (symbol is ClassSymbol cs)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), Boxed = false, RefType = false };

                    //generics
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.Generics)
                    {
                        List<Node> nl = (List<Node>)childNode.Data;
                        childNode = childNode.Child;
                        for (int i = 0; i < nl.Count; i++)
                        {
                            normalType.Generics.Add(NodeToType(nl[i]));
                        }
                        if (nl.Count != cs.GenericCount) Throw(Error.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, cs.GenericCount.ToString());
                    }
                    else if (cs.GenericCount != 0) return symbol;

                    type = normalType;
                }
                else if (symbol is StructSymbol ss)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), Boxed = false, RefType = true };

                    //see if boxed
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.BoxedSpecifier)
                    {
                        normalType.Boxed = true;
                        normalType.RefType = true;
                        childNode = childNode.Child;
                    }

                    type = normalType;
                }
                else if (symbol is VariableSymbol vs)
                {
                    value = ConvertValue(new SymbolValue { Base = vs.ToString(), Type = vs.Type }, typeContext);
                }
            }

            if (value == null && type == null) return symbol;

            //additions
            while (childNode != null)
            {
                if (value != null)
                {
                    if (childNode.NodeType == SecondaryNodeType.Call)
                    {
                        if (value.Type is FunctionType ft)
                        {
                            var callValue = new CallValue { Function = value };
                            List<Node> nl = (List<Node>)childNode.Data;
                            if (nl.Count != ft.Parameters.Count) Throw(Error.ParameterCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, ft.Parameters.Count.ToString());
                            int limit = nl.Count < ft.Parameters.Count ? nl.Count : ft.Parameters.Count;
                            for (int i = 0; i < limit; i++)
                            {
                                callValue.Parameters.Add(NodeToValue(nl[i], ft.Parameters[i].Item1));
                            }
                            value = ConvertValue(callValue, typeContext);
                        }
                        else
                        {
                            Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                        }
                    }
                    else if (childNode.NodeType == SecondaryNodeType.Member)
                    {
                        string name = Tokens[childNode.Token + 1].Match;
                        if (value.Type is NormalType nt)
                        {
                            var variableSymbol = (VariableSymbol)Project.Current.GetSymbol(nt.Base).Children.Find(x => x.Name == name);
                            if (variableSymbol == null)
                            {
                                Throw(Error.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, nt.Base);
                            }
                            else
                            {
                                value = ConvertValue(new MemberValue { Base = value, Name = name, Type = variableSymbol.Type }, typeContext);
                            }
                        }
                        else if (value.Type is RecordType rt)
                        {
                            var field = rt.Fields.First(x => x.Item1 == name);
                            if (field.Item1 == null)
                            {
                                Throw(Error.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, rt.ToString());
                            }
                            else
                            {
                                value = new MemberValue { Base = value, Name = name, Type = field.Item2 };
                            }
                        }
                        else
                        {
                            Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                        }
                    }
                }
                else if (type != null)
                {
                    if (childNode.NodeType == SecondaryNodeType.CollectionTypeSpecifier)
                    {
                        type = new CollectionType { Base = type };
                    }
                    else if (childNode.NodeType == SecondaryNodeType.CollectionValue)
                    {
                        value = ConvertValue(new CollectionValue { Type = type }, null);
                    }
                    else
                    {
                        Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                        break;
                    }
                }

                childNode = childNode.Child;
            }

            if (value != null) return value;
            return type;
        }
        Value NodeToValue(Node n, _Type typeContext)
        {
            object o = MatchNode(n, false, typeContext);
            if (o is Value v) return v;
            if (o != null) Throw(Error.ValueExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return NullValue.Value;
        }
        _Type NodeToType(Node n)
        {
            object o = MatchNode(n, false);
            if (o is _Type t) return t;
            if (o != null) Throw(Error.TypeExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return ObjectType.Value;
        }
        Procedure StatementToProcedure(Statement s)
        {
            var p = new Procedure();
            LocalsPush();
            p.Instructions.Add(StatementToInstruction(s));
            LocalsPop();
            return p;
        }
        static Instruction StatementToInstruction(Statement s)
        {
            return new Instruction();
        }

        //structural analysis
        void AnalyzeGlobal(Statement s)
        {
            switch (s.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[s.Token].Place);
                    break;

                case StatementType.Main:
                    {
                        Statement st = (Statement)s.Data[0];
                        if(st.StatementType == StatementType.Break || st.StatementType == StatementType.Continue)
                        {
                            Throw(Error.ControlOutsideScope0, ThrowType.Error, Tokens[st.Token].Place);
                            break;
                        }
                        var p = StatementToProcedure(st);
                        if (Project.Current.EntryPoint != null) Throw(Error.MultipleMain0, ThrowType.Error, Tokens[s.Token].Place);
                        Library.EntryPoint = p;
                        break;
                    }
                case StatementType.Element:
                    {
                        string name = (string)s.Data[0];
                        ElementSymbol sy = new ElementSymbol { Name = name };
                        if (MatchIdentifier(name) != null)
                        {
                            Throw(Error.IdentifierDefined1, ThrowType.Error, Tokens[s.Token].Place, name);
                        }
                        else
                        {
                            Library.Symbols.Add(sy);
                        }
                        List<Statement> stl = (List<Statement>)s.Data[1];
                        SymbolStack.Push(sy.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElement(stl[i], sy);
                        }
                        SymbolStack.Pop();
                        break;
                    }
                case StatementType.Alias:
                    {
                        Node n = (Node)s.Data[0];
                        if(n.NodeType == NodeType.Assignment)
                        {
                            var nodes = ((Node, Node))n.Data;
                            string name = null;
                            if (nodes.Item1.NodeType == NodeType.Identifier)
                            {
                                name = Tokens[nodes.Item1.Token].Match;
                                if (MatchIdentifier(name) != null)
                                {
                                    Throw(Error.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                    name = null;
                                }
                            }
                            else
                            {
                                Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, Tokens[nodes.Item1.Token].Match);
                            }
                            object value = MatchNode(nodes.Item2, true);
                            if (name != null) Library.Aliases.Add(name, value);
                        }
                        break;
                    }
            }
        }
        void AnalyzeElement(Statement statement, ElementSymbol element)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    {
                        AnalyzeVariable(statement, element);
                        break;
                    }
                case StatementType.PropertyDeclaration:
                    {
                        AnalyzeProperty(statement, element);
                        break;
                    }
                case StatementType.Class:
                    {
                        string name = (string)statement.Data[0];
                        ClassSymbol sy = new ClassSymbol { Name = name, Parent = element.ToString() };
                        if(element.Children.Find(x => x.Name == name) != null)
                        {
                            Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(sy);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        SymbolStack.Push(sy.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeClass(stl[i], sy);
                        }
                        SymbolStack.Pop();
                        break;
                    }
                case StatementType.Struct:
                    {
                        string name = (string)statement.Data[0];
                        StructSymbol sy = new StructSymbol { Name = name, Parent = element.ToString() };
                        if (element.Children.Find(x => x.Name == name) != null)
                        {
                            Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(sy);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        SymbolStack.Push(sy.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeStruct(stl[i], sy);
                        }
                        SymbolStack.Pop();
                        break;
                    }
            }
        }
        //variable and property analyzers are the same in element, class, struct so they are functions
        void AnalyzeVariable(Statement statement, Symbol symbol)
        {
            var getType = (AccessorType)statement.Data[0];
            var setType = (AccessorType)statement.Data[1];
            var type = NodeToType((Node)statement.Data[2]);
            var valueNode = (Node)statement.Data[3];

            string name = null;
            Value value = null;

            if (valueNode.NodeType == NodeType.Assignment)
            {
                var nodes = ((Node, Node))valueNode.Data;
                if (nodes.Item1.NodeType == NodeType.Identifier)
                {
                    name = Tokens[nodes.Item1.Token].Match;
                    if (symbol.Children.Find(x => x.Name == name) != null)
                    {
                        Throw(Error.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, symbol.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                }
                value = NodeToValue(nodes.Item2, type);
            }
            else
            {
                value = NullValue.Value;
                if (valueNode.NodeType == NodeType.Identifier)
                {
                    name = Tokens[valueNode.Token].Match;
                    if (symbol.Children.Find(x => x.Name == name) != null)
                    {
                        Throw(Error.MemberDefined2, ThrowType.Error, Tokens[valueNode.Token].Place, name, symbol.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[valueNode.Token].Place, Tokens[valueNode.Token].Match);
                }
            }

            if (name != null)
            {
                symbol.Children.Add(new VariableSymbol { Name = name, Parent = symbol.ToString(), Type = type, DefaultValue = value, Get = getType, Set = setType });
            }
        }
        void AnalyzeProperty(Statement statement, Symbol symbol)
        {
            var GetType = (AccessorType)statement.Data[0];
            var SetType = (AccessorType)statement.Data[1];
            var type = NodeToType((Node)statement.Data[2]);
            var name = (string)statement.Data[3];
            var st = (Statement)statement.Data[4];

            var prop = new PropertySymbol { Get = GetType, Set = SetType, Name = name, Type = type, Parent = symbol.ToString() };
            if (GetType == AccessorType.None)
            {
                prop.Setter = StatementToProcedure(st);
            }
            else
            {
                prop.Getter = StatementToProcedure(st);
            }

            var sy = symbol.Children.Find(x => x.Name == name);
            if (sy == null)
            {
                symbol.Children.Add(sy);
            }
            else if (sy is PropertySymbol existingProp)
            {
                if (GetType == AccessorType.None)
                {
                    if (existingProp.Get == AccessorType.None)
                    {
                        Throw(Error.PropertySetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
                    }
                    else
                    {
                        existingProp.Set = prop.Set;
                        existingProp.Setter = prop.Setter;
                    }
                }
                else if (SetType == AccessorType.None)
                {
                    if (existingProp.Set == AccessorType.None)
                    {
                        Throw(Error.PropertyGetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
                    }
                    else
                    {
                        existingProp.Get = prop.Get;
                        existingProp.Getter = prop.Getter;
                    }
                }
            }
            else
            {
                Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, symbol.ToString());
            }
        }
        void AnalyzeClass(Statement s, ClassSymbol c)
        {
            switch (s.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[s.Token].Place);
                    break;

            }
        }
        void AnalyzeStruct(Statement s, StructSymbol stru)
        {
            switch (s.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[s.Token].Place);
                    break;

            }
        }
    }
}
