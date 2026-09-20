namespace Retrack.API.Models.Enums;

public enum PickupRequestStatus
{
    PENDING,
    SCHEDULED,
    IN_PROGRESS,
    WEIGHED,
    SELLER_CONFIRMED,
    AWAITING_PAYMENT,
    PAYMENT_SENT,
    DONE,
    CANCELLED
}
