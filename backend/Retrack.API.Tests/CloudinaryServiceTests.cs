using Microsoft.Extensions.Configuration;
using Retrack.API.Services.Shared;
using Xunit;

namespace Retrack.API.Tests;

public sealed class CloudinaryServiceTests
{
    [Fact]
    public async Task Missing_cloud_configuration_does_not_return_a_sample_image()
    {
        var service = new CloudinaryService(new ConfigurationBuilder().Build());
        await using var image = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadImageAsync(image, "batch.png"));
        Assert.Contains("ảnh", error.Message);
    }
}
