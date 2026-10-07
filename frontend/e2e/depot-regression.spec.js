import { test, expect } from '@playwright/test';

async function login(page, email, password) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/(depot(?:\/|$)|seller(?:\/|$))/);
}

const screens = [
  ['dashboard', 'Bảng điều khiển', 'dashboard'],
  ['profile', 'Thông tin kho vựa', 'profile'],
  ['inventory', 'Quản lý Tồn Kho', 'inventory'],
  ['batches', 'Danh sách lô xuất hàng', 'batches'],
  ['partners', 'Đối tác nhà máy', 'partners'],
  ['staff', 'Quản Lý Nhân Sự', 'staff'],
  ['staff/performance', 'Hiệu suất nhân sự', 'reports/staff'],
  ['payments', 'Thanh toán chờ duyệt', 'payments'],
  ['payments/fees', 'Phí nền tảng', 'reports/fees/summary'],
  ['reports', 'Báo cáo doanh thu', 'reports/revenue'],
];

test('10 màn Depot tải dữ liệu thật theo kho đã chọn và giữ sau reload', async ({ page }) => {
  test.skip(!process.env.E2E_DEPOT_EMAIL || !process.env.E2E_DEPOT_PASSWORD || !process.env.E2E_DEPOT_ID,
    'Cần tài khoản Depot và UUID kho test có dữ liệu.');
  test.setTimeout(120_000);
  page.setDefaultTimeout(15_000);
  await login(page, process.env.E2E_DEPOT_EMAIL, process.env.E2E_DEPOT_PASSWORD);
  for (const [route, title, apiPath] of screens) {
    await page.goto(`/depot/${route}`);
    const request = page.waitForResponse(r => r.url().includes(`/api/depot/${apiPath}`)
      && r.url().includes(`depotId=${process.env.E2E_DEPOT_ID}`) && r.request().method() === 'GET');
    await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
    expect((await request).status(), `API của /depot/${route}`).toBe(200);
    await expect(page.getByRole('heading', { name: title, exact: false }).first()).toBeVisible();
    await expect(page.locator('main').getByRole('alert')).toHaveCount(0);
    await page.reload();
    await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
    await expect(page.getByRole('heading', { name: title, exact: false }).first()).toBeVisible();
  }
});

test('không đăng nhập và Seller không truy cập được màn Depot', async ({ page }) => {
  await page.goto('/depot/batches');
  await expect(page).toHaveURL(/\/login/);
  test.skip(!process.env.E2E_SELLER_EMAIL || !process.env.E2E_SELLER_PASSWORD, 'Cần tài khoản Seller test.');
  await login(page, process.env.E2E_SELLER_EMAIL, process.env.E2E_SELLER_PASSWORD);
  await page.goto('/depot/batches');
  await expect(page).toHaveURL(/\/unauthorized/);
  await expect(page.getByText('403 — Không có quyền truy cập')).toBeVisible();
});

test('dashboard Depot ở màn hình di động vẫn đọc được', async ({ page }) => {
  test.skip(!process.env.E2E_DEPOT_EMAIL || !process.env.E2E_DEPOT_PASSWORD, 'Cần tài khoản Depot test.');
  await page.setViewportSize({ width: 390, height: 844 });
  await login(page, process.env.E2E_DEPOT_EMAIL, process.env.E2E_DEPOT_PASSWORD);
  await page.goto('/depot/dashboard');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await expect(page.getByRole('heading', { name: 'Bảng điều khiển' })).toBeVisible();
  await expect(page.getByText('Cơ cấu tồn kho')).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath('depot-dashboard-mobile.png'), fullPage: true });
  await page.getByRole('button', { name: 'Mở menu' }).click();
  await expect(page.getByRole('navigation').getByRole('link', { name: /Thanh toán chờ duyệt/ })).toBeVisible();
  await page.getByRole('navigation').getByRole('link', { name: /Thanh toán chờ duyệt/ }).click();
  await expect(page).toHaveURL(/\/depot\/payments$/);
  await expect(page.getByRole('button', { name: 'Mở menu' })).toHaveAttribute('aria-expanded', 'false');
});
