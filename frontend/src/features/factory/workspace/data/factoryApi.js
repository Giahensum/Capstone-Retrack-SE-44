const API_BASE = import.meta.env.VITE_API_BASE_URL || "";
export const TOKEN_KEY = "retrack.accessToken";

export async function request(path, { method = "GET", body, token = localStorage.getItem(TOKEN_KEY) } = {}) {
  const response = await fetch(`${API_BASE}${path}`, {
    method,
    headers: {
      ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  const payload = await response.json().catch(() => null);
  if (!response.ok || payload?.success === false) {
    if (response.status === 401) {
      localStorage.removeItem(TOKEN_KEY);
      window.dispatchEvent(new Event("retrack:unauthorized"));
    }
    throw new Error(payload?.message || `Yêu cầu thất bại (${response.status}).`);
  }
  return payload?.data;
}

export async function login(email, password) {
  const result = await request("/api/auth/login", { method: "POST", body: { email, password }, token: null });
  return {
    accessToken: result.accessToken || result.token,
    user: result.user || {
      id: result.userId,
      role: result.role,
      fullName: result.fullName,
      email: result.email,
    },
  };
}

export async function register({ email, password, fullName, phone }) {
  return request("/api/auth/register", {
    method: "POST",
    body: { email, password, fullName, phone, role: "FACTORY" },
    token: null,
  });
}

const dateOnly = (value) => value?.slice(0, 10) || "";

function mapProfile(profile) {
  const accepted = profile.acceptedMaterials || [];
  return {
    companyName: profile.companyName || "",
    taxCode: profile.taxCode || "",
    address: profile.address || "",
    industrialZone: profile.industrialZone || "",
    phone: profile.contactPhone || profile.user?.phone || "",
    capacity: profile.capacityKgPerMonth || 0,
    purity: profile.minimumPurityPercent || 0,
    latitude: profile.latitude ?? "",
    longitude: profile.longitude ?? "",
    materials: accepted,
    primaryMaterial: accepted[0] || "PET",
    businessLicense: profile.businessLicenseUrl
      ? { name: "Giấy phép kinh doanh", data: profile.businessLicenseUrl }
      : null,
    environmentLicense: profile.environmentalLicenseUrl
      ? { name: "Giấy phép môi trường", data: profile.environmentalLicenseUrl }
      : null,
  };
}

function mapDemand(demand) {
  return {
    id: demand.id,
    material: demand.materialType,
    kg: demand.quantityKg,
    minPrice: demand.minPricePerKg ?? 0,
    maxPrice: demand.maxPricePerKg ?? 0,
    deadline: dateOnly(demand.deadline),
    note: demand.note || "",
    active: demand.isActive,
  };
}

function mapOrder(order) {
  const ticket = order.weightTicket;
  const verification = order.weightVerification;
  const net = verification?.factoryWeightKg || ticket?.netWeightKg || 0;
  const invoice = order.invoice;
  const transportStatus = order.transport?.status;
  const apiStatus = order.status === "COMPLETED" ? "PAID" : order.status;
  const status = ["ACCEPTED", "IN_TRANSIT"].includes(apiStatus) && transportStatus === "DELIVERED"
    ? "DELIVERED"
    : ["ACCEPTED", "IN_TRANSIT"].includes(apiStatus) && ["PICKED_UP", "ON_THE_WAY", "IN_PROGRESS"].includes(transportStatus)
      ? "IN_TRANSIT"
      : apiStatus;
  const normalizedStatus = status === "CANCELLED" ? "REJECTED" : status;
  const statusSteps = normalizedStatus === "REJECTED"
    ? ["ACCEPTED", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "REJECTED"]
    : ["ACCEPTED", "IN_TRANSIT", "DELIVERED", "RECEIVED", "WEIGHED", "VERIFIED", "PAID"];
  const reached = statusSteps.indexOf(normalizedStatus);
  const history = statusSteps.slice(0, reached >= 0 ? reached + 1 : 1).map((itemStatus) => ({
    status: itemStatus,
    at: itemStatus === "RECEIVED" ? order.receivedAt || order.createdAt : itemStatus === "VERIFIED" || itemStatus === "REJECTED" ? order.decidedAt || order.createdAt : itemStatus === "PAID" ? order.settledAt || order.createdAt : order.createdAt,
  }));
  return {
    id: order.id,
    batchId: order.batchCode || order.batchId,
    depotId: order.depotId,
    material: order.materialType,
    kg: order.estimatedWeightKg,
    status: normalizedStatus,
    createdAt: order.createdAt,
    receivedAt: order.receivedAt,
    transport: order.transport,
    rejectionReason: order.rejectionReason,
    history,
    weight: verification || ticket ? {
      gross: ticket?.grossWeightKg || net,
      tare: ticket?.tareWeightKg || 0,
      net,
      difference: verification?.differencePercentage || 0,
      flagged: Math.abs(verification?.differencePercentage || 0) > 5,
      note: verification?.note || "",
      attachment: ticket?.ticketImageUrl ? { name: ticket.ticketNumber || "Phiếu cân", data: ticket.ticketImageUrl } : null,
    } : null,
    qc: verification?.grade ? {
      purity: verification.purityPercent,
      moisture: verification.moisturePercent,
      contamination: verification.contaminationPercent,
      grade: verification.grade,
      decision: verification.isVerified ? "accept" : "reject",
      note: verification.qualityNote || order.rejectionReason || "",
      resolution: verification.qualityNote?.startsWith("RENEGOTIATE:") ? "RENEGOTIATE" : "RETURN",
    } : null,
    payment: order.settledAt ? {
      price: order.agreedPrice,
      total: order.totalAmount || net * order.agreedPrice,
      fee: order.feeAmount || 0,
      payable: order.netPayableAmount || 0,
      reference: order.paymentReference || "",
      at: order.settledAt,
    } : null,
    invoice: invoice?.invoiceFileUrl ? {
      name: invoice.invoiceNumber || "Hóa đơn",
      data: invoice.invoiceFileUrl,
      invoiceNumber: invoice.invoiceNumber,
      status: invoice.status,
    } : null,
    rating: order.rating?.rating ? { stars: order.rating.rating, comment: order.rating.comment || "" } : null,
    depotName: order.depotName,
    depotPhone: order.depotPhone,
    depotAddress: order.depotAddress,
  };
}

export async function loadFactoryState(token) {
  const [profileResult, demandsResult, ordersResult, marketplaceResult, directOffersResult, pricesResult, partnersResult] = await Promise.all([
    request("/api/factory/profile", { token }),
    request("/api/factory/demands?page=1&pageSize=100", { token }),
    request("/api/factory/orders?page=1&pageSize=100", { token }),
    request("/api/factory/marketplace/batches?page=1&pageSize=100", { token }),
    request("/api/factory/marketplace/batches?page=1&pageSize=100&directOnly=true", { token }),
    request("/api/factory/marketplace/prices", { token }),
    request("/api/factory/partners?page=1&pageSize=100", { token }),
  ]);
  const partners = partnersResult.items || [];
  const batches = [...(marketplaceResult.items || []), ...(directOffersResult.items || [])].map((batch) => ({
    id: batch.id,
    batchCode: batch.batchCode,
    depotId: batch.depot.id,
    material: batch.materialType,
    kg: batch.estimatedWeightKg,
    direct: batch.isDirectOffer,
    status: batch.status || "LISTED",
    createdAt: batch.createdAt,
    note: batch.description || "",
    imageUrl: batch.thumbnailImageUrl,
  }));
  const depotsById = new Map();
  for (const partner of partners) depotsById.set(partner.depotId, {
    id: partner.depotId,
    name: partner.name,
    address: partner.address || "",
    phone: partner.contactPhone || "",
    status: partner.status,
    distance: null,
    rating: partner.latestRating || partner.rating,
    comment: partner.latestComment || "",
    orderCount: partner.orderCount,
  });
  for (const batch of [...(marketplaceResult.items || []), ...(directOffersResult.items || [])]) {
    if (!depotsById.has(batch.depot.id)) depotsById.set(batch.depot.id, {
      id: batch.depot.id,
      name: batch.depot.companyName,
      address: batch.depot.address || "",
      phone: batch.depot.contactPhone || "",
      status: "PENDING",
      distance: null,
    });
  }
  const orders = (ordersResult.items || []).map(mapOrder);
  const prices = pricesResult.map((price) => ({
    material: price.materialType,
    price: price.pricePerKg,
    date: dateOnly(price.effectiveDate),
    source: price.source || "",
  }));
  return {
    version: 2,
    profile: mapProfile(profileResult),
    depots: [...depotsById.values()],
    batches,
    orders,
    demands: (demandsResult.items || []).map(mapDemand),
    prices,
  };
}

export async function performFactoryAction(type, payload) {
  const id = encodeURIComponent(payload.id || "");
  switch (type) {
    case "PROFILE":
      return request("/api/factory/profile", { method: "PUT", body: {
        companyName: payload.companyName,
        taxCode: payload.taxCode,
        address: payload.address,
        industrialZone: payload.industrialZone,
        contactPhone: payload.phone,
        latitude: payload.latitude === "" ? null : Number(payload.latitude),
        longitude: payload.longitude === "" ? null : Number(payload.longitude),
        capacityKgPerMonth: Number(payload.capacity),
        minimumPurityPercent: Number(payload.purity),
        acceptedMaterials: payload.materials,
        businessLicenseUrl: payload.businessLicense?.data || null,
        environmentalLicenseUrl: payload.environmentLicense?.data || null,
      } });
    case "SAVE_DEMAND":
      return request(`/api/factory/demands${payload.id ? `/${id}` : ""}`, {
        method: payload.id ? "PUT" : "POST",
        body: {
          materialType: payload.material,
          quantityKg: Number(payload.kg),
          minPricePerKg: Number(payload.minPrice),
          maxPricePerKg: Number(payload.maxPrice),
          deadline: payload.deadline ? new Date(`${payload.deadline}T23:59:59Z`).toISOString() : null,
          isActive: payload.active,
          note: payload.note,
        },
      });
  case "TOGGLE_DEMAND":
      return request(`/api/factory/demands/${id}/status`, { method: "PATCH", body: { isActive: payload.isActive } });
    case "DELETE_DEMAND":
      return request(`/api/factory/demands/${id}`, { method: "DELETE" });
    case "ACCEPT_BATCH":
      return request(`/api/factory/marketplace/batches/${id}/accept`, { method: "POST", body: { agreedPricePerKg: 0 } });
    case "REJECT_OFFER":
      return request(`/api/factory/marketplace/offers/${id}/reject`, { method: "POST", body: { reason: payload.reason } });
    case "RECEIVE":
      return request(`/api/factory/orders/${id}/receive`, { method: "POST", body: {} });
    case "WEIGH":
      return request(`/api/factory/qc/orders/${id}/weigh`, { method: "POST", body: {
        grossWeightKg: Number(payload.gross),
        tareWeightKg: Number(payload.tare),
        ticketNumber: payload.ticketNumber || null,
        ticketImageUrl: payload.attachment?.data || null,
        note: payload.note,
      } });
    case "QC":
      return request(`/api/factory/qc/orders/${id}/quality`, { method: "POST", body: {
        accept: payload.decision === "accept",
        purityPercent: Number(payload.purity),
        moisturePercent: Number(payload.moisture),
        contaminationPercent: Number(payload.contamination),
        grade: payload.grade,
        note: payload.note,
        resolution: payload.decision === "reject" ? payload.resolution : null,
      } });
    case "SETTLE":
      return request(`/api/factory/orders/${id}/settle`, { method: "POST", body: {
        agreedPricePerKg: Number(payload.price),
        paymentReference: payload.reference,
      } });
    case "INVOICE":
      return request(`/api/factory/orders/${id}/invoice`, { method: "PUT", body: {
        invoiceNumber: payload.file.invoiceNumber || payload.file.name,
        invoiceFileUrl: payload.file.data,
        vatAmount: 0,
      } });
    case "RATE":
      return request(`/api/factory/partners/orders/${id}/rating`, { method: "POST", body: {
        rating: Number(payload.stars),
        comment: payload.comment,
        blockPartner: false,
      } });
    case "PARTNER_STATUS":
      return request(`/api/factory/partners/${encodeURIComponent(payload.id)}/status`, { method: "PUT", body: { status: payload.status } });
    case "SIMULATE_TRANSPORT":
      throw new Error("Trạng thái vận chuyển do luồng tài xế cập nhật; Factory không thể tự mô phỏng trên dữ liệu máy chủ.");
    default:
      throw new Error("Thao tác này chưa được nối với API.");
  }
}
