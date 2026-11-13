import { test, expect } from '@playwright/test';
import fs from 'node:fs';

test('Depot đối chiếu VAT, nguồn vốn và chi phí qua nhiều trang sau khi tải lại', async ({ page }) => {
  test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture trên DB test.');
  const fixture = JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE, 'utf8'));
  test.skip(!fixture.VatDepotId, 'Cần fixture quyết toán VAT.');
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(fixture.OwnerEmail);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_FINISH_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await page.getByLabel('Kho đang quản lý').selectOption(fixture.VatDepotId);

  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.goto('/depot/batches');
    await page.getByLabel('Kho đang quản lý').selectOption(fixture.VatDepotId);
    await page.getByRole('button', { name: `Xem chi tiết lô ${fixture.VatBatchCode}`, exact: true }).click();
    const dialog = page.getByRole('dialog', { name: `Chi tiết lô ${fixture.VatBatchCode}`, exact: true });
    await expect(dialog).toContainText('1.000.000 đ');
    await expect(dialog).toContainText('11.000.000 đ');
    await expect(dialog).toContainText('10.500.000 đ');
    const cost = dialog.locator('section').filter({ has: page.getByRole('heading', { name: 'Nguồn giá vốn và chi phí riêng của kho', exact: true }) });
    await expect(cost).toContainText('1.500.000 đ');
    const sources = cost.getByRole('navigation', { name: 'Phân trang nguồn giá vốn' });
    const expenses = cost.getByRole('navigation', { name: 'Phân trang chi phí giao và bán' });
    await expect(sources).toContainText('Trang 1/2 · 21 mục');
    await expect(expenses).toContainText('Trang 1/2 · 21 mục');
    await sources.getByRole('button', { name: 'Sau', exact: true }).click();
    await expect(sources).toContainText('Trang 2/2 · 21 mục');
    await expect(cost.getByRole('table').locator('tbody tr')).toHaveCount(1);
    await expect(expenses).toContainText('Trang 1/2 · 21 mục');
    await expenses.getByRole('button', { name: 'Sau', exact: true }).click();
    await expect(expenses).toContainText('Trang 2/2 · 21 mục');
    await expect(cost.getByRole('listitem')).toHaveCount(1);
    await expect(cost).toContainText('8.000.000 đ');
    await expect(cost).toContainText('1.500.000 đ');
    await page.screenshot({ path: test.info().outputPath(`quyet-toan-vat-${width}.png`), fullPage: true });
    await page.keyboard.press('Escape');

    await page.goto('/depot/reports');
    await page.getByLabel('Kho đang quản lý').selectOption(fixture.VatDepotId);
    await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
    const region = page.getByRole('region', { name: 'Đối chiếu giá vốn theo kênh bán' });
    await expect(region).toContainText('10.500.000 đ');
    await expect(region).toContainText('1.000.000 đ');
    const factory = region.getByRole('row').filter({ hasText: 'Lô xuất nhà máy' });
    await expect(factory).toContainText('9.500.000 đ');
    await expect(factory).toContainText('8.000.000 đ');
    await expect(factory).toContainText('1.500.000 đ');
    await page.screenshot({ path: test.info().outputPath(`bao-cao-vat-${width}.png`), fullPage: true });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  }
  await page.reload();
  await page.getByLabel('Kho đang quản lý').selectOption(fixture.VatDepotId);
  await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
  await expect(page.getByRole('row').filter({ hasText: 'Lô xuất nhà máy' })).toContainText('1.500.000 đ');
  expect(errors).toEqual([]);
});
