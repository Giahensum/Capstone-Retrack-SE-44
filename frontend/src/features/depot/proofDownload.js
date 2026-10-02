// Tương thích chứng từ inline của Factory; không nhận HTML/SVG hay URL thực thi.
export function proofExtension(url) {
  if (typeof url !== 'string' || url.length > 960_000) return null;
  const match = /^data:(application\/pdf|image\/png|image\/jpeg);base64,([A-Za-z0-9+/]+={0,2})$/.exec(url);
  if (!match) return null;
  try {
    const bytes = atob(match[2]);
    if (!bytes.length || bytes.length > 700 * 1024) return null;
    const signatures = {
      'application/pdf': ['%PDF-', 'pdf'],
      'image/png': ['\x89PNG\r\n\x1a\n', 'png'],
      'image/jpeg': ['\xff\xd8\xff', 'jpg'],
    };
    const [signature, extension] = signatures[match[1]];
    return bytes.startsWith(signature) ? extension : null;
  } catch { return null; }
}
