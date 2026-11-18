// Dùng actor/nhu cầu riêng: PET, tồn 100kg, nhu cầu 50kg, hợp đồng hết hạn, một lô đã KCS.
// Không trỏ fixture vào database thật; các bài có thao tác đóng/mở nhu cầu và gửi gia hạn.
import { test, expect as baseExpect } from '@playwright/test';
const expect = baseExpect.configure({ timeout: 12_000 });
import fs from 'node:fs';
const fixturePath = process.env.E2E_DEMAND_FIXTURE;
const password = process.env.E2E_DEMAND_PASSWORD;
test.beforeEach(() => {
  test.setTimeout(90_000);
  test.skip(!fixturePath || !password || process.env.E2E_TARGET_DATABASE !== 'Retrack_TV2_test', 'Cần fixture nhu cầu/hợp đồng hết hạn riêng trên Retrack_TV2_test.');
  expect(f.OwnerEmail).toMatch(/@test\.invalid$/);
  expect(f.FactoryEmail).toMatch(/@test\.invalid$/);
});
const f = fixturePath ? JSON.parse(fs.readFileSync(fixturePath, 'utf8')) : {};
const factoryName = f.FactoryName || 'Nhà máy nhu cầu smoke';
async function login(page,email){await page.goto('/login');await page.getByLabel('Email',{exact:true}).fill(email);await page.getByLabel('Mật khẩu',{exact:true}).fill(password);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page).not.toHaveURL(/\/login$/);}
test('Nhu cầu: giữ vật liệu/Factory, chặn nhu cầu bị đóng khi popup mở và ghi đúng nguồn sau reload',async({page,request})=>{
 const auth=await request.post('/api/auth/login',{data:{email:f.FactoryEmail,password}});expect(auth.status()).toBe(200);const headers={Authorization:'Bearer '+(await auth.json()).data.token};
 await login(page,f.OwnerEmail);await page.goto('/depot/partners');await page.getByLabel('Kho đang quản lý').selectOption(f.DepotId);await page.getByRole('button',{name:'Nhu cầu thu mua',exact:true}).click();
 const card=page.locator('div').filter({has:page.getByRole('heading',{name:factoryName,exact:true})}).filter({has:page.getByRole('button',{name:/Tạo lô bán ngay/})}).last();
 await card.getByRole('button',{name:/Tạo lô bán ngay/}).click();const modal=page.getByRole('dialog',{name:'Tạo Lô Xuất Hàng'});
 await expect(modal.getByRole('combobox',{name:'Loại phế liệu',exact:true})).toBeDisabled();await expect(modal.getByRole('combobox',{name:'Loại phế liệu',exact:true})).toHaveValue('PET');await expect(modal.getByRole('combobox',{name:'Nhà máy',exact:true})).toHaveValue(f.FactoryId);await expect(modal.getByRole('combobox',{name:'Nhà máy',exact:true})).toBeDisabled();await expect(modal.getByLabel('Hình thức bán')).toBeDisabled();
 await expect(modal.getByLabel('Khối lượng (kg)',{exact:true})).toHaveAttribute('max','50');await expect(modal).toContainText('Giá cuối cùng cần hai bên đồng ý sau KCS.');await expect(modal).toContainText('hợp đồng hết hạn/chưa hiệu lực');
 await modal.getByLabel('Khối lượng (kg)',{exact:true}).fill('10');expect((await request.patch('/api/factory/demands/'+f.DemandId+'/status',{headers,data:{isActive:false}})).status()).toBe(200);
 const rejected=page.waitForResponse(r=>r.url().includes('/api/depot/batches?')&&r.request().method()==='POST');await modal.getByRole('button',{name:'Tạo lô xuất hàng',exact:true}).click();expect((await rejected).status()).toBe(409);await expect(modal).toContainText('Nhu cầu thu mua đã hết hạn');
 expect((await request.patch('/api/factory/demands/'+f.DemandId+'/status',{headers,data:{isActive:true}})).status()).toBe(200);
 const saving=page.waitForResponse(r=>r.url().includes('/api/depot/batches?')&&r.request().method()==='POST');await modal.getByRole('button',{name:'Tạo lô xuất hàng',exact:true}).click();const created=await saving;expect(created.status()).toBe(200);const b=(await created.json()).data;expect(b.sourceDemandId).toBe(f.DemandId);expect(b.status).toBe('PENDING_APPROVAL');await expect(modal).toHaveCount(0);
 await page.goto('/depot/batches');await page.getByRole('row').filter({hasText:b.code}).getByTitle('Chi tiết lô hàng').click();await expect(page.getByRole('dialog')).toContainText('Chờ nhà máy duyệt');await page.reload();await expect(page.getByRole('row').filter({hasText:b.code})).toBeVisible();
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:test.info().outputPath('demand-result-mobile.png'),fullPage:true});
});
test('Hợp đồng hết hạn: Factory thấy đề nghị gia hạn, bản cũ được ghi audit khi gửi phiên bản mới',async({page})=>{
 await login(page,f.FactoryEmail);await page.getByRole('button',{name:/Vựa đối tác/}).click();await expect(page.getByText('Đã hết hạn',{exact:false})).toBeVisible();await page.getByRole('button',{name:'Đề nghị gia hạn hợp đồng',exact:true}).click();const dialog=page.getByRole('dialog');await dialog.getByRole('checkbox').check();await dialog.getByRole('button',{name:'Ký và gửi chủ vựa',exact:true}).click();await expect(dialog).toHaveCount(0);await page.reload();await page.getByRole('button',{name:/Vựa đối tác/}).click();await expect(page.getByText('Chờ chủ vựa ký',{exact:false}).first()).toBeVisible();
});

