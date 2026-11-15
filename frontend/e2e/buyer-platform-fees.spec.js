import { test, expect } from '@playwright/test';
import fs from 'node:fs';

const enabled = process.env.E2E_BUYER_FEES_FIXTURE === 'true';
const fixture = enabled ? JSON.parse(fs.readFileSync(process.env.E2E_BUYER_FEES_FIXTURE_FILE, 'utf8')) : null;

test('Phí bên mua: Seller–Employee–Depot và Factory–Depot–Admin giữ cùng snapshot sau tải lại', async ({ browser, request }) => {
  test.skip(!enabled, 'Cần fixture tài chính cô lập trong Retrack_TV2_test; không chạy trên dữ liệu thật.');
  test.setTimeout(120_000);
  const password = process.env.E2E_BUYER_FEES_PASSWORD;
  const contexts = [];
  async function login(email, prefix) {
    const context = await browser.newContext({ baseURL: process.env.E2E_BASE_URL, viewport: { width: 1440, height: 1000 } }); contexts.push(context);
    const page = await context.newPage(); page.setDefaultTimeout(15_000);
    await page.goto('/login');
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/${prefix}`));
    return page;
  }
  async function apiLogin(email) {
    const response = await request.post('/api/auth/login', { data: { email, password } });
    expect(response.ok()).toBeTruthy(); return (await response.json()).data.token;
  }
  async function api(method, path, token, body) {
    const response = await request.fetch(path, { method, headers: { Authorization: `Bearer ${token}` }, data: body });
    expect(response.ok(), `API ${method} ${path} trả ${response.status()}`).toBeTruthy(); return (await response.json()).data;
  }
  try {
    const employeeToken = await apiLogin(fixture.employeeEmail);
    const draft = await api('PUT', `/api/employee/pickup/${fixture.pickupId}/classification`, employeeToken,
      { expectedRevision: 0, items: [{ materialType: 'PET', weightKg: 10, pricePerKg: 1000 }] });
    expect(draft.netAmount).toBe(10000); expect(draft.platformFeeAmount).toBe(500);
    await api('POST', `/api/employee/pickup/${fixture.pickupId}/submit-weigh`, employeeToken, { expectedRevision: draft.revision });
    const seller = await login(fixture.sellerEmail, 'seller');
    await seller.goto(`/seller/requests/${fixture.pickupId}`);
    await expect(seller.getByText(/do kho vựa trả riêng/)).toBeVisible();
    await expect(seller.getByText(/10\.000\s*[₫đ]/).first()).toBeVisible();
    await seller.getByRole('button', { name: 'Đồng ý kết quả' }).click();
    await expect(seller.getByText('Đã xác nhận cân · Đang chờ bàn giao kho')).toBeVisible();
    await api('POST', `/api/employee/pickup/${fixture.pickupId}/finalize`, employeeToken, { expectedRevision: draft.revision });
    const depot = await login(fixture.ownerEmail, 'depot');
    await depot.goto('/depot/payments');
    await depot.getByLabel('Kho đang quản lý').selectOption(fixture.depotId);
    await depot.getByRole('row').filter({ hasText: fixture.pickupId.slice(0, 8) }).getByRole('button', { name: 'Đối soát' }).click();
    await expect(depot.getByText(/Tổng chi của kho:/)).toContainText('10.500 đ');
    await expect(depot.getByText(/Phí kho trả riêng/).last()).toBeVisible();
    await depot.setViewportSize({ width: 390, height: 844 });
    await depot.screenshot({ path: test.info().outputPath('depot-phi-rieng-390.png') });
    await depot.setViewportSize({ width: 1440, height: 1000 });
    await depot.getByLabel('Đường dẫn chứng từ').fill('https://example.com/buyer-fee-fixture.png');
    await depot.getByRole('checkbox').check();
    await depot.getByRole('button', { name: 'Xác nhận đã chuyển tiền' }).click();
    await seller.reload();
    await expect(seller.getByText(/Nếu đã nhận đủ số tiền/)).toContainText(/10\.000\s*[₫đ]/);
    await seller.getByRole('button', { name: 'Xác nhận đã nhận tiền' }).click();
    await seller.reload();
    const ownerToken = await apiLogin(fixture.ownerEmail);
    const paid = await api('GET', `/api/depot/payments/${fixture.pickupId}?depotId=${fixture.depotId}`, ownerToken);
    expect(paid.status).toBe('DONE'); expect(paid.netAmount).toBe(10000); expect(paid.buyerTotalAmount).toBe(10500);
    await seller.goto(`/seller/requests/${fixture.legacyPickupId}`);
    await expect(seller.getByText(/Đơn cũ: phí dịch vụ/)).toBeVisible();
    await expect(seller.getByText(/Nếu đã nhận đủ số tiền/)).toContainText(/9\.500\s*[₫đ]/);

    const factory = await login(fixture.factoryEmail, 'factory');
    await factory.getByRole('button', { name: /Quyết toán$/ }).click();
    await factory.getByRole('row').filter({ hasText: 'FEE-NEW-001' }).getByRole('button', { name: /Chi tiết/ }).click();
    await factory.getByRole('button', { name: 'Thỏa thuận giá & Quyết toán' }).click();
    await factory.getByLabel(/Đơn giá đã thỏa thuận sau KCS/).fill('1000');
    await factory.getByLabel('VAT theo hóa đơn của vựa').selectOption('TAXABLE:10');
    await factory.getByLabel('Số hóa đơn điện tử').fill('FIXTURE-HD');
    await factory.getByLabel('Ngày hóa đơn').fill(new Date().toLocaleDateString('en-CA'));
    await factory.getByLabel('Mã số thuế bên bán (vựa)').fill('0314892841');
    await factory.getByLabel('Mã tham chiếu chuyển khoản').fill('FIXTURE-BUYER-FEE');
    await expect(factory.getByText('Phí Factory trả riêng (5%)')).toBeVisible();
    await expect(factory.locator('.financial-total')).toContainText('11.000');
    await expect(factory.getByText('Tổng chi gồm nghĩa vụ phí nền tảng').locator('..')).toContainText('11.500');
    await factory.locator('.financial-total').scrollIntoViewIfNeeded();
    await factory.screenshot({ path: test.info().outputPath('factory-phi-rieng-1440.png') });
    await factory.getByRole('checkbox').last().check();
    await factory.getByRole('button', { name: 'Ghi nhận quyết toán', exact: true }).click();
    await expect(factory.getByText(/Mã tham chiếu: FIXTURE-BUYER-FEE/)).toBeVisible();
    await factory.reload();
    const factoryToken = await apiLogin(fixture.factoryEmail);
    const settled = await api('GET', `/api/factory/orders/${fixture.batchId}`, factoryToken);
    expect(settled.netPayableAmount).toBe(11000); expect(settled.buyerTotalAmount).toBe(11500); expect(settled.feeAmount).toBe(500);
    await depot.goto('/depot/batches');
    await depot.getByRole('row').filter({ hasText: 'FEE-NEW-001' }).getByTitle('Chi tiết lô hàng').click();
    await expect(depot.getByText('Phí do Factory trả riêng')).toBeVisible();
    await expect(depot.getByText('11.000 đ', { exact: true }).first()).toBeVisible();
    await depot.screenshot({ path: test.info().outputPath('depot-nhan-du-tien-1440.png') });

    const adminToken = await apiLogin(fixture.adminEmail);
    const now = new Date(); now.setHours(now.getHours() + 7);
    const invoices = await api('POST', '/api/admin/billing/invoices/generate', adminToken,
      { year: now.getUTCFullYear(), month: now.getUTCMonth() + 1 });
    expect(invoices).toHaveLength(2); expect(invoices.map(x => x.totalFeeAmount)).toEqual([500, 500]);
    const replay = await api('POST', '/api/admin/billing/invoices/generate', adminToken,
      { year: now.getUTCFullYear(), month: now.getUTCMonth() + 1 });
    expect(replay).toHaveLength(0);
  } finally { await Promise.all(contexts.map(context => context.close())); }
});
