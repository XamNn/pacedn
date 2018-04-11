using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pace.CommonLibrary;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)
using _Type = Pace.CommonLibrary.Type;

namespace Pace.Translator
{
    public static class Info
    {
        public static string Version = "pacedntjs experimental 0.2.0 (not functional)";
    }
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
    public class Translator
    {
        SortedSet<Type> TypeSet;
        public void Translate(string Filename)
        {

        }
    }
}
