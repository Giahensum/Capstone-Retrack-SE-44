using Retrack.API.Models;
using Retrack.API.Services.Employee;
using Retrack.API.Services.Depot;
using Xunit;
namespace Retrack.API.Tests;
public sealed class PickupScheduleTests
{
    private static readonly DateOnly Day=new(2026,11,27);
    private static readonly DateTime Now=PickupSchedule.At(Day,12);
    [Theory]
    [InlineData(7)][InlineData(9)][InlineData(11)][InlineData(13)][InlineData(15)][InlineData(17)]
    public void AcceptsSixOfficialTwoHourSlots(int hour)=>PickupSchedule.Validate(Day,PickupSchedule.At(Day,hour),PickupSchedule.At(Day,hour+2),PickupSchedule.At(Day,6));
    [Fact] public void RejectsWrongDurationPastWindowAndMissingEnd(){
        Assert.Throws<ArgumentException>(()=>PickupSchedule.Validate(Day,PickupSchedule.At(Day,13),null,Now));
        Assert.Throws<ArgumentException>(()=>PickupSchedule.Validate(Day,PickupSchedule.At(Day,13),PickupSchedule.At(Day,16),Now));
        Assert.Throws<ArgumentException>(()=>PickupSchedule.Validate(Day,PickupSchedule.At(Day,9),PickupSchedule.At(Day,11),Now));
    }
    [Fact] public void CheckInIsBlockedBeforeAfterAndDuringOwnerReview(){
        var p=new PickupRequest {CollectionDate=Day,PreferredDatetime=PickupSchedule.At(Day,13),PreferredEndDatetime=PickupSchedule.At(Day,15)};
        Assert.Throws<DepotConflictException>(()=>PickupSchedule.RequireCheckIn(p,Now));
        PickupSchedule.RequireCheckIn(p,PickupSchedule.At(Day,14));
        Assert.Throws<DepotConflictException>(()=>PickupSchedule.RequireCheckIn(p,PickupSchedule.At(Day,15)));
        p.RequiresOwnerReview=true;
        Assert.Throws<DepotConflictException>(()=>PickupSchedule.RequireCheckIn(p,PickupSchedule.At(Day,14)));
    }
    [Fact] public void FlexibleDateHasBusinessHoursWithoutInventingEightOClockAppointment(){
        var p=new PickupRequest {CollectionDate=Day};
        Assert.Equal(PickupSchedule.At(Day,7),PickupSchedule.Start(p));Assert.Equal(PickupSchedule.At(Day,19),PickupSchedule.End(p));
    }
}
