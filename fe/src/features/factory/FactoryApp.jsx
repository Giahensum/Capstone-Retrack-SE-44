import { useState } from "react";
import { FactoryProvider, useFactory } from "./components/FactoryUI";
import {
  Dashboard,
  Marketplace,
  Demands,
  Partners,
  Profile,
  Prices,
} from "./pages/FactoryViews";
import { Operations } from "./pages/FactoryOperations";
import "./factory.css";

const tabs = [
  ["dashboard", "Tổng quan", "◫"],
  ["marketplace", "Sàn nguyên liệu", "▦"],
  ["demands", "Nhu cầu thu mua", "▤"],
  ["orders", "Đơn hàng & Vận chuyển", "⇄"],
  ["qc", "Trạm cân & KCS", "⚖"],
  ["settlements", "Quyết toán", "▣"],
  ["partners", "Vựa đối tác", "♧"],
  ["prices", "Giá tham khảo", "↗"],
  ["profile", "Hồ sơ nhà máy", "⚙"],
];
function Workspace() {
  const { state } = useFactory();
  const [tab, setTab] = useState("dashboard");
  const [focusId, setFocusId] = useState(null);
  function navigate(next, orderId = null) {
    setFocusId(orderId);
    setTab(next);
    window.scrollTo({ top: 0 });
  }
  const pending = state.orders.filter((o) =>
    ["DELIVERED", "RECEIVED", "WEIGHED"].includes(o.status),
  ).length;
  return (
    <div className="factory-shell">
      <aside className="factory-sidebar">
        <a
          className="brand"
          href="#"
          onClick={(e) => {
            e.preventDefault();
            navigate("dashboard");
          }}
        >
          <span className="brand-mark">R</span>
          <span>
            retrack<span className="brand-sub">FACTORY WORKSPACE</span>
          </span>
        </a>
        <div className="workspace-name">
          <span className="workspace-avatar">GC</span>
          <div>
            <strong>{state.profile.companyName}</strong>
            <small>Nhà máy tái chế</small>
          </div>
        </div>
        <div className="nav-caption">KHÔNG GIAN LÀM VIỆC</div>
        <nav aria-label="Điều hướng nhà máy">
          {tabs.map(([key, label, icon]) => (
            <button
              key={key}
              className={tab === key ? "active" : ""}
              aria-current={tab === key ? "page" : undefined}
              onClick={() => navigate(key)}
            >
              <span className="nav-icon" aria-hidden="true">
                {icon}
              </span>
              {label}
              {key === "qc" && pending > 0 && (
                <span className="nav-count">{pending}</span>
              )}
            </button>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <span className="live-dot" /> Phiên bản trình diễn
          <small>Dữ liệu được lưu trên trình duyệt này.</small>
        </div>
      </aside>
      <div className="factory-workspace">
        <header className="workspace-topbar">
          <div>
            Nhà máy <span>/</span>{" "}
            <strong>{tabs.find((t) => t[0] === tab)?.[1]}</strong>
          </div>
          <span className="user-chip">
            <span>GC</span> Quản lý nhà máy
          </span>
        </header>
        <main className="factory-main">
          <div className="demo-banner">
            <span className="live-dot" />
            <strong>Không gian demo</strong>
            <span>
              Chưa kết nối máy chủ hoặc ngân hàng. Số liệu và trạng thái vận
              chuyển được mô phỏng.
            </span>
          </div>
          {tab === "dashboard" && <Dashboard navigate={navigate} />}
          {tab === "marketplace" && <Marketplace navigate={navigate} />}
          {tab === "demands" && <Demands />}
          {["orders", "qc", "settlements"].includes(tab) && (
            <Operations
              key={tab}
              mode={tab}
              focusId={focusId}
              navigate={navigate}
            />
          )}
          {tab === "partners" && <Partners navigate={navigate} />}
          {tab === "profile" && <Profile />}
          {tab === "prices" && <Prices />}
        </main>
        <footer className="factory-footer">
          RETRACK / Factory Operations
          <span>Nguồn nguyên liệu minh bạch, vận hành có kiểm soát.</span>
        </footer>
      </div>
    </div>
  );
}
export default function FactoryApp() {
  return (
    <FactoryProvider>
      <Workspace />
    </FactoryProvider>
  );
}
