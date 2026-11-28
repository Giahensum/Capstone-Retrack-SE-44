using Microsoft.EntityFrameworkCore;
using Retrack.API.Data;
using Retrack.API.Repositories.Interfaces;

namespace Retrack.API.Repositories;

public sealed class FactoryAttachmentRepository(AppDbContext db) : IFactoryAttachmentRepository
{
    public Task<bool> DepotMayReadBatchProofAsync(Guid actorId, Guid ownerId, string url, CancellationToken ct) =>
        db.InventoryBatches.AsNoTracking().AnyAsync(batch =>
            batch.Depot.OwnerId == actorId && batch.TargetFactory != null && batch.TargetFactory.OwnerId == ownerId &&
            (batch.ReturnHandoverProofUrl == url || (batch.QualityCheck != null &&
                (batch.QualityCheck.TicketImageUrl == url || batch.QualityCheck.InvoiceFileUrl == url || batch.QualityCheck.PaymentProofUrl == url))), ct);
}
