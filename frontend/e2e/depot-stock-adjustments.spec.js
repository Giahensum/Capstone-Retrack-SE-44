import { test, expect } from '@playwright/test';

test('chi phí sau ghi sổ có lịch sử, giữ snapshot cũ và áp dụng cho lần xuất mới', async ({ page, request }) => {
  test.skip(!process.env.E2E_STOCK_EMAIL || !process.env.E2E_STOCK_PASSWORD, 'Cần Owner fixture riêng, kho đã được xác minh.');
  const login = await request.post('/api/auth/login', { data: { email: process.env.E2E_STOCK_EMAIL, password: process.env.E2E_STOCK_PASSWORD } });
  const headers = { Authorization: `Bearer ${(await login.json()).data.token}` };
  const depots = (await (await request.get('/api/depot/owned', { headers })).json()).data;
  expect(depots.length).toBeGreaterThan(0); const depotId = depots[0].id;
  const reference = `E2E chi phí bổ sung ${Date.now()}`;
  const receiptResponse = await request.post(`/api/depot/stock/receipts?depotId=${depotId}`, { headers, data: {
    operationId: crypto.randomUUID(), sourceType: 'EXTERNAL_PURCHASE', reference, receivedAt: new Date().toISOString(), purchaseCost: 1000, costsConfirmed: true,
    lines: [{ kind: 'MATERIAL', materialType: 'COPPER', name: 'Đồng', unit: 'kg', quantity: 100, referencePrice: 10, directCost: 0 }], expenses: [] } });
  expect(receiptResponse.ok()).toBe(true); const receipt = (await receiptResponse.json()).data;
  expect((await request.patch(`/api/depot/stock/receipts/${receipt.id}/post?depotId=${depotId}`, { headers })).ok()).toBe(true);
  const batch = async () => { const response = await request.post(`/api/depot/batches?depotId=${depotId}`, { headers, data: { operationId: crypto.randomUUID(), materialType: 'COPPER', weightKg: 10 } }); expect(response.ok()).toBe(true); return (await response.json()).data; };
  const before = await batch();
  await page.goto('/login'); await page.getByLabel('Email', { exact: true }).fill(process.env.E2E_STOCK_EMAIL);
  await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_STOCK_PASSWORD); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).toHaveURL(/\/depot/, { timeout: 20_000 }); await page.goto('/depot/inventory?tab=receipts');
  await page.getByRole('row').filter({ hasText: reference }).getByRole('button', { name: 'Chi tiết / duyệt' }).click();
  await page.getByLabel('Số tiền điều chỉnh (đ)').fill('500');
  await page.getByLabel('Lý do / chứng từ điều chỉnh').fill('Chi phí xăng xe đối soát bổ sung');
  await page.getByRole('button', { name: 'Ghi khoản điều chỉnh', exact: true }).click();
  await expect(page.getByRole('article').filter({ hasText: 'Chi phí xăng xe đối soát bổ sung' })).toBeVisible();
  await page.getByRole('dialog').getByLabel('Đóng').click(); await page.reload();
  await page.getByRole('row').filter({ hasText: reference }).getByRole('button', { name: 'Chi tiết / duyệt' }).click();
  await expect(page.getByRole('article').filter({ hasText: 'Chi phí xăng xe đối soát bổ sung' })).toBeVisible();
  const oldCost = (await (await request.get(`/api/depot/stock/batches/${before.id}/costs?depotId=${depotId}`, { headers })).json()).data;
  expect(oldCost.costOfGoods).toBe(100); expect(oldCost.needsCostReconciliation).toBe(true);
  const after = await batch();
  const newCost = (await (await request.get(`/api/depot/stock/batches/${after.id}/costs?depotId=${depotId}`, { headers })).json()).data;
  expect(newCost.costOfGoods).toBe(150); expect(newCost.needsCostReconciliation).toBe(false);
});
