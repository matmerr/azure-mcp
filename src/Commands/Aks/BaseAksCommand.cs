using System.Diagnostics.CodeAnalysis;
using AzureMcp.Commands.Subscription;
using AzureMcp.Models.Option;
using AzureMcp.Options.Aks;

namespace AzureMcp.Commands.Aks;

public abstract class BaseAksCommand<
    [DynamicallyAccessedMembers(TrimAnnotations.CommandAnnotations)] TOptions>
    : SubscriptionCommand<TOptions> where TOptions : BaseAksOptions, new()
{
    protected new readonly Option<string> _resourceGroupOption = OptionDefinitions.Common.ResourceGroup;
    protected readonly Option<string> _clusterOption = OptionDefinitions.Aks.Cluster;

    // Allow derived classes to specify if resource group is required
    protected virtual bool RequiresResourceGroup => true;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);

        // Create a copy of the resource group option with the required setting from derived class
        var resourceGroupOption = new Option<string>(
            _resourceGroupOption.Name,
            _resourceGroupOption.Description!)
        {
            IsRequired = RequiresResourceGroup
        };
        command.AddOption(resourceGroupOption);

        // Only add cluster option for commands that need it (BaseClusterOptions or derived)
        if (typeof(TOptions).IsAssignableTo(typeof(Options.Aks.Cluster.BaseClusterOptions)))
        {
            command.AddOption(_clusterOption);
        }
    }

    protected override TOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);

        // Find the resource group option that was actually added to the command
        var resourceGroupOption = parseResult.CommandResult.Command.Options
            .FirstOrDefault(o => o.Name == _resourceGroupOption.Name) as Option<string>;

        if (resourceGroupOption != null)
        {
            options.ResourceGroup = parseResult.GetValueForOption(resourceGroupOption);
        }

        // Only bind cluster-specific options for commands that need them
        if (options is Options.Aks.Cluster.BaseClusterOptions clusterOptions)
        {
            clusterOptions.Cluster = parseResult.GetValueForOption(_clusterOption);
        }

        return options;
    }
}
