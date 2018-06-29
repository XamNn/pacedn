using Pace.CommonLibrary;
using SSSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)
using _Type = Pace.CommonLibrary.Type;

//pacednc compiles source files to Package objects (implemented in pacednl)

//todo:
//- make throw calls use DefaultMatch instead of string literals

namespace Pace.Compiler
{
    public static class Info
    {
        public static readonly string Version = "pacednc experimental 0.4.0 targetting pacednl A-1";
    }
    static class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 0)
            {
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    var line = Console.ReadLine();
                    if (line == "\u0004")
                    {
                        sb.Append('\n');
                        break;
                    }
                    else
                    {
                        sb.Append(line);
                        sb.Append('\n');
                    }
                }
                new Compiler()
                    .Compile(new[] { (sb.ToString(), "<stdin>") }, "stdin", true)
                    ?.Save();
            }
            else if (!System.IO.File.Exists(args[0])) Console.WriteLine("File not found");
            else
            {
                new Compiler()
                    .Compile(new[] { (System.IO.File.ReadAllText(args[0]), args[0]) }, System.IO.Path.GetFileNameWithoutExtension(args[0]), args.Length != 1 && args[1] == "--debug")
                    ?.Save();
            }
        }
    }
    public class Compiler
    {   
        //change these if you want more operator characters or different precedences
        //changing wont break anything but might prevent from using operators defined in some other package
        static string LowOperatorChars = @"*/|&^";
        static string HighOperatorChars = @"-+!#¤%£$€´`~=@?<>\";

        static string IndexerGetterName = "Indexer_get";
        static string IndexerSetterName = "Indexer_set";

        static string ElementAlternateTypeName = "type";

        static string TypelessTypeName = "<typeless>";

        static (string, TokenType)[] TokenPatterns = new(string, TokenType)[]
        {
            //keywords
            ("type", TokenType.TypeWord),
            ("return", TokenType.ReturnWord),
            ("null", TokenType.NullWord),
            ("default", TokenType.DefaultWord),
            ("object", TokenType.ObjectWord),
            ("bool", TokenType.BoolWord),
            ("get", TokenType.GetWord),
            ("set", TokenType.SetWord),
            ("alias", TokenType.AliasWord),
            ("if", TokenType.IfWord),
            ("when", TokenType.WhenWord),
            ("else", TokenType.ElseWord),
            ("break", TokenType.BreakWord),
            ("continue", TokenType.ContinueWord),
            ("is", TokenType.IsWord),
            ("config", TokenType.ConfigWord),
            ("convertion", TokenType.ConvertionWord),
            ("operator", TokenType.OperatorWord),
            ("func", TokenType.FuncWord),
            ("public", TokenType.PublicWord),
            ("private", TokenType.PrivateWord),
            ("visible", TokenType.VisibleWord),
            ("this", TokenType.ThisWord),
            ("new", TokenType.NewWord),
            ("match", TokenType.MatchWord),
            ("true", TokenType.TrueWord),
            ("false", TokenType.FalseWord),
            ("not", TokenType.NotWord),
            ("and", TokenType.AndWord),
            ("or", TokenType.OrWord),
            ("throw", TokenType.ThrowWord),
            ("catch", TokenType.CatchWord),

            //braces
            ("(", TokenType.PrimaryOpen),
            (")", TokenType.PrimaryClose),
            ("[", TokenType.SecondaryOpen),
            ("]", TokenType.SecondaryClose),
            ("{", TokenType.TertiaryOpen),
            ("}", TokenType.TertiaryClose),

            //reserved operators
            (".", TokenType.Period),
            (";", TokenType.Semicolon),
            (",", TokenType.Comma),
            ("=", TokenType.Equals),
            (":", TokenType.Colon),
            ("<", TokenType.LeftAngleBracket),
            (">", TokenType.RightAngleBracket),
            ("=>", TokenType.Lambda),
            ("@", TokenType.At),
            ("?", TokenType.QuestionMark),
            ("::", TokenType.PackageSpecifier)
        };

        //non-reserved keywords
        static string
            PackageClauseSpecifier = "package",
            ImportClauseSpecifier = "depends",
            MainSpecifier = "main",
            ElementSpecifier = "element",
            ClassSpecifier = "class",
            StructSpecifier = "struct",
            ConfigSpecifier = "config",
            UseSpecifier = "use",
            ImplicitConvertionWord = "implicit",
            AutomaticConvertionWord = "automatic",
            ThenWord = "then",
            attributesStart = "<attributes>",
            attributesEnd = "</attributes>";

        //these should not be changed too much since other code depends on it
        //namely the Statement.Data array should stay the same
        //tokentypes can be changed though
        static StatementPattern[] StatementPatterns = new StatementPattern[]
        {

            //executable statements
            new StatementPattern("=b", StatementType.Scope, null, null, new object[] { ((string)null, EmptyPlace) }),
            new StatementPattern("t|!=i|=b", StatementType.Scope, new[]{ TokenType.At }, null, null),
            new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, new object[] { ((string)null, EmptyPlace),
            new StatementPattern("t|!=i|t|!e", StatementType.Break, new[]{ TokenType.At, TokenType.BreakWord }, null, null),}),
            new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, new object[] { ((string)null, EmptyPlace) }),
            new StatementPattern("t|!=i|t|!e", StatementType.Scope, new[]{ TokenType.At, TokenType.ContinueWord }, null, null),
            new StatementPattern("t|e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, new object[] { null }),
            new StatementPattern("t|=n|!e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null),
            new StatementPattern("t|t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, null),
            new StatementPattern("t|=n|!m|=s", StatementType.If, new[]{ TokenType.IfWord }, new[] { ThenWord }, null),
            new StatementPattern("t|=s", StatementType.Else, new[]{ TokenType.ElseWord }, null, null),

            //variable (field) declaration
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PrivateWord, TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, new object[] { AccessorType.Public, AccessorType.Private }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PrivateWord, TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.Public }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PublicWord, TokenType.SetWord }, null, new object[] { AccessorType.Private, AccessorType.Public }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PublicWord, TokenType.GetWord }, null, new object[] { AccessorType.Public, AccessorType.Private }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PublicWord, TokenType.SetWord }, null, new object[] { AccessorType.Public, AccessorType.Public }),
            new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PublicWord, TokenType.GetWord }, null, new object[] { AccessorType.Public, AccessorType.Public }),
            new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.SetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.SetWord }, null, new object[] { AccessorType.Public, AccessorType.Public }),
            new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.GetWord }, null, new object[] { AccessorType.Public, AccessorType.Public }),
            new StatementPattern("t|t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.Lambda }, null, new object[] { AccessorType.Private, AccessorType.None }),
            new StatementPattern("t|t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.Lambda }, null, new object[] { AccessorType.None, AccessorType.Private }),
            new StatementPattern("t|t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.Lambda }, null, new object[] { AccessorType.Public, AccessorType.None }),
            new StatementPattern("t|t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.Lambda }, null, new object[] { AccessorType.None, AccessorType.None }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.None }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, new object[] { AccessorType.None, AccessorType.Private }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, new object[] { AccessorType.Public, AccessorType.None }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, new object[] { AccessorType.None, AccessorType.None }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord, TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord, TokenType.SetWord }, null, new object[] { AccessorType.Private, AccessorType.Private }),
            new StatementPattern("t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.SetWord, TokenType.Lambda }, null, new object[] { AccessorType.None, AccessorType.Public }),
            new StatementPattern("t|=n|=i|t|=n|!e", StatementType.PropertyDeclaration, new[] { TokenType.GetWord, TokenType.Lambda }, null, new object[] { AccessorType.Private, AccessorType.None }),
            new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord }, null, new object[] { AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate }),
            new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord }, null, new object[] { AccessorType.ImpliedPublic, AccessorType.ImpliedPrivate }),
            new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord }, null, new object[] { AccessorType.Private, AccessorType.None }),
            new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord }, null, new object[] { AccessorType.None, AccessorType.Public }),
            new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.VisibleWord }, null, new object[] { AccessorType.Public, AccessorType.Private }),

            new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, null),

            //keyword blocks
            new StatementPattern("m|=i|=b", StatementType.Element, null, new string[] { ElementSpecifier }, null),
            new StatementPattern("m|=b", StatementType.Class, null, new string[] { ClassSpecifier }, new object[] { (string.Empty, EmptyPlace) }),
            new StatementPattern("m|=i|=b", StatementType.Class, null, new string[] { ClassSpecifier }, null),
            new StatementPattern("m|t|=l|!=b", StatementType.Class, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { ClassSpecifier }, new object[] { (string.Empty, EmptyPlace) }),
            new StatementPattern("m|=i|!t|=l|!=b", StatementType.Class, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { ClassSpecifier }, null),
            new StatementPattern("m|=b", StatementType.Struct, null, new string[] { StructSpecifier }, new object[] { (string.Empty, EmptyPlace) }),
            new StatementPattern("m|=i|=b", StatementType.Struct, null, new string[] { StructSpecifier }, null),
            new StatementPattern("m|t|=l|!=b", StatementType.Struct, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { StructSpecifier }, new object[] { (string.Empty, EmptyPlace) }),
            new StatementPattern("m|=i|!t|=l|!=b", StatementType.Struct, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { StructSpecifier }, null),
            new StatementPattern("m|=i|!=b", StatementType.Config, null, new string[] { ConfigSpecifier }, null),
            new StatementPattern("m|t|=n|!e", StatementType.Main, new[] { TokenType.Lambda }, new[] { MainSpecifier }, null),

            //config statements
            new StatementPattern("t|=n|!e", StatementType.OperatorDeclaration, new TokenType[] { TokenType.OperatorWord }, null, new object[] { null }),
            new StatementPattern("m|t|=p|!e", StatementType.ConvertionModeDefinition, new[] { TokenType.ConvertionWord, TokenType.Colon }, new string[] { ImplicitConvertionWord }, new object[] { ConvertionType.Implicit }),
            new StatementPattern("m|t|=p|!e", StatementType.ConvertionModeDefinition, new[] { TokenType.ConvertionWord, TokenType.Colon }, new string[] { AutomaticConvertionWord }, new object[] { ConvertionType.Automatic }),
            new StatementPattern("m|=i|t|=i|!e", StatementType.Use, new[] { TokenType.PackageSpecifier }, new[] { UseSpecifier }, null),
            new StatementPattern("m|=i|!e", StatementType.Use, null, new[] { UseSpecifier }, new object[] { ((string)null, EmptyPlace) }),

            //convertions
            new StatementPattern("t|=p|!t|=n|!e", StatementType.ConvertionDeclaration, new TokenType[] { TokenType.ConvertionWord, TokenType.Equals }, null, null),

            //package stuff
            new StatementPattern("t|m|=i|!t", StatementType.Package, new[] { TokenType.SecondaryOpen, TokenType.SecondaryClose }, new[] { PackageClauseSpecifier }, null),
            new StatementPattern("t|m|=i|!t", StatementType.Import, new[] { TokenType.SecondaryOpen, TokenType.SecondaryClose }, new[] { ImportClauseSpecifier }, null),

            //throws
            new StatementPattern("t|!=i|!=q|!e", StatementType.Throw, new TokenType[] { TokenType.ThrowWord }, null, null),
            new StatementPattern("t|=l|!t|=s", StatementType.Catch, new TokenType[] { TokenType.CatchWord }, new[] { ThenWord }, null),
            new StatementPattern("t|!t|!=l|!t|=s", StatementType.Catch, new TokenType[] { TokenType.CatchWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, null),

            //attributes
            new StatementPattern("t|=s", StatementType.Attributes, new[] { TokenType.Attributes }, null, null)
        };

        static Place EmptyPlace = new Place();
        static readonly List<(Node, Node)> EmptyParameterNodesList = new List<(Node, Node)>(0);
        static readonly List<Node> EmptyNodeList = new List<Node>(0);
        static readonly Dictionary<string, string> EmptyAttributes = new Dictionary<string, string>(0);
        static readonly List<string> EmptyStringList = new List<string>(0);

        Token[] Tokens;
        Package Package;
        Dictionary<string, string[]> Sources = new Dictionary<string, string[]>();
        Config LocalConfig = new Config();
        Node Main;
        List<Config> UsedConfigs = new List<Config>();

        bool DebugMode;

        void UseConfig(Config config)
        {
            if (UsedConfigs.Contains(config)) return;
            UsedConfigs.Add(config);
            for (int i = 0; i < config.Configs.Count; i++)
            {
                Config useConfig = null;
                UseConfig(useConfig);
            }
        }

        public Package Compile((string text, string filename)[] Source, string Packagename, bool _dbug)
        {
            try
            {
                DebugMode = _dbug;

                //Tokenize, see Tokenize function below
                {
                    var tokenlist = new List<Token>();
                    for (int i = 0; i < Source.Length; i++)
                    {
                        tokenlist.AddRange(Tokenize(Source[i].text, Source[i].filename));
                    }
                    if (tokenlist.Count == 0) return null;
                    Tokens = tokenlist.ToArray();
                }

                //!!! optional !!! print tokens to console

                //for (int i = 0; i < Tokens.Length; i++)
                //{
                //    Console.WriteLine($"{$"{Tokens[i].Place.Line + 1} : {Tokens[i].Place.Index + 1}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
                //}

                //!!! end optional !!!

                //Parse, see StatementPattern struct and NextStatement function
                //match all tokens into statements
                List<Statement> Statements = new List<Statement>();
                {
                    int i = 0;
                    while (true)
                    {
                        Statements.Add(NextStatement(ref i, false));
                        if (i == Tokens.Length) break;
                    }
                }

                if (Statements.Count == 0) return null;

                //process statements
                int StatementIndex = 0;
                if (Statements[StatementIndex].StatementType == StatementType.Package)
                {
                    StatementIndex++;
                    var name = ((string, Place))Statements[0].Data[0];
                    if (!Settings.PackageNameValid(name.Item1))
                        Throw(Text.PackageNameInvalid1, ThrowType.Error, name.Item2, name.Item1);
                    else Packagename = name.Item1;
                }

                //at this point we know the name of the package we are compiling

                //try to unimport if package imported, otherwise fatal error
                if (Project.Current.Packages.TryGetValue(Packagename, out Package))
                {
                    string error = Project.Current.Unimport(Package);
                    if (error != null) Throw(Text.PackageAlreadyImported2, ThrowType.Fatal, null, Packagename, error);
                }

                //create package
                Package = new Package { Name = Packagename };
                Project.Current.Packages.Add(Packagename, Package);
                UsedConfigs.Add(LocalConfig);

                //imports come after and before anything else
                for (; StatementIndex < Statements.Count; StatementIndex++)
                {
                    if (Statements[StatementIndex].StatementType == StatementType.Import)
                    {
                        var name = ((string, Place))Statements[StatementIndex].Data[0];
                        var error = Package.Load(name.Item1, out var package);
                        if (error != null) Throw(Text.OperationResultError1, ThrowType.Error, name.Item2, error);
                        else Package.Dependencies.Add(package);
                    }
                    else break;
                }

                //analyze statements
                //elements, configs, etc
                for (; StatementIndex < Statements.Count; StatementIndex++)
                {
                    AnalyzeGlobalStatement(Statements[StatementIndex], null);
                }

                //process pending stuff
                //things like variables, properties, convertions, and configs "pend" until all types are defined
                //since they might use types defined later in the file
                //variables and properties pend once again because the values might need to know the types of some vars/props
                for (int i = 0; i < PendingSymbols.Count; i++)
                {
                    if (PendingSymbols[i].Item1 != null)
                    {
                        GenericsPush();
                        for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                        {
                            GenericTypes.Add(PendingSymbols[i].Item1[i2]);
                        }
                    }
                    CurrentSymbol = PendingSymbols[i].Item4;
                    for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage1(PendingSymbols[i].Item2[i2]);
                    for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage1(PendingSymbols[i].Item3[i2]);
                    CurrentSymbol = null;
                    if (PendingSymbols[i].Item1 != null) GenericsPop();
                }
                for (int i = 0; i < PendingConvertions.Count; i++)
                {
                    ProcessPendingConvertion(PendingConvertions[i]);
                }
                for (int i = 0; i < PendingConfigs.Count; i++)
                {
                    for (int i2 = 0; i2 < PendingConfigs[i].Item1.Count; i2++)
                    {
                        AnalyzeConfigStatement(PendingConfigs[i].Item1[i2], PendingConfigs[i].Item2);
                    }
                }
                for (int i = 0; i < PendingSymbols.Count; i++)
                {
                    if (PendingSymbols[i].Item1 != null)
                    {
                        GenericsPush();
                        for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                        {
                            GenericTypes.Add(PendingSymbols[i].Item1[i2]);
                        }
                    }
                    CurrentSymbol = PendingSymbols[i].Item4;
                    CurrentThis = PendingSymbols[i].Item5;
                    for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage2(PendingSymbols[i].Item2[i2]);
                    for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage2(PendingSymbols[i].Item3[i2]);
                    CurrentSymbol = null;
                    CurrentThis = null;
                    if (PendingSymbols[i].Item1 != null) GenericsPop();
                }

                //main is processed last
                if (Main != null) Package.EntryPoint = NodeToValue(Main, null, strictProcedureType: true);

                //return if compilation is successful
                if (HasErrors)
                {
                    Project.Current.Packages.Remove(Packagename);
                    throw new FatalError();
                }
                return Package;
            }
            catch (FatalError)
            {
                return null;
            }
        }

        #region errors

        //Errors
        bool HasErrors = false;
        enum Text
        {
            Unknown0,
            CharacterIllegal1,
            TokenIllegal1,
            TokenNotANode1,
            StatementIllegal0,
            CannotUseSemicolonAsStatement0,
            TokenExpected2,
            IdentifierExpected1,
            StringExpected1,
            ValueExpected1,
            TypeExpected1,
            NodeNotAValueOrTypeOrSymbol1,
            IdentifierNotDefined1,
            MemberNotDefined2,
            IdentifierDefined1,
            MemberDefined2,
            PropertyGetterDefined1,
            PropertySetterDefined1,
            TooManyParameters0,
            MissingParameter1,
            MissingParameter2,
            DuplicateParameter1,
            IndexerIllegal0,
            GenericCountIllegal1,
            ParameterCountIllegal1,
            ValueNotInvokable1,
            TypeConvertionIllegal2,
            ExpectedTypedInsteadOfTypeless1,
            PackageNotImported1,
            ImplicitConvertionIllegal2,
            ValueNotGettable1,
            ValueNotSettable1,
            PropertyTypeMismatch0,
            CannotDeclareTypelessVariable0,
            CannotAssignTyplessValue0,
            ImplicitlyTypedFunctionIllegal0,
            ValueTypeCycle0,
            IdentifierAmbiguityV,
            FunctionCycle0,
            ValueOfNullableTypeExpected0,
            CannotAssignNullToNonNullable1,
            XmlParsingError1,
            OperatorValueIllegal0,
            AttributesNotValidForItem0,
            PackageNameInvalid1,
            MultipleMain0,
            OperationResultError1,
            AssignToItself0,
            ConditionAlwaysTrue1,
            ConditionAlwaysFalse1,
            SymbolDeprecated1,
            SymbolDeprecatedMessage2,
            PackageAlreadyImported2,
        }
        static string GetMessage(Text e, params string[] args)
        {
            switch (e)
            {
                case Text.Unknown0: return "Unknown error occured.";
                case Text.CharacterIllegal1: return $"The character '{args[0]}' does not match any token.";
                case Text.TokenIllegal1: return $"The token '{args[0]}' is not legal in this context.";
                case Text.TokenNotANode1: return $"Expression expected. Instead got '{args[0]}'.";
                case Text.CannotUseSemicolonAsStatement0: return "';' may only appear when terminating a statement.";
                case Text.StatementIllegal0: return "This statement is illegal in this context.";
                case Text.TokenExpected2: return $"Expected '{args[0]}'. Instead got '{args[1]}'.";
                case Text.IdentifierExpected1: return $"Expected identifier. Instead got '{args[0]}'.";
                case Text.StringExpected1: return $"String expected. Instead got '{args[0]}'.";
                case Text.ValueExpected1: return $"'{args[0]}' is not a value.";
                case Text.TypeExpected1: return $"'{args[0]}' is not a type.";
                case Text.NodeNotAValueOrTypeOrSymbol1: return $"'{args[0]}' is not a value, type, or symbol.";
                case Text.IdentifierNotDefined1: return $"'{args[0]}' is not defined.";
                case Text.MemberNotDefined2: return $"'{args[0]}' is not defined in '{args[1]}'.";
                case Text.IdentifierDefined1: return $"'{args[0]}' is already defined.";
                case Text.MemberDefined2: return $"'{args[0]}' is already defined in '{args[1]}'.";
                case Text.PropertyGetterDefined1: return $"Getter already defined for the property '{args[0]}'.";
                case Text.PropertySetterDefined1: return $"Setter already defined for the property '{args[0]}'.";
                case Text.TooManyParameters0: return "Too many parameters in function call.";
                case Text.MissingParameter1: return $"The required parameter at position {args[0]} is missing a value.";
                case Text.MissingParameter2: return $"The required parameter '{args[0]}' at position {args[1]} is missing a value.";
                case Text.DuplicateParameter1: return $"The parameter '{args[0]}' is already specified.";
                case Text.IndexerIllegal0: return "Illegal indexer.";
                case Text.GenericCountIllegal1: return $"Wrong amount of generics, {args[0]} expected.";
                case Text.ParameterCountIllegal1: return $"Wrong amount of parameters, {args[0]} expected.";
                case Text.ValueNotInvokable1: return $"'{args[0]}' is not invokable.";
                case Text.TypeConvertionIllegal2: return $"Values of type '{args[0]}' cannot be converted to the type '{args[1]}'.";
                case Text.ExpectedTypedInsteadOfTypeless1: return $"Expected a value of type '{args[0]}'. instead got a typeless value.";
                case Text.PackageNotImported1: return $"Package '{args[0]}' not imported.";
                case Text.ImplicitConvertionIllegal2: return $"Values of type '{args[0]}' cannot be implicitly converted to the type '{args[1]}'. Try to convert explicitly.";
                case Text.ValueNotGettable1: return $"Cannot get the value of '{args[0]}' in this context. You might lack permission.";
                case Text.ValueNotSettable1: return $"Cannot set the value of '{args[0]}' in this context. You might lack permission, or the value might be constant.";
                case Text.PropertyTypeMismatch0: return $"The getter and setter of a property much be the of the same type.";
                case Text.CannotDeclareTypelessVariable0: return $"Variables must have an explicitly specified type.";
                case Text.CannotAssignTyplessValue0: return $"Cannot assign a typeless value to a variable. Did you forget to return a value?";
                case Text.ImplicitlyTypedFunctionIllegal0: return "Implicitly typed function illegal in this context.";
                case Text.ValueTypeCycle0: return "A variable of this type in this context causes a cycle.";
                case Text.IdentifierAmbiguityV:
                    {
                        var sb = new StringBuilder($"'{args[0]}' is ambiguous with '{args[1]}'");
                        for (int i = 2; i < args.Length; i++)
                        {
                            sb.Append(", '");
                            sb.Append(args[i]);
                            sb.Append("'");
                        }
                        sb.Append(".");
                        return sb.ToString();
                    }
                case Text.FunctionCycle0: return "Function cannot return itself.";
                case Text.ValueOfNullableTypeExpected0: return "Expected a value of a nullable type.";
                case Text.CannotAssignNullToNonNullable1: return $"Null is not valid for the non-nullable type '{args[0]}'.";
                case Text.XmlParsingError1: return $"Xml parsing error '{args[0]}'.";
                case Text.OperatorValueIllegal0: return "This value is not compatible with the operator.";
                case Text.AttributesNotValidForItem0: return "Attributes are not valid for this item.";
                case Text.PackageNameInvalid1: return $"'{args[0]}' is not a valid name for a package.";
                case Text.MultipleMain0: return "Entry point already defined.";
                case Text.OperationResultError1: return $"Operation result error: {args[0]}.";
                case Text.AssignToItself0: return "Assignment to itself.";
                case Text.ConditionAlwaysTrue1: return $"The condition '{args[0]}' always evaluates to true.";
                case Text.ConditionAlwaysFalse1: return $"The condition '{args[0]}' always evaluates to false.";
                case Text.SymbolDeprecated1: return $"'{args[0]}' is deprecated.";
                case Text.SymbolDeprecatedMessage2: return $"'{args[0]}' is deprecated '{args[1]}'.";
                case Text.PackageAlreadyImported2: return $"Package '{args[0]}' cannot be unimported for recompilation '{args[1]}'.";
            }
            return "!!! Error message not defined !!!";
        }
        class FatalError : Exception { }
        struct Place
        {
            public string File;
            public ushort Line;
            public ushort Index;
        }
        bool PlaceBefore(Place x, Place y)
        {
            return x.Line < y.Line || (x.Line == y.Line && x.Index < y.Index);
        }
        enum ThrowType
        {
            Fatal,
            Error,
            Warning,
            Message,
        }
        void Throw(Text e, ThrowType tt, Place? p, params string[] args)
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
                Console.WriteLine("At " + (p.Value.File) + " : " + (p.Value.Line + 1) + " : " + (p.Value.Index + 1));
            }
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(p == null ? "└ " : "├ ");
            Console.ForegroundColor = ConsoleColor.White;
            var message = GetMessage(e, args);
            Console.WriteLine(GetMessage(e, args));
            if (p != null)
            {
                var lines = Sources[p.Value.File];
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("| ");
                Console.ForegroundColor = ConsoleColor.White;

                string trimmedLine = p.Value.Line < lines.Length ? Sources[p.Value.File][p.Value.Line].TrimStart() : "{NONEXISTENT LINE}";
                Console.WriteLine(trimmedLine);

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("| ");
                Console.ForegroundColor = ConsoleColor.White;

                int arrowPos = p.Value.Line < lines.Length ? p.Value.Index - (lines[p.Value.Line].Length - trimmedLine.Length) : 0;
                for (int i = 0; i < arrowPos; i++)
                {
                    Console.Write(' ');
                }
                Console.WriteLine("↑");
            }
            Console.ForegroundColor = color;

            //breakpoint on the next line for debugging errors
            if (tt == ThrowType.Error)
                HasErrors = true;
            else if (tt == ThrowType.Fatal)
                throw new FatalError();
        }
        #endregion
        #region lexing

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
            LeftAngleBracket,
            RightAngleBracket,
            Lambda,
            At,
            QuestionMark,

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
            TypeWord,
            ReturnWord,
            DefaultWord,
            NullWord,
            ObjectWord,
            BoolWord,
            GetWord,
            SetWord,
            AliasWord,
            IfWord,
            WhenWord,
            ElseWord,
            BreakWord,
            ContinueWord,
            IsWord,
            ConfigWord,
            OperatorWord,
            ConvertionWord,
            FuncWord,
            PublicWord,
            PrivateWord,
            VisibleWord,
            ThisWord,
            NewWord,
            MatchWord,
            YieldWord,
            TrueWord,
            FalseWord,
            NotWord,
            AndWord,
            OrWord,
            ThrowWord,
            CatchWord,

            //Other
            PackageSpecifier, //global::
            Attributes, //<attributes> stuff </attributes>
        }
        string DefaultMatch(TokenType tokentype)
        {
            for (int i = 0; i < TokenPatterns.Length; i++)
            {
                if (TokenPatterns[i].Item2 == tokentype)
                    return TokenPatterns[i].Item1;
            }
            return null;
        }
        bool IsOperator(TokenType tokentype)
        {
            return
                tokentype == TokenType.LowOperator ||
                tokentype == TokenType.HighOperator ||
                tokentype == TokenType.NotWord ||
                tokentype == TokenType.AndWord ||
                tokentype == TokenType.OrWord;
        }
        struct Token
        {
            public TokenType TokenType;
            public string Match;
            public Place Place;

            public override string ToString()
            {
                return Match;
            }
        }

        bool SubstringEquals(string x, int xindex, string y)
        {
            if (x.Length - xindex < y.Length) return false;
            for (int i = 0; i < y.Length; i++)
            {
                if (y[i] != x[xindex + i]) return false;
            }
            return true;
        }
        bool SubstringEquals(StringBuilder x, int xindex, string y)
        {
            if (x.Length - xindex < y.Length) return false;
            for (int i = 0; i < y.Length; i++)
            {
                if (y[i] != x[xindex + i]) return false;
            }
            return true;
        }
        bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }
        bool IsWordStartChar(char c)
        {
            return char.IsLetter(c);
        }
        bool IsDecChar(char c)
        {
            return char.IsDigit(c);
        }
        bool IsHexChar(char c)
        {
            return char.IsDigit(c) || "abcdefABCDEF".Contains(c);
        }
        bool IsBinChar(char c)
        {
            return c == '0' || c == '1';
        }
        bool IsLowOperatorChar(char c)
        {
            return LowOperatorChars.Contains(c);
        }
        bool IsHighOperatorChar(char c)
        {
            return HighOperatorChars.Contains(c);
        }
        bool IsOperatorChar(char c)
        {
            return IsLowOperatorChar(c) || IsHighOperatorChar(c);
        }

        //This functions matches text with a regular expression
        //if a match is found, it generates a token with the according TokenType
        IEnumerable<Token> Tokenize(string text, string filename)
        {
            var lines = text.Split(Environment.NewLine);
            Sources.Add(filename, lines);
            bool inComment = false;
            StringBuilder currentString = null;
            Place currentStringPlace = EmptyPlace;
            StringBuilder currentAttributes = null;
            Place currentAttributesPlace = EmptyPlace;
            for (int line = 0; line < lines.Length; line++)
            {
                int index = 0;

                start:;
                int longestToken = 0;

                Place GetPlace()
                {
                    return new Place { Index = (ushort)(index), Line = (ushort)(line), File = filename };
                }
                if (index == lines[line].Length) continue;
                if (inComment)
                {
                    while (true)
                    {
                        if (index + 2 >= lines[line].Length)
                        {
                            goto nextline;
                        }
                        if (lines[line][index] == '-' && lines[line][index + 1] == '-' && lines[line][index + 2] == '>')
                        {
                            inComment = false;
                            index += 3;
                            goto start;
                        }
                        index++;
                    }
                }
                if (currentString != null)
                {
                    while (true)
                    {
                        if (lines[line][index] == '"')
                        {
                            yield return new Token { Match = currentString.ToString(), Place = currentStringPlace, TokenType = TokenType.String };
                            currentString = null;
                            index++;
                            goto start;
                        }
                        else
                        {
                            currentString.Append(lines[line][index]);
                            index++;
                            if (index == lines[line].Length) goto nextline;
                        }
                    }
                }
                if (currentAttributes != null)
                {
                    while (true)
                    {
                        if (SubstringEquals(lines[line], index, attributesEnd))
                        {
                            yield return new Token { Match = currentAttributes.ToString(), Place = currentAttributesPlace, TokenType = TokenType.Attributes };
                            currentAttributes = null;
                            index += attributesEnd.Length;
                            goto start;
                        }
                        else
                        {
                            //carriage returns fuck up stuff, best to get rid of them while we can
                            if (lines[line][index] != '\r')
                            {
                                currentAttributes.Append(lines[line][index]);
                            }
                            index++;
                            if (index == lines[line].Length) goto nextline;
                        }
                    }
                }
                if (char.IsWhiteSpace(lines[line][index]))
                {
                    index++;
                    goto start;
                }
                if (lines[line].Length + 1 != lines[line].Length && lines[line][index] == '/' && lines[line][index + 1] == '/') continue;
                if (index + 3 < lines[line].Length && lines[line][index] == '<' && lines[line][index + 1] == '!' && lines[line][index + 2] == '-' && lines[line][index + 3] == '-')
                {
                    inComment = true;
                    goto start;
                }
                if (lines[line][index] == '"')
                {
                    currentStringPlace = GetPlace();
                    index++;
                    currentString = new StringBuilder();
                    goto start;
                }
                if (SubstringEquals(lines[line], index, attributesStart))
                {
                    currentAttributesPlace = GetPlace();
                    index += attributesStart.Length;
                    currentAttributes = new StringBuilder();
                    goto start;
                }
                TokenType longestTokenType = TokenType.None;
                string longestMatch = null;
                for (int i = 0; i < TokenPatterns.Length; i++)
                {
                    if (SubstringEquals(lines[line], index, TokenPatterns[i].Item1))
                    {
                        if (longestToken < TokenPatterns[i].Item1.Length)
                        {
                            longestToken = TokenPatterns[i].Item1.Length;
                            longestTokenType = TokenPatterns[i].Item2;
                            longestMatch = TokenPatterns[i].Item1;
                        }
                    }
                }
                if (IsWordStartChar(lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (lines[line].Length != index && IsWordChar(lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.Word };
                        goto start;
                    }
                    index = starti;
                }
                else if (IsLowOperatorChar(lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (lines[line].Length != index && IsOperatorChar(lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.LowOperator };
                        goto start;
                    }
                    index = starti;
                }
                else if (IsHighOperatorChar(lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (lines[line].Length != index && IsOperatorChar(lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.HighOperator };
                        goto start;
                    }
                    index = starti;
                }
                else if (lines[line][index] == '\'')
                {
                    index++;
                    int starti = index;
                    while (lines[line].Length != index && lines[line][index] != '\'')
                    {
                        if (!IsWordChar(lines[line][index]))
                        {
                            Throw(Text.CharacterIllegal1, ThrowType.Error, GetPlace(), lines[line][index].ToString());
                        }
                        index++;
                    }
                    index++;
                    yield return new Token { TokenType = TokenType.LowOperator, Match = lines[line].Substring(starti, index - starti - 1), Place = new Place() { Index = (ushort)(starti), Line = (ushort)(line), File = filename } };
                    goto start;
                }

                if (longestToken == 0)
                {
                    if (IsDecChar(lines[line][index]))
                    {
                        if (lines[line][index] == '0' && index + 1 != lines[line].Length)
                        {
                            if (lines[line][index + 1] == 'x' || lines[line][index + 1] == 'X')
                            {
                                index += 2;
                                int starti = index;
                                while (lines[line].Length != index && IsHexChar(lines[line][index]))
                                {
                                    if (lines[line].Length == index) break;
                                    index++;
                                }
                                if (starti != index)
                                {
                                    yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.HexInteger };
                                    goto start;
                                }
                            }
                            else if (lines[line][index + 1] == 'b' || lines[line][index + 1] == 'B')
                            {
                                index += 2;
                                int starti = index;
                                while (lines[line].Length != index && IsBinChar(lines[line][index]))
                                {
                                    if (lines[line].Length == index) break;
                                    index++;
                                }
                                if (starti != index)
                                {
                                    yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line }, TokenType = TokenType.HexInteger };
                                    goto start;
                                }
                            }
                        }
                        {
                            int starti = index;
                            index++;
                            while (lines[line].Length != index && IsDecChar(lines[line][index]))
                            {
                                if (lines[line].Length == index) break;
                                index++;
                            }
                            if (index + 2 < lines[line].Length && lines[line][index] == '.' && IsDecChar(lines[line][index + 1]))
                            {
                                index += 2;
                                while (lines[line].Length != index && IsDecChar(lines[line][index]))
                                {
                                    index++;
                                }
                                yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.DecNonInteger };
                                goto start;
                            }
                            else
                            {
                                yield return new Token { Match = lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.DecInteger };
                                goto start;
                            }
                        }
                    }
                    Throw(Text.CharacterIllegal1, ThrowType.Error, GetPlace(), lines[line][index].ToString());
                    index++;
                }
                else
                {
                    yield return new Token { Place = GetPlace(), Match = longestMatch, TokenType = longestTokenType };
                    index += longestToken;
                }
                goto start;
                nextline:;
            }
            yield return new Token { Place = new Place { Line = (ushort)(lines.Length - 1), Index = lines.Length == 0 ? (ushort)0 : (ushort)lines[lines.Length - 1].Length, File = filename }, Match = "END OF FILE", TokenType = TokenType.EndOfFile };
        }

        #endregion
        #region parsing

        //Parsing

        //misc helper functions
        void RefNodeList(ref int i, ref List<Node> nl, TokenType end, bool allowEmpties)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (allowEmpties && Tokens[i].TokenType == TokenType.Comma)
                    {
                        nl.Add(null);
                        i++;
                        continue;
                    }
                    nl.Add(NextNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + DefaultMatch(end), Tokens[i].Match); break; }
                }
        }
        List<Node> NodeList(ref int i, TokenType end, bool allowEmpties)
        {
            var x = new List<Node>();
            RefNodeList(ref i, ref x, end, allowEmpties);
            return x;
        }
        void RefPrimaryNodeList(ref int i, ref List<Node> nl, TokenType end, bool allowEmpties)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (allowEmpties && Tokens[i].TokenType == TokenType.Comma)
                    {
                        nl.Add(null);
                        i++;
                        continue;
                    }
                    nl.Add(NextPrimaryNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + DefaultMatch(end), Tokens[i].Match); break; }
                }
        }
        List<Node> PrimaryNodeList(ref int i, TokenType end, bool allowEmpties)
        {
            var x = new List<Node>();
            RefPrimaryNodeList(ref i, ref x, end, allowEmpties);
            return x;
        }
        void RefDualNodeList(ref int i, ref List<(Node, Node)> nl, TokenType end)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    nl.Add((NextNode(ref i), NextNode(ref i)));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(end) + "' or ',", Tokens[i].Match); break; }
                }
        }
        List<(Node, Node)> DualNodeList(ref int i, TokenType end)
        {
            var x = new List<(Node, Node)>();
            RefDualNodeList(ref i, ref x, end);
            return x;
        }
        void NodeListOrDualNodeList(ref int i, out List<Node> SingleNodeList, out List<(Node, Node)> DualNodeList, TokenType end, bool allowSingleNodeEmpties)
        {
            SingleNodeList = null;
            DualNodeList = null;
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = EmptyNodeList;
                return;
            }
            if (allowSingleNodeEmpties && Tokens[i].TokenType == TokenType.Comma)
            {
                i++;
                SingleNodeList = new List<Node>();
                SingleNodeList.Add(null);
                RefNodeList(ref i, ref SingleNodeList, end, allowSingleNodeEmpties);
                return;
            }
            var firstNode = NextNode(ref i);
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node>(1);
                SingleNodeList.Add(firstNode);
                return;
            }
            if (Tokens[i].TokenType == TokenType.Comma)
            {
                i++;
                SingleNodeList = new List<Node>();
                SingleNodeList.Add(firstNode);
                RefNodeList(ref i, ref SingleNodeList, end, allowSingleNodeEmpties);
                return;
            }
            var secondNode = NextNode(ref i);
            DualNodeList = new List<(Node, Node)>();
            DualNodeList.Add((firstNode, secondNode));
            if (Tokens[i].TokenType == TokenType.Comma) i++;
            else if (Tokens[i].TokenType == end)
            {
                i++;
                return;
            }
            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(end) + "' or ',' expected", Tokens[i].Match);
            RefDualNodeList(ref i, ref DualNodeList, end);
            return;
        }
        void RefIdentifierList(ref int i, ref List<(string, Place)> il, TokenType end)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (Tokens[i].TokenType == TokenType.Word)
                    {
                        il.Add((Tokens[i].Match, Tokens[i].Place));
                        i++;
                    }
                    else Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(end) + "' or ',", Tokens[i].Match); break; }
                }
        }
        List<(string, Place)> IdentifierList(ref int i, TokenType end)
        {
            List<(string, Place)> x = new List<(string, Place)>();
            RefIdentifierList(ref i, ref x, end);
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
            return PrimaryNodeList(ref i, TokenType.RightAngleBracket, false);
        }

        //statement parsing
        enum StatementType : byte
        {
            None, //statement which is already thrown an error and will be accepted anywhere
            Node,
            DoubleNode,
            VariableDeclaration,
            PropertyDeclaration,
            Scope,
            No_op,
            Element,
            Class,
            Struct,
            Continue,
            Break,
            Return,
            Label,
            Alias,
            Main,
            If,
            Else,
            Package,
            Import,
            Use,
            ConvertionDeclaration,
            Config,
            OperatorDeclaration,
            ConvertionModeDefinition,
            Throw,
            Catch,
            Attributes,
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

            public object[] Data; //these objects will be added to the final statements data, before any data from pattern matching

            //The format string must constist of clauses separated by "|"
            //Clauses must be as follows: "!" (throw flag, throw a compiler error if not a match) "=" (save match to data) [any single character] (the clause
            //Examples of a clause: !t, =s, a
            //Example of a format string: !t|=s|a
            public StatementPattern(string format, StatementType statementType, TokenType[] tokenTypes, string[] matches, object[] data)
            {
                Clauses = format.Split('|');
                StatementType = statementType;
                TokenTypes = tokenTypes;
                Matches = matches;
                Data = data;
            }
        }

        //Statement parsing function
        Statement NextStatement(ref int i, bool throwifendoffile = true)
        {
            //the statement to return
            Statement s = new Statement { Token = i };

            if(Tokens[i].TokenType == TokenType.EndOfFile)
            {
                if (throwifendoffile)
                    Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                i++;
                return s;
            }

            //helper variables for throwing
            Text currentError = Text.Unknown0;
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
                                    currentError = Text.TokenExpected2;
                                    currentErrorArgs = new[] { DefaultMatch(Tokens[i].TokenType), Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            else if (save) data.Add(Tokens[i].Match);
                            i++;
                            break;

                        //match, matches a token with the match from the matches array
                        case 'm':
                            if (Tokens[i].Match != p.Matches[matchindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Text.TokenExpected2;
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
                                    currentError = Text.IdentifierExpected1;
                                    currentErrorArgs = new[] { Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            if (save) data.Add((Tokens[i].Match, Tokens[i].Place));
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.StringExpected1;
                                        currentErrorArgs = new[] { Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                if (save) data.Add(Tokens[i].Match);
                                i++;
                                break;
                            }

                        //identifier list, ends with the specified token
                        case 'l':
                            {
                                var l = IdentifierList(ref i, p.TokenTypes[tokentypeindex++]);
                                if (save) data.Add(l);
                                break;
                            }

                        //dual node list
                        case 'd':
                            {
                                var l = DualNodeList(ref i, p.TokenTypes[tokentypeindex++]);
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

                if (Tokens[i].TokenType == TokenType.Semicolon)
                {
                    //uncomment if semicolon should be no-op
                    
                    //i++;
                    //s.StatementType = StatementType.No_op;
                    //return s;

                    Throw(Text.CannotUseSemicolonAsStatement0, ThrowType.Error, Tokens[i].Place);

                    i++;
                    s.StatementType = StatementType.None;
                    return s;
                }

                int t = i;
                Node n = NextNode(ref i);
                if (t == i)
                {
                    i++;
                    return NextStatement(ref i, throwifendoffile);
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
                else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ";", Tokens[i].Match);
                s.StatementType = StatementType.DoubleNode;
                s.Data = new object[] { n, n2 };
                return s;
            }

            //throw with the helper variables
            throww:
            Throw(currentError, ThrowType.Error, Tokens[i].Place, currentErrorArgs);
            s.StatementType = StatementType.None;
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
            TypedFunction, //List<(Node, Node)>                    //ex: func(int x, int y) = x + y
            UntypedFunction, //List<Node>                          //ex: func(x, y) = x + y
            FunctionType, //List<Node>                             //ex: func(int, int) => 
            MatchOperator, //Node                                  //ex: match ==
            MatchNode, //Node                                      //ex: match MyFunction
            Record,                                                //ex: [x = 1, y = 2]
            RecordType,                                            //ex: [int x, int y]
            Collection, //List<Node>                               //ex: [1, 2, 3]
            FunctionDeclaration, //Node, (Statement or Node)       //ex: FunctionName(int x, int y) = x - y
            Init, //(Node, List<Node)                              //ex: init MyClass
            Convertion, //(Node, Node)                             //ex: x:MyType
            Is, //(Node, Node)                                     //ex: x is y
            IsNot, //(Node, Node)                                  //ex: x is not y
            Not, //Node                                            //ex: not x
            And, //(Node, Node)                                    //ex: x and y
            Or, //(Node, Node)                                     //ex: x or y
            Xor, //(Node, Node)                                    //ex: x xor y
            Object,                                                //ex: object
            Bool,                                                  //ex: bool
            True,                                                  //ex: true
            False,                                                 //ex: false
            This,                                                  //ex: this
            Null,                                                  //ex: null
            IsNull,                                                //ex: x is null
            Default,                                               //ex: default
            PackageSpecifier,                                      //ex: mypackage::Element.Variable
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
            sb.Append(Tokens[n.Token].Match);
            for (int i = n.Token + 1; i < n.EndToken; i++)
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
            Generics,                     //ex: <int, string>
            ParameterList,                //ex: (int x, int y)
            BoxedSpecifier,               //ex: ?
        }
        class SecondaryNode
        {
            public SecondaryNodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
        }

        SecondaryNode GetLastChild(SecondaryNode n)
        {
            if (n.Child == null) return n;
            return GetLastChild(n.Child);
        }
        SecondaryNode GetSecondLastChild(SecondaryNode n)
        {
            if (n.Child.Child == null) return n;
            return GetSecondLastChild(n.Child);
        }
        void RemoveLastChild(SecondaryNode n)
        {
            if (n.Child.Child == null) n.Child = null;
            else RemoveLastChild(n.Child);
        }
        void RemoveLastChild(Node n)
        {
            if (n.Child.Child == null) n.Child = null;
            else RemoveLastChild(n.Child);
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
        Node NextNode(ref int i, bool ignorePrimitiveOperators = false)
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
                        n1 = new Node { Data = (n1, Tokens[i++].Match, NextPrimaryNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //When operators have high priority, they take the previous PrimaryNode (n1) and the next Node
                    case TokenType.HighOperator:
                        n1 = new Node { Data = (n1, Tokens[i++].Match, NextNode(ref i, true)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //Assignment operators take the previous PrimaryNode and the next Node
                    case TokenType.Equals:
                        if (ignorePrimitiveOperators) goto default;
                        i++;
                        n1 = new Node { NodeType = NodeType.Assignment, Data = (n1, NextNode(ref i)), Token = n1.Token, EndToken = i };
                        break;

                    //function decl
                    case TokenType.Lambda:
                        if (n1.Child != null)
                        {
                            i++;
                            var lastchild = GetLastChild(n1.Child);
                            if (lastchild.NodeType == SecondaryNodeType.ParameterList || (lastchild.NodeType == SecondaryNodeType.Call && ((List<Node>)lastchild.Data).Count == 0))
                            {
                                List<Node> generics = null;
                                if (n1.Child.Child != null)
                                {
                                    var secondlastchild = GetSecondLastChild(n1.Child);
                                    if (secondlastchild.NodeType == SecondaryNodeType.Generics)
                                    {
                                        generics = (List<Node>)secondlastchild.Data;
                                        RemoveLastChild(n1.Child);
                                    }
                                }
                                RemoveLastChild(n1);
                                n1 = new Node { NodeType = NodeType.FunctionDeclaration, Data = (n1, generics, lastchild.NodeType == SecondaryNodeType.ParameterList ? (List<(Node, Node)>)lastchild.Data : EmptyParameterNodesList, NextNode(ref i)), Token = n1.Token, EndToken = i };
                                break;
                            }
                        }
                        goto default;

                    //these work like a binary operators
                    case TokenType.IsWord:
                        if (ignorePrimitiveOperators) goto default;
                        i++;
                        if (Tokens[i].TokenType == TokenType.NotWord)
                        {
                            i++;
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.IsNot, Token = n1.Token, EndToken = i };
                        }
                        else if (Tokens[i].TokenType == TokenType.NullWord)
                        {
                            i++;
                            n1 = new Node { Data = n1, NodeType = NodeType.IsNull, Token = n1.Token, EndToken = i };
                        }
                        else
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Is, Token = n1.Token, EndToken = i };
                        }
                        break;
                    case TokenType.AndWord:
                        if (ignorePrimitiveOperators) goto default;
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.And, Token = n1.Token, EndToken = i };
                        break;
                    case TokenType.OrWord:
                        if (ignorePrimitiveOperators) goto default;
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Or, Token = n1.Token, EndToken = i };
                        break;
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
                    Throw(Text.TokenNotANode1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    break;

                //Identifier
                case TokenType.Word:
                    i++;
                    if (Tokens[i].TokenType == TokenType.PackageSpecifier)
                    {
                        i++;
                        n.NodeType = NodeType.PackageSpecifier;
                        n.Data = NextPrimaryNode(ref i);
                    }
                    else
                    {
                        n.NodeType = NodeType.Identifier;
                    }
                    break;

                //Parantheses and parameter lists
                case TokenType.PrimaryOpen:
                    {
                        n.NodeType = NodeType.Parentheses;
                        i++;
                        n.Data = NextNode(ref i);
                        if (Tokens[i].TokenType == TokenType.PrimaryClose) i++;
                        else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(TokenType.PrimaryClose), Tokens[i].Match);
                        break;
                    }

                //not
                case TokenType.NotWord:
                    n.NodeType = NodeType.Not;
                    i++;
                    n.Data = NextNode(ref i);
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
                case TokenType.TrueWord:
                    n.NodeType = NodeType.True;
                    i++;
                    break;
                case TokenType.FalseWord:
                    n.NodeType = NodeType.False;
                    i++;
                    break;
                case TokenType.ThisWord:
                    n.NodeType = NodeType.This;
                    i++;
                    break;
                case TokenType.ObjectWord:
                    n.NodeType = NodeType.Object;
                    i++;
                    break;
                case TokenType.BoolWord:
                    n.NodeType = NodeType.Bool;
                    i++;
                    break;
                case TokenType.NullWord:
                    n.NodeType = NodeType.Null;
                    i++;
                    break;
                case TokenType.DefaultWord:
                    n.NodeType = NodeType.Default;
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                case TokenType.LeftAngleBracket:
                case TokenType.RightAngleBracket:
                    n.NodeType = NodeType.UnaryOperator;
                    n.Data = (Tokens[i++].Match, NextPrimaryNode(ref i));
                    break;

                //record
                case TokenType.SecondaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, false);
                        if (dual == null)
                        {
                            if (IsAssignmentOrFuncDecl(single[0]))
                            {
                                n.NodeType = NodeType.Record;
                                n.Data = single;
                            }
                            else
                            {
                                n.NodeType = NodeType.Collection;
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

                //init
                case TokenType.NewWord:
                    {
                        n.NodeType = NodeType.Init;
                        i++;
                        var node = NextPrimaryNode(ref i);
                        List<Node> nodes;
                        if (Tokens[i].TokenType == TokenType.TertiaryOpen)
                        {
                            i++;
                            nodes = NodeList(ref i, TokenType.TertiaryClose, false);
                        }
                        else
                        {
                            nodes = null;
                        }
                        n.Data = (node, nodes);
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
                //when (condition x) then (value if condition x is true)
                //when (condition y) then (value if other condition y is true)
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
                            if (Tokens[i].Match == ThenWord) i++;
                            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ThenWord, Tokens[i].Match);
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
                                Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(TokenType.WhenWord) + "' or '" + DefaultMatch(TokenType.ElseWord), Tokens[i].Match);
                                n.NodeType = NodeType.None;
                                goto quit;
                            }
                        }
                        n.Data = (conditions, elseValue);
                        quit:
                        break;
                    }

                //function type and value
                case TokenType.FuncWord:
                    {
                        i++;
                        var generics = GenericList(ref i);
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[i - 1].Place, Tokens[i - 1].Match);
                            return n;
                        }
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, false);
                        if (dual == null)
                        {
                            if (Tokens[i].Match == "->")
                            {
                                i++;
                                n.NodeType = NodeType.FunctionType;
                                n.Data = (generics, single, NextPrimaryNode(ref i));
                            }
                            else if (Tokens[i].TokenType == TokenType.Lambda)
                            {
                                i++;
                                n.NodeType = NodeType.UntypedFunction;
                                n.Data = (generics, single, NextNode(ref i));
                            }
                            else
                            {
                                n.NodeType = NodeType.FunctionType;
                                n.Data = (generics, single, (Node)null);
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.TypedFunction;
                            if (Tokens[i].TokenType == TokenType.Lambda) i++;
                            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, DefaultMatch(TokenType.Lambda), Tokens[i].Match);
                            n.Data = (generics, dual, NextNode(ref i));
                        }
                        break;
                    }

                // (=> x) is short syntax for (func() => x)
                case TokenType.Lambda:
                    {
                        n.NodeType = NodeType.TypedFunction;
                        i++;
                        n.Data = ((List<Node>)null, EmptyParameterNodesList, NextNode(ref i));
                        break;
                    }

                case TokenType.MatchWord:
                    {
                        i++;
                        if (IsOperator(Tokens[i].TokenType))
                        {
                            n.NodeType = NodeType.MatchOperator;
                        }
                        else
                        {
                            n.NodeType = NodeType.MatchNode;
                            n.Data = NextNode(ref i);
                        }
                        break;
                    }

            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            //convertion if there is
            if (Tokens[i].TokenType == TokenType.Colon)
            {
                i++;
                n = new Node { Data = (n, NextPrimaryNode(ref i)), NodeType = NodeType.Convertion, Token = n.Token, EndToken = i };
            }

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
                        if (Tokens[i].TokenType != TokenType.Word)
                        {
                            Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                        }
                        i++;
                        break;
                    }

                //Parentheses and TypedParameterList
                case TokenType.PrimaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, true);
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
                        var nodes = NodeList(ref i, TokenType.SecondaryClose, false);
                        n.NodeType = SecondaryNodeType.Indexer;
                        n.Data = nodes;
                        break;
                    }

                //nullable type
                case TokenType.QuestionMark:
                    {
                        n.NodeType = SecondaryNodeType.BoxedSpecifier;
                        i++;
                        break;
                    }
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            return true;
        }

        #endregion
        #region general semantics

        //semantics

        Symbol CurrentSymbol;
        ThisValue CurrentThis;

        List<LocalValue> Locals = new List<LocalValue>();
        Stack<int> LocalSeparators = new Stack<int>();

        void LocalsPush()
        {
            LocalSeparators.Push(Locals.Count);
        }
        void LocalsPop()
        {
            int i = LocalSeparators.Pop();
            Locals.RemoveRange(i, Locals.Count - i);
        }

        List<GenericType> GenericTypes = new List<GenericType>();
        Stack<int> GenericSeparators = new Stack<int>();

        //These functions "push" and "pop" (as in a stack) the generics
        void GenericsPush()
        {
            GenericSeparators.Push(GenericTypes.Count);
        }
        void GenericsPop()
        {
            int i = GenericSeparators.Pop();
            GenericTypes.RemoveRange(i, GenericTypes.Count - i);
        }

        //the generics which types are known
        Stack<List<(string, _Type)>> KnownGenericTypes = new Stack<List<(string, _Type)>>();

        bool CanGet(Value v)
        {
            if (v is SymbolValue symbolVal) return CanGet(symbolVal.Symbol);
            return true;
        }
        bool CanSet(Value v)
        {
            if (v is SymbolValue symbolVal) return CanSet(symbolVal.Symbol);
            if (v is LocalValue || v is MemberValue || v is InvalidValue) return true;
            return false;
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
            Symbol permissionNeeded = s.Parent;
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = permissionNeeded.Parent;

            //privates can be accessed only if we have permission
            //first we start with the permission and check if the item is a child
            //then the parent of the permission etc.
            if (CurrentSymbol != null)
            {
                Symbol x = CurrentSymbol;
                while (true)
                {
                    if (permissionNeeded == x) return true;
                    if (x.Parent == null) break;
                    x = x.Parent;
                }
            }
            return false;
        }
        bool CanGet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = s.Parent;
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = permissionNeeded.Parent;
            Symbol x = CurrentSymbol;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = x.Parent;
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
            Symbol permissionNeeded = s.Parent;
            if (!(permissionNeeded is ElementSymbol es))
            {
                if (permissionNeeded.Parent == null) return false;
                permissionNeeded = permissionNeeded.Parent;
            }
            Symbol x = CurrentSymbol;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = x.Parent;
            }
            return false;
        }
        bool CanSet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = s.Parent;
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = permissionNeeded.Parent;
            Symbol x = CurrentSymbol;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = x.Parent;
            }
            return false;
        }
        bool CanSet(Symbol s)
        {
            if (s is VariableSymbol var) return CanSet(var);
            if (s is PropertySymbol prop) return CanSet(prop);
            return false;
        }

        //used for value type cycle checking
        //if a variable with a value type (not a reference type like a class or function)
        //is the same type as the type being defined, it will cause the type to have undefined size
        bool CausesCycle(_Type thisType, _Type targetType)
        {
            //reference types do not cause cycles
            if (targetType.IsRefType) return false;

            if (thisType is NormalType nt)
            {
                var children = nt.Base.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is VariableSymbol variable)
                    {
                        if (!variable.Type.IsRefType)
                        {
                            if (variable.Type.Equals(thisType) || CausesCycle(thisType, variable.Type)) return true;
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
                            if (f.Item2.Equals(thisType) || CausesCycle(thisType, f.Item2)) return true;
                        }
                    }
                }
            }

            return false;
        }

        //pending stuff
        List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>, Symbol, ThisValue)> PendingSymbols = new List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>, Symbol, ThisValue)>();
        Stack<List<PendingVariable>> CurrentPendingVariables = new Stack<List<PendingVariable>>();
        Stack<List<PendingProperty>> CurrentPendingProperties = new Stack<List<PendingProperty>>();
        List<PendingConvertion> PendingConvertions = new List<PendingConvertion>();
        List<(List<Statement>, Config)> PendingConfigs = new List<(List<Statement>, Config)>();

        class PendingVariable
        {
            public Node TypeNode, ValueNode;
            public VariableSymbol Symbol;
            public bool IsFunction;
        }
        class PendingProperty
        {
            public bool IsSetter;
            public Node TypeNode;
            public Node ValueNode;
            public PropertySymbol Symbol;
        }
        struct PendingConvertion
        {
            public Node To, From, Value;
        }

        //Stage1 resolves the type
        void ProcessPendingVariableStage1(PendingVariable x)
        {
            if (x.IsFunction)
            {
                var functype = TypedFunctionToFunctionType(x.ValueNode);
                if(functype.Generics.Count != 0)
                {
                    GenericsPush();
                    for (int i = 0; i < functype.Generics.Count; i++)
                    {
                        GenericTypes.Add(new GenericType { Name = functype.Generics[i] });
                    }
                }
                functype.ReturnType = x.TypeNode ==null?null: NodeToType(x.TypeNode);
                if (functype.Generics.Count != 0) GenericsPop();
                x.Symbol.Type = functype;
            }
            else
            {
                if (x.TypeNode == null)
                {
                    Throw(Text.CannotDeclareTypelessVariable0, ThrowType.Error, Tokens[x.ValueNode.Token].Place);
                    x.Symbol.Type = InvalidType.Value;
                }
                else x.Symbol.Type = NodeToType(x.TypeNode);
            }
        }
        void ProcessPendingPropertyStage1(PendingProperty x)
        {
            var type = NodeToType(x.TypeNode);
            if(x.Symbol.Type == null) x.Symbol.Type = type;

            //throw if types dont match (getter and setter different types)
            else if (!x.Symbol.Type.Equals(type))
            {
                Throw(Text.PropertyTypeMismatch0, ThrowType.Error, Tokens[x.TypeNode.Token].Place);
            }
        }

        //Stage2 resolves the value
        void ProcessPendingVariableStage2(PendingVariable x)
        {
            if (x.ValueNode == null) x.Symbol.Value = x.Symbol.Type.GetDefaultValue();
            else x.Symbol.Value = NodeToValue(x.ValueNode, x.Symbol.Type);
        }
        void ProcessPendingPropertyStage2(PendingProperty x)
        {
            if (x.IsSetter)
            {
                x.Symbol.Setter = NodeToValue(x.ValueNode, null, strictProcedureType: true);
            }
            else
            {
                x.Symbol.Getter = NodeToValue(x.ValueNode, x.Symbol.Type);
            }
        }

        void ProcessPendingConvertion(PendingConvertion x)
        {
            var fromType = NodeToType(x.From, true);
            var toType = NodeToType(x.To, true);
            FunctionType funcType;
            if (fromType is NormalType a && toType is NormalType b && a.Generics.Count == 0)
            {
            }
            funcType = new FunctionType { Parameters = new List<(_Type, string, bool)>(1) { (fromType, null, false) }, ReturnType = toType, Generics = EmptyStringList };
            Package.Convertions.Add((NodeToType(x.From), toType, NodeToValue(x.Value, funcType)));
        }

        #endregion
        #region node semantics

        void ThrowIfNotIdentifier(Node n)
        {
            if (n.NodeType != NodeType.Identifier)
            {
                Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[n.Token].Place, Tokens[n.Token].Match);
            }
            if (n.Child != null)
            {
                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[n.Child.Token].Place, Tokens[n.Child.Token].Match);
            }
        }

        //matches an identifier to the corresponding Symbol, _Type, or Value
        object MatchIdentifier(string name, bool dothrow, Place? place, bool createLocals = false)
        {
            //generics
            for (int i = 0; i < GenericTypes.Count; i++)
            {
                if (GenericTypes[i].Name == name) return GenericTypes[i];
            }

            //locals
            for (int i = 0; i < Locals.Count; i++)
            {
                if (Locals[i].Name == name)
                {
                    return Locals[i];
                }
            }

            //first CurrentSymbols children, then its parents children etc
            if (CurrentSymbol != null)
            {
                Symbol symbol = CurrentSymbol;
                while (true)
                {
                    for (int i = 0; i < symbol.Children.Count; i++)
                    {
                        if (symbol.Children[i].Name == name) return symbol.Children[i];
                    }
                    if (symbol.Parent == null) break;
                    symbol = symbol.Parent;
                }
            }

            //aliases of the configs that are in use
            for (int i = 0; i < UsedConfigs.Count; i++)
            {
                if (UsedConfigs[i].Aliases.ContainsKey(name))
                {
                    return UsedConfigs[i].Aliases[name];
                }
            }


            //top level symbols
            var symbols = Project.Current.GetAllTopLevelSymbols(name).ToList();
            if (symbols.Count == 1) return symbols[0];

            if (createLocals)
            {
                var local = new LocalValue { Name = name };
                Locals.Add(local);
                return local;
            }

            //if nothing, throw and return null
            if (dothrow) Throw(Text.IdentifierNotDefined1, ThrowType.Error, place, name);
            return null;
        }
        bool LocalNameValid(string s)
        {
            return MatchIdentifier(s, false, null) == null;
        }
        bool SymbolNameValid(string s, Symbol nspace)
        {
            return !nspace.Children.Any(x => x.Name == s);
        }
        bool GlobalNameValid(string s)
        {
            return MatchIdentifier(s, false, null) == null;
        }

        //will convert the value to the correctly typed value
        //type can be null, means that Value will be the values own type, or automatically converted
        //all values MUST be convertable to object
        Value ConvertValue(Value value, _Type type, Place? place, bool dothrow = true, bool allowExplicitConvert = false)
        {
            if (value.Type == null)
            {
                if (type != null) Throw(Text.ExpectedTypedInsteadOfTypeless1, ThrowType.Error, place, type.ToString());
                return value;
            }

            if (type == null)
            {
                //check the automatic convertions from used configs
                for (int i = 0; i < UsedConfigs.Count; i++)
                {
                    for (int i2 = 0; i2 < UsedConfigs[i].ConvertionTypes.Count; i2++)
                    {
                        if (UsedConfigs[i].ConvertionTypes[i2].Item3 == ConvertionType.Automatic && value.Type.Equals(UsedConfigs[i].ConvertionTypes[i2].Item1))
                        {
                            return new CallValue { Function = Project.Current.GetConvertion(value.Type, type), Parameters = new List<Value>(1) { value } };
                        }
                    }
                }

                //if no automatic convertion, just return the value
                return value;
            }

            //replace known generic types with the actual type
            if (type is GenericType gt && KnownGenericTypes.Count != 0)
            {
                var l = KnownGenericTypes.Peek();
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i].Item1 == gt.Name)
                    {
                        type = l[i].Item2;
                        goto done;
                    }
                }
                done:;
            }

            //the value is of correct type
            if (value.Type.Equals(type)) return value;

            if (type is NullableType nullable)
            {
                var v = ConvertValue(value, nullable.Base, place, false);
                if (v != null) return new ConvertValue { Base = v, Type = type };
            }

            if (type == ObjectType.Value && !value.Type.IsNullable)
            {
                //values of reftypes do not need to be converted
                if (value.Type.IsRefType) return new ConvertValue { Base = value, Type = ObjectType.Value };

                //if not a reftype, we box
                else return new BoxedValue { Base = value, Type = value.Type };
            }

            //implicit convertions
            for (int i = 0; i < UsedConfigs.Count; i++)
            {
                for (int i2 = 0; i2 < UsedConfigs[i].ConvertionTypes.Count; i2++)
                {
                    if (UsedConfigs[i].ConvertionTypes[i2].Item1.Equals(value.Type) && UsedConfigs[i].ConvertionTypes[i2].Item2.Equals(type))
                    {
                        return new CallValue { Function = Project.Current.GetConvertion(value.Type, type), Parameters = new List<Value>(1) { value } };
                    }
                }
            }

            {
                Value v = Project.Current.GetConvertion(value.Type, type);
                if (v != null)
                {
                    if (allowExplicitConvert)
                    {
                        return new CallValue { Function = v, Parameters = new List<Value>(1) { value } };
                    }
                    else if (dothrow)
                    {
                        Throw(Text.ImplicitConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
                        dothrow = false;
                    }
                }
            }

            if (dothrow)
            {
                Throw(Text.TypeConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
                return type.GetDefaultValue();
            }
            else return null;
        }

        class IndexerSetTemporaryValue : SymbolValue
        {
            public IndexerSetTemporaryValue(CallValue v)
            {
                Instance = v;
            }
            public CallValue Call => (CallValue)base.Instance;
        }

        //represents an invalid value, should never get into a successfull compilation
        class InvalidValue : Value
        {
            public static readonly InvalidValue UnknownType = new InvalidValue { Type = InvalidType.Value };

            public override _Type Type { get; set; }

            public override void Read(ObjectNode node)
            {
                throw new NotImplementedException();
            }

            public override void Write(ObjectNode node)
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return "?";
            }
        }
        //invalid type
        class InvalidType : _Type
        {
            public static readonly InvalidType Value = new InvalidType();

            public override bool IsRefType => true;
            public override bool IsNullable => true;
            public override bool Equals(_Type t)
            {
                return false;
            }

            public override Value GetDefaultValue()
            {
                return new InvalidValue { Type = this };
            }

            public override void Read(ObjectNode node)
            {
                throw new NotImplementedException();
            }

            public override void ReplaceAllSubtypes(Func<_Type, _Type> func)
            {
                throw new NotImplementedException();
            }

            public override void Write(ObjectNode node)
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                return "?";
            }
        }

        Symbol MatchFullSymbol(ref SecondaryNode childNode, Symbol symbol)
        {
            while (childNode != null && childNode.NodeType == SecondaryNodeType.Member && symbol is ElementSymbol)
            {
                string membername = Tokens[childNode.Token + 1].Match;
                var s = symbol.Children.Find(x => x.Name == membername);
                if (s == null)
                {
                    Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, Tokens[childNode.Token + 1].Match, symbol.ToString());
                    return null;
                }
                symbol = s;
                childNode = childNode.Child;
            }
            return symbol;
        }

        //This function returns the Symbol, Value, or _Type that the Node represents
        //typeContext specifies to what type values should be converted into
        object MatchNode(Node node, bool prioritizeSymbol, _Type typeContext = null, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false, bool setIndexer = false, bool allowGenericlessTypes = false)
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

            //true if known generics pushed
            bool knownGenericsPushed = false;

            void addKnownGenerics(NormalType normalType)
            {
                if (normalType.Generics.Count != 0)
                {
                    if (knownGenericsPushed)
                    {
                        KnownGenericTypes.Pop();
                    }
                    else
                    {
                        knownGenericsPushed = true;
                    }
                    var l = new List<(string, _Type)>();
                    for (int i = 0; i < normalType.Generics.Count; i++)
                    {
                        l.Add((normalType.Generics[i].Item1, normalType.Generics[i].Item2));
                    }
                    KnownGenericTypes.Push(l);
                }
            }

            Value MakeValue(Value v)
            {
                if (v.Type is GenericType gt && KnownGenericTypes.Count != 0)
                {
                    var l = KnownGenericTypes.Peek();
                    for (int i = 0; i < l.Count; i++)
                    {
                        if (l[i].Item1 == gt.Name)
                        {
                            v.Type = l[i].Item2;
                        }
                    }
                }
                Value val = ConvertValue(v, childNode == null ? typeContext : null, Tokens[node.Token].Place, allowExplicitConvert: allowExplicitConvert);
                if (val?.Type != null && val.Type is NormalType nt) addKnownGenerics(nt);
                return val;
            }

            //get the initial symbol/type/value
            switch (node.NodeType)
            {
                case NodeType.None: break;
                default:
                    Throw(Text.NodeNotAValueOrTypeOrSymbol1, ThrowType.Error, Tokens[node.Token].Place, NodeToString(node));
                    return null;

                case NodeType.Parentheses:
                    {
                        //value inside parens is evaluated in a type free context, this can be useful
                        value = MakeValue(NodeToValue((Node)node.Data, null, checkCanGet, createLocals, ignoreChildren, strictProcedureType, false, setIndexer));
                        break;
                    }

                case NodeType.PackageSpecifier:
                    {
                        var symbolnode = (Node)node.Data;
                        ThrowIfNotIdentifier(node);
                        childNode = symbolnode.Child;
                        string packagename = Tokens[node.Token].Match;
                        string firstsymbolname = Tokens[symbolnode.Token].Match;
                        if (packagename == "local")
                        {
                            symbol = Package.GetTopLevelSymbol(firstsymbolname);
                        }
                        else if (packagename == "global")
                        {
                            foreach (var x in Project.Current.GetAllTopLevelSymbols(firstsymbolname))
                            {
                                if (symbol != null)
                                {
                                    symbol = null;
                                    break;
                                }
                                symbol = x;
                            }
                        }
                        else if (Project.Current.Packages.TryGetValue(packagename, out var package))
                        {
                            symbol = package.GetTopLevelSymbol(firstsymbolname);
                        }
                        else
                        {
                            Throw(Text.PackageNotImported1, ThrowType.Error, Tokens[node.Token].Place, packagename);
                            return null;
                        }
                        if (symbol == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[symbolnode.Token].Place);
                            return null;
                        }
                        symbol = MatchFullSymbol(ref childNode, symbol);
                        if (symbol == null) return null;
                        break;
                    }
                case NodeType.Identifier:
                    {
                        object o = MatchIdentifier(Tokens[node.Token].Match, true, Tokens[node.Token].Place, createLocals);
                        if (o is Value vv) value = MakeValue(vv);
                        else if (o is _Type tt) type = tt;
                        else if (o is Symbol ss)
                        {
                            symbol = MatchFullSymbol(ref childNode, ss);
                            if (symbol == null) return null;
                        }
                        break;
                    }
                case NodeType.Literal:
                    {
                        Token t = Tokens[node.Token];
                        switch (t.TokenType)
                        {
                            case TokenType.String:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.String, Value = t.Match });
                                break;
                            case TokenType.DecInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = BigInteger.Parse(t.Match).ToString("x") });
                                break;
                            case TokenType.DecNonInteger:
                                {
                                    int sep = t.Match.IndexOf('.');
                                    BigInteger numerator = BigInteger.Pow(10, t.Match.Length - sep - 1);
                                    BigInteger denominator = BigInteger.Parse(t.Match.Remove(sep) + t.Match.Substring(sep + 1));
                                    BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
                                    numerator = numerator / divisor;
                                    denominator = denominator / divisor;
                                    value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Fractional, Value = denominator.ToString("x") + "/" + numerator.ToString("x") });
                                    break;
                                }
                            case TokenType.HexInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = t.Match.Substring(2) });
                                break;
                            case TokenType.BinInteger:
                                {
                                    //make sure string can be divided into chunks of 8
                                    var sb = new StringBuilder(t.Match);
                                    sb.Remove(0, 2);
                                    var rem = sb.Length % 8;
                                    if (rem != 0) sb.Append('0', rem);
                                    var result = new StringBuilder(sb.Length / 4);
                                    for (int i = 0; i < sb.Length; i += 8)
                                    {
                                        result.Append(Convert.ToByte(sb.ToString(i, 8), 2).ToString("x"));
                                    }
                                    value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = result.ToString() });
                                    break;
                                }
                        }
                        break;
                    }
                case NodeType.Null:
                    if (typeContext == null || typeContext is ObjectType)
                    {
                        value = NullValue.ObjectNull;
                    }
                    if (typeContext == null)
                    {
                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                        value = InvalidValue.UnknownType;
                    }
                    else if (!typeContext.IsNullable)
                    {
                        Throw(Text.CannotAssignNullToNonNullable1, ThrowType.Error, Tokens[node.Token].Place, typeContext.ToString());
                        value = InvalidValue.UnknownType;
                    }
                    else value = new NullValue { Type = typeContext };
                    break;
                case NodeType.Default:
                    if (typeContext == null)
                    {
                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                        value = InvalidValue.UnknownType;
                    }
                    else
                    {
                        value = typeContext.GetDefaultValue();
                    }
                    break;
                case NodeType.True:
                    value = MakeValue(LiteralValue.True);
                    break;
                case NodeType.False:
                    value = MakeValue(LiteralValue.False);
                    break;
                case NodeType.This:
                    if (CurrentThis == null)
                    {
                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                        value = new ThisValue();
                    }
                    else
                    {
                        value = CurrentThis;
                    }
                    break;
                case NodeType.Object:
                    type = ObjectType.Value;
                    break;
                case NodeType.Bool:
                    type = BooleanType.Value;
                    break;
                case NodeType.Procedural:
                    {
                        if (typeContext == null && !strictProcedureType) MakeValue(value = ScopeToProcedure((Statement)node.Data));
                        else value = MakeValue(ScopeToProcedure((Statement)node.Data, typeContext));
                        break;
                    }
                case NodeType.Convertion:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var converttype = NodeToType(nodes.Item2);
                        value = MakeValue(NodeToValue(nodes.Item1, converttype, allowExplicitConvert: true));
                        break;
                    }
                case NodeType.Record:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var recordValue = new RecordValue();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            FunctionDeclToAssignment(nodeList[i]);
                            var nodes = ((Node, Node))nodeList[i].Data;
                            string name = Tokens[nodes.Item1.Token].Match;
                            ThrowIfNotIdentifier(nodes.Item1);
                            if (!recordValue.Fields.TrueForAll(x => x.Item1 != name))
                            {
                                Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                            }
                            recordValue.Fields.Add((name, NodeToValue(nodes.Item2, null)));
                        }
                        value = recordValue;
                        break;
                    }
                case NodeType.RecordType:
                    {
                        var nodeList = (List<(Node, Node)>)node.Data;
                        var recordType = new RecordType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            var name = Tokens[nodeList[i].Item2.Token].Match;
                            ThrowIfNotIdentifier(nodeList[i].Item2);
                            if (!recordType.Fields.Add((name, NodeToType(nodeList[i].Item1))))
                            {
                                Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[nodeList[i].Item2.Token].Place, name);
                            }
                        }
                        type = recordType;
                        break;
                    }
                case NodeType.Init:
                    {
                        var data = ((Node, List<Node>))node.Data;
                        var newType = NodeToType(data.Item1);
                        if (data.Item2 != null && data.Item2.Count != 0)
                        {
                            if (newType is NormalType normalType)
                            {
                                addKnownGenerics(normalType);
                                var newValue = new NewValue { Type = newType };
                                var newSymbol = normalType.Base;
                                newValue.FieldValues.Capacity = data.Item2.Count;
                                for (int i = 0; i < data.Item2.Count; i++)
                                {
                                    if (IsAssignmentOrFuncDecl(data.Item2[i]))
                                    {
                                        FunctionDeclToAssignment(data.Item2[i]);
                                        var nodes = ((Node, Node))data.Item2[i].Data;
                                        string name = Tokens[nodes.Item1.Token].Match;
                                        ThrowIfNotIdentifier(nodes.Item1);
                                        var symbolIndex = newSymbol.Children.FindIndex(x => x.Name == name);
                                        if (symbolIndex == -1)
                                        {
                                            Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, normalType.Base.ToString());
                                        }
                                        else
                                        {
                                            var fieldSymbol = newSymbol.Children[symbolIndex];
                                            if (!CanSet(fieldSymbol))
                                            {
                                                Throw(Text.ValueNotSettable1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                            }
                                            newValue.FieldValues.Add((fieldSymbol, NodeToValue(nodes.Item2, fieldSymbol is VariableSymbol vs ? vs.Type : ((PropertySymbol)fieldSymbol).Type)));
                                        }
                                    }
                                    else
                                    {
                                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item2[i].Token].Place, Tokens[data.Item2[i].Token].Match);
                                    }
                                }
                                value = newValue;
                            }
                            else if (newType is CollectionType collectionType)
                            {
                                var collectionValue = new CollectionValue { Type = collectionType };
                                collectionValue.Values.Capacity = data.Item2.Count;
                                for (int i = 0; i < data.Item2.Count; i++)
                                {
                                    collectionValue.Values.Add(NodeToValue(data.Item2[i], collectionType.Base));
                                }
                                value = collectionValue;
                            }
                            else
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item1.Token].Place, Tokens[data.Item1.Token].Match);
                                value = new InvalidValue { Type = typeContext };
                            }
                        }
                        else
                        {
                            if (newType is NormalType normalType)
                            {
                                value = MakeValue(new NewValue { Type = newType });
                            }
                            else if (newType is CollectionType collectionType)
                            {
                                value = MakeValue(new CollectionValue { Type = collectionType });
                            }
                            else
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item1.Token].Place, Tokens[data.Item1.Token].Match);
                                value = typeContext == null ? NullValue.ObjectNull : typeContext.GetDefaultValue();
                            }
                        }
                        break;
                    }
                case NodeType.Collection:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var collection = new CollectionValue();
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
                case NodeType.FunctionType:
                    {
                        var data = ((List<Node>, List<Node>, Node))node.Data;
                        var funcType = new FunctionType();
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcType.Generics.Add(name);
                            }
                        }
                        for (int i = 0; i < data.Item2.Count; i++)
                        {
                            funcType.Parameters.Add((NodeToType(data.Item2[i]), null, false));
                        }
                        if (data.Item3 != null) funcType.ReturnType = NodeToType(data.Item3);
                        if (data.Item1 != null) GenericsPop();
                        type = funcType;
                        break;
                    }
                case NodeType.TypedFunction:
                    {
                        var data = ((List<Node>, List<(Node, Node)>, Node))node.Data;
                        var funcType = new FunctionType();
                        var funcValue = new FunctionValue { Type = funcType };
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcType.Generics.Add(name);
                                funcValue.Generics.Add(name);
                            }
                        }
                        if (data.Item2.Count != 0)
                        {
                            LocalsPush();
                            for (int i = 0; i < data.Item2.Count; i++)
                            {
                                var nodes = data.Item2[i];
                                var localtype = NodeToType(nodes.Item1);
                                string name;
                                Place nameplace;
                                Value val;
                                if(nodes.Item2.NodeType == NodeType.Assignment)
                                {
                                    var anodes = ((Node, Node))nodes.Item2.Data;
                                    ThrowIfNotIdentifier(anodes.Item1);
                                    name = Tokens[anodes.Item1.Token].Match;
                                    nameplace = Tokens[anodes.Item1.Token].Place;
                                    val = NodeToValue(anodes.Item2, localtype);
                                }
                                else
                                {
                                    ThrowIfNotIdentifier(nodes.Item2);
                                    name = Tokens[nodes.Item2.Token].Match;
                                    nameplace = Tokens[nodes.Item2.Token].Place;
                                    val = null;
                                }
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, nameplace, name);
                                }
                                Locals.Add(new LocalValue { Name = name, Type = localtype });
                                funcType.Parameters.Add((localtype, name, val != null));
                                funcValue.Parameters.Add((name, val));
                            }
                        }

                        //this is mainly for good error messages when making methods
                        //doesnt hurt in other places either
                        if (typeContext is FunctionType ft) funcValue.Value = NodeToValue(data.Item3, ft.ReturnType, strictProcedureType: true);

                        //normal behaviour
                        else funcValue.Value = NodeToValue(data.Item3, null);

                        funcType.ReturnType = funcValue.Value.Type;
                        if (data.Item1 != null) GenericsPop();
                        if (data.Item2.Count != 0) LocalsPop();
                        value = MakeValue(funcValue);
                        break;
                    }
                case NodeType.UntypedFunction:
                    {
                        var data = ((List<Node>, List<Node>, Node))node.Data;
                        FunctionType funcType;
                        FunctionValue funcValue;
                        if (typeContext == null)
                        {
                            if (data.Item2.Count == 0)
                            {
                                funcType = new FunctionType();
                                funcValue = new FunctionValue { Type = funcType };
                                if (data.Item1 != null)
                                {
                                    GenericsPush();
                                    for (int i = 0; i < data.Item1.Count; i++)
                                    {
                                        string name = Tokens[data.Item1[i].Token].Match;
                                        ThrowIfNotIdentifier(data.Item1[i]);
                                        if (!LocalNameValid(name))
                                        {
                                            Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                        }
                                        GenericTypes.Add(new GenericType { Name = name });
                                        funcValue.Generics.Add(name);
                                        funcType.Generics.Add(name);
                                    }
                                }
                                funcValue.Value = NodeToValue(data.Item3, null);
                                funcType.ReturnType = funcValue.Value.Type;
                                value = funcValue;
                                if (data.Item1 != null) GenericsPop();
                                break;
                            }
                            funcType = new FunctionType();
                            Throw(Text.ImplicitlyTypedFunctionIllegal0, ThrowType.Error, Tokens[node.Token].Place);
                        }
                        else if (typeContext is FunctionType)
                        {
                            funcType = (FunctionType)typeContext;
                        }
                        else
                        {
                            funcType = new FunctionType();
                            Throw(Text.ImplicitlyTypedFunctionIllegal0, ThrowType.Error, Tokens[node.Token].Place);
                        }
                        funcValue = new FunctionValue { Type = funcType };
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            if (data.Item1.Count != funcType.Generics.Count)
                            {
                                Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[node.Token + 1].Place, funcType.Generics.Count.ToString());
                            }
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcValue.Generics.Add(name);
                            }
                        }
                        if (data.Item2.Count != funcType.Parameters.Count)
                        {
                            Throw(Text.ParameterCountIllegal1, ThrowType.Error, Tokens[node.Token].Place, funcType.Parameters.Count.ToString());
                        }
                        int limit = data.Item2.Count < funcType.Parameters.Count ? data.Item2.Count : funcType.Parameters.Count;
                        if (limit != 0)
                        {
                            LocalsPush();
                            for (int i = 0; i < limit; i++)
                            {
                                string name = Tokens[data.Item2[i].Token].Match;
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item2[i].Token].Place, name);
                                }
                                ThrowIfNotIdentifier(data.Item2[i]);
                                funcValue.Parameters.Add((name, null));
                                Locals.Add(new LocalValue { Name = name, Type = funcType.Parameters[i].Item1 });
                            }
                        }
                        funcValue.Value = NodeToValue(data.Item3, funcType.ReturnType, strictProcedureType: true);
                        if (limit != 0) LocalsPop();
                        if (data.Item1 != null) GenericsPop();
                        value = funcValue;
                        break;
                    }
                case NodeType.Not:
                    {
                        var cond = NodeToValue((Node)node.Data, BooleanType.Value);
                        value = new OperationValue { OperationType = OperationType.Not, Values = new List<Value>(1) { cond } };
                        break;
                    }
                case NodeType.And:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var cond1 = NodeToValue(nodes.Item1, BooleanType.Value);
                        var cond2 = NodeToValue(nodes.Item2, BooleanType.Value);
                        value = new OperationValue { OperationType = OperationType.And, Values = new List<Value>(2) { cond1, cond2 } };
                        break;
                    }
                case NodeType.Or:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var first = MatchNode(nodes.Item1, false, BooleanType.Value);
                        if (first is Value cond1)
                        {
                            var cond2 = NodeToValue(nodes.Item2, BooleanType.Value);
                            value = new OperationValue { OperationType = OperationType.Or, Values = new List<Value>(2) { cond1, cond2 } };
                        }
                        else if (first is _Type type1)
                        {
                            var type2 = NodeToType(nodes.Item2);
                            type = CombineTypes(type1, type2);
                        }
                        else
                        {
                            Throw(Text.ValueExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, NodeToString(nodes.Item1));
                        }
                        break;
                    }
                case NodeType.Is:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var _type = NodeToType(nodes.Item2);
                        var _value = NodeToValue(nodes.Item1, type);
                        value = new OperationValue { OperationType = OperationType.Is, Types = new List<_Type>(1) { _type }, Values = new List<Value>(1) { _value } };
                        break;
                    }
                case NodeType.IsNot:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var _type = NodeToType(nodes.Item2);
                        var _value = NodeToValue(nodes.Item1, type);
                        value = new OperationValue { OperationType = OperationType.IsNot, Types = new List<_Type>(1) { _type }, Values = new List<Value>(1) { _value } };
                        break;
                    }
                case NodeType.IsNull:
                    {
                        var n = (Node)node.Data;
                        var _value = NodeToValue((Node)node.Data, null);
                        if (!_value.Type.IsNullable)
                        {
                            Throw(Text.ValueOfNullableTypeExpected0, ThrowType.Error, Tokens[n.Token].Place);
                        }
                        value = new OperationValue { OperationType = OperationType.IsNull, Values = new List<Value>(1) { _value } };
                        break;
                    }
                case NodeType.UnaryOperator:
                    {
                        var data = ((string, Node))node.Data;
                        List<(_Type, Value)> possibilities = new List<(_Type, Value)>();
                        for (int i = 0; i < UsedConfigs.Count; i++)
                        {
                            for (int i2 = 0; i2 < UsedConfigs[i].UnaryOperators.Count; i2++)
                            {
                                if (UsedConfigs[i].UnaryOperators[i2].Item1 == data.Item1)
                                {
                                    possibilities.Add((UsedConfigs[i].UnaryOperators[i2].Item2, UsedConfigs[i].UnaryOperators[i2].Item3));
                                }
                            }
                        }
                        if (possibilities.Count == 1)
                        {
                            value = new CallValue { Function = possibilities[0].Item2, Parameters = new List<Value>(1) { NodeToValue(data.Item2, possibilities[0].Item1) } };
                            break;
                        }
                        var operand = NodeToValue(data.Item2, null);
                        if (operand.Type is InvalidType)
                        {
                            value = InvalidValue.UnknownType;
                            break;
                        }
                        for (int i = 0; i < possibilities.Count; i++)
                        {
                            if (possibilities[i].Item1.Equals(operand.Type))
                            {
                                value = MakeValue(new CallValue { Function = possibilities[i].Item2, Parameters = new List<Value>(1) { operand } });
                                break;
                            }
                        }
                        if (value == null)
                        {
                            value = InvalidValue.UnknownType;
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, data.Item1 + " " + (operand.Type == null ? TypelessTypeName : operand.Type.ToString()));
                        }
                        break;
                    }
                case NodeType.BinaryOperator:
                    {
                        var data = ((Node, string, Node))node.Data;
                        var operand1 = NodeToValue(data.Item1, null);
                        if (operand1.Type is InvalidType)
                        {
                            value = InvalidValue.UnknownType;
                            break;
                        }
                        List<(_Type, Value)> possibles = new List<(_Type, Value)>();
                        for (int i = 0; i < UsedConfigs.Count; i++)
                        {
                            for (int i2 = 0; i2 < UsedConfigs[i].BinaryOperators.Count; i2++)
                            {
                                if (UsedConfigs[i].BinaryOperators[i2].Item2 == data.Item2 && UsedConfigs[i].BinaryOperators[i2].Item1.Equals(operand1.Type))
                                {
                                    possibles.Add((UsedConfigs[i].BinaryOperators[i2].Item3, UsedConfigs[i].BinaryOperators[i2].Item4));
                                }
                            }
                        }
                        if (possibles.Count == 1)
                        {
                            value = MakeValue(new CallValue { Function = possibles[0].Item2, Parameters = new List<Value>(2) { operand1, NodeToValue(data.Item3, possibles[0].Item1) } });
                            break;
                        }
                        if (possibles.Count != 0)
                        {
                            var operand2 = NodeToValue(data.Item3, null);
                            if (operand2.Type is InvalidType)
                            {
                                value = InvalidValue.UnknownType;
                                break;
                            }
                            for (int i = 0; i < possibles.Count; i++)
                            {
                                if (possibles[i].Item1.Equals(operand2.Type))
                                {
                                    value = MakeValue(new CallValue { Function = possibles[i].Item2, Parameters = new List<Value>(2) { operand1,  operand2 } });
                                    break;
                                }
                            }
                            if (value != null) break;
                        }
                        Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, (operand1.Type?.ToString() ?? TypelessTypeName) + " " + data.Item2 + " " + (NodeToValue(data.Item3, null).Type?.ToString() ?? TypelessTypeName));
                        break;
                    }

                //conditionals are implemented as procedural values with a bunch of if statements
                case NodeType.Conditional:
                    {
                        var scope = new ScopeInstruction();
                        var data = ((List<(Node, Node)>, Node))node.Data;
                        _Type condType = typeContext;
                        for (int i = 0; i < data.Item1.Count; i++)
                        {
                            var val = NodeToValue(data.Item1[i].Item2, condType);
                            if (condType == null) condType = val.Type;
                            scope.Instructions.Add(new IfInstruction { Condition = NodeToValue(data.Item1[i].Item1, BooleanType.Value), Instruction = new ReturnInstruction { Value = val }, File = Tokens[data.Item1[i].Item1.Token].Place.File, Line = Tokens[data.Item1[i].Item1.Token].Place.Line, Index = Tokens[data.Item1[i].Item1.Token].Place.Index });
                        }
                        scope.Instructions.Add(new ReturnInstruction { Value = NodeToValue(data.Item2, condType), File = Tokens[data.Item2.Token].Place.File, Line = Tokens[data.Item2.Token].Place.Line, Index = Tokens[data.Item2.Token].Place.Index });
                        value = new ProceduralValue { Instruction = scope, Type = condType };
                        break;
                    }
            }

            //convert symbol to value or type
            //ClassSymbols and StructSymbols can be converted to types
            //VariableSymbols and PropertySymbols can be converted to values
            if(symbol != null)
            {
                //throw a warning if symbol is deprecated
                if(symbol.Attributes.TryGetValue("deprecated", out string deprmessage))
                {
                    if (deprmessage == null) Throw(Text.SymbolDeprecated1, ThrowType.Warning, Tokens[node.Token].Place, symbol.ToString());
                    else Throw(Text.SymbolDeprecatedMessage2, ThrowType.Warning, Tokens[node.Token].Place, symbol.ToString(), deprmessage);
                }

                if (prioritizeSymbol && childNode == null) return symbol;

                if (symbol is ElementSymbol es && es.Alternate != null) symbol = es.Alternate;

                if (symbol is TypeSymbol typeSymbol)
                {
                    var normalType = new NormalType { Base = symbol, RefType = true };

                    //generics
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.Generics)
                    {
                        List<Node> nl = (List<Node>)childNode.Data;
                        for (int i = 0; i < nl.Count; i++)
                        {
                            var t = NodeToType(nl[i]);
                            if (i < typeSymbol.Generics.Count)
                            {
                                normalType.Generics.Add((typeSymbol.Generics[i], t));
                            }
                        }
                        if (nl.Count != typeSymbol.Generics.Count) Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, typeSymbol.Generics.Count.ToString());
                        childNode = childNode.Child;
                    }
                    else if (!allowGenericlessTypes && typeSymbol.Generics.Count != 0) return symbol;

                    type = normalType;
                }
                else if (symbol is VariableSymbol vs)
                {
                    if (checkCanGet && !CanGet(vs))
                    {
                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Symbol = vs, Type = vs.Type, Instance = CurrentThis != null && vs.Parent == CurrentSymbol ? CurrentThis : null });
                }
                else if (symbol is PropertySymbol ps)
                {
                    if (!CanGet(ps))
                    {
                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Symbol = ps, Type = ps.Type, Instance = CurrentThis != null && ps.Parent == CurrentSymbol ? CurrentThis : null });
                }
            }

            if (value == null && type == null) return symbol;
            else symbol = null;

            //additions
            if (!ignoreChildren)
            {
                while (childNode != null)
                {
                    if (value != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.Call || childNode.NodeType == SecondaryNodeType.Indexer)
                        {
                            FunctionType funcType = null;
                            if (childNode.NodeType == SecondaryNodeType.Call)
                            {
                                if (value.Type is FunctionType ft)
                                {
                                    funcType = ft;
                                }
                                else
                                {
                                    Throw(Text.ValueNotInvokable1, ThrowType.Error, Tokens[childNode.Token].Place, value.ToString());
                                    funcType = new FunctionType();
                                    childNode = childNode.Child;
                                    continue;
                                }
                            }
                            else if (childNode.NodeType == SecondaryNodeType.Indexer)
                            {
                                if (value.Type is NormalType nt)
                                {
                                    var typeSymbol = nt.Base;
                                    string nameToFind = setIndexer && childNode.Child == null ? IndexerSetterName : IndexerGetterName;
                                    int childIndex = typeSymbol.Children.FindIndex(x => x.Name == nameToFind);
                                    if (childIndex != -1)
                                    {
                                        var indexerSymbol = typeSymbol.Children[childIndex];
                                        if (indexerSymbol is VariableSymbol vs && vs.Type is FunctionType ft && ft.Parameters.Count != 0)
                                        {
                                            value = new SymbolValue { Symbol = vs, Instance = value, Type = vs.Type };
                                            if (setIndexer && childNode.Child == null)
                                            {
                                                var newParams = new List<(_Type, string, bool)>(ft.Parameters.Count - 1);
                                                for (int i = 0; i < newParams.Capacity; i++)
                                                {
                                                    newParams.Add(ft.Parameters[i]);
                                                }
                                                funcType = new FunctionType { Generics = ft.Generics, Parameters = newParams, ReturnType = ft.ReturnType };
                                            }
                                            else funcType = ft;
                                            goto nothrow;
                                        }
                                    }
                                }
                                Throw(Text.IndexerIllegal0, ThrowType.Error, Tokens[childNode.Token].Place);
                                funcType = new FunctionType();
                                nothrow:;
                            }
                            var callValue = new CallValue { Function = value };
                            List<Node> callNodes = (List<Node>)childNode.Data;
                            Node[] positionedNodes = new Node[funcType.Parameters.Count];
                            for (int i = 0; i < callNodes.Count; i++)
                            {
                                if (i >= funcType.Parameters.Count)
                                {
                                    Throw(Text.TooManyParameters0, ThrowType.Error, Tokens[callNodes[i].Token].Place);
                                    break;
                                }
                                if (IsAssignmentOrFuncDecl(callNodes[i]))
                                {
                                    FunctionDeclToAssignment(callNodes[i]);
                                    var nodes = ((Node, Node))callNodes[i].Data;
                                    ThrowIfNotIdentifier(nodes.Item1);
                                    string name = Tokens[nodes.Item1.Token].Match;
                                    int index = funcType.Parameters.FindIndex(x => x.name == name);
                                    if (index == -1)
                                    {
                                        Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                    }
                                    else
                                    {
                                        positionedNodes[index] = nodes.Item2;
                                    }
                                }
                                else
                                {
                                    if (positionedNodes[i] != null) Throw(Text.DuplicateParameter1, ThrowType.Error, Tokens[callNodes[i].Token].Place, funcType.Parameters[i].name);
                                    positionedNodes[i] = callNodes[i];
                                }
                            }
                            for (int i = 0; i < positionedNodes.Length; i++)
                            {
                                if (positionedNodes[i] == null)
                                {
                                    if (!funcType.Parameters[i].optional)
                                    {
                                        if (funcType.Parameters[i].name == null)
                                            Throw(Text.MissingParameter1, ThrowType.Error, Tokens[childNode.Token].Place, (i + 1).ToString());
                                        else Throw(Text.MissingParameter2, ThrowType.Error, Tokens[childNode.Token].Place, funcType.Parameters[i].name, (i + 1).ToString());
                                    }
                                    callValue.Parameters.Add(null);
                                }
                                else
                                {
                                    callValue.Parameters.Add(NodeToValue(positionedNodes[i], funcType.Parameters[i].type));
                                }
                            }
                            childNode = childNode.Child;
                            if (setIndexer && childNode == null)
                            {
                                return new IndexerSetTemporaryValue(callValue);
                            }
                            else
                            {
                                value = MakeValue(callValue);
                            }
                        }
                        else if (childNode.NodeType == SecondaryNodeType.Member)
                        {
                            string name = Tokens[childNode.Token + 1].Match;
                            if (value.Type is NormalType nt)
                            {
                                var baseSymbol = nt.Base;
                                var memberIndex = baseSymbol.Children.FindIndex(x => x.Name == name);
                                if (memberIndex == -1)
                                {
                                    Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, nt.Base.ToString());
                                    childNode = childNode.Child;
                                }
                                else
                                {
                                    childNode = childNode.Child;
                                    var memberSymbol = baseSymbol.Children[memberIndex];
                                    if (!CanGet(memberSymbol))
                                    {
                                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[childNode.Token + 1].Place, memberSymbol.ToString());
                                    }
                                    value = MakeValue(new SymbolValue { Symbol = memberSymbol, Instance = value, Type = memberSymbol is VariableSymbol vs ? vs.Type : ((PropertySymbol)memberSymbol).Type });
                                }
                            }
                            else if (value.Type is RecordType rt)
                            {
                                var field = rt.Fields.First(x => x.name == name);
                                if (field.name == null)
                                {
                                    Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, rt.ToString());
                                    childNode = childNode.Child;
                                }
                                else
                                {
                                    childNode = childNode.Child;
                                    value = MakeValue(new MemberValue { Base = value, Name = name, Type = field.Item2 });
                                }
                            }
                            else if (value.Type is CollectionType ct)
                            {
                                if (name == "Length")
                                {
                                    value = new OperationValue { OperationType = OperationType.Length, Values = new List<Value>(1) { value } };
                                }
                                else if (name == "Iterate")
                                {
                                    value = new OperationValue
                                    {
                                        OperationType = OperationType.Iterate,
                                        Values = new List<Value>(1) { value },
                                        Type = new FunctionType { Parameters = new List<(_Type type, string name, bool optional)>(1) { (new FunctionType { Parameters = new List<(_Type type, string name, bool optional)>(1) { (ct.Base, null, false) } }, null, false) } }
                                    };
                                }
                                else
                                {
                                    Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                                }
                                childNode = childNode.Child;
                            }
                            else
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                                childNode = childNode.Child;
                            }
                        }
                        else if (childNode.NodeType == SecondaryNodeType.Generics && value.Type is FunctionType funcType)
                        {
                            //function generics are handled by returning a new function with the set types

                            var genericNodes = (List<Node>)childNode.Data;
                            var replacedTypes = new _Type[genericNodes.Count];

                            childNode = childNode.Child;

                            if (genericNodes.Count != funcType.Generics.Count)
                            {
                                Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, funcType.Generics.Count.ToString());
                            }

                            for (int i = 0; i < genericNodes.Count; i++)
                            {
                                replacedTypes[i] = NodeToType(genericNodes[i]);
                            }

                            _Type ResolveType(_Type t)
                            {
                                if (t is GenericType gt)
                                {
                                    int gi = funcType.Generics.FindIndex(x => gt.Name == x);
                                    if (gi == -1 || replacedTypes.Length < gi) return t;
                                    return replacedTypes[gi];
                                }
                                else
                                {
                                    t.ReplaceAllSubtypes(ResolveType);
                                    return t;
                                }
                            }

                            var newtype = new FunctionType { ReturnType = ResolveType(funcType.ReturnType) };
                            for (int i = 0; i < funcType.Parameters.Count; i++)
                            {
                                newtype.Parameters.Add((ResolveType(funcType.Parameters[i].Item1), funcType.Parameters[i].Item2, funcType.Parameters[i].Item3));
                            }
                            value.Type = newtype;
                        }
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            childNode = childNode.Child;
                        }
                    }
                    else if (type != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.CollectionTypeSpecifier)
                        {
                            type = new CollectionType { Base = type };
                        }
                        else if (childNode.NodeType == SecondaryNodeType.BoxedSpecifier)
                        {
                            if (type is NullableType)
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            }
                            else type = new NullableType { Base = type };
                        }
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            break;
                        }
                        childNode = childNode.Child;
                    }
                }
            }

            if (knownGenericsPushed) KnownGenericTypes.Pop();
            return value ?? (object)type;
        }
        Value NodeToValue(Node n, _Type typeContext, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false, bool setIndexer = false)
        {
            object o = MatchNode(n, false, typeContext, checkCanGet, createLocals, ignoreChildren, strictProcedureType, allowExplicitConvert, setIndexer);
            if (o is Value v) return v;
            if (o != null) Throw(Text.ValueExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return new InvalidValue { Type = typeContext ?? InvalidType.Value };
        }
        _Type NodeToType(Node n, bool allowGenericlessTypes = false)
        {
            object o = MatchNode(n, false, allowGenericlessTypes: allowGenericlessTypes);
            if (o is _Type t) return t;
            if (o != null) Throw(Text.TypeExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return InvalidType.Value;
        }

        #endregion
        #region analyzing

        void ThrowAttributesNotValid(Statement st)
        {
            Throw(Text.AttributesNotValidForItem0, ThrowType.Error, Tokens[st.Token].Place);
        }

        Dictionary<string, string> ParseAttributes(Token t)
        {
            var dict = new Dictionary<string, string>();
            using (var stringReader = new System.IO.StringReader(t.Match))
            using (var xml = XmlReader.Create(stringReader, new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment }))
            {
                try
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            if (!dict.ContainsKey(xml.LocalName))
                            {
                                dict.Add(xml.LocalName, xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                            }
                            else xml.ReadElementContentAsString();
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw(Text.XmlParsingError1, ThrowType.Error, t.Place, e.Message);
                }
            }
            return dict;
        }

        //structural analyzing
        //source file structure like what statements in global context, what inside elements, what inside classes etc.
        //Symbol tree construction to Package.Symbols
        void AnalyzeGlobalStatement(Statement statement, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                case StatementType.None:break;
                default:
                    if (attributes != null) ThrowAttributesNotValid(statement);
                    AnalyzeConfigStatement(statement, LocalConfig);
                    break;;

                case StatementType.Main:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        if (Main != null)
                        {
                            Throw(Text.MultipleMain0, ThrowType.Error, Tokens[statement.Token].Place);
                        }
                        Main = (Node)statement.Data[0];
                        break;
                    }

                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1, Attributes = attributes ?? EmptyAttributes, Package = Package };
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        Package.Symbols.Add(symbol);
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol, null));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        break;
                    }
                case StatementType.Use:
                    {
                        var packagename = ((string, Place))statement.Data[0];
                        var configname = ((string, Place))statement.Data[1];
                        Config config;
                        if (packagename.Item1 == null)
                        {
                            var configs = Project.Current.GetAllConfigs(configname.Item1).ToList();
                            if (configs.Count == 1) config = configs[0];
                            else
                            {
                                var args = configs.ConvertAll(x => x.ToString());
                                args.Insert(0, configname.Item1);
                                Throw(Text.IdentifierAmbiguityV, ThrowType.Error, configname.Item2, args.ToArray());
                                break;
                            }
                        }
                        else
                        {
                            if (!Project.Current.Packages.TryGetValue(packagename.Item1, out var pack))
                            {
                                Throw(Text.PackageNotImported1, ThrowType.Error, packagename.Item2, packagename.Item1);
                                break;
                            }
                            config = pack.GetConfig(configname.Item1);
                            if (config == null)
                            {
                                Throw(Text.IdentifierNotDefined1, ThrowType.Error, configname.Item2, configname.Item1);
                                break;
                            }
                        }
                        UseConfig(config);
                        break;
                    }
                case StatementType.Config:
                    {
                        var name = ((string, Place))statement.Data[0];
                        var statements = (List<Statement>)statement.Data[1];
                        var config = new Config { Name = name.Item1 };
                        Package.Configs.Add(config);
                        PendingConfigs.Add((statements, config));
                        break;
                    }
                case StatementType.ConvertionDeclaration:
                    {
                        var node = (Node)statement.Data[0];
                        if (node.NodeType == NodeType.Convertion)
                        {
                            var nodes = ((Node, Node))node.Data;
                            PendingConvertions.Add(new PendingConvertion { From = nodes.Item1, To = nodes.Item2, Value = (Node)statement.Data[1] });
                        }
                        else
                        {
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, ":", Tokens[node.EndToken].Match);
                        }
                        break;
                    }
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        var attr = ParseAttributes(Tokens[statement.Token]);
                        AnalyzeGlobalStatement((Statement)statement.Data[0], attr);
                        break;
                    }
            }
        }
        void AnalyzeElementStatement(Statement statement, ElementSymbol element, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                case StatementType.None:break;
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    {
                        AnalyzeVariableDeclaration(statement, element, attributes);
                        break;
                    }
                case StatementType.DoubleNode:
                case StatementType.Node:
                    {
                        AnalyzeVariableAssignment(statement, element, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate, attributes);
                        break;
                    }
                case StatementType.PropertyDeclaration:
                    {
                        AnalyzeProperty(statement, element, attributes);
                        break;
                    }
                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1, Parent = element, Attributes = attributes ?? EmptyAttributes };
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol, null));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        break;
                    }
                case StatementType.Class:
                    {
                        AnalyzeTypeDeclaration<ClassSymbol>(statement, element, attributes);
                        break;
                    }
                case StatementType.Struct:
                    {
                        AnalyzeTypeDeclaration<StructSymbol>(statement, element, attributes);
                        break;
                    }
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        AnalyzeElementStatement((Statement)statement.Data[0], element, ParseAttributes(Tokens[statement.Token]));
                        break;
                    }
            }
        }
        void AnalyzeTypeDeclarationStatement(Statement statement, TypeSymbol type, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                case StatementType.None:break;
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    AnalyzeVariableDeclaration(statement, type, attributes);
                    break;
                case StatementType.DoubleNode:
                case StatementType.Node:
                    AnalyzeVariableAssignment(statement, type, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate, attributes);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, type, attributes);
                    break;
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        AnalyzeTypeDeclarationStatement((Statement)statement.Data[0], type, ParseAttributes(Tokens[statement.Token]));
                        break;
                    }
            }
        }
        void AnalyzeConfigStatement(Statement statement, Config config)
        {
            switch (statement.StatementType)
            {
                case StatementType.None:break;
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.Use:
                    {
                        var name = ((string, Place))statement.Data[0];
                        Config c = Project.Current.GetConfig(name.Item1);
                        if (c == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        else
                        {
                            config.Configs.Add(c);
                        }
                        break;
                    }
                case StatementType.Alias:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        var target = MatchNode((Node)statement.Data[1], true);
                        if (target != null) config.Aliases.Add(name.Item1, target is Symbol s ? s.ToString() : target);
                        break;
                    }
                case StatementType.OperatorDeclaration:
                    {
                        var generics = (List<string>)statement.Data[0];
                        var node = (Node)statement.Data[1];
                        (Node, Node) nodes;
                        if (node.NodeType == NodeType.Assignment)
                        {
                            nodes = ((Node, Node))node.Data;
                        }
                        else
                        {
                            nodes = (node, null);
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, "=", Tokens[node.EndToken].Match);
                        }
                        if (nodes.Item1.NodeType == NodeType.UnaryOperator)
                        {
                            var data = ((string, Node))nodes.Item1.Data;
                            var operand = NodeToType(data.Item2);
                            for (int i = 0; i < config.UnaryOperators.Count; i++)
                            {
                                if (config.UnaryOperators[i].Item1 == data.Item1 && config.UnaryOperators[i].Item2.Equals(operand))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[node.Token].Place, data.Item1 + operand.ToString());
                                }
                            }
                            if (nodes.Item2 != null)
                            {
                                var value = NodeToValue(nodes.Item2, null);
                                if (!(value.Type is FunctionType funcType && funcType.Parameters.Count == 1 && funcType.Parameters[0].Item1.Equals(operand)))
                                {
                                    Throw(Text.OperatorValueIllegal0, ThrowType.Error, Tokens[nodes.Item2.Token].Place, Tokens[nodes.Item2.Token].Match);
                                }
                                config.UnaryOperators.Add((data.Item1, operand, value));
                            }
                        }
                        else if (nodes.Item1.NodeType == NodeType.BinaryOperator)
                        {
                            var data = ((Node, string, Node))nodes.Item1.Data;
                            var operand1 = NodeToType(data.Item1);
                            var operand2 = NodeToType(data.Item3);
                            for (int i = 0; i < config.BinaryOperators.Count; i++)
                            {
                                if (config.BinaryOperators[i].Item2 == data.Item2 && config.BinaryOperators[i].Item1.Equals(operand1) && config.BinaryOperators[i].Item3.Equals(operand2))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[node.Token].Place, operand1.ToString() + " " + data.Item2 + " " + operand2.ToString());
                                }
                            }
                            if (nodes.Item2 != null)
                            {
                                var value = NodeToValue(nodes.Item2, null);
                                if (!(value.Type is FunctionType funcType && funcType.Parameters.Count == 2 && funcType.Parameters[0].Item1.Equals(operand1) && funcType.Parameters[1].Item1.Equals(operand2)))
                                {
                                    Throw(Text.OperatorValueIllegal0, ThrowType.Error, Tokens[nodes.Item2.Token].Place, Tokens[nodes.Item2.Token].Match);
                                }
                                config.BinaryOperators.Add((operand1, data.Item2, operand2, value));
                            }
                        }
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[statement.Token].Place, Tokens[statement.Token].Match);
                        }
                        break;
                    }
                case StatementType.ConvertionModeDefinition:
                    {
                        var node = (Node)statement.Data[1];
                        if (node.NodeType == NodeType.Convertion)
                        {
                            var nodes = ((Node, Node))node.Data;
                            _Type fromType = NodeToType(nodes.Item1, true), toType = NodeToType(nodes.Item2, true);
                            if (Project.Current.GetConvertion(fromType, toType) == null)
                            {
                                Throw(Text.TypeConvertionIllegal2, ThrowType.Error, Tokens[statement.Token].Place, fromType.ToString(), toType.ToString());
                            }
                            else config.ConvertionTypes.Add((fromType, toType, (ConvertionType)statement.Data[0]));
                            break;
                        }
                        else
                        {
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, ":", Tokens[node.EndToken].Match);
                        }
                        break;
                    }
            }
        }

        void AnalyzeTypeDeclaration<T>(Statement statement, ElementSymbol element, Dictionary<string, string> attributes) where T : TypeSymbol, new()
        {
            var name = ((string, Place))statement.Data[0];
            TypeSymbol symbol = new T { Name = name.Item1, Parent = element, Attributes = attributes ?? EmptyAttributes };

            if (name.Item1 == string.Empty)
            {
                symbol.Name = ElementAlternateTypeName;
                element.Alternate = symbol;
            }
            if (!SymbolNameValid(name.Item1, element))
            {
                Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
            }
            else
            {
                element.Children.Add(symbol);
            }
            List<Statement> stl;
            List<GenericType> localGenericTypes = null;
            if (statement.Data[1] is List<(string, Place)> generics)
            {
                stl = (List<Statement>)statement.Data[2];
                symbol.Generics.Capacity = generics.Count;
                localGenericTypes = new List<GenericType>(generics.Count);
                for (int i = 0; i < generics.Count; i++)
                {
                    if (!GlobalNameValid(generics[i].Item1))
                    {
                        Throw(Text.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                    }
                    localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                    symbol.Generics.Add(generics[i].Item1);
                }
                GenericsPush();
                GenericTypes.Capacity += localGenericTypes.Count;
                for (int i = 0; i < localGenericTypes.Count; i++)
                {
                    GenericTypes.Add(localGenericTypes[i]);
                }
            }
            else
            {
                stl = (List<Statement>)statement.Data[1];
            }
            CurrentSymbol = symbol;
            CurrentPendingVariables.Push(new List<PendingVariable>());
            CurrentPendingProperties.Push(new List<PendingProperty>());
            PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol, new ThisValue { Type = new NormalType { Base = symbol } }));
            for (int i = 0; i < stl.Count; i++)
            {
                AnalyzeTypeDeclarationStatement(stl[i], symbol, null);
            }
            CurrentPendingVariables.Pop();
            CurrentPendingProperties.Pop();
            if (symbol.Generics.Count != 0) GenericsPop();
            CurrentSymbol = element;
        }
        void AnalyzeVariableDeclaration(Statement statement, Symbol parent, Dictionary<string, string> attributes)
        {
            AnalyzeVariableAssignment((Statement)statement.Data[2], parent, (AccessorType)statement.Data[0], (AccessorType)statement.Data[1], attributes);
        }
        void AnalyzeVariableAssignment(Statement statement, Symbol parent, AccessorType getType, AccessorType setType, Dictionary<string, string> attributes, _Type parentType = null)
        {
            Node typeNode;
            Node valueNode;
            bool isFunc = false;

            if (statement.Data.Length == 1)
            {
                typeNode = null;
                valueNode = (Node)statement.Data[0];
            }
            else
            {
                typeNode = (Node)statement.Data[0];
                valueNode = (Node)statement.Data[1];
            }

            int nametoken = -1;

            if (valueNode.NodeType == NodeType.Assignment)
            {
                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.Private;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.Public;

                var nodes = ((Node, Node))valueNode.Data;
                valueNode = nodes.Item2;
                ThrowIfNotIdentifier(nodes.Item1);
                nametoken = nodes.Item1.Token;
            }
            else if (valueNode.NodeType == NodeType.FunctionDeclaration)
            {
                isFunc = true;

                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.None;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.None;

                FunctionDeclToAssignment(valueNode);
                var nodes = ((Node, Node))valueNode.Data;
                valueNode = nodes.Item2;
                ThrowIfNotIdentifier(nodes.Item1);
                nametoken = nodes.Item1.Token;
            }
            else if (valueNode.NodeType == NodeType.Identifier)
            {
                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.Private;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.Public;

                ThrowIfNotIdentifier(valueNode);

                nametoken = valueNode.Token;
                valueNode = null;
            }
            else
            {
                Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[valueNode.Token].Place, Tokens[valueNode.Token].Match);
                return;
            }

            if(!SymbolNameValid(Tokens[nametoken].Match, parent))
            {
                Throw(Text.MemberDefined2, ThrowType.Error, Tokens[nametoken].Place, Tokens[nametoken].Match, parent.ToString());
            }

            var s = new VariableSymbol { Name = Tokens[nametoken].Match, Parent = parent, Get = getType, Set = setType, Attributes = attributes ?? EmptyAttributes };
            parent.Children.Add(s);
            CurrentPendingVariables.Peek().Add(new PendingVariable { Symbol = s, TypeNode = typeNode, ValueNode = valueNode, IsFunction = isFunc });
        }
        void AnalyzeProperty(Statement statement, Symbol parent, Dictionary<string, string> attributes)
        {
            var getType = (AccessorType)statement.Data[0];
            var setType = (AccessorType)statement.Data[1];
            var typeNode = (Node)statement.Data[2];
            var name = ((string, Place))statement.Data[3];
            var valueNode = (Node)statement.Data[4];

            var prop = new PropertySymbol { Get = getType, Set = setType, Name = name.Item1, Parent = parent, Attributes = attributes ?? EmptyAttributes };
            var pendingProp = new PendingProperty { IsSetter = getType == AccessorType.None, TypeNode = typeNode, ValueNode = valueNode };
            var existingSymbol = parent.Children.Find((Symbol x) => x.Name == name.Item1);
            if (existingSymbol == null)
            {
                parent.Children.Add(prop);
                pendingProp.Symbol = prop;
            }
            else if (existingSymbol is PropertySymbol existingProp)
            {
                pendingProp.Symbol = existingProp;
                if (getType == AccessorType.None)
                {
                    if (existingProp.Get == AccessorType.None)
                    {
                        Throw(Text.PropertySetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
                    }
                    else
                    {
                        existingProp.Set = prop.Set;
                        existingProp.Setter = prop.Setter;
                    }
                }
                else if (getType == AccessorType.None)
                {
                    if (existingProp.Set == AccessorType.None)
                    {
                        Throw(Text.PropertyGetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
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
                Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, existingSymbol.ToString());
            }

            CurrentPendingProperties.Peek().Add(pendingProp);
        }

        #endregion
        #region procedure semantics

        bool IsAssignmentOrFuncDecl(Node n) => n.NodeType == NodeType.Assignment || n.NodeType == NodeType.FunctionDeclaration;
        void FunctionDeclToAssignment(Node n)
        {
            if (n.NodeType == NodeType.FunctionDeclaration)
            {
                n.NodeType = NodeType.Assignment;
                var data = ((Node, List<Node>, List<(Node, Node)>, Node))n.Data;
                n.Data = (data.Item1, new Node { NodeType = NodeType.TypedFunction, Data = (data.Item2, data.Item3, data.Item4), Token = n.Token, EndToken = n.EndToken });
            }
        }

        FunctionType TypedFunctionToFunctionType(Node node)
        {
            var data = ((List<Node>, List<(Node, Node)>, Node))node.Data;
            var functype = new FunctionType();
            if (data.Item1 != null)
            {
                GenericsPush();
                for (int i = 0; i < data.Item1.Count; i++)
                {
                    string name = Tokens[data.Item1[i].Token].Match;
                    ThrowIfNotIdentifier(data.Item1[i]);
                    if (!LocalNameValid(name))
                    {
                        Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                    }
                    GenericTypes.Add(new GenericType { Name = name });
                    functype.Generics.Add(name);
                }
            }
            for (int i = 0; i < data.Item2.Count; i++)
            {
                var nodes = data.Item2[i];
                string name;
                bool optional;
                if (nodes.Item2.NodeType == NodeType.Assignment)
                {
                    optional = true;
                    var namenode = (((Node, Node))nodes.Item2.Data).Item1;
                    name = Tokens[namenode.Token].Match;
                }
                else
                {
                    optional = false;
                    name = Tokens[nodes.Item2.Token].Match;
                }
                var localtype = NodeToType(nodes.Item1);
                functype.Parameters.Add((localtype, name, optional));
            }
            if (data.Item1 != null) GenericsPop();
            return functype;
        }

        class ProcedureInfo
        {
            public bool ReturnTypeDetermined;
            public _Type ReturnType;
            public Stack<List<(LocalValue, _Type)>> OldLocalTypes = new Stack<List<(LocalValue, _Type)>>();
            public bool LastWasIf;
            public bool? LastIfCondition;
            public List<string> Scopes = new List<string>();
        }

        List<ProcedureInfo> Procedures = new List<ProcedureInfo>();
        ProcedureInfo CurrentProcedure => Procedures[Procedures.Count - 1];

        ProceduralValue ScopeToProcedure(Statement statement)
        {
            Procedures.Add(new ProcedureInfo());
            LocalsPush();
            var value = new ProceduralValue { Instruction = StatementToInstruction(statement), Type = Procedures[Procedures.Count - 1].ReturnType };
            LocalsPop();
            Procedures.RemoveAt(Procedures.Count - 1);
            return value;
        }
        ProceduralValue ScopeToProcedure(Statement statement, _Type rettype)
        {
            Procedures.Add(new ProcedureInfo { ReturnTypeDetermined = true, ReturnType = rettype });
            LocalsPush();
            var value = new ProceduralValue { Instruction = StatementToInstruction(statement), Type = rettype };
            LocalsPop();
            Procedures.RemoveAt(Procedures.Count - 1);
            return value;
        }

        _Type CombineTypes(_Type x, _Type y)
        {
            if (x.Equals(y)) return x;
            HashSet<_Type> types = new HashSet<_Type>();
            if (x is MultiType mt) types.UnionWith(mt.Types);
            else types.Add(x);
            if (y is MultiType mt2) types.UnionWith(mt2.Types);
            else types.Add(y);
            return new MultiType { Types = types };
        }

        List<(LocalValue, _Type)> CreateLocalTypeList()
        {
            List<(LocalValue, _Type)> list = new List<(LocalValue, _Type)>();
            for (int i = 0; i < Locals.Count; i++)
            {
                list.Add((Locals[i], Locals[i].Type));
            }
            return list;
        }
        void PushLocalTypes()
        {
            CurrentProcedure.OldLocalTypes.Push(CreateLocalTypeList());
        }

        void PopLocalTypes()
        {
            var l = CurrentProcedure.OldLocalTypes.Pop();
            for (int i = 0; i < l.Count; i++)
            {
                if (l[i].Item1.Type != null)
                {
                    l[i].Item1.Type = CombineTypes(l[i].Item1.Type, l[i].Item2);
                }
            }
        }

        bool? ConditionIsConstant(Value cond)
        {
            if (cond == LiteralValue.True) return true;
            if (cond == LiteralValue.False) return false;
            if (cond is OperationValue op && op.OperationType == OperationType.Is && op.Values[0] is LocalValue local)
            {
                if (op.Types[0].Equals(local.Type)) return true;
                if (local.Type is MultiType && !(op.Types[0] is MultiType)) return null;
                return false;
            }
            return null;
        }

        Instruction StatementToInstruction(Statement statement)
        {
            Instruction makeInst(Instruction inst)
            {
                if (DebugMode)
                {
                    inst.File = Tokens[statement.Token].Place.File;
                    inst.Line = Tokens[statement.Token].Place.Line + (uint)1;
                    inst.Index = Tokens[statement.Token].Place.Index + (uint)1;
                }
                return inst;
            }
            if (CurrentProcedure.LastWasIf)
            {
                CurrentProcedure.LastWasIf = false;
                if (statement.StatementType == StatementType.Else)
                {
                    if (CurrentProcedure.LastIfCondition.HasValue)
                    {
                        if (CurrentProcedure.LastIfCondition.Value)
                        {
                            return makeInst(StatementToInstruction((Statement)statement.Data[0]));
                        }
                        return NoOpInstruction.Value;
                    }
                    //copy the local types before if
                    var l = CurrentProcedure.OldLocalTypes.Pop();
                    for (int i = 0; i < l.Count; i++)
                    {
                        l[i].Item1.Type = l[i].Item2;
                    }
                    for (int i = 0; i < Locals.Count; i++)
                    {
                        if (l.TrueForAll(x => x.Item1 != Locals[i])) Locals.RemoveAt(i);
                    }
                    PushLocalTypes();
                    var inst = makeInst(StatementToInstruction((Statement)statement.Data[0]));
                    PopLocalTypes();
                    return makeInst(new ElseInstruction { Instruction = inst });
                }
                PopLocalTypes();
            }

            switch (statement.StatementType)
            {
                case StatementType.None:return NoOpInstruction.Value;
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    return NoOpInstruction.Value;

                case StatementType.Node:
                    {
                        Node node = (Node)statement.Data[0];
                        switch (node.NodeType)
                        {
                            case NodeType.None: return NoOpInstruction.Value;
                            default:
                                {
                                    var value = NodeToValue(node, null);
                                    return makeInst(new ActionInstruction { Value = value });
                                }

                            case NodeType.Assignment:
                            case NodeType.FunctionDeclaration:
                                {
                                    FunctionDeclToAssignment(node);
                                    var nodes = ((Node, Node))node.Data;
                                    Value left = NodeToValue(nodes.Item1, null, checkCanGet: false, createLocals: true, setIndexer: true);
                                    Value right;
                                    if (left is LocalValue)
                                    {
                                        right = NodeToValue(nodes.Item2, null);
                                        if (right.Type == null)
                                        {
                                            Throw(Text.CannotAssignTyplessValue0, ThrowType.Error, Tokens[nodes.Item2.Token].Place);
                                            right = InvalidValue.UnknownType;
                                        }
                                        left.Type = right.Type;
                                    }
                                    else
                                    {
                                        if (left is IndexerSetTemporaryValue indexerSet)
                                        {
                                            var funcType = (FunctionType)indexerSet.Call.Function.Type;
                                            indexerSet.Call.Parameters.Add(NodeToValue(nodes.Item2, funcType.Parameters[funcType.Parameters.Count - 1].type));
                                            return makeInst(new ActionInstruction { Value = indexerSet.Call });
                                        }
                                        right = NodeToValue(nodes.Item2, left.Type);
                                        if (left.Type == null)
                                        {
                                            left.Type = right.Type;
                                        }
                                        if (!CanSet(left))
                                        {
                                            Throw(Text.ValueNotSettable1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, left.ToString());
                                        }
                                        if (right.Type == null)
                                        {
                                            Throw(Text.CannotAssignTyplessValue0, ThrowType.Error, Tokens[nodes.Item2.Token].Place);
                                            right = InvalidValue.UnknownType;
                                        }
                                    }
                                    return makeInst(new AssignInstruction { Left = left, Right = right });
                                }
                        }
                    }
                case StatementType.Scope:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        CurrentProcedure.Scopes.Add(name.Item1);
                        var stl = (List<Statement>)statement.Data[1];
                        if (stl.Count == 0) return makeInst(NoOpInstruction.Value);
                        var instl = new List<Instruction>(stl.Count);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            instl.Add(StatementToInstruction(stl[i]));
                        }
                        CurrentProcedure.Scopes.RemoveAt(CurrentProcedure.Scopes.Count - 1);
                        return makeInst(new ScopeInstruction { Instructions = instl, Name = name.Item1 });
                    }
                case StatementType.If:
                    {
                        var node = (Node)statement.Data[0];
                        var cond = NodeToValue(node, BooleanType.Value);
                        var x = ConditionIsConstant(cond);
                        PushLocalTypes();
                        var inst = StatementToInstruction((Statement)statement.Data[1]);
                        CurrentProcedure.LastWasIf = true;
                        if (x.HasValue)
                        {
                            if (x.Value)
                            {
                                Throw(Text.ConditionAlwaysTrue1, ThrowType.Warning, Tokens[node.Token].Place, cond.ToString());
                                return inst;
                            }
                            Throw(Text.ConditionAlwaysFalse1, ThrowType.Warning, Tokens[node.Token].Place, cond.ToString());
                            return NoOpInstruction.Value;
                        }
                        return makeInst(new IfInstruction { Condition = cond, Instruction = inst });
                    }
                case StatementType.Return:
                    {
                        var node = (Node)statement.Data[0];
                        if (node == null)
                        {
                            if (CurrentProcedure.ReturnTypeDetermined)
                            {
                                if (CurrentProcedure.ReturnType != null)
                                {
                                    Throw(Text.ValueExpected1, ThrowType.Error, Tokens[statement.Token + 1].Place, Tokens[statement.Token + 1].Match);
                                }
                            }
                            else
                            {
                                CurrentProcedure.ReturnTypeDetermined = true;
                                CurrentProcedure.ReturnType = null;
                            }
                            return makeInst(new ReturnInstruction());
                        }
                        else
                        {
                            var value = NodeToValue(node, CurrentProcedure.ReturnType);
                            if (CurrentProcedure.ReturnTypeDetermined)
                            {
                                if (CurrentProcedure.ReturnType == null)
                                {
                                    Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.Token].Place, ";", Tokens[node.Token].Match);
                                }
                            }
                            else
                            {
                                CurrentProcedure.ReturnTypeDetermined = true;
                                CurrentProcedure.ReturnType = value.Type;
                            }
                            return makeInst(new ReturnInstruction { Value = value });
                        }
                    }
                case StatementType.Break:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && !CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        return makeInst(new ControlInstruction { Name = name.Item1, Type = ControlInstructionType.Break });
                    }
                case StatementType.Continue:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && !CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        return makeInst(new ControlInstruction { Name = name.Item1, Type = ControlInstructionType.Continue });
                    }
                case StatementType.Throw:
                    {
                        string messageMatch = (string)statement.Data[1];
                        return makeInst(new ThrowInstruction { Exception = (((string, Place))statement.Data[0]).Item1, Message = messageMatch });
                    }
                case StatementType.Catch:
                    {
                        return makeInst(new CatchInstruction { Exceptions = ((List<(string, Place)>)statement.Data[0]).ConvertAll(x => x.Item1), Instruction = StatementToInstruction((Statement)statement.Data[1]) });
                    }
            }
        }

        #endregion
    }
}
