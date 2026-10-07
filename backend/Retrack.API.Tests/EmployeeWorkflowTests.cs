using Microsoft.EntityFrameworkCore;
using Retrack.API.Models;
using Retrack.API.DTOs.Employee;
using Retrack.API.Services.Depot;
using Retrack.API.Services.Employee;
using Xunit;

namespace Retrack.API.Tests;

public sealed partial class EmployeeCollectionTests
{
    private Task<EmployeeCollectionDto> Transition(string action, int revision = 1) =>
        Service.TransitionAsync(employee.Id, pickup.Id, action, revision, default);
    private async Task PrepareSubmission()
    {
        await CheckIn();
        await Save(0, new ClassificationItemInput("PET", 2, 10000));
    }
    [Fact]
    public async Task SubmitPersistsSnapshotMoneyAndNotificationWithIdempotentRetry()
    {
        await PrepareSubmission();
        var result = await Transition("SUBMITTED");
        Assert.Equal("WEIGHED", result.Status); Assert.False(result.CanEdit);
        Assert.Equal(20000m, pickup.GrossAmount); Assert.Equal(600m, pickup.PlatformFeeAmount); Assert.Equal(19400m, pickup.NetAmount);
        var audit = await db.EmployeeCollectionEvents.SingleAsync();
        Assert.Contains("PET", audit.SnapshotJson); Assert.Equal(employee.Id, audit.EmployeeId);
        var count = await db.Notifications.CountAsync();
        await Transition("SUBMITTED");
        Assert.Equal(count, await db.Notifications.CountAsync());
        Assert.Single(await db.EmployeeCollectionEvents.ToListAsync());
        await Assert.ThrowsAsync<DepotConflictException>(() => Save(1, new ClassificationItemInput("PET", 3, 10000)));
        Assert.Empty(await db.PlatformTransactions.ToListAsync());
    }
    [Fact]
    public async Task HandoverRequiresSellerDecisionAndNeverPaysOrCompletes()
    {
        await PrepareSubmission(); await Transition("SUBMITTED");
        await Assert.ThrowsAsync<DepotConflictException>(() => Transition("HANDED_OVER"));
        pickup.Status = "SELLER_CONFIRMED"; await db.SaveChangesAsync(); // Fixture mô phỏng API Seller, không triển khai thay role.
        var result = await Transition("HANDED_OVER");
        Assert.Equal("AWAITING_PAYMENT", result.Status);
        Assert.Null(pickup.PaymentProofUrl); Assert.Empty(await db.PlatformTransactions.ToListAsync());
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == depot.OwnerId && n.Title == "Đơn thu gom chờ thanh toán"));
        var count = await db.Notifications.CountAsync(); await Transition("HANDED_OVER");
        Assert.Equal(count, await db.Notifications.CountAsync());
    }
    [Fact]
    public async Task HandoverRejectsChangedAmountsAfterSellerConfirmation()
    {
        await PrepareSubmission(); await Transition("SUBMITTED");
        pickup.Status = "SELLER_CONFIRMED"; pickup.NetAmount++; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepotConflictException>(() => Transition("HANDED_OVER"));
        Assert.False(await db.EmployeeCollectionEvents.AnyAsync(e => e.Kind == "HANDED_OVER"));
    }
    [Fact]
    public async Task SellerReturnCanBeReopenedWithoutLosingProofOrPreviousSnapshot()
    {
        await PrepareSubmission(); await Transition("SUBMITTED");
        var original = (await db.EmployeeCollectionEvents.SingleAsync()).SnapshotJson;
        pickup.Status = "SCHEDULED"; await db.SaveChangesAsync();
        var result = await Transition("REOPENED"); Assert.True(result.CanEdit); Assert.Equal(2, result.Revision);
        await Transition("REOPENED"); Assert.Equal(2, (await db.PickupCheckIns.SingleAsync()).Revision);
        await Save(2, new ClassificationItemInput("PET", 3, 10000));
        await Transition("SUBMITTED", 3);
        Assert.Equal(original, (await db.EmployeeCollectionEvents.SingleAsync(e => e.Kind == "SUBMITTED" && e.Revision == 1)).SnapshotJson);
        Assert.Equal(1, images.UploadCount);
    }
    [Fact]
    public async Task WorkflowRejectsForeignUserEmptyDraftAndStaleRevision()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service.TransitionAsync(colleague.Id, pickup.Id, "SUBMITTED", 0, default));
        await CheckIn();
        await Assert.ThrowsAsync<DepotConflictException>(() => Transition("SUBMITTED", 0));
        await Save(0, new ClassificationItemInput("IRON", 1, 100));
        await Assert.ThrowsAsync<DepotConflictException>(() => Transition("SUBMITTED", 0));
        Assert.Empty(await db.EmployeeCollectionEvents.ToListAsync());
        Assert.Equal("IN_PROGRESS", pickup.Status);
    }
    [Fact]
    public async Task EmployeeReportsScopeHistoryAndNotificationReadToCurrentUser()
    {
        var reports = new EmployeeReportingService(db);
        db.Notifications.AddRange(new Notification { UserId = employee.Id, Title = "Của tôi" }, new Notification { UserId = colleague.Id, Title = "Người khác" });
        await db.SaveChangesAsync();
        var mine = await reports.NotificationsAsync(employee.Id, 1, 20, default);
        Assert.Single(mine.Items); Assert.Equal(1, mine.UnreadCount);
        await reports.MarkReadAsync(employee.Id, mine.Items[0].Id, default);
        await reports.MarkReadAsync(employee.Id, mine.Items[0].Id, default);
        Assert.Equal(0, (await reports.NotificationsAsync(employee.Id, 1, 20, default)).UnreadCount);
        var otherId = await db.Notifications.Where(n => n.UserId == colleague.Id).Select(n => n.Id).SingleAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => reports.MarkReadAsync(employee.Id, otherId, default));
        Assert.Single((await reports.HistoryAsync(employee.Id, 1, 20, "SCHEDULED", default)).Items);
        Assert.Empty((await reports.HistoryAsync(colleague.Id, 1, 20, null, default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => reports.HistoryAsync(employee.Id, 0, 20, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reports.HistoryAsync(employee.Id, 1, 100, null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reports.HistoryAsync(employee.Id, 1, 20, "INVALID", default));
    }
    [Fact]
    public async Task NotificationFailureRollsBackSubmissionMoneyStatusAndAudit()
    {
        await PrepareSubmission();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_notification BEFORE INSERT ON notifications BEGIN SELECT RAISE(ABORT, 'test notification failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Transition("SUBMITTED"));
        var stored = await db.PickupRequests.AsNoTracking().SingleAsync();
        Assert.Equal("IN_PROGRESS", stored.Status); Assert.Equal(0m, stored.GrossAmount);
        Assert.Empty(await db.EmployeeCollectionEvents.AsNoTracking().ToListAsync());
    }
    [Fact]
    public void VietnamDateBoundariesIncludeEarlyLocalMorningAndMonthRollover()
    {
        var utc = new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), EmployeeReportingService.VietnamDayStart(utc));
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), EmployeeReportingService.VietnamMonthStart(utc));
    }
}
