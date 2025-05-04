using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", """
                                       s = {not a; -123; #function() end}
                                       """), new ConsoleReporter());
        var b = p.ParseBlock();
        Console.WriteLine(Emitter.Emit(b));
    }
}