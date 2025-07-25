// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using AzureMcp.Areas.Aks.Commands.Cluster;
using AzureMcp.Areas.Aks.Models;
using AzureMcp.Areas.Aks.Services;
using AzureMcp.Models.Command;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AzureMcp.Tests.Areas.Aks.UnitTests.Cluster;

public class ClusterCreateCommandTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAksService _aksService;
    private readonly ILogger<ClusterCreateCommand> _logger;
    private readonly ClusterCreateCommand _command;
    private readonly CommandContext _context;
    private readonly Parser _parser;

    public ClusterCreateCommandTests()
    {
        _aksService = Substitute.For<IAksService>();
        _logger = Substitute.For<ILogger<ClusterCreateCommand>>();

        var collection = new ServiceCollection().AddSingleton(_aksService);
        _serviceProvider = collection.BuildServiceProvider();
        _command = new(_logger);
        _context = new(_serviceProvider);
        _parser = new(_command.GetCommand());
    }

    [Fact]
    public void Constructor_InitializesCommandCorrectly()
    {
        var command = _command.GetCommand();
        Assert.Equal("create", command.Name);
        Assert.NotNull(command.Description);
        Assert.NotEmpty(command.Description);
        Assert.Contains("Create a new Azure Kubernetes Service", command.Description);
    }

    [Fact]
    public void Command_HasRequiredOptions()
    {
        var command = _command.GetCommand();
        var options = command.Options.ToDictionary(opt => opt.Name, opt => opt);

        Assert.True(options.ContainsKey("cluster-name"));
        Assert.True(options.ContainsKey("location"));
        Assert.True(options.ContainsKey("resource-group"));
        Assert.True(options.ContainsKey("subscription"));
        
        // Verify required options
        Assert.True(options["cluster-name"].IsRequired);
        Assert.True(options["location"].IsRequired);
        Assert.True(options["resource-group"].IsRequired);
        Assert.True(options["subscription"].IsRequired);
    }

    [Fact]
    public void Command_HasOptionalOptions()
    {
        var command = _command.GetCommand();
        var options = command.Options.ToDictionary(opt => opt.Name, opt => opt);

        Assert.True(options.ContainsKey("node-count"));
        Assert.True(options.ContainsKey("node-vm-size"));
        Assert.True(options.ContainsKey("kubernetes-version"));
        Assert.True(options.ContainsKey("dns-prefix"));
        Assert.True(options.ContainsKey("generate-ssh-keys"));
        Assert.True(options.ContainsKey("ssh-key-value"));

        // Verify optional options
        Assert.False(options["node-count"].IsRequired);
        Assert.False(options["node-vm-size"].IsRequired);
        Assert.False(options["kubernetes-version"].IsRequired);
        Assert.False(options["dns-prefix"].IsRequired);
        Assert.False(options["generate-ssh-keys"].IsRequired);
        Assert.False(options["ssh-key-value"].IsRequired);
    }

    [Fact]
    public async Task Execute_WithValidParameters_CreatesCluster()
    {
        // Arrange
        var testCluster = new AzureMcp.Areas.Aks.Models.Cluster
        {
            Name = "test-cluster",
            Location = "eastus",
            ResourceGroupName = "test-rg",
            SubscriptionId = "test-subscription"
        };

        _aksService.CreateCluster(
            "test-sub",
            "test-cluster",
            "test-rg",
            "eastus",
            3,
            "Standard_DS2_v2",
            null,
            null,
            "azure",
            "cilium",
            "cilium",
            "overlay",
            null,
            null)
            .Returns(testCluster);

        var parseResult = _parser.Parse(
            "--subscription test-sub --resource-group test-rg --cluster-name test-cluster --location eastus");

        // Act
        var result = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(200, result.Status);
        Assert.NotNull(result.Results);

        await _aksService.Received(1).CreateCluster(
            "test-sub",
            "test-cluster", 
            "test-rg",
            "eastus",
            3, // default node count
            "Standard_DS2_v2", // default VM size
            null, // kubernetes version
            null, // dns prefix
            false, // generate ssh keys
            null, // ssh key value
            "azure", // network plugin
            "cilium", // network dataplane
            "cilium", // network policy
            "overlay", // network plugin mode
            null, // tenant
            Arg.Any<AzureMcp.Options.RetryPolicyOptions?>());
    }

    [Fact]
    public async Task Execute_WithCustomParameters_UsesProvidedValues()
    {
        // Arrange
        var testCluster = new AzureMcp.Areas.Aks.Models.Cluster
        {
            Name = "my-cluster",
            Location = "westus2",
            ResourceGroupName = "my-rg",
            SubscriptionId = "my-subscription"
        };

        _aksService.CreateCluster(
            "my-sub",
            "my-cluster",
            "my-rg",
            "westus2",
            5,
            "Standard_D4s_v3",
            "1.28.0",
            "my-dns",
            true,
            null,
            "azure",
            "cilium",
            "cilium",
            "overlay",
            null,
            null)
            .Returns(testCluster);

        var parseResult = _parser.Parse(
            "--subscription my-sub --resource-group my-rg --cluster-name my-cluster --location westus2 " +
            "--node-count 5 --node-vm-size Standard_D4s_v3 --kubernetes-version 1.28.0 " +
            "--dns-prefix my-dns --generate-ssh-keys true");

        // Act
        var result = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(200, result.Status);

        await _aksService.Received(1).CreateCluster(
            "my-sub",
            "my-cluster", 
            "my-rg",
            "westus2",
            5, // custom node count
            "Standard_D4s_v3", // custom VM size
            "1.28.0", // kubernetes version
            "my-dns", // dns prefix
            true, // generate ssh keys
            null, // ssh key value
            "azure", // network plugin
            "cilium", // network dataplane
            "cilium", // network policy
            "overlay", // network plugin mode
            null, // tenant
            Arg.Any<AzureMcp.Options.RetryPolicyOptions?>());
    }

    [Fact]
    public async Task Execute_WithMissingRequiredParameters_ReturnsError()
    {
        // Arrange
        var parseResult = _parser.Parse("--subscription test-sub"); // Missing required parameters

        // Act
        var result = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.NotEqual(200, result.Status);
        // Service should not be called when validation fails
    }

    [Fact]
    public async Task Execute_WhenServiceThrowsException_HandlesError()
    {
        // Arrange
        _aksService.CreateCluster(
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<int>(),    
            Arg.Any<string>(), 
            Arg.Any<string?>(), 
            Arg.Any<string?>(), 
            Arg.Any<bool>(),   
            Arg.Any<string?>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(), 
            Arg.Any<string?>(), 
            Arg.Any<AzureMcp.Options.RetryPolicyOptions?>())
            .Returns(Task.FromException<AzureMcp.Areas.Aks.Models.Cluster>(new Exception("Test exception")));

        var parseResult = _parser.Parse(
            "--subscription test-sub --resource-group test-rg --cluster-name test-cluster --location eastus");

        // Act
        var result = await _command.ExecuteAsync(_context, parseResult);

        // Assert
        Assert.Equal(500, result.Status);
        Assert.Contains("Test exception", result.Message);
    }

    [Theory]
    [InlineData(409, "A cluster with this name already exists")]
    [InlineData(403, "Authorization failed creating the AKS cluster")]
    [InlineData(400, "Invalid cluster configuration")]
    public void GetErrorMessage_WithAzureRequestFailedException_ReturnsSpecificMessage(int statusCode, string expectedMessage)
    {
        // Arrange
        var exception = new Azure.RequestFailedException(statusCode, "Test Azure error", "TestErrorCode", null);

        // Act
        var message = _command.GetType()
            .GetMethod("GetErrorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(_command, new object[] { exception }) as string;

        // Assert
        Assert.Contains(expectedMessage, message);
    }

    [Fact]
    public void Title_ReturnsExpectedValue()
    {
        Assert.Equal("Create AKS Cluster", _command.Title);
    }
}
