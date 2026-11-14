using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Retrack.API.Tests;

public sealed class SecurityDependencyTests
{
    [Fact]
    public void Signed_cms_round_trip_verifies_and_rejects_changed_content()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=RetRack test fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var cms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes("Nội dung kiểm thử")), detached: true);
        cms.ComputeSignature(new CmsSigner(cert));
        var encoded = cms.Encode();
        var verified = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes("Nội dung kiểm thử")), detached: true);
        verified.Decode(encoded);
        verified.CheckSignature(verifySignatureOnly: true);
        var changed = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes("Nội dung đã thay")), detached: true);
        changed.Decode(encoded);
        Assert.Throws<CryptographicException>(() => changed.CheckSignature(verifySignatureOnly: true));
    }

    [Fact]
    public async Task Native_sqlite_is_patched_and_can_execute_parameterized_queries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        var version = Version.Parse((string)(await command.ExecuteScalarAsync())!);
        Assert.True(version >= new Version(3, 50, 2), "SQLite native phải chứa bản vá CVE-2025-6965.");
        command.CommandText = "SELECT @value";
        command.Parameters.AddWithValue("@value", "Dữ liệu kiểm thử");
        Assert.Equal("Dữ liệu kiểm thử", await command.ExecuteScalarAsync());
    }
}
