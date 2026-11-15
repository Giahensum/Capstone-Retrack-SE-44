import { test, expect } from '@playwright/test';
import { deliverTestBatch } from './transport-fixture';
const proof = { name: 'chung-tu-e2e.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') };

async function login(page, role) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env[`E2E_${role}_EMAIL`]);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env[`E2E_${role}_PASSWORD`]);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(role === 'DEPOT' ? /\/depot/ : /\/factory/);
}

test('Depot tạo lô → Factory nhận/cân/KCS/quyết toán → Depot đọc lại sau reload', async ({ page, browser }) => {
  test.skip(process.env.E2E_TRANSPORT_FIXTURE !== 'true', 'Cần database local và fixture giao hàng; chưa kiểm thử luồng Driver.');
  test.setTimeout(120_000);
  page.setDefaultTimeout(15_000);
  await login(page, 'DEPOT');
  await page.goto('/depot/batches');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  await page.getByLabel('Loại phế liệu').selectOption('PET');
  await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1');
  await page.getByLabel('Mô tả / ghi chú').fill(`E2E-Depot-Factory-${Date.now()}`);
  const created = page.waitForResponse(r => /\/api\/depot\/batches\?/.test(r.url()) && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click();
  const response = await created;
  expect(response.status()).toBe(200);
  const batch = (await response.json()).data;
  const factoryContext = await browser.newContext({ baseURL: process.env.E2E_BASE_URL || 'http://localhost:5173' });
  try {
    const factoryPage = await factoryContext.newPage();
    factoryPage.setDefaultTimeout(15_000);
    await login(factoryPage, 'FACTORY');
    await factoryPage.getByRole('button', { name: /Sàn nguyên liệu/ }).click();
    await factoryPage.locator('article.batch-card').filter({ hasText: batch.code }).getByRole('button', { name: /Xem lô/ }).click();
    await factoryPage.getByRole('button', { name: 'Nhận lô hàng', exact: true }).click();
    await expect(factoryPage.getByRole('row').filter({ hasText: batch.code })).toBeVisible();
    deliverTestBatch(batch.id);
    await factoryPage.reload();
    await factoryPage.getByRole('button', { name: /Trạm cân & KCS/ }).click();
    await factoryPage.getByRole('row').filter({ hasText: batch.code }).getByRole('button', { name: /Chi tiết/ }).click();
    await factoryPage.getByRole('button', { name: 'Xác nhận nhận hàng', exact: true }).click();
    await factoryPage.getByRole('button', { name: 'Lập phiếu cân', exact: true }).click();
    await factoryPage.getByLabel('Gross • Xe có hàng (kg)').fill('11');
    await factoryPage.getByLabel('Tare • Xe rỗng (kg)').fill('10');
    await factoryPage.getByLabel('Ảnh hoặc PDF phiếu cân').setInputFiles(proof);
    await expect(factoryPage.getByRole('link', { name: proof.name })).toBeVisible();
    await factoryPage.getByRole('button', { name: 'Lưu phiếu cân', exact: true }).click();
    await factoryPage.getByRole('button', { name: 'Kiểm tra & Chốt KCS', exact: true }).click();
    await factoryPage.getByLabel('Độ tinh khiết (%)').fill('95');
    await factoryPage.getByLabel('Độ ẩm (%)').fill('3');
    await factoryPage.getByLabel('Tạp chất (%)').fill('2');
    await factoryPage.getByLabel(/^Grade/).selectOption('A');
    await factoryPage.getByRole('button', { name: 'Chốt kết quả KCS', exact: true }).click();
    const invoiceResponse = factoryPage.waitForResponse(r => r.url().endsWith('/invoice') && r.request().method() === 'PUT');
    await factoryPage.getByLabel('Đính kèm hóa đơn', { exact: true }).setInputFiles(proof);
    expect((await invoiceResponse).status()).toBe(200);
    await factoryPage.getByRole('button', { name: 'Thỏa thuận giá & Quyết toán', exact: true }).click();
    await factoryPage.getByLabel('Đơn giá đã thỏa thuận sau KCS (đ/kg)').fill('12000');
    const reference = `E2E-${batch.code}`;
    await factoryPage.getByLabel('Mã tham chiếu chuyển khoản').fill(reference);
    await factoryPage.getByRole('checkbox').check();
    await factoryPage.getByRole('button', { name: 'Ghi nhận quyết toán', exact: true }).click();
    await expect(factoryPage.getByText(`Mã tham chiếu: ${reference}`, { exact: false })).toBeVisible();
    await page.reload();
    await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
    await page.getByRole('row').filter({ hasText: batch.code }).getByTitle('Chi tiết lô hàng').click();
    await expect(page.getByText(reference, { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Cân và kiểm tra chất lượng (KCS)' })).toBeVisible();
    await expect(page.locator('dd').filter({ hasText: /^Chấp nhận$/ })).toBeVisible();
    await expect(page.locator('dd').filter({ hasText: /^12\.000 đ$/ })).toHaveCount(2);
    await expect(page.getByRole('link', { name: 'Tải phiếu cân', exact: true })).toBeVisible();
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('link', { name: 'Tải hóa đơn nhà máy', exact: true }).click();
    const download = await downloadPromise;
    expect(await download.failure()).toBeNull();
  } finally {
    await factoryContext.close();
  }
});
