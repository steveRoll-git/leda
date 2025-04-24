using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        Lexer l = new Lexer(new Source("", "ifaא4_ true \n+>=(-. .."));
        while (!l.ReachedEnd)
        {
            Console.WriteLine(l.ReadToken());
        }
    }
}