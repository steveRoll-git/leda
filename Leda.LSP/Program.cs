using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server;
using Leda.Lang;

namespace Leda.LSP;

internal static class Program
{
    private static async Task Main(string[] args)
    {
#if DEBUG
        if (args.Contains("--waitForDebugger"))
        {
            Debugger.Launch();
            while (!Debugger.IsAttached)
            {
                await Task.Delay(100);
            }
        }
#endif

        await new LedaServer().Run();
    }
}