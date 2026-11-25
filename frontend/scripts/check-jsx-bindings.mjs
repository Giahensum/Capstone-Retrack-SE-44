import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

// Build JSX không bắt tên component chưa import. Chặn lỗi này mà chưa ép dự án JS chuyển sang strict TypeScript.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const walk = dir => fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => entry.isDirectory()
  ? walk(path.join(dir, entry.name)) : entry.name.endsWith('.jsx') ? [path.join(dir, entry.name)] : []);
const files = walk(path.join(root, 'src'));
const names = new Set(files.map(file => path.normalize(file).toLowerCase()));
const program = ts.createProgram(files, {
  allowJs: true, checkJs: true, noEmit: true, skipLibCheck: true, jsx: ts.JsxEmit.ReactJSX,
  module: ts.ModuleKind.ESNext, moduleResolution: ts.ModuleResolutionKind.Bundler,
  target: ts.ScriptTarget.ES2022, paths: { '@/*': [path.join(root, 'src/*')] },
});
const errors = ts.getPreEmitDiagnostics(program).filter(diagnostic => {
  if (![2304, 2552].includes(diagnostic.code) || !diagnostic.file || !names.has(path.normalize(diagnostic.file.fileName).toLowerCase())) return false;
  // Chỉ xét identifier trong thẻ JSX mở/đóng; các lỗi kiểu dữ liệu khác thuộc đợt kiểm tra riêng.
  return /<\/?$/.test(diagnostic.file.text.slice(Math.max(0, diagnostic.start - 2), diagnostic.start));
});
for (const error of errors) {
  const line = error.file.getLineAndCharacterOfPosition(error.start).line + 1;
  console.error(`${path.relative(root, error.file.fileName)}:${line}: ${ts.flattenDiagnosticMessageText(error.messageText, ' ')}`);
}
console.log(`JSX: đã kiểm tra ${files.length} file, ${errors.length} lỗi component chưa khai báo/import.`);
process.exitCode = errors.length ? 1 : 0;
