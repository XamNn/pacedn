using pacednl.Serializer;
using pacednl.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pacednl
{

    public static class Info
    {
        public static readonly string Name = "pacednl";
        public static readonly int MajorVersion = 1, MinorVersion = 0;
    }

    public class Project
    {
        public static Project Current;

        public List<string> Imported = new List<string>();
        public Dictionary<string, Item> Items = new Dictionary<string, Item>();
        public Item Match(string name)
        {
            try
            {
                var names = name.Split('.');
                Item Current = Items[names[0]];
                for (int i = 1; i < names.Length; i++)
                {
                    if (Current is Element e) Current = e.Children[names[i]];
                    else return null;
                }
                return Current;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }

    public class Library
    {
        public List<(string, bool)> Dependencies = new List<(string, bool)>();
        public List<string> SupportedTranslators = new List<string>();
        public List<Item> Items = new List<Item>();

        public Structure Serialize()
        {
            var x = new Structure();
            x.Fields.Add("paceVersion", $"{Info.Name} {Info.MajorVersion}.{Info.MinorVersion}");
            x.Fields.Add("buildDate", $"UTC DD.MM.YYYY {DateTime.UtcNow.ToString("dd:mm:yyyy")}");
            var sl = new Structure();
            x.Children.Add(sl);
            sl.Fields.Add("kind", "supportedTranslators");
            for (int i = 0; i < SupportedTranslators.Count; i++)
            {
                var y = new Structure();
                y.Fields.Add("translator", SupportedTranslators[i]);
                sl.Children.Add(y);
            }
            var ds = new Structure();
            x.Children.Add(ds);
            ds.Fields.Add("kind", "dependencies");
            for (int i = 0; i < Dependencies.Count; i++)
            {
                var y = new Structure();
                y.Fields.Add("file", Dependencies[i].Item1);
                y.Fields.Add("inUse", Dependencies[i].Item2.ToString());
                ds.Children.Add(y);
            }
            var il = new Structure();
            x.Children.Add(il);
            il.Fields.Add("kind", "items");
            for (int i = 0; i < Items.Count; i++)
            {
                il.Children.Add(Items[i].Serialize(Items[i].Name));
            }
            return x;
        }
    }

    public abstract class Item
    {
        public string Name;
        public abstract string Kind { get; }
        public virtual Structure Serialize(string localname)
        {
            var x = new Structure();
            x.Fields.Add("kind", Kind);
            x.Fields.Add("localName", Kind);
            x.Fields.Add("name", Name);
            return x;
        }
    }
    public abstract class Parent : Item
    {
        public Dictionary<string, Item> Children = new Dictionary<string, Item>();

        public override Structure Serialize(string localname)
        {
            var x = base.Serialize(localname);
            foreach (var y in Children)
            {
                x.Children.Add(y.Value.Serialize(y.Key));
            }
            return x;
        }
    }
    public class Element : Parent
    {
        public override string Kind => "element";
    }
    public class BaseType : Parent
    {
        public override string Kind => "type";

        public bool IsRefType = true;

        public override Structure Serialize(string localname)
        {
            var x = base.Serialize(localname);
            x.Fields.Add("isRefType", IsRefType.ToString());
            return x;
        }
    }
    public class Variable : Item
    {
        public override string Kind => "variable";

        public Type Type;
        public Value Value;
    }
    public class Alias : Item
    {
        public override string Kind => "alias";

        public Ref<Item> Target;
    }

    public class Type { }
    public class NormalType : Type
    {
        public static List<NormalType> All = new List<NormalType>();
        
        public NormalType Get(BaseType t, bool boxed) => Get(t, boxed, EmptyGenerics);
        public NormalType Get(BaseType t, bool boxed, List<Type> generics)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].BaseType == t && All[i].IsBoxed == boxed && Tools.ListEquals(All[i].Generics, generics)) return All[i];
            var x = new NormalType { BaseType = t, Generics = generics };
            All.Add(x);
            return x;
        }

        static List<Type> EmptyGenerics = new List<Type>();

        BaseType BaseType;
        List<Type> Generics = new List<Type>();
        public readonly bool IsBoxed, IsRefType;
    }
    public class FunctionType : Type
    {
        public static List<FunctionType> All = new List<FunctionType>();

        public FunctionType Get(Type returnType, List<(Type, Value)> parameters)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].ReturnType == returnType && Tools.ListEquals(All[i].Parameters, parameters)) return All[i];
            var x = new FunctionType { ReturnType = returnType, Parameters = parameters };
            All.Add(x);
            return x;
        }

        public Type ReturnType;
        public List<(Type, Value)> Parameters;
    }
    public class RecordType : Type
    {
        public static List<RecordType> All = new List<RecordType>();

        public RecordType Get(List<(Type, string)> fields)
        {
            for (int i = 0; i < All.Count; i++) if (Tools.ListEquals(All[i].Fields, fields)) return All[i];
            var x = new RecordType { Fields = fields };
            All.Add(x);
            return x;
        }

        public List<(Type, string)> Fields;
    }

    public class Value
    {
        public virtual Structure Serialize()
        {
            var x = new Structure();
            x.Fields.Add("kind", "value");
            return x;
        }
    }
    public class VariableValue : Value
    {
        public Ref<Variable> Variable;

        public override Structure Serialize()
        {
            var x = base.Serialize();
            x.Fields.Add("valueKind", "variable");
            return x;
        }
    }
    public class LocalValue : Value
    {
        public override Structure Serialize()
        {
            var x = base.Serialize();
            x.Fields.Add("valueKind", "local");
            return x;
        }
    }
    public class FunctionValue : Value
    {
        public FunctionType Type;
        public Procedure Procedure;
    }
    public class RecordValue : Value
    {
        public List<Value> Values;

        public override Structure Serialize()
        {
            var x = base.Serialize();
            x.Fields.Add("valueKind", "record");
            return x;
        }
    }
    public class CallValue : Value
    {
        public Value Parent;
        public List<Value> Parameters;

        public override Structure Serialize()
        {
            var x = base.Serialize();
            x.Fields.Add("valueKind", "call");
            return x;
        }
    }
    public class MemberValue : Value
    {
        public Value Parent;
        public Ref<Variable> Variable;

        public override Structure Serialize()
        {
            var x = base.Serialize();
            x.Fields.Add("valueKind", "member");
            x.Fields.Add("variable", Variable.Target.Name);
            x.Children.Add(Parent.Serialize());
            return x;
        }
    }

    public class Procedure
    {
        Type ReturnType;
        List<(Type, bool)> Parameters;
        List<Action> Actions;

        public Structure Serialize()
        {
            throw new NotImplementedException();
        }
    }
    public enum ActionType
    {
        Assign,
        Scope,
        Break,
        Continue,
        If,
        Else,
        Call,
    }
    public struct Action
    {
        public object Data;
    }

    public class Ref<T> where T : Item
    {
        string name;
        T item;

        public Item Target
        {
            get
            {
                if (item == null) item = Project.Current.Match(name) as T;
                return item;
            }
        }

        public Ref(string target)
        {
            name = target;
        }
        public Ref(T target)
        {
            item = target;
        }
    }
}
namespace pacednl.Serializer
{
    public class Structure
    {
        static string StartString = "{";
        static string EndString = "}";
        static string SeparatorString = "\n";
        static string KeyValueSeparatorString = ": ";

