# Factory Sequence Diagrams - Layered Two-Line Style

Moi participant co hai dong: stereotype o dong dau va ten lop/interface o dong sau. Sao chep tung khoi `@startuml` den `@enduml` vao Visual Paradigm.

## 3.2.5.1 Hoan thien ho so nha may

```plantuml
@startuml
title 3.2.5.1 HOAN THIEN HO SO NHA MAY
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBorderColor #8D899B
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nFactory Profile Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryProfileController" as Controller #E5E3EF
participant "<<Service>>\nIFactoryProfileService" as Service #E5E3EF
participant "<<Service>>\nICloudinaryService" as Cloud #E5E3EF
participant "<<Repository>>\nIFactoryRepository" as FactoryRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Open profile and enter information
UI -> Controller: UpdateProfile(profileDto, licenseFile)
activate Controller
Controller -> Service: UpdateProfileAsync(factoryId, profileDto, file)
activate Service
Service -> Cloud: UploadAsync(licenseFile, folder="factory-license")
activate Cloud
Cloud --> Service: licenseUrl
deactivate Cloud
Service -> FactoryRepo: GetByIdAsync(factoryId)
activate FactoryRepo
FactoryRepo -> DB: SELECT FROM factories WHERE id=@factoryId
DB --> FactoryRepo: Factory
FactoryRepo --> Service: Factory entity
deactivate FactoryRepo
Service -> FactoryRepo: UpdateAsync(factory)
activate FactoryRepo
FactoryRepo -> DB: UPDATE factories SET ...
DB --> FactoryRepo: OK
FactoryRepo --> Service: Saved profile
deactivate FactoryRepo
Service --> Controller: FactoryProfileDto
deactivate Service
Controller --> UI: 200 OK - Profile updated
deactivate Controller
UI --> Factory: Show update success
@enduml
```

## 3.2.5.2 Xem dashboard nha may

```plantuml
@startuml
title 3.2.5.2 XEM DASHBOARD NHA MAY
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nFactory Dashboard Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryDashboardController" as Controller #E5E3EF
participant "<<Service>>\nIFactoryDashboardService" as Service #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nIFactoryDemandRepository" as DemandRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Open dashboard
UI -> Controller: GetDashboard(dateRange)
activate Controller
Controller -> Service: GetDashboardAsync(factoryId, dateRange)
activate Service
Service -> OrderRepo: GetOrderSummaryAsync(factoryId, dateRange)
activate OrderRepo
OrderRepo -> DB: SELECT status, weight, amount FROM batch_orders
DB --> OrderRepo: Order statistics
OrderRepo --> Service: OrderSummary
deactivate OrderRepo
Service -> DemandRepo: GetActiveDemandCountAsync(factoryId)
activate DemandRepo
DemandRepo -> DB: SELECT COUNT(*) FROM factory_demands
DB --> DemandRepo: ActiveDemandCount
DemandRepo --> Service: Demand summary
deactivate DemandRepo
Service --> Controller: FactoryDashboardDto
deactivate Service
Controller --> UI: 200 OK - Dashboard data
deactivate Controller
UI --> Factory: Display cards and charts
@enduml
```

## 3.2.5.3 Dang va quan ly nhu cau thu mua

