import { test, expect } from "@playwright/test";

const email = process.env.E2E_FACTORY_EMAIL;
const password = process.env.E2E_FACTORY_PASSWORD;
const orderId = process.env.E2E_FACTORY_ORDER_ID;

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
    await expect(page.getByText(fixtureNote)).toBeVisible();

    await page.reload();
    await page.getByRole("button", { name: "Nhu cầu thu mua" }).click();
    const row = page.getByRole("row").filter({ hasText: fixtureNote });
    await expect(row).toBeVisible();
    await row.getByRole("button", { name: "Xóa" }).click();
    await page.getByRole("button", { name: "Xác nhận" }).click();
    await expect(page.getByText(fixtureNote)).toHaveCount(0);
  });

  test("receives a delivered Depot batch, saves real QC, settles, and reloads persisted status", async ({ page }) => {
    test.skip(!orderId, "Seed E2E_FACTORY_ORDER_ID on the isolated test database.");
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
    await page.getByRole("button", { name: "Lưu phiếu cân" }).click();
    await expect(page.getByText("Đã lưu phiếu cân. Lô hàng sẵn sàng kiểm tra KCS.")).toBeVisible();

    await page.getByRole("button", { name: "Kiểm tra & Chốt KCS" }).click();
    await page.getByLabel("Độ tinh khiết (%)").fill("95");
    await page.getByLabel("Độ ẩm (%)").fill("2");
    await page.getByLabel("Tạp chất (%)").fill("3");
    await page.getByLabel("Grade").selectOption("A");
    await page.getByRole("button", { name: "Chốt kết quả KCS" }).click();
    await expect(page.getByText("KCS đạt. Đã chuyển lô sang chờ quyết toán.")).toBeVisible();

    await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
    await page.getByRole("row").filter({ hasText: orderId }).getByRole("button", { name: "Chi tiết" }).click();
    await page.getByRole("button", { name: "Thỏa thuận giá & Quyết toán" }).click();
    await page.getByLabel("Đơn giá đã thỏa thuận sau KCS (đ/kg)").fill("1200");
    await page.getByLabel("Mã tham chiếu chuyển khoản").fill("LIVE-E2E-FACTORY");
    await page.locator(".checkbox-line input[type=checkbox]").check();
    await page.getByRole("button", { name: "Ghi nhận quyết toán" }).click();
    await expect(page.getByText("Đã ghi nhận quyết toán. Có thể đánh giá lô hàng.")).toBeVisible();

    await page.reload();
    await page.getByRole("button", { name: "Quyết toán", exact: true }).click();
    await expect(page.getByRole("table").getByText("Đã quyết toán", { exact: true })).toBeVisible();
  });
});
