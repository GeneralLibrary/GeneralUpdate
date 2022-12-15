using Colorful;
using CommandLine.Text;
using CommandLine;
using System.Drawing;
using Console = Colorful.Console;

namespace GeneralUpdate.PacketCommand
{
    internal class Program
    {
        private const string fontColor = "#FFF0F5";

        //font : http://www.figlet.org/examples.html
        static void Main(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            var font = FigletFont.Load("./Resources/slant.flf");
            var figlet = new Figlet(font);
            Console.WriteLine(figlet.ToAscii("GeneralUpdate"), ColorTranslator.FromHtml(fontColor));
            Console.WriteLine("Welcome to use GeneralUpdate PacketCommand tools.", ColorTranslator.FromHtml(fontColor));
            Console.WriteLine("©2022-2023 JusterZhu. All rights reserved.", ColorTranslator.FromHtml(fontColor));
        }
    }
}