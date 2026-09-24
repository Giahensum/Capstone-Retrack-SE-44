import { createContext, useContext, useEffect, useRef, useState } from "react";
import { labels } from "../data/factoryState";
import {
  TOKEN_KEY,
  loadFactoryState,
  login as apiLogin,
  performFactoryAction,
  register as apiRegister,
} from "../data/factoryApi";

const USER_KEY = "retrack.user";
const Context = createContext(null);
export const useFactory = () => useContext(Context);

export function FactoryProvider({ children }) {
  const [state, setState] = useState(null);
  const current = useRef(null);
  const token = useRef(localStorage.getItem(TOKEN_KEY));
  const [user, setUser] = useState(() => {
    try { return JSON.parse(localStorage.getItem(USER_KEY)); } catch { return null; }
  });
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);

  async function refresh(accessToken = token.current) {
    if (!accessToken) return;
    const next = await loadFactoryState(accessToken);
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
    const accessToken = localStorage.getItem(TOKEN_KEY);
    token.current = accessToken;
    if (!accessToken) { setLoading(false); return () => { mounted = false; }; }
    loadFactoryState(accessToken).then((next) => {
      if (!mounted) return;
      current.current = next;
      setState(next);
    }).catch((error) => {
      if (!mounted) return;
      setNotice({ text: error.message, error: true });
      if (!localStorage.getItem(TOKEN_KEY)) { token.current = null; setUser(null); }
    }).finally(() => { if (mounted) setLoading(false); });
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    const unauthorized = () => {
      token.current = null;
      current.current = null;
      localStorage.removeItem(USER_KEY);
      setState(null);
      setUser(null);
    };
    window.addEventListener("retrack:unauthorized", unauthorized);
    return () => window.removeEventListener("retrack:unauthorized", unauthorized);
  }, []);

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), 7000);
    return () => clearTimeout(timer);
  }, [notice]);

  async function finishLogin(credentials) {
    setLoading(true);
    try {
      const session = await apiLogin(credentials.email, credentials.password);
      if (session.user?.role !== "FACTORY") throw new Error("Tài khoản này không có quyền Nhà máy.");
      token.current = session.accessToken;
      localStorage.setItem(TOKEN_KEY, session.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(session.user));
      setUser(session.user);
      await refresh(session.accessToken);
      setNotice({ text: "Đăng nhập thành công. Dữ liệu đã tải từ máy chủ." });
      return true;
    } catch (error) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      token.current = null;
      setNotice({ text: error.message, error: true });
      return false;
    } finally { setLoading(false); }
  }

  async function register(credentials) {
    setLoading(true);
    try {
      await apiRegister(credentials);
      setNotice({ text: "Tạo tài khoản xong. Đang đăng nhập…" });
      return await finishLogin(credentials);
    } catch (error) {
      setNotice({ text: error.message, error: true });
      return false;
    } finally { setLoading(false); }
  }

  function logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    token.current = null;
    current.current = null;
    setState(null);
    setUser(null);
    setNotice({ text: "Đã đăng xuất." });
  }

  async function act(type, payload, successMessage = "Đã lưu thay đổi trên máy chủ.") {
    if (!token.current) { setNotice({ text: "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.", error: true }); return false; }
    setBusy(true);
    try {
      const body = type === "TOGGLE_DEMAND"
        ? { ...payload, isActive: !current.current.demands.find((x) => x.id === payload.id)?.active }
        : payload;
      await performFactoryAction(type, body);
      await refresh();
      setNotice({ text: successMessage });
      return true;
    } catch (error) {
      if (type === "ACCEPT_BATCH") await refresh();
      setNotice({ text: error.message, error: true });
      return false;
    } finally { setBusy(false); }
  }

  return (
    <Context.Provider value={{ state, act, refresh, retry, login: finishLogin, register, logout, user, loading, busy, hasSession: Boolean(token.current), authenticated: Boolean(token.current && state), notify: (text, error = false) => setNotice({ text, error }) }}>
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
export function Field({ label, children, ...props }) {
  return <label className="field"><span>{label}</span>{children || <input {...props} />}</label>;
}
export function Modal({ title, children, onClose }) {
  const ref = useRef();
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    const overflow = document.body.style.overflow;
    dialog.showModal(); document.body.style.overflow = "hidden";
    return () => { dialog.close(); document.body.style.overflow = overflow; previous?.focus(); };
  }, []);
  return <dialog ref={ref} className="factory-dialog" aria-labelledby="factory-dialog-title" onCancel={onClose}>
    <div className="dialog-head"><h2 id="factory-dialog-title">{title}</h2><button onClick={onClose} aria-label="Đóng hộp thoại">×</button></div>
    <div className="dialog-body">{children}</div>
  </dialog>;
}
export async function readAttachment(file) {
  if (!file) return null;
  if (file.size > 700 * 1024) throw new Error("Mỗi tệp tối đa 700 KB.");
  if (!["application/pdf", "image/jpeg", "image/png"].includes(file.type)) throw new Error("Chỉ nhận tệp PDF, JPG hoặc PNG.");
  const data = await new Promise((resolve, reject) => {
    const reader = new FileReader(); reader.onload = () => resolve(reader.result); reader.onerror = () => reject(new Error("Không đọc được tệp.")); reader.readAsDataURL(file);
  });
  return { name: file.name, type: file.type, data };
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
    {value && <a href={value.data} download={value.name}>{value.name}</a>}
  </div>;
}
export function Confirm({ title, text, onClose, onConfirm, danger }) {
  return <Modal title={title} onClose={onClose}><p>{text}</p><div className="form-actions"><Button secondary onClick={onClose}>Hủy</Button><Button danger={danger} onClick={onConfirm}>Xác nhận</Button></div></Modal>;
}
