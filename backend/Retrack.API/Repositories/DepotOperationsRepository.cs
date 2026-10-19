using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.DTOs;
using Retrack.API.DTOs.Depot;
using Retrack.API.Models;
using Retrack.API.Repositories.Interfaces;
namespace Retrack.API.Repositories;
public sealed class DepotOperationsRepository(AppDbContext db) : IDepotOperationsRepository
{
    private IQueryable<Notification> Notices(Guid owner, Guid depot) => db.Notifications.Where(x => x.UserId == owner &&
        (x.TransportJobId == null && x.PickupRequestId == null || db.TransportJobs.Any(t => t.Id == x.TransportJobId && t.Batch.DepotId == depot) ||
         db.PickupRequests.Any(p => p.Id == x.PickupRequestId && p.TargetDepotId == depot)));
    public Task<PagedResult<DepotNoticeDto>> NoticesAsync(Guid owner, Guid depot, DepotQuery query)
    {
        var rows = Notices(owner, depot).AsNoTracking();
        if (query.Status == "UNREAD") rows = rows.Where(x => !x.IsRead);
        if (query.Status == "READ") rows = rows.Where(x => x.IsRead);
        return DepotRepositoryPage.ReadAsync(rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new DepotNoticeDto(
            x.Id, x.Title, x.Message, x.IsRead, x.CreatedAt, db.TransportJobs.Where(t => t.Id == x.TransportJobId).Select(t => (Guid?)t.BatchId).FirstOrDefault(), x.PickupRequestId)), query);
    }
    public Task<int> UnreadAsync(Guid owner, Guid depot) => Notices(owner, depot).CountAsync(x => !x.IsRead);
    public async Task<bool> ReadAsync(Guid owner, Guid depot, Guid id) => await Notices(owner, depot).Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true)) > 0;
    public Task<PagedResult<DepotDriverEventDto>> EventsAsync(Guid depot, Guid batch, DepotQuery query) => DepotRepositoryPage.ReadAsync(db.DriverDeliveryEvents.AsNoTracking()
        .Where(x => x.Job.BatchId == batch && x.Job.Batch.DepotId == depot).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
        .Select(x => new DepotDriverEventDto(x.Id, x.Action, x.Driver.FullName, x.Driver.Phone, x.Reason, x.ImageUrl, x.CreatedAt,
            db.DepotIncidentFollowups.Where(f => f.EventId == x.Id).OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id).Select(f => f.Status).FirstOrDefault())), query);
    public Task<DriverDeliveryEvent?> FindEventAsync(Guid depot, Guid id) => db.DriverDeliveryEvents.SingleOrDefaultAsync(x => x.Id == id && x.Job.Batch.DepotId == depot);
    public Task<PagedResult<IncidentFollowupDto>> FollowupsAsync(Guid id, DepotQuery query) => DepotRepositoryPage.ReadAsync(db.DepotIncidentFollowups.AsNoTracking()
        .Where(x => x.EventId == id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new IncidentFollowupDto(x.Id, x.Status, x.Note, x.CreatedBy, x.CreatedAt)), query);
    public Task<DepotIncidentFollowup?> FindFollowupAsync(Guid id) => db.DepotIncidentFollowups.SingleOrDefaultAsync(x => x.Id == id);
    public void AddFollowup(DepotIncidentFollowup row) => db.DepotIncidentFollowups.Add(row);
}
