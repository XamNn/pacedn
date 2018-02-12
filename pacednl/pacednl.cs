using pacednl.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

//General pace library for c# which implements data structure for pace library files

namespace pacednl
{
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
        public static Project Current;

        public List<TranslatorProfile> TranslatorProfiles = new List<TranslatorProfile>();
        public List<Symbol> Symbols = new List<Symbol>();
        public List<Library> Libraries = new List<Library>();

        public Library Merge()
        {
            Libraries.Clear();
            var x = new Library();
            x.TranslatorProfiles = TranslatorProfiles;
            x.Symbols = Symbols;
            return x;
        }
    }

    public class Library
    {
        public string FileName;
        public string Name;
        public TranslatorProfile Common;
        public List<TranslatorProfile> TranslatorProfiles = new List<TranslatorProfile>();
        public List<Symbol> Symbols = new List<Symbol>();

        public void Save(string file)
        {
            XmlWriter xml = XmlWriter.Create(file);
            xml.WriteStartElement("PaceLibrary");
            xml.WriteStartElement("Symbols");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
            if (Common != null) Common.Write(xml);
            for (int i = 0; i < TranslatorProfiles.Count; i++)
            {
                TranslatorProfiles[i].Write(xml);
            }
            xml.WriteEndElement();
        }

        public OperationResult Read(string file)
        {
            XmlReader xml = XmlReader.Create(file);
            var x = new Library();
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
                                    case "Symbols":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element)
                                            {
                                                x.Symbols.Add(Symbol.ReadSymbol(xml));
                                            }
                                        }
                                        break;
                                    case "Profile":
                                        TranslatorProfiles.Add(TranslatorProfile.Read(xml, false));
                                        break;
                                    case "Common":
                                        x.Common = TranslatorProfile.Read(xml, true);
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                return new OperationResult("The following error occured when importing library: " + e.Message);
                            }
                        }
                    }
                }
            }
            return OperationResult.Success;
        }
    }

    public class TranslatorProfile
    {
        public string Name;
        public List<string> Dependencies = new List<string>();

        public void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Profile");
            xml.WriteAttributeString("Name", Name);
            for (int i = 0; i < Dependencies.Count; i++)
            {
                xml.WriteElementString("Dependency", Dependencies[i]);
            }
        }
        public static TranslatorProfile Read(XmlReader xml, bool unnamed)
        {
            var x = new TranslatorProfile();
            if (!unnamed) x.Name = xml.GetAttribute("Name");
            while (true)
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if(xml.NodeType == XmlNodeType.Element)
                {
                    switch (xml.LocalName)
                    {
                        case "Dependency":
                            x.Dependencies.Add(xml.Value);
                            break;
                    }
                }
            }
            return x;
        }
    }

    public abstract class Symbol
    {
        public string Name;

        public abstract void Write(XmlWriter xml);
        protected abstract void Read(XmlReader xml);

        public static Symbol ReadSymbol(XmlReader xml)
        {
            Symbol x = null;
            switch (xml.LocalName)
            {
                case "Element": x = new ElementSymbol(); break;
                case "Class": x = new ClassSymbol(); break;
                case "Struct": x = new StructSymbol(); break;
                case "Variable": x = new VariableSymbol(); break;
            }
            x.Read(xml);
            return x;
        }
    }
    public class ElementSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }

    }
    public class ClassSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }
    }
    public class StructSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }
    }
    public class VariableSymbol : Symbol
    {
        public bool Get;
        public bool Set;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Variable");
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Attribute)
                {
                    switch (xml.LocalName)
                    {
                        case "Get": Get = bool.Parse(xml.Value); break;
                        case "Set": Set = bool.Parse(xml.Value); break;
                    }
                }
            }
        }
    }

    abstract class Type
    {
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Type ReadType(XmlReader xml)
        {

        }
    }
    class NormalType : Type
    {
        public string Base;
        public bool Boxed;
        public bool RefType;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Normal");
            xml.WriteAttributeString("Base", Base);
            xml.WriteAttributeString("Boxed", Boxed.ToString());
            xml.WriteAttributeString("RefType", RefType.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            Boxed = bool.Parse(xml.GetAttribute("Boxed"));
            RefType = bool.Parse(xml.GetAttribute("RefType"));
        }
    }
    class FunctionType : Type
    {
        public Type ReturnType;
        public List<(Type, Value)> Parameters;
        public Procedure Procedure;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Function");
            ReturnType.Write(xml);
            Procedure.Write(xml);
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                Parameters[i].Item1.Write(xml);
                if (Parameters[i].Item2 != null) Parameters[i].Item2.Write(xml);
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
                                break;
                            }
                        case "Procedure":
                            Procedure = Procedure.Read(xml);
                            break;
                    }
                }
            }
        }
    }
    class RecordType : Type
    {
        public List<Type> Fields;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Record");
            for (int i = 0; i < Fields.Count; i++)
            {
                Fields[i].Write(xml);
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
                    Fields.Add(ReadType(xml));
                }
            }
        }
    }
    class ObjectType : Type
    {
        public static ObjectType Value = new ObjectType();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Object");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
        }
    }

    abstract class Value
    {
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Value ReadValue(XmlReader xml)
        {
            Value x = null;
            switch (xml.LocalName)
            {
                case "Local": x = new LocalValue(); break;
                case "Variable": x = new VariableValue(); break;
                case "Function": x = new FunctionValue(); break;
                case "Record": x = new RecordValue(); break;
                case "Member": x = new MemberValue(); break;
                case "Boxed": x = new BoxedValue(); break;
                case "Null": x = NullValue.Value; break;
            }
            x.Read(xml);
            return x;
        }

        public Type Type;
    }
    class LocalValue : Value
    {
        public int ID;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("ID", ID.ToString());
            Type.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            ID = int.Parse(xml.GetAttribute("ID"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Type = Type.ReadType(xml);
                }
            }
        }
    }
    class VariableValue : Value
    {
        public string Base;
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Variable");
            xml.WriteAttributeString("Base", Base);
            Type.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Type = Type.ReadType(xml);
                }
            }
        }
    }
    class FunctionValue : Value
    {
        public 
    }
    class RecordValue : Value
    {

    }
    class MemberValue : Value
    {

    }
    class BoxedValue : Value
    {

    }
    class NullValue : Value
    {
        public static NullValue Value = new NullValue();
    }

    class Procedure
    {
        public void Write(XmlWriter xml)
        {

        }
        public static Procedure Read(XmlReader xml)
        {

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
