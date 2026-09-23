export const STORAGE_KEY = "retrack.factory.v2";
export const materials = {
  PET: "Nhựa PET",
  HDPE: "Nhựa HDPE",
  PVC: "Nhựa PVC",
  PAPER: "Giấy vụn",
  CARDBOARD: "Giấy carton",
  ALUMINUM: "Nhôm",
  IRON: "Sắt",
  STEEL: "Thép",
  COPPER: "Đồng",
  ELECTRONIC_WASTE: "Phế liệu điện tử",
  OTHER: "Khác",
};
export const labels = {
  ACCEPTED: "Chờ tài xế",
  IN_TRANSIT: "Đang vận chuyển",
  DELIVERED: "Đã giao • Chờ nhận",
  RECEIVED: "Chờ cân",
  WEIGHED: "Chờ KCS",
  VERIFIED: "Chờ quyết toán",
  REJECTED: "Từ chối nhận",
  PAID: "Đã quyết toán",
  APPROVED: "Đã duyệt",
  PENDING: "Chờ duyệt",
  BLOCKED: "Đã chặn",
};
export const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value || 0);
export const number = (value) =>
  new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(
    value || 0,
  );
export const stamp = () => new Date().toISOString();
export const today = () => new Date().toLocaleDateString("en-CA");
const id = (prefix) => `${prefix}-${crypto.randomUUID().slice(0, 8)}`;
const requireThat = (ok, message) => {
  if (!ok) throw new Error(message);
};
const positive = (n) => Number.isFinite(Number(n)) && Number(n) > 0;
export function weigh(gross, tare, estimated) {
  requireThat(
    positive(gross) &&
      Number.isFinite(Number(tare)) &&
      Number(tare) >= 0 &&
      Number(gross) > Number(tare),
    "Số cân phải hợp lệ: Gross > Tare ≥ 0.",
  );
  requireThat(positive(estimated), "Thiếu khối lượng vựa khai báo.");
  const net = Math.round((Number(gross) - Number(tare)) * 100) / 100;
  const difference = ((net - estimated) / estimated) * 100;
  return {
    gross: Number(gross),
    tare: Number(tare),
    net,
    difference,
    flagged: Math.abs(difference) > 5,
  };
}
export function settlement(net, price) {
  requireThat(
    positive(net) && positive(price),
    "Khối lượng và đơn giá phải lớn hơn 0.",
  );
  const total = Math.round(net * price);
  const fee = Math.round(total * 0.05);
  return { price: Number(price), total, fee, payable: total - fee };
}
export function createInitialState() {
  const now = stamp();
  const depots = [
    {
      id: "D1",
      name: "Vựa Xanh Thủ Đức",
      address: "Linh Trung, TP. Thủ Đức",
      phone: "0908 112 334",
      status: "APPROVED",
      distance: 28,
    },
    {
      id: "D2",
      name: "Kho Bình Chánh",
      address: "An Phú Tây, Bình Chánh",
      phone: "0903 554 112",
      status: "PENDING",
      distance: 16,
    },
    {
      id: "D3",
      name: "Vựa Tân Phú",
      address: "Tân Sơn Nhì, Tân Phú",
      phone: "0902 441 228",
      status: "APPROVED",
      distance: 32,
    },
  ];
  const batch = (key, depotId, material, kg, direct = false) => ({
    id: key,
    depotId,
    material,
    kg,
    direct,
    status: "LISTED",
    createdAt: now,
    note: "Đóng kiện, giao tại nhà máy. Giá được thỏa thuận sau KCS.",
  });
  const order = (key, status, depotId, material, kg) => ({
    id: key,
    batchId: `LO-${key.slice(3)}`,
    depotId,
    material,
    kg,
    status,
    createdAt: now,
    history: [
      { status: "ACCEPTED", at: now },
      ...(status !== "ACCEPTED"
        ? [
            { status: "IN_TRANSIT", at: now },
            { status: "DELIVERED", at: now },
          ]
        : []),
    ],
  });
  const delivered = order("DH-1001", "DELIVERED", "D1", "PET", 8200);
  const verified = {
    ...order("DH-1002", "VERIFIED", "D2", "CARDBOARD", 6000),
    receivedAt: now,
    weight: weigh(12000, 6100, 6000),
    qc: {
      purity: 96,
      moisture: 2,
      contamination: 2,
      grade: "A",
      decision: "accept",
      note: "",
    },
  };
  verified.history.push(
    ...["RECEIVED", "WEIGHED", "VERIFIED"].map((status) => ({
      status,
      at: now,
    })),
  );
  const paid = {
    ...order("DH-1003", "PAID", "D3", "IRON", 5000),
    receivedAt: now,
    weight: weigh(11000, 6000, 5000),
    qc: {
      purity: 97,
      moisture: 1,
      contamination: 2,
      grade: "A",
      decision: "accept",
      note: "",
    },
    payment: { ...settlement(5000, 8500), reference: "DEMO-1003", at: now },
  };
  paid.history.push(
    ...["RECEIVED", "WEIGHED", "VERIFIED", "PAID"].map((status) => ({
      status,
      at: now,
    })),
  );
  return {
    version: 2,
    profile: {
      companyName: "GreenCycle Việt Nam",
      taxCode: "0314892841",
      address: "KCN Hiệp Phước, Nhà Bè, TP. Hồ Chí Minh",
      industrialZone: "Hiệp Phước",
      phone: "028 3780 1122",
      latitude: "10.62",
      longitude: "106.74",
      capacity: 4500000,
      purity: 95,
      primaryMaterial: "PET",
      materials: ["PET", "HDPE", "CARDBOARD", "IRON"],
    },
    depots,
    batches: [
      batch("LO-2001", "D1", "PET", 12000),
      batch("LO-2002", "D3", "IRON", 9000),
      batch("LO-2003", "D2", "CARDBOARD", 15000, true),
    ],
    orders: [delivered, verified, paid],
    demands: [
      {
        id: "NC-1001",
        material: "PET",
        kg: 50000,
        minPrice: 10000,
        maxPrice: 12000,
        deadline: new Date(Date.now() + 14 * 86400000)
          .toISOString()
          .slice(0, 10),
        note: "Ép kiện, không lẫn PVC.",
        active: true,
      },
    ],
    prices: [
      { material: "PET", price: 11500 },
      { material: "CARDBOARD", price: 3900 },
      { material: "IRON", price: 8500 },
      { material: "HDPE", price: 14200 },
      { material: "COPPER", price: 165000 },
    ].map((p) => ({
      ...p,
      date: now.slice(0, 10),
      source: "Bảng giá minh họa",
    })),
  };
}
export function transition(state, action) {
  const next = structuredClone(state);
  const { type, payload: p = {} } = action;
  const order = next.orders.find((o) => o.id === p.id);
  const advance = (status) => {
    order.status = status;
    order.history.push({ status, at: stamp() });
  };
  if (
    [
      "RECEIVE",
      "WEIGH",
      "QC",
      "SETTLE",
      "RATE",
      "INVOICE",
      "SIMULATE_TRANSPORT",
    ].includes(type)
  )
    requireThat(order, "Không tìm thấy đơn hàng.");
  switch (type) {
    case "ACCEPT_BATCH": {
      const b = next.batches.find((b) => b.id === p.id);
      requireThat(
        b?.status === "LISTED",
        "Lô này đã được nhận hoặc không còn khả dụng.",
      );
      const depot = next.depots.find((d) => d.id === b.depotId);
      requireThat(
        depot.status !== "BLOCKED",
        "Vựa đang bị chặn. Hãy xem lại quan hệ đối tác.",
      );
      b.status = "ACCEPTED";
      if (b.direct) depot.status = "APPROVED";
      next.orders.unshift({
        id: id("DH"),
        batchId: b.id,
        depotId: b.depotId,
        material: b.material,
        kg: b.kg,
        status: "ACCEPTED",
        createdAt: stamp(),
        history: [{ status: "ACCEPTED", at: stamp() }],
      });
      break;
    }
    case "REJECT_OFFER": {
      const b = next.batches.find((b) => b.id === p.id);
      requireThat(
        b?.direct && b.status === "LISTED",
        "Yêu cầu không còn khả dụng.",
      );
      requireThat(p.reason?.trim(), "Nhập lý do từ chối.");
      b.status = "REJECTED";
      b.reason = p.reason.trim();
      break;
    }
    case "SIMULATE_TRANSPORT":
      requireThat(
        ["ACCEPTED", "IN_TRANSIT"].includes(order.status),
        "Chuyến đã giao hoặc không thể cập nhật.",
      );
      advance(order.status === "ACCEPTED" ? "IN_TRANSIT" : "DELIVERED");
      break;
    case "RECEIVE":
      requireThat(
        order.status === "DELIVERED",
        "Chỉ xác nhận khi tài xế đã giao hàng.",
      );
      order.receivedAt = stamp();
      advance("RECEIVED");
      break;
    case "WEIGH":
      requireThat(
        ["RECEIVED", "WEIGHED"].includes(order.status),
        "Chỉ cân sau khi nhận hàng và trước khi chốt KCS.",
      );
      order.weight = {
        ...weigh(p.gross, p.tare, order.kg),
        note: p.note || "",
        attachment: p.attachment || order.weight?.attachment,
      };
      requireThat(
        !order.weight.flagged || order.weight.note.trim(),
        "Lệch cân trên 5%: cần ghi chú biên bản.",
      );
      advance("WEIGHED");
      break;
    case "QC":
      requireThat(
        order.status === "WEIGHED",
        "Cần hoàn tất cân trước khi chốt KCS.",
      );
      for (const field of ["purity", "moisture", "contamination"])
        requireThat(
          p[field] !== "" &&
            p[field] != null &&
            Number.isFinite(Number(p[field])) &&
            Number(p[field]) >= 0 &&
            Number(p[field]) <= 100,
          "Các tỷ lệ chất lượng phải từ 0 đến 100%.",
        );
      requireThat(["A", "B", "C"].includes(p.grade), "Chọn Grade A, B hoặc C.");
      requireThat(
        ["accept", "reject"].includes(p.decision),
        "Chọn quyết định nghiệm thu.",
      );
      requireThat(
        p.decision !== "reject" ||
          (p.note?.trim() && ["RETURN", "RENEGOTIATE"].includes(p.resolution)),
        "Từ chối cần lý do và hướng xử lý.",
      );
      order.qc = {
        ...p,
        purity: Number(p.purity),
        moisture: Number(p.moisture),
        contamination: Number(p.contamination),
        at: stamp(),
      };
      advance(p.decision === "accept" ? "VERIFIED" : "REJECTED");
      break;
    case "SETTLE":
      requireThat(
        order.status === "VERIFIED",
        "Chỉ quyết toán đơn KCS đạt và chưa thanh toán.",
      );
      requireThat(p.reference?.trim(), "Cần mã tham chiếu chuyển khoản.");
      order.payment = {
        ...settlement(order.weight.net, p.price),
        reference: p.reference.trim(),
        at: stamp(),
      };
      advance("PAID");
      break;
    case "RATE": {
      requireThat(
        order.status === "PAID" && !order.rating,
        "Chỉ đánh giá một lần sau quyết toán.",
      );
      requireThat(
        Number.isInteger(Number(p.stars)) && p.stars >= 1 && p.stars <= 5,
        "Chọn từ 1 đến 5 sao.",
      );
      requireThat(
        ["APPROVED", "BLOCKED"].includes(p.partnership),
        "Chọn tiếp tục hợp tác hoặc chặn.",
      );
      order.rating = {
        stars: Number(p.stars),
        comment: p.comment,
        at: stamp(),
      };
      next.depots.find((d) => d.id === order.depotId).status = p.partnership;
      break;
    }
    case "INVOICE":
      requireThat(
        ["VERIFIED", "PAID"].includes(order.status),
        "Hóa đơn chỉ đính kèm sau nghiệm thu.",
      );
      requireThat(p.file?.data && p.file?.name, "Chọn tệp hóa đơn.");
      order.invoice = p.file;
      break;
    case "SAVE_DEMAND": {
      requireThat(
        materials[p.material] && positive(p.kg),
        "Chọn vật liệu và khối lượng lớn hơn 0.",
      );
      requireThat(
        p.minPrice !== "" &&
          p.maxPrice !== "" &&
          Number.isFinite(Number(p.minPrice)) &&
          Number.isFinite(Number(p.maxPrice)) &&
          Number(p.minPrice) >= 0 &&
          Number(p.maxPrice) >= Number(p.minPrice),
        "Khoảng giá không hợp lệ.",
      );
      requireThat(
        /^\d{4}-\d{2}-\d{2}$/.test(p.deadline) && p.deadline >= today(),
        "Hạn nhận hàng phải từ hôm nay trở đi.",
      );
      const demand = {
        ...p,
        id: p.id || id("NC"),
        kg: Number(p.kg),
        minPrice: Number(p.minPrice),
        maxPrice: Number(p.maxPrice),
        active: p.active ?? true,
      };
      const index = next.demands.findIndex((d) => d.id === demand.id);
      if (p.id) requireThat(index >= 0, "Nhu cầu không còn tồn tại.");
      if (index >= 0) next.demands[index] = demand;
      else next.demands.unshift(demand);
      break;
    }
    case "DELETE_DEMAND":
      next.demands = next.demands.filter((d) => d.id !== p.id);
      break;
    case "TOGGLE_DEMAND": {
      const d = next.demands.find((d) => d.id === p.id);
      requireThat(d, "Không tìm thấy nhu cầu.");
      d.active = !d.active;
      break;
    }
    case "PARTNER_STATUS": {
      const d = next.depots.find((d) => d.id === p.id);
      requireThat(
        d && ["APPROVED", "BLOCKED"].includes(p.status),
        "Trạng thái không hợp lệ.",
      );
      d.status = p.status;
      break;
    }
    case "PROFILE":
      requireThat(
        p.companyName?.trim() &&
          p.taxCode?.trim() &&
          p.address?.trim() &&
          p.phone?.trim(),
        "Điền đầy đủ tên, mã số thuế, địa chỉ và điện thoại.",
      );
      requireThat(
        positive(p.capacity) &&
          p.purity !== "" &&
          Number.isFinite(Number(p.purity)) &&
          p.purity >= 0 &&
          p.purity <= 100,
        "Công suất hoặc độ tinh khiết không hợp lệ.",
      );
      requireThat(
        p.materials?.length &&
          p.materials.every((m) => materials[m]) &&
          p.materials.includes(p.primaryMaterial),
        "Vật liệu chính phải thuộc danh sách chấp nhận.",
      );
      requireThat(
        p.latitude !== "" &&
          p.longitude !== "" &&
          Number.isFinite(Number(p.latitude)) &&
          Number.isFinite(Number(p.longitude)) &&
          Math.abs(Number(p.latitude)) <= 90 &&
          Math.abs(Number(p.longitude)) <= 180,
        "Tọa độ không hợp lệ.",
      );
      next.profile = { ...p };
      break;
    default:
      throw new Error("Thao tác không được hỗ trợ.");
  }
  return next;
}
