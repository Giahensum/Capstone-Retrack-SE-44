import { decodePolyline } from "./coordinates.js";

// Product policy: save distance while allowing at most 15% extra travel time.
// This selects among the routes supplied by the provider, not the whole road network.
export const BALANCED_TIME_FACTOR = 1.15;

/** Convert provider routes to comparable totals in meters and seconds. */
export function normalizeRoutes(rawRoutes) {
  if (!Array.isArray(rawRoutes)) return [];

  return rawRoutes.flatMap((route, index) => {
    const legs = route?.legs;
    const polylineEncoded = route?.overview_polyline?.points;
    if (!Array.isArray(legs) || !legs.length) return [];

    let distanceMeters = 0;
    let durationSeconds = 0;
    for (const leg of legs) {
      const distance = leg?.distance?.value;
      const duration = leg?.duration?.value;
      if (
        !Number.isFinite(distance) ||
        !Number.isFinite(duration) ||
        distance < 0 ||
        duration < 0
      )
        return [];
      distanceMeters += distance;
      durationSeconds += duration;
    }

    // A duplicate waypoint may produce a zero leg, but a drawable travelling
    // route must have positive totals. Missing metrics are never treated as zero.
    if (
      !Number.isFinite(distanceMeters) ||
      !Number.isFinite(durationSeconds) ||
      distanceMeters <= 0 ||
      durationSeconds <= 0
    )
      return [];

    try {
      if (decodePolyline(polylineEncoded).length < 2) return [];
    } catch {
      return [];
    }

    return [{ id: String(index), distanceMeters, durationSeconds, polylineEncoded }];
  });
}

/**
 * Select one normalized route without changing the provider's route order.
 * Ties keep the first provider result after the secondary metric is compared.
 */
export function selectRoute(routes, mode = "balanced") {
  if (!["balanced", "shortest", "fastest"].includes(mode))
    throw new RangeError("Unknown route selection mode.");
  if (!Array.isArray(routes) || !routes.length) return null;

  let candidates = routes;
  if (mode === "balanced") {
    const fastestSeconds = routes.reduce(
      (minimum, route) => Math.min(minimum, route.durationSeconds),
      Infinity,
    );
    const limit = fastestSeconds * BALANCED_TIME_FACTOR;
    // Include an exact 15% boundary despite IEEE-754 multiplication rounding.
    const tolerance = Number.EPSILON * Math.max(1, limit) * 4;
    candidates = routes.filter((route) => route.durationSeconds <= limit + tolerance);
  }

  const primary = mode === "fastest" ? "durationSeconds" : "distanceMeters";
  const secondary = mode === "fastest" ? "distanceMeters" : "durationSeconds";
  return candidates.reduce((best, route) => {
    if (
      !best ||
      route[primary] < best[primary] ||
      (route[primary] === best[primary] && route[secondary] < best[secondary])
    )
      return route;
    return best;
  }, null);
}
