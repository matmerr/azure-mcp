// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace AzureMcp.Areas.Aks.Options;

public static class AksOptionDefinitions
{
    public const string ClusterName = "cluster-name";
    public const string NodeCount = "node-count";
    public const string NodeVmSize = "node-vm-size";
    public const string KubernetesVersion = "kubernetes-version";
    public const string Location = "location";
    public const string DnsPrefix = "dns-prefix";
    public const string NetworkPlugin = "network-plugin";
    public const string NetworkDataplane = "network-dataplane";
    public const string NetworkPolicy = "network-policy";
    public const string NetworkPluginMode = "network-plugin-mode";
    public const string EnableAcns = "enable-acns";

    public static readonly Option<string> Cluster = new(
        $"--{ClusterName}",
        "AKS Cluster name."
    )
    {
        IsRequired = true
    };

    public static readonly Option<int?> NodeCountOption = new(
        $"--{NodeCount}",
        "Number of agent nodes for the cluster. Must be between 1 and 1000."
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> NodeVmSizeOption = new(
        $"--{NodeVmSize}",
        "VM size for the agent pool nodes. Default: Standard_DS2_v2"
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> KubernetesVersionOption = new(
        $"--{KubernetesVersion}",
        "Version of Kubernetes to use for the cluster."
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> LocationOption = new(
        $"--{Location}",
        "Azure region where the cluster will be created."
    )
    {
        IsRequired = true
    };

    public static readonly Option<string> DnsPrefixOption = new(
        $"--{DnsPrefix}",
        "DNS prefix for the cluster. If not specified, defaults to cluster name."
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> NetworkPluginOption = new(
        $"--{NetworkPlugin}",
        "Network plugin to use for networking. Options: 'azure', 'kubenet', 'none'. Default: azure"
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> NetworkDataplaneOption = new(
        $"--{NetworkDataplane}",
        "Network dataplane to use. Options: 'azure', 'cilium'. Default: cilium"
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> NetworkPolicyOption = new(
        $"--{NetworkPolicy}",
        "Network policy to use. Options: 'azure', 'calico', 'cilium', 'none'. Default: cilium"
    )
    {
        IsRequired = false
    };

    public static readonly Option<string> NetworkPluginModeOption = new(
        $"--{NetworkPluginMode}",
        "Network plugin mode to use. Options: 'overlay', 'bridge'. Default: overlay"
    )
    {
        IsRequired = false
    };

    public static readonly Option<bool> EnableAcnsOption = new(
        $"--{EnableAcns}",
        "Enable Azure Container Networking Service (ACNS). Default: true"
    )
    {
        IsRequired = false
    };
}
