import { test, expect } from '@playwright/test';
import fs from 'node:fs';

test('Chủ kho chuyển trang lô đang mở và bảng doanh thu', async ({ page }) => {
  test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture Depot trên DB test.');
  const fixture = JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE, 'utf8'));
  test.skip(!fixture.PagingLotSearch || !fixture.ReportFrom, 'Cần dữ liệu phân trang và báo cáo của fixture.');

  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(fixture.OwnerEmail);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_FINISH_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).not.toHaveURL(/\/login$/);

  await page.goto('/depot/liquidations');
  const search = page.getByRole('searchbox', { name: 'Tìm theo tên lô / khu vực' });
  await search.fill(fixture.PagingLotSearch);
  const lots = page.getByRole('article').filter({ hasText: fixture.PagingLotSearch });
  const next = page.getByRole('button', { name: 'Sau', exact: true });
  const previous = page.getByRole('button', { name: 'Trước', exact: true });
  await expect(page.getByText('Trang 1/2 · 13 mục')).toBeVisible();
  await expect(lots).toHaveCount(12);
  await expect(previous).toBeDisabled();
  await next.click();
  await expect(page.getByText('Trang 2/2 · 13 mục')).toBeVisible();
  await expect(lots).toHaveCount(1);
  await expect(next).toBeDisabled();
  await previous.click();
  await expect(lots).toHaveCount(12);

  await page.goto('/depot/reports');
  await page.getByLabel('Kho đang quản lý').selectOption(fixture.SecondDepotId);
  await page.getByLabel('Từ ngày', { exact: true }).fill(fixture.ReportFrom);
  await page.getByLabel('Đến ngày', { exact: true }).fill(fixture.ReportTo);
  const rows = page.getByRole('table').locator('tbody tr');
  const periodTotal = page.getByText('18.900 đ', { exact: true });
  await expect(page.getByText('Trang 1/2 · 21 mục')).toBeVisible();
  await expect(rows).toHaveCount(20);
  await expect(periodTotal).toBeVisible();
  await next.click();
  await expect(page.getByText('Trang 2/2 · 21 mục')).toBeVisible();
  await expect(rows).toHaveCount(1);
  await expect(periodTotal).toBeVisible();
  await page.getByLabel('Nhóm theo').selectOption('month');
  await expect(page.getByText(/^Trang 1\/1 · [12] mục$/)).toBeVisible();
  await expect(periodTotal).toBeVisible();
});
