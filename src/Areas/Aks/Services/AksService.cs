// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using AzureMcp.Areas.Aks.Models;
using AzureMcp.Options;
using AzureMcp.Services.Azure;
using AzureMcp.Services.Azure.ResourceGroup;
using AzureMcp.Services.Azure.Subscription;
using AzureMcp.Services.Azure.Tenant;
using AzureMcp.Services.Caching;

namespace AzureMcp.Areas.Aks.Services;

public sealed class AksService(
    ISubscriptionService subscriptionService,
    ITenantService tenantService,
    ICacheService cacheService,
    IResourceGroupService resourceGroupService) : BaseAzureService(tenantService), IAksService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly IResourceGroupService _resourceGroupService = resourceGroupService ?? throw new ArgumentNullException(nameof(resourceGroupService));

    private const string CacheGroup = "aks";
    private const string AksClustersCacheKey = "clusters";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<Cluster>> ListClusters(
        string subscription,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"{AksClustersCacheKey}_{subscription}"
            : $"{AksClustersCacheKey}_{subscription}_{tenant}";

        // Try to get from cache first
        var cachedClusters = await _cacheService.GetAsync<List<Cluster>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedClusters != null)
        {
            return cachedClusters;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
        var clusters = new List<Cluster>();

        try
        {
            await foreach (var cluster in subscriptionResource.GetContainerServiceManagedClustersAsync())
            {
                if (cluster?.Data != null)
                {
                    clusters.Add(ConvertToClusterModel(cluster));
                }
            }

            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, clusters, s_cacheDuration);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS clusters: {ex.Message}", ex);
        }

        return clusters;
    }

    public async Task<Cluster?> GetCluster(
        string subscription,
        string clusterName,
        string resourceGroup,
        string? tenant = null,
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, clusterName, resourceGroup);

        // Create cache key
        var cacheKey = string.IsNullOrEmpty(tenant)
            ? $"cluster_{subscription}_{resourceGroup}_{clusterName}"
            : $"cluster_{subscription}_{resourceGroup}_{clusterName}_{tenant}";

        // Try to get from cache first
        var cachedCluster = await _cacheService.GetAsync<Cluster>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedCluster != null)
        {
            return cachedCluster;
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        try
        {
            var resourceGroupResource = await subscriptionResource
                .GetResourceGroupAsync(resourceGroup);

            if (resourceGroupResource?.Value == null)
            {
                return null;
            }

            var clusterResource = await resourceGroupResource.Value
                .GetContainerServiceManagedClusters()
                .GetAsync(clusterName);

            if (clusterResource?.Value?.Data == null)
            {
                return null;
            }

            var cluster = ConvertToClusterModel(clusterResource.Value);

            // Cache the result
            await _cacheService.SetAsync(CacheGroup, cacheKey, cluster, s_cacheDuration);

            return cluster;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving AKS cluster '{clusterName}': {ex.Message}", ex);
        }
    }

    public async Task<Cluster> CreateCluster(
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
        RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, clusterName, resourceGroup, location);

        if (nodeCount < 1 || nodeCount > 1000)
        {
            throw new ArgumentException("Node count must be between 1 and 1000.", nameof(nodeCount));
        }

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        try
        {
            // Check if resource group exists, create it if it doesn't
            var existingResourceGroup = await _resourceGroupService.GetResourceGroup(
                subscriptionResource.Data.SubscriptionId!, 
                resourceGroup, 
                tenant, 
                retryPolicy);

            if (existingResourceGroup == null)
            {
                // Resource group doesn't exist, create it
                Console.WriteLine($"Resource group '{resourceGroup}' not found. Creating it in location '{location}'...");
                await _resourceGroupService.CreateResourceGroup(subscription, resourceGroup, location, tenant, retryPolicy);
                Console.WriteLine($"Resource group '{resourceGroup}' created successfully.");
                
                // Add a small delay to ensure the resource group is available
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            // Get the resource group (should exist now)
            var resourceGroupResource = await subscriptionResource
                .GetResourceGroupAsync(resourceGroup);

            if (resourceGroupResource?.Value == null)
            {
                throw new InvalidOperationException($"Resource group '{resourceGroup}' could not be found or created.");
            }

            // Build cluster data
            var clusterData = new ContainerServiceManagedClusterData(new Azure.Core.AzureLocation(location))
            {
                DnsPrefix = dnsPrefix ?? clusterName,
                KubernetesVersion = kubernetesVersion,
                Identity = new Azure.ResourceManager.Models.ManagedServiceIdentity(Azure.ResourceManager.Models.ManagedServiceIdentityType.SystemAssigned),
                EnableRbac = true,
                NetworkProfile = CreateNetworkProfile(networkPlugin, networkDataplane, networkPolicy, networkPluginMode, enableAcns),
                Sku = new ManagedClusterSku()
                {
                    Name = ManagedClusterSkuName.Base,
                    Tier = ManagedClusterSkuTier.Free
                },
                
            };

            // Add agent pool profile
            var agentPoolProfile = new ManagedClusterAgentPoolProfile("nodepool1")
            {
                Count = nodeCount,
                VmSize = nodeVmSize,
                OSType = ContainerServiceOSType.Linux,
                Mode = AgentPoolMode.System,
                EnableAutoScaling = false
                // Note: Not setting AvailabilityZones to ensure compatibility across all regions
            };

            clusterData.AgentPoolProfiles.Add(agentPoolProfile);

            // Start cluster creation
            var createOperation = await resourceGroupResource.Value
                .GetContainerServiceManagedClusters()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, clusterName, clusterData);

            if (createOperation?.Value?.Data == null)
            {
                throw new InvalidOperationException($"Failed to create AKS cluster '{clusterName}'.");
            }

            var cluster = ConvertToClusterModel(createOperation.Value);

            // Invalidate cache for this subscription to refresh cluster lists
            var listCacheKey = string.IsNullOrEmpty(tenant)
                ? $"{AksClustersCacheKey}_{subscription}"
                : $"{AksClustersCacheKey}_{subscription}_{tenant}";
            await _cacheService.DeleteAsync(CacheGroup, listCacheKey);

            return cluster;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating AKS cluster '{clusterName}': {ex.Message}", ex);
        }
    }

    private static Cluster ConvertToClusterModel(ContainerServiceManagedClusterResource clusterResource)
    {
        var data = clusterResource.Data;
        var agentPool = data.AgentPoolProfiles?.FirstOrDefault();

        return new Cluster
        {
            Name = data.Name,
            SubscriptionId = clusterResource.Id.SubscriptionId,
            ResourceGroupName = clusterResource.Id.ResourceGroupName,
            Location = data.Location.ToString(),
            KubernetesVersion = data.KubernetesVersion,
            ProvisioningState = data.ProvisioningState?.ToString(),
            PowerState = data.PowerStateCode?.ToString(),
            DnsPrefix = data.DnsPrefix,
            Fqdn = data.Fqdn,
            NodeCount = agentPool?.Count,
            NodeVmSize = agentPool?.VmSize,
            IdentityType = data.Identity?.ManagedServiceIdentityType.ToString(),
            EnableRbac = data.EnableRbac,
            NetworkPlugin = data.NetworkProfile?.NetworkPlugin?.ToString(),
            NetworkPolicy = data.NetworkProfile?.NetworkPolicy?.ToString(),
            ServiceCidr = data.NetworkProfile?.ServiceCidr,
            DnsServiceIP = data.NetworkProfile?.DnsServiceIP?.ToString(),
            SkuTier = data.Sku?.Tier?.ToString(),
            Tags = data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private static ContainerServiceNetworkProfile CreateNetworkProfile(
        string networkPlugin,
        string networkDataplane,
        string networkPolicy,
        string networkPluginMode,
        bool enableAcns)
    {
        var networkProfile = new ContainerServiceNetworkProfile()
        {
            NetworkPlugin = networkPlugin,
            NetworkDataplane = networkDataplane,
            NetworkPolicy = networkPolicy,
            NetworkPluginMode = networkPluginMode,
            ServiceCidr = "10.0.0.0/16",
            DnsServiceIP = "10.0.0.10",
            PodCidr = "10.244.0.0/16"
        };

        // Add ACNS configuration if enabled using serializedAdditionalRawData
        if (enableAcns)
        {
            // Create the Advanced Networking configuration according to Azure REST API spec
            var advancedNetworkingJson = """
            {
                "enabled": true,
                "observability": {
                    "enabled": true
                },
                "security": {
                    "enabled": true
                }
            }
            """;
            
            // Note: This approach uses reflection to access the private field
            // In a real implementation, you might need to use a different approach
            // or wait for official SDK support for ACNS
            var field = typeof(ContainerServiceNetworkProfile)
                .GetField("_serializedAdditionalRawData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                var additionalData = new Dictionary<string, BinaryData>
                {
                    ["advancedNetworking"] = BinaryData.FromString(advancedNetworkingJson)
                };
                field.SetValue(networkProfile, additionalData);
            }
        }

        return networkProfile;
    }
}