```plantuml
@startuml
title 3.2.5.3 DANG VA QUAN LY NHU CAU THU MUA
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nFactory Demand Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryDemandController" as Controller #E5E3EF
participant "<<Service>>\nIFactoryDemandService" as Service #E5E3EF
participant "<<Repository>>\nIFactoryDemandRepository" as DemandRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "DEPOT" as Depot #E5E3EF

Factory -> UI: Enter material, quantity, price and deadline
UI -> Controller: CreateDemand(demandDto)
activate Controller
Controller -> Service: CreateDemandAsync(factoryId, demandDto)
activate Service
Service -> Service: Validate quantity and deadline
alt Valid data
  Service -> DemandRepo: AddAsync(newDemand)
  activate DemandRepo
  DemandRepo -> DB: INSERT INTO factory_demands (...)
  DB --> DemandRepo: Created demand
  DemandRepo --> Service: FactoryDemand
  deactivate DemandRepo
  Service --> Controller: DemandDto
  Controller --> UI: 201 Created
  UI --> Factory: Show publish success
  Depot -> Controller: GetActiveDemands(filters)
  Controller -> Service: GetActiveDemandsAsync(filters)
  Service -> DemandRepo: SearchActiveAsync(filters)
  activate DemandRepo
  DemandRepo -> DB: SELECT FROM factory_demands WHERE is_active=true
  DB --> DemandRepo: Active demands
  DemandRepo --> Service: Demand list
  deactivate DemandRepo
  Service --> Controller: DemandListDto
  Controller --> Depot: Display Demand Board
else Invalid data
  Service --> Controller: ValidationErrors
  Controller --> UI: 400 Bad Request
end
deactivate Service
deactivate Controller
@enduml
```

## 3.2.5.4 Duyet va nhan lo tren Marketplace

```plantuml
@startuml
title 3.2.5.4 DUYET VA NHAN LO TREN MARKETPLACE
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nMarketplace Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryMarketController" as Controller #E5E3EF
participant "<<Service>>\nIFactoryMarketService" as Service #E5E3EF
participant "<<Repository>>\nIInventoryBatchRepository" as BatchRepo #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nINotificationRepository" as NotificationRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "DEPOT" as Depot #E5E3EF

Factory -> UI: Filter and select a listed batch
UI -> Controller: AcceptBatch(batchId)
activate Controller
Controller -> Service: AcceptBatchAsync(factoryId, batchId)
activate Service
Service -> BatchRepo: GetListedForUpdateAsync(batchId)
activate BatchRepo
BatchRepo -> DB: SELECT FROM inventory_batches WHERE id=@batchId FOR UPDATE
DB --> BatchRepo: InventoryBatch
BatchRepo --> Service: Batch entity
deactivate BatchRepo
alt Batch status is LISTED
  Service -> OrderRepo: AddAsync(new BatchOrder)
  activate OrderRepo
  OrderRepo -> DB: INSERT INTO batch_orders (...)
  DB --> OrderRepo: New order
  OrderRepo --> Service: BatchOrder
  deactivate OrderRepo
  Service -> BatchRepo: UpdateStatusAsync(batchId, ACCEPTED)
  activate BatchRepo
  BatchRepo -> DB: UPDATE inventory_batches SET status='ACCEPTED'
  DB --> BatchRepo: OK
  deactivate BatchRepo
  Service -> NotificationRepo: AddAsync(notification)
  activate NotificationRepo
  NotificationRepo -> DB: INSERT INTO notifications (...)
  DB --> NotificationRepo: OK
  NotificationRepo --> Depot: Notify accepted batch
  deactivate NotificationRepo
  Service --> Controller: BatchOrderDto
  Controller --> UI: 200 OK - Batch accepted
else Batch is unavailable
  Service --> Controller: BatchUnavailable
  Controller --> UI: 409 Conflict
end
deactivate Service
deactivate Controller
UI --> Factory: Show result
@enduml
```

## 3.2.5.5 Duyet request chi dinh tu Depot

