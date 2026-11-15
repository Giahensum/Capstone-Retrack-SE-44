namespace Retrack.API.Services.Interfaces;

public interface IVerificationDocumentStorage
{
    string Provider { get; }
    Task<string> UploadAsync(Guid operationId, Guid documentId, byte[] content, string contentType, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string key, int expectedSize, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
