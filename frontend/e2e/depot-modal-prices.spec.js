import { test, expect } from '@playwright/test';
import fs from 'node:fs';

const fixture = () => JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE, 'utf8'));
async function login(page, email) {
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_FINISH_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).not.toHaveURL(/\/login$/);
}
test.beforeEach(() => test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture cô lập trên DB test.'));

test('Popup nhập kho hiển thị đúng công thức 11 triệu +700 nghìn, hỗ trợ bàn phím và mobile', async ({ page }) => {
  const f = fixture(); await login(page, f.OwnerEmail);
  await page.goto('/depot/inventory?tab=receipts');
  await page.getByRole('button', { name: 'Lập phiếu nhập', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Lập phiếu nhập kho đa nguồn' });
  await modal.getByLabel('Chứng từ / tham chiếu nguồn').fill('Xem trước 11 triệu và tivi');
  await modal.getByLabel('Số lượng (kg)').fill('1000');
  await modal.getByLabel('Giá mua cả lô / giá trị tồn đầu kỳ (đ)').fill('11000000');
  await modal.getByRole('button', { name: 'Thêm dòng hàng' }).click();
  const second = modal.locator('section').filter({ has: page.getByRole('heading', { name: 'Dòng 2', exact: true }) });
  await second.getByRole('combobox', { name: 'Nhóm hàng', exact: true }).selectOption('REUSABLE');
  await second.getByLabel('Tên đồ / hãng / model').fill('Tivi cũ');
  await second.getByLabel('Số lượng (cái)').fill('5');
  await modal.getByRole('checkbox', { name: /Đủ dữ liệu chi phí/ }).check();
  await modal.getByLabel('Giá bán tham khảo / kg (đ)').fill('9994');
  await modal.getByLabel('Giá bán tham khảo / cái (đ)').fill('400000');
  await modal.getByRole('button', { name: 'Thêm chi phí', exact: true }).click();
  await modal.getByLabel('Mô tả / căn cứ phân bổ').fill('Nhân công và xăng xe');
  await modal.getByLabel('Số tiền (đ)', { exact: true }).fill('700000');
  const preview = modal.locator('section').filter({ has: page.getByRole('heading', { name: 'Xem trước phân bổ giá vốn', exact: true }) });
  await expect(preview).toContainText('11.994.000 đ');
  await expect(preview).toContainText('9.749.025 đ');
  await expect(preview).toContainText('390.195 đ');
  await preview.getByRole('button', { name: 'Cách tính giá vốn', exact: true }).click();
  await expect(preview).toContainText('Không cộng kg và số cái để chia đều.');
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await preview.scrollIntoViewIfNeeded();
    const bounds = await modal.boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0); expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
    await expect(modal.getByRole('button', { name: 'Đóng', exact: true })).toBeInViewport();
    await expect(modal.getByRole('button', { name: 'Lưu phiếu nháp', exact: true })).toBeInViewport();
    await page.screenshot({ path: test.info().outputPath(`phan-bo-von-${width}.png`), fullPage: true });
  }
  await page.keyboard.press('Escape'); await expect(modal).toHaveCount(0);
  await page.goto('/depot/staff'); await page.getByRole('button', { name: /Thêm nhân viên/ }).click();
  await expect(page.getByRole('dialog').locator('header')).toBeVisible();
  await page.keyboard.press('Escape'); await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('Owner công bố giá khác nhau theo kho; Seller xem, cập nhật và ẩn đúng bảng giá', async ({ page, browser }) => {
  test.setTimeout(90000);
  const f = fixture(); await login(page, f.OwnerEmail);
  await page.goto('/depot/buying-prices');
  async function publish(depotId, amount, note) {
    await page.getByRole('combobox', { name: 'Kho đang quản lý' }).selectOption(depotId);
    await page.getByRole('button', { name: 'Công bố giá thu mua', exact: true }).click();
    const form = page.getByRole('dialog');
    await form.getByRole('combobox', { name: 'Vật liệu', exact: true }).selectOption('IRON');
    await form.getByLabel('Giá thu mua tham khảo (đ/kg)').fill(amount);
    await form.getByLabel('Điều kiện áp dụng').fill(note);
    await form.getByRole('button', { name: 'Lưu bảng giá', exact: true }).click();
    await expect(form).toHaveCount(0);
    await expect(page.getByRole('row').filter({ hasText: 'Sắt' })).toContainText(Number(amount).toLocaleString('vi-VN'));
  }
  await publish(f.DepotId, '6000', 'Sắt sạch, không lẫn tạp');
  await publish(f.SecondDepotId, '6500', 'Nhận từ 50 kg');
  const context = await browser.newContext({ baseURL: process.env.E2E_BASE_URL });
  try {
    const seller = await context.newPage(); await login(seller, f.SellerEmail);
    async function compare() {
      await seller.goto('/seller/create');
      await seller.getByPlaceholder('Nhập địa chỉ hoặc bấm nút GPS để chọn vị trí...').fill('Ngũ Hành Sơn, Đà Nẵng');
      await seller.getByRole('button', { name: 'Tiếp tục chọn hình thức' }).click();
      await seller.getByRole('button', { name: /Chọn kho vựa cụ thể/ }).click();
    }
    await compare();
    const first = seller.locator(`[data-depot-id="${f.DepotId}"]`);
    const second = seller.locator(`[data-depot-id="${f.SecondDepotId}"]`);
    await first.getByRole('button', { name: /Xem bảng giá thu mua/ }).click();
    await expect(first).toContainText('6.000 đ/kg'); await expect(first).toContainText('Sắt sạch, không lẫn tạp');
    await second.getByRole('button', { name: /Xem bảng giá thu mua/ }).click();
    await expect(second).toContainText('6.500 đ/kg'); await expect(second).toContainText('Nhận từ 50 kg');
    await page.getByRole('combobox', { name: 'Kho đang quản lý' }).selectOption(f.DepotId);
    await page.getByRole('row').filter({ hasText: 'Sắt' }).getByRole('button', { name: 'Cập nhật', exact: true }).click();
    await page.getByRole('dialog').getByLabel('Giá thu mua tham khảo (đ/kg)').fill('6100');
    await page.getByRole('dialog').getByRole('button', { name: 'Lưu bảng giá', exact: true }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await compare();
    await first.getByRole('button', { name: /Xem bảng giá thu mua/ }).click();
    await expect(first).toContainText('6.100 đ/kg');
    await page.getByRole('row').filter({ hasText: 'Sắt' }).getByRole('button', { name: 'Cập nhật', exact: true }).click();
    await page.getByRole('dialog').getByRole('checkbox', { name: /Công bố cho Seller/ }).uncheck();
    await page.getByRole('dialog').getByRole('button', { name: 'Lưu bảng giá', exact: true }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
    await compare();
    await expect(first).toContainText('Kho chưa công bố giá thu mua');
    await page.reload(); await expect(page.getByRole('row').filter({ hasText: 'Sắt' })).toContainText('Đã ẩn');
    await seller.screenshot({ path: test.info().outputPath('seller-gia-rieng-tung-kho.png'), fullPage: true });
  } finally { await context.close(); }
});