```plantuml
@startuml
title 3.2.5.5 DUYET REQUEST CHI DINH TU DEPOT
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nDirect Request Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryMarketController" as Controller #E5E3EF
participant "<<Service>>\nIFactoryMarketService" as Service #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nIPartnershipRepository" as PartnerRepo #E5E3EF
participant "<<Repository>>\nINotificationRepository" as NotificationRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "DEPOT" as Depot #E5E3EF

Factory -> UI: Review pending direct request
UI -> Controller: SubmitDecision(requestId, decision, reason)
activate Controller
Controller -> Service: ReviewDirectRequestAsync(factoryId, requestId, decision)
activate Service
Service -> OrderRepo: GetPendingRequestAsync(requestId)
activate OrderRepo
OrderRepo -> DB: SELECT FROM batch_orders WHERE id=@requestId
DB --> OrderRepo: Pending request
OrderRepo --> Service: BatchOrder
deactivate OrderRepo
alt APPROVE
  Service -> OrderRepo: UpdateStatusAsync(requestId, ACCEPTED)
  OrderRepo -> DB: UPDATE batch_orders SET status='ACCEPTED'
  Service -> PartnerRepo: UpsertAsync(factoryId, depotId, APPROVED)
  PartnerRepo -> DB: INSERT OR UPDATE partnerships
  Service -> NotificationRepo: AddAsync(approvalNotification)
  NotificationRepo -> DB: INSERT INTO notifications (...)
  NotificationRepo --> Depot: Request approved
  Service --> Controller: ApprovedOrderDto
  Controller --> UI: 200 OK
else REJECT
  Service -> OrderRepo: RejectAsync(requestId, reason)
  OrderRepo -> DB: UPDATE batch_orders SET status='REJECTED'
  Service -> NotificationRepo: AddAsync(rejectionNotification)
  NotificationRepo -> DB: INSERT INTO notifications (...)
  NotificationRepo --> Depot: Request rejected
  Service --> Controller: RejectedResult
  Controller --> UI: 200 OK
end
deactivate Service
deactivate Controller
UI --> Factory: Show decision result
@enduml
```

## 3.2.5.6 Theo doi van chuyen

```plantuml
@startuml
title 3.2.5.6 THEO DOI VAN CHUYEN LO HANG
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "DRIVER" as Driver #E5E3EF
participant "<<UI>>\nTracking Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryOrderController" as Controller #E5E3EF
participant "<<Service>>\nITransportService" as Service #E5E3EF
participant "<<Repository>>\nITransportJobRepository" as JobRepo #E5E3EF
participant "<<Repository>>\nITrackingLogRepository" as LogRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "FACTORY" as Factory #E5E3EF

Driver -> Service: UpdateTrackingAsync(jobId, status, GPS)
activate Service
Service -> LogRepo: AddAsync(trackingLog)
activate LogRepo
LogRepo -> DB: INSERT INTO transport_tracking_logs (...)
DB --> LogRepo: OK
deactivate LogRepo
Service -> JobRepo: UpdateStatusAsync(jobId, status)
activate JobRepo
JobRepo -> DB: UPDATE transport_jobs SET status=@status
DB --> JobRepo: OK
deactivate JobRepo
Service --> Driver: Tracking updated
deactivate Service
Factory -> UI: Open tracking screen
UI -> Controller: GetTracking(orderId)
activate Controller
Controller -> Service: GetLatestTrackingAsync(orderId)
activate Service
Service -> JobRepo: GetByOrderIdAsync(orderId)
JobRepo -> DB: SELECT FROM transport_jobs
DB --> JobRepo: TransportJob
Service -> LogRepo: GetLatestAsync(jobId)
LogRepo -> DB: SELECT FROM transport_tracking_logs ORDER BY created_at DESC
DB --> LogRepo: LatestTrackingLog
Service --> Controller: TrackingDto
deactivate Service
Controller --> UI: Status, GPS and timeline
deactivate Controller
UI --> Factory: Display map and status
@enduml
```

## 3.2.5.7 Xac nhan nhan hang

