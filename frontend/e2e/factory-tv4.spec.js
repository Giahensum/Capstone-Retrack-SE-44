import { test, expect } from "@playwright/test";

const factoryId = "10000000-0000-0000-0000-000000000001";
const depotId = "20000000-0000-0000-0000-000000000001";
const batchId = "30000000-0000-0000-0000-000000000001";
const orderId = "40000000-0000-0000-0000-000000000001";

function apiFixture() {
  const state = {
    profile: {
      ownerId: factoryId, companyName: "Nhà máy kiểm thử", taxCode: "TEST-001",
      address: "Khu công nghiệp thử nghiệm", industrialZone: "KCN Test",
      contactPhone: "0900000000", capacityKgPerMonth: 50000,
      minimumPurityPercent: 80, platformFeePercentage: 1,
      latitude: 10.8, longitude: 106.6, acceptedMaterials: ["PET"],
    },
    demands: [],
    batches: [{
      id: batchId, batchCode: "LO-TEST-001", depot: { id: depotId, companyName: "Vựa kiểm thử", address: "Địa chỉ vựa", contactPhone: "0900000001" },
      materialType: "PET", estimatedWeightKg: 100, isDirectOffer: false,
      status: "LISTED", createdAt: "2026-10-01T00:00:00Z", description: "Lô test", imageUrls: [],
    }],
    partners: [{ depotId, name: "Vựa kiểm thử", address: "Địa chỉ vựa", contactPhone: "0900000001", status: "APPROVED", blockedByFactory: false, blockedByDepot: false, legacyBlocked: false }],
    orders: [{
      id: orderId, batchId, batchCode: "LO-TEST-002", depotId, depotName: "Vựa kiểm thử",
      depotAddress: "Địa chỉ vựa", depotPhone: "0900000001", materialType: "PET",
      estimatedWeightKg: 100, status: "DELIVERED", createdAt: "2026-10-01T00:00:00Z",
      transport: { status: "DELIVERED" },
    }],
    prices: [{ materialType: "PET", pricePerKg: 1000, effectiveDate: "2026-10-01", source: "Test" }],
    calls: [],
  };
  const envelope = (data) => ({ success: true, data });
  const paged = (items) => ({ items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 });
  return { state, envelope, paged };
}

