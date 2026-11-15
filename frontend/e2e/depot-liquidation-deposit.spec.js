import { test, expect } from '@playwright/test';
import fs from 'node:fs';
test('Seller và Depot ký, xác nhận cọc/tiền/giao nhận và lập phiếu đúng giá trúng', async ({ page, browser }) => {
  test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture riêng trên DB test.');
  test.setTimeout(120000);
  const f=JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE,'utf8'));
  const sellerContext=await browser.newContext();const seller=await sellerContext.newPage();
  const errors=[];for(const p of [page,seller])p.on('pageerror',e=>errors.push(e.message));
  async function login(p,email){await p.goto('/login');await p.getByLabel('Email',{exact:true}).fill(email);await p.getByLabel('Mật khẩu',{exact:true}).fill(process.env.E2E_FINISH_PASSWORD);await p.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(p).not.toHaveURL(/\/login$/);}
  async function ownerOpen(){await page.goto(`/depot/liquidations?lotId=${f.LotId}`);await page.getByRole('button',{name:'Mở thỏa thuận & giao nhận',exact:true}).click();}
  async function sellerOpen(){await seller.goto(`/seller/liquidations/${f.LotId}`);await expect(seller.getByRole('heading',{name:'Lô hợp đồng hoàn thiện fixture',exact:true})).toBeVisible();}
  async function sellerStep(label){await seller.getByRole('button',{name:label,exact:true}).click();await seller.getByRole('dialog').getByRole('button',{name:label,exact:true}).click();await expect(seller.getByRole('dialog')).toHaveCount(0);}
  const agreement=page.getByRole('dialog',{name:'Thỏa thuận mua lô trúng',exact:true});
  try {
    await login(page,f.OwnerEmail);await login(seller,f.SellerEmail);await ownerOpen();
    await agreement.getByLabel('Tôi đã đọc và đồng ý ký hợp đồng đặt cọc mua trọn lô này.').check();await agreement.getByRole('button',{name:'Ký hợp đồng đặt cọc',exact:true}).click();await expect(agreement).toContainText('Đang chờ Seller ký');
    await sellerOpen();await sellerStep('Ký hợp đồng đặt cọc');await ownerOpen();
    await agreement.getByRole('button',{name:'Xem / In / Tải PDF'}).click();const contract=page.getByRole('dialog',{name:'Hợp đồng đặt cọc',exact:true});await expect(contract).toContainText('Lô hợp đồng hoàn thiện fixture');
    const downloading=page.waitForEvent('download');await contract.getByRole('button',{name:'Tải file PDF',exact:true}).click();const download=await downloading;const pdfPath=test.info().outputPath('hop-dong-fixture.pdf');await download.saveAs(pdfPath);const bytes=fs.readFileSync(pdfPath);expect(bytes.subarray(0,5).toString()).toBe('%PDF-');expect(bytes.length).toBeGreaterThan(10000);await contract.getByLabel('Đóng',{exact:true}).click();
    await agreement.getByLabel(/Tham chiếu \/ nội dung chuyển cọc/).fill('COC-FIXTURE-1000000');await agreement.getByLabel('Tôi đã chuyển đủ tiền cọc cho Seller.').check();await agreement.getByRole('button',{name:'Báo đã chuyển cọc',exact:true}).click();await expect(agreement).toContainText('Chờ Seller xác nhận nhận cọc');
    await sellerOpen();await sellerStep('Tôi đã nhận tiền cọc');await ownerOpen();
    await agreement.getByLabel(/Tham chiếu \/ nội dung đã chuyển/).fill('CON-LAI-FIXTURE-9000000');await agreement.getByRole('checkbox').check();await agreement.getByRole('button',{name:'Báo đã chuyển tiền',exact:true}).click();await expect(agreement).toContainText('Chờ Seller xác nhận nhận đủ tiền');
    await sellerOpen();await sellerStep('Tôi đã nhận đủ tiền');await sellerStep('Tôi đã bàn giao lô');await ownerOpen();
    await agreement.getByLabel('Tôi đã thu gom đủ và chở hàng thực tế về kho.').check();await agreement.getByRole('button',{name:'Xác nhận đã nhận đủ hàng về kho',exact:true}).click();await agreement.getByRole('button',{name:'Lập phiếu nhập từ lô trúng',exact:true}).click();
    const receipt=page.getByRole('dialog',{name:'Lập phiếu nhập kho đa nguồn'});await expect(receipt.getByLabel('Giá mua cả lô / giá trị tồn đầu kỳ (đ)')).toHaveValue('10000000');await receipt.getByLabel('Số lượng (kg)').fill('100');await receipt.getByRole('button',{name:'Lưu phiếu nháp',exact:true}).click();await expect(receipt).toHaveCount(0);await ownerOpen();await expect(agreement).toContainText('Đã lập phiếu nhập');expect(errors).toEqual([]);
  } finally {await sellerContext.close();}
});