```plantuml
@startuml
title 3.2.5.7 XAC NHAN NHAN HANG TAI NHA MAY
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nReceiving Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryQCController" as Controller #E5E3EF
participant "<<Service>>\nIQCService" as Service #E5E3EF
participant "<<Repository>>\nITransportJobRepository" as JobRepo #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Confirm vehicle arrived
UI -> Controller: ConfirmReceiving(orderId)
activate Controller
Controller -> Service: ConfirmReceivingAsync(factoryId, orderId)
activate Service
Service -> JobRepo: GetByOrderIdAsync(orderId)
activate JobRepo
JobRepo -> DB: SELECT FROM transport_jobs WHERE batch_order_id=@orderId
DB --> JobRepo: TransportJob
JobRepo --> Service: Delivery status
deactivate JobRepo
alt Transport status is DELIVERED
  Service -> OrderRepo: MarkReadyForQCAsync(orderId, receivedAt)
  activate OrderRepo
  OrderRepo -> DB: UPDATE batch_orders SET receiving_time=NOW()
  DB --> OrderRepo: OK
  OrderRepo --> Service: Updated order
  deactivate OrderRepo
  Service --> Controller: ReceivingResult
  Controller --> UI: 200 OK - Continue to weighbridge
else Shipment is not delivered
  Service --> Controller: InvalidTransportStatus
  Controller --> UI: 409 Conflict
end
deactivate Service
deactivate Controller
UI --> Factory: Show result
@enduml
```

## 3.2.5.8 Can doi trong

```plantuml
@startuml
title 3.2.5.8 CAN DOI TRONG TAI NHA MAY
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nWeighbridge Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryQCController" as Controller #E5E3EF
participant "<<Service>>\nIQCService" as Service #E5E3EF
participant "<<Service>>\nICloudinaryService" as Cloud #E5E3EF
participant "<<Repository>>\nIWeightTicketRepository" as WeightRepo #E5E3EF
participant "<<Repository>>\nIInventoryBatchRepository" as BatchRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Enter gross, tare and ticket image
UI -> Controller: CreateWeightTicket(orderId, data, image)
activate Controller
Controller -> Service: CreateWeightTicketAsync(orderId, data, image)
activate Service
Service -> Cloud: UploadAsync(image, folder="weight-tickets")
activate Cloud
Cloud --> Service: ticketImageUrl
deactivate Cloud
Service -> BatchRepo: GetByOrderIdAsync(orderId)
activate BatchRepo
BatchRepo -> DB: SELECT estimated_weight_kg FROM inventory_batches
DB --> BatchRepo: EstimatedWeightKg
BatchRepo --> Service: Estimated weight
deactivate BatchRepo
Service -> Service: NetWeight = Gross - Tare
Service -> Service: Calculate difference percentage
Service -> WeightRepo: AddTicketAndVerificationAsync(data)
activate WeightRepo
WeightRepo -> DB: INSERT weight_ticket AND weight_verification
DB --> WeightRepo: COMMIT
WeightRepo --> Service: VerificationResult
deactivate WeightRepo
alt Difference is greater than 5 percent
  Service --> Controller: Result with warning
  Controller --> UI: Display discrepancy warning
else Difference is acceptable
  Service --> Controller: Verified result
  Controller --> UI: Continue to quality inspection
end
deactivate Service
deactivate Controller
@enduml
```

## 3.2.5.9 Kiem tra chat luong

```plantuml
@startuml
title 3.2.5.9 KIEM TRA CHAT LUONG VA PHAN LOAI
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nQuality Inspection Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryQCController" as Controller #E5E3EF
participant "<<Service>>\nIQCService" as Service #E5E3EF
participant "<<Service>>\nICloudinaryService" as Cloud #E5E3EF
participant "<<Repository>>\nIQualityReportRepository" as ReportRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Enter purity, moisture, contamination and grade
UI -> Controller: SaveQualityReport(orderId, report, images)
activate Controller
Controller -> Service: SaveQualityReportAsync(orderId, report, images)
activate Service
Service -> Service: Validate percentages and grade
Service -> Cloud: UploadManyAsync(images, folder="qc-evidence")
activate Cloud
Cloud --> Service: EvidenceUrls
deactivate Cloud
alt Report is valid
  Service -> ReportRepo: AddAsync(qualityReport)
  activate ReportRepo
  ReportRepo -> DB: INSERT INTO quality_reports (...)
  DB --> ReportRepo: Created report
  ReportRepo --> Service: QualityReport
  deactivate ReportRepo
  Service --> Controller: QualityReportDto
  Controller --> UI: 201 Created - Continue to decision
else Report is invalid
  Service --> Controller: ValidationErrors
  Controller --> UI: 400 Bad Request
end
deactivate Service
deactivate Controller
UI --> Factory: Show inspection result
@enduml
```

