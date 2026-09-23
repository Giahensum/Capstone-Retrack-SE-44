using System;
using System.Collections.Generic;

namespace Retrack.FactoryClassDiagramForVisualParadigm
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Factory FactoryProfile { get; set; }
        public Depot DepotProfile { get; set; }
        public Driver DriverProfile { get; set; }
    }

    public class Factory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CompanyName { get; set; }
        public string TaxCode { get; set; }
        public string Address { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; }
        public ICollection<FactoryDemand> Demands { get; set; }
        public ICollection<BatchOrder> Orders { get; set; }
        public ICollection<Partnership> Partnerships { get; set; }
    }

    public class FactoryDemand
    {
        public Guid Id { get; set; }
        public Guid FactoryId { get; set; }
        public MaterialType MaterialType { get; set; }
        public decimal QuantityKg { get; set; }
        public decimal PricePerKg { get; set; }
        public DateTime Deadline { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public Factory Factory { get; set; }
    }

    public class Depot
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string CompanyName { get; set; }
        public string TaxCode { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string ContactPhone { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; }
        public ICollection<InventoryBatch> InventoryBatches { get; set; }
        public ICollection<Driver> Drivers { get; set; }
        public ICollection<Partnership> Partnerships { get; set; }
    }

    public class InventoryBatch
    {
        public Guid Id { get; set; }
        public Guid DepotId { get; set; }
        public string BatchCode { get; set; }
        public MaterialType MaterialType { get; set; }
        public decimal EstimatedWeightKg { get; set; }
        public decimal ActualWeightKg { get; set; }
        public decimal UnitPrice { get; set; }
        public string Description { get; set; }
        public string ThumbnailImageUrl { get; set; }
        public BatchStatus Status { get; set; }
        public TransportType TransportType { get; set; }
        public DateTime CreatedAt { get; set; }

        public Depot Depot { get; set; }
        public ICollection<BatchImage> Images { get; set; }
        public BatchOrder BatchOrder { get; set; }
    }

    public class BatchImage
    {
        public Guid Id { get; set; }
        public Guid BatchId { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public InventoryBatch Batch { get; set; }
    }

    public class BatchOrder
    {
        public Guid Id { get; set; }
        public Guid BatchId { get; set; }
        public Guid FactoryId { get; set; }
        public decimal AgreedPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public BatchStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public InventoryBatch Batch { get; set; }
        public Factory Factory { get; set; }
        public TransportJob TransportJob { get; set; }
        public WeightTicket WeightTicket { get; set; }
        public WeightVerification WeightVerification { get; set; }
        public Invoice Invoice { get; set; }
        public EprCertificate EprCertificate { get; set; }
    }

    public class Driver
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid DepotId { get; set; }
        public string LicenseNumber { get; set; }
        public string VehiclePlate { get; set; }
        public string VehicleType { get; set; }
        public decimal MaxCapacityKg { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }

        public User User { get; set; }
        public Depot Depot { get; set; }
        public ICollection<TransportJob> TransportJobs { get; set; }
    }

    public class TransportJob
    {
        public Guid Id { get; set; }
        public Guid BatchOrderId { get; set; }
        public Guid DriverId { get; set; }
        public string PickupAddress { get; set; }
        public string DeliveryAddress { get; set; }
        public decimal TransportFee { get; set; }
        public TransportStatus Status { get; set; }
        public DateTime PickupTime { get; set; }
        public DateTime DeliveredTime { get; set; }
        public DateTime CreatedAt { get; set; }

        public BatchOrder BatchOrder { get; set; }
        public Driver Driver { get; set; }
        public ICollection<TransportTrackingLog> TrackingLogs { get; set; }
    }

    public class TransportTrackingLog
    {
        public Guid Id { get; set; }
        public Guid TransportJobId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }

        public TransportJob TransportJob { get; set; }
    }

    public class WeightTicket
    {
        public Guid Id { get; set; }
        public Guid BatchOrderId { get; set; }
        public string TicketNumber { get; set; }
        public decimal GrossWeightKg { get; set; }
        public decimal TareWeightKg { get; set; }
        public decimal NetWeightKg { get; set; }
        public string TicketImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public BatchOrder BatchOrder { get; set; }
    }

    public class WeightVerification
    {
        public Guid Id { get; set; }
        public Guid BatchOrderId { get; set; }
        public decimal DepotWeightKg { get; set; }
        public decimal FactoryWeightKg { get; set; }
        public decimal DifferencePercentage { get; set; }
        public bool IsVerified { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }

        public BatchOrder BatchOrder { get; set; }
    }

    public class Invoice
    {
        public Guid Id { get; set; }
        public Guid BatchOrderId { get; set; }
        public string InvoiceNumber { get; set; }
        public string InvoiceFileUrl { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public InvoiceStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public BatchOrder BatchOrder { get; set; }
    }

    public class Partnership
    {
        public Guid Id { get; set; }
        public Guid DepotId { get; set; }
        public Guid FactoryId { get; set; }
        public PartnershipStatus Status { get; set; }
        public decimal Rating { get; set; }
        public DateTime CreatedAt { get; set; }

        public Depot Depot { get; set; }
        public Factory Factory { get; set; }
    }

    public class EprCertificate
    {
        public Guid Id { get; set; }
        public Guid BatchOrderId { get; set; }
        public string CertificateCode { get; set; }
        public string HashValue { get; set; }
        public MaterialType MaterialType { get; set; }
        public decimal CertifiedWeightKg { get; set; }
        public DateTime IssuedAt { get; set; }

        public BatchOrder BatchOrder { get; set; }
    }

    public class FactoryDemandController
    {
        public void CreateDemand() { }
        public void UpdateDemand() { }
        public void DeleteDemand() { }
        public void GetMyDemands() { }
    }

    public class FactoryMarketController
    {
        public void BrowseMarketplace() { }
        public void AcceptListedBatch() { }
        public void ApproveDirectRequest() { }
        public void RejectDirectRequest() { }
    }

    public class FactoryQCController
    {
        public void ConfirmReceiving() { }
        public void CreateWeightTicket() { }
        public void VerifyWeight() { }
        public void SubmitQualityDecision() { }
    }

    public class FactoryOrderController
    {
        public void GetAcceptedOrders() { }
        public void TrackTransport() { }
        public void SettleOrder() { }
        public void UploadInvoice() { }
    }

    public class FactoryPartnerController
    {
        public void GetPartners() { }
        public void RateDepot() { }
        public void BlockDepot() { }
        public void UnblockDepot() { }
    }

    public interface IFactoryMarketService
    {
        void BrowseListedBatches();
        void AcceptBatch(Guid batchId);
        void ReviewDirectRequest(Guid orderId, bool approve);
    }

    public interface IQCService
    {
        void ConfirmReceiving(Guid batchOrderId);
        void CreateWeightTicket(Guid batchOrderId);
        void VerifyWeight(Guid batchOrderId);
        void AcceptOrRejectBatch(Guid batchOrderId);
    }

    public interface IPaymentService
    {
        void CalculateSettlement(Guid batchOrderId);
        void SettleFactoryPayment(Guid batchOrderId);
        void UploadInvoice(Guid batchOrderId);
    }

    public enum UserRole
    {
        ADMIN,
        DEPOT_OWNER,
        DEPOT_EMPLOYEE,
        FACTORY,
        DRIVER,
        SELLER
    }

    public enum MaterialType
    {
        PET,
        HDPE,
        PVC,
        PAPER,
        CARDBOARD,
        ALUMINUM,
        IRON,
        STEEL,
        COPPER,
        ELECTRONIC_WASTE,
        OTHER
    }

    public enum BatchStatus
    {
        DRAFT,
        LISTED,
        ACCEPTED,
        READY_FOR_PICKUP,
        IN_PROGRESS,
        DELIVERED,
        VERIFIED,
        REJECTED,
        CANCELLED
    }

    public enum TransportStatus
    {
        PENDING,
        ASSIGNED,
        PICKED_UP,
        ON_THE_WAY,
        DELIVERED,
        CANCELLED
    }

    public enum PartnershipStatus
    {
        PENDING,
        APPROVED,
        BLOCKED
    }

    public enum InvoiceStatus
    {
        PENDING,
        UPLOADED,
        VERIFIED,
        REJECTED
    }

    public enum TransportType
    {
        DEPOT_DRIVER,
        THIRD_PARTY
    }
}
