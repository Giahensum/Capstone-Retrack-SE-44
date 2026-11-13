import { MaterialIcon } from './DepotIcon';
import { notificationGroups, notificationTarget, noticeTime } from '../notificationLinks';

export default function NotificationCard({ notice, busy, onRead, onOpen, compact = false }) {
  const group = notificationGroups[notice.category] ?? notificationGroups.SYSTEM;
  const target = notificationTarget(notice);
  return <article className={`rounded-2xl border p-4 transition-colors ${notice.isRead ? 'border-d-border-subtle bg-white' : 'border-d-primary/25 bg-d-surface-accent'}`}>
    <div className="flex gap-3 items-start">
      <span className="shrink-0 rounded-xl bg-white border border-d-border-subtle p-2 text-d-primary"><MaterialIcon name={group.icon} /></span>
      <div className="min-w-0 flex-1 space-y-1.5">
        <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-d-on-surface-variant"><span>{group.label}</span>{!notice.isRead && <span className="rounded-full bg-d-primary text-white px-2 py-0.5 font-medium">Chưa đọc</span>}</div>
        <h2 className={`font-semibold [overflow-wrap:anywhere] ${compact ? 'text-sm' : 'text-base sm:text-lg'}`}>{notice.title}</h2>
        <p className={`text-d-on-surface-variant [overflow-wrap:anywhere] ${compact ? 'text-sm line-clamp-2' : 'whitespace-pre-wrap text-sm leading-6'}`}>{notice.message}</p>
        <time dateTime={notice.createdAt} className="block text-xs text-d-on-surface-variant">{noticeTime(notice.createdAt)}</time>
      </div>
    </div>
    <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
      {(!compact || !target) && <button type="button" disabled={busy} onClick={() => onRead(notice)} className="min-h-11 rounded-full border border-d-border-subtle bg-white px-4 py-2 text-sm font-medium hover:bg-d-surface disabled:opacity-40">{notice.isRead ? 'Đánh dấu chưa đọc' : 'Đánh dấu đã đọc'}</button>}
      {target && <button type="button" disabled={busy} onClick={() => onOpen(notice, target)} className="min-h-11 rounded-full bg-d-primary-container px-4 py-2 text-sm font-semibold text-d-on-primary-container hover:brightness-95 disabled:opacity-40">{target.label}</button>}
    </div>
  </article>;
}
