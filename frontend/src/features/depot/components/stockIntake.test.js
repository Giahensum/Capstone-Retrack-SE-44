import test from 'node:test';
import assert from 'node:assert/strict';
import { linesFromIntakeSource } from './stockIntake.js';

test('điền đúng vật liệu, tên và đơn vị, không coi lượng đăng là lượng đã thực nhập', () => {
 const source={items:[{agreementItemId:'a',kind:'MATERIAL',materialCode:'COPPER',name:'Đồng đã tháo dỡ',unit:'kg',postedQuantity:100,winningUnitPrice:3000}]};
 const [line]=linesFromIntakeSource(source);
 assert.equal(line.materialType,'COPPER');assert.equal(line.name,'Đồng đã tháo dỡ');assert.equal(line.unit,'kg');
 assert.equal(line.quantity,'');assert.equal(line.referencePrice,'');assert.match(line.sourceHint,/100/);
 assert.equal(source.items[0].postedQuantity,100);
});
test('đồ cũ quy đổi đơn vị cái và bộ, tách khỏi phế liệu kg', () => {
 const lines=linesFromIntakeSource({items:[{agreementItemId:'a',kind:'USED_GOODS',name:'Tivi',unit:'cai',postedQuantity:2},{agreementItemId:'b',kind:'USED_GOODS',name:'Bàn ghế',unit:'bo',postedQuantity:1}]});
 assert.equal(lines[0].kind,'REUSABLE');assert.equal(lines[0].unit,'cái');assert.equal(lines[1].unit,'bộ');assert.equal(lines[0].materialType,null);
});
test('hạng mục hỗn hợp chưa có mã không tự biến thành sắt', () => {
 const [line]=linesFromIntakeSource({items:[{agreementItemId:'x',kind:'MATERIAL',materialCode:null,name:'Phế liệu hỗn hợp',unit:'kg',postedQuantity:100}]});
 assert.equal(line.materialType,'');assert.equal(line.quantity,'');
});
