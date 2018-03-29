using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Pace.CommonLibrary;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)

using _Type = Pace.CommonLibrary.Type;

//pacednc compiles source files to Package objects (implemented in pacednl)

namespace Pace.Compiler
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
        //convert a source file to a pace portable package

        static string IdentifierCharMatch = @"\w";
        static string LowOperatorChars = @"-+!#¤%£$€´`~:";
        static string HighOperatorChars = @"*/|&^";
        static string ReservedOperatorChars = @"=@?<>\.";

        static string UnnamedPlaceholder = "<unnamed>";

        static List<Node> EmptyNodeList = new List<Node>();

        static StatementPattern[] StatementPatterns;

        Token[] Tokens;
        List<Package> LibrariesInUse = new List<Package>();
        Package Package;

        public Package Compile(string Source, string LocalPath)
        {

            //Tokenize, see Tokenize function below
            Tokens = Tokenize(Source, new List<(Regex, TokenType)>
            {
                //keywords
                (new Regex(@"^element"), TokenType.ElementWord),
                (new Regex(@"^class"), TokenType.ClassWord),
                (new Regex(@"^struct"), TokenType.StructWord),
                (new Regex(@"^return"), TokenType.ReturnWord),
                (new Regex(@"^null"), TokenType.NullWord),
                (new Regex(@"^object"), TokenType.ObjectWord),
                (new Regex(@"^enum"), TokenType.EnumWord),
                (new Regex(@"^get"), TokenType.GetWord),
                (new Regex(@"^set"), TokenType.SetWord),
                (new Regex(@"^alias"), TokenType.AliasWord),
                (new Regex(@"^if"), TokenType.IfWord),
                (new Regex(@"^when"), TokenType.WhenWord),
                (new Regex(@"^then"), TokenType.ThenWord),
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
                (new Regex(@"^init"), TokenType.InitWord),
                (new Regex(@"^clean"), TokenType.CleanWord),
                (new Regex(@"^for"), TokenType.ForWord),
                (new Regex(@"^yield"), TokenType.YieldWord),
                (new Regex(@"^explicit"), TokenType.ExplicitWord),
                (new Regex(@"^implicit"), TokenType.ImplicitWord),
                (new Regex(@"^automatic"), TokenType.AutomaticWord),
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

                //reserved operators
                (new Regex(@"^\."), TokenType.Period),
                (new Regex(@"^;"), TokenType.Semicolon),
                (new Regex(@"^,"), TokenType.Comma),
                (new Regex(@"^="), TokenType.Equals),
                (new Regex(@"^<"), TokenType.LeftAngleBracket),
                (new Regex(@"^>"), TokenType.RightAngleBracket),
                (new Regex(@"^=>"), TokenType.Lambda),
                (new Regex(@"^@"), TokenType.At),
                (new Regex(@"^\?"), TokenType.QuestionMark),
                (new Regex(@"^#"), TokenType.Hash),

                //custom operators
                (new Regex($@"^[{LowOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]?"), TokenType.LowOperator),
                (new Regex($@"^[{HighOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]?"), TokenType.HighOperator),
                (new Regex($@"^[{ReservedOperatorChars}][{LowOperatorChars}{HighOperatorChars}{ReservedOperatorChars}]"), TokenType.LowOperator),

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

                //executable statements
                new StatementPattern("=b", StatementType.Scope, null, null, null, null),
                new StatementPattern("t|!=i|=s", StatementType.Label, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, null),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, null),
                new StatementPattern("t|e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null, new object[] { null }),
                new StatementPattern("t|=n|!e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null, null),
                new StatementPattern("t|=n|!e", StatementType.Yield, new[]{ TokenType.YieldWord }, null, null, null),
                new StatementPattern("t|t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, new[]{ "(", ")" }, null),
                new StatementPattern("t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.ThenWord }, null, new[]{ "then" }, null),
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

                //keyword blocks
                new StatementPattern("t|=s", StatementType.Main, new[]{ TokenType.MainWord }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Element, new[]{ TokenType.ElementWord }, null, null, null),
                new StatementPattern("t|!=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, new object[] { string.Empty }),
                new StatementPattern("t|!=i|!=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, null),
                new StatementPattern("t|!t|=l|-|!=b", StatementType.Class, new[]{ TokenType.ClassWord, TokenType.LeftAngleBracket }, null, null, new object[] { string.Empty }),
                new StatementPattern("t|!=i|!=b", StatementType.Struct, new[]{ TokenType.StructWord }, null, null, null),
                new StatementPattern("t|!=i|=n", StatementType.Enum, new[]{ TokenType.EnumWord }, null, null, null),

                //convertions
                new StatementPattern("t|=n|!t|=p|!t|=n", StatementType.Convertion, new[] { TokenType.ExplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, new object[] { ConvertionType.Explicit }),
                new StatementPattern("t|=n|!t|=p|!t|=n", StatementType.Convertion, new[] { TokenType.ImplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, new object[] { ConvertionType.Implicit }),
                new StatementPattern("t|=n|!t|=p|!t|=n", StatementType.Convertion, new[] { TokenType.AutomaticWord, TokenType.ToWord, TokenType.Equals }, null, null, new object[] { ConvertionType.Automatic }),

                //importing
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, new object[] { false }),
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.UseWord }, null, null, new object[] { true }),

                //no-op
                new StatementPattern("e", StatementType.No_op, null, null, null, null),
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

            //create package and other objects
            Package = new CommonLibrary.Package { Name = "CompiledPackage" };
            SymbolStack = new Stack<List<Symbol>>();
            SymbolStack.Push(Project.Current.Symbols);
            SymbolStack.Push(Package.Symbols);

            //these libraries are "in use"
            LibrariesInUse.Add(Package);

            //import and translator specifier statements, must be before any other statements
            //this library depends on these files, 
            //they must exist when translating or importing or doing whatever with this package
            int StatementIndex = 0;
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                if (Statements[StatementIndex].StatementType == StatementType.Import)
                {
                    string n = (string)Statements[StatementIndex].Data[1];
                    Package.Dependencies.Add(n);
                    var l = new Package();
                    var or = l.Read(Config.FormatPackageFilename(n, LocalPath, true));
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                    or = Project.Current.Import(l, true);
                    if (!or.IsSuccessful) Throw(Error.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                }
                else break;
            }

            //analyze statements
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                AnalyzeGlobalStatement(Statements[StatementIndex]);
            }

            //process pending vars/props

            if (HasErrors) return null;
            return Package;
        }

        //Errors
        bool HasErrors = false;
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
            ValueNotGettable1,
            ValueNotSettable1,
            PropertyTypeMismatch0,
            CannotAssignTyplessValue0,
            TooFewTypesInMultiType0,
            ValueTypeCycle0,
            ElseNotAfterIf0,
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
                case Error.ValueNotGettable1: return $"Cannot get the value of '{args[0]}' in this context.";
                case Error.ValueNotSettable1: return $"Cannot set the value of '{args[1]}' in this context.";
                case Error.PropertyTypeMismatch0: return $"The getter and setter of a property much be the of the same type.";
                case Error.CannotAssignTyplessValue0: return $"Cannot assign typeless value to a variable. Did you forget to return a value?";
                case Error.TooFewTypesInMultiType0: return "Multitypes must contain at least 2 types.";
                case Error.ValueTypeCycle0: return "A variable of this type in this context causes a cycle.";
                case Error.ElseNotAfterIf0: return "Else statement must be after an if statement.";
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
        void Throw(Error e, ThrowType tt, Place? p, params string[] args)
        {
            Console.WriteLine();
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
            LeftAngleBracket,
            RightAngleBracket,
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
            WhenWord,
            ThenWord,
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
            ForWord,
            YieldWord,
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
        Token[] Tokenize(string text, List<(Regex, TokenType)> matches)
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
        void RefPrimaryNodeList(ref int i, ref List<Node> nl, TokenType end, string endtokenmatch)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add(NextPrimaryNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
                }
        }
        List<Node> PrimaryNodeList(ref int i, TokenType end, string endtokenmatch)
        {
            var x = new List<Node>();
            RefPrimaryNodeList(ref i, ref x, end, endtokenmatch);
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
        List<Node> GenericList(ref int i)
        {
            if (Tokens[i].TokenType != TokenType.LeftAngleBracket) return null;

            //check to see if it's closed with RightAngleBracket
            //if we encounter a semicolon or a brace that is not opened we stop
            int primaries = 0;
            int secondaries = 0;
            int tertiaries = 0;
            int angles = 0;
            for (int i2 = i + 1; i2 < Tokens.Length; i2++)
            {
                switch (Tokens[i2].TokenType)
                {
                    default:
                        break;
                    case TokenType.Semicolon:
                        return null;
                    case TokenType.RightAngleBracket:
                        if (angles-- == 0) goto genericsClosed;
                        break;
                    case TokenType.PrimaryClose:
                        if (primaries-- == 0) return null;
                        break;
                    case TokenType.SecondaryClose:
                        if (secondaries-- == 0) return null;
                        break;
                    case TokenType.TertiaryClose:
                        if (tertiaries-- == 0) return null;
                        break;
                }
            }
            return null;

        genericsClosed:
            i++;
            return PrimaryNodeList(ref i, TokenType.RightAngleBracket, ">");
        }

        //statement parsing
        enum StatementType : byte
        {
            Node,
            VariableDeclaration,
            PropertyDeclaration,

            Scope,
            No_op,

            Element,
            Class,
            Struct,
            Enum,

            Init,
            Clean,

            Convertion,

            Continue,
            Break,
            Return,
            Yield,

            Label,

            Alias,

            Main,
            Initialize,
            Finalize,

            If,
            Else,

            Each,

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

                //current position in the matches array
                int matchindex = 0;

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
                        //token, tokentype from the Patterns array (see above)
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

                        //match, matches a token with the match from the matches array
                        case 'm':
                            if (Tokens[i].Match != p.Matches[matchindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Error.TokenExpected2;
                                    currentErrorArgs = new[] { p.Matches[matchindex - 1], Tokens[i].Match };
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

                        //primary node, will always match and throw if invalid
                        case 'p':
                            {
                                var n = NextPrimaryNode(ref i);
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
                                if (Tokens[i].TokenType == TokenType.Semicolon) i++;
                                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose) { }
                                else
                                {
                                    if (dothrow)
                                    {
                                        currentError = Error.TokenExpected2;
                                        currentErrorArgs = new[] { ";", Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
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

                        //node list, matches anything and ends with a specified token
                        case 'l':
                            {
                                var l = NodeList(ref i, p.TokenTypes[tokentypeindex++], p.Misc[miscindex++]);
                                if (save) data.Add(l);
                                break;
                            }

                        //backtrack, move one token backward
                        case '-':
                            i--;
                            break;
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
                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose)
                {
                    s.StatementType = StatementType.Node;
                    s.Data = new object[] { n };
                    return s;
                }
                Node n2 = NextNode(ref i);
                if (Tokens[i].TokenType == TokenType.Semicolon) i++;
                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose) { }
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
            Procedural, //(Statement)                              //ex: { return }
            Conditional, //(List<(Node, Node)>, Node)              //ex: when x == y then x when x == z then z else y
            Record,                                                //ex: [x = 1, y = 2]
            RecordType,                                            //ex: [int x, int y]
            Collection, //List<Node>                               //ex: {1, 2, 3}
            Function, //(Node, Statement or Node)                  //ex: int(int x, int y) => x - y
            FunctionDeclaration, //Node, (Statement or Node)       //ex: FunctionName(int x, int y) = x - y
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
            Generics,                     //ex: <int, string>
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
                    case TokenType.LeftAngleBracket:
                    case TokenType.RightAngleBracket:
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
                        if (n1.NodeType == NodeType.Identifier && n1.Child != null && (n1.Child.NodeType == SecondaryNodeType.ParameterList || (n1.Child.NodeType == SecondaryNodeType.Call && ((List<Node>)n1.Child.Data).Count == 0)))
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.FunctionDeclaration, Token = n1.Token, EndToken = i };
                        }
                        else
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Assignment, Token = n1.Token, EndToken = i };
                        }
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
                        if (n1.NodeType == NodeType.ParameterList || (n1.NodeType == NodeType.Identifier && n1.Child != null && n1.Child.NodeType == SecondaryNodeType.ParameterList))
                        {
                            i++;
                            return new Node { NodeType = NodeType.Function, Data = (n1, NextNode(ref i)), Token = n1.Token, EndToken = i };
                        }
                        else goto default;
                }
            }
        }

        //The primary node parsing function
        //PrimaryNodes use the Node type
        Node NextPrimaryNode(ref int i)
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
                        i++;
                        if (Tokens[i].TokenType == TokenType.PrimaryClose)
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = (new List<(Node, Node)>(0));
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
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = (new List<Node>(0), dual);
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
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                case TokenType.RightAngleBracket:
                    n.NodeType = NodeType.UnaryOperator;
                    i++;
                    n.Data = NextPrimaryNode(ref i);
                    break;

                //might be a function signature
                case TokenType.LeftAngleBracket:
                    {
                        var generics = GenericList(ref i);

                        //if not generics act as unary operator
                        if (generics == null) goto case TokenType.LowOperator;

                        n.NodeType = NodeType.ParameterList;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(");
                        n.Data = (generics, DualNodeList(ref i, TokenType.PrimaryClose, ")"));
                        break;
                    }

                //record
                case TokenType.SecondaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]");
                        if (dual == null)
                        {
                            if (single.Count == 0 || single[0].NodeType != NodeType.Assignment)
                            {
                                n.NodeType = NodeType.Collection;
                                n.Data = single;
                            }
                            else
                            {
                                n.NodeType = NodeType.Record;
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.RecordType;
                            n.Data = dual;
                        }
                        break;
                    }

                //Procedural values are like functions that take no parameters and return a value,
                //this is the value that this node is equal to
                case TokenType.TertiaryOpen:
                case TokenType.At:
                    n.NodeType = NodeType.Procedural;
                    n.Data = NextStatement(ref i);
                    break;

                //conditional values work like this:
                //when (condition) then (value if condition is true)
                //when (other condition) then (value if other condition is true)
                //else (value when all the conditions are false)
                case TokenType.WhenWord:
                    {
                        n.NodeType = NodeType.Conditional;
                        i++;
                        List<(Node, Node)> conditions = new List<(Node, Node)>();
                        Node elseValue;
                        while (true)
                        {
                            var cond = NextNode(ref i);
                            if (Tokens[i].TokenType == TokenType.ThenWord) i++;
                            else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "then", Tokens[i].Match);
                            conditions.Add((cond, NextNode(ref i)));
                            if (Tokens[i].TokenType == TokenType.WhenWord) continue;
                            if (Tokens[i].TokenType == TokenType.ElseWord)
                            {
                                i++;
                                elseValue = NextNode(ref i);
                                break;
                            }
                            else
                            {
                                Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "when' or 'else", Tokens[i].Match);
                            }
                        }
                        n.Data = (conditions, elseValue);
                        break;
                    }

                //function type
                case TokenType.FuncWord:
                    {
                        n.NodeType = NodeType.FunctionType;
                        i++;
                        List<Node> generics = new List<Node>();
                        if (Tokens[i].TokenType == TokenType.LeftAngleBracket)
                        {
                            i++;
                            RefPrimaryNodeList(ref i, ref generics, TokenType.RightAngleBracket, ">");
                        }
                        Node rettype = null;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else if (Tokens[i].TokenType == TokenType.LeftAngleBracket)
                        {
                            i++;
                            rettype = (NextNode(ref i));
                            if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                            else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        }
                        else Throw(Error.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        n.Data = (generics, rettype, NodeList(ref i, TokenType.PrimaryClose, ")"));
                        break;
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
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            n.EndToken = i;
            return n;
        }

        //SecondaryNode parse function
        bool NextSecondaryNode(ref int i, out SecondaryNode n)
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
                case TokenType.LeftAngleBracket:
                    {
                        var l = GenericList(ref i);
                        if (l == null) return false;
                        n.NodeType = SecondaryNodeType.Generics;
                        n.Data = l;
                        break;
                    }

                //indexer and collection type
                case TokenType.SecondaryOpen:
                    {
                        if (Tokens[i + 1].TokenType == TokenType.SecondaryClose)
                        {
                            n.NodeType = SecondaryNodeType.CollectionTypeSpecifier;
                            i += 2;
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
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            return true;
        }

        //semantics

        Stack<List<Symbol>> SymbolStack;

        //the symbol the code is currently 'in' and has access to private symbols inside it
        Symbol Permission;

        //name of generic types when declaring a type
        List<string> GenericNames;

        //generic types when declaring type or function
        //also, the actual types of generic types when getting, setting, calling something from a value of a type with generics
        List<(GenericType, _Type)> GenericTypes;

        //The local variables of the current function or functions
        List<LocalValue> Locals = new List<LocalValue>();

        //the indexes where the first local of a function is
        Stack<int> LocalSeparators = new Stack<int>();

        //the local aliases, first string is the name, second is the symbol
        List<(string, string)> LocalAliases = new List<(string, string)>();

        //same idea as LocalSeparators
        Stack<int> LocalAliasSeparators = new Stack<int>();

        //Scope names
        Stack<Stack<string>> Scopes;

        //true if the last instruction was an if
        bool LastWasIf = false;

        //These functions "push" and "pop" (as in a stack) the locals and local aliases
        void LocalsPush()
        {
            LocalSeparators.Push(Locals.Count);
            LocalAliasSeparators.Push(LocalAliases.Count);
        }
        void LocalsPop()
        {
            for (int i = LocalSeparators.Pop(); i < Locals.Count; i++)
            {
                Locals.RemoveAt(i);
            }
            for (int i = LocalAliasSeparators.Pop(); i < LocalAliases.Count; i++)
            {
                LocalAliases.RemoveAt(i);
            }
        }

        //These functions return true if the code currently has permission to get or set the variable or property
        //it depends on if the item is private or public, and the current Permission (see above)
        bool CanGet(VariableSymbol s)
        {
            //The item does not have a getter
            if (s.Get == AccessorType.None) return false;

            //publics can be accessed anywhere
            if (s.Get == AccessorType.Public) return true;

            //the permission that we need
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);

            //privates can be accessed only if we have permission
            //first we start with the permission and check if the item is a child
            //then the parent of the permission etc.
            Symbol x = Permission;
            while(x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanGet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent); Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanGet(Symbol s)
        {
            if (s is VariableSymbol var) return CanGet(var);
            if (s is PropertySymbol prop) return CanGet(prop);
            return false;
        }
        bool CanSet(VariableSymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);
            Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);
            Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(Symbol s)
        {
            if (s is VariableSymbol var) return CanSet(var);
            if (s is PropertySymbol prop) return CanSet(prop);
            return false;
        }

        //matches an identifier to the corresponding Symbol, _Type, or Value
        object MatchIdentifier(string name)
        {
            //first we check the locals
            for (int i = 0; i < Locals.Count; i++)
            {
                if (Locals[i].Name == name)
                {
                    return Locals[i];
                }
            }

            //local aliases
            for (int i = 0; i < LocalAliases.Count; i++)
            {
                if (LocalAliases[i].Item1 == name) return LocalAliases[i].Item2;
            }

            //then the symbol stack (see above)
            foreach (var sl in SymbolStack)
            {
                for (int i = 0; i < sl.Count; i++)
                {
                    if (sl[i].Name == name) return sl[i];
                }
            }

            //then the aliases of the libraries that are in use
            for (int i = 0; i < LibrariesInUse.Count; i++)
            {
                if (LibrariesInUse[i].Aliases.ContainsKey(name))
                {
                    object o = LibrariesInUse[i].Aliases[name];
                    if (o is string s) return GetSymbol(s);
                    return o;
                }
            }

            //if nothing, return null
            return null;
        }

        Symbol GetSymbol(string s)
        {
            var p = s.Split('.');
            var x = Tools.MatchSymbol(p, Package.Symbols);
            if (x == null) return Tools.MatchSymbol(p, Project.Current.Symbols);
            return x;
        }

        //will convert the value to the correctly typed value
        //type can be null, means that Value will be the values own type, or automatically converted
        //all values MUST be convertable to object
        Value ConvertValue(Value value, _Type type, bool dothrow = false, Place? place = null)
        {
            //the value is of correct type
            if (value.Type == type) return value;

            if (value.Type == ObjectType.Value)
            {
                //values of reftypes do not need to be converted
                if (value.Type.IsRefType) return value;

                //if not a reftype, we box
                else return new BoxedValue { Base = value };
            }

            //check the automatic convertions from the libraries in use
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

                //if no automatic convertion, just return the value
                return value;
            }

            //look if a convertion exists
            if (Project.Current.Convertions.ContainsKey((value.Type, type)))
            {
                return new ConvertValue { Base = value, Type = type };
            }

            if (dothrow)
            {
                Throw(Error.TypeConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
            }

            //return null if not convertable
            return NullValue.Value;
        }

        //This function returns the Symbol, Value, or _Type that the Node represents
        //typeContext specifies to what type values should be converted into
        object MatchNode(Node node, bool prioritizeSymbol, _Type typeContext = null, bool checkCanGet = true)
        {
            //!!! Note: new values should always be run through the ConvertValue function (with the typeContext param)

            //our current value
            Value value = null;

            //current type
            _Type type = null;

            //current symbol
            Symbol symbol = null;

            //the next childnode not processed
            SecondaryNode childNode = node.Child;

            //get the initial symbol/type/value
            switch (node.NodeType)
            {
                //values with global:: at the start will be symbols from the global scope
                case NodeType.Global:
                    {
                        node = (Node)node.Data;
                        if (node.NodeType == NodeType.Identifier)
                        {
                            string name = Tokens[node.Token].Match;
                            for (int i = 0; i < Package.Symbols.Count; i++)
                            {
                                if (Package.Symbols[i].Name == name)
                                {
                                    symbol = Package.Symbols[i];
                                    goto symbolfound;
                                }
                            }
                            symbol = GetSymbol(name);
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
                        var recordValue = new RecordValue();
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
                                recordValue.Values.Add((name, NodeToValue(nodes.Item2, null)));
                            }
                        }
                        break;
                    }
                case NodeType.RecordType:
                    {
                        var nodeList = (List<(Node, Node)>)node.Data;
                        var recordType = new RecordType();
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
                            if (!recordType.Fields.Add((name, NodeToType(nodeList[i].Item2))))
                            {
                                Throw(Error.IdentifierDefined1, ThrowType.Error, Tokens[nodeList[i].Item2.Token].Place, name);
                            }
                        }
                        break;
                    }
                case NodeType.Collection:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var collection = new CollectionValue();
                        if (nodeList.Count == 1)
                        {
                            var match = MatchNode(nodeList[0], false);
                            if (match is _Type t)
                            {
                                type = new CollectionType { Base = t };
                                break;
                            }
                            if (match is Value v)
                            {
                                collection.Values.Add(v);
                            }
                            else
                            {
                                collection.Values.Add(NullValue.Value);
                                Throw(Error.ValueExpected1, ThrowType.Error, Tokens[nodeList[0].Token].Place, NodeToString(nodeList[0]));
                            }
                        }
                        if (nodeList.Count == 0)
                        {
                            value = new CollectionValue { Type = new CollectionType { Base = ObjectType.Value } };
                            break;
                        }
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
                        var nodeList = (List<Node>)node.Data;
                        if (nodeList.Count < 2)
                        {
                            Throw(Error.TooFewTypesInMultiType0, ThrowType.Error, Tokens[node.Token].Place);
                            if (nodeList.Count == 1)
                            {
                                return NodeToType(nodeList[0]);
                            }
                            return null;
                        }
                        var multiType = new MultiType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            multiType.Types.Add(NodeToType(nodeList[i]));
                        }
                        type = multiType;
                        break;
                    }
            }

            //convert from symbol to value or type
            //ClassSymbols and StructSymbols can be converted to types
            //VariableSymbols and PropertySymbols can be converted to values
            if(symbol != null)
            {
                if (prioritizeSymbol && childNode == null) return symbol;

                {
                    //match nameless item such as nameless class inside element
                    var x = symbol.Children.Find(s => s.Name == string.Empty);
                    if (x != null) symbol = x;
                }

                if (symbol is ClassSymbol classSymbol)
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
                        if (nl.Count != classSymbol.GenericCount) Throw(Error.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, classSymbol.GenericCount.ToString());
                    }
                    else if (classSymbol.GenericCount != 0) return symbol;

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
                    if (checkCanGet && !CanGet(vs))
                    {
                        Throw(Error.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = ConvertValue(new SymbolValue { Base = vs.ToString(), Type = vs.Type }, typeContext);
                }
                else if (symbol is PropertySymbol ps)
                {
                    if (!CanGet(ps))
                    {
                        Throw(Error.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = ConvertValue(new SymbolValue { Base = ps.ToString(), Type = ps.Type }, typeContext);
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
                            var variableSymbol = (VariableSymbol)GetSymbol(nt.Base).Children.Find(x => x.Name == name);
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
        Value NodeToValue(Node n, _Type typeContext, bool checkCanGet = true)
        {
            object o = MatchNode(n, false, typeContext, checkCanGet);
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
        Instruction StatementToInstruction(Statement statement)
        {
            switch (statement.StatementType)
            {
                case StatementType.Node:
                    {
                        Node n = (Node)statement.Data[0];
                        switch (n.NodeType)
                        {
                            case NodeType.Assignment:
                                {
                                    var nodes = ((Node, Node))n.Data;
                                    var left = NodeToValue(nodes.Item1, null, false);
                                    var right = NodeToValue(nodes.Item2, null);
                                    if (left is SymbolValue sv)
                                    {
                                        if (!CanGet(GetSymbol(sv.Base))) Throw(Error.ValueNotSettable1, ThrowType.Error, Tokens[nodes.Item1.Token].Place);
                                    }
                                    if (right.Type == null)
                                    {
                                        Throw(Error.CannotAssignTyplessValue0, ThrowType.Error, Tokens[nodes.Item2.Token].Place);
                                    }
                                    if (left is LocalValue lv)
                                    {
                                        return new Instruction { Type = InstructionType.Assign, Data = (left, right) };
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case StatementType.Scope:
                    {
                        LastWasIf = false;
                        var inst = new Instruction { Type = InstructionType.Scope, Data = new List<Instruction>() };
                        var ilist = new List<Instruction>();
                        var stlist = (List<Statement>)statement.Data[0];
                        for (int i = 0; i < stlist.Count; i++)
                        {
                            ilist.Add(StatementToInstruction(stlist[i]));
                        }
                        return inst;
                    }
                case StatementType.If:
                    {
                        LastWasIf = true;
                        var cond = NodeToValue((Node)statement.Data[0], LiteralValue.ConditionType);
                        var inst = StatementToInstruction((Statement)statement.Data[1]);
                        return new Instruction { Type = InstructionType.If, Data = (cond, inst) };
                    }
                case StatementType.Else:
                    {
                        if (!LastWasIf) Throw(Error.ElseNotAfterIf0, ThrowType.Error, Tokens[statement.Token].Place);
                        LastWasIf = false;
                        return new Instruction { Type = InstructionType.Else, Data = StatementToInstruction((Statement)statement.Data[0]) };
                    }
            }
            return new Instruction();
        }

        //used for value type cycle checking
        //if a variable with a value type (not a reference or a "pointer")
        //is the same type as the type being defined, it will cause the type to have undefined size
        bool CausesCycle(_Type thisType, _Type targetType)
        {
            //reference types do not cause cycles
            if (targetType.IsRefType) return false;

            if (thisType is NormalType nt)
            {
                var children = ((StructSymbol)GetSymbol(nt.Base)).Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is VariableSymbol variable)
                    {
                        if (!variable.Type.IsRefType)
                        {
                            if (variable.Type.Equals(thisType) || CausesCycle(thisType, variable.Type)) return false;
                        }
                    }
                }
            }
            else if (thisType is RecordType rt)
            {
                var fields = rt.Fields;
                for (int i = 0; i < fields.Count; i++)
                {
                    foreach (var f in fields)
                    {
                        if (!f.Item2.IsRefType)
                        {
                            if (f.Item2.Equals(thisType) || CausesCycle(thisType, f.Item2)) return false;
                        }
                    }
                }
            }

            return false;
        }

        List<PendingVariable> PendingVariables = new List<PendingVariable>();
        List<PendingProperty> PendingProperties = new List<PendingProperty>();
        struct PendingVariable
        {
            public Node TypeNode, ValueNode;
            public VariableSymbol Symbol;
        }
        struct PendingProperty
        {
            public bool IsSetter;
            public Node TypeNode;
            public Statement Function;
            public PropertySymbol Symbol;
        }
        void ProcessPendingVariable(PendingVariable x)
        {
            x.Symbol.Type = NodeToType(x.TypeNode);
            x.Symbol.DefaultValue = NodeToValue(x.ValueNode, x.Symbol.Type);
        }
        void ProcessPendingProperty(PendingProperty x)
        {
            var type = NodeToType(x.TypeNode);
            if (x.Symbol.Type == null) x.Symbol.Type = type;

            //throw if types dont match (getter and setter different types)
            else if (!x.Symbol.Type.Equals(type))
            {
                Throw(Error.PropertyTypeMismatch0, ThrowType.Error, Tokens[x.TypeNode.Token].Place);
            }
        }

        //structural analyzing
        //source file structure like what statements in global context, what inside elements, what inside classes etc.
        //Symbol tree construction to Package.Symbols
        void AnalyzeGlobalStatement(Statement statement)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.Main:
                    {
                        Statement st = (Statement)statement.Data[0];
                        if(st.StatementType == StatementType.Break || st.StatementType == StatementType.Continue)
                        {
                            Throw(Error.ControlOutsideScope0, ThrowType.Error, Tokens[st.Token].Place);
                            break;
                        }
                        var p = StatementToProcedure(st);
                        if (Project.Current.EntryPoint != null) Throw(Error.MultipleMain0, ThrowType.Error, Tokens[statement.Token].Place);
                        Package.EntryPoint = p;
                        break;
                    }
                case StatementType.Element:
                    {
                        string name = (string)statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name };
                        if (MatchIdentifier(name) != null)
                        {
                            Throw(Error.IdentifierDefined1, ThrowType.Error, Tokens[statement.Token].Place, name);
                        }
                        else
                        {
                            Package.Symbols.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop(); break;
                    }
                case StatementType.Alias:
                    {
                        Node n = (Node)statement.Data[0];
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
                            if (name != null) Package.Aliases.Add(name, value);
                        }
                        break;
                    }
            }
        }
        void AnalyzeElementStatement(Statement statement, ElementSymbol element)
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
                case StatementType.Element:
                    {
                        string name = (string)statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name, Parent = element?.ToString() };
                        if (element.Children.Find(x => x.Name == name) != null)
                        {
                            Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop(); break;
                    }
                case StatementType.Class:
                    {
                        string name = (string)statement.Data[0];
                        ClassSymbol symbol = new ClassSymbol { Name = name, Parent = element.ToString() };
                        if(element.Children.Find(x => x.Name == name) != null)
                        {
                            Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeClassStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop();
                        Permission = element;
                        break;
                    }
                case StatementType.Struct:
                    {
                        string name = (string)statement.Data[0];
                        StructSymbol symbol = new StructSymbol { Name = name, Parent = element.ToString() };
                        if (element.Children.Find(x => x.Name == name) != null)
                        {
                            Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeStructStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop();
                        Permission = element;
                        break;
                    }
            }
        }

        //variable and property analyzers are the same in element, class, struct so they are functions
        //the parentType variable 
        void AnalyzeVariable(Statement statement, Symbol parent, _Type parentType = null)
        {
            var getType = (AccessorType)statement.Data[0];
            var setType = (AccessorType)statement.Data[1];
            var typeNode = (Node)statement.Data[2];
            var valueNode = (Node)statement.Data[3];

            string name = null;

            if (valueNode.NodeType == NodeType.Assignment)
            {
                var nodes = ((Node, Node))valueNode.Data;
                if (nodes.Item1.NodeType == NodeType.Identifier)
                {
                    if (nodes.Item1.Child != null)
                    {
                        Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[nodes.Item1.Child.Token].Place, Tokens[nodes.Item1.Child.Token].Match);
                    }
                    name = Tokens[nodes.Item1.Token].Match;
                    if (parent.Children.Find(x => x.Name == name) != null)
                    {
                        Throw(Error.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, parent.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                }
            }
            else
            {
                if (valueNode.NodeType == NodeType.Identifier)
                {
                    if (valueNode.Child != null)
                    {
                        Throw(Error.TokenIllegal1, ThrowType.Error, Tokens[valueNode.Child.Token].Place, Tokens[valueNode.Child.Token].Match);
                    }
                    name = Tokens[valueNode.Token].Match;
                    if (parent.Children.Find(x => x.Name == name) != null)
                    {
                        Throw(Error.MemberDefined2, ThrowType.Error, Tokens[valueNode.Token].Place, name, parent.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Error.IdentifierExpected1, ThrowType.Error, Tokens[valueNode.Token].Place, Tokens[valueNode.Token].Match);
                }
            }

            var s = new VariableSymbol { Name = name, Parent = parent.ToString(), Get = getType, Set = setType };
            PendingVariables.Add(new PendingVariable { Symbol = s, TypeNode = typeNode, ValueNode = valueNode });

            if (name != null)
            {
                parent.Children.Add(s);
            }
        }
        void AnalyzeProperty(Statement statement, Symbol parent)
        {
            var GetType = (AccessorType)statement.Data[0];
            var SetType = (AccessorType)statement.Data[1];
            var typeNode = (Node)statement.Data[2];
            var name = (string)statement.Data[3];
            var functionStatement = new Statement { StatementType = StatementType.Scope, Data = new object[] { (List<Statement>)statement.Data[4] } };

            var prop = new PropertySymbol { Get = GetType, Set = SetType, Name = name, Parent = parent.ToString() };
            if (GetType == AccessorType.None)
            {
                prop.Setter = StatementToProcedure(functionStatement);
            }
            else
            {
                prop.Getter = StatementToProcedure(functionStatement);
            }

            var pendingProp = new PendingProperty { IsSetter = GetType == AccessorType.None, Function = functionStatement };
            var existingSymbol = parent.Children.Find((Symbol x) => x.Name == name);
            if (existingSymbol == null)
            {
                parent.Children.Add(prop);
                pendingProp.Symbol = prop;
            }
            else if (existingSymbol is PropertySymbol existingProp)
            {
                pendingProp.Symbol = existingProp;
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
                Throw(Error.MemberDefined2, ThrowType.Error, Tokens[statement.Token].Place, name, existingSymbol.ToString());
            }

            PendingProperties.Add(pendingProp);
        }
        void AnalyzeClassStatement(Statement statement, ClassSymbol _class)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;
                case StatementType.VariableDeclaration:
                    AnalyzeVariable(statement, _class);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _class);
                    break;
            }
        }
        void AnalyzeStructStatement(Statement statement, StructSymbol _struct)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Error.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;
                case StatementType.VariableDeclaration:
                    AnalyzeVariable(statement, _struct);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _struct);
                    break;

            }
        }
    }
}
