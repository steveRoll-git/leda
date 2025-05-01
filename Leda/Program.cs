using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", """
                                       if 1 + (2 == 3) and cool then
                                       print("wow")
                                       else
                                       return 1
                                       end
                                       """), new ConsoleReporter());
        var b = p.ParseBlock();
        Console.WriteLine(Emitter.Emit(b));
    }
}