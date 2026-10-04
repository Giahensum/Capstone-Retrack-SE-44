import { test, expect } from '@playwright/test';

test('trang đối tác cuộn theo khung chính và popup tạo lô giữ thao tác dễ thấy', async ({ page }) => {
  test.skip(!process.env.E2E_DEPOT_EMAIL || !process.env.E2E_DEPOT_PASSWORD, 'Cần tài khoản Depot test.');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env.E2E_DEPOT_EMAIL);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_DEPOT_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/depot/);
  await page.goto('/depot/partners');
  const selector = page.getByLabel('Kho đang quản lý');
  const depotId = await selector.locator('option[value]:not([value=""])').first().getAttribute('value');
  await selector.selectOption(depotId);
  await page.getByRole('button', { name: 'Nhu cầu thu mua' }).click();
  await expect(page.getByRole('button', { name: 'Tạo lô bán ngay' }).first()).toBeVisible();
  const layout = await page.evaluate(() => {
    const content = document.querySelector('main > div:last-child');
    const partners = content.firstElementChild;
    return { mainScroll: getComputedStyle(content).overflowY, partnersScroll: getComputedStyle(partners).overflowY };
  });
  expect(layout.mainScroll).toBe('auto');
  expect(layout.partnersScroll).not.toBe('auto');
  await page.getByRole('button', { name: 'Tạo lô bán ngay' }).first().click();
  const dialog = page.getByRole('dialog', { name: 'Tạo Lô Xuất Hàng' });
  await expect(dialog).toBeVisible();
  const desktop = await dialog.evaluate((el) => ({ width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height, viewport: innerHeight }));
  expect(desktop.width).toBeGreaterThan(650);
  expect(desktop.height).toBeLessThanOrEqual(desktop.viewport * 0.9 + 2);
  await expect(dialog.getByRole('button', { name: 'Tạo lô xuất hàng' })).toBeVisible();
  await dialog.screenshot({ path: test.info().outputPath('tao-lo-doi-tac-desktop.png') });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(dialog).toBeVisible();
  const mobile = await dialog.evaluate((el) => ({ width: el.getBoundingClientRect().width, viewport: innerWidth }));
  expect(mobile.width).toBeLessThanOrEqual(mobile.viewport - 16);
  await expect(dialog.getByRole('button', { name: 'Hủy bỏ' })).toBeVisible();
  await dialog.screenshot({ path: test.info().outputPath('tao-lo-doi-tac-mobile.png') });
  await dialog.getByRole('button', { name: 'Hủy bỏ' }).click();
  await page.route('**/api/depot/partners/demands?**', async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    body.data.items[0].isBlocked = true;
    await route.fulfill({ response, json: body });
  });
  await page.reload();
  await page.getByRole('button', { name: 'Nhu cầu thu mua' }).click();
  await expect(page.getByRole('button', { name: 'Tạo lô bán ngay' }).first()).toBeDisabled();
  await expect(page.getByText('Quan hệ với nhà máy đang bị chặn; chưa thể tạo lô.')).toBeVisible();
});
