using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        Lexer l = new Lexer(new Source("", "123.5.456"));
        Console.WriteLine(l.ReadToken());
        Console.WriteLine(l.ReadToken());
    }
}