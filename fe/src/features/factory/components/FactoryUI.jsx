import { createContext, useContext, useEffect, useRef, useState } from "react";
import {
  createInitialState,
  STORAGE_KEY,
  transition,
  labels,
} from "../data/factoryState";

const Context = createContext();
export const useFactory = () => useContext(Context);
function load() {
  try {
    const data = JSON.parse(localStorage.getItem(STORAGE_KEY));
    if (
      data?.version === 2 &&
      Array.isArray(data.orders) &&
      Array.isArray(data.depots) &&
      Array.isArray(data.batches) &&
      Array.isArray(data.demands) &&
      data.profile &&
      data.prices
    )
      return { data };
  } catch {
    return {
      data: createInitialState(),
      warning: "Không đọc được dữ liệu đã lưu. Đang hiển thị dữ liệu mẫu.",
    };
  }
  return { data: createInitialState() };
}
export function FactoryProvider({ children }) {
  const [initial] = useState(load);
  const [state, setState] = useState(initial.data);
  const current = useRef(state);
  const [notice, setNotice] = useState(
    initial.warning ? { text: initial.warning, error: true } : null,
  );
  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), 7000);
    return () => clearTimeout(timer);
  }, [notice]);
  useEffect(() => {
    const sync = (event) => {
      if (event.key === STORAGE_KEY) {
        const loaded = load();
        current.current = loaded.data;
        setState(loaded.data);
      }
    };
    window.addEventListener("storage", sync);
    return () => window.removeEventListener("storage", sync);
  }, []);
  function act(type, payload, text = "Đã lưu thay đổi.") {
    try {
      const next = transition(current.current, { type, payload });
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
      } catch {
        throw new Error(
          "Không lưu được vào trình duyệt. Bộ nhớ có thể đã đầy; hãy chọn tệp nhỏ hơn hoặc cho phép lưu dữ liệu.",
        );
      }
      current.current = next;
      setState(next);
      setNotice({ text });
      return true;
    } catch (error) {
      setNotice({ text: error.message, error: true });
      return false;
    }
  }
  return (
    <Context.Provider
      value={{
        state,
        act,
        notify: (text, error = false) => setNotice({ text, error }),
      }}
    >
      {children}
      {notice && (
        <div
          className={`factory-toast ${notice.error ? "error" : ""}`}
          role={notice.error ? "alert" : "status"}
        >
          {notice.text}
          <button onClick={() => setNotice(null)} aria-label="Đóng thông báo">
            ×
          </button>
        </div>
      )}
    </Context.Provider>
  );
}
export function Status({ value }) {
  return (
    <span className={`status status-${value}`}>{labels[value] || value}</span>
  );
}
export function Button({ children, secondary, danger, ...props }) {
  return (
    <button
      className={`btn ${secondary ? "secondary" : ""} ${danger ? "danger" : ""}`}
      type="button"
      {...props}
    >
      {children}
    </button>
  );
}
export function PageHead({ eyebrow, title, text, children }) {
  return (
    <div className="page-head">
      <div>
        <span className="eyebrow">{eyebrow || "VẬN HÀNH NHÀ MÁY"}</span>
        <h1>{title}</h1>
        <p>{text}</p>
      </div>
      <div className="actions">{children}</div>
    </div>
  );
}
export function Card({ title, children, action, className = "" }) {
  return (
    <section className={`panel ${className}`}>
      {title && (
        <div className="panel-heading">
          <h2>{title}</h2>
          {action}
        </div>
      )}
      <div className="panel-body">{children}</div>
    </section>
  );
}
export function Empty({ text = "Chưa có dữ liệu phù hợp." }) {
  return (
    <div className="empty">
      <span>◇</span>
      <h3>{text}</h3>
      <p>Dữ liệu sẽ xuất hiện khi có giao dịch hoặc bạn thay đổi bộ lọc.</p>
    </div>
  );
}
export function Field({ label, children, ...props }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children || <input {...props} />}
    </label>
  );
}
export function Modal({ title, children, onClose }) {
  const ref = useRef();
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    const overflow = document.body.style.overflow;
    dialog.showModal();
    document.body.style.overflow = "hidden";
    return () => {
      dialog.close();
      document.body.style.overflow = overflow;
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className="factory-dialog"
      aria-labelledby="factory-dialog-title"
      onCancel={onClose}
    >
      <div className="dialog-head">
        <h2 id="factory-dialog-title">{title}</h2>
        <button onClick={onClose} aria-label="Đóng hộp thoại">
          ×
        </button>
      </div>
      <div className="dialog-body">{children}</div>
    </dialog>
  );
}
export async function readAttachment(file) {
  if (!file) return null;
  if (file.size > 700 * 1024)
    throw new Error("Bản demo hỗ trợ mỗi tệp tối đa 700 KB.");
  if (!["application/pdf", "image/jpeg", "image/png"].includes(file.type))
    throw new Error("Chỉ nhận tệp PDF, JPG hoặc PNG.");
  const data = await new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(new Error("Không đọc được tệp."));
    reader.readAsDataURL(file);
  });
  return { name: file.name, type: file.type, data };
}
export function Attachment({ label, value, onChange }) {
  const { notify } = useFactory();
  const [busy, setBusy] = useState(false);
  return (
    <div className="field">
      <span>{label}</span>
      <input
        aria-label={label}
        type="file"
        disabled={busy}
        accept=".pdf,.jpg,.jpeg,.png"
        onChange={async (e) => {
          const input = e.target;
          setBusy(true);
          try {
            const file = await readAttachment(input.files?.[0]);
            if (file) onChange(file);
          } catch (error) {
            notify(error.message, true);
          } finally {
            input.value = "";
            setBusy(false);
          }
        }}
      />
      <small>
        {busy
          ? "Đang đọc tệp…"
          : "PDF, JPG, PNG · Tối đa 700 KB/tệp trong bản demo"}
      </small>
      {value && (
        <a href={value.data} download={value.name}>
          ↓ {value.name}
        </a>
      )}
    </div>
  );
}
export function Confirm({ title, text, onClose, onConfirm, danger }) {
  return (
    <Modal title={title} onClose={onClose}>
      <p>{text}</p>
      <div className="form-actions">
        <Button secondary onClick={onClose}>
          Hủy
        </Button>
        <Button danger={danger} onClick={onConfirm}>
          Xác nhận
        </Button>
      </div>
    </Modal>
  );
}
