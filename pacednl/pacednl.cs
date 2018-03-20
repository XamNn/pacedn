using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

//General pace library for c# which implements data structure for pace portable packages

namespace pacednl
{
    public static class Info
    {
        public static string Version = "pacednl beta-180318";
    }

    public static class Config
    {
        public static string LibraryDirectory = AppDomain.CurrentDomain.BaseDirectory + @"\libraries";
        public static string LibraryFileExtention = ".pacelib";

        public static string FormatLibraryFilename(string s, string localpath, bool checkexists)
        {
            if (File.Exists(s)) return s;
            if (localpath != null)
            {
                string x = localpath + "\\" + s + LibraryFileExtention;
                if (!checkexists || File.Exists(x)) return x;
            }
            return LibraryDirectory + "\\" + s + LibraryFileExtention;
        }
    }

    public struct OperationResult 
    {
        public bool IsSuccessful;
        public string Message;

        public static OperationResult Success = new OperationResult { IsSuccessful = true, Message = "Operation completed successfully." };
        public OperationResult(string message)
        {
            IsSuccessful = false;
            Message = message;
        }
    }

    public class Project
    {
        public static Project Current = new Project();

        public List<Symbol> Symbols = new List<Symbol>();
        public List<Library> Libraries = new List<Library>();
        public Dictionary<(Type, Type), Value> Convertions = new Dictionary<(Type, Type), Value>();
        public Procedure EntryPoint;

        public Library Merge()
        {
            Libraries.Clear();
            var x = new Library();
            Libraries.Add(x);
            x.Name = "MergedLibrary";
            x.Symbols = Symbols;
            return x;
        }
        public Symbol GetSymbol(string match)
        {
            var sl = match.Split('.');
            List<Symbol> l = Symbols;
            Symbol s = null;
            for (int i = 0; i < Symbols.Count; i++)
            {
                for (int i2 = 0; i2 < l.Count; i2++)
                {
                    if (l[i2].Name == sl[i])
                    {
                        s = l[i2];
                        l = s.Children;
                        break;
                    }
                }
            }
            return s;
        }
        public OperationResult Import(Library l, bool checkSymbols)
        {
            if(l.EntryPoint != null)
            {
                if (EntryPoint == null) EntryPoint = l.EntryPoint;
                else return new OperationResult("Entry point already defined");
            }
            if (checkSymbols)
            {
                for (int i = 0; i < Symbols.Count; i++)
                {
                    for (int i2 = 0; i2 < l.Symbols.Count; i2++)
                    {
                        if (Symbols[i].Name == l.Symbols[i2].Name)
                            return new OperationResult("Library contains symbols already defined");
                    }
                }
            }
            foreach (var x in Convertions)
            {
                if (Convertions.ContainsKey(x.Key))
                {
                    return new OperationResult("Library contains convertions already defined");
                }
            }
            Symbols.Capacity += l.Symbols.Count;
            Symbols.AddRange(l.Symbols);
            foreach(var x in l.Convertions)
            {
                Convertions.Add(x.Key, x.Value.Item1);
            }
            Libraries.Add(l);
            return OperationResult.Success;
        }
        public Project Clone()
        {
            Project p = new Project();
            for (int i = 0; i < Symbols.Count; i++) p.Symbols.Add(Symbols[i]);
            for (int i = 0; i < Libraries.Count; i++) p.Libraries.Add(Libraries[i]);
            return p;
        }
    }

    public enum ConvertionType
    {
        Implicit,
        Explicit,
        Automatic
    }
    public class Library
    {
        public string Name;
        public List<Symbol> Symbols = new List<Symbol>();
        public Dictionary<string, object> Aliases = new Dictionary<string, object>();
        public Dictionary<(Type, Type), (Value, ConvertionType)> Convertions = new Dictionary<(Type, Type), (Value, ConvertionType)>();
        public List<string> Dependencies = new List<string>();
        public Procedure EntryPoint;

