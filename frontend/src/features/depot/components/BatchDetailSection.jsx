import { ChevronDown } from 'lucide-react';

// Phần thông tin tùy chọn; summary hỗ trợ mở bằng chuột và bàn phím.
export default function BatchDetailSection({ title, children }) {
  return <details className="rounded-2xl border border-d-border-subtle bg-white [&[open]>summary>svg]:rotate-180">
    <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-2xl p-4 font-semibold text-d-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-d-primary [&::-webkit-details-marker]:hidden">
      {title}<ChevronDown size={20} aria-hidden="true" className="shrink-0 transition-transform" />
    </summary>
    <div className="border-t border-d-border-subtle p-4 space-y-4">{children}</div>
  </details>;
}
