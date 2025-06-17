using AzureMcp.Models.Command;
using AzureMcp.Models.Option;
using AzureMcp.Options.Aks.Kubectl;
using AzureMcp.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Commands.Aks;

public sealed class KubectlCommand(ILogger<KubectlCommand> logger, int processTimeoutSeconds = 300) : GlobalCommand<KubectlOptions>
{
    private const string CommandTitle = "kubectl Command";
    private readonly ILogger<KubectlCommand> _logger = logger;
    private readonly int _timeoutSeconds = processTimeoutSeconds;
    private readonly Option<string> _commandOption = OptionDefinitions.Aks.Command;
    private readonly Option<string> _kubeConfigOption = OptionDefinitions.Aks.KubeConfig;

    public override string Name => "kubectl";

    public override string Description => "Execute kubectl commands against a cluster.";

    public override string Title => CommandTitle;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_commandOption);
        command.AddOption(_kubeConfigOption);
    }

    protected override KubectlOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.Command = parseResult.GetValueForOption(_commandOption);
        options.KubeConfig = parseResult.GetValueForOption(_kubeConfigOption);
        return options;
    }

    private static string? FindKubectlPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;
        foreach (var path in pathEnv.Split(Path.PathSeparator))
        {
            var full = Path.Combine(path.Trim(), "kubectl");
            if (File.Exists(full))
                return full;
        }
        return null;
    }

    [McpServerTool(Destructive = true, ReadOnly = false, Title = CommandTitle)]
    public override async Task<CommandResponse> ExecuteAsync(CommandContext context, ParseResult parseResult)
    {
        var options = BindOptions(parseResult);
        try
        {
            if (!Validate(parseResult.CommandResult, context.Response).IsValid)
            {
                return context.Response;
            }

            ArgumentNullException.ThrowIfNull(options.Command);
            ArgumentNullException.ThrowIfNull(options.KubeConfig);

            var processService = context.GetService<IExternalProcessService>();
            var kubectlPath = FindKubectlPath() ?? "kubectl";
            var args = $"--kubeconfig {options.KubeConfig} {options.Command}";
            var result = await processService.ExecuteAsync(kubectlPath, args, _timeoutSeconds);
            if (result.ExitCode != 0)
            {
                context.Response.Status = 500;
                context.Response.Message = result.Error;
            }

            var elem = processService.ParseJsonOutput(result);
            context.Response.Results = ResponseResult.Create(elem, JsonSourceGenerationContext.Default.JsonElement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing kubectl command.");
            HandleException(context.Response, ex);
        }
        return context.Response;
    }
}
