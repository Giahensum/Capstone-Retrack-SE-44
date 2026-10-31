import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import { randomUUID } from 'node:crypto';
test('Lô trúng: nhập sắt/đồ cũ, phân bổ vốn, chặn xuất vượt tồn và Factory nhận lô', async ({ page, browser, request }) => {
    test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture lô trúng riêng trên DB test.');
    test.setTimeout(180000);
    page.setDefaultTimeout(12000);
    const f = JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE, 'utf8'));
    test.skip(!f.FactoryEmail, 'Cần Factory fixture riêng để kiểm tra nhận lô.');
    const contexts = [];
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    async function otherPage() { const context = await browser.newContext({ baseURL: process.env.E2E_BASE_URL }); contexts.push(context); const p = await context.newPage(); p.setDefaultTimeout(12000); p.on('pageerror', e => errors.push(e.message)); return p; }
    async function login(p, email) { await p.goto('/login'); await p.getByLabel('Email', { exact: true }).fill(email); await p.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_FINISH_PASSWORD); await p.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(p).not.toHaveURL(/\/login$/); }
    async function openAgreement() { await page.goto(`/depot/liquidations?lotId=${f.LotId}`); await page.getByRole('button', { name: 'Mở thỏa thuận & giao nhận', exact: true }).click(); }
    const agreement = page.getByRole('dialog', { name: 'Thỏa thuận mua lô trúng', exact: true });
    try {
        const seller = await otherPage();
        await login(page, f.OwnerEmail);
        await login(seller, f.SellerEmail);
        const auth = await request.post('/api/auth/login', { data: { email: f.OwnerEmail, password: process.env.E2E_FINISH_PASSWORD } });
        const headers = { Authorization: `Bearer ${(await auth.json()).data.token}` };
        const get = async (path) => (await (await request.get(`/api/depot/${path}${path.includes('?') ? '&' : '?'}depotId=${f.DepotId}`, { headers })).json()).data;
        const stock = async () => ((await get('inventory')).find(x => x.materialType === 'IRON') ?? { receivedKg: 0, reservedKg: 0, availableKg: 0 });
        async function sellerStep(label) { await seller.goto(`/seller/liquidations/${f.LotId}`); await seller.getByRole('button', { name: label, exact: true }).click(); await seller.getByRole('dialog').getByRole('button', { name: label, exact: true }).click(); await expect(seller.getByRole('dialog')).toHaveCount(0); }
        // Chưa xác nhận tiền/giao nhận: API cũng phải chặn, không chỉ ẩn nút.
        const early = await request.post(`/api/depot/stock/receipts?depotId=${f.DepotId}`, { headers, data: { operationId: randomUUID(), bulkAgreementId: f.AgreementId, sourceType: 'BULK_PURCHASE', reference: 'Không được nhập sớm', receivedAt: new Date().toISOString(), purchaseCost: 10000000, costsConfirmed: false, expenses: [], lines: [{ kind: 'MATERIAL', materialType: 'IRON', name: 'Sắt', unit: 'kg', quantity: 1000, referencePrice: 0, directCost: 0 }] } });
        expect(early.status()).toBe(409);
        await openAgreement();
        await agreement.getByRole('checkbox').check();
        await agreement.getByRole('button', { name: 'Ký hợp đồng đặt cọc', exact: true }).click();
        await expect(agreement).toContainText('Đang chờ Seller ký');
        await sellerStep('Ký hợp đồng đặt cọc');
        await openAgreement();
        await agreement.getByLabel(/Tham chiếu \/ nội dung chuyển cọc/).fill('COC-TEST-1000000');
        await agreement.getByRole('checkbox').check();
        await agreement.getByRole('button', { name: 'Báo đã chuyển cọc', exact: true }).click();
        await expect(agreement).toContainText('Chờ Seller xác nhận nhận cọc');
        await sellerStep('Tôi đã nhận tiền cọc');
        await openAgreement();
        await agreement.getByLabel(/Tham chiếu \/ nội dung đã chuyển/).fill('CONLAI-TEST-9000000');
        await agreement.getByRole('checkbox').check();
        await agreement.getByRole('button', { name: 'Báo đã chuyển tiền', exact: true }).click();
        await expect(agreement).toContainText('Chờ Seller xác nhận nhận đủ tiền');
        await sellerStep('Tôi đã nhận đủ tiền');
        await sellerStep('Tôi đã bàn giao lô');
        await openAgreement();
        await agreement.getByRole('checkbox').check();
        await agreement.getByRole('button', { name: 'Xác nhận đã nhận đủ hàng về kho', exact: true }).click();
        await agreement.getByRole('button', { name: 'Lập phiếu nhập từ lô trúng', exact: true }).click();
        const form = page.getByRole('dialog', { name: 'Lập phiếu nhập kho đa nguồn' });
        await expect(form.getByRole('combobox', { name: 'Nguồn hàng', exact: true })).toContainText('Mua lô trúng giá');
        await expect(form.getByLabel('Giá mua cả lô / giá trị tồn đầu kỳ (đ)')).toHaveAttribute('readonly', '');
        await form.getByLabel('Số lượng (kg)').fill('1000');
        await form.getByRole('button', { name: 'Thêm dòng hàng' }).click();
        const second = form.locator('section').filter({ has: page.getByRole('heading', { name: 'Dòng 2', exact: true }) });
        await second.getByRole('combobox', { name: 'Nhóm hàng', exact: true }).selectOption('REUSABLE');
        await second.getByLabel('Tên đồ / hãng / model').fill('Ghế lô trúng kiểm thử');
        await second.getByLabel('Số lượng (cái)').fill('10');
        for (const [i, category, amount, note] of [[0, 'LABOR', '1000000', 'Công thu gom thực tế kiểm thử'], [1, 'FUEL', '200000', 'Xăng xe kiểm thử']]) {
            await form.getByRole('button', { name: 'Thêm chi phí', exact: true }).click();
            await form.getByRole('combobox', { name: `Nhóm chi phí ${i + 1}`, exact: true }).selectOption(category);
            await form.getByLabel('Số tiền (đ)', { exact: true }).nth(i).fill(amount);
            await form.getByLabel('Mô tả / căn cứ phân bổ').nth(i).fill(note);
        }
        await form.getByRole('checkbox').check();
        await form.getByLabel('Giá bán tham khảo / kg (đ)').fill('6000');
        await form.getByLabel('Giá bán tham khảo / cái (đ)').fill('500000');
        const saving = page.waitForResponse(r => r.url().includes('/stock/receipts?') && r.request().method() === 'POST');
        await form.getByRole('button', { name: 'Lưu phiếu nháp' }).click();
        const saved = await saving;
        expect(saved.status()).toBe(200);
        const receipt = (await saved.json()).data;
        await expect(form).toHaveCount(0);
        expect(receipt.purchaseCost).toBe(10000000);
        expect(receipt.totalCost).toBe(11200000);
        expect(receipt.lines.map(x => x.allocatedCost)).toEqual([6109091, 5090909]);
        expect((await stock()).receivedKg).toBe(0);
        const payload = saved.request().postDataJSON();
        const repeated = await request.post(`/api/depot/stock/receipts?depotId=${f.DepotId}`, { headers, data: payload });
        expect(repeated.status()).toBe(200);
        expect((await repeated.json()).data.id).toBe(receipt.id);
        const duplicate = await request.post(`/api/depot/stock/receipts?depotId=${f.DepotId}`, { headers, data: { ...payload, operationId: randomUUID() } });
        expect(duplicate.status()).toBe(409);
        await openAgreement();
        await agreement.getByRole('link', { name: 'Đến phiếu nhập để kiểm tra / duyệt' }).click();
        await expect(page).toHaveURL(new RegExp(`receiptId=${receipt.id}`));
        const review = page.getByRole('dialog', { name: /^Phiếu nhập:/ });
        await expect(review).toContainText('6.109.091 đ');
        await expect(review).toContainText('5.090.909 đ');
        await review.getByRole('button', { name: 'Bổ sung / chỉnh chi phí trước ghi sổ' }).click();
        await expect(review.getByLabel('Giá mua cả lô (đ)', { exact: true })).toHaveAttribute('readonly', '');
        await review.getByLabel('Đóng', { exact: true }).click();
        await page.goto(`/depot/inventory?tab=receipts&receiptId=${receipt.id}`);
        await review.getByRole('button', { name: 'Xác nhận hàng thực nhận và ghi sổ' }).click();
        await expect(review).toHaveCount(0);
        expect((await stock()).receivedKg).toBe(1000);
        const postAgain = await request.patch(`/api/depot/stock/receipts/${receipt.id}/post?depotId=${f.DepotId}`, { headers });
        expect(postAgain.status()).toBe(200);
        expect((await stock()).receivedKg).toBe(1000);
        await page.goto('/depot/inventory?tab=reusable');
        const chairs = page.getByRole('row').filter({ hasText: 'Ghế lô trúng kiểm thử' });
        await expect(chairs).toContainText('10 cái');
        await chairs.getByRole('button', { name: 'Ghi bán ngoài' }).click();
        const sale = page.getByRole('dialog');
        await sale.getByLabel('Lượng đã giao (cái)').fill('3');
        await sale.getByLabel('Giá bán / cái (đ)').fill('650000');
        await sale.getByLabel('Chi phí bán và khoản liên quan đã xác định (đ)').fill('50000');
        await sale.getByLabel('Ghi chú giao nhận / chứng từ').fill('Đã giao 3 ghế kiểm thử');
        await sale.getByRole('button', { name: 'Xác nhận đã giao và ghi bán' }).click();
        await expect(sale).toHaveCount(0);
        await expect(chairs).toContainText('7 cái');
        const sales = await get('stock/sales');
        expect(sales.items[0].revenue).toBe(1950000);
        expect(sales.items[0].pretaxDifference).toBeCloseTo(372727.3, 3);
        const reusable = (await get('stock/reusable')).items[0];
        const oversold = await request.post(`/api/depot/stock/reusable/${reusable.id}/sales?depotId=${f.DepotId}`, { headers, data: { operationId: randomUUID(), quantity: 8, unitPrice: 650000, sellingCost: 0, note: 'Không được bán quá 7 ghế còn lại' } });
        expect(oversold.status()).toBe(409);
        const tooMuch = async (weight) => request.post(`/api/depot/batches?depotId=${f.DepotId}`, { headers, data: { operationId: randomUUID(), materialType: 'IRON', weightKg: weight, description: 'Kiểm thử chặn vượt tồn' } });
        expect((await tooMuch(1300)).status()).toBe(409);
        async function exportBatch(weight) { await page.goto('/depot/inventory'); await page.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click(); const modal = page.getByRole('dialog'); await modal.getByLabel('Loại phế liệu').selectOption('IRON'); await modal.getByLabel('Khối lượng (kg)', { exact: true }).fill(String(weight)); const waiting = page.waitForResponse(r => /\/api\/depot\/batches\?/.test(r.url()) && r.request().method() === 'POST'); await modal.getByRole('button', { name: 'Tạo lô xuất hàng', exact: true }).click(); const result = await waiting; expect(result.status()).toBe(200); await expect(modal).toHaveCount(0); return (await result.json()).data; }
        const reserved = await exportBatch(600);
        expect((await stock()).availableKg).toBe(400);
        expect((await tooMuch(500)).status()).toBe(409);
        await page.goto('/depot/batches');
        await page.getByRole('row').filter({ hasText: reserved.code }).getByTitle('Hủy lô', { exact: true }).click();
        await page.getByRole('button', { name: 'Xác nhận hủy', exact: true }).click();
        await expect(page.getByRole('dialog', { name: 'Xác nhận hủy lô hàng' })).toHaveCount(0);
        expect((await stock()).availableKg).toBe(1000);
        const batch = await exportBatch(1000);
        const costs = await get(`stock/batches/${batch.id}/costs`);
        expect(costs.sources[0].quantity).toBe(1000);
        expect(costs.sources[0].unitCost).toBeCloseTo(6109.091, 3);
        expect((await stock()).availableKg).toBe(0);
        const factory = await otherPage();
        await login(factory, f.FactoryEmail);
        await factory.getByRole('button', { name: /Sàn nguyên liệu/ }).click();
        await factory.locator('article.batch-card').filter({ hasText: batch.code }).getByRole('button', { name: /Xem lô/ }).click();
        await factory.getByRole('button', { name: 'Nhận lô hàng', exact: true }).click();
        await expect(factory.getByRole('row').filter({ hasText: batch.code })).toBeVisible();
        await page.goto('/depot/batches');
        await expect(page.getByRole('row').filter({ hasText: batch.code })).toContainText('Nhà máy kiểm thử sắt Ngũ Hành Sơn');
        expect((await stock()).availableKg).toBe(0);
        for (const width of [390, 1440]) {
            await page.setViewportSize({ width, height: 1000 });
            await page.goto(`/depot/inventory?tab=receipts&receiptId=${receipt.id}`);
            await expect(review).toBeVisible();
            const bounds = await review.boundingBox();
            expect(bounds.x).toBeGreaterThanOrEqual(0);
            expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
            await page.screenshot({ path: test.info().outputPath(`phieu-nhap-lo-trung-${width}.png`), fullPage: true });
            await page.keyboard.press('Escape');
        }
        expect(errors).toEqual([]);
    }
    finally {
        for (const context of contexts)
            await context.close();
    }
});
