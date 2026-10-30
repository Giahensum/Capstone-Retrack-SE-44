import { test, expect } from '@playwright/test';

test.use({ trace: 'off' });

// API thật phải trỏ tới DB test riêng; tài khoản fixture được tạo mới mỗi lần chạy.
test('Seller tạo lô, nhận thông báo, đọc/chưa đọc, mở đúng lô và tải lại', async ({ page, request }) => {
  test.skip(process.env.E2E_TARGET_DATABASE !== 'Retrack_TV2_test' || !process.env.E2E_SELLER_EMAIL || !process.env.E2E_SELLER_PASSWORD,
    'Cần API riêng trên Retrack_TV2_test và thông tin tài khoản fixture mới qua biến môi trường.');
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  const email = process.env.E2E_SELLER_EMAIL;
  const password = process.env.E2E_SELLER_PASSWORD;
  const registered = await request.post('/api/auth/register', {
    data: { email, password, fullName: 'Seller kiểm thử thông báo', phone: '0900000000', role: 'SELLER' },
  });
  expect(registered.ok()).toBe(true);
  const auth = (await registered.json()).data;
  const headers = { Authorization: `Bearer ${auth.token}` };
  const inMinutes = (minutes) => new Date(Date.now() + minutes * 60_000).toISOString();
  const payload = {
    operationId: crypto.randomUUID(), title: 'Lô kiểm thử thông báo Seller',
    description: 'Lô thanh lý dùng riêng để kiểm thử thông báo.', scopeDetails: 'Toàn bộ vật tư được mô tả trong lô kiểm thử.',
    collectionTerms: 'Đối chiếu tiền thực nhận trước khi bàn giao hàng.', publicArea: 'Đà Nẵng', address: '12 Đường kiểm thử Đà Nẵng',
    contactPhone: '0900000000', imageUrls: [], surveyStart: inMinutes(5), surveyEnd: inMinutes(65),
    biddingStart: inMinutes(70), biddingEnd: inMinutes(130), collectionDeadline: inMinutes(1440),
    startingPrice: 1000000, bidIncrement: 100000, depositPercent: 10,
  };
  for (let attempt = 0; attempt < 2; attempt++) {
    const created = await request.post('/api/seller/liquidations', { headers, data: payload });
    expect(created.ok()).toBe(true);
  }
  const foreign = await request.post('/api/auth/register', {
    data: { email: email.replace('@', '-other@'), password, fullName: 'Seller khác', phone: '0900000001', role: 'SELLER' },
  });
  expect(foreign.ok()).toBe(true);
  const foreignHeaders = { Authorization: `Bearer ${(await foreign.json()).data.token}` };
  const notices = await request.get('/api/seller/notifications', { headers });
  const notice = (await notices.json()).data.items[0];
  expect(notice.targetId).toBe(payload.operationId);
  expect((await request.patch(`/api/seller/notifications/${notice.id}/read`, { headers: foreignHeaders })).status()).toBe(404);

  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/seller/);
  await page.goto('/seller/notifications');
  await page.getByRole('button', { name: 'Đấu giá / Thanh lý', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đã đăng lô thanh lý', exact: true })).toHaveCount(1);
  await page.getByRole('link', { name: 'Xem lô', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/seller/liquidations/${payload.operationId}$`));
  await expect(page.getByRole('heading', { name: payload.title, exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: payload.title, exact: true })).toBeVisible();

  await page.goto('/seller/notifications');
  await page.getByRole('button', { name: 'Đánh dấu chưa đọc', exact: true }).click();
  await page.reload();
  await expect(page.getByRole('button', { name: /Mở thông báo, 1 chưa đọc/ })).toBeVisible();
  await page.getByRole('button', { name: /Mở thông báo/ }).click();
  await page.getByRole('dialog', { name: 'Thông báo người bán' }).getByRole('button', { name: /Đã đăng lô thanh lý/ }).click();
  await expect(page).toHaveURL(new RegExp(`/seller/liquidations/${payload.operationId}$`));

  const cancelled = await request.post(`/api/seller/liquidations/${payload.operationId}/cancel`, { headers, data: { reason: 'Kết thúc lô kiểm thử thông báo' } });
  expect(cancelled.ok()).toBe(true);
  await page.goto('/seller/notifications');
  await expect(page.getByRole('heading', { name: 'Đã hủy lô thanh lý', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc', exact: true }).click();
  await page.reload();
  expect((await (await request.get('/api/seller/notifications/unread-count', { headers })).json()).data).toBe(0);
  expect(errors).toEqual([]);
});
