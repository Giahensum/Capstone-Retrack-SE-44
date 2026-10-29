// Logo thương hiệu dùng chung: biểu tượng vòng tái chế + chữ RETRACK (giống màn Seller).
// Ảnh /brand/logo_mark.png đã cắt sát, nền trong suốt.
export function BrandMark({ size = 40, className = '' }) {
  return (
    <img
      src="/brand/logo_mark.png"
      alt=""
      aria-hidden="true"
      width={size}
      height={size}
      className={`shrink-0 object-contain select-none ${className}`}
      style={{ width: size, height: size }}
      draggable={false}
    />
  );
}

export default function BrandLogo({ size = 40, showText = true, textClassName = 'text-d-primary', className = '', textSize }) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      <BrandMark size={size} />
      {showText && (
        <span
          className={`font-bold tracking-tighter ${textClassName}`}
          style={{ fontSize: textSize ?? Math.max(16, Math.round(size * 0.55)) }}
        >
          RETRACK
        </span>
      )}
    </span>
  );
}