async function useFactoryApi(page, fixture) {
  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const path = url.pathname;
    const method = request.method();
    const body = request.postDataJSON?.() || {};
    const { state, envelope, paged } = fixture;
    let data;

    if (path === "/api/auth/login" && method === "POST") {
      data = { token: "test-factory-token", userId: factoryId, role: "FACTORY", fullName: "Factory Test", email: "factory@test.invalid" };
    } else if (path === "/api/factory/profile" && method === "GET") data = state.profile;
    else if (path === "/api/factory/dashboard" && method === "GET") data = {
      orderCount: state.orders.length, activeDemandCount: state.demands.length,
      partnerCount: state.partners.length, pendingQcCount: state.orders.filter((x) => ["DELIVERED", "RECEIVED", "WEIGHED"].includes(x.status)).length,
      pendingSettlementCount: state.orders.filter((x) => x.status === "VERIFIED").length,
      monthlyPurchasedKg: 0, monthlyNetPayment: 0, periodPurchasedKg: 0,
      periodGrossAmount: 0, periodNetPayment: 0, periodFeeAmount: 0,
      materialVolumes: [], sixMonthPayments: [], priorityOrders: state.orders.slice(0, 10), recentOrders: state.orders.slice(0, 5),
    };
    else if (path === "/api/factory/profile" && method === "PUT") { state.profile = { ...state.profile, ...body }; data = state.profile; }
    else if (path === "/api/factory/demands" && method === "GET") data = paged(state.demands);
    else if (path === "/api/factory/demands" && method === "POST") {
      state.demands.push({ id: "demand-test", ...body }); data = state.demands.at(-1);
    } else if (path.startsWith("/api/factory/demands/") && method === "DELETE") {
      state.demands = state.demands.filter((d) => d.id !== path.split("/").at(-1)); data = true;
    } else if (path.endsWith("/status") && method === "PATCH") {
      const id = path.split("/").at(-2); state.demands = state.demands.map((d) => d.id === id ? { ...d, ...body } : d); data = true;
    } else if (path === "/api/factory/orders" && method === "GET") data = paged(state.orders);
    else if (path === `/api/factory/orders/${orderId}` && method === "GET") data = state.orders[0];
    else if (path === `/api/factory/orders/${orderId}/receive` && method === "POST") {
      state.orders[0] = { ...state.orders[0], status: "RECEIVED", receivedAt: new Date().toISOString() }; data = state.orders[0];
    } else if (path === `/api/factory/qc/orders/${orderId}/weigh` && method === "POST") {
      state.calls.push({ path, body });
      state.orders[0] = { ...state.orders[0], status: "WEIGHED", weightTicket: { grossWeightKg: body.grossWeightKg, tareWeightKg: body.tareWeightKg, netWeightKg: body.grossWeightKg - body.tareWeightKg, ticketNumber: body.ticketNumber, ticketImageUrl: body.ticketImageUrl }, weightVerification: { factoryWeightKg: body.grossWeightKg - body.tareWeightKg, differencePercentage: 0, note: body.note } };
      data = state.orders[0];
    } else if (path === `/api/factory/qc/orders/${orderId}/quality` && method === "POST") {
      state.calls.push({ path, body });
      state.orders[0] = { ...state.orders[0], status: body.accept ? "VERIFIED" : "REJECTED", weightVerification: { ...state.orders[0].weightVerification, factoryWeightKg: 95, differencePercentage: 0, isVerified: body.accept, purityPercent: body.purityPercent, moisturePercent: body.moisturePercent, contaminationPercent: body.contaminationPercent, grade: body.grade, qualityNote: body.note } };
      data = state.orders[0];
    } else if (path === `/api/factory/orders/${orderId}/settle` && method === "POST") {
      state.calls.push({ path, body });
      const total = 95 * body.agreedPricePerKg; const fee = total * 0.01;
      state.orders[0] = { ...state.orders[0], status: "COMPLETED", agreedPrice: body.agreedPricePerKg, totalAmount: total, feeAmount: fee, platformFeePercentage: 1, netPayableAmount: total - fee, paymentReference: body.paymentReference, settledAt: new Date().toISOString() };
      data = state.orders[0];
    } else if (path === "/api/factory/marketplace/batches" && method === "GET") {
      const isDirect = url.searchParams.get("directOnly") === "true";
      data = paged(state.batches.filter((b) => b.isDirectOffer === isDirect));
    } else if (path === `/api/factory/marketplace/batches/${batchId}/accept` && method === "POST") {
      state.calls.push({ path, body }); state.batches = state.batches.filter((b) => b.id !== batchId); data = { id: orderId };
    } else if (path === "/api/factory/marketplace/prices" && method === "GET") data = state.prices;
    else if (path === "/api/factory/partners" && method === "GET") data = paged(state.partners);
    else if (path.startsWith("/api/factory/partners/") && method === "PUT") {
      state.calls.push({ path, body });
      state.partners = state.partners.map((partner) => partner.depotId === path.split("/").at(-2) ? { ...partner, status: body.status, blockedByFactory: body.status === "BLOCKED" } : partner);
      data = true;
    }
    else { await route.fulfill({ status: 404, json: { success: false, message: `Unhandled API: ${method} ${path}` } }); return; }

    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(envelope(data)) });
  });
}

async function login(page) {
  await page.goto("/login");
  await page.getByPlaceholder("email@example.com").fill("factory@test.invalid");
  await page.getByPlaceholder("••••••••").fill("password");
  await page.getByRole("button", { name: "Đăng nhập" }).click();
  await expect(page).toHaveURL(/\/factory/);
  await expect(page.getByText("FACTORY WORKSPACE")).toBeVisible();
}

