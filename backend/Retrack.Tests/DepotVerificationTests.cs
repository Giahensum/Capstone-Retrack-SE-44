using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Retrack.API.Data;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Retrack.API.Services.Shared;
using System.Collections.Concurrent;
using Xunit;
namespace Retrack.Tests;

public sealed class DepotVerificationTests : IAsyncLifetime
{
    private readonly Guid owner = Guid.NewGuid(), admin = Guid.NewGuid(), stranger = Guid.NewGuid(), depot = Guid.NewGuid();
    private static AppDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("RETRACK_TEST_CONNECTION")!;
        if (new NpgsqlConnectionStringBuilder(connection).Database != "Retrack_TV2_test") throw new InvalidOperationException("Sai DB test");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
    private static DepotService Scope(AppDbContext db) => new(new DepotOwnerRepository(db), new DepotPaymentReadRepository(db));
    private static DepotVerificationService Service(AppDbContext db, IVerificationFileScanner? scanner = null, IVerificationDocumentStorage? storage = null) =>
        new(new DepotVerificationRepository(db), new DepotUnitOfWork(db), Scope(db), scanner ?? new FixtureScanner(), storage ?? new DatabaseFixtureStorage(), NullLogger<DepotVerificationService>.Instance);
    private sealed class DatabaseFixtureStorage : IVerificationDocumentStorage
    {
        public string Provider => "DATABASE";
        public Task<string> UploadAsync(Guid operationId, Guid documentId, byte[] content, string type, CancellationToken ct) => throw new NotSupportedException();
        public Task<byte[]> ReadAsync(string key, int size, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class CloudFixtureStorage : IVerificationDocumentStorage
    {
        public string Provider => "CLOUDINARY";
        public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
        public ConcurrentBag<string> Deleted { get; } = [];
        public int Uploads, Reads;
        public int FailAt { get; set; }
        public Func<Task>? OnUpload { get; set; }
        public Func<Task>? OnRead { get; set; }
        public async Task<string> UploadAsync(Guid operationId, Guid documentId, byte[] content, string type, CancellationToken ct)
        {
            var count = Interlocked.Increment(ref Uploads);
            if (count == FailAt) throw new VerificationStorageUnavailableException();
            var key = $"retrack/private-depot-verifications/{operationId:N}/{documentId:N}.pdf";
            Objects[key] = content.ToArray();
            if (OnUpload != null) await OnUpload();
            return key;
        }
        public async Task<byte[]> ReadAsync(string key, int size, CancellationToken ct)
        { Interlocked.Increment(ref Reads); if (OnRead != null) await OnRead(); return Objects[key].ToArray(); }
        public Task DeleteAsync(string key, CancellationToken ct) { Objects.TryRemove(key, out _); Deleted.Add(key); return Task.CompletedTask; }
    }
    [Fact]
    public async Task Cloud_submission_persists_metadata_only_and_preserves_private_download_and_retry()
    {
        await using var db = Open(); var cloud = new CloudFixtureStorage();
        var service = Service(db, storage: cloud); await service.CreateDepotAsync(owner, Input());
        var id = await service.SubmitAsync(owner, depot, Guid.NewGuid(), "Hồ sơ cloud fixture", [File()], ["REGISTRATION"]);
        db.ChangeTracker.Clear();
        var record = await db.DepotVerificationDocuments.SingleAsync(x => x.SubmissionId == id);
        Assert.Empty(record.Content); Assert.Equal("CLOUDINARY", record.StorageProvider);
        Assert.NotNull(record.ContentSha256); Assert.True(record.ByteSize > 0);
        var item = Assert.Single((await service.ListAsync(owner, depot, new())).Items);
        Assert.Equal(record.ByteSize, Assert.Single(item.Files).Size);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(stranger, record.Id));
        Assert.Equal(0, cloud.Reads);
        var expected = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7\nTest fixture");
        Assert.Equal(expected, (await service.FileAsync(owner, record.Id)).Content);
        Assert.Equal(expected, (await service.FileAsync(admin, record.Id)).Content);
        cloud.FailAt = 2;
        Assert.Equal(id, await service.SubmitAsync(owner, depot, id, "Hồ sơ cloud fixture", [File()], ["REGISTRATION"]));
        Assert.Equal(1, cloud.Uploads); Assert.Empty(cloud.Deleted);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SubmitAsync(owner, depot, id, "Đổi nội dung", [File()], ["REGISTRATION"]));
    }
    [Fact]
    public async Task Partial_upload_failure_compensates_owned_assets_without_falling_back_to_database()
    {
        await using var db = Open(); var cloud = new CloudFixtureStorage { FailAt = 2 };
        var service = Service(db, storage: cloud); await service.CreateDepotAsync(owner, Input());
        await Assert.ThrowsAsync<VerificationStorageUnavailableException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File(), File()]));
        Assert.Empty(cloud.Objects); Assert.Single(cloud.Deleted);
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
        Assert.Equal("UNVERIFIED", (await db.Depots.SingleAsync(x => x.Id == depot)).VerificationStatus);
    }
    [Fact]
    public async Task Legacy_database_documents_remain_readable_and_replayable_after_cloud_is_enabled()
    {
        await using var db = Open(); var original = Service(db); await original.CreateDepotAsync(owner, Input());
        var id = await original.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]);
        await db.DepotVerificationDocuments.Where(x => x.SubmissionId == id).ExecuteUpdateAsync(set =>
            set.SetProperty(x => x.ByteSize, (int?)null).SetProperty(x => x.ContentSha256, (string?)null));
        db.ChangeTracker.Clear();
        var cloud = new CloudFixtureStorage(); var service = Service(db, storage: cloud);
        var item = Assert.Single((await service.ListAsync(owner, depot, new())).Items);
        var file = Assert.Single(item.Files); Assert.True(file.Size > 0);
        Assert.NotEmpty((await service.FileAsync(owner, file.Id)).Content);
        Assert.Equal(id, await service.SubmitAsync(owner, depot, id, null, [File()]));
        Assert.Equal(0, cloud.Reads); Assert.Equal(0, cloud.Uploads);
        Assert.Equal("DATABASE", (await db.DepotVerificationDocuments.SingleAsync(x => x.Id == file.Id)).StorageProvider);
    }
    [Fact]
    public async Task Pending_submission_conflict_cleans_only_new_assets_and_keeps_winner_readable()
    {
        await using var db = Open(); var cloud = new CloudFixtureStorage();
        var service = Service(db, storage: cloud); await service.CreateDepotAsync(owner, Input());
        var first = await service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]));
        Assert.Single(cloud.Objects); Assert.Single(cloud.Deleted);
        var document = await db.DepotVerificationDocuments.SingleAsync(x => x.SubmissionId == first);
        Assert.NotEmpty((await service.FileAsync(owner, document.Id)).Content);
    }
    [Fact]
    public async Task Concurrent_cloud_retries_have_one_submission_and_compensate_losing_upload()
    {
        await using (var setup = Open()) await Service(setup).CreateDepotAsync(owner, Input());
        var cloud = new CloudFixtureStorage(); var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        cloud.OnUpload = async () => { if (Interlocked.Increment(ref arrived) == 2) ready.SetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(10)); };
        var id = Guid.NewGuid();
        async Task<Guid> Submit() { await using var db = Open(); return await Service(db, storage: cloud).SubmitAsync(owner, depot, id, null, [File()]); }
        var results = await Task.WhenAll(Submit(), Submit());
        Assert.All(results, result => Assert.Equal(id, result)); Assert.Single(cloud.Objects); Assert.Single(cloud.Deleted);
        await using var verify = Open();
        Assert.Equal(1, await verify.DepotVerificationSubmissions.CountAsync(x => x.DepotId == depot));
        Assert.Equal(1, await verify.DepotVerificationDocuments.CountAsync(x => x.SubmissionId == id));
    }
    [Fact]
    public async Task Corrupted_cloud_body_and_rights_revoked_during_download_never_return_document()
    {
        await using var db = Open(); var cloud = new CloudFixtureStorage(); var service = Service(db, storage: cloud);
        await service.CreateDepotAsync(owner, Input());
        var id = await service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]);
        var document = await db.DepotVerificationDocuments.SingleAsync(x => x.SubmissionId == id);
        var original = cloud.Objects[document.StorageKey!].ToArray();
        cloud.Objects[document.StorageKey!][0] ^= 1;
        await Assert.ThrowsAsync<VerificationStorageUnavailableException>(() => service.FileAsync(owner, document.Id));
        cloud.Objects[document.StorageKey!] = original;
        cloud.OnRead = async () => { await using var change = Open(); await change.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false)); };
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(owner, document.Id));
    }
    private sealed class FixtureScanner : IVerificationFileScanner
    {
        public VerificationScanPolicy Policy => new(true, "Fixture", "Chỉ dùng trong test.");
        public Task<bool> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
    private sealed class ScriptedScanner(Func<Task<bool>> run) : IVerificationFileScanner
    {
        public int Calls { get; private set; }
        public VerificationScanPolicy Policy => new(true, "Fixture", "Chỉ dùng trong test.");
        public async Task<bool> ScanAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        { Calls++; cancellationToken.ThrowIfCancellationRequested(); return await run(); }
    }
    [Fact]
    public async Task Scan_happens_before_depot_lock_and_successful_retry_does_not_require_scanner()
    {
        await using var db = Open(); await Service(db).CreateDepotAsync(owner, Input());
        var unavailable = false;
        var scanner = new ScriptedScanner(async () =>
        {
            if (unavailable) throw new VerificationScannerUnavailableException();
            await using var probe = Open(); await using var tx = await probe.Database.BeginTransactionAsync();
            await probe.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM depots WHERE id = {depot} FOR UPDATE NOWAIT");
            await tx.RollbackAsync();
            return true;
        });
        var service = Service(db, scanner); var id = Guid.NewGuid();
        await service.SubmitAsync(owner, depot, id, null, [File()], ["REGISTRATION"]);
        unavailable = true;
        Assert.Equal(id, await service.SubmitAsync(owner, depot, id, null, [File()], ["REGISTRATION"]));
        Assert.Equal(1, scanner.Calls);
        db.ChangeTracker.Clear();
        var submission = Assert.Single((await service.ListAsync(owner, depot, new())).Items);
        var file = Assert.Single(submission.Files);
        Assert.Equal("CLEAN", file.ScanStatus); Assert.NotNull(file.ScannedAt);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Scanner_rejection_or_outage_leaves_no_partial_files_or_pending_submission(bool malware)
    {
        await using var db = Open(); await Service(db).CreateDepotAsync(owner, Input());
        var scanCalls = 0;
        var scanner = new ScriptedScanner(() =>
        {
            if (++scanCalls == 1) return Task.FromResult(true);
            return malware ? Task.FromException<bool>(new ArgumentException("Tệp không an toàn"))
                : Task.FromException<bool>(new VerificationScannerUnavailableException());
        });
        var service = Service(db, scanner);
        if (malware) await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File(), File()]));
        else await Assert.ThrowsAsync<VerificationScannerUnavailableException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File(), File()]));
        db.ChangeTracker.Clear();
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
        Assert.False(await db.DepotVerificationDocuments.AnyAsync(x => x.Submission.DepotId == depot));
        Assert.Equal("UNVERIFIED", (await db.Depots.SingleAsync(x => x.Id == depot)).VerificationStatus);
    }
    [Fact]
    public async Task Owner_disabled_while_scan_is_running_cannot_persist_new_submission()
    {
        await using var db = Open(); await Service(db).CreateDepotAsync(owner, Input());
        var scanner = new ScriptedScanner(async () =>
        {
            await using var change = Open();
            await change.Users.Where(x => x.Id == owner).ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false));
            return true;
        });
        await Assert.ThrowsAsync<DepotForbiddenException>(() =>
            Service(db, scanner).SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()], ["REGISTRATION"]));
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
        Assert.False(await db.DepotVerificationDocuments.AnyAsync(x => x.Submission.DepotId == depot));
    }
    private CreateDepotDto Input() => new() { OperationId = depot, Profile = new() { Name = "Kho xác minh thử", Address = "Ngũ Hành Sơn, Đà Nẵng", TaxCode = "TEST-ONLY" } };
    private static IFormFile File(string content = "%PDF-1.7\nTest fixture") => new FormFile(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), 0, System.Text.Encoding.UTF8.GetByteCount(content), "files", "ho-so.pdf");
    public async Task InitializeAsync()
    {
        await using var db = Open(); await TestDatabase.MigrateAsync(db);
        db.Users.AddRange(new User { Id = owner, Role = "DEPOT_OWNER", Email = $"verify-{owner}@test.invalid" },
            new User { Id = admin, Role = "ADMIN", Email = $"verify-{admin}@test.invalid" }, new User { Id = stranger, Role = "DEPOT_OWNER", Email = $"verify-{stranger}@test.invalid" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        await using var db = Open();
        await db.Depots.Where(x => x.Id == depot).ExecuteDeleteAsync();
        await db.Users.Where(x => x.Id == owner || x.Id == admin || x.Id == stranger).ExecuteDeleteAsync();
    }
    [Fact]
    public async Task Classified_documents_preserve_purpose_across_retry_review_and_private_download()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var photoBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
        IFormFile Photo() => new FormFile(new MemoryStream(photoBytes), 0, photoBytes.Length, "files", "mat-tien.png");
        var id = Guid.NewGuid();
        await service.SubmitAsync(owner, depot, id, "Đối chiếu kho", [File(), Photo()], ["REGISTRATION", "DEPOT_PHOTO"]);
        Assert.Equal(id, await service.SubmitAsync(owner, depot, id, "Đối chiếu kho", [File(), Photo()], ["REGISTRATION", "DEPOT_PHOTO"]));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SubmitAsync(owner, depot, id, "Đối chiếu kho", [File(), Photo()], ["REGISTRATION", "SUPPORTING"]));
        db.ChangeTracker.Clear();
        var submission = Assert.Single((await service.ListAsync(owner, depot, new())).Items);
        Assert.Equal(2, submission.Files.Count);
        var photo = Assert.Single(submission.Files, x => x.Kind == "DEPOT_PHOTO");
        Assert.Equal(photoBytes, (await service.FileAsync(admin, photo.Id)).Content);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(stranger, photo.Id));
        await service.ReviewAsync(admin, id, new(true, "Đã đối chiếu giấy tờ và ảnh"));
        Assert.Empty(db.ChangeTracker.Entries<DepotVerificationDocument>());
        Assert.Equal("APPROVED", (await db.Depots.SingleAsync(x => x.Id == depot)).VerificationStatus);
        Assert.Equal(2, await db.DepotVerificationDocuments.CountAsync(x => x.SubmissionId == id));
        (await db.Users.SingleAsync(x => x.Id == admin)).IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(admin, photo.Id));
        (await db.Users.SingleAsync(x => x.Id == owner)).IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(owner, photo.Id));
    }
    [Theory]
    [InlineData("DEPOT_PHOTO", null)]
    [InlineData("REGISTRATION", "UNKNOWN")]
    [InlineData("REGISTRATION", "DEPOT_PHOTO")]
    [InlineData("LEGACY", null)]
    public async Task Invalid_classification_or_pdf_as_warehouse_photo_never_creates_submission(string first, string? second)
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var kinds = second == null ? new[] { first } : new[] { first, second };
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null,
            second == null ? [File()] : [File(), File()], kinds));
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
    }
    [Fact]
    public async Task Disguised_extension_or_mismatched_classification_count_is_rejected()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var bytes = System.Text.Encoding.UTF8.GetBytes("%PDF-1.7\nTest");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "ho-so.html");
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [file]));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()], ["REGISTRATION", "SUPPORTING"]));
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
    }
    [Fact]
    public async Task Incorrect_stream_length_is_bounded_and_rejected_before_persistence()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var file = new MisdeclaredFile();
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [file]));
        Assert.InRange(file.BytesRead, 0, 6);
        Assert.False(await db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == depot));
    }
    private sealed class MisdeclaredFile : IFormFile
    {
        private readonly CountingStream stream = new(new byte[3 * 1024 * 1024]);
        public long BytesRead => stream.BytesRead;
        public string ContentType => "application/pdf";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public long Length => 5;
        public string Name => "files";
        public string FileName => "khai-bao-sai.pdf";
        public Stream OpenReadStream() => stream;
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class CountingStream(byte[] content) : MemoryStream(content)
    {
        public long BytesRead { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken); BytesRead += read; return read;
        }
    }
    [Fact]
    public async Task New_owner_creates_warehouse_with_retry_but_cannot_trade_or_claim_another_owners_operation()
    {
        await using var db = Open(); var service = Service(db);
        Assert.Empty(await Scope(db).GetDepotsAsync(owner));
        var created = await service.CreateDepotAsync(owner, Input());
        Assert.Equal("UNVERIFIED", created.VerificationStatus);
        Assert.Equal(created.Id, (await service.CreateDepotAsync(owner, Input())).Id);
        Assert.Single(await Scope(db).GetDepotsAsync(owner));
        await Assert.ThrowsAsync<DepotConflictException>(() => Scope(db).RequireTradingAsync(owner, depot));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.CreateDepotAsync(stranger, Input()));
    }
    [Fact]
    public async Task Private_files_require_owner_or_active_admin_and_invalid_uploads_do_not_create_pending_submissions()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, []));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File("<html>fake.pdf</html>")]));
        var id = Guid.NewGuid(); await service.SubmitAsync(owner, depot, id, "Hồ sơ test", [File()]);
        Assert.Equal(id, await service.SubmitAsync(owner, depot, id, "Hồ sơ test", [File()]));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SubmitAsync(owner, depot, id, "Hồ sơ test", [File("%PDF-Other")]));
        await Assert.ThrowsAsync<DepotConflictException>(() => service.SubmitAsync(owner, depot, Guid.NewGuid(), "Hồ sơ test", [File()]));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.ListAsync(stranger, depot, new()));
        var submission = Assert.Single((await service.ListAsync(owner, depot, new())).Items);
        var file = Assert.Single(submission.Files);
        Assert.Equal("application/pdf", (await service.FileAsync(owner, file.Id)).ContentType);
        Assert.Equal("application/pdf", (await service.FileAsync(admin, file.Id)).ContentType);
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.FileAsync(stranger, file.Id));
        await Assert.ThrowsAsync<DepotForbiddenException>(() => service.ReviewAsync(stranger, id, new(true, "Giả Admin")));
    }
    [Fact]
    public async Task Rejected_submission_can_be_resubmitted_and_approved_with_immutable_review_history()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var first = await service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]);
        await service.ReviewAsync(admin, first, new(false, "Thiếu thông tin đối soát"));
        await Assert.ThrowsAsync<DepotConflictException>(() => Scope(db).RequireTradingAsync(owner, depot));
        var second = await service.SubmitAsync(owner, depot, Guid.NewGuid(), "Bổ sung", [File()]);
        await service.ReviewAsync(admin, second, new(true, "Đã đối soát hồ sơ"));
        await service.ReviewAsync(admin, second, new(true, "Đã đối soát hồ sơ"));
        await Scope(db).RequireTradingAsync(owner, depot);
        await Assert.ThrowsAsync<DepotConflictException>(() => service.ReviewAsync(admin, first, new(true, "Sửa quyết định cũ")));
        db.ChangeTracker.Clear();
        Assert.Equal("APPROVED", (await Scope(db).GetProfileAsync(owner, depot)).VerificationStatus);
        Assert.Equal(2, (await service.ListAsync(owner, depot, new())).TotalCount);
        var notices = await db.Notifications.Where(x => x.UserId == owner && x.DepotId == depot).ToListAsync();
        Assert.Equal(2, notices.Count); Assert.All(notices, x => { Assert.Equal("VERIFICATION", x.Category); Assert.Equal("PROFILE", x.TargetType); });
    }
    [Fact]
    public async Task Identity_changes_are_locked_during_review_and_require_verification_again_after_approval()
    {
        await using var db = Open(); var service = Service(db); await service.CreateDepotAsync(owner, Input());
        var id = await service.SubmitAsync(owner, depot, Guid.NewGuid(), null, [File()]);
        var edit = Input().Profile; edit.Name = "Kho đổi tên";
        await Assert.ThrowsAsync<DepotConflictException>(() => Scope(db).UpdateProfileAsync(owner, depot, edit));
        await service.ReviewAsync(admin, id, new(true, "Đã đối soát"));
        await Scope(db).UpdateProfileAsync(owner, depot, edit);
        await Assert.ThrowsAsync<DepotConflictException>(() => Scope(db).RequireTradingAsync(owner, depot));
    }
    [Fact]
    public async Task Concurrent_admin_decisions_have_one_winner_and_do_not_overwrite_the_review()
    {
        await using (var db = Open()) { await Service(db).CreateDepotAsync(owner, Input()); await Service(db).SubmitAsync(owner, depot, depot, null, [File()]); }
        async Task<bool> Decide(bool approve)
        {
            await using var db = Open();
            try { await Service(db).ReviewAsync(admin, depot, new(approve, approve ? "Đủ hồ sơ" : "Thiếu hồ sơ")); return true; }
            catch (DepotConflictException) { return false; }
        }
        var results = await Task.WhenAll(Decide(true), Decide(false)); Assert.Single(results, x => x);
        await using var verify = Open();
        var submission = await verify.DepotVerificationSubmissions.SingleAsync(x => x.Id == depot);
        Assert.Equal(submission.Status, (await verify.Depots.SingleAsync(x => x.Id == depot)).VerificationStatus);
        Assert.Equal(admin, submission.ReviewedBy);
    }
}
