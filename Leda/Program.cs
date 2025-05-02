using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", """
                                       do
                                       a
                                       local thing = 123
                                       end
                                       """), new ConsoleReporter());
        var b = p.ParseBlock();
        Console.WriteLine(Emitter.Emit(b));
    }
}