## 3.2.5.10 Chap nhan hoac tu choi lo

```plantuml
@startuml
title 3.2.5.10 CHAP NHAN HOAC TU CHOI LO HANG
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nQC Decision Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryQCController" as Controller #E5E3EF
participant "<<Service>>\nIQCService" as Service #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nINotificationRepository" as NotificationRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "DEPOT" as Depot #E5E3EF

Factory -> UI: Review QC summary and select decision
UI -> Controller: SubmitDecision(orderId, decision, reason)
activate Controller
Controller -> Service: SubmitDecisionAsync(orderId, decision, reason)
activate Service
Service -> OrderRepo: GetQCSummaryAsync(orderId)
activate OrderRepo
OrderRepo -> DB: SELECT weight and quality results
DB --> OrderRepo: QCSummary
OrderRepo --> Service: QC data
deactivate OrderRepo
alt ACCEPT
  Service -> OrderRepo: UpdateStatusAsync(orderId, VERIFIED)
  OrderRepo -> DB: UPDATE batch_orders SET status='VERIFIED'
  Service -> NotificationRepo: AddAsync(acceptedNotification)
  NotificationRepo -> DB: INSERT INTO notifications (...)
  NotificationRepo --> Depot: Batch accepted
  Service --> Controller: VERIFIED
  Controller --> UI: Continue to settlement
else REJECT
  Service -> OrderRepo: RejectAsync(orderId, reason)
  OrderRepo -> DB: UPDATE batch_orders SET status='REJECTED'
  Service -> NotificationRepo: AddAsync(rejectedNotification)
  NotificationRepo -> DB: INSERT INTO notifications (...)
  NotificationRepo --> Depot: Batch rejected with reason
  Service --> Controller: REJECTED
  Controller --> UI: Display rejection result
end
deactivate Service
deactivate Controller
@enduml
```

## 3.2.5.11 Quyet toan va upload hoa don

```plantuml
@startuml
title 3.2.5.11 QUYET TOAN VA UPLOAD HOA DON
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nSettlement Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryOrderController" as Controller #E5E3EF
participant "<<Service>>\nIPaymentService" as Service #E5E3EF
participant "<<Service>>\nICloudinaryService" as Cloud #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nIInvoiceRepository" as InvoiceRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Open verified order
UI -> Controller: GetSettlement(orderId)
activate Controller
Controller -> Service: CalculateSettlementAsync(orderId)
activate Service
Service -> OrderRepo: GetVerifiedOrderAsync(orderId)
activate OrderRepo
OrderRepo -> DB: SELECT weight, price and fee_rate
DB --> OrderRepo: SettlementData
OrderRepo --> Service: Verified order
deactivate OrderRepo
Service -> Service: Calculate subtotal, fee and depot payment
Service --> Controller: SettlementBreakdownDto
Controller --> UI: Display settlement breakdown
Factory -> UI: Confirm settlement and upload invoice
UI -> Controller: SettleOrder(orderId, invoiceFile)
Controller -> Service: SettleOrderAsync(orderId, invoiceFile)
Service -> Cloud: UploadAsync(invoiceFile, folder="invoices")
activate Cloud
Cloud --> Service: InvoiceFileUrl
deactivate Cloud
Service -> InvoiceRepo: AddAsync(newInvoice)
activate InvoiceRepo
InvoiceRepo -> DB: INSERT INTO invoices (...)
DB --> InvoiceRepo: Invoice
InvoiceRepo --> Service: Saved invoice
deactivate InvoiceRepo
Service -> OrderRepo: MarkSettledAsync(orderId)
OrderRepo -> DB: UPDATE batch_orders SET settlement_status='PAID'
Service --> Controller: SettlementResult
deactivate Service
Controller --> UI: 200 OK - Settlement completed
deactivate Controller
UI --> Factory: Show invoice and success
@enduml
```

## 3.2.5.12 Danh gia va quan ly doi tac Depot

```plantuml
@startuml
title 3.2.5.12 DANH GIA VA QUAN LY DOI TAC DEPOT
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nPartner Management Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryPartnerController" as Controller #E5E3EF
participant "<<Service>>\nIPartnershipService" as Service #E5E3EF
participant "<<Repository>>\nIPartnershipRepository" as PartnerRepo #E5E3EF
participant "<<Repository>>\nINotificationRepository" as NotificationRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8
actor "DEPOT" as Depot #E5E3EF

Factory -> UI: Rate depot and choose cooperation status
UI -> Controller: RateDepot(depotId, stars, cooperate)
activate Controller
Controller -> Service: RateDepotAsync(factoryId, depotId, data)
activate Service
Service -> PartnerRepo: GetAsync(factoryId, depotId)
activate PartnerRepo
PartnerRepo -> DB: SELECT FROM partnerships
DB --> PartnerRepo: Partnership
PartnerRepo --> Service: Partner entity
deactivate PartnerRepo
Service -> PartnerRepo: UpdateRatingAndStatusAsync(rating, status)
activate PartnerRepo
PartnerRepo -> DB: UPDATE partnerships SET rating=@rating, status=@status
DB --> PartnerRepo: OK
deactivate PartnerRepo
Service -> NotificationRepo: AddAsync(partnerNotification)
activate NotificationRepo
NotificationRepo -> DB: INSERT INTO notifications (...)
DB --> NotificationRepo: OK
NotificationRepo --> Depot: Partnership status updated
deactivate NotificationRepo
Service --> Controller: PartnershipDto
deactivate Service
Controller --> UI: 200 OK
deactivate Controller
UI --> Factory: Show APPROVED or BLOCKED
@enduml
```

## 3.2.5.13 Xem va tao chung nhan EPR

```plantuml
@startuml
title 3.2.5.13 XEM VA TAO CHUNG NHAN EPR
autonumber 1
skinparam ArrowColor #55525F
skinparam LifeLineBorderColor #B8B5C5
skinparam ParticipantBackgroundColor #E5E3EF

actor "FACTORY" as Factory #E5E3EF
participant "<<UI>>\nEPR Report Screen" as UI #E5E3EF
participant "<<Controller>>\nFactoryEprController" as Controller #E5E3EF
participant "<<Service>>\nIEprService" as Service #E5E3EF
participant "<<Repository>>\nIBatchOrderRepository" as OrderRepo #E5E3EF
participant "<<Repository>>\nIEprCertificateRepository" as EprRepo #E5E3EF
database "PostgreSQL" as DB #D9D6E8

Factory -> UI: Select verified order and generate certificate
UI -> Controller: GenerateCertificate(orderId)
activate Controller
Controller -> Service: GenerateCertificateAsync(factoryId, orderId)
activate Service
Service -> OrderRepo: GetVerifiedOrderAsync(orderId)
activate OrderRepo
OrderRepo -> DB: SELECT verified weight, material and source
DB --> OrderRepo: VerifiedRecyclingData
OrderRepo --> Service: Verified order
deactivate OrderRepo
Service -> Service: Generate certificate code and hash
Service -> EprRepo: AddAsync(newCertificate)
activate EprRepo
EprRepo -> DB: INSERT INTO epr_certificates (...)
DB --> EprRepo: EprCertificate
EprRepo --> Service: Saved certificate
deactivate EprRepo
Service --> Controller: EprCertificateDto
deactivate Service
Controller --> UI: 201 Created
deactivate Controller
UI --> Factory: Display downloadable certificate
@enduml
```
