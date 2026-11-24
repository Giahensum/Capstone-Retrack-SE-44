import { test, expect } from '@playwright/test';
import { deliverTestBatch } from './transport-fixture';

const image = { name: 'chung-tu-fixture.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') };
async function login(page, role) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env[`E2E_${role}_EMAIL`]);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env[`E2E_${role}_PASSWORD`]);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(role === 'DEPOT' ? /\/depot/ : /\/factory/);
}
test('Giá cần kho đồng ý, Factory gửi chứng từ, kho xác nhận nhận tiền rồi mới hoàn tất', async ({ page, browser }) => {
  test.skip(process.env.E2E_TRANSPORT_FIXTURE !== 'true' || !process.env.E2E_DEPOT_ID || !process.env.E2E_FACTORY_EMAIL,
    'Cần actor riêng và fixture vận chuyển trong DB test. Không phải nghiệm thu Driver thiết bị.');
  test.setTimeout(150000);
  page.setDefaultTimeout(15000);
  await login(page, 'DEPOT'); await page.goto('/depot/batches');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  await page.getByLabel('Loại phế liệu').selectOption('PET');
  await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1');
  await page.getByLabel('Mô tả / ghi chú').fill('E2E-Depot-Factory-Approval-' + Date.now());
  const created = page.waitForResponse(r => r.url().includes('/api/depot/batches?') && r.request().method() === 'POST');
  await page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click();
  const response = await created; expect(response.status()).toBe(200); const batch = (await response.json()).data;
  const context = await browser.newContext(); const factory = await context.newPage();
  factory.setDefaultTimeout(15000);
  const errors = []; for (const p of [page, factory]) p.on('pageerror', e => errors.push(e.message));
  try {
    await login(factory, 'FACTORY');
    await factory.getByRole('button', { name: /Sàn nguyên liệu/ }).click();
    await factory.locator('article.batch-card').filter({ hasText: batch.code }).getByRole('button', { name: /Xem lô/ }).click();
    await factory.getByRole('button', { name: 'Nhận lô hàng', exact: true }).click();
    await expect(factory.getByRole('row').filter({ hasText: batch.code })).toBeVisible();
    deliverTestBatch(batch.id);
    await factory.reload(); await factory.getByRole('button', { name: /Trạm cân & KCS/ }).click();
    await factory.getByRole('row').filter({ hasText: batch.code }).getByRole('button', { name: /Chi tiết/ }).click();
    await factory.getByRole('button', { name: 'Xác nhận nhận hàng', exact: true }).click();
    await factory.getByRole('button', { name: 'Lập phiếu cân', exact: true }).click();
    await factory.getByLabel('Gross • Xe có hàng (kg)').fill('11'); await factory.getByLabel('Tare • Xe rỗng (kg)').fill('10');
    await factory.getByLabel('Ảnh hoặc PDF phiếu cân').setInputFiles(image);
    await factory.getByRole('button', { name: 'Lưu và chuyển sang KCS', exact: true }).click();
    await factory.getByLabel('Độ tinh khiết (%)').fill('95'); await factory.getByLabel('Độ ẩm (%)').fill('3');
    await factory.getByLabel('Tạp chất (%)').fill('2'); await factory.getByLabel(/^Grade/).selectOption('A');
    await factory.getByRole('button', { name: 'Chốt kết quả KCS', exact: true }).click();
    await factory.getByRole('button', { name: 'Gửi đề xuất giá cho vựa', exact: true }).click();
    const price = factory.getByRole('dialog');
    await price.getByLabel(/^Đơn giá đã thỏa thuận/).fill('12000');
    await price.getByRole('checkbox', { name: /Tôi đã kiểm tra đơn giá/ }).check();
    await price.getByRole('button', { name: 'Gửi đề xuất giá', exact: true }).click(); await expect(price).toHaveCount(0);
    async function depotDetail() {
      await page.goto('/depot/batches'); await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
      await page.getByRole('row').filter({ hasText: batch.code }).getByTitle('Chi tiết lô hàng').click();
    }
    await depotDetail();
    await expect(page.getByRole('button', { name: 'Xác nhận đã nhận tiền', exact: true })).toHaveCount(0);
    await page.getByLabel('Tôi đồng ý khối lượng, đơn giá và số tiền của phiên bản này.').check();
    await page.getByRole('button', { name: 'Đồng ý giá và số tiền', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText('Đã đồng ý giá, chờ chuyển tiền');
    async function factoryDetail() {
      await factory.reload();
      await factory.getByRole('button', { name: /Quyết toán/ }).first().click();
      await factory.getByRole('row').filter({ hasText: batch.code }).getByRole('button', { name: /Chi tiết/ }).click();
    }
    await factoryDetail();
    await factory.getByRole('button', { name: 'Xác nhận đã chuyển tiền', exact: true }).click();
    const proof = factory.getByRole('dialog');
    await proof.getByLabel('Mã tham chiếu chuyển khoản').fill('CK-APPROVAL-' + batch.code);
    await proof.getByLabel('Chứng từ chuyển khoản (PDF, JPG, PNG; tối đa 700 KB)').setInputFiles(image);
    await proof.getByRole('button', { name: 'Xác nhận đã chuyển tiền', exact: true }).click(); await expect(proof).toHaveCount(0);
    await depotDetail(); await page.getByLabel('Tôi đã kiểm tra tài khoản và nhận đủ số tiền trên.').check();
    await page.getByRole('button', { name: 'Xác nhận đã nhận tiền', exact: true }).click();
    await expect(page.getByRole('dialog')).toContainText('Đã nhận tiền');
    await page.reload(); await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
    await page.getByRole('row').filter({ hasText: batch.code }).getByTitle('Chi tiết lô hàng').click();
    await expect(page.getByRole('dialog')).toContainText('Kho xác nhận nhận đủ tiền');
    await page.screenshot({ path: test.info().outputPath('depot-da-nhan-tien.png'), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    const mobileDialog = page.getByRole('dialog');
    await expect(mobileDialog).toBeVisible();
    const bounds = await mobileDialog.boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(391);
    expect(bounds.height).toBeLessThanOrEqual(844);
    await page.getByRole('heading', { name: 'Lịch sử xác nhận của hai bên' }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: test.info().outputPath('depot-lich-su-mobile.png'), fullPage: true });
    await factoryDetail(); await expect(factory.getByText('Vựa đã xác nhận nhận tiền', { exact: true }).filter({ visible: true }).first()).toBeVisible();
    expect(errors).toEqual([]);
  } finally { await context.close(); }
});
