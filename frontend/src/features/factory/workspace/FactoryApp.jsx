import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
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
import FactoryPlatformFees from "./pages/FactoryPlatformFees";
import FactoryNotifications, { FactoryNotificationBell } from "./pages/FactoryNotifications";
import MoneyEarnings from "@/features/money-payee/EarningsPage";
import { request } from "./data/factoryApi";
import AppShell from "@/components/layout/AppShell";
import {
  LayoutDashboard, Store, ClipboardList, Truck, Scale, FileCheck2, ReceiptText, Wallet, Bell, Handshake, TrendingUp, Building2,
} from "lucide-react";
import "./factory.css";

// Menu nhà máy: mỗi mục là một tab nội bộ (không đổi URL), nhóm theo nghiệp vụ như các vai trò khác.
const tabs = [
  ["dashboard", "Tổng quan", LayoutDashboard],
  ["marketplace", "Sàn nguyên liệu", Store],
  ["demands", "Nhu cầu thu mua", ClipboardList],
  ["orders", "Đơn hàng & Vận chuyển", Truck],
  ["qc", "Trạm cân & KCS", Scale],
  ["settlements", "Quyết toán", FileCheck2],
  ["platform-fees", "Hóa đơn phí cũ", ReceiptText],
  ["earnings", "Thu nhập & chi trả", Wallet],
  ["notifications", "Thông báo", Bell],
  ["partners", "Vựa đối tác", Handshake],
  ["prices", "Giá tham khảo", TrendingUp],
  ["profile", "Hồ sơ nhà máy", Building2],
];
const groups = [
  ["Chung", ["dashboard"]],
  ["Thu mua", ["marketplace", "demands", "partners", "prices"]],
  ["Vận hành", ["orders", "qc", "settlements"]],
  ["Tài chính", ["earnings", "platform-fees"]],
  ["Tài khoản", ["profile"]],
];

function Workspace() {
  const { state, user, logout, loading, retry } = useFactory();
  const historicalInvoices = useQuery({
    queryKey: ["factory", "platform-fees", "invoice-count"],
    queryFn: () => request("/api/factory/platform-fees/invoices?page=1&pageSize=1"),
    enabled: Boolean(state),
    staleTime: 60_000,
  });
  const navigateRoute = useNavigate();
  // DocuSign đưa người dùng quay lại với ?esign=return: mở thẳng tab đối tác để thấy kết quả ký.
  const [tab, setTab] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    return params.has("payos") ? "platform-fees" : params.get("esign") === "return" ? "partners" : "dashboard";
  });
  const [focusId, setFocusId] = useState(null);
  function navigate(next, orderId = null) {
    setFocusId(orderId);
    setTab(next);
    window.scrollTo({ top: 0 });
  }
  if (loading) return <main className="factory-auth-page" role="status">Đang tải dữ liệu nhà máy…</main>;
  if (!state) return <main className="factory-auth-page"><section className="factory-auth-card"><h1>Không tải được dữ liệu nhà máy</h1><p>Kiểm tra kết nối máy chủ rồi thử lại bằng phiên đăng nhập hiện tại.</p><button className="btn" onClick={retry}>Thử kết nối lại</button></section></main>;
  const pending = state.dashboard?.pendingQcCount ?? 0;
  // Hóa đơn phí cũ chỉ hiện khi còn dữ liệu lịch sử (hoặc chưa kiểm tra được).
  const showFees = historicalInvoices.isError || (historicalInvoices.data?.totalCount ?? 0) > 0;
  const byKey = Object.fromEntries(tabs.map(([key, label, icon]) => [key, { key, label, icon }]));
  const nav = groups.map(([title, keys]) => ({
    title,
    items: keys
      .filter((key) => key !== "platform-fees" || showFees)
      .map((key) => ({ ...byKey[key], active: tab === key, onClick: () => navigate(key), badge: key === "qc" ? pending : 0 })),
  }));
  return (
    <AppShell
      nav={nav}
      eyebrow="Nhà máy tái chế"
      onHome={() => navigate("dashboard")}
      homeLabel={state.profile.companyName || "Nhà máy"}
      crumb={byKey[tab]?.label}
      roleLabel={state.profile.companyName || "Nhà máy tái chế"}
      roleBadge="FACTORY"
      user={user}
      onLogout={() => { logout(); navigateRoute("/login", { replace: true }); }}
      menuItems={[{ key: "profile", icon: Building2, label: "Hồ sơ nhà máy", onClick: () => navigate("profile") }]}
      storageKey="retrack_factory_sidebar"
      navLabel="Điều hướng nhà máy"
      headerActions={<FactoryNotificationBell navigate={navigate} />}
    >
      <div className="factory-shell factory-shell--embedded">
        <div className="factory-main">
          {tab === "dashboard" && <Dashboard navigate={navigate} />}
          {tab === "marketplace" && <Marketplace navigate={navigate} directOnly={Boolean(focusId)} />}
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
          {tab === "platform-fees" && <FactoryPlatformFees />}
          {tab === "earnings" && <MoneyEarnings />}
          {tab === "notifications" && <FactoryNotifications navigate={navigate} />}
          {tab === "profile" && <Profile />}
          {tab === "prices" && <Prices />}
        </div>
      </div>
    </AppShell>
  );
}
export default function FactoryApp() {
  return (
    <FactoryProvider>
      <Workspace />
    </FactoryProvider>
  );
}
