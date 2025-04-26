using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", "local a: number, b"), new ConsoleReporter());
        var b = (p.ParseBlock());
    }
}