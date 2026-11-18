import { useEffect, useState } from 'react';
import { MapContainer, TileLayer, Circle, CircleMarker, Popup, useMap } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import { dispatchPoint } from './dispatchPresentation';

function FitPoints({ signature }) {
  const map = useMap();
  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      map.invalidateSize({ pan: false, animate: false });
      map.fitBounds(JSON.parse(signature), { padding: [24, 24], maxZoom: 15, animate: false });
    });
    return () => cancelAnimationFrame(frame);
  }, [map, signature]);
  return null;
}
export default function DispatchMap({ items = [], areas = false, onSelect, title = 'Vị trí trên trang đang xem' }) {
  const [tileError, setTileError] = useState(false);
  const points = items.flatMap(item => {
    const position = dispatchPoint(item.latitude, item.longitude);
    return position ? [{ item, position }] : [];
  });
  const bounds = points.flatMap(({ item, position: [lat, lng] }) => {
    const radius = areas ? Number(item.radiusMeters) : 100;
    const deltaLat = Math.max(0.001, radius / 111320);
    const deltaLng = deltaLat / Math.max(0.1, Math.cos(lat * Math.PI / 180));
    return [[lat - deltaLat, lng - deltaLng], [lat + deltaLat, lng + deltaLng]];
  });
  const signature = JSON.stringify(bounds);
  return <section aria-label={title} className="space-y-3 rounded-2xl border border-d-border-subtle bg-white p-4">
    <h3 className="font-semibold text-d-primary">{title}</h3>
    <p className="text-sm text-d-on-surface-variant">{areas ? 'Vòng tròn là vùng phục vụ được kho cấu hình, không phải vị trí nhân viên.' : 'Điểm thu gom do Seller cung cấp, không phải GPS trực tiếp của nhân viên.'} Bản đồ chỉ hiển thị dữ liệu trên trang đang xem; điểm thiếu tọa độ vẫn có trong danh sách.</p>
    {points.length ? <>
      <div className="relative isolate z-0 overflow-hidden rounded-xl border border-d-border-subtle">
        <MapContainer center={points[0].position} zoom={13} scrollWheelZoom={false} zoomAnimation={false} fadeAnimation={false} markerZoomAnimation={false} style={{ height: 288, width: '100%' }}>
          <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://tile.openstreetmap.org/{z}/{x}/{y}.png" eventHandlers={{ tileerror: () => setTileError(true) }} />
          <FitPoints signature={signature} />
          {points.map(({ item, position }) => areas
            ? <Circle key={item.id} center={position} radius={item.radiusMeters} pathOptions={{ color: item.isActive ? 'var(--color-d-primary)' : 'var(--color-d-on-surface-variant)', fillOpacity: 0.1 }}><Popup><strong>{item.name}</strong><p>{item.isActive ? 'Đang hoạt động' : 'Đã tắt'} · {Number(item.radiusMeters).toLocaleString('vi-VN')} m</p></Popup></Circle>
            : <CircleMarker key={item.id} center={position} radius={8} pathOptions={{ color: 'var(--color-d-primary)', fillOpacity: 0.8 }}><Popup><strong>{item.sellerName}</strong><p>{item.address}</p>{onSelect && <button type="button" className="font-semibold underline" onClick={() => onSelect(item.id)}>Chi tiết / phân công</button>}</Popup></CircleMarker>)}
        </MapContainer>
      </div>
      {tileError && <p role="status" className="text-sm text-d-on-surface-variant">Chưa tải được nền bản đồ. Hãy kiểm tra mạng; địa chỉ và tọa độ trong danh sách vẫn sử dụng được.</p>}
    </> : <p role="status" className="rounded-xl bg-d-surface-accent p-4 text-sm">Chưa có tọa độ hợp lệ để hiển thị bản đồ. Không suy đoán vị trí từ địa chỉ.</p>}
  </section>;
}
