// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using AzureMcp.Areas.Aks.Options;

namespace AzureMcp.Areas.Aks.Options.Cluster;

public class ClusterCreateOptions : BaseAksOptions
{
    [JsonPropertyName(AksOptionDefinitions.ClusterName)]
    public string? ClusterName { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NodeCount)]
    public int? NodeCount { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NodeVmSize)]
    public string? NodeVmSize { get; set; }

    [JsonPropertyName(AksOptionDefinitions.KubernetesVersion)]
    public string? KubernetesVersion { get; set; }

    [JsonPropertyName(AksOptionDefinitions.Location)]
    public string? Location { get; set; }

    [JsonPropertyName(AksOptionDefinitions.DnsPrefix)]
    public string? DnsPrefix { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NetworkPlugin)]
    public string? NetworkPlugin { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NetworkDataplane)]
    public string? NetworkDataplane { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NetworkPolicy)]
    public string? NetworkPolicy { get; set; }

    [JsonPropertyName(AksOptionDefinitions.NetworkPluginMode)]
    public string? NetworkPluginMode { get; set; }

    [JsonPropertyName(AksOptionDefinitions.EnableAcns)]
    public bool? EnableAcns { get; set; }
}
