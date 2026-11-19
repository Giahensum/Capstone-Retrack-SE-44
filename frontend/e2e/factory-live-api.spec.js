import { test, expect } from "@playwright/test";
import { cleanupDeliveredFactoryOrder, createDeliveredFactoryOrder } from "./factory-live-fixture";

const email = process.env.E2E_FACTORY_EMAIL;
const password = process.env.E2E_FACTORY_PASSWORD;
const configuredOrderId = process.env.E2E_FACTORY_ORDER_ID;

test.describe("Factory UI with the local API and PostgreSQL test database", () => {
  test.skip(!email || !password, "Set E2E_FACTORY_EMAIL and E2E_FACTORY_PASSWORD to run against the local API.");

  test("logs in, saves a demand to PostgreSQL, reloads it, and deletes its own fixture", async ({ page }) => {
    const fixtureNote = `E2E Factory ${Date.now()}`;
    await page.goto("/login");
    await page.getByPlaceholder("email@example.com").fill(email);
    await page.getByPlaceholder("••••••••").fill(password);
    await page.getByRole("button", { name: "Đăng nhập" }).click();
    await expect(page).toHaveURL(/\/factory/);
    await expect(page.getByText("FACTORY WORKSPACE")).toBeVisible();

    await page.getByRole("button", { name: "Nhu cầu thu mua" }).click();
    await page.getByRole("button", { name: /Đăng nhu cầu/ }).click();
    await page.locator("dialog select").first().selectOption("PET");
    await page.getByLabel("Khối lượng (kg)").fill("37");
    await page.getByLabel("Giá từ (đ/kg)").fill("7000");
    await page.getByLabel("Giá đến (đ/kg)").fill("9000");
    await page.getByLabel("Hạn nhận hàng").fill("2027-12-31");
    await page.locator("dialog textarea").fill(fixtureNote);
    await page.getByRole("button", { name: "Lưu nhu cầu" }).click();
    await expect(page.getByRole("status").filter({ hasText: "Đã lưu thay đổi trên máy chủ." })).toBeVisible();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await expect(page.getByRole("row").filter({ hasText: fixtureNote })).toBeVisible();

    await page.reload();
    await page.getByRole("button", { name: "Nhu cầu thu mua" }).click();
    const row = page.getByRole("row").filter({ hasText: fixtureNote });
    await expect(row).toBeVisible();
    await row.locator("summary").click();
    await row.getByRole("button", { name: "Xóa" }).click();
    await page.getByRole("button", { name: "Xác nhận" }).click();
    await expect(page.getByText(fixtureNote)).toHaveCount(0);
  });

  test("receives a delivered Depot batch, saves real QC, settles, and reloads persisted status", async ({ page }) => {
    const canCreateFixture = process.env.E2E_FACTORY_DB_FIXTURE === "true";
    test.skip(!configuredOrderId && !canCreateFixture, "Cần E2E_FACTORY_ORDER_ID hoặc bật fixture trên database test local.");
    const fixture = configuredOrderId ? null : createDeliveredFactoryOrder();
    const orderId = configuredOrderId || fixture.batchId;
    try {
      await page.goto("/login");
      await page.getByPlaceholder("email@example.com").fill(email);
      await page.getByPlaceholder("••••••••").fill(password);
      await page.getByRole("button", { name: "Đăng nhập" }).click();
      await expect(page).toHaveURL(/\/factory/);

      await page.getByRole("button", { name: "Trạm cân & KCS" }).click();
      const row = page.getByRole("row").filter({ hasText: orderId });
      await expect(row).toBeVisible();
      await row.getByRole("button", { name: "Chi tiết" }).click();
      await page.getByRole("button", { name: "Xác nhận nhận hàng" }).click();
      await expect(page.getByText("Đã xác nhận xe giao hàng. Có thể lập phiếu cân.")).toBeVisible();

      await page.getByRole("button", { name: "Lập phiếu cân" }).click();
      await page.getByLabel("Gross • Xe có hàng (kg)").fill("105");
      await page.getByLabel("Tare • Xe rỗng (kg)").fill("5");
      await page.getByRole("button", { name: "Lưu và chuyển sang KCS" }).click();
      await expect(page.getByText("Đã lưu phiếu cân. Tiếp tục nhập kết quả KCS.")).toBeVisible();
      await page.getByLabel("Độ tinh khiết (%)").fill("95");
      await page.getByLabel("Độ ẩm (%)").fill("2");
      await page.getByLabel("Tạp chất (%)").fill("3");
      await page.getByLabel("Grade").selectOption("A");
      await page.getByRole("button", { name: "Chốt kết quả KCS" }).click();
      await expect(page.getByText("KCS đạt. Đã chuyển lô sang chờ quyết toán.")).toBeVisible();

      await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
      await page.getByRole("row").filter({ hasText: orderId }).getByRole("button", { name: "Chi tiết" }).click();
      await page.getByRole("button", { name: "Thỏa thuận giá & Quyết toán" }).click();
      await page.getByLabel(/Đơn giá đã thỏa thuận sau KCS/).fill("1200");
      await page.getByLabel("VAT theo hóa đơn của vựa").selectOption("TAXABLE:10");
      await page.getByLabel("Số hóa đơn điện tử").fill(`HD-E2E-${Date.now()}`);
      await page.getByLabel("Ngày hóa đơn").fill("2026-10-18");
      await page.getByLabel("Mã số thuế bên bán (vựa)").fill("0314892841");
      await page.getByLabel(/Tôi xác nhận giá và số tiền quyết toán/).check();
      await page.getByRole("button", { name: "Chốt quyết toán" }).click();
      await expect(page.getByText("Đã chốt quyết toán. Bước tiếp theo: xác nhận đã chuyển tiền và gửi chứng từ.")).toBeVisible();

      await page.reload();
      await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
      await expect(page.getByRole("table").getByText("Đã quyết toán, chờ chuyển tiền", { exact: true })).toBeVisible();
    } finally {
      if (fixture) cleanupDeliveredFactoryOrder(fixture.batchId);
    }
  });
});
