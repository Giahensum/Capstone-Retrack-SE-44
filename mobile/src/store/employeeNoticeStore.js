import { create } from "zustand";

// Chỉ lưu bộ đếm giao diện; API vẫn kiểm tra người nhận bằng JWT ở mỗi lần đọc.
const useEmployeeNoticeStore = create(set => ({
  unread: null,
  setUnread: unread => set({ unread }),
}));
export default useEmployeeNoticeStore;
