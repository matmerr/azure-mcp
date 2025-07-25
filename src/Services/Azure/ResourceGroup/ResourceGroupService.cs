// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using Azure.ResourceManager.Resources;
using AzureMcp.Models.ResourceGroup;
using AzureMcp.Options;
using AzureMcp.Services.Azure.Subscription;
using AzureMcp.Services.Caching;

namespace AzureMcp.Services.Azure.ResourceGroup;

public class ResourceGroupService(ICacheService cacheService, ISubscriptionService subscriptionService)
    : BaseAzureService, IResourceGroupService
{
    private readonly ICacheService _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    private readonly ISubscriptionService _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
    private const string CacheGroup = "resourcegroup";
    private const string CacheKey = "resourcegroups";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromHours(1);

    public async Task<List<ResourceGroupInfo>> GetResourceGroups(string subscription, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription);

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
        var subscriptionId = subscriptionResource.Data.SubscriptionId;

        // Try to get from cache first
        var cacheKey = $"{CacheKey}_{subscriptionId}_{tenant ?? "default"}";
        var cachedResults = await _cacheService.GetAsync<List<ResourceGroupInfo>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults;
        }

        // If not in cache, fetch from Azure
        try
        {
            var resourceGroups = await subscriptionResource.GetResourceGroups()
                .GetAllAsync()
                .Select(rg => new ResourceGroupInfo(
                    rg.Data.Name,
                    rg.Data.Id.ToString(),
                    rg.Data.Location.ToString()))
                .ToListAsync();

            // Cache the results
            await _cacheService.SetAsync(CacheGroup, cacheKey, resourceGroups, s_cacheDuration);

            return resourceGroups;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving resource groups: {ex.Message}", ex);
        }
    }

    public async Task<ResourceGroupInfo?> GetResourceGroup(string subscription, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroupName);

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
        var subscriptionId = subscriptionResource.Data.SubscriptionId;

        // Try to get from cache first
        var cacheKey = $"{CacheKey}_{subscriptionId}_{tenant ?? "default"}";
        var cachedResults = await _cacheService.GetAsync<List<ResourceGroupInfo>>(CacheGroup, cacheKey, s_cacheDuration);
        if (cachedResults != null)
        {
            return cachedResults.FirstOrDefault(rg => rg.Name.Equals(resourceGroupName, StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            var rg = await GetResourceGroupResource(subscription, resourceGroupName, tenant, retryPolicy);
            if (rg == null)
            {
                return null;
            }

            return new ResourceGroupInfo(
                rg.Data.Name,
                rg.Data.Id.ToString(),
                rg.Data.Location.ToString());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving resource group {resourceGroupName}: {ex.Message}", ex);
        }
    }

    public async Task<ResourceGroupResource?> GetResourceGroupResource(string subscription, string resourceGroupName, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroupName);

        try
        {
            var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);
            var resourceGroupResponse = await subscriptionResource.GetResourceGroups()
                .GetAsync(resourceGroupName)
                .ConfigureAwait(false);

            return resourceGroupResponse?.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Resource group not found - return null instead of throwing
            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving resource group {resourceGroupName}: {ex.Message}", ex);
        }
    }

    public async Task<ResourceGroupInfo> CreateResourceGroup(string subscription, string resourceGroupName, string location, string? tenant = null, RetryPolicyOptions? retryPolicy = null)
    {
        ValidateRequiredParameters(subscription, resourceGroupName, location);

        var subscriptionResource = await _subscriptionService.GetSubscription(subscription, tenant, retryPolicy);

        try
        {
            var resourceGroupData = new ResourceGroupData(location);
            var operation = await subscriptionResource.GetResourceGroups()
                .CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData)
                .ConfigureAwait(false);

            var createdResourceGroup = operation.Value;
            
            // Invalidate cache since we created a new resource group
            var subscriptionId = subscriptionResource.Data.SubscriptionId;
            var cacheKey = $"{CacheKey}_{subscriptionId}_{tenant ?? "default"}";
            await _cacheService.DeleteAsync(CacheGroup, cacheKey);

            return new ResourceGroupInfo(
                createdResourceGroup.Data.Name,
                createdResourceGroup.Data.Id.ToString(),
                createdResourceGroup.Data.Location.ToString());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating resource group '{resourceGroupName}' in location '{location}': {ex.Message}", ex);
        }
    }
}
