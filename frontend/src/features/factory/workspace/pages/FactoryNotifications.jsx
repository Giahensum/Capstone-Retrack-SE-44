import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Bell } from "lucide-react";
import { Button, Card, PageHead, Pagination, useFactory } from "../components/FactoryUI";
import { request } from "../data/factoryApi";

const time = (value) => new Date(value).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
const targetFor = (notice) => notice.targetType === "PARTNERS" && notice.depotId
  ? { tab: "partners", label: "Xem vựa đối tác" }
  : notice.targetType === "FEES" ? { tab: "platform-fees", label: "Xem phí nền tảng" }
    : notice.targetType === "MARKETPLACE" ? { tab: "marketplace", label: "Xem yêu cầu chỉ định", id: notice.batchId }
    : notice.targetType === "NONE" ? null
    : notice.batchId ? { tab: "orders", label: "Xem lô hàng", id: notice.batchId } : null;

function useNotices(page, pageSize, unreadOnly = false, enabled = true) {
  const { user } = useFactory();
  const accountKey = user?.id ?? user?.email;
  return useQuery({
    queryKey: ["factory", "notifications", accountKey, page, pageSize, unreadOnly],
    queryFn: () => request(`/api/factory/notifications?page=${page}&pageSize=${pageSize}&unreadOnly=${unreadOnly}`),
    enabled: enabled && Boolean(accountKey),
    refetchInterval: enabled && accountKey ? 30000 : false,
  });
}

function useNoticeActions() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, isRead }) => request(id ? `/api/factory/notifications/${id}/${isRead ? "read" : "unread"}` : "/api/factory/notifications/read-all", { method: "PATCH" }),
    onSuccess: () => client.invalidateQueries({ queryKey: ["factory", "notifications"] }),
  });
}

function Notice({ notice, onOpen, onRead, busy }) {
  const target = targetFor(notice);
  return <article className={`factory-notice ${notice.isRead ? "" : "unread"}`}>
    <div className="factory-notice-head"><strong>{notice.title}</strong>{!notice.isRead && <span>Chưa đọc</span>}</div>
    <p>{notice.message}</p>
    <small>{time(notice.createdAt)}</small>
    <div className="factory-notice-actions">
      <button type="button" disabled={busy} onClick={() => onRead(notice)}>{notice.isRead ? "Đánh dấu chưa đọc" : "Đánh dấu đã đọc"}</button>
      {target && <button type="button" disabled={busy} onClick={() => onOpen(notice)}>{target.label}</button>}
    </div>
  </article>;
}

export function FactoryNotificationBell({ navigate }) {
  const [open, setOpen] = useState(false);
  const box = useRef(null), trigger = useRef(null), panel = useRef(null);
  const { user, notify } = useFactory();
  const accountKey = user?.id ?? user?.email;
  const count = useQuery({ queryKey: ["factory", "notifications", accountKey, "count"], queryFn: () => request("/api/factory/notifications/unread-count"), enabled: Boolean(accountKey), refetchInterval: accountKey ? 30000 : false });
  const notices = useNotices(1, 5, false, open);
  const action = useNoticeActions();
  useEffect(() => {
    if (!open) return;
    panel.current?.focus();
    const outside = (event) => { if (!box.current?.contains(event.target)) setOpen(false); };
    const escape = (event) => { if (event.key === "Escape") { setOpen(false); trigger.current?.focus(); } };
    document.addEventListener("pointerdown", outside);
    document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("pointerdown", outside); document.removeEventListener("keydown", escape); };
  }, [open]);
  const mark = async (notice, isRead) => {
    try { await action.mutateAsync({ id: notice.id, isRead }); }
    catch (error) { notify(error.message, true); }
  };
  const show = async (notice) => {
    const target = targetFor(notice);
    if (!target) return;
    if (!notice.isRead) {
      try { await action.mutateAsync({ id: notice.id, isRead: true }); }
      catch (error) { notify(error.message, true); return; }
    }
    setOpen(false); navigate(target.tab, target.id);
  };
  return <div className="factory-notification-box" ref={box}>
    <button ref={trigger} type="button" className="factory-bell" aria-label={count.isError ? "Thông báo: chưa tải được số chưa đọc" : `Mở thông báo, ${count.data ?? 0} chưa đọc`}
      aria-expanded={open} aria-controls="factory-notification-panel" onClick={() => setOpen(!open)}>
      <Bell size={20} aria-hidden="true" />
      {count.isSuccess && count.data > 0 && <span className="factory-bell-count" aria-hidden="true">{count.data > 99 ? "99+" : count.data}</span>}
      {count.isError && <span className="factory-bell-error" aria-hidden="true">!</span>}
    </button>
    {open && <section ref={panel} id="factory-notification-panel" role="dialog" aria-label="Thông báo nhanh" tabIndex={-1} className="factory-notification-panel">
      <div className="factory-notification-heading"><div><strong>Thông báo</strong><small>5 thông báo mới nhất</small></div><button type="button" aria-label="Đóng thông báo" onClick={() => { setOpen(false); trigger.current?.focus(); }}>×</button></div>
      <div className="factory-notification-list">
        {notices.isPending && <p role="status">Đang tải thông báo…</p>}
        {notices.isError && <p role="alert">{notices.error.message} <button type="button" onClick={() => notices.refetch()}>Thử lại</button></p>}
        {notices.data?.items.map((notice) => <Notice key={notice.id} notice={notice} busy={action.isPending} onOpen={show} onRead={(item) => void mark(item, !item.isRead)} />)}
        {notices.data?.totalCount === 0 && <p role="status">Chưa có thông báo.</p>}
      </div>
      <button type="button" className="factory-notification-all" onClick={() => { setOpen(false); navigate("notifications"); }}>Xem tất cả thông báo</button>
    </section>}
  </div>;
}

export default function FactoryNotifications({ navigate }) {
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const notices = useNotices(page, 20, unreadOnly);
  const action = useNoticeActions();
  const { notify } = useFactory();
  const run = async (payload) => {
    try { await action.mutateAsync(payload); return true; }
    catch (error) { notify(error.message, true); return false; }
  };
  return <>
    <PageHead title="Thông báo" text="Theo dõi vận chuyển, lô hàng và xác nhận tiền từ vựa." />
    <Card>
      <div className="factory-notification-tools">
        <label><input type="checkbox" checked={unreadOnly} onChange={(event) => { setUnreadOnly(event.target.checked); setPage(1); }} /> Chỉ xem chưa đọc</label>
        <Button secondary disabled={action.isPending || !notices.data?.totalCount} onClick={() => void run({})}>Đánh dấu tất cả đã đọc</Button>
      </div>
      {notices.isPending && <p role="status">Đang tải thông báo…</p>}
      {notices.isError && <p role="alert">{notices.error.message} <Button secondary onClick={() => notices.refetch()}>Thử lại</Button></p>}
      {notices.data?.items.map((notice) => <Notice key={notice.id} notice={notice} busy={action.isPending}
        onOpen={async (item) => { const target = targetFor(item); if (!target || (!item.isRead && !await run({ id: item.id, isRead: true }))) return; navigate(target.tab, target.id); }}
        onRead={(item) => void run({ id: item.id, isRead: !item.isRead })} />)}
      {notices.data?.totalCount === 0 && <p role="status">Chưa có thông báo phù hợp.</p>}
      {notices.data && <Pagination page={page} totalCount={notices.data.totalCount} totalPages={notices.data.totalPages} onChange={setPage} />}
    </Card>
  </>;
}
