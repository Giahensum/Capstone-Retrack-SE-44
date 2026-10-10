import { test, expect } from '@playwright/test';

const depotId = '11111111-1111-1111-1111-111111111111';
const paymentId = '22222222-2222-2222-2222-222222222222';
const payment = {
  id: paymentId, depotId, sellerName: 'Người bán kiểm thử', sellerPhone: '0900000000',
  collectorName: 'Nhân viên kiểm thử', address: 'Ngũ Hành Sơn, Đà Nẵng', status: 'AWAITING_PAYMENT',
  grossAmount: 100000, platformFeePercentage: 5, platformFeeAmount: 5000, netAmount: 95000,
  paymentProofUrl: null, checkinImageUrl: null,
  items: [{ materialType: 'PET', weightKg: 10, pricePerKg: 10000, subTotal: 100000 }],
};

async function openPayment(page, current = payment) {
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({
    state: { token: 'ui-test-token', user: { userId: '33333333-3333-3333-3333-333333333333', fullName: 'Chủ kho kiểm thử', role: 'DEPOT_OWNER' }, isAuthenticated: true },
    version: 0,
  })));
  await page.route('**/api/depot/owned', route => route.fulfill({ json: { data: [{ id: depotId, name: 'Kho kiểm thử' }] } }));
  await page.route('**/api/depot/payments/summary?**', route => route.fulfill({ json: { data: { pendingCount: 1, pendingAmount: 95000, sentTodayAmount: 0 } } }));
  await page.route('**/api/depot/payments?**', route => route.fulfill({ json: { data: { items: [payment], totalCount: 1 } } }));
  await page.route(`**/api/depot/payments/${paymentId}?**`, route => route.fulfill({ json: { data: current } }));
  await page.goto('/depot/payments');
  await page.getByRole('button', { name: 'Đối soát' }).click();
  await expect(page.getByRole('heading', { name: /Đối soát đơn/ })).toBeVisible();
}

test('Depot đối soát tiền và chứng từ trước khi ghi nhận', async ({ page }) => {
  await openPayment(page);
  const submit = page.getByRole('button', { name: 'Xác nhận đã chuyển tiền' });
  await expect(page.getByText('95.000 đ').first()).toBeVisible();
  await expect(submit).toBeDisabled();
  await page.getByLabel('Đường dẫn chứng từ').fill('https://example.com/proof.png');
  await expect(submit).toBeDisabled();
  await page.getByRole('checkbox').check();
  await expect(submit).toBeEnabled();
  await page.screenshot({ path: test.info().outputPath('depot-payment-dialog.png') });
});

test('Đơn đã được xử lý trong lúc mở danh sách không thể duyệt lại', async ({ page }) => {
  await openPayment(page, { ...payment, status: 'PAYMENT_SENT', paymentProofUrl: 'https://example.com/proof.png' });
  await expect(page.getByText('Chờ người bán xác nhận', { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Xem chứng từ' })).toHaveAttribute('href', 'https://example.com/proof.png');
  await expect(page.getByRole('button', { name: 'Xác nhận đã chuyển tiền' })).toHaveCount(0);
});

test('Gửi xác nhận chỉ một lần sau khi đối soát', async ({ page }) => {
  const requests = [];
  await page.route(`**/api/depot/pickup-requests/${paymentId}/payment-sent`, async route => {
    requests.push(route.request().postDataJSON());
    await route.fulfill({ json: { data: { ...payment, status: 'PAYMENT_SENT' } } });
  });
  await openPayment(page);
  await page.getByLabel('Đường dẫn chứng từ').fill('https://example.com/proof.png');
  await page.getByRole('checkbox').check();
  await page.getByRole('button', { name: 'Xác nhận đã chuyển tiền' }).click();
  await expect(page.getByRole('heading', { name: /Đối soát đơn/ })).toHaveCount(0);
  expect(requests).toEqual([{ paymentProofUrl: 'https://example.com/proof.png' }]);
});

test('Seller thấy chứng từ trước khi xác nhận nhận tiền', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({
    state: { token: 'ui-test-token', user: { userId: '44444444-4444-4444-4444-444444444444', fullName: 'Người bán kiểm thử', role: 'SELLER' }, isAuthenticated: true },
    version: 0,
  })));
  await page.route(`**/api/seller/pickups/${paymentId}`, route => route.fulfill({ json: { data: {
    ...payment, status: 'PAYMENT_SENT', paymentProofUrl: 'https://example.com/proof.png',
    description: 'PET', createdAt: '2026-10-09T00:00:00Z',
  } } }));
  await page.goto(`/seller/requests/${paymentId}`);
  await expect(page.getByRole('link', { name: 'Xem chứng từ chuyển khoản' })).toHaveAttribute('href', 'https://example.com/proof.png');
  await expect(page.getByRole('button', { name: 'Xác nhận đã nhận tiền' })).toBeVisible();
  await expect(page.getByText('Đã thanh toán', { exact: true }).last()).toBeVisible();
});
