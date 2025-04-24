using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        Lexer l = new Lexer(new Source("", "ifaא4_ true \"a\\\nsd\\bf\" 123 +>=(-. .."));
        while (!l.ReachedEnd)
        {
            Console.WriteLine(l.ReadToken());
        }
    }
}