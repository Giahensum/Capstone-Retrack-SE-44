import { test, expect } from '@playwright/test';
import fs from 'node:fs';
test('Owner tạo khu vực/ca, phân công ngoại lệ, nhận thông báo đúng đích và ngừng ca', async ({ page, request }) => {
  test.skip(!process.env.E2E_FINISH_FIXTURE || !process.env.E2E_FINISH_PASSWORD, 'Cần fixture test riêng.');
  test.setTimeout(120000);
  const f=JSON.parse(fs.readFileSync(process.env.E2E_FINISH_FIXTURE,'utf8'));
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('/login');await page.getByLabel('Email',{exact:true}).fill(f.OwnerEmail);await page.getByLabel('Mật khẩu',{exact:true}).fill(process.env.E2E_FINISH_PASSWORD);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await expect(page).toHaveURL(/\/depot/);
  const login=await request.post('/api/auth/login',{data:{email:f.OwnerEmail,password:process.env.E2E_FINISH_PASSWORD}});const token=(await login.json()).data.token;
  await page.goto('/depot/dispatch');await expect(page.getByRole('article')).toHaveCount(20);
  await page.getByRole('button',{name:'Sau',exact:true}).click();await expect(page.getByRole('article')).toHaveCount(3);await page.getByRole('button',{name:'Trước',exact:true}).click();
  await page.getByRole('button',{name:'＋ Thêm khu vực',exact:true}).click();const area=page.getByRole('dialog',{name:'Thêm khu vực thu gom'});
  await area.getByLabel('Tên khu vực').fill('Vùng Ngũ Hành Sơn fixture');await area.getByLabel('Vĩ độ tâm vùng').fill('16.02');await area.getByLabel('Kinh độ tâm vùng').fill('108.25');await area.getByRole('button',{name:'Lưu khu vực'}).click();await expect(area).toHaveCount(0);
  // Worker chỉ xét kho đã có khu vực. Đợi producer thật trước khi mở Inbox polling 30 giây.
  await expect.poll(async()=>((await (await request.get(`/api/depot/notifications?depotId=${f.DepotId}&category=PICKUP`,{headers:{Authorization:`Bearer ${token}`}})).json()).data.totalCount),{timeout:25000}).toBeGreaterThan(0);
  await page.getByRole('button',{name:'＋ Bố trí ca',exact:true}).click();const shift=page.getByRole('dialog',{name:'Bố trí ca thu gom'});
  await shift.getByRole('combobox',{name:'Khu vực thu gom',exact:true}).selectOption({label:'Vùng Ngũ Hành Sơn fixture'});await shift.getByRole('combobox',{name:'Nhân viên thu gom',exact:true}).selectOption(f.EmployeeId);
  await shift.getByRole('button',{name:'Lưu ca thu gom'}).click();await expect(shift).toHaveCount(0);
  await page.getByRole('tab',{name:'Ca thu gom',exact:true}).click();await expect(page.getByRole('row').filter({hasText:'DEPOT_EMPLOYEE hoàn thiện fixture'})).toContainText('Sẵn sàng');
  await page.getByRole('tab',{name:'Đơn thu gom',exact:true}).click();const row=page.getByRole('article').filter({hasText:'Đơn điều phối 00'});await row.getByRole('button',{name:'Chi tiết / phân công'}).click();
  const dialog=page.getByRole('dialog',{name:'Chi tiết và phân công thu gom'});const id=new URL(page.url()).searchParams.get('pickupId');
  await dialog.getByLabel('Ca nhận đơn').selectOption({index:1});await dialog.getByLabel('Lý do phân công / chuyển người').fill('Chủ kho đã liên hệ nhân viên để xử lý ngoại lệ');await dialog.getByRole('button',{name:'Xác nhận phân công',exact:true}).click();await expect(dialog).toContainText('Đã lưu phân công và thông báo');
  await page.reload();await expect(dialog).toContainText('DEPOT_EMPLOYEE hoàn thiện fixture');await expect(dialog.getByRole('article')).toContainText('Chủ kho đã liên hệ');
  await dialog.getByLabel('Đóng',{exact:true}).click();await page.getByLabel('Trạng thái đơn').selectOption('SCHEDULED');await expect(page.getByRole('article')).toHaveCount(1);
  // Thông báo thực tế từ worker mới có nhóm PICKUP; phải mở màn điều phối, không mở thanh toán PENDING.
  await page.goto('/depot/notifications');await page.getByLabel('Nhóm thông báo').selectOption('PICKUP');const notice=page.getByRole('article').filter({hasText:'Đơn đang chờ điều phối'}).first();await expect(notice).toBeVisible({timeout:20000});await notice.getByRole('button',{name:'Xem đơn điều phối'}).click();await expect(dialog).toBeVisible();await dialog.getByLabel('Đóng',{exact:true}).click();
  for(const width of [390,768,1440]){
    await page.setViewportSize({width,height:900});await page.goto(`/depot/dispatch?pickupId=${id}`);await expect(dialog).toBeVisible();const bounds=await dialog.boundingBox();expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(width);await page.screenshot({path:test.info().outputPath(`dieu-phoi-${width}.png`),fullPage:true});await page.keyboard.press('Escape');await expect(dialog).toHaveCount(0);
  }
  await page.goto('/depot/dispatch?tab=shifts');await page.getByRole('button',{name:'Ngừng ca',exact:true}).click();const stop=page.getByRole('dialog',{name:'Ngừng ca thu gom'});await stop.getByLabel('Lý do ngừng ca').fill('Nhân viên xin nghỉ và chủ kho đã xác nhận');await stop.getByRole('button',{name:'Xác nhận ngừng ca'}).click();await expect(stop).toHaveCount(0);await expect(page.getByRole('row').filter({hasText:'DEPOT_EMPLOYEE hoàn thiện fixture'})).toContainText('Đã ngừng ca');
  const response=()=>request.get(`/api/depot/${f.DepotId}/employee-dispatch/pickups/${id}`,{headers:{Authorization:`Bearer ${token}`}});
  await expect.poll(async()=>((await (await response()).json()).data.status),{timeout:25000}).toBe('PENDING');
  expect(errors).toEqual([]);
});
