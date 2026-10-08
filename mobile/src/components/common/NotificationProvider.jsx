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
    void feed.load();
    const refresh = () => {
      if (AppState.currentState === "active") void feed.load(feed.getSnapshot().data?.page || 1, true);
    };
    const timer = setInterval(refresh, 30000);
    const subscription = AppState.addEventListener("change", value => { if (value === "active") refresh(); });
    return () => { clearInterval(timer); subscription.remove(); feed.dispose(); };
  }, [feed]);
  return <Context.Provider value={{ feed, state, role }}>{children}</Context.Provider>;
}
