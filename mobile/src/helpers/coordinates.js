export function coordinatesOf(value) {
  if (
    value?.latitude == null ||
    value?.longitude == null ||
    value.latitude === "" ||
    value.longitude === ""
  )
    return null;
  const latitude = Number(value.latitude);
  const longitude = Number(value.longitude);
  return Number.isFinite(latitude) &&
    Number.isFinite(longitude) &&
    Math.abs(latitude) <= 90 &&
    Math.abs(longitude) <= 180
    ? { latitude, longitude }
    : null;
}

export function decodePolyline(encoded) {
  if (typeof encoded !== "string" || !encoded || encoded.length > 1000000)
    throw new Error("Dữ liệu tuyến đường không hợp lệ.");
  const points = [];
  let index = 0,
    lat = 0,
    lng = 0;
  function read() {
    let result = 0,
      shift = 0,
      byte;
    do {
      if (index >= encoded.length || shift > 30)
        throw new Error("Dữ liệu tuyến đường không hợp lệ.");
      byte = encoded.charCodeAt(index++) - 63;
      if (byte < 0 || byte > 63)
        throw new Error("Dữ liệu tuyến đường không hợp lệ.");
      result |= (byte & 31) << shift;
      shift += 5;
    } while (byte >= 32);
    return result & 1 ? ~(result >> 1) : result >> 1;
  }
  while (index < encoded.length) {
    lat += read();
    lng += read();
    const point = coordinatesOf({ latitude: lat / 1e5, longitude: lng / 1e5 });
    if (!point) throw new Error("Tọa độ tuyến đường không hợp lệ.");
    points.push(point);
  }
  return points;
}
