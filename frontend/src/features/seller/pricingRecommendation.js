import { CLASS_METADATA_MAP } from './aiWasteMetadata.js';

/**
 * Điều kiện chất lượng phế liệu thu gom thực tế
 */
export const QUALITY_CONDITIONS = [
  {
    id: 'CLEAN_COMPACTED',
    label: 'Hạng A: Sạch, ép dẹp, khô ráo',
    shortLabel: 'Hạng A (+8%)',
    desc: 'Đã phân loại riêng, bẹp/gấp phẳng, không dính cặn bẩn (+8% giá thu mua)',
    bonusRate: 0.08,
    badgeColor: 'text-emerald-700 bg-emerald-500/15 border-emerald-500/30',
  },
  {
    id: 'STANDARD',
    label: 'Hạng B: Tiêu chuẩn thông thường',
    shortLabel: 'Hạng B (Chuẩn)',
    desc: 'Phế liệu tương đối sạch, khô ráo, chưa ép phẳng (100% giá niêm yết)',
    bonusRate: 0.0,
    badgeColor: 'text-blue-700 bg-blue-500/15 border-blue-500/30',
  },
  {
    id: 'DIRTY_MIXED',
    label: 'Hạng C: Chưa sạch / Lẫn tạp chất',
    shortLabel: 'Hạng C (-10%)',
    desc: 'Còn dính cặn lỏng, dầu mỡ hoặc lẫn tạp chất (-10% khấu trừ làm sạch)',
    bonusRate: -0.10,
    badgeColor: 'text-amber-700 bg-amber-500/15 border-amber-500/30',
  },
];

/**
 * Hình thức bàn giao / vận chuyển phế liệu
 */
export const FULFILLMENT_METHODS = [
  {
    id: 'DOORSTEP',
    label: 'Nhân viên đến thu gom tận nhà',
    desc: 'Điều phối nhân viên đến tận nơi cân đo và thanh toán theo tuyến đường Goong.io',
    icon: 'local_shipping',
  },
  {
    id: 'DROP_OFF',
    label: 'Tự mang đến kho vựa gần nhất',
    desc: 'Nhận trọn vẹn 100% mức giá cao nhất của vựa, không mất phí điều phối xe',
    icon: 'storefront',
  },
];

/**
 * Tính khoảng cách đường chim bay Haversine (km) làm cơ chế dự phòng
 */
export function calculateHaversineDistanceKm(lat1, lon1, lat2, lon2) {
  if (lat1 == null || lon1 == null || lat2 == null || lon2 == null) return null;
  const R = 6371.0;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return Math.round(R * c * 10) / 10;
}

/**
 * Tính toán khoảng cách thực tế giữa người bán và các kho vựa bằng Goong Distance Matrix API
 * (Có cơ chế fallback Haversine tự động nếu Goong gián đoạn hoặc quá hạn mức)
 */
export async function calculateDepotsDistanceWithGoong(depots, userLat, userLng) {
  if (!depots || depots.length === 0 || userLat == null || userLng == null) {
    return depots || [];
  }

  const validDepots = depots.filter((d) => d.latitude != null && d.longitude != null);
  const invalidDepots = depots.filter((d) => d.latitude == null || d.longitude == null);

  const GOONG_API_KEY = import.meta.env.VITE_GOONG_API_KEY;

  if (GOONG_API_KEY && GOONG_API_KEY !== 'your_goong_api_key_here' && validDepots.length > 0) {
    try {
      const origins = `${userLat},${userLng}`;
      const destinations = validDepots.map((d) => `${d.latitude},${d.longitude}`).join('|');
      const url = `https://rsapi.goong.io/DistanceMatrix?origins=${origins}&destinations=${destinations}&vehicle=bike&api_key=${GOONG_API_KEY}`;

      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        if (data.rows && data.rows[0]?.elements) {
          const elements = data.rows[0].elements;
          validDepots.forEach((depot, idx) => {
            if (elements[idx]?.status === 'OK') {
              depot.routingDistanceKm = elements[idx].distance.value / 1000;
              depot.routingDurationText = elements[idx].duration.text;
              depot.isGoongRouting = true;
            } else {
              depot.routingDistanceKm = calculateHaversineDistanceKm(userLat, userLng, depot.latitude, depot.longitude);
              depot.routingDurationText = depot.routingDistanceKm ? `~${Math.round(depot.routingDistanceKm * 3.5)} phút` : null;
              depot.isGoongRouting = false;
            }
          });

          return [...validDepots, ...invalidDepots].sort((a, b) => {
            const distA = a.routingDistanceKm ?? a.distanceKm ?? 9999;
            const distB = b.routingDistanceKm ?? b.distanceKm ?? 9999;
            return distA - distB;
          });
        }
      }
    } catch (err) {
      console.warn('Goong Distance Matrix gián đoạn, chuyển sang dự phòng Haversine:', err);
    }
  }

  // Fallback Haversine nếu không có Goong hoặc API lỗi
  validDepots.forEach((depot) => {
    depot.routingDistanceKm = calculateHaversineDistanceKm(userLat, userLng, depot.latitude, depot.longitude);
    depot.routingDurationText = depot.routingDistanceKm ? `~${Math.round(depot.routingDistanceKm * 3.5)} phút` : null;
    depot.isGoongRouting = false;
  });

  return [...validDepots, ...invalidDepots].sort((a, b) => {
    const distA = a.routingDistanceKm ?? a.distanceKm ?? 9999;
    const distB = b.routingDistanceKm ?? b.distanceKm ?? 9999;
    return distA - distB;
  });
}

