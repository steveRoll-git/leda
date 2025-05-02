using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", """
                                       while next(t) do
                                        print(123)
                                       end
                                       """), new ConsoleReporter());
        var b = p.ParseBlock();
        Console.WriteLine(Emitter.Emit(b));
    }
}