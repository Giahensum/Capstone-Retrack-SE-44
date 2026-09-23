# Factory Sequence Diagrams - Member 4

Copy one PlantUML block at a time into Visual Paradigm AI Diagram Generation and select **Sequence Diagram**.

## 3.2.5.1 Complete Factory Profile

```plantuml
@startuml
title 3.2.5.1 Complete Factory Profile
actor Factory
boundary "Factory Web App" as UI
control FactoryProfileController as Controller
control FactoryProfileService as Service
database "RETRACK Database" as DB
collections "Cloud Storage" as Storage

Factory -> UI: Open factory profile
UI -> Controller: GET profile
Controller -> Service: GetProfile(factoryId)
Service -> DB: Find factory profile
DB --> Service: Factory information
Service --> Controller: Profile data
Controller --> UI: Display profile form
Factory -> UI: Enter company and location information
Factory -> UI: Upload business and environmental licenses
UI -> Storage: Upload license files
Storage --> UI: File URLs
UI -> Controller: PUT profile(profileData, fileUrls)
Controller -> Service: UpdateProfile(factoryId, profileData)
Service -> Service: Validate tax code and required fields
alt Information is valid
  Service -> DB: Update Factory profile
  DB --> Service: Updated profile
  Service --> Controller: Update successful
  Controller --> UI: Show completed profile
else Information is invalid
  Service --> Controller: Validation errors
  Controller --> UI: Show error messages
end
@enduml
```

## 3.2.5.2 View Factory Dashboard

```plantuml
@startuml
title 3.2.5.2 View Factory Dashboard
actor Factory
boundary "Factory Dashboard" as UI
control FactoryDashboardController as Controller
control FactoryDashboardService as Service
database "RETRACK Database" as DB

Factory -> UI: Open dashboard
UI -> Controller: GET dashboard(dateRange)
Controller -> Service: GetDashboard(factoryId, dateRange)
par Load order summary
  Service -> DB: Count orders by status
  DB --> Service: Order summary
else Load purchasing statistics
  Service -> DB: Sum verified weight and amount
  DB --> Service: Purchasing statistics
else Load pending work
  Service -> DB: Find pending requests and QC orders
  DB --> Service: Pending work
end
Service --> Controller: Dashboard data
Controller --> UI: Display cards and charts
@enduml
```

## 3.2.5.3 Publish and Manage Purchasing Demand

```plantuml
@startuml
title 3.2.5.3 Publish and Manage Purchasing Demand
actor Factory
actor Depot
boundary "Factory Demand UI" as FactoryUI
boundary "Public Demand Board" as DemandBoard
control FactoryDemandController as Controller
control FactoryDemandService as Service
database "RETRACK Database" as DB

Factory -> FactoryUI: Create purchasing demand
FactoryUI --> Factory: Display demand form
Factory -> FactoryUI: Enter material, required quantity,
Factory -> FactoryUI: expected price and deadline
FactoryUI -> Controller: POST demand(data)
Controller -> Service: CreateDemand(factoryId, data)
Service -> Service: Validate capacity, quantity and deadline

alt Demand information is valid
  Service -> DB: Insert active FactoryDemand
  DB --> Service: Created demand
  Service --> Controller: Published demand
  Controller --> FactoryUI: Show publish success
  Depot -> DemandBoard: Browse factory demands
  DemandBoard -> Service: GetActiveDemands(filters)
  Service -> DB: Query active demands
  DB --> Service: Matching factory demands
  Service --> DemandBoard: Demand list
  DemandBoard --> Depot: Display purchasing requirements
else Demand information is invalid
  Service --> Controller: Validation errors
  Controller --> FactoryUI: Request corrections
end

opt Factory edits or closes its demand
  Factory -> FactoryUI: Update demand or deactivate it
  FactoryUI -> Controller: PUT demand(demandId, changes)
  Controller -> Service: UpdateOwnedDemand(factoryId, demandId, changes)
  Service -> DB: Update demand information/status
  DB --> Service: Demand updated
  Service --> Controller: Update successful
  Controller --> FactoryUI: Display latest demand
end
@enduml
```

## 3.2.5.4 Browse and Accept Marketplace Batch

```plantuml
@startuml
title 3.2.5.4 Browse and Accept Marketplace Batch
actor Factory
actor Depot
boundary "Marketplace UI" as UI
control FactoryMarketController as Controller
control FactoryMarketService as Service
database "RETRACK Database" as DB
participant "Notification Service" as Notification

Depot -> DB: Publish inventory batch as LISTED
Factory -> UI: Open marketplace
UI -> Controller: GET batches(filters)
Controller -> Service: BrowseListedBatches(filters)
Service -> DB: Query batches with status LISTED
DB --> Service: Matching batches
Service --> Controller: Batch list
Controller --> UI: Display available batches
Factory -> UI: Select a batch and click Accept
UI -> Controller: POST accept(batchId)
Controller -> Service: AcceptBatch(factoryId, batchId)
Service -> DB: Lock and read batch
DB --> Service: Current batch status
alt Batch is still LISTED
  Service -> DB: Create BatchOrder
  Service -> DB: Set batch status ACCEPTED
  DB --> Service: Transaction committed
  Service -> Notification: Notify depot of accepted batch
  Notification --> Depot: Factory accepted the batch
  Service --> Controller: Accepted order
  Controller --> UI: Show order confirmation
else Batch was already accepted
  Service --> Controller: Batch unavailable
  Controller --> UI: Show availability error
end
@enduml
```

## 3.2.5.5 Approve or Reject Direct Request

```plantuml
@startuml
title 3.2.5.5 Approve or Reject Direct Request
actor Factory
boundary "Direct Request UI" as UI
control FactoryMarketController as Controller
control FactoryMarketService as Service
database "RETRACK Database" as DB
participant "Notification Service" as Notification

Factory -> UI: Open pending direct requests
UI -> Controller: GET direct requests
Controller -> Service: GetPendingRequests(factoryId)
Service -> DB: Query pending assigned batches
DB --> Service: Request list
Service --> Controller: Request list
Controller --> UI: Display requests
Factory -> UI: Review batch details
alt Factory approves
  Factory -> UI: Click Approve
  UI -> Controller: POST approve(requestId)
  Controller -> Service: ApproveRequest(factoryId, requestId)
  Service -> DB: Create order and set ACCEPTED
  Service -> DB: Set partnership APPROVED
  Service -> Notification: Notify depot of approval
  Notification --> Factory: Notification queued
  Service --> Controller: Approved
  Controller --> UI: Show accepted order
else Factory rejects
  Factory -> UI: Enter reason and click Reject
  UI -> Controller: POST reject(requestId, reason)
  Controller -> Service: RejectRequest(factoryId, requestId, reason)
  Service -> DB: Set request REJECTED
  Service -> Notification: Notify depot of rejection
  Service --> Controller: Rejected
  Controller --> UI: Show rejection result
end
@enduml
```

## 3.2.5.6 Track Batch Transportation

```plantuml
@startuml
title 3.2.5.6 Track Batch Transportation
actor Factory
actor Driver
boundary "Tracking UI" as UI
control FactoryOrderController as Controller
control TransportService as Service
database "RETRACK Database" as DB

Driver -> Service: Update status and GPS location
Service -> DB: Save TransportTrackingLog
DB --> Service: Log saved

loop While shipment is in progress
  Factory -> UI: Refresh order tracking
  UI -> Controller: GET tracking(orderId)
  Controller -> Service: GetLatestTracking(orderId)
  Service -> DB: Read transport job and latest log
  DB --> Service: Status, location and timestamps
  Service --> Controller: Tracking information
  Controller --> UI: Display timeline and map
end

Driver -> Service: Confirm delivery at factory
Service -> DB: Set transport status DELIVERED
Service -> DB: Record delivered time
Service --> UI: Delivery status available
@enduml
```

## 3.2.5.7 Confirm Batch Receiving

```plantuml
@startuml
title 3.2.5.7 Confirm Batch Receiving
actor Factory
boundary "Receiving UI" as UI
control FactoryQCController as Controller
control QCService as Service
database "RETRACK Database" as DB

Factory -> UI: Open delivered order
UI -> Controller: GET receiving details(orderId)
Controller -> Service: GetReceivingDetails(factoryId, orderId)
Service -> DB: Read order and transport status
DB --> Service: Order details
Service --> Controller: Receiving information
Controller --> UI: Display shipment details
Factory -> UI: Confirm vehicle and shipment arrived
UI -> Controller: POST confirm receiving(orderId)
Controller -> Service: ConfirmReceiving(factoryId, orderId)
Service -> DB: Verify transport is DELIVERED
alt Shipment can be received
  Service -> DB: Record receiving time
  Service -> DB: Set order ready for QC
  DB --> Service: Updated order
  Service --> Controller: Receiving confirmed
  Controller --> UI: Open weighbridge step
else Shipment is not delivered
  Service --> Controller: Invalid transport status
  Controller --> UI: Show receiving error
end
@enduml
```

## 3.2.5.8 Perform Weighbridge Verification

```plantuml
@startuml
title 3.2.5.8 Perform Weighbridge Verification
actor Factory
boundary "Weighbridge UI" as UI
control FactoryQCController as Controller
control QCService as Service
database "RETRACK Database" as DB
collections "Cloud Storage" as Storage

Factory -> UI: Enter gross and tare weights
Factory -> UI: Upload weight-ticket image
UI -> Storage: Upload ticket image
Storage --> UI: Ticket image URL
UI -> Controller: POST weight ticket(orderId, weights, imageUrl)
Controller -> Service: CreateWeightTicket(orderId, data)
Service -> Service: Calculate net weight
Service -> DB: Read depot estimated weight
DB --> Service: Estimated weight
Service -> Service: Calculate difference percentage
Service -> DB: Save WeightTicket
Service -> DB: Save WeightVerification
alt Difference is greater than 5 percent
  Service --> Controller: Verification with discrepancy warning
  Controller --> UI: Highlight weight discrepancy
else Difference is acceptable
  Service --> Controller: Weight verified
  Controller --> UI: Continue to quality inspection
end
@enduml
```

## 3.2.5.9 Perform Quality Inspection

```plantuml
@startuml
title 3.2.5.9 Perform Quality Inspection
actor Factory
boundary "Quality Inspection UI" as UI
control FactoryQCController as Controller
control QCService as Service
database "RETRACK Database" as DB
collections "Cloud Storage" as Storage

Factory -> UI: Open quality inspection form
UI -> Controller: GET QC information(orderId)
Controller -> Service: GetQCInformation(orderId)
Service -> DB: Read batch and weight verification
DB --> Service: QC input data
Service --> Controller: QC information
Controller --> UI: Display inspection form
Factory -> UI: Enter purity, moisture, contamination and grade
Factory -> UI: Add notes and evidence images
UI -> Storage: Upload evidence images
Storage --> UI: Evidence URLs
UI -> Controller: POST quality report(orderId, report)
Controller -> Service: SaveQualityReport(orderId, report)
Service -> Service: Validate percentages and grade
alt Report is valid
  Service -> DB: Save quality inspection report
  DB --> Service: Report saved
  Service --> Controller: Inspection completed
  Controller --> UI: Show quality decision step
else Report is invalid
  Service --> Controller: Validation errors
  Controller --> UI: Show fields requiring correction
end
@enduml
```

## 3.2.5.10 Accept or Reject Batch

```plantuml
@startuml
title 3.2.5.10 Accept or Reject Batch
actor Factory
boundary "QC Decision UI" as UI
control FactoryQCController as Controller
control QCService as Service
database "RETRACK Database" as DB
participant "Notification Service" as Notification

Factory -> UI: Review weight and quality results
UI -> Controller: GET QC summary(orderId)
Controller -> Service: GetQCSummary(orderId)
Service -> DB: Read weight and quality reports
DB --> Service: QC summary
Service --> Controller: QC summary
Controller --> UI: Display decision information
alt Factory accepts batch
  Factory -> UI: Click Accept
  UI -> Controller: POST decision(orderId, ACCEPT)
  Controller -> Service: AcceptBatch(orderId)
  Service -> DB: Set order and batch VERIFIED
  Service -> Notification: Notify depot of acceptance
  Service --> Controller: Batch verified
  Controller --> UI: Open settlement step
else Factory rejects batch
  Factory -> UI: Enter reason and click Reject
  UI -> Controller: POST decision(orderId, REJECT, reason)
  Controller -> Service: RejectBatch(orderId, reason)
  Service -> DB: Set order and batch REJECTED
  Service -> DB: Save rejection reason
  Service -> Notification: Notify depot of rejection
  Service --> Controller: Batch rejected
  Controller --> UI: Show rejection result
end
@enduml
```