        public Dictionary<string, string> Fields = new Dictionary<string, string>();
        public List<Structure> Children = new List<Structure>();
        public void Write(StreamWriter s, int tabs = 0)
        {
            void WriteLine(string content)
            {
                StartLine();
                s.WriteLine(content);
            }
            void StartLine()
            {
                for (int i = 0; i < tabs; i++)
                {
                    s.Write('\t');
                }
            }

            WriteLine(StartString);
            tabs++;
            foreach (var x in Fields)
            {
                StartLine();
                s.Write(x.Key);
                s.Write(KeyValueSeparatorString);
                s.WriteLine(x.Value);
            }
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(s, tabs);
            }
            tabs--;
            WriteLine(EndString);
        }
        public static Structure Read(StreamReader s)
        {
            s.Read();
            s.Read();
            return ReadAfterStartString(s);
        }
        static Structure ReadAfterStartString(StreamReader s)
        {
            Structure x = new Structure();
            char c;
            while (true)
            {
                c = (char)s.Read();
                switch (c)
                {
                    case '\t':
                    case '\r':
                    case '\n':
                        continue;
                    case '{':
                        x.Children.Add(ReadAfterStartString(s));
                        break;
                    case '}':
                        return x;
                    case '#':
                        while ((char)s.Read() != '#') ;
                        break;
                    default:
                        StringBuilder sb = new StringBuilder();
                        while (true)
                        {
                            sb.Append(c);
                            c = (char)s.Read();
                            if (c == ':') break;
                        }
                        s.Read();
                        x.Fields.Add(sb.ToString(), s.ReadLine());
                        break;
                }
            }
        }
    }
}
namespace pacednl.Misc
{
    public static class Tools
    {
        public static bool ListEquals<T>(List<T> x, List<T> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
            {
                if (!ReferenceEquals(x[i], y[i])) return false;
            }
            return true;
        }
    }
}