/**
 * Thuật toán định giá thị trường thông minh (Zero-Hallucination Pricing Engine)
 * Kết hợp:
 * 1. Giá niêm yết kho vựa đối tác gần nhất trong hệ thống
 * 2. Khoảng cách đường bộ thực tế (Goong.io)
 * 3. Tình trạng chất lượng phế liệu
 * 4. Trợ giá khối lượng lớn (Volume incentive)
 * 5. Hình thức thu gom tận nơi hay mang tới vựa
 */
export function calculateSmartPriceRecommendation({
  wasteItem,
  nearestDepot,
  weightKg = 10,
  qualityId = 'STANDARD',
  fulfillment = 'DOORSTEP',
  _userLocation,
}) {
  if (!wasteItem || wasteItem.is_recyclable === false) {
    return {
      isRecyclable: false,
      recommendedUnitPrice: 0,
      estimatedTotal: 0,
      basePrice: 0,
      priceSource: 'NON_RECYCLABLE',
      breakdown: null,
      message: 'Vật phẩm thuộc nhóm không thu gom (Rác hữu cơ / Rác hỗn hợp đô thị).',
    };
  }

  const meta = CLASS_METADATA_MAP[wasteItem.class_key] || {};
  const materialKey = (wasteItem.material_type || meta.material_type || 'OTHER').toUpperCase();

  // Bảng giá cơ sở chuẩn hóa theo dữ liệu thị trường thực tế ReTrack
  const DEFAULT_BASE_PRICES = {
    metal: 22000,       // Lon nhôm, sắt thép
    cardboard: 4500,    // Thùng carton
    paper: 4800,        // Sách báo cũ, tài liệu A4
    plastic: 8500,      // Chai nhựa PET, can HDPE
    battery: 20000,     // Pin, ắc quy cũ
    'brown-glass': 1000,// Vỏ chai bia nâu
    'green-glass': 1000,// Vỏ chai bia xanh
    'white-glass': 1200,// Thủy tinh trắng
    clothes: 3500,      // Quần áo cũ, vải vụn
    shoes: 4000,        // Giày dép cũ
  };

  // 1. Xác định Giá Cơ Sở (Base Price) từ dữ liệu kho hoặc hệ thống
  let basePrice = 0;
  let priceSource = 'MARKET_INDEX'; // 'DEPOT_PUBLISHED' | 'SYSTEM_LATEST' | 'MARKET_INDEX'
  let depotPublishedPrice = null;

  // Ưu tiên 1: Lấy trực tiếp từ bảng giá công bố của kho vựa gần nhất
  if (nearestDepot?.prices && Object.keys(nearestDepot.prices).length > 0) {
    const candidates = [
      materialKey,
      wasteItem.class_key?.toUpperCase(),
      wasteItem.class_key,
      // mapping phụ trợ
      wasteItem.class_key === 'metal' ? 'ALUMINUM' : null,
      wasteItem.class_key === 'plastic' ? 'PET' : null,
      wasteItem.class_key === 'cardboard' ? 'CARDBOARD' : null,
      wasteItem.class_key === 'paper' ? 'PAPER' : null,
    ].filter(Boolean);

    for (const key of candidates) {
      if (nearestDepot.prices[key] && Number(nearestDepot.prices[key]) > 0) {
        basePrice = Number(nearestDepot.prices[key]);
        depotPublishedPrice = basePrice;
        priceSource = 'DEPOT_PUBLISHED';
        break;
      }
    }
  }

  // Ưu tiên 2: Nếu kho chưa có giá loại này, lấy giá thị trường hệ thống từ backend
  if (!basePrice) {
    if (wasteItem.current_market_price && Number(wasteItem.current_market_price) > 0) {
      basePrice = Number(wasteItem.current_market_price);
      priceSource = 'SYSTEM_LATEST';
    } else {
      basePrice = DEFAULT_BASE_PRICES[wasteItem.class_key] || 5000;
      priceSource = 'MARKET_INDEX';
    }
  }

  // 2. Điều chỉnh theo Tình trạng chất lượng phế liệu
  const quality = QUALITY_CONDITIONS.find((q) => q.id === qualityId) || QUALITY_CONDITIONS[1];
  const qualityAdjustment = Math.round(basePrice * quality.bonusRate);

  // 3. Trợ giá Khối lượng lớn (Volume incentive)
  let volumeIncentivePerKg = 0;
  if (weightKg >= 50) {
    volumeIncentivePerKg = Math.round(basePrice * 0.05); // +5% nếu bán từ 50kg trở lên
  } else if (weightKg >= 20) {
    volumeIncentivePerKg = Math.round(basePrice * 0.025); // +2.5% nếu bán từ 20kg trở lên
  }

  // 4. Chi phí điều phối / vận chuyển theo cự ly thực tế (Goong.io km)
  let logisticsDeductionPerKg = 0;
  const distanceKm = nearestDepot?.routingDistanceKm ?? nearestDepot?.distanceKm ?? null;
  const durationText = nearestDepot?.routingDurationText ?? null;

  if (fulfillment === 'DOORSTEP') {
    if (weightKg >= 30) {
      // Đơn hàng lớn từ 30kg: Vựa hỗ trợ miễn phí vận chuyển tận nhà
      logisticsDeductionPerKg = 0;
    } else {
      // Đơn hàng nhỏ: Khấu trừ chi phí xăng xe di chuyển dựa trên km Goong thực tế
      const dist = distanceKm || 2.5; // Mặc định 2.5km nếu chưa định vị
      // Cước di chuyển tham khảo ~3.000đ/km (tối thiểu 5.000đ, tối đa 25.000đ)
      const tripFee = Math.min(25000, Math.max(5000, Math.round(dist * 3000)));
      // Khấu trừ trên mỗi kg, nhưng không vượt quá 15% đơn giá phế liệu để bảo vệ quyền lợi người bán
      const rawDeduction = Math.round(tripFee / Math.max(weightKg, 1));
      logisticsDeductionPerKg = Math.min(rawDeduction, Math.round(basePrice * 0.15));
    }
  }

  // 5. Tính đơn giá khuyến nghị sau cùng (Làm tròn tới 100 VNĐ)
  const calculatedUnitPrice = basePrice + qualityAdjustment + volumeIncentivePerKg - logisticsDeductionPerKg;
  const recommendedUnitPrice = Math.max(500, Math.round(calculatedUnitPrice / 100) * 100);

  // 6. Tổng số tiền thực nhận ước tính
  const estimatedTotal = Math.round(recommendedUnitPrice * weightKg);

  return {
    isRecyclable: true,
    recommendedUnitPrice,
    estimatedTotal,
    basePrice,
    priceSource,
    depotPublishedPrice,
    distanceKm,
    durationText,
    isGoongRouting: nearestDepot?.isGoongRouting ?? false,
    quality,
    breakdown: {
      basePrice,
      qualityAdjustment,
      qualityRatePercent: quality.bonusRate * 100,
      qualityLabel: quality.label,
      volumeIncentivePerKg,
      logisticsDeductionPerKg,
      fulfillmentMethod: fulfillment,
      weightKg,
    },
    nearestDepot: nearestDepot || null,
  };
}
