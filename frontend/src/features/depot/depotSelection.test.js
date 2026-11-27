import test from 'node:test';
import assert from 'node:assert/strict';
import { readDepotSelection, saveDepotSelection } from './depotSelection.js';

test('ghi nhớ kho riêng cho từng chủ kho', () => {
 const values=new Map();const storage={getItem:k=>values.get(k),setItem:(k,v)=>values.set(k,v)};
 saveDepotSelection('owner-a','depot-a',storage);saveDepotSelection('owner-b','depot-b',storage);
 assert.equal(readDepotSelection('owner-a',storage),'depot-a');assert.equal(readDepotSelection('owner-b',storage),'depot-b');
 assert.equal(readDepotSelection('owner-c',storage),'');
});
test('chưa đăng nhập không dùng lựa chọn chung giữa các tài khoản',()=>{
 const storage={getItem:()=>{throw Error('Không được đọc');},setItem:()=>{throw Error('Không được ghi');}};
 assert.equal(readDepotSelection(undefined,storage),'');assert.doesNotThrow(()=>saveDepotSelection(undefined,'x',storage));
});
test('storage bị chặn không làm ứng dụng lỗi',()=>{
 const storage={getItem:()=>{throw Error('Bị chặn');},setItem:()=>{throw Error('Hết dung lượng');}};
 assert.equal(readDepotSelection('a',storage),'');assert.doesNotThrow(()=>saveDepotSelection('a','b',storage));
});
