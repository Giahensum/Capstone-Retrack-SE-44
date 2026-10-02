using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Retrack.API.Controllers.Depot;
using Xunit;

namespace Retrack.API.Tests;

public sealed class DepotPaymentSimulationControllerTests
{
    [Fact]
    public async Task Payos_simulation_endpoint_is_not_available_outside_development()
    {
        var controller = new DepotReportController(null!, new TestEnvironment("Production"));

        var result = await controller.SimulatePayment(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Retrack.API.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
