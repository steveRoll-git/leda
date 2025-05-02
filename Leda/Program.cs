using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", """
                                       for i, v in ipairs(list) do
                                       print(i)
                                       end
                                       """), new ConsoleReporter());
        var b = p.ParseBlock();
        Console.WriteLine(Emitter.Emit(b));
    }
}