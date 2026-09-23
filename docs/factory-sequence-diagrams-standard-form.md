# Factory Sequence Diagrams - Standard Form

Moi khoi ma la mot sequence diagram rieng. Sao chep tu `@startuml` den `@enduml` va dan vao Visual Paradigm AI Diagram Generation.

## 3.2.5.1 Hoan thien ho so nha may

```plantuml
@startuml
title 3.2.5.1 HOAN THIEN HO SO NHA MAY
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "FactoryProfileForm" as Form
control "FactoryProfileService" as Service
database "AppDbContext" as DB
control "CloudStorageService" as Storage

== 1. TAI THONG TIN HO SO ==
Factory -> Form: Mo trang ho so
activate Form
Form -> Service: GetProfileAsync(factoryId)
activate Service
Service -> DB: Factories.FindAsync(factoryId)
activate DB
DB --> Service: FactoryProfile
deactivate DB
Service --> Form: Hien thi thong tin hien tai
deactivate Service

== 2. CAP NHAT HO SO ==
Factory -> Form: Nhap thong tin va chon giay phep
Form -> Storage: UploadAsync(licenseFiles)
activate Storage
Storage --> Form: FileUrls
deactivate Storage
Form -> Service: UpdateProfileAsync(data, fileUrls)
activate Service
Service -> Service: Validate tax code va du lieu
alt Du lieu hop le
  Service -> DB: Update Factory
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service --> Form: Cap nhat thanh cong
else Du lieu khong hop le
  Service --> Form: Hien thi validation errors
end
deactivate Service
deactivate Form
@enduml
```

## 3.2.5.2 Xem dashboard nha may

```plantuml
@startuml
title 3.2.5.2 XEM DASHBOARD NHA MAY
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "FactoryDashboard" as Dashboard
control "FactoryDashboardService" as Service
database "AppDbContext" as DB

== 1. YEU CAU DASHBOARD ==
Factory -> Dashboard: Mo dashboard
activate Dashboard
Dashboard -> Service: GetDashboardAsync(factoryId, dateRange)
activate Service

== 2. TONG HOP DU LIEU ==
Service -> DB: Dem don theo trang thai
activate DB
DB --> Service: OrderSummary
deactivate DB
Service -> DB: Tong hop khoi luong va chi phi
activate DB
DB --> Service: PurchaseStatistics
deactivate DB
Service -> DB: Lay request va don cho QC
activate DB
DB --> Service: PendingTasks
deactivate DB

== 3. HIEN THI KET QUA ==
Service --> Dashboard: FactoryDashboardDto
deactivate Service
Dashboard --> Factory: Hien thi card va bieu do
deactivate Dashboard
@enduml
```

## 3.2.5.3 Dang va quan ly nhu cau thu mua

```plantuml
@startuml
title 3.2.5.3 DANG VA QUAN LY NHU CAU THU MUA
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "FactoryDemandForm" as Form
control "FactoryDemandService" as Service
database "AppDbContext" as DB
actor "Kho vua" as Depot

== 1. DANG NHU CAU THU MUA ==
Factory -> Form: Nhap vat lieu, so luong, gia va deadline
activate Form
Form -> Service: CreateDemandAsync(factoryId, data)
activate Service
Service -> Service: Validate quantity va deadline
alt Du lieu hop le
  Service -> DB: FactoryDemands.Add(newDemand)
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service --> Form: Dang nhu cau thanh cong
else Du lieu khong hop le
  Service --> Form: Hien thi validation errors
end
deactivate Service
deactivate Form

== 2. KHO VUA XEM DEMAND BOARD ==
Depot -> Service: GetActiveDemands(filters)
activate Service
Service -> DB: Query active FactoryDemands
activate DB
DB --> Service: DemandList
deactivate DB
Service --> Depot: Hien thi nhu cau nha may
deactivate Service

== 3. SUA HOAC DONG NHU CAU ==
opt Nha may thay doi nhu cau
  Factory -> Form: Sua thong tin hoac tat nhu cau
  activate Form
  Form -> Service: UpdateDemandAsync(demandId, changes)
  activate Service
  Service -> DB: Update FactoryDemand
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service --> Form: Cap nhat thanh cong
  deactivate Service
  deactivate Form
end
@enduml
```

## 3.2.5.4 Duyet va nhan lo tren Marketplace

```plantuml
@startuml
title 3.2.5.4 DUYET VA NHAN LO TREN MARKETPLACE
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "MarketplacePage" as Page
control "FactoryMarketService" as Service
database "AppDbContext" as DB
control "NotificationService" as Notification
actor "Kho vua" as Depot

== 1. DUYET CAC LO DANG BAN ==
Factory -> Page: Mo Marketplace va chon bo loc
activate Page
Page -> Service: BrowseListedBatchesAsync(filters)
activate Service
Service -> DB: Query batch co status LISTED
activate DB
DB --> Service: BatchList
deactivate DB
Service --> Page: Hien thi cac lo kha dung
deactivate Service

== 2. NHAN LO HANG ==
Factory -> Page: Chon lo va bam Nhan lo
Page -> Service: AcceptBatchAsync(factoryId, batchId)
activate Service
Service -> DB: Lock va kiem tra trang thai lo
activate DB
alt Lo van con LISTED
  Service -> DB: Tao BatchOrder, dat ACCEPTED
  DB --> Service: SaveChangesAsync()
  Service -> Notification: NotifyDepotBatchAccepted(orderId)
  activate Notification
  Notification --> Depot: Nha may da nhan lo
  deactivate Notification
  Service --> Page: Hien thi xac nhan don hang
else Lo da duoc nha may khac nhan
  DB --> Service: BatchUnavailable
  Service --> Page: Thong bao lo khong con kha dung
end
deactivate DB
deactivate Service
deactivate Page
@enduml
```

## 3.2.5.5 Duyet request chi dinh tu Depot

```plantuml
@startuml
title 3.2.5.5 DUYET REQUEST CHI DINH TU DEPOT
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "DirectRequestPage" as Page
control "FactoryMarketService" as Service
database "AppDbContext" as DB
control "NotificationService" as Notification
actor "Kho vua" as Depot

== 1. XEM REQUEST DANG CHO ==
Factory -> Page: Mo danh sach request
activate Page
Page -> Service: GetPendingRequestsAsync(factoryId)
activate Service
Service -> DB: Query request PENDING
activate DB
DB --> Service: PendingRequests
deactivate DB
Service --> Page: Hien thi request va chi tiet lo
deactivate Service

== 2. DUYET REQUEST ==
alt Nha may dong y
  Factory -> Page: Bam Approve
  Page -> Service: ApproveRequestAsync(requestId)
  activate Service
  Service -> DB: Tao order, dat ACCEPTED va APPROVED
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service -> Notification: Gui thong bao chap nhan
  activate Notification
  Notification --> Depot: Request duoc chap nhan
  deactivate Notification
  Service --> Page: Hien thi don hang moi
  deactivate Service
else Nha may tu choi
  Factory -> Page: Nhap ly do va bam Reject
  Page -> Service: RejectRequestAsync(requestId, reason)
  activate Service
  Service -> DB: Dat request REJECTED
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service -> Notification: Gui ly do tu choi
  activate Notification
  Notification --> Depot: Request bi tu choi
  deactivate Notification
  Service --> Page: Hien thi ket qua
  deactivate Service
end
deactivate Page
@enduml
```

## 3.2.5.6 Theo doi van chuyen

```plantuml
@startuml
title 3.2.5.6 THEO DOI VAN CHUYEN LO HANG
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Tai xe" as Driver
control "TransportService" as Transport
database "AppDbContext" as DB
boundary "TrackingPage" as Page
actor "Nha may" as Factory

== 1. CAP NHAT HANH TRINH ==
loop Moi diem check-in hoac thay doi trang thai
  Driver -> Transport: UpdateTracking(jobId, status, GPS)
  activate Transport
  Transport -> DB: Add TransportTrackingLog
  activate DB
  DB --> Transport: SaveChangesAsync()
  deactivate DB
  Transport --> Driver: Cap nhat thanh cong
  deactivate Transport
end

== 2. NHA MAY THEO DOI ==
Factory -> Page: Mo trang tracking
activate Page
Page -> Transport: GetLatestTrackingAsync(orderId)
activate Transport
Transport -> DB: Lay TransportJob va log moi nhat
activate DB
DB --> Transport: Status, GPS va timestamps
deactivate DB
Transport --> Page: Hien thi timeline va ban do
deactivate Transport
deactivate Page

== 3. HOAN TAT GIAO HANG ==
Driver -> Transport: ConfirmDelivered(jobId)
activate Transport
Transport -> DB: Dat DELIVERED va DeliveredTime
activate DB
DB --> Transport: SaveChangesAsync()
deactivate DB
Transport --> Driver: Hoan tat chuyen
deactivate Transport
@enduml
```

## 3.2.5.7 Xac nhan nhan hang

```plantuml
@startuml
title 3.2.5.7 XAC NHAN NHAN HANG TAI NHA MAY
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "ReceivingPage" as Page
control "QCService" as Service
database "AppDbContext" as DB

== 1. KIEM TRA LO DA GIAO ==
Factory -> Page: Mo don hang vua giao
activate Page
Page -> Service: GetReceivingDetailsAsync(orderId)
activate Service
Service -> DB: Doc order va transport status
activate DB
DB --> Service: ReceivingDetails
deactivate DB
Service --> Page: Hien thi thong tin xe va lo
deactivate Service

== 2. XAC NHAN XE DEN ==
Factory -> Page: Bam Xac nhan nhan hang
Page -> Service: ConfirmReceivingAsync(orderId)
activate Service
Service -> DB: Kiem tra transport status
activate DB
alt Status la DELIVERED
  Service -> DB: Luu ReceivingTime, dat ReadyForQC
  DB --> Service: SaveChangesAsync()
  Service --> Page: Chuyen sang buoc can doi trong
else Chua giao den nha may
  DB --> Service: InvalidTransportStatus
  Service --> Page: Hien thi loi
end
deactivate DB
deactivate Service
deactivate Page
@enduml
```

## 3.2.5.8 Can doi trong

```plantuml
@startuml
title 3.2.5.8 CAN DOI TRONG TAI NHA MAY
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "WeighbridgeForm" as Form
control "QCService" as Service
database "AppDbContext" as DB
control "CloudStorageService" as Storage

== 1. GHI NHAN PHIEU CAN ==
Factory -> Form: Nhap gross weight va tare weight
Factory -> Form: Chon anh phieu can
activate Form
Form -> Storage: UploadAsync(ticketImage)
activate Storage
Storage --> Form: TicketImageUrl
deactivate Storage
Form -> Service: CreateWeightTicketAsync(orderId, data)
activate Service
Service -> Service: NetWeight = GrossWeight - TareWeight

== 2. DOI CHIEU TRONG LUONG ==
Service -> DB: Lay EstimatedWeightKg cua Depot
activate DB
DB --> Service: EstimatedWeightKg
deactivate DB
Service -> Service: Tinh DifferencePercentage
Service -> DB: Luu WeightTicket va WeightVerification
activate DB
DB --> Service: SaveChangesAsync()
deactivate DB
alt Chenh lech lon hon 5 phan tram
  Service --> Form: Hien thi canh bao chenh lech
else Chenh lech chap nhan duoc
  Service --> Form: Chuyen sang kiem tra chat luong
end
deactivate Service
deactivate Form
@enduml
```

## 3.2.5.9 Kiem tra chat luong

```plantuml
@startuml
title 3.2.5.9 KIEM TRA CHAT LUONG VA PHAN LOAI
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "QualityInspectionForm" as Form
control "QCService" as Service
database "AppDbContext" as DB
control "CloudStorageService" as Storage

== 1. TAI DU LIEU KIEM TRA ==
Factory -> Form: Mo form QC
activate Form
Form -> Service: GetQCInformationAsync(orderId)
activate Service
Service -> DB: Lay batch va ket qua can
activate DB
DB --> Service: QCInputData
deactivate DB
Service --> Form: Hien thi thong tin lo
deactivate Service

== 2. LUU BAO CAO CHAT LUONG ==
Factory -> Form: Nhap purity, moisture, contamination, grade
Factory -> Form: Them ghi chu va anh minh chung
Form -> Storage: UploadAsync(evidenceImages)
activate Storage
Storage --> Form: EvidenceUrls
deactivate Storage
Form -> Service: SaveQualityReportAsync(orderId, report)
activate Service
Service -> Service: Validate percentages va grade
alt Bao cao hop le
  Service -> DB: Add QualityInspectionReport
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service --> Form: Chuyen sang quyet dinh QC
else Bao cao khong hop le
  Service --> Form: Hien thi validation errors
end
deactivate Service
deactivate Form
@enduml
```

## 3.2.5.10 Chap nhan hoac tu choi lo

```plantuml
@startuml
title 3.2.5.10 CHAP NHAN HOAC TU CHOI LO HANG
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "QCDecisionPage" as Page
control "QCService" as Service
database "AppDbContext" as DB
control "NotificationService" as Notification
actor "Kho vua" as Depot

== 1. XEM TONG HOP QC ==
Factory -> Page: Xem ket qua can va chat luong
activate Page
Page -> Service: GetQCSummaryAsync(orderId)
activate Service
Service -> DB: Lay WeightVerification va QualityReport
activate DB
DB --> Service: QCSummary
deactivate DB
Service --> Page: Hien thi ket qua
deactivate Service

== 2. RA QUYET DINH ==
alt Chap nhan lo
  Factory -> Page: Bam Accept
  Page -> Service: AcceptBatchAsync(orderId)
  activate Service
  Service -> DB: Dat order va batch VERIFIED
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service -> Notification: NotifyBatchAccepted(orderId)
  activate Notification
  Notification --> Depot: Lo hang da duoc chap nhan
  deactivate Notification
  Service --> Page: Chuyen sang quyet toan
  deactivate Service
else Tu choi lo
  Factory -> Page: Nhap ly do va bam Reject
  Page -> Service: RejectBatchAsync(orderId, reason)
  activate Service
  Service -> DB: Dat REJECTED va luu ly do
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service -> Notification: Gui ly do tu choi
  activate Notification
  Notification --> Depot: Lo hang bi tu choi
  deactivate Notification
  Service --> Page: Hien thi ket qua tu choi
  deactivate Service
end
deactivate Page
@enduml
```

## 3.2.5.11 Quyet toan va hoa don

```plantuml
@startuml
title 3.2.5.11 QUYET TOAN VA UPLOAD HOA DON
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "SettlementPage" as Page
control "PaymentService" as Service
database "AppDbContext" as DB
control "CloudStorageService" as Storage

== 1. TINH SO TIEN QUYET TOAN ==
Factory -> Page: Mo don hang VERIFIED
activate Page
Page -> Service: CalculateSettlementAsync(orderId)
activate Service
Service -> DB: Lay verified weight va agreed price
activate DB
DB --> Service: WeightAndPrice
deactivate DB
Service -> Service: Subtotal = Weight x Price
Service -> Service: Fee = Subtotal x FeeRate
Service -> Service: DepotPayment = Subtotal - Fee
Service --> Page: Hien thi tong goc, phi va thuc tra
deactivate Service

== 2. XAC NHAN QUYET TOAN ==
Factory -> Page: Bam Xac nhan
Page -> Service: SettleOrderAsync(orderId)
activate Service
Service -> DB: Luu transaction va cong no phi
activate DB
DB --> Service: SaveChangesAsync()
deactivate DB
Service --> Page: Quyet toan thanh cong
deactivate Service

== 3. UPLOAD HOA DON VAT ==
Factory -> Page: Chon tep hoa don
Page -> Storage: UploadAsync(invoiceFile)
activate Storage
Storage --> Page: InvoiceFileUrl
deactivate Storage
Page -> Service: CreateInvoiceAsync(orderId, fileUrl)
activate Service
Service -> DB: Add Invoice status UPLOADED
activate DB
DB --> Service: SaveChangesAsync()
deactivate DB
Service --> Page: Hien thi hoa don
deactivate Service
deactivate Page
@enduml
```

