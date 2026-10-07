import {
  Activity, Archive, ArrowRight, Badge, Banknote, Box, Boxes, Building2,
  CalendarDays, ChartColumn, ChartNoAxesCombined, ChevronRight, CircleCheck,
  CircleX, ClipboardCheck, ClipboardList, Clock, Eye, Factory,
  Globe, Handshake, Hourglass, LayoutDashboard, Lightbulb, ListFilter,
  LockKeyhole, Menu, PackageCheck, Plus, Recycle, ReceiptText, Scale, Search,
  ShoppingCart, SquarePlus, Star, TrendingUp, TriangleAlert, Truck, Undo2,
  UserRoundPlus, Wallet, Warehouse,
} from 'lucide-react';

const icons = {
  add: Plus, add_box: SquarePlus, arrow_forward: ArrowRight,
  auto_graph: TrendingUp, badge: Badge, bar_chart: ChartColumn,
  calendar_today: CalendarDays, cancel: CircleX, category: Boxes,
  check_circle: CircleCheck, chevron_right: ChevronRight, dashboard: LayoutDashboard,
  domain: Building2, fact_check: ClipboardCheck, factory: Factory,
  filter_list: ListFilter, handshake: Handshake, hourglass_empty: Hourglass,
  inventory: Archive, inventory_2: Box, lightbulb: Lightbulb,
  local_shipping: Truck, lock: LockKeyhole, menu: Menu, monitoring: ChartNoAxesCombined,
  payments: Banknote, person_add: UserRoundPlus, public: Globe,
  publish: PackageCheck, receipt: ReceiptText, receipt_long: ClipboardList,
  recycling: Recycle, scale: Scale, search: Search, shopping_cart: ShoppingCart,
  star: Star, task_alt: CircleCheck, undo: Undo2, visibility: Eye,
  warehouse: Warehouse, warning: TriangleAlert,
  account_balance_wallet: Wallet, schedule: Clock, check_circle_filled: CircleCheck,
};

// Icon Depot được đóng gói cùng ứng dụng, không phụ thuộc font tải từ bên ngoài.
export function MaterialIcon({ name, className = '', style }) {
  const Icon = icons[name] ?? Activity;
  return <Icon aria-hidden="true" focusable="false" className={className} style={style} width="1em" height="1em" strokeWidth={2} />;
}
