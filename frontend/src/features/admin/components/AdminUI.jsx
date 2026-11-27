import Pagination from '@/components/ui/Pagination';
import { Modal, ConfirmModal } from '@/components/liquidation/LiquidationUI';
import { ui } from '@/components/ui/kit';

// Khu Admin dùng bộ thành phần chuẩn của RETRACK (components/ui/kit); giữ nguyên tên export cũ.
export {
  TONES, Badge, Notice, PageHeader, PrimaryButton, GhostButton, Button, StatCard, FilterTabs, Panel, SearchBox,
  Th, Td, DataTable, TableMessage, IconAction, Field, DetailList, Avatar, FormFooter, EmptyState, ErrorState, LoadingState,
} from '@/components/ui/kit';
export { Modal, ConfirmModal, Pagination };

export const inputClass = ui.input;
export const labelClass = ui.label;