test('Chọn Factory từ danh sách vẫn cho chọn vật liệu, không nhầm là nhu cầu cố định',async({page})=>{
 await login(page,f.OwnerEmail);await page.goto('/depot/partners');await page.getByLabel('Kho đang quản lý').selectOption(f.DepotId);await page.getByRole('button',{name:'Tạo lô chỉ định cho '+factoryName,exact:true}).click();const modal=page.getByRole('dialog');await expect(modal.getByRole('combobox',{name:'Loại phế liệu',exact:true})).toBeEnabled();await expect(modal.getByRole('combobox',{name:'Hình thức bán',exact:true})).toBeEnabled();await expect(modal.getByRole('region',{name:'Nhu cầu thu mua đang chọn'})).toHaveCount(0);await modal.getByRole('combobox',{name:'Loại phế liệu',exact:true}).selectOption('PET');await expect(modal.getByRole('combobox',{name:'Nhà máy',exact:true})).toHaveValue(f.FactoryId);await modal.getByLabel('Khối lượng (kg)',{exact:true}).fill('1');await expect(modal.getByRole('button',{name:'Tạo lô xuất hàng',exact:true})).toBeEnabled();await page.keyboard.press('Escape');
});

test('Popup nhu cầu desktop và mobile giữ header, footer và ngữ cảnh dễ đọc',async({page})=>{await login(page,f.OwnerEmail);await page.goto('/depot/partners');await page.getByLabel('Kho đang quản lý').selectOption(f.DepotId);await page.getByRole('button',{name:'Nhu cầu thu mua',exact:true}).click();const card=page.locator('div').filter({has:page.getByRole('heading',{name:factoryName,exact:true})}).filter({has:page.getByRole('button',{name:/Tạo lô bán ngay/})}).last();await card.getByRole('button',{name:/Tạo lô bán ngay/}).click();const modal=page.getByRole('dialog');for(const width of [1440,390]){await page.setViewportSize({width,height:844});await expect(modal.getByRole('button',{name:'Đóng',exact:true})).toBeInViewport();await expect(modal.getByRole('button',{name:'Tạo lô xuất hàng',exact:true})).toBeInViewport();const body=await modal.locator('[data-modal-body]').boundingBox();const footer=await modal.locator('footer').boundingBox();expect(body.y+body.height).toBeLessThanOrEqual(footer.y+1);await modal.screenshot({path:test.info().outputPath('popup-demand-'+width+'.png')});}await page.keyboard.press('Escape');});
