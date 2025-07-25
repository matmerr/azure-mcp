// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Aks.Models;
using AzureMcp.Areas.Aks.Options;
using AzureMcp.Areas.Aks.Options.Cluster;
using AzureMcp.Areas.Aks.Services;
using AzureMcp.Commands.Aks;
using AzureMcp.Services.Telemetry;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Areas.Aks.Commands.Cluster;

public sealed class ClusterCreateCommand(ILogger<ClusterCreateCommand> logger) : BaseAksCommand<ClusterCreateOptions>()
{
    private const string CommandTitle = "Create AKS Cluster";
    private readonly ILogger<ClusterCreateCommand> _logger = logger;

    // Define options from OptionDefinitions
    private readonly Option<string> _clusterNameOption = AksOptionDefinitions.Cluster;
    private readonly Option<int?> _nodeCountOption = AksOptionDefinitions.NodeCountOption;
    private readonly Option<string> _nodeVmSizeOption = AksOptionDefinitions.NodeVmSizeOption;
    private readonly Option<string> _kubernetesVersionOption = AksOptionDefinitions.KubernetesVersionOption;
    private readonly Option<string> _locationOption = AksOptionDefinitions.LocationOption;
    private readonly Option<string> _dnsPrefixOption = AksOptionDefinitions.DnsPrefixOption;
    private readonly Option<string> _networkPluginOption = AksOptionDefinitions.NetworkPluginOption;
    private readonly Option<string> _networkDataplaneOption = AksOptionDefinitions.NetworkDataplaneOption;
    private readonly Option<string> _networkPolicyOption = AksOptionDefinitions.NetworkPolicyOption;
    private readonly Option<string> _networkPluginModeOption = AksOptionDefinitions.NetworkPluginModeOption;
    private readonly Option<bool> _enableAcnsOption = AksOptionDefinitions.EnableAcnsOption;

    public override string Name => "create";

    public override string Description =>
        """
        Create a new Azure Kubernetes Service (AKS) cluster.
        Creates a managed Kubernetes cluster with system node pool and basic networking configuration.
        Before the cluster creation process starts, verify that the supplied resource group exists in the subscription.
        If the resource doesn't exist, prompt the user if they would like to create the resource group.
        
        Required options:
        - cluster-name: Name for the AKS cluster
        - location: Azure region where the cluster will be created
        - resource-group: Resource group for the cluster
        
        Optional configuration:
        - node-count: Number of agent nodes (default: 3)
        - node-vm-size: VM size for nodes (default: Standard_DS2_v2)
        - kubernetes-version: Kubernetes version to use
        - dns-prefix: DNS prefix for the cluster
        - network-plugin: Network plugin (azure, kubenet, none) - default: azure
        - network-dataplane: Network dataplane (azure, cilium) - default: cilium
        - network-policy: Network policy (azure, calico, cilium, none) - default: cilium
        - network-plugin-mode: Network plugin mode (overlay) - default: overlay
        - enable-acns: Enable Advanced Container Networking Services for observability and security features - default: true
        
        The cluster will be created with:
        - System-assigned managed identity
        - RBAC enabled
        - Azure CNI with Cilium networking
        - Free tier SKU
        """;

    public override string Title => CommandTitle;

    protected override void RegisterOptions(Command command)
    {
        base.RegisterOptions(command);
        command.AddOption(_resourceGroupOption);
        command.AddOption(_clusterNameOption);
        command.AddOption(_locationOption);
        command.AddOption(_nodeCountOption);
        command.AddOption(_nodeVmSizeOption);
        command.AddOption(_kubernetesVersionOption);
        command.AddOption(_dnsPrefixOption);
        command.AddOption(_networkPluginOption);
        command.AddOption(_networkDataplaneOption);
        command.AddOption(_networkPolicyOption);
        command.AddOption(_networkPluginModeOption);
        command.AddOption(_enableAcnsOption);
    }

    protected override ClusterCreateOptions BindOptions(ParseResult parseResult)
    {
        var options = base.BindOptions(parseResult);
        options.ResourceGroup = parseResult.GetValueForOption(_resourceGroupOption);
        options.ClusterName = parseResult.GetValueForOption(_clusterNameOption);
        options.Location = parseResult.GetValueForOption(_locationOption);
        options.NodeCount = parseResult.GetValueForOption(_nodeCountOption);
        options.NodeVmSize = parseResult.GetValueForOption(_nodeVmSizeOption);
        options.KubernetesVersion = parseResult.GetValueForOption(_kubernetesVersionOption);
        options.DnsPrefix = parseResult.GetValueForOption(_dnsPrefixOption);
        options.NetworkPlugin = parseResult.GetValueForOption(_networkPluginOption);
        options.NetworkDataplane = parseResult.GetValueForOption(_networkDataplaneOption);
        options.NetworkPolicy = parseResult.GetValueForOption(_networkPolicyOption);
        options.NetworkPluginMode = parseResult.GetValueForOption(_networkPluginModeOption);
        options.EnableAcns = parseResult.GetValueForOption(_enableAcnsOption);
        return options;
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

            context.Activity?.WithSubscriptionTag(options);

            var aksService = context.GetService<IAksService>();
            var cluster = await aksService.CreateCluster(
                options.Subscription!,
                options.ClusterName!,
                options.ResourceGroup!,
                options.Location!,
                options.NodeCount ?? 3,
                options.NodeVmSize ?? "Standard_DS2_v2",
                options.KubernetesVersion,
                options.DnsPrefix,
                options.NetworkPlugin ?? "azure",
                options.NetworkDataplane ?? "cilium",
                options.NetworkPolicy ?? "cilium",
                options.NetworkPluginMode ?? "overlay",
                options.EnableAcns ?? true,
                options.Tenant,
                options.RetryPolicy);

            context.Response.Results = ResponseResult.Create(
                new ClusterCreateCommandResult(cluster),
                AksJsonContext.Default.ClusterCreateCommandResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating AKS cluster. Subscription: {Subscription}, ResourceGroup: {ResourceGroup}, ClusterName: {ClusterName}, Location: {Location}, Options: {@Options}",
                options.Subscription, options.ResourceGroup, options.ClusterName, options.Location, options);
            HandleException(context, ex);
        }

        return context.Response;
    }

    protected override string GetErrorMessage(Exception ex) => ex switch
    {
        Azure.RequestFailedException reqEx when reqEx.Status == 409 =>
            "A cluster with this name already exists in the resource group.",
        Azure.RequestFailedException reqEx when reqEx.Status == 403 =>
            $"Authorization failed creating the AKS cluster. Details: {reqEx.Message}",
        Azure.RequestFailedException reqEx when reqEx.Status == 400 =>
            $"Invalid cluster configuration. Details: {reqEx.Message}",
        ArgumentException argEx =>
            $"Invalid argument: {argEx.Message}",
        InvalidOperationException invOpEx =>
            $"Invalid operation: {invOpEx.Message}",
        Azure.RequestFailedException reqEx => reqEx.Message,
        _ => base.GetErrorMessage(ex)
    };

    protected override int GetStatusCode(Exception ex) => ex switch
    {
        Azure.RequestFailedException reqEx => reqEx.Status,
        ArgumentException => 400,
        InvalidOperationException => 400,
        _ => base.GetStatusCode(ex)
    };

    internal record ClusterCreateCommandResult(AzureMcp.Areas.Aks.Models.Cluster Cluster);
}
