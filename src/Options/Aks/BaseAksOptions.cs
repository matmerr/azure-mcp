using System.Text.Json.Serialization;
using AzureMcp.Models.Option;
using AzureMcp.Options;

namespace AzureMcp.Options.Aks;

public class BaseAksOptions : SubscriptionOptions
{
    [JsonPropertyName(OptionDefinitions.Common.ResourceGroupName)]
    public new string? ResourceGroup { get; set; }
}
