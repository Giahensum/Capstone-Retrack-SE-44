import { useState } from "react";
import { useFactory } from "./FactoryUI";

export default function FactoryAuth() {
  const { login, register, loading, hasSession, retry } = useFactory();
  const [mode, setMode] = useState("login");
  const [form, setForm] = useState({ fullName: "", email: "", phone: "", password: "" });
  const set = (key, value) => setForm((current) => ({ ...current, [key]: value }));
  const submit = async (event) => {
    event.preventDefault();
    if (mode === "register") await register(form);
    else await login(form);
  };
  return <main className="factory-auth-page">
    <section className="factory-auth-card">
      <div className="factory-auth-brand"><span className="brand-mark">R</span><div>retrack<small>FACTORY WORKSPACE</small></div></div>
      {hasSession ? <>
        <h1>Không tải được dữ liệu nhà máy</h1>
        <p>Kiểm tra backend tại <code>localhost:5211</code>, sau đó thử tải lại bằng phiên hiện tại.</p>
        <button className="btn" disabled={loading} onClick={retry}>{loading ? "Đang tải…" : "Thử kết nối lại"}</button>
      </> : <>
        <h1>{mode === "login" ? "Đăng nhập nhà máy" : "Tạo tài khoản nhà máy"}</h1>
        <p>Dữ liệu nghiệp vụ sẽ được tải và lưu trên máy chủ Retrack.</p>
        <div className="factory-auth-tabs">
          <button className={mode === "login" ? "active" : ""} onClick={() => setMode("login")}>Đăng nhập</button>
          <button className={mode === "register" ? "active" : ""} onClick={() => setMode("register")}>Đăng ký</button>
        </div>
        <form onSubmit={submit}>
          {mode === "register" && <>
            <label className="field"><span>Tên người đại diện / nhà máy</span><input required maxLength={200} autoComplete="name" value={form.fullName} onChange={(e) => set("fullName", e.target.value)} /></label>
            <label className="field"><span>Số điện thoại</span><input type="tel" autoComplete="tel" value={form.phone} onChange={(e) => set("phone", e.target.value)} /></label>
          </>}
          <label className="field"><span>Email</span><input required type="email" autoComplete="email" value={form.email} onChange={(e) => set("email", e.target.value)} /></label>
          <label className="field"><span>Mật khẩu</span><input required minLength={mode === "register" ? 8 : undefined} type="password" autoComplete={mode === "login" ? "current-password" : "new-password"} value={form.password} onChange={(e) => set("password", e.target.value)} /></label>
          <button className="btn" type="submit" disabled={loading}>{loading ? "Đang xử lý…" : mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}</button>
        </form>
      </>}
    </section>
  </main>;
}
