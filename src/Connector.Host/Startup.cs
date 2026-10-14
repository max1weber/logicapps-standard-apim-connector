using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.Workflows.ServiceProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: WebJobsStartup(typeof(Connector.Host.Startup))]
namespace Connector.Host;

public sealed class Startup : IWebJobsStartup
{
    public void Configure(IWebJobsBuilder builder)
    {
        builder.AddExtension<Registration>();
        builder.Services.TryAddSingleton<ApimServiceProvider>();
    }
}

[Extension("ApimConnector", configurationSection: "ApimConnector")]
public sealed class Registration : IExtensionConfigProvider
{
    public Registration(ServiceOperationsProvider registry, ApimServiceProvider provider) =>
        registry.RegisterService(ApimServiceProvider.ServiceName, ApimServiceProvider.ServiceId, provider);
    public void Initialize(ExtensionConfigContext context) { }
}
