# RETRACK - Factory Class Diagram Guide for Visual Paradigm

Use this guide to redraw the Factory Operation class diagram in Visual Paradigm.
Scope: Member 4 - Factory Operation B2B Flow from `TASK_ALLOCATION.md`.

## 1. Main Packages

Create these packages in one Class Diagram:

- Identity
- Factory Profile & Demand Board
- Marketplace & Batch Order
- Transport Tracking
- Receiving, QC & Settlement
- Factory Services / Controllers

## 2. Core Classes

### Identity

- `User`
- `UserRole` enum

### Factory Profile & Demand Board

- `Factory`
- `FactoryDemand`

### Marketplace & Batch Order

- `Depot`
- `InventoryBatch`
- `BatchImage`
- `BatchOrder`
- `Partnership`

### Transport Tracking

- `Driver`
- `TransportJob`
- `TransportTrackingLog`

### Receiving, QC & Settlement

- `WeightTicket`
- `WeightVerification`
- `Invoice`
- `EprCertificate`

### Factory Services / Controllers

- `FactoryDemandController`
- `FactoryMarketController`
- `FactoryQCController`
- `FactoryOrderController`
- `FactoryPartnerController`
- `IFactoryMarketService`
- `IQCService`
- `IPaymentService`

## 3. Enums

- `MaterialType`: PET, HDPE, PVC, PAPER, CARDBOARD, ALUMINUM, IRON, STEEL, COPPER, ELECTRONIC_WASTE, OTHER
- `BatchStatus`: DRAFT, LISTED, ACCEPTED, READY_FOR_PICKUP, IN_PROGRESS, DELIVERED, VERIFIED, REJECTED, CANCELLED
- `TransportStatus`: PENDING, ASSIGNED, PICKED_UP, ON_THE_WAY, DELIVERED, CANCELLED
- `PartnershipStatus`: PENDING, APPROVED, BLOCKED
- `InvoiceStatus`: PENDING, UPLOADED, VERIFIED, REJECTED
- `TransportType`

## 4. Associations and Multiplicity

Draw these associations:

- `User 1 -- 0..1 Factory`
- `User 1 -- 0..1 Depot`
- `User 1 -- 0..1 Driver`
- `Factory 1 -- 0..* FactoryDemand`
- `Depot 1 -- 0..* InventoryBatch`
- `InventoryBatch 1 -- 0..* BatchImage`
- `InventoryBatch 1 -- 0..1 BatchOrder`
- `Factory 1 -- 0..* BatchOrder`
- `BatchOrder 1 -- 0..1 TransportJob`
- `BatchOrder 1 -- 0..1 WeightTicket`
- `BatchOrder 1 -- 0..1 WeightVerification`
- `BatchOrder 1 -- 0..1 Invoice`
- `BatchOrder 1 -- 0..1 EprCertificate`
- `Depot 1 -- 0..* Partnership`
- `Factory 1 -- 0..* Partnership`
- `Depot 1 -- 0..* Driver`
- `Driver 1 -- 0..* TransportJob`
- `TransportJob 1 -- 0..* TransportTrackingLog`

Draw dependency arrows:

- `FactoryMarketController ..> IFactoryMarketService`
- `FactoryQCController ..> IQCService`
- `FactoryOrderController ..> IPaymentService`
- `FactoryDemandController ..> FactoryDemand`
- `FactoryPartnerController ..> Partnership`

## 5. Suggested Layout

Place the classes from left to right following the business flow:

`Depot / InventoryBatch` -> `BatchOrder` -> `TransportJob` -> `WeightTicket / WeightVerification` -> `Invoice / EprCertificate`

Put `Factory` above `BatchOrder`, because Factory owns demand posts and receives many orders.

Put `Partnership` between `Depot` and `Factory`, because it represents the B2B relationship and rating/blocking decision after settlement.

## 6. Notes for Report Explanation

Factory flow covered by this class diagram:

1. Factory posts buying needs through `FactoryDemand`.
2. Factory browses marketplace batches from `InventoryBatch`.
3. Factory accepts a batch and creates/owns a `BatchOrder`.
4. Depot driver delivers through `TransportJob`.
5. Factory performs receiving, weighbridge, and QC through `WeightTicket` and `WeightVerification`.
6. If accepted, Factory settles payment through `Invoice`.
7. After settlement, Factory rates or blocks the depot through `Partnership`.
8. If EPR is included later, `EprCertificate` links to the verified `BatchOrder`.

The ready-to-copy PlantUML source is in:

`docs/factory-class-diagram-member-4.puml`
