import { createContext, useContext, useEffect, useState, useSyncExternalStore } from "react";
import { AppState } from "react-native";
import { pickupApi } from "../../api/pickupApi";
import { driverApi } from "../../api/driverApi";
import { errorMessage } from "../../api/client";
import { createNotificationFeed } from "../../helpers/notificationFeed";

const Context = createContext(/** @type {any} */ (null));
export const useNotifications = () => useContext(Context);

// AppNavigator remount provider theo token, không chia sẻ dữ liệu giữa hai tài khoản.
export default function NotificationProvider({ role, children }) {
  const [feed] = useState(() => createNotificationFeed(
    role === "DRIVER" ? driverApi.notices : pickupApi.getNotifications,
    role === "DRIVER" ? driverApi.read : pickupApi.readNotification,
    errorMessage,
  ));
  const state = useSyncExternalStore(feed.subscribe, feed.getSnapshot);
  useEffect(() => {
    feed.start();
    let active = true;
    let timer;
    // Chờ tải xong để mạng chậm không bị hủy request bởi lần polling kế tiếp.
    const poll = async (initial = false) => {
      if (initial || AppState.currentState === "active")
        await feed.load(initial ? 1 : feed.getSnapshot().data?.page || 1, !initial);
      if (active) timer = setTimeout(() => { void poll(); }, role === "DEPOT_EMPLOYEE" ? 5000 : 30000);
    };
    void poll(true);
    const refresh = () => {
      if (AppState.currentState === "active") void feed.load(feed.getSnapshot().data?.page || 1, true);
    };
    const subscription = AppState.addEventListener("change", value => { if (value === "active") refresh(); });
    return () => { active = false; clearTimeout(timer); subscription.remove(); feed.dispose(); };
  }, [feed, role]);
  return <Context.Provider value={{ feed, state, role }}>{children}</Context.Provider>;
}
