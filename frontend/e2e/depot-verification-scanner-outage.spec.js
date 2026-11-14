import { test, expect } from '@playwright/test';

test('Dịch vụ quét gián đoạn: báo chưa nộp và giữ biểu mẫu, không lưu hồ sơ nửa chừng', async ({ page, request }) => {
  test.skip(process.env.E2E_SCANNER_OUTAGE !== '1' || !process.env.E2E_VERIFY_OWNER || !process.env.E2E_VERIFY_PASSWORD,
    'Chỉ chạy với fixture riêng và API được cấu hình ClamAv tới daemon test không hoạt động.');
  const auth = await request.post('/api/auth/login', { data: { email: process.env.E2E_VERIFY_OWNER, password: process.env.E2E_VERIFY_PASSWORD } });
  const token = (await auth.json()).data.token;
  const headers = { Authorization: 'Bearer ' + token }, depotId = crypto.randomUUID();
  const created = await request.post('/api/depot/owned', { headers, data: { operationId: depotId, profile: { name: 'Kho thử lỗi dịch vụ quét', address: 'Ngũ Hành Sơn, Đà Nẵng' } } });
  expect(created.status()).toBe(200);
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env.E2E_VERIFY_OWNER);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_VERIFY_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/depot/);
  await page.goto('/depot/profile');
  await page.getByLabel('Giấy tờ kinh doanh', { exact: true }).setInputFiles({
    name: 'giay-to-fixture.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.7\nFixture khong phai giay to that\n%%EOF'),
  });
  await page.getByLabel('Ghi chú hồ sơ').fill('Giữ ghi chú khi dịch vụ quét bị lỗi');
  const completed = page.waitForResponse(response => response.request().method() === 'POST' && response.url().includes('/api/depot/verifications?'));
  await page.getByRole('button', { name: 'Nộp hồ sơ xác minh' }).click();
  const failure = await completed;
  expect(failure.status()).toBe(503);
  expect((await failure.json()).code).toBe('VERIFICATION_SCANNER_UNAVAILABLE');
  await expect(page.getByRole('alert').filter({ hasText: 'Hồ sơ chưa được nộp' })).toBeVisible();
  await expect(page.getByLabel('Ghi chú hồ sơ')).toHaveValue('Giữ ghi chú khi dịch vụ quét bị lỗi');
  await expect(page.getByRole('list', { name: 'Tệp đã chọn' })).toContainText('giay-to-fixture.pdf');
  const list = await request.get('/api/depot/verifications?depotId=' + depotId, { headers });
  expect((await list.json()).data.totalCount).toBe(0);
  await page.screenshot({ path: test.info().outputPath('scanner-chua-san-sang.png'), fullPage: true });
});
