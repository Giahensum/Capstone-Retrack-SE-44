import { test, expect } from '@playwright/test';

const email = process.env.E2E_DEPOT_EMAIL;
const password = process.env.E2E_DEPOT_PASSWORD;
async function selectDepot(page) {
  if (process.env.E2E_DEPOT_ID) await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
}

test.describe('Depot — biểu mẫu lô xuất', () => {
  test.skip(!email || !password, 'Cần E2E_DEPOT_EMAIL và E2E_DEPOT_PASSWORD của tài khoản thử nghiệm.');
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/mật khẩu|password/i).fill(password);
    await page.getByRole('button', { name: /đăng nhập|login/i }).click();
    await expect(page).toHaveURL(/\/depot/);
    await page.goto('/depot/batches');
    await selectDepot(page);
    await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  });

  test('chặn hơn năm ảnh và khôi phục khi chọn ảnh hợp lệ', async ({ page }) => {
    const input = page.getByLabel('Ảnh vật liệu của lô (tối đa 5 ảnh)');
    const buffer = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64');
    await input.setInputFiles(Array.from({ length: 6 }, (_, i) => ({ name: `vat-lieu-${i}.png`, mimeType: 'image/png', buffer })));
    await expect(page.getByRole('alert')).toContainText('Chỉ chọn tối đa 5 ảnh.');
    await expect(page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true })).toBeDisabled();
    await input.setInputFiles([{ name: 'vat-lieu.png', mimeType: 'image/png', buffer }]);
    await expect(page.getByAltText('Ảnh vật liệu 1: vat-lieu.png')).toBeVisible();
    await expect(page.getByText('Chỉ chọn tối đa 5 ảnh.')).toHaveCount(0);
  });

  test('tạo lô có ảnh thật, Factory xem được và Depot hủy hoàn tồn', async ({ page, browser }) => {
    test.skip(process.env.E2E_UPLOAD !== 'true', 'Bật E2E_UPLOAD=true để tạo lô và tải ảnh lên Cloudinary thật.');
    test.setTimeout(90_000);
    const material = page.getByLabel('Loại phế liệu');
    const options = material.locator('option');
    await expect.poll(() => options.count()).toBeGreaterThan(1);
    await material.selectOption({ index: 1 });
    await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1');
    await page.getByLabel('Mô tả / ghi chú').fill(`E2E-Depot-Image-${Date.now()}`);
    await page.getByLabel('Ảnh vật liệu của lô (tối đa 5 ảnh)').setInputFiles({
      name: 'vat-lieu-e2e.png', mimeType: 'image/png',
      buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64'),
    });
    const responsePromise = page.waitForResponse(r => r.url().includes('/batches/with-images') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click();
    const response = await responsePromise;
    expect(response.status(), 'API tạo lô multipart').toBe(200);
    const batch = (await response.json()).data;
    expect(batch.imageUrls).toHaveLength(1);
    expect(batch.imageUrls[0]).toMatch(/^https:\/\/res\.cloudinary\.com\//);
    let factoryContext;
    try {
      await page.reload();
      await selectDepot(page);
      const row = page.getByRole('row').filter({ hasText: batch.code });
      await row.getByTitle('Chi tiết lô hàng').click();
      const photo = page.getByAltText('Ảnh vật liệu 1', { exact: true });
      await expect(photo).toHaveAttribute('src', batch.imageUrls[0]);
      await expect.poll(() => photo.evaluate(img => img.complete && img.naturalWidth > 0)).toBe(true);
      factoryContext = await browser.newContext();
      const factoryPage = await factoryContext.newPage();
      await factoryPage.goto(`${process.env.E2E_BASE_URL || 'http://localhost:5173'}/login`);
      await factoryPage.getByLabel(/email/i).fill(process.env.E2E_FACTORY_EMAIL);
      await factoryPage.getByLabel(/mật khẩu|password/i).fill(process.env.E2E_FACTORY_PASSWORD);
      await factoryPage.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
      await factoryPage.getByRole('button', { name: /Sàn nguyên liệu/ }).click();
      const card = factoryPage.locator('article.batch-card').filter({ hasText: batch.code });
      await card.getByRole('button', { name: /Xem lô/ }).click();
      await expect(factoryPage.getByAltText('Ảnh vật liệu 1')).toHaveAttribute('src', batch.imageUrls[0]);
      await expect.poll(() => factoryPage.getByAltText('Ảnh vật liệu 1').evaluate(img => img.complete && img.naturalWidth > 0)).toBe(true);
    } finally {
      await factoryContext?.close();
      await page.goto('/depot/batches');
      await selectDepot(page);
      const row = page.getByRole('row').filter({ hasText: batch.code });
      await row.getByTitle('Hủy lô', { exact: true }).click();
      await page.getByRole('button', { name: 'Xác nhận hủy', exact: true }).click();
      await expect(row).toContainText('Đã hủy');
      await page.reload();
      await selectDepot(page);
      await expect(page.getByRole('row').filter({ hasText: batch.code })).toContainText('Đã hủy');
    }
  });
});