## 3.2.5.11 Settle Batch Order and Upload Invoice

```plantuml
@startuml
title 3.2.5.11 Settle Batch Order and Upload Invoice
actor Factory
boundary "Settlement UI" as UI
control FactoryOrderController as Controller
control PaymentService as Service
database "RETRACK Database" as DB
collections "Cloud Storage" as Storage

Factory -> UI: Open verified order settlement
UI -> Controller: GET settlement(orderId)
Controller -> Service: CalculateSettlement(orderId)
Service -> DB: Read verified weight and agreed price
DB --> Service: Weight and price
Service -> Service: subtotal = weight * price
Service -> Service: fee = subtotal * platform fee rate
Service -> Service: depotPayment = subtotal - fee
Service --> Controller: Settlement breakdown
Controller --> UI: Display subtotal, fee and depot payment
Factory -> UI: Confirm settlement
UI -> Controller: POST settlement(orderId)
Controller -> Service: SettleOrder(orderId)
Service -> DB: Save settlement transaction
DB --> Service: Settlement completed
Service --> Controller: Payment recorded
Controller --> UI: Show settlement success

Factory -> UI: Upload VAT invoice
UI -> Storage: Upload invoice file
Storage --> UI: Invoice URL
UI -> Controller: POST invoice(orderId, invoiceData, URL)
Controller -> Service: CreateInvoice(orderId, invoiceData, URL)
Service -> DB: Save Invoice with UPLOADED status
DB --> Service: Invoice saved
Service --> Controller: Upload successful
Controller --> UI: Display invoice
@enduml
```

## 3.2.5.12 Rate and Manage Depot Partnership

```plantuml
@startuml
title 3.2.5.12 Rate and Manage Depot Partnership
actor Factory
boundary "Partner Management UI" as UI
control FactoryPartnerController as Controller
control PartnershipService as Service
database "RETRACK Database" as DB

Factory -> UI: Open depot partners
UI -> Controller: GET partners
Controller -> Service: GetPartners(factoryId)
Service -> DB: Query partnerships and depot ratings
DB --> Service: Partner list
Service --> Controller: Partner list
Controller --> UI: Display partners

alt Rate depot after settlement
  Factory -> UI: Submit rating and cooperation decision
  UI -> Controller: POST rating(depotId, stars, cooperate)
  Controller -> Service: RateDepot(factoryId, depotId, data)
  Service -> DB: Save rating
  Service -> DB: Set APPROVED or BLOCKED
  DB --> Service: Partnership updated
  Service --> Controller: Rating saved
  Controller --> UI: Show updated partner
else Block depot
  Factory -> UI: Click Block
  UI -> Controller: POST block(depotId)
  Controller -> Service: BlockDepot(factoryId, depotId)
  Service -> DB: Set partnership BLOCKED
  Service --> Controller: Depot blocked
  Controller --> UI: Show blocked status
else Unblock depot
  Factory -> UI: Click Unblock
  UI -> Controller: POST unblock(depotId)
  Controller -> Service: UnblockDepot(factoryId, depotId)
  Service -> DB: Set partnership APPROVED
  Service --> Controller: Depot unblocked
  Controller --> UI: Show approved status
end
@enduml
```

## 3.2.5.13 View and Generate EPR Certificate

```plantuml
@startuml
title 3.2.5.13 View and Generate EPR Certificate
actor Factory
boundary "EPR Report UI" as UI
control FactoryEprController as Controller
control EprService as Service
database "RETRACK Database" as DB

Factory -> UI: Open EPR reports
UI -> Controller: GET EPR report(filters)
Controller -> Service: GetEprReport(factoryId, filters)
Service -> DB: Aggregate verified weight by material
DB --> Service: Recycling totals and certificates
Service --> Controller: EPR report
Controller --> UI: Display totals and traceability records

Factory -> UI: Generate certificate for verified order
UI -> Controller: POST certificate(orderId)
Controller -> Service: GenerateCertificate(factoryId, orderId)
Service -> DB: Read verified order and source information
DB --> Service: Verified recycling data
Service -> Service: Generate unique code and hash
Service -> DB: Save EprCertificate
DB --> Service: Certificate saved
Service --> Controller: Certificate details
Controller --> UI: Display downloadable certificate
@enduml
```
