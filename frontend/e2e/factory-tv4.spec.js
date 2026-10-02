import { test, expect } from "@playwright/test";

const email = process.env.E2E_FACTORY_EMAIL;
const password = process.env.E2E_FACTORY_PASSWORD;

test.describe("Factory TV4 browser flow", () => {
  test.skip(!email || !password, "Set E2E_FACTORY_EMAIL and E2E_FACTORY_PASSWORD to run against seeded data.");
  test.beforeEach(async ({ page }) => {
    await page.goto("/login");
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/mật khẩu|password/i).fill(password);
    await page.getByRole("button", { name: /đăng nhập|login/i }).click();
    await expect(page).toHaveURL(/\/factory/);
    await expect(page.getByText(/FACTORY WORKSPACE/i)).toBeVisible();
  });

  test("shared login opens Factory without the legacy login form", async ({ page }) => {
    await expect(page.getByText(/Đăng nhập nhà máy|Tạo tài khoản nhà máy/)).toHaveCount(0);
    await expect(page.getByRole("button", { name: "Đăng xuất" })).toBeVisible();
  });

  test("logout returns to the shared login page", async ({ page }) => {
    await page.getByRole("button", { name: "Đăng xuất" }).click();
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByRole("button", { name: /đăng nhập|login/i })).toBeVisible();
    await expect(page.getByText("Đăng nhập nhà máy")).toHaveCount(0);
  });

  test("Factory workflow screens are available for Depot-synchronised operations", async ({ page }) => {
    for (const name of ["Đơn hàng & Vận chuyển", "Trạm cân & KCS", "Quyết toán"]) {
      await page.getByRole("button", { name: new RegExp(name) }).click();
      await expect(page.locator("main.factory-main")).toBeVisible();
    }
  });
});