test.describe("Factory TV4 browser flow", () => {
  test("shared login and logout use the common session", async ({ page }) => {
    await useFactoryApi(page, apiFixture());
    await login(page);
    await expect(page.getByText(/Đăng nhập nhà máy|Tạo tài khoản nhà máy/)).toHaveCount(0);
    await page.getByRole("button", { name: "Đăng xuất" }).click();
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByRole("button", { name: "Đăng nhập" })).toBeVisible();
  });

  test("receives Depot delivery, records weighing and QC, then settles once", async ({ page }) => {
    const fixture = apiFixture();
    await useFactoryApi(page, fixture);
    await login(page);

    await page.getByRole("button", { name: "Trạm cân & KCS" }).click();
    await page.getByRole("button", { name: "Chi tiết" }).click();
    await page.getByRole("button", { name: "Xác nhận nhận hàng" }).click();
    await expect(page.getByText("Đã xác nhận xe giao hàng. Có thể lập phiếu cân.")).toBeVisible();

    await page.getByRole("button", { name: "Lập phiếu cân" }).click();
    await page.getByLabel("Gross • Xe có hàng (kg)").fill("105");
    await page.getByLabel("Tare • Xe rỗng (kg)").fill("10");
    await page.getByRole("button", { name: "Lưu phiếu cân" }).click();
    await expect(page.getByText("Đã lưu phiếu cân. Lô hàng sẵn sàng kiểm tra KCS.")).toBeVisible();

    await page.getByRole("button", { name: "Kiểm tra & Chốt KCS" }).click();
    await page.getByLabel("Độ tinh khiết (%)").fill("92");
    await page.getByLabel("Độ ẩm (%)").fill("3");
    await page.getByLabel("Tạp chất (%)").fill("2");
    await page.getByLabel("Grade").selectOption("A");
    await page.getByRole("button", { name: "Chốt kết quả KCS" }).click();
    await expect(page.getByText("KCS đạt. Đã chuyển lô sang chờ quyết toán.")).toBeVisible();

    await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
    await page.getByRole("button", { name: "Chi tiết" }).click();
    await page.getByRole("button", { name: "Thỏa thuận giá & Quyết toán" }).click();
    await page.getByLabel("Đơn giá đã thỏa thuận sau KCS (đ/kg)").fill("1200");
    await page.getByLabel("Mã tham chiếu chuyển khoản").fill("BANK-TEST-001");
    await page.locator(".checkbox-line input[type=checkbox]").check();
    await page.getByRole("button", { name: "Ghi nhận quyết toán" }).click();
    await expect(page.getByText("Đã ghi nhận quyết toán. Có thể đánh giá lô hàng.")).toBeVisible();
    expect(fixture.state.orders[0].status).toBe("COMPLETED");
    expect(fixture.state.calls.map((call) => call.path)).toEqual([
      `/api/factory/qc/orders/${orderId}/weigh`,
      `/api/factory/qc/orders/${orderId}/quality`,
      `/api/factory/orders/${orderId}/settle`,
    ]);
    await page.reload();
    await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
    await expect(page.getByRole("table").getByText("Đã quyết toán", { exact: true })).toBeVisible();
  });

  test("accepts an available Depot batch and creates a demand", async ({ page }) => {
    const fixture = apiFixture();
    await useFactoryApi(page, fixture);
    await login(page);
    await page.getByRole("button", { name: "Sàn nguyên liệu" }).click();
    await page.getByRole("button", { name: "Xem lô & nhận hàng" }).click();
    await page.getByRole("button", { name: "Nhận lô hàng" }).click();
    await expect(page.getByText("Đã nhận lô. Đơn hàng đang chờ tài xế.")).toBeVisible();
    expect(fixture.state.calls[0].path).toBe(`/api/factory/marketplace/batches/${batchId}/accept`);

    await page.getByRole("button", { name: "Nhu cầu thu mua" }).click();
    await page.getByRole("button", { name: /Đăng nhu cầu/ }).click();
    await page.locator("dialog select").first().selectOption("PET");
    await page.getByLabel("Khối lượng (kg)").fill("250");
    await page.getByLabel("Giá từ (đ/kg)").fill("1000");
    await page.getByLabel("Giá đến (đ/kg)").fill("1500");
    await page.getByLabel("Hạn nhận hàng").fill("2027-12-31");
    await page.getByRole("button", { name: "Lưu nhu cầu" }).click();
    await expect(page.getByText("250 kg")).toBeVisible();
    expect(fixture.state.demands).toHaveLength(1);
  });

  test("records the Factory decision to block a Depot partner", async ({ page }) => {
    const fixture = apiFixture();
    await useFactoryApi(page, fixture);
    await login(page);
    await page.getByRole("button", { name: "Vựa đối tác" }).click();
    await page.getByRole("button", { name: "Chặn vựa" }).click();
    await expect(page.getByText(/Ngừng nhận lô mới/)).toBeVisible();
    await page.getByRole("button", { name: "Xác nhận" }).click();
    await expect(page.getByText("Đã lưu thay đổi trên máy chủ.")).toBeVisible();
    expect(fixture.state.calls.at(-1)).toMatchObject({
      path: `/api/factory/partners/${depotId}/status`,
      body: { status: "BLOCKED" },
    });
    await expect(page.getByRole("button", { name: "Bỏ chặn của nhà máy" })).toBeVisible();
  });
});
