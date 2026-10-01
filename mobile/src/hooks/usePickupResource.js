import { useCallback, useRef, useState } from "react";
import { useFocusEffect } from "@react-navigation/native";
import { errorMessage } from "../api/client";

// Screen-scoped data: abort on blur/unmount and ignore an older refresh response.
export default function usePickupResource(loader) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const pending = useRef(null);
  const load = useCallback(
    async (refresh = false) => {
      pending.current?.abort();
      const controller = new AbortController();
      pending.current = controller;
      setError("");
      setLoading(!refresh);
      setRefreshing(refresh);
      try {
        const result = await loader(controller.signal);
        if (!controller.signal.aborted) setData(result);
      } catch (e) {
        if (!controller.signal.aborted) {
          setData(null);
          setError(errorMessage(e));
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
          setRefreshing(false);
        }
      }
    },
    [loader],
  );
  useFocusEffect(
    useCallback(() => {
      void load();
      return () => pending.current?.abort();
    }, [load]),
  );
  return { data, setData, loading, refreshing, error, reload: load };
}
