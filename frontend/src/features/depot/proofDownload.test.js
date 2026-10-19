import test from 'node:test';
import assert from 'node:assert/strict';
import { proofExtension, factoryProofPath } from './proofDownload.js';

test('chỉ gắn JWT cho endpoint chứng từ nội bộ đúng định dạng', () => {
  const route = `/api/factory/attachments/${'a'.repeat(32)}/${'b'.repeat(32)}`;
  assert.equal(factoryProofPath(route), route.slice(4));
  for (const value of [`https://evil.invalid${route}`, `${route}?token=x`, `${route}/../x`, '//evil.invalid/api', undefined])
    assert.equal(factoryProofPath(value), null);
});

test('nhận đúng chứng từ PDF/PNG/JPEG có chữ ký tương ứng', () => {
  for (const [mime, bytes, extension] of [
    ['application/pdf', '%PDF-1.7', 'pdf'],
    ['image/png', '\x89PNG\r\n\x1a\n', 'png'],
    ['image/jpeg', '\xff\xd8\xff', 'jpg'],
  ]) assert.equal(proofExtension(`data:${mime};base64,${btoa(bytes)}`), extension);
});

test('không cung cấp link tải cho HTML/SVG, sai chữ ký, base64 lỗi và tệp quá lớn', () => {
  for (const url of [undefined, 'javascript:alert(1)', 'data:text/html;base64,PGgxPg==',
    'data:image/svg+xml;base64,PHN2Zz4=', 'data:image/png;base64,PGgxPg==',
    'data:image/png;base64,=', `data:application/pdf;base64,${btoa('%PDF-' + 'x'.repeat(700 * 1024))}`]) {
    assert.equal(proofExtension(url), null);
  }
});