## 3.2.5.12 Danh gia va quan ly doi tac

```plantuml
@startuml
title 3.2.5.12 DANH GIA VA QUAN LY DOI TAC DEPOT
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "PartnerManagementPage" as Page
control "PartnershipService" as Service
database "AppDbContext" as DB
control "NotificationService" as Notification
actor "Kho vua" as Depot

== 1. XEM DANH SACH DOI TAC ==
Factory -> Page: Mo danh sach doi tac
activate Page
Page -> Service: GetPartnersAsync(factoryId)
activate Service
Service -> DB: Lay partnership va rating
activate DB
DB --> Service: PartnerList
deactivate DB
Service --> Page: Hien thi APPROVED va BLOCKED
deactivate Service

== 2. DANH GIA SAU QUYET TOAN ==
Factory -> Page: Gui so sao va quyet dinh hop tac
Page -> Service: RateDepotAsync(depotId, stars, cooperate)
activate Service
Service -> DB: Luu rating va partnership status
activate DB
DB --> Service: SaveChangesAsync()
deactivate DB
Service -> Notification: Gui ket qua danh gia
activate Notification
Notification --> Depot: Quan he hop tac da cap nhat
deactivate Notification
Service --> Page: Hien thi trang thai moi
deactivate Service

== 3. CHAN HOAC BO CHAN ==
opt Nha may thay doi trang thai doi tac
  Factory -> Page: Bam Block hoac Unblock
  Page -> Service: ChangeStatusAsync(depotId, status)
  activate Service
  Service -> DB: Update PartnershipStatus
  activate DB
  DB --> Service: SaveChangesAsync()
  deactivate DB
  Service --> Page: Lam moi danh sach
  deactivate Service
end
deactivate Page
@enduml
```

## 3.2.5.13 Xem va tao chung nhan EPR

```plantuml
@startuml
title 3.2.5.13 XEM VA TAO CHUNG NHAN EPR
autonumber 1
skinparam sequenceMessageAlign center
skinparam ArrowColor #555260
skinparam LifeLineBorderColor #B9B6C7
skinparam ParticipantBorderColor #8E8A9F
skinparam ParticipantBackgroundColor #E8E5F2
skinparam ActorBorderColor #8E8A9F
skinparam ActorBackgroundColor #E8E5F2
skinparam DatabaseBorderColor #8E8A9F
skinparam DatabaseBackgroundColor #DCD8EC
skinparam ControlBorderColor #8E8A9F
skinparam ControlBackgroundColor #E8E5F2
skinparam BoundaryBorderColor #8798AA
skinparam BoundaryBackgroundColor #E5EEF5
actor "Nha may" as Factory
boundary "EprReportPage" as Page
control "EprService" as Service
database "AppDbContext" as DB

== 1. XEM BAO CAO EPR ==
Factory -> Page: Mo bao cao va chon bo loc
activate Page
Page -> Service: GetEprReportAsync(factoryId, filters)
activate Service
Service -> DB: Tong hop kg VERIFIED theo vat lieu
activate DB
DB --> Service: RecyclingTotals va Certificates
deactivate DB
Service --> Page: Hien thi bao cao va truy xuat
deactivate Service

== 2. PHAT HANH CHUNG NHAN ==
Factory -> Page: Chon don va bam Tao certificate
Page -> Service: GenerateCertificateAsync(orderId)
activate Service
Service -> DB: Lay don VERIFIED va nguon goc lo
activate DB
DB --> Service: VerifiedRecyclingData
deactivate DB
Service -> Service: Tao CertificateCode va HashValue
Service -> DB: Add EprCertificate
activate DB
DB --> Service: SaveChangesAsync()
deactivate DB
Service --> Page: Hien thi chung nhan de tai
deactivate Service
deactivate Page
@enduml
```
