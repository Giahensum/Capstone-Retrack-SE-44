import { test, expect } from '@playwright/test';

const screens = [
  ['dashboard', 'Tổng quan'], ['profile', 'Hồ sơ kho'], ['inventory', 'Tồn kho'],
  ['batches', 'Lô xuất'], ['partners', 'Đối tác'], ['staff', 'Nhân sự'],
  ['staff/performance', 'Hiệu suất'], ['payments', 'Thanh toán'],
  ['payments/fees', 'Phí'], ['reports', 'Doanh thu'],
];

async function login(page) {
  test.skip(!process.env.E2E_DEPOT_EMAIL || !process.env.E2E_DEPOT_PASSWORD || !process.env.E2E_DEPOT_ID,
    'Cần tài khoản và UUID kho test.');
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env.E2E_DEPOT_EMAIL);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_DEPOT_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/depot/);
}

test('kiểm tra thị giác 10 màn Depot ở desktop và mobile', async ({ page }) => {
  test.setTimeout(180_000);
  await login(page);
  for (const [width, height, label] of [[1440, 900, 'desktop'], [390, 844, 'mobile']]) {
    await page.setViewportSize({ width, height });
    for (const [route, name] of screens) {
      await page.goto(`/depot/${route}`);
      await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
      await expect(page.locator('main h1, main h2').first()).toBeVisible();
      await page.waitForLoadState('networkidle');
      await page.evaluate(() => document.fonts.ready);
      const result = await page.evaluate(() => {
        const buttons = [...document.querySelectorAll('main button')].filter(b => b.getClientRects().length);
        const controls = [...document.querySelectorAll('main input, main select')].filter(i => i.getClientRects().length);
        const label = el => el.getAttribute('aria-label') || el.labels?.[0]?.textContent?.trim() || el.placeholder || '';
        return {
          documentOverflow: document.documentElement.scrollWidth - window.innerWidth,
          bodyFont: getComputedStyle(document.querySelector('main')).fontFamily,
          headingFont: getComputedStyle(document.querySelector('main h1, main h2')).fontFamily,
          buttonCount: buttons.length,
          buttonRadii: [...new Set(buttons.map(b => getComputedStyle(b).borderRadius))],
          unlabeledControls: controls.filter(i => !label(i)).length,
          pagerCount: buttons.filter(b => ['Trước', 'Sau'].includes(b.textContent.trim())).length,
        };
      });
      console.log(`${label} ${name}: ${JSON.stringify(result)}`);
      expect(result.documentOverflow, `${label} ${name}: trang không được tràn ngang`).toBeLessThanOrEqual(1);
      expect(result.headingFont, `${label} ${name}: font tiêu đề Depot`).toContain('Inter');
      expect(result.unlabeledControls, `${label} ${name}: mọi ô nhập có nhãn`).toBe(0);
      await expect(page.locator('main .material-symbols-outlined')).toHaveCount(0);
      await page.screenshot({ path: test.info().outputPath(`${label}-${route.replace('/', '-')}.png`) });
    }
  }
});

test('popup, tab và nút tìm đối tác hoạt động trên mobile', async ({ page }) => {
  test.setTimeout(90_000);
  await login(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/depot/batches');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  const dialog = page.getByRole('dialog', { name: 'Tạo Lô Xuất Hàng' });
  await expect(dialog).toBeVisible();
  await expect(page.getByLabel('Ảnh vật liệu của lô (tối đa 5 ảnh)')).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Đóng' })).toHaveCSS('width', '44px');
  await dialog.screenshot({ path: test.info().outputPath('mobile-popup-tao-lo.png') });
  await page.getByLabel('Hình thức bán').selectOption('direct');
  await expect(page.getByLabel('Tìm nhà máy')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);

  await page.goto('/depot/payments/fees');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  const invoices = page.getByRole('button', { name: 'Hóa đơn hàng tháng' });
  await invoices.click();
  await expect(invoices).toHaveAttribute('aria-current', 'page');
  await expect(page.getByRole('button', { name: 'Chi tiết phí' })).not.toHaveAttribute('aria-current', 'page');

  await page.goto('/depot/partners');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await page.getByRole('button', { name: 'Tìm đối tác mới' }).click();
  await expect(page.getByRole('textbox', { name: 'Tìm tên nhà máy' })).toBeFocused();
});

test('popup hồ sơ, nhân sự và chi tiết lô mở được, cuộn được, đóng được', async ({ page }) => {
  test.setTimeout(90_000);
  await login(page);
  await page.setViewportSize({ width: 390, height: 844 });
  for (const [route, trigger, dialogName] of [
    ['profile', 'Chỉnh sửa hồ sơ', 'Chỉnh sửa hồ sơ kho vựa'],
    ['staff', '＋ Thêm nhân viên', 'Thêm nhân viên mới'],
  ]) {
    await page.goto(`/depot/${route}`);
    await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
    await page.getByRole('button', { name: trigger }).click();
    const dialog = page.getByRole('dialog', { name: dialogName });
    await expect(dialog).toBeVisible();
    const dimensions = await dialog.evaluate(el => ({ height: el.getBoundingClientRect().height, viewport: innerHeight, overflow: getComputedStyle(el).overflowY }));
    expect(dimensions.height).toBeLessThanOrEqual(dimensions.viewport);
    expect(dimensions.overflow).toBe('auto');
    await dialog.getByRole('button', { name: 'Đóng' }).click();
    await expect(dialog).toHaveCount(0);
  }

  await page.goto('/depot/batches');
  await page.getByLabel('Kho đang quản lý').selectOption(process.env.E2E_DEPOT_ID);
  await page.getByRole('button', { name: /Xem chi tiết lô/ }).first().click();
  const detail = page.getByRole('dialog', { name: /Chi tiết lô/ });
  await expect(detail.getByText('Ảnh vật liệu khi tạo lô')).toBeVisible();
  await expect(detail.getByText('Cân và kiểm tra chất lượng (KCS)')).toBeVisible();
  await detail.getByRole('button', { name: 'Đóng' }).click();
  await expect(detail).toHaveCount(0);
});
