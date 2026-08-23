using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SignatureHelp;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace Leda.LSP;

public class SignatureHelpHandler(LedaServer server) : SignatureHelpHandlerBase
{
    protected override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token)
    {
        var position = request.Position.ToLeda();

        var source = server.GetSourceByUri(request.TextDocument.Uri);
        // The source's syntax tree and types need to be up tp date for signature help to work properly.
        server.Project.Check(source);

        if (CallFinder.FindCall(source, position, server.Project.TypeEvaluator) is { } result)
        {
            var label = $"{result.FunctionName}(";

            List<ParameterInformation> parameters = [];
            for (var i = 0; i < result.Parameters.Count; i++)
            {
                if (i > 0)
                {
                    label += ", ";
                }

                var parameter = result.Parameters[i];

                parameters.Add(new()
                {
                    Label = ((uint)label.Length, (uint)(label.Length + parameter.Length)),
                });

                label += parameter;
            }

            label += ")";

            return Task.FromResult(new SignatureHelp
            {
                Signatures =
                [
                    new()
                    {
                        Label = label,
                        Parameters = parameters,
                        ActiveParameter = (uint)result.ArgumentIndex,
                    },
                ],
                ActiveSignature = 0,
            });
        }

        return Task.FromResult<SignatureHelp?>(null)!;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities)
    {
        serverCapabilities.SignatureHelpProvider = new()
        {
            TriggerCharacters = ["("],
        };
    }
}