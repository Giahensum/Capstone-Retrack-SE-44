// Một nguồn dữ liệu cho các chuông trong cùng phiên; không lưu thông báo xuống máy.
export function createNotificationFeed(list, markRead, messageForError) {
  let state = { data: null, loading: false, error: "", busy: false };
  let active = true, generation = 0, request = null, readRequest = null;
  const listeners = new Set();
  const update = patch => {
    state = { ...state, ...patch };
    listeners.forEach(listener => listener());
  };
  const feed = {
    getSnapshot: () => state,
    subscribe: listener => { listeners.add(listener); return () => listeners.delete(listener); },
    start: () => { active = true; },
    dispose: () => {
      active = false; generation++;
      request?.abort(); readRequest?.abort();
    },
    async load(page = 1, quiet = false) {
      if (!active || state.busy) return;
      request?.abort();
      const controller = new AbortController();
      request = controller;
      const version = ++generation;
      update({ loading: !quiet, error: "" });
      try {
        const data = await list(page, controller.signal);
        if (active && version === generation && !controller.signal.aborted) update({ data });
      } catch (error) {
        if (active && version === generation && !controller.signal.aborted)
          update({ error: messageForError(error), data: null });
      } finally {
        if (active && version === generation) update({ loading: false });
      }
    },
    async read(item) {
      if (!active || state.busy) return false;
      request?.abort();
      const version = ++generation;
      const controller = new AbortController();
      readRequest = controller;
      update({ busy: true, loading: false, error: "" });
      try {
        await markRead(item.id, controller.signal);
        if (!active || version !== generation || controller.signal.aborted) return false;
        const data = state.data;
        const stored = data?.items.find(n => n.id === item.id);
        if (data) update({ data: { ...data,
          unreadCount: Math.max(0, data.unreadCount - (stored && !stored.isRead ? 1 : 0)),
          items: data.items.map(n => n.id === item.id ? { ...n, isRead: true } : n),
        } });
        return true;
      } catch (error) {
        if (active && version === generation && !controller.signal.aborted)
          update({ error: messageForError(error) });
        return false;
      } finally {
        if (active && version === generation) update({ busy: false });
      }
    },
  };
  return feed;
}

export function notificationDestination(role, item) {
  if (role === "DEPOT_EMPLOYEE" && item.pickupRequestId)
    return { name: "Pool", params: { screen: "PickupDetail", params: { pickupId: item.pickupRequestId } } };
  if (role === "DRIVER" && item.jobId)
    return { name: "Jobs", params: { screen: "JobDetail", params: { jobId: item.jobId } } };
  return null;
}
