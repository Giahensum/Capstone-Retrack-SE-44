import { test, expect } from '@playwright/test';

test('Owner tạo kho, nộp hồ sơ, Admin yêu cầu bổ sung rồi duyệt và mở giao dịch', async ({ browser, request }) => {
  test.skip(!process.env.E2E_VERIFY_OWNER || !process.env.E2E_VERIFY_ADMIN || !process.env.E2E_VERIFY_PASSWORD, 'Cần hai tài khoản fixture riêng, Owner chưa có kho.');
  test.setTimeout(120_000);
  const ownerContext = await browser.newContext(), adminContext = await browser.newContext();
  const owner = await ownerContext.newPage(), admin = await adminContext.newPage();
  const errors = []; owner.on('pageerror', e => errors.push(e.message)); admin.on('pageerror', e => errors.push(e.message));
  const name = `Kho E2E xác minh ${Date.now()}`;
  const login = async (page, email) => {
    await page.goto('/login'); await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_VERIFY_PASSWORD);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await expect(page).toHaveURL(/\/(depot|admin)/, { timeout: 20_000 });
  };
  await login(owner, process.env.E2E_VERIFY_OWNER);
  await owner.getByRole('button', { name: 'Tạo kho đầu tiên' }).click();
  await owner.getByLabel('Tên kho', { exact: true }).fill(name);
  await owner.getByLabel('Địa chỉ', { exact: true }).fill('Ngũ Hành Sơn, Đà Nẵng');
  await owner.getByRole('dialog').getByRole('button', { name: 'Tạo kho', exact: true }).click();
  await expect(owner.getByRole('dialog')).toHaveCount(0);
  await owner.goto('/depot/profile');
  await expect(owner.getByText('Chưa nộp hồ sơ', { exact: true })).toBeVisible();
  const depotId = await owner.getByLabel('Kho đang quản lý').inputValue();
  const loginResponse = await request.post('/api/auth/login', { data: { email: process.env.E2E_VERIFY_OWNER, password: process.env.E2E_VERIFY_PASSWORD } });
  const token = (await loginResponse.json()).data.token;
  const createBatch = () => request.post(`/api/depot/batches?depotId=${depotId}`, { headers: { Authorization: `Bearer ${token}` }, data: { operationId: crypto.randomUUID(), materialType: 'PET', weightKg: 1 } });
  const blocked = await createBatch(); expect(blocked.status()).toBe(409); expect((await blocked.json()).message).toContain('xác minh');
  const submit = async () => {
    await owner.getByLabel('Giấy tờ kinh doanh', { exact: true }).setInputFiles({ name: 'dang-ky-kinh-doanh.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\nHo so fixture rieng\n%%EOF') });
    await owner.getByLabel('Ảnh kho', { exact: true }).setInputFiles({ name: 'anh-kho.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=', 'base64') });
    await expect(owner.getByRole('img', { name: 'Xem trước anh-kho.png' })).toBeVisible();
    await owner.getByRole('button', { name: 'Xóa anh-kho.png', exact: true }).click();
    await expect(owner.getByRole('img', { name: 'Xem trước anh-kho.png' })).toHaveCount(0);
    await owner.getByLabel('Ảnh kho', { exact: true }).setInputFiles({ name: 'anh-kho.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=', 'base64') });
    await owner.getByLabel('Ghi chú hồ sơ').fill('Hồ sơ kiểm thử, không phải giấy tờ thực tế');
    for (const width of [390, 1440]) {
      await owner.setViewportSize({ width, height: 900 });
      if (width < 1024) await expect.poll(async () => { const box = await owner.locator('aside[aria-label="Menu kho vựa"]').boundingBox(); return box.x + box.width; }).toBeLessThanOrEqual(1);
      await owner.getByRole('heading', { name: 'Hồ sơ xác minh kho', exact: true }).scrollIntoViewIfNeeded();
      expect(await owner.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
      await owner.screenshot({ path: test.info().outputPath('nop-ho-so-' + width + '.png'), fullPage: true });
    }
    const submitted = owner.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/api/depot/verifications?'));
    await owner.getByRole('button', { name: 'Nộp hồ sơ xác minh' }).click();
    expect((await submitted).status()).toBe(200);
    await expect(owner.getByRole('button', { name: 'Nộp hồ sơ xác minh' })).toHaveCount(0);
  };
  await submit(); await owner.reload();
  await expect(owner.getByRole('heading', { name: new RegExp(`${name} — Chờ Admin duyệt`) })).toBeVisible();
  const downloadEvent = owner.waitForEvent('download'); await owner.getByRole('button', { name: 'Tải dang-ky-kinh-doanh.pdf' }).click();
  expect((await downloadEvent).suggestedFilename()).toBe('dang-ky-kinh-doanh.pdf');
  await owner.getByRole('button', { name: 'Xem anh-kho.png', exact: true }).click();
  await expect(owner.getByRole('dialog').getByRole('img', { name: 'anh-kho.png', exact: true })).toBeVisible();
  await owner.keyboard.press('Escape'); await expect(owner.getByRole('dialog')).toHaveCount(0);
  await login(admin, process.env.E2E_VERIFY_ADMIN); await admin.goto('/admin/depot-verifications');
  let card = admin.locator('article').filter({ hasText: name });
  await card.getByRole('button', { name: 'Xem anh-kho.png', exact: true }).click();
  await expect(admin.getByRole('dialog').getByRole('img', { name: 'anh-kho.png', exact: true })).toBeVisible();
  await admin.keyboard.press('Escape');
  await card.getByRole('button', { name: 'Yêu cầu bổ sung', exact: true }).click();
  await admin.getByLabel('Lý do / kết quả đối soát').fill('Bổ sung thông tin kho');
  await admin.getByRole('button', { name: 'Lưu kết quả' }).click(); await expect(admin.getByRole('dialog')).toHaveCount(0);
  await owner.reload(); await expect(owner.getByText('Bổ sung thông tin kho', { exact: false })).toBeVisible();
  await submit(); await admin.reload(); card = admin.locator('article').filter({ hasText: name });
  await card.getByRole('button', { name: 'Duyệt hồ sơ', exact: true }).click();
  await admin.getByLabel('Lý do / kết quả đối soát').fill('Đã đối soát đủ hồ sơ');
  await admin.getByRole('button', { name: 'Lưu kết quả' }).click(); await expect(admin.getByRole('dialog')).toHaveCount(0);
  await owner.reload(); await expect(owner.getByRole('status').filter({ hasText: /^Đã xác minh$/ })).toBeVisible();
  const allowedButEmptyStock = await createBatch(); expect(allowedButEmptyStock.status()).toBe(409);
  expect((await allowedButEmptyStock.json()).message).toContain('Tồn kho');
  for (const width of [390, 1440]) {
    await owner.setViewportSize({ width, height: 900 });
    if (width < 1024) await expect.poll(async () => { const box = await owner.locator('aside[aria-label="Menu kho vựa"]').boundingBox(); return box.x + box.width; }).toBeLessThanOrEqual(1);
    await owner.screenshot({ path: test.info().outputPath(`ho-so-${width}.png`) });
    expect(await owner.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    expect(await owner.locator('main h1, main dd').evaluateAll(elements => elements.every(el => el.scrollWidth <= el.clientWidth + 1))).toBe(true);
  }
  // Thử burst sau các thao tác UI để không cản luồng duyệt chính.
  const headers = { Authorization: 'Bearer ' + token };
  const missing = '/api/depot/verifications/documents/' + crypto.randomUUID();
  expect((await request.get(missing)).status()).toBe(401);
  const downloads = [];
  for (let attempt = 0; attempt < 31; attempt++) downloads.push(await request.get(missing, { headers }));
  const limited = downloads.find(response => response.status() === 429);
  expect(limited).toBeTruthy();
  expect(Number(limited.headers()['retry-after'])).toBeGreaterThan(0);
  expect((await limited.json()).code).toBe('RATE_LIMITED');
  const adminLogin = await request.post('/api/auth/login', { data: { email: process.env.E2E_VERIFY_ADMIN, password: process.env.E2E_VERIFY_PASSWORD } });
  const adminToken = (await adminLogin.json()).data.token;
  expect((await request.get(missing, { headers: { Authorization: 'Bearer ' + adminToken } })).status()).toBe(404);
  const uploads = [];
  for (let attempt = 0; attempt < 9; attempt++) uploads.push(await request.post('/api/depot/verifications?depotId=' + depotId, { headers, multipart: { operationId: crypto.randomUUID() } }));
  expect(uploads.some(response => response.status() === 429)).toBe(true);
  await owner.getByRole('button', { name: 'Tải dang-ky-kinh-doanh.pdf' }).first().click();
  await expect(owner.getByRole('alert').filter({ hasText: 'Bạn tải hồ sơ quá nhanh' })).toBeVisible();
  expect(errors).toEqual([]); await ownerContext.close(); await adminContext.close();
});
