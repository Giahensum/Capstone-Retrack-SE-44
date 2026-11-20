import { test, expect } from '@playwright/test';

// Cần backend chạy với money.mode = PLATFORM và một đơn thu gom AWAITING_PAYMENT thật của chủ kho thử nghiệm.
test('Trang /pay/return lấy kết quả từ máy chủ, không tin tham số URL và giữ trạng thái sau tải lại', async ({ page }) => {
  test.skip(!process.env.E2E_MONEY_PICKUP_ID || !process.env.E2E_DEPOT_EMAIL, 'Cần E2E_MONEY_PICKUP_ID (đơn AWAITING_PAYMENT, money.mode=PLATFORM) và tài khoản chủ kho thử nghiệm.');
  await page.goto('/login');
  await page.getByLabel('Email', { exact: true }).fill(process.env.E2E_DEPOT_EMAIL);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_DEPOT_PASSWORD);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/depot/);
  // status=PAID trên URL không được coi là đã thanh toán: kết quả lấy từ payos-sync.
  const sync = page.waitForResponse(r => r.url().includes('/payos-sync') && r.request().method() === 'POST');
  await page.goto(`/pay/return?flow=PICKUP&source=${process.env.E2E_MONEY_PICKUP_ID}&code=00&cancel=false&status=PAID`);
  const dto = (await (await sync).json()).data;
  if (dto.status === 'PAID') await expect(page.getByText('Thanh toán thành công')).toBeVisible();
  else await expect(page.getByText('Chưa ghi nhận')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Về trang thanh toán' })).toHaveAttribute('href', '/depot/payments');
  await page.reload();
  await expect(page.getByText(/Thanh toán thành công|Chưa ghi nhận/)).toBeVisible();
});