        public void Save(string file)
        {
            if (!Directory.Exists(Config.LibraryDirectory)) Directory.CreateDirectory(Config.LibraryDirectory);
            XmlWriter xml = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
            xml.WriteStartElement("PaceLibrary");
            for (int i = 0; i < Dependencies.Count; i++)
            {
                xml.WriteStartElement("Dependency");
                xml.WriteAttributeString("Name", Dependencies[i]);
                xml.WriteEndElement();
            }
            if (EntryPoint != null)
            {
                EntryPoint.Write(xml);
            }
            if (Symbols.Count != 0)
            {
                xml.WriteStartElement("Symbols");
                for (int i = 0; i < Symbols.Count; i++)
                {
                    Symbols[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            if (Aliases.Count != 0)
            {
                xml.WriteStartElement("Aliases");
                foreach (var x in Aliases)
                {
                    xml.WriteStartElement("Alias");
                    xml.WriteAttributeString("Name", x.Key);
                    if (x.Value is Type t) t.Write(xml);
                    else if (x.Value is Value v) v.Write(xml);
                    else if (x.Value is Symbol s) xml.WriteAttributeString("Symbol", s.ToString());
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
            xml.Close();
        }
        
        public OperationResult Read(string file)
        {
            if (!File.Exists(file)) return new OperationResult("File does not exist");
            XmlReader xml = XmlReader.Create(file);
            Name = Path.GetFileNameWithoutExtension(file);
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Element)
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            try
                            {
                                switch (xml.LocalName)
                                {
                                    case "Dependency":
                                        Dependencies.Add(xml.GetAttribute("Name"));
                                        break;
                                    case "Procedure":
                                        EntryPoint = Procedure.Read(xml);
                                        break;
                                    case "Symbols":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element)
                                            {
                                                Symbols.Add(Symbol.ReadSymbol(xml, null));
                                            }
                                        }
                                        break;
                                    case "Aliases":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Alias")
                                            {
                                                string n = xml.GetAttribute("Name");
                                                object o = null;
                                                while (xml.Read())
                                                {
                                                    if (xml.NodeType == XmlNodeType.EndElement) break;
                                                    if (xml.NodeType == XmlNodeType.Element)
                                                    {
                                                        if (xml.LocalName == "Type")
                                                        {
                                                            o = Type.ReadType(xml);
                                                        }
                                                        else if (xml.LocalName == "Value")
                                                        {
                                                            o = Value.ReadValue(xml);
                                                        }
                                                        else o = xml.GetAttribute("Symbol");
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                return new OperationResult("Cannot import library: " + e.Message);
                            }
                        }
                    }
                }
            }
            xml.Close();
            return OperationResult.Success;
        }
    }

    public abstract class Symbol
    {
        public string Name;
        public string Parent;
        public abstract List<Symbol> Children { get; }

        public abstract void Write(XmlWriter xml);
        protected abstract void Read(XmlReader xml);

        public override string ToString()
        {
            if (Parent == null) return Name;
            return Parent + "." + Name;
        }

        public static Symbol ReadSymbol(XmlReader xml, string parent)
        {
            Symbol x = null;
            switch (xml.LocalName)
            {
                case "Element": x = new ElementSymbol(); break;
                case "Class": x = new ClassSymbol(); break;
                case "Struct": x = new StructSymbol(); break;
                case "Variable": x = new VariableSymbol(); break;
                case "Property": x = new PropertySymbol(); break;
            }
            x.Read(xml);
            x.Parent = parent;
            return x;
        }
    }
    public class ElementSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children => c;
        public Symbol Alternate;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            if(Alternate != null)
            {
                xml.WriteStartElement("Alternate");
                Alternate.Write(xml);
                xml.WriteEndElement();
            }
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if(xml.LocalName == "Alternate")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element) Alternate = ReadSymbol(xml, ToString());
                        }
                    }
                    Children.Add(ReadSymbol(xml, ToString()));
                }
            }
        }

    }
    public class ClassSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children => c;
        public int GenericCount;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            xml.WriteAttributeString("Name", Name);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Children.Add(ReadSymbol(xml, ToString()));
            }
        }
    }
    public class StructSymbol : Symbol
    {
        public List<Symbol> c;
        public override List<Symbol> Children => c;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            xml.WriteAttributeString("Name", Name);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Children.Add(ReadSymbol(xml, ToString()));
            }
        }
    }

    public enum AccessorType
    {
        None,
        Public,
        Private,
    }
    public class VariableSymbol : Symbol
    {
        public override List<Symbol> Children => null;

        public AccessorType Get;
        public AccessorType Set;
        public Type Type;
        public Value DefaultValue;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Variable");
            xml.WriteAttributeString("Name", Name);
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            Type.Write(xml);
            if (DefaultValue != null) DefaultValue.Write(xml);
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Get"));
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Set"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Type")
                    {
                        Type = Type.ReadType(xml);
                    }
                    else if (xml.LocalName == "Value")
                    {
                        DefaultValue = Value.ReadValue(xml);
                    }
                }
            }
        }
    }
    public class PropertySymbol : Symbol
    {
        public override List<Symbol> Children => null;

        public AccessorType Get;
        public AccessorType Set;
        public Procedure Getter;
        public Procedure Setter;
        public Type Type;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Property");
            xml.WriteAttributeString("Name", Name);
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            Type.Write(xml);
            if (Getter != null)
            {
                xml.WriteStartElement("Getter");
                Getter.Write(xml);
                xml.WriteEndElement();
            }
            if (Setter != null)
            {
                xml.WriteStartElement("Setter");
                Setter.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Get"));
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Set"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Type")
                    {
                        Type = Type.ReadType(xml);
                    }
                    else if (xml.LocalName == "Getter")
                    {
                        Getter = Procedure.Read(xml);
                    }
                    else if (xml.LocalName == "Setter")
                    {
                        Setter = Procedure.Read(xml);
                    }
                }
            }
        }
    }
    public abstract class Type
    {
        public abstract bool Equals(Type t);
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);
        public abstract bool IsRefType { get; }

        public static Type ReadType(XmlReader xml)
        {
            Type t = null;
            switch (xml.GetAttribute("Kind"))
            {
                case "Normal": t = new NormalType(); break;
                case "Function": t = new FunctionType(); break;
                case "Record": t = new RecordType(); break;
                case "Collection": t = new CollectionType(); break;
                case "Multi": t = new MultiType(); break;
                case "Object": t = new ObjectType(); break;
            }
            t.Read(xml);
            return t;
        }
    }
    public class NormalType : Type
    {
        public string Base;
        public bool Boxed;
        public bool RefType;
        public List<Type> Generics = new List<Type>();

        public override bool IsRefType => RefType;
        public override bool Equals(Type t)
        {
            return t is NormalType tt && Base == tt.Base && Boxed == tt.Boxed && RefType == tt.RefType;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Normal");
            xml.WriteAttributeString("Base", Base);
            xml.WriteAttributeString("Boxed", Boxed.ToString());
            xml.WriteAttributeString("RefType", RefType.ToString());
            if(Generics.Count != 0)
            {
                xml.WriteStartElement("Generics");
                for (int i = 0; i < Generics.Count; i++)
                {
                    Generics[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            Boxed = bool.Parse(xml.GetAttribute("Boxed"));
            RefType = bool.Parse(xml.GetAttribute("RefType"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Generics")
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            Generics.Add(ReadType(xml));
                        }
                    }
                }
            }
        }
        public override string ToString()
        {
            return Boxed ? Base + "?" : Base;
        }
    }
    public class FunctionType : Type
    {
        public Type ReturnType;
        public List<(Type, Value)> Parameters;

        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            return t is FunctionType tt && ReturnType.Equals(tt.ReturnType) && Tools.ListEquals(Parameters, tt.Parameters);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Function");
            if (ReturnType != null)
            {
                ReturnType.Write(xml);
            }
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                Parameters[i].Item1.Write(xml);
                if(Parameters[i].Item2 != null) Parameters[i].Item2.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    switch (xml.LocalName)
                    {
                        case "Type":
                            ReturnType.Read(xml);
                            break;
                        case "Parameter":
                            {
                                Type t = null;
                                Value v = null;
                                while (xml.Read())
                                {
                                    if (xml.NodeType == XmlNodeType.EndElement) break;
                                    if (xml.NodeType == XmlNodeType.Element)
                                    {
                                        if (xml.LocalName == "Type")
                                        {
                                            t = ReadType(xml);
                                        }
                                        else if(xml.LocalName == "Value")
                                        {
                                            v = Value.ReadValue(xml);
                                        }
                                    }
                                }
                                Parameters.Add((t, v));
                                break;
                            }
                    }
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("func(");
            if(Parameters.Count != 0)
            {
                sb.Append(Parameters[0].ToString());
                for (int i = 1; i < Parameters.Count; i++)
                {
                    sb.Append(", ");
                    sb.Append(Parameters[i].ToString());
                }
            }
            return sb.ToString();
        }
    }
    public class RecordType : Type
    {
        public HashSet<(string, Type)> Fields = new HashSet<(string, Type)>();

        public override bool IsRefType => false;
        public override bool Equals(Type t)
        {
            return t is RecordType tt && Fields.SetEquals(tt.Fields);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Record");
            foreach (var x in Fields)
            {
                xml.WriteStartElement("Field");
                xml.WriteAttributeString("Name", x.Item1);
                x.Item2.Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Field")
                {
                    string n = xml.GetAttribute("Name");
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                        {
                            Fields.Add((n, ReadType(xml)));
                            break;
                        }
                    }
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");
            bool notfirst = false;
            foreach (var x in Fields)
            {
                if (notfirst) sb.Append(", ");
                sb.Append(x.Item2.ToString());
                sb.Append(' ');
                sb.Append(x.Item1);
            }
            return sb.ToString();
        }
    }
    public class CollectionType : Type
    {
        public Type Base;

        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            return t is CollectionType tt && Base.Equals(tt.Base);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Collection");
            Base.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Base = ReadType(xml);
                }
            }
        }
        public override string ToString()
        {
            return Base + "[]";
        }
    }
    public class MultiType : Type
    {
        public HashSet<Type> Types = new HashSet<Type>();

        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            return t is MultiType tt && Types.SetEquals(tt.Types);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Multi");
            foreach (var x in Types)
            {
                x.Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Types.Add(ReadType(xml));
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("(");
            bool notfirst = false;
            foreach (var x in Types)
            {
                if (notfirst) sb.Append(", ");
                sb.Append(x.ToString());
                notfirst = true;
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
    public class GenericType : Type
    {
        public int Number;

        public override bool IsRefType => false;
        public override bool Equals(Type t)
        {
            return t is GenericType tt && tt.Number == Number;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Object");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
        }
        public override string ToString()
        {
            return "object";
        }
    }
    public class ObjectType : Type
    {
        public static ObjectType Value = new ObjectType();

        public override bool IsRefType => false;
        public override bool Equals(Type t)
        {
            return t == Value;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Object");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
        }
        public override string ToString()
        {
            return "object";
        }
    }

    public abstract class Value
    {
        protected Type _type;
        public abstract Type Type { get; set; }

        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Value ReadValue(XmlReader xml)
        {
            Value x = null;
            switch (xml.GetAttribute("Kind"))
            {
                case "Local": x = new LocalValue(); break;
                case "Symbol": x = new SymbolValue(); break;
                case "Call": x = new CallValue(); break;
                case "Action": x = new ActionValue(); break;
                case "Convert": x = new ConvertValue(); break;
                case "Function": x = new FunctionValue(); break;
                case "Record": x = new RecordValue(); break;
                case "Collection": x = new CollectionValue(); break;
                case "Member": x = new MemberValue(); break;
                case "Boxed": x = new BoxedValue(); break;
                case "Literal": x = new LiteralValue(); break;
                case "Null": x = NullValue.Value; break;
            }
            x.Read(xml);
            return x;
        }
    }
    public class LocalValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = ObjectType.Value;
                }
                return _type;
            }
            set => _type = value;
        }
        public string Name;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Local");
            xml.WriteAttributeString("Name", Name);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
        }
    }
    public class SymbolValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = ObjectType.Value;
                }
                return _type;
            }
            set => _type = value;
        }
        public string Base;
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Symbol");
            xml.WriteAttributeString("Base", Base);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
        }
    }
    public class CallValue : Value
    {
        public override Type Type
        {
            get => ((FunctionType)Function.Type).ReturnType;
            set { }
        }
        public Value Function;
        public List<Value> Parameters = new List<Value>();
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Call");
            Function.Write(xml);
            if (Parameters.Count != 0)
            {
                xml.WriteStartElement("Parameters");
                for (int i = 0; i < Parameters.Count; i++)
                {
                    Parameters[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if(xml.LocalName == "Value")
                    {
                        Function = ReadValue(xml);
                    }
                    else if(xml.LocalName == "Parameters")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Parameters.Add(ReadValue(xml));
                            }
                        }
                    }
                }
            }
        }
    }
    public enum ActionType
    {
        Is,
        IsNot,
        And,
        Or,
        Init,
        Clean,
    }
    public class ActionValue : Value
    {
        Type t;
        public override Type Type
        {
            get => t;
            set => t = value;
        }

        public ActionType ActionType;
        public List<Value> Values = new List<Value>();
        public List<Type> Types = new List<Type>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Action");
            xml.WriteAttributeString("Type", ActionType.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Type")
                    {
                        t = Type.ReadType(xml);
                    }
                    else if (xml.LocalName == "Values")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Values")
                            {
                                Values.Add(ReadValue(xml));
                            }
                        }
                    }
                    else if (xml.LocalName == "Types")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                            {
                                Types.Add(Type.ReadType(xml));
                            }
                        }
                    }
                }
            }
        }
    }
    public class ConvertValue : Value
    {
        public Type t;
        public override Type Type
        {
            get => t;
            set => t = value;
        }
        public Value Base;
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Convert");
            Base.Write(xml);
            Type.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Value")
                    {
                        Base = ReadValue(xml);
                    }
                    else if (xml.LocalName == "Type")
                    {
                        t = Type.ReadType(xml);
                    }
                }
            }
        }
    }
    public class FunctionValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    var l = new List<Type>();
                    for (int i = 0; i < Args.Count; i++)
                    {
                        l.Add(ObjectType.Value);
                    }
                }
                return _type;
            }
            set => _type = value;
        }
        public Procedure Procedure;
        public List<string> Args = new List<string>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Function");
            for (int i = 0; i < Args.Count; i++)
            {
                xml.WriteStartElement("Arg");
                xml.WriteAttributeString("Name", Args[i]);
                xml.WriteEndElement();
            }
            Procedure.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Arg")
                    {
                        Args.Add(xml.GetAttribute("Name"));
                    }
                    else if (xml.LocalName == "Procedure")
                    {
                        Procedure = Procedure.Read(xml);
                    }
                }
            }
        }
    }
    public class RecordValue : Value
    {
        public override Type Type
        {
            get
            {
                if(_type == null)
                {
                    var l = new HashSet<(string, Type)>();
                    for (int i = 0; i < Values.Count; i++)
                    {
                        l.Add((Values[i].Item1, Values[i].Item2.Type));
                    }
                    _type = new RecordType { Fields = l };
                }
                return _type;
            }
            set => _type = value;
        }
        public List<(string, Value)> Values = new List<(string, Value)>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Record");
            for (int i = 0; i < Values.Count; i++)
            {
                xml.WriteStartElement("Field");
                xml.WriteAttributeString("Name", Values[i].Item1);
                Values[i].Item2.Write(xml);
            }
        }
        public override void Read(XmlReader xml)
        {

        }
    }
    public class CollectionValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public List<Value> Values = new List<Value>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Collection");
            for (int i = 0; i < Values.Count; i++)
            {
                Values[i].Write(xml);
            }
            xml.WriteEndAttribute();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Values.Add(ReadValue(xml));
                }
            }
        }
    }
    public class MemberValue : Value
    {
        public override Type Type
        {
            get;
            set;
        }
        public Value Base;
        public string Name;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Member");
            xml.WriteAttributeString("Name", Name);
            Base.Write(xml);
            xml.WriteEndAttribute();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Base = ReadValue(xml);
                }
            }
        }
    }
    public class BoxedValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = new NormalType { Base = ((NormalType)Base.Type).Base, Boxed = true, RefType = true };
                }
                return _type;
            }
            set => _type = value;
        }
        public Value Base;

        public override void Write(XmlWriter xml)
        {
            xml.WriteAttributeString("Kind", "Boxed");
            Base.Write(xml);
            xml.WriteEndAttribute();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Base = ReadValue(xml);
                }
            }
        }
    }
    public enum LiteralValueType
    {
        String,
        Integer,
        Fractional,
        Boolean,
    }
    public class LiteralValue : Value
    {
        public static LiteralValue True  = new LiteralValue { LiteralType = LiteralValueType.Boolean, Value = "True" };
        public static LiteralValue False = new LiteralValue { LiteralType = LiteralValueType.Boolean, Value = "False" };

        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    switch (LiteralType)
                    {
                        case LiteralValueType.String: _type = StringLiteralType; break;
                        case LiteralValueType.Integer: _type = IntegerLiteralType; break;
                        case LiteralValueType.Fractional: _type = FractionalLiteralType; break;
                        case LiteralValueType.Boolean: _type = BooleanLiteralType; break;
                    }
                }
                return _type;
            }
            set => _type = value;
        }
        static Type StringLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("String", ObjectType.Value)); return t; })();
        static Type IntegerLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("Integer", ObjectType.Value)); return t; })();
        static Type FractionalLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("Numerator", ObjectType.Value)); t.Fields.Add(("Denominator", ObjectType.Value)); return t; })();
        static Type BooleanLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("Condition", ObjectType.Value)); return t; })();

        public LiteralValueType LiteralType;
        public string Value;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Literal");
            xml.WriteAttributeString("Value", Value);
            xml.WriteAttributeString("LiteralType", LiteralType.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Value = xml.GetAttribute("Value");
            LiteralType = (LiteralValueType)Enum.Parse(typeof(LiteralValueType), xml.GetAttribute("LiteralType"));
        }
    }
    public class NullValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = ObjectType.Value;
                }
                return _type;
            }
            set => _type = value;
        }
        public static NullValue Value = new NullValue();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Null");
        }
        public override void Read(XmlReader xml)
        {
        }
    }

    public enum InstructionType
    {
        No_op,
        Scope,
        Break,
        Continue,
        Operation,
        Assign,
        Throw,
        If,
        Else,
        Special,
    }
    public struct Instruction
    {
        public InstructionType Type;
        public object Data;
    }
    public class Procedure
    {
        public List<Instruction> Instructions = new List<Instruction>();

        public void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Procedure");
            for (int i = 0; i < Instructions.Count; i++)
            {
                WriteInstruction(Instructions[i], xml);
            }
            xml.WriteEndElement();
        }
        void WriteInstruction(Instruction i, XmlWriter xml)
        {
            switch (i.Type)
            {
                case InstructionType.Scope:
                    {
                        xml.WriteStartElement("Scope");
                        var data = ((string, List<Instruction>))i.Data;
                        xml.WriteAttributeString("ID", data.Item1);
                        for (int x = 0; x < data.Item2.Count; x++)
                        {
                            WriteInstruction(data.Item2[x], xml);
                        }
                        break;
                    }
                case InstructionType.Break:
                    {
                        xml.WriteStartElement("Break");
                        xml.WriteAttributeString("ID", (string)i.Data);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Continue:
                    {
                        xml.WriteStartElement("Continue");
                        xml.WriteAttributeString("ID", (string)i.Data);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Operation:
                    {
                        xml.WriteStartElement("Operation");
                        var data = (Value)i.Data;
                        data.Write(xml);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Assign:
                    {
                        xml.WriteStartElement("Assign");
                        var data = ((Value, Value))i.Data;
                        xml.WriteStartElement("Left");
                        data.Item1.Write(xml);
                        xml.WriteEndElement();
                        xml.WriteStartElement("Right");
                        data.Item2.Write(xml);
                        xml.WriteEndElement();
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Throw:
                    {
                        xml.WriteStartElement("Throw");
                        var data = ((string, string))i.Data;
                        xml.WriteAttributeString("Exception", data.Item1);
                        xml.WriteAttributeString("Message", data.Item2);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.If:
                    {
                        xml.WriteStartElement("If");
                        var data = ((Value, Instruction))i.Data;
                        data.Item1.Write(xml);
                        WriteInstruction(data.Item2, xml);
                        break;
                    }
                case InstructionType.Else:
                    {
                        xml.WriteStartElement("Else");
                        var data = (Instruction)i.Data;
                        WriteInstruction(data, xml);
                        break;
                    }
                case InstructionType.Special:
                    {
                        xml.WriteStartElement("Special");
                        var data = ((string, string))i.Data;
                        xml.WriteAttributeString("Translator", data.Item1);
                        xml.WriteAttributeString("Value", data.Item2);
                        xml.WriteEndElement();
                        break;
                    }
            }
        }

        public static Procedure Read(XmlReader xml)
        {
            var x = new Procedure();
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    x.Instructions.Add(ReadInstruction(xml));
                }
            }
            return x;
        }
        static Instruction ReadInstruction(XmlReader xml)
        {
            switch (xml.LocalName)
            {
                case "Scope":
                    {
                        string a = xml.GetAttribute("ID");
                        List<Instruction> b = new List<Instruction>();
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                b.Add(ReadInstruction(xml));
                            }
                        }
                        return new Instruction { Type = InstructionType.Scope, Data = (a, b) };
                    }
                case "Break":
                    {
                        string a = xml.GetAttribute("ID");
                        return new Instruction { Type = InstructionType.Break, Data = a };
                    }
                case "Continue":
                    {
                        string a = xml.GetAttribute("ID");
                        return new Instruction { Type = InstructionType.Continue, Data = a };
                    }
                case "Operation":
                    {
                        Value a = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                a =  Value.ReadValue(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.Operation, Data = a };
                    }
                case "Assign":
                    {
                        Value a = null;
                        Value b = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if(xml.LocalName == "Left")
                                {
                                    while (xml.Read())
                                    {
                                        if (xml.NodeType == XmlNodeType.EndElement) break;
                                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                                        {
                                            a = Value.ReadValue(xml);
                                        }
                                    }
                                }
                                else if(xml.LocalName == "Right")
                                {
                                    while (xml.Read())
                                    {
                                        if (xml.NodeType == XmlNodeType.EndElement) break;
                                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                                        {
                                            b = Value.ReadValue(xml);
                                        }
                                    }
                                }
                            }
                        }
                        return new Instruction { Type = InstructionType.Assign, Data = (a, b) };
                    }
                case "Throw":
                    {
                        string a = xml.GetAttribute("Exception");
                        string b = xml.GetAttribute("Message");
                        return new Instruction { Type = InstructionType.Scope, Data = (a, b) };
                    }
                case "If":
                    {
                        Value a = null;
                        Instruction b = new Instruction { Type = InstructionType.No_op, Data = null };
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if (xml.LocalName == "Value") a = Value.ReadValue(xml);
                                else b = ReadInstruction(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.If, Data = (a, b) };
                    }
                case "Else":
                    {
                        Value a = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                a = Value.ReadValue(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.Else, Data = a };
                    }
                case "Special":
                    {
                        string a = xml.GetAttribute("Translator");
                        string b = xml.GetAttribute("Value");
                        return new Instruction { Type = InstructionType.Special, Data = (a, b) };
                    }
            }
            return new Instruction { Type = InstructionType.No_op, Data = null };
        }
    }
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
