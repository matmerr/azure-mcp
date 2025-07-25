// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureMcp.Areas.Aks.Models;
using AzureMcp.Options;

namespace AzureMcp.Areas.Aks.Services;

public interface IAksService
{
    Task<List<Cluster>> ListClusters(
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<Cluster?> GetCluster(
        string subscription,
        string clusterName,
        string resourceGroup,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);

    Task<Cluster> CreateCluster(
        string subscription,
        string clusterName,
        string resourceGroup,
        string location,
        int nodeCount = 3,
        string nodeVmSize = "Standard_DS2_v2",
        string? kubernetesVersion = null,
        string? dnsPrefix = null,
        string networkPlugin = "azure",
        string networkDataplane = "cilium",
        string networkPolicy = "cilium",
        string networkPluginMode = "overlay",
        bool enableAcns = true,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null);
}
