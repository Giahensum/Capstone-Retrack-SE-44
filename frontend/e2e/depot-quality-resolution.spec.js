import { test, expect } from '@playwright/test';
import fs from 'node:fs';
test('KCS không đạt → chọn trả → Factory bàn giao → Depot nhận đủ và hoàn tồn sau reload', async ({ page, browser, request }) => {
  test.skip(!process.env.E2E_QUALITY_FIXTURE || !process.env.E2E_QUALITY_PASSWORD, 'Cần fixture QC riêng trong DB test.');
  test.setTimeout(120000);
  page.setDefaultTimeout(15000);
  const f=JSON.parse(fs.readFileSync(process.env.E2E_QUALITY_FIXTURE,'utf8'));
  const ownerContext=await browser.newContext({baseURL:process.env.E2E_BASE_URL}),owner=await ownerContext.newPage();
  owner.setDefaultTimeout(15000);
  const errors=[];for(const p of [page,owner])p.on('pageerror',e=>errors.push(e.message));
  const login=async(p,email)=>{await p.goto('/login');await p.getByLabel('Email',{exact:true}).fill(email);await p.getByLabel('Mật khẩu',{exact:true}).fill(process.env.E2E_QUALITY_PASSWORD);await p.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(p).not.toHaveURL(/\/login$/);};
  const ownerOpen=async()=>{await owner.goto('/depot/batches');await owner.getByRole('row').filter({hasText:f.Code}).getByTitle('Chi tiết lô hàng',{exact:true}).click();};
  try {
    await login(page,f.FactoryEmail);await login(owner,f.OwnerEmail);
    await page.getByRole('button',{name:/Trạm cân & KCS/}).click();
    await page.getByRole('row').filter({hasText:f.Code}).getByRole('button',{name:/Chi tiết/}).click();
    await page.getByRole('button',{name:'Kiểm tra & Chốt KCS',exact:true}).click();
    await page.getByLabel('Độ tinh khiết (%)').fill('50');await page.getByLabel('Độ ẩm (%)').fill('10');await page.getByLabel('Tạp chất (%)').fill('40');await page.getByLabel(/^Grade/).selectOption('C');
    await page.getByLabel(/^Quyết định/).selectOption('reject');await page.getByLabel(/^Hướng xử lý/).selectOption('RENEGOTIATE');
    await page.getByLabel(/Lý do từ chối/).fill('Vật liệu không đạt ngưỡng; xem xét mua theo hiện trạng');
    await page.getByRole('button',{name:'Chốt kết quả KCS',exact:true}).click();
    await expect(page.getByRole('button',{name:'Gửi đề xuất giá cho vựa',exact:true})).toBeVisible();
    await page.getByRole('button',{name:'Gửi đề xuất giá cho vựa',exact:true}).click();
    await expect(page.getByRole('dialog')).toContainText('Mua theo hiện trạng KCS không đạt');
    await page.getByRole('dialog').getByRole('button',{name:'Hủy',exact:true}).click();
    await page.getByRole('button',{name:'Chọn hướng xử lý KCS',exact:true}).click();
    await page.getByRole('dialog').getByRole('combobox').selectOption('RETURN');await page.getByLabel('Ghi chú lựa chọn hướng xử lý').fill('Hai bên chọn trả toàn bộ lô về kho');await page.getByRole('button',{name:'Lưu hướng xử lý',exact:true}).click();
    await page.getByRole('button',{name:'Xác nhận bàn giao trả',exact:true}).click();
    const handover=page.getByRole('dialog');
    await handover.getByLabel('Tham chiếu bàn giao trả').fill('TRA-QC-UI');await handover.getByLabel('Ghi chú bàn giao trả').fill('Đã bàn giao nguyên lô để chở về kho');
    await handover.getByLabel(/Chứng từ bàn giao trả/).setInputFiles({name:'bien-ban-fixture.png',mimeType:'image/png',buffer:Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=','base64')});
    await handover.getByRole('checkbox').check();await handover.getByRole('button',{name:'Xác nhận đã bàn giao trả',exact:true}).click();await expect(handover).toHaveCount(0);
    await ownerOpen();const detail=owner.getByRole('dialog');await expect(detail).toContainText('TRA-QC-UI');
    await detail.getByLabel('Khối lượng thực nhận về kho (kg)').fill('9');await expect(detail.getByRole('button',{name:'Xác nhận nhận đủ lô trả về'})).toBeDisabled();
    await detail.getByLabel('Khối lượng thực nhận về kho (kg)').fill('10');await detail.getByLabel('Tham chiếu nhận hàng trả').fill('NHAN-QC-UI');await detail.getByLabel('Ghi chú cân, kiểm nhận').fill('Đã cân 10kg và kiểm đủ toàn bộ lô về kho');await detail.getByRole('checkbox',{name:/Tôi đã cân, kiểm/}).check();
    await detail.getByRole('button',{name:'Xác nhận nhận đủ lô trả về',exact:true}).click();await expect(detail).toContainText('Đã hoàn nguồn tồn đúng một lần');
    await owner.reload();await owner.getByRole('row').filter({hasText:f.Code}).getByTitle('Chi tiết lô hàng',{exact:true}).click();await expect(detail).toContainText('NHAN-QC-UI');
    const downloadEvent=owner.waitForEvent('download');await detail.getByRole('button',{name:'Tải biên bản bàn giao trả',exact:true}).click();expect(await (await downloadEvent).failure()).toBeNull();
    for(const width of [390,1440]){await owner.setViewportSize({width,height:900});await owner.screenshot({path:test.info().outputPath(`nhan-tra-${width}.png`),fullPage:true});}
    const auth=await request.post('/api/auth/login',{data:{email:f.OwnerEmail,password:process.env.E2E_QUALITY_PASSWORD}});const headers={Authorization:'Bearer '+(await auth.json()).data.token};
    const inventory=await request.get('/api/depot/inventory?depotId='+f.Depot,{headers});expect(inventory.status()).toBe(200);expect((await inventory.json()).data.find(x=>x.materialType==='PET'||x.material==='PET').availableKg).toBe(100);
    expect(errors).toEqual([]);
  } finally {await ownerContext.close();}
});



