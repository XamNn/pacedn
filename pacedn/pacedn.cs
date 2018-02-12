using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using pacednl;
using System.Xml;

//This is the general user interface, doesn't have function on it's own

namespace pacedn
{
    class Program
    {
        static void Main(string[] args)
        {
            Library l = new Library();
            var e = new ElementItem { Name = "x" };
            e.Children.Add("m", new ElementItem());
            l.Items.Add(e);
            l.Dependencies.Add(("test", true));
            l.SupportedTranslators.Add("hello");
            l.Save("testlib.pacelib");
            l = new Library();
            var y = l.Read("testlib.pacelib");
            Console.WriteLine(y.Message ?? string.Empty);

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                string[] parts = input.Split(new[] { ' ' }, 1);
                switch (parts[0].ToLower())
                {
                    default: continue;
                    case "?":
                    case "help":
                        Console.WriteLine("List of commands");
                        break;
                    case "merge":
                        Project = Project.Current.Merge();
                }
            }
        }
    }
}
