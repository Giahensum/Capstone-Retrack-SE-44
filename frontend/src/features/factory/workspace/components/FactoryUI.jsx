import { createContext, useContext, useEffect, useRef, useState } from "react";
import { useAuthStore } from "@/app/store/useAuthStore";
import { useQueryClient } from "@tanstack/react-query";
import { labels } from "../data/factoryState";
import { loadFactoryState, performFactoryAction } from "../data/factoryApi";
import { downloadFactoryAttachment } from "../data/factoryApi";

const Context = createContext(null);
export const useFactory = () => useContext(Context);

export function FactoryProvider({ children }) {
  const [state, setState] = useState(null);
  const current = useRef(null);
  const queryClient = useQueryClient();
  const token = useAuthStore((auth) => auth.token);
  const user = useAuthStore((auth) => auth.user);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);

  useEffect(() => {
    // Dọn khóa phiên Factory đời cũ; phiên hiện hành chỉ nằm trong auth store chung.
    localStorage.removeItem("retrack.accessToken");
    localStorage.removeItem("retrack.user");
  }, []);

  async function refresh() {
    if (!useAuthStore.getState().token) return;
    const next = await loadFactoryState();
    current.current = next;
    setState(next);
  }

  async function retry() {
    setLoading(true);
    try { await refresh(); }
    catch (error) { setNotice({ text: error.message, error: true }); }
    finally { setLoading(false); }
  }

  useEffect(() => {
    let mounted = true;
    if (!token) { setState(null); setLoading(false); return () => { mounted = false; }; }
    setLoading(true);
    loadFactoryState().then((next) => {
      if (!mounted) return;
      current.current = next;
      setState(next);
    }).catch((error) => {
      if (!mounted) return;
      setNotice({ text: error.message, error: true });
    }).finally(() => { if (mounted) setLoading(false); });
    return () => { mounted = false; };
  }, [token]);

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), 7000);
    return () => clearTimeout(timer);
  }, [notice]);

  function logout() {
    useAuthStore.getState().logout();
    current.current = null;
    setState(null);
  }

  async function act(type, payload, successMessage = "Đã lưu thay đổi trên máy chủ.") {
    if (!useAuthStore.getState().token) { setNotice({ text: "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.", error: true }); return false; }
    setBusy(true);
    try {
      await performFactoryAction(type, payload);
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ["factory"] });
      setNotice({ text: successMessage });
      return true;
    } catch (error) {
      if (type === "ACCEPT_BATCH") await refresh();
      setNotice({ text: error.message, error: true });
      return false;
    } finally { setBusy(false); }
  }

  return (
    <Context.Provider value={{ state, act, refresh, retry, logout, user, loading, busy, hasSession: Boolean(token), authenticated: Boolean(token && state), notify: (text, error = false) => setNotice({ text, error }) }}>
      {children}
      {notice && <div className={`factory-toast ${notice.error ? "error" : ""}`} role={notice.error ? "alert" : "status"}>
        {notice.text}<button onClick={() => setNotice(null)} aria-label="Đóng thông báo">×</button>
      </div>}
    </Context.Provider>
  );
}

export function Status({ value }) {
  return <span className={`status status-${value}`}>{labels[value] || value}</span>;
}
export function Button({ children, secondary, danger, ...props }) {
  return <button className={`btn ${secondary ? "secondary" : ""} ${danger ? "danger" : ""}`} type="button" {...props}>{children}</button>;
}
export function PageHead({ eyebrow, title, text, children }) {
  return <div className="page-head"><div><span className="eyebrow">{eyebrow || "VẬN HÀNH NHÀ MÁY"}</span><h1>{title}</h1><p>{text}</p></div><div className="actions">{children}</div></div>;
}
export function Card({ title, children, action, className = "" }) {
  return <section className={`panel ${className}`}>{title && <div className="panel-heading"><h2>{title}</h2>{action}</div>}<div className="panel-body">{children}</div></section>;
}
export function Empty({ text = "Chưa có dữ liệu phù hợp." }) {
  return <div className="empty"><span>◇</span><h3>{text}</h3><p>Dữ liệu sẽ xuất hiện khi có giao dịch hoặc bạn thay đổi bộ lọc.</p></div>;
}
export function Pagination({ page, totalPages = 0, totalCount = 0, onChange }) {
  if (totalPages <= 1) return null;
  return <nav className="factory-pagination" aria-label="Phân trang">
    <Button secondary disabled={page <= 1} onClick={() => onChange(page - 1)}>← Trước</Button>
    <span>Trang {page}/{totalPages} · {totalCount} kết quả</span>
    <Button secondary disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Sau →</Button>
  </nav>;
}
export function Field({ label, children, ...props }) {
  return <label className="field"><span>{label}</span>{children || <input {...props} />}</label>;
}
export function Modal({ title, children, onClose, busy = false }) {
  const ref = useRef();
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    const overflow = document.body.style.overflow;
    dialog.showModal(); document.body.style.overflow = "hidden";
    return () => { dialog.close(); document.body.style.overflow = overflow; previous?.focus(); };
  }, []);
  return <dialog ref={ref} className="factory-dialog" aria-labelledby="factory-dialog-title" onCancel={(e) => { if (busy) e.preventDefault(); else onClose(); }}>
    <div className="dialog-head"><h2 id="factory-dialog-title">{title}</h2><button disabled={busy} onClick={onClose} aria-label="Đóng hộp thoại">×</button></div>
    <div className="dialog-body">{children}</div>
  </dialog>;
}
export async function readAttachment(file) {
  if (!file) return null;
  if (file.size > 700 * 1024) throw new Error("Mỗi tệp tối đa 700 KB.");
  if (!["application/pdf", "image/jpeg", "image/png"].includes(file.type)) throw new Error("Chỉ nhận tệp PDF, JPG hoặc PNG.");
  return { name: file.name, type: file.type, file };
}
export function Attachment({ label, value, onChange }) {
  const { notify } = useFactory();
  const [busy, setBusy] = useState(false);
  return <div className="field"><span>{label}</span><input aria-label={label} type="file" disabled={busy} accept=".pdf,.jpg,.jpeg,.png" onChange={async (e) => {
    const input = e.target; setBusy(true);
    try { const file = await readAttachment(input.files?.[0]); if (file) onChange(file); }
    catch (error) { notify(error.message, true); }
    finally { input.value = ""; setBusy(false); }
  }} />
    <small>{busy ? "Đang đọc tệp…" : "PDF, JPG, PNG · Tối đa 700 KB/tệp"}</small>
    {value && (value.file ? <span>{value.name}</span> : <button type="button" onClick={() => void downloadFactoryAttachment(value.data, value.name).catch((error) => notify(error.message, true))}>{value.name}</button>)}
  </div>;
}
export function Confirm({ title, text, onClose, onConfirm, danger }) {
  const [busy, setBusy] = useState(false);
  return <Modal title={title} onClose={onClose} busy={busy}><p>{text}</p><div className="form-actions"><Button secondary disabled={busy} onClick={onClose}>Hủy</Button><Button danger={danger} disabled={busy} onClick={async () => { if (busy) return; setBusy(true); try { await onConfirm(); } finally { setBusy(false); } }}>{busy ? 'Đang lưu…' : 'Xác nhận'}</Button></div></Modal>;
}
