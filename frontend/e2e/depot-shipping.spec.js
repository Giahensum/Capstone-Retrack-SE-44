import { test, expect } from '@playwright/test';

const depotId = '11111111-1111-4111-8111-111111111111';
const factoryId = '22222222-2222-4222-8222-222222222222';
const batchId = '33333333-3333-4333-8333-333333333333';
const quote = { id: '44444444-4444-4444-8444-444444444444', amount: 500000, factor: .9, estimatedCost: 450000, currency: 'VND', distanceKm: 12, expiresAt: new Date(Date.now() + 300000).toISOString() };
const batch = { id: batchId, code: 'LO-TEST', materialType: 'PET', weightKg: 1000, status: 'TRANSPORT_READY', targetFactoryId: factoryId, factoryName: 'Nhà máy kiểm thử', createdAt: new Date().toISOString() };

async function setup(page, mode = 'INTERNAL') {
  let shipment = null;
  let savedQuote = null;
  const requests = [];
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({ state: { token: 'test-browser-token', user: { role: 'DEPOT_OWNER', userId: 'owner-fixture', fullName: 'Chủ kho kiểm thử' }, isAuthenticated: true }, version: 0 })));
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let data = { items: [], totalCount: 0, sources: [], expenses: [], unreadCount: 0 };
    if (path.endsWith('/owned')) data = [{ id: depotId, name: 'Kho kiểm thử' }];
    else if (path.endsWith('/profile')) data = { name: 'Kho kiểm thử', verificationStatus: 'APPROVED' };
    else if (path.endsWith('/inventory')) data = [{ materialType: 'PET', availableKg: 2000 }];
    else if (path.endsWith('/partners')) data = { items: [{ id: factoryId, name: 'Nhà máy kiểm thử', canDirectDelivery: true }], totalCount: 1 };
    else if (path.endsWith('/shipping/quotes')) { const body = request.postDataJSON(); requests.push({ path, body }); data = { ...quote, factor: body.method === 'EXTERNAL' ? 1 : .9, estimatedCost: body.method === 'EXTERNAL' ? 500000 : 450000 }; }
    else if (path.endsWith('/sale-preview')) { requests.push({ path, body: request.postDataJSON() }); data = { weightKg: 1000, costOfGoods: 1000000, hasCompleteCost: true, revenue: 2000000, estimatedDifference: 550000 }; }
    else if (path.endsWith('/method')) { savedQuote = { ...quote, factor: 1, estimatedCost: 500000 }; data = 'Đã lưu'; }
    else if (path.endsWith('/book')) { requests.push({ path, body: request.postDataJSON() }); shipment = { id: 'shipment-fixture', providerOrderId: 'RT-TEST-001', status: 'ASSIGNING_DRIVER', amount: 500000, createdAt: new Date().toISOString() }; data = 'Đã nhận'; }
    else if (path.endsWith(`/shipping/batches/${batchId}`)) data = { method: mode, enabled: true, quote: savedQuote, shipment, events: shipment ? [{ status: shipment.status, createdAt: shipment.createdAt }] : [] };
    else if (path.endsWith(`/batches/${batchId}`)) data = { batch, transportStatus: 'PENDING', quality: null, settlement: null };
    else if (path.endsWith('/batches') && request.method() === 'POST') { requests.push({ path, body: request.postDataJSON() }); data = batch; }
    else if (path.endsWith('/batches')) data = { items: [batch], totalCount: 1 };
    await route.fulfill({ json: { success: true, data } });
  });
  return requests;
}

test('báo giá xe kho 90% được cộng vào dự kiến lãi và mất hiệu lực khi đổi khối lượng', async ({ page }) => {
  const requests = await setup(page);
  await page.goto('/depot/batches');
  await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  await page.getByLabel('Loại phế liệu').selectOption('PET');
  await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1000');
  await page.getByLabel('Hình thức bán').selectOption('direct');
  await page.getByRole('combobox', { name: 'Nhà máy', exact: true }).selectOption(factoryId);
  await page.getByRole('button', { name: 'Lấy báo giá vận chuyển' }).click();
  await expect(page.getByText('450.000 đ', { exact: true })).toBeVisible();
  await page.getByText('Ước tính lãi trước khi bán (tùy chọn)', { exact: true }).click();
  await page.getByLabel('Giá bán dự kiến / kg (đ)').fill('2000');
  await expect.poll(() => requests.filter(r => r.path.endsWith('/sale-preview')).at(-1)?.body.sellingCost).toBe(450000);
  await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1100');
  await expect(page.getByText('450.000 đ', { exact: true })).toHaveCount(0);
});

test('Owner dùng báo giá vừa lấy để đặt xe mà không phải bấm lưu riêng', async ({ page }) => {
  const requests = await setup(page, 'EXTERNAL');
  await page.goto(`/depot/batches?batchId=${batchId}`);
  await expect(page.getByRole('button', { name: 'Lấy báo giá vận chuyển' })).toBeEnabled();
  await page.getByRole('button', { name: 'Lấy báo giá vận chuyển' }).click();
  await expect.poll(() => requests.find(r => r.path.endsWith('/shipping/quotes'))?.body.factoryId).toBe(factoryId);
  await page.getByLabel('Tên người giao tại kho', { exact: true }).fill('Nguyễn Văn Giao');
  await page.getByLabel('Điện thoại người giao', { exact: true }).fill('0901234567');
  await page.getByLabel('Tên người nhận tại nhà máy', { exact: true }).fill('Trần Văn Nhận');
  await page.getByLabel('Điện thoại người nhận', { exact: true }).fill('0912345678');
  await expect(page.getByRole('button', { name: 'Xác nhận gọi xe' })).toBeDisabled();
  await page.getByRole('checkbox', { name: /Tôi đã kiểm tra loại xe/ }).check();
  await page.getByRole('button', { name: 'Xác nhận gọi xe' }).click();
  await expect(page.getByText('Mã vận đơn: RT-TEST-001')).toBeVisible();
  expect(requests.filter(r => r.path.endsWith('/book'))).toHaveLength(1);
  await page.reload();
  await expect(page.getByText('Mã vận đơn: RT-TEST-001')).toBeVisible();
});

test('tạo lô thuê ngoài tự mở bước nhập giao nhận, chưa đặt xe khi chỉ tạo lô', async ({ page }) => {
  const requests = await setup(page, 'EXTERNAL');
  await page.goto('/depot/batches');
  await page.getByRole('button', { name: 'Tạo lô xuất hàng mới' }).click();
  await page.getByLabel('Loại phế liệu').selectOption('PET');
  await page.getByLabel('Khối lượng (kg)', { exact: true }).fill('1000');
  await page.getByLabel('Hình thức bán').selectOption('direct');
  await page.getByRole('combobox', { name: 'Nhà máy', exact: true }).selectOption(factoryId);
  await page.getByRole('combobox', { name: 'Phương thức vận chuyển', exact: true }).selectOption('EXTERNAL');
  await page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Chi tiết lô LO-TEST' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Thông tin giao nhận và đặt xe' })).toBeVisible();
  await expect(page.getByText('Chưa đặt xe vận chuyển', { exact: true })).toBeVisible();
  expect(requests.find(r => r.path.endsWith('/batches'))?.body.shippingMethod).toBe('EXTERNAL');
  expect(requests.filter(r => r.path.endsWith('/book'))).toHaveLength(0);
});
