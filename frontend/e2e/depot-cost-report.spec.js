import { test, expect } from '@playwright/test';

test('báo cáo giá vốn dùng API thật, tải lại và hiển thị đúng khi chưa có quyết toán', async ({ page }) => {
  test.skip(!process.env.E2E_STOCK_EMAIL || !process.env.E2E_STOCK_PASSWORD, 'Cần kho fixture riêng trên Retrack_TV2_test.');
  await page.goto('/login'); await page.getByLabel(/email/i).fill(process.env.E2E_STOCK_EMAIL);
  await page.getByLabel(/mật khẩu|password/i).fill(process.env.E2E_STOCK_PASSWORD);
  await page.getByRole('button', { name: /đăng nhập|login/i }).click(); await expect(page).toHaveURL(/\/depot/);
  const errors = []; page.on('pageerror', e => errors.push(e.message));
  for (const width of [320, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 1000 }); await page.goto('/depot/reports');
    const response = page.waitForResponse(r => r.url().includes('/reports/costs') && r.request().method() === 'GET');
    await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
    expect((await response).status()).toBe(200);
    const section = page.getByRole('region', { name: 'Đối chiếu giá vốn theo kênh bán' });
    await expect(section).toContainText('0 giao dịch trong kỳ');
    await expect(section).toContainText('Chưa có lô Factory đã quyết toán');
    await expect(section.getByRole('row')).toHaveCount(3);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({ path: test.info().outputPath(`bao-cao-gia-von-${width}.png`), fullPage: true });
  }
  await page.reload(); await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
  await expect(page.getByRole('heading', { name: 'Đối chiếu giá vốn theo kênh bán' })).toBeVisible();
  await page.getByRole('link', { name: 'Xem đồ tái sử dụng và lịch sử bán' }).click();
  await expect(page).toHaveURL(/tab=reusable/); await expect(page.getByRole('button', { name: 'Lịch sử bán ngoài' })).toBeVisible();
  if (process.env.E2E_COST_SALE === 'true') {
    await page.getByRole('button', { name: 'Phiếu nhập đa nguồn', exact: true }).click();
    await page.getByRole('button', { name: 'Lập phiếu nhập', exact: true }).click();
    const receipt = page.getByRole('dialog');
    await receipt.getByLabel('Chứng từ / tham chiếu nguồn').fill('E2E-Ghế báo cáo');
    await receipt.getByLabel('Nhóm hàng').selectOption('REUSABLE');
    await receipt.getByLabel('Tên đồ / hãng / model').fill('Ghế kiểm thử báo cáo');
    await receipt.getByLabel('Số lượng (cái)').fill('10');
    await receipt.getByLabel('Giá mua cả lô / giá trị tồn đầu kỳ (đ)').fill('3000000');
    await receipt.getByRole('checkbox').check(); await receipt.getByLabel('Giá bán tham khảo / cái (đ)').fill('300000');
    await receipt.getByRole('button', { name: 'Lưu phiếu nháp' }).click(); await expect(receipt).toHaveCount(0);
    await page.getByRole('row').filter({ hasText: 'E2E-Ghế báo cáo' }).getByRole('button', { name: 'Chi tiết / duyệt' }).click();
    await page.getByRole('button', { name: 'Xác nhận hàng thực nhận và ghi sổ' }).click(); await expect(page.getByRole('dialog')).toHaveCount(0);
    await page.getByRole('button', { name: 'Đồ tái sử dụng', exact: true }).click();
    const row = page.getByRole('row').filter({ hasText: 'Ghế kiểm thử báo cáo' });
    await row.getByRole('button', { name: 'Ghi bán ngoài' }).click();
    const sale = page.getByRole('dialog');
    await sale.getByLabel('Lượng đã giao (cái)').fill('3'); await sale.getByLabel('Giá bán / cái (đ)').fill('450000');
    await sale.getByLabel('Chi phí bán và khoản liên quan đã xác định (đ)').fill('50000');
    await sale.getByLabel('Ghi chú giao nhận / chứng từ').fill('E2E-Đối chiếu báo cáo giá vốn');
    await sale.getByRole('button', { name: 'Xác nhận đã giao và ghi bán' }).click(); await expect(sale).toHaveCount(0);
    await page.goto('/depot/reports'); await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
    const section = page.getByRole('region', { name: 'Đối chiếu giá vốn theo kênh bán' });
    await expect(section).toContainText('1 giao dịch trong kỳ · 0 giao dịch chưa đủ');
    const result = section.getByRole('row').filter({ hasText: 'Đồ tái sử dụng bán ngoài' });
    await expect(result).toContainText('1.350.000 đ'); await expect(result).toContainText('900.000 đ'); await expect(result).toContainText('400.000 đ');
    await page.reload(); await page.getByRole('tab', { name: 'Giá vốn và chênh lệch' }).click();
    await expect(section.getByRole('row').filter({ hasText: 'Đồ tái sử dụng bán ngoài' })).toContainText('400.000 đ');
    await page.screenshot({ path: test.info().outputPath('bao-cao-gia-von-co-giao-dich.png'), fullPage: true });
  }
  expect(errors).toEqual([]);
});
