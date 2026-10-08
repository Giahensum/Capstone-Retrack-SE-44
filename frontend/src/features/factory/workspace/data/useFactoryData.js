import { useQuery } from "@tanstack/react-query";
import { mapDemand, mapOrder, request } from "./factoryApi";

const paths = {
  demands: "/api/factory/demands",
  orders: "/api/factory/orders",
  marketplace: "/api/factory/marketplace/batches",
  partners: "/api/factory/partners",
};

function mapBatch(batch) {
  return {
    id: batch.id,
    batchCode: batch.batchCode,
    depotId: batch.depot.id,
    depotName: batch.depot.companyName,
    depotAddress: batch.depot.address || "",
    depotPhone: batch.depot.contactPhone || "",
    material: batch.materialType,
    kg: batch.estimatedWeightKg,
    direct: batch.isDirectOffer,
    status: batch.status || "LISTED",
    createdAt: batch.createdAt,
    note: batch.description || "",
    imageUrl: batch.thumbnailImageUrl,
    imageUrls: batch.imageUrls || [],
  };
}

function mapPartner(partner) {
  return {
    id: partner.depotId,
    name: partner.name,
    address: partner.address || "",
    phone: partner.contactPhone || "",
    status: partner.status,
    blockedByFactory: partner.blockedByFactory,
    blockedByDepot: partner.blockedByDepot,
    legacyBlocked: partner.legacyBlocked,
    rating: partner.latestRating ?? partner.rating,
    comment: partner.latestComment || "",
    reviewCount: partner.reviewCount,
    orderCount: partner.orderCount,
    completedOrderCount: partner.completedOrderCount,
  };
}

const mappers = { demands: mapDemand, orders: mapOrder, marketplace: mapBatch, partners: mapPartner };

export function useFactoryPage(kind, options = {}) {
  const query = { page: 1, pageSize: 20, ...options };
  return useQuery({
    queryKey: ["factory", kind, query],
    queryFn: async () => {
      const params = new URLSearchParams();
      for (const [key, value] of Object.entries(query)) {
        if (value !== "" && value !== null && value !== undefined) params.set(key, String(value));
      }
      const result = await request(`${paths[kind]}?${params}`);
      return { ...result, items: (result.items || []).map(mappers[kind]) };
    },
    placeholderData: (previous) => previous,
  });
}

export function useFactoryDashboard(period = "month") {
  return useQuery({
    queryKey: ["factory", "dashboard", period],
    queryFn: () => request(`/api/factory/dashboard?period=${encodeURIComponent(period)}`),
  });
}

export function useFactoryOrder(id) {
  return useQuery({
    queryKey: ["factory", "order", id],
    queryFn: async () => mapOrder(await request(`/api/factory/orders/${encodeURIComponent(id)}`)),
    enabled: Boolean(id),
  });
}
