using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;

public sealed class DepotVerificationRepository(AppDbContext db) : IDepotVerificationRepository
{
    public async Task LockOwnerAsync(Guid id) => await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {id} FOR UPDATE").LoadAsync();
    public Task<Depot?> FindDepotAsync(Guid id) => db.Depots.SingleOrDefaultAsync(x => x.Id == id);
    public void AddDepot(Depot depot) => db.Depots.Add(depot);
    public async Task LockDepotAsync(Guid id)
    {
        var depot = await db.Depots.FromSqlInterpolated($"SELECT * FROM depots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (depot != null) await db.Entry(depot).ReloadAsync();
    }
    public Task<bool> ActiveAdminAsync(Guid id) => db.Users.AnyAsync(x => x.Id == id && x.Role == "ADMIN" && x.IsActive);
    public async Task<DepotVerificationSubmission?> FindAsync(Guid id)
    {
        var row = await db.DepotVerificationSubmissions.Include(x => x.Documents).SingleOrDefaultAsync(x => x.Id == id);
        if (row != null) await db.Entry(row).ReloadAsync();
        return row;
    }
    public Task<bool> HasPendingAsync(Guid id) => db.DepotVerificationSubmissions.AnyAsync(x => x.DepotId == id && x.Status == "SUBMITTED");
    public Task<int> RecentCountAsync(Guid id) => db.DepotVerificationSubmissions.CountAsync(x => x.DepotId == id && x.SubmittedAt > DateTime.UtcNow.AddDays(-1));
    public void Add(DepotVerificationSubmission submission) => db.DepotVerificationSubmissions.Add(submission);
    public void AddNotice(Notification notice) => db.Notifications.Add(notice);
    public Task<PagedResult<VerificationSubmissionDto>> ListAsync(Guid? depotId, DepotQuery query)
    {
        var source = db.DepotVerificationSubmissions.AsNoTracking().Where(x => depotId == null || x.DepotId == depotId);
        if (!string.IsNullOrEmpty(query.Status)) source = source.Where(x => x.Status == query.Status);
        return DepotRepositoryPage.ReadAsync(source.OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
            .Select(x => new VerificationSubmissionDto(x.Id, x.DepotId, x.DepotName, x.Status, x.Note, x.SubmittedAt,
                x.ReviewedAt, x.ReviewReason, x.Documents.OrderBy(d => d.Id).Select(d => new VerificationFileDto(d.Id, d.Name, d.ContentType, d.Content.Length)).ToList(), x.DepotAddress, x.TaxCode)), query);
    }
    public async Task<(Guid OwnerId, DepotVerificationDocument Document)?> FileAsync(Guid id)
    {
        var row = await db.DepotVerificationDocuments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { OwnerId = x.Submission.Depot.OwnerId, Document = x }).SingleOrDefaultAsync();
        return row == null ? null : (row.OwnerId, row.Document);
    }
}
