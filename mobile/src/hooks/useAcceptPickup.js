import { useEffect, useRef, useState } from "react";
import { Alert } from "react-native";
import { pickupApi } from "../api/pickupApi";
import { errorMessage } from "../api/client";
import useAuthStore from "../store/authStore";

export default function useAcceptPickup(onAccepted, onConflict) {
  const [acceptingId, setAcceptingId] = useState(null);
  const busy = useRef(false);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  function confirm(pickup) {
    if (busy.current) return;
    busy.current = true;
    const token = useAuthStore.getState().token;
    const current = () =>
      mounted.current && useAuthStore.getState().token === token;
    Alert.alert(
      "Xác nhận nhận đơn?",
      "Bạn sẽ chịu trách nhiệm đến thu gom tại địa chỉ này.",
      [
        {
          text: "Hủy",
          style: "cancel",
          onPress: () => {
            busy.current = false;
          },
        },
        {
          text: "Nhận đơn",
          onPress: async () => {
            if (!current()) {
              busy.current = false;
              return;
            }
            setAcceptingId(pickup.id);
            try {
              const result = await pickupApi.acceptPickup(pickup.id);
              if (current()) {
                onAccepted(result);
                Alert.alert(
                  "Đã nhận đơn thành công",
                  "Hãy đến địa chỉ người bán.",
                );
              }
            } catch (e) {
              if (current()) {
                if (e.response?.status === 409) {
                  onConflict();
                  Alert.alert(
                    "Đơn không còn khả dụng",
                    "Đơn này đã được nhận hoặc không còn ở trạng thái chờ. Danh sách sẽ được cập nhật.",
                  );
                } else Alert.alert("Không thể nhận đơn", errorMessage(e));
              }
            } finally {
              busy.current = false;
              if (current()) setAcceptingId(null);
            }
          },
        },
      ],
      { cancelable: false },
    );
  }
  return { acceptingId, confirm };
}
