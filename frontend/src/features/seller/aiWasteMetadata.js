export const CLASS_METADATA_MAP = {
  cardboard: {
    name_vi: 'Bìa Carton & Thùng giấy',
    material_type: 'CARDBOARD',
    is_recyclable: true,
    icon: 'category',
    unit: 'kg',
    price_range: '3.000 - 5.500 đ/kg',
    recycling_tip: 'Tháo dỡ băng keo, ép dẹp các thùng carton phẳng và giữ khô ráo để đạt khối lượng và mức giá tốt nhất.',
    suggested_description: 'Có phế liệu bìa carton và thùng giấy cũ đã gấp gọn, tình trạng khô ráo cần kho vựa thu gom.',
  },
  paper: {
    name_vi: 'Giấy vụn, Báo & Sách vở',
    material_type: 'PAPER',
    is_recyclable: true,
    icon: 'description',
    unit: 'kg',
    price_range: '3.500 - 6.000 đ/kg',
    recycling_tip: 'Sách báo cũ, tài liệu photo giấy A4 văn phòng nên buộc thành từng cọc phẳng phiu, tránh dính nước hoặc bùn đất.',
    suggested_description: 'Cần thanh lý phế liệu giấy vụn, sách báo cũ và tài liệu giấy đã gom gọn, nhờ kho vựa đến cân và thu gom.',
  },
  plastic: {
    name_vi: 'Nhựa tái chế (Chai PET, Can nhựa, Hộp)',
    material_type: 'PET',
    is_recyclable: true,
    icon: 'recycling',
    unit: 'kg',
    price_range: '6.000 - 11.000 đ/kg',
    recycling_tip: 'Đổ sạch chất lỏng dư thừa, giẫm bẹp vỏ chai để giảm thể tích cồng kềnh giúp gom được nhiều hơn.',
    suggested_description: 'Cần thu gom phế liệu nhựa tái chế (chai nhựa PET, vỏ hộp/can nhựa cũ) đã để ráo sạch, nhờ nhân viên kho đến cân thực tế.',
  },
  metal: {
    name_vi: 'Kim loại (Lon nhôm, Sắt thép, Đồ cũ)',
    material_type: 'ALUMINUM',
    is_recyclable: true,
    icon: 'hardware',
    unit: 'kg',
    price_range: '16.000 - 28.000 đ/kg (Lon nhôm) / 8.000 - 13.000 đ/kg (Sắt)',
    recycling_tip: 'Tách riêng vỏ lon nhôm mềm (lon bia, nước ngọt) và kim loại sắt vụn để nhân viên cân tính giá chính xác nhất.',
    suggested_description: 'Cần bán phế liệu kim loại (vỏ lon nhôm, sắt vụn hoặc vật dụng kim loại cũ) đã gom riêng, nhờ kho đến cân và thu gom.',
  },
  battery: {
    name_vi: 'Pin & Ắc quy cũ (Rác nguy hại)',
    material_type: 'ELECTRONIC_WASTE',
    is_recyclable: true,
    icon: 'battery_alert',
    unit: 'kg',
    price_range: '10.000 - 30.000 đ/kg',
    recycling_tip: 'Rác thải điện tử có chứa hóa chất. Hãy dán băng keo bọc hai đầu cực, đựng trong hộp nhựa kín tránh ẩm và va chạm mạnh.',
    suggested_description: 'Pin phế liệu đã qua sử dụng và ắc quy cũ cần bàn giao kho thu gom xử lý theo tiêu chuẩn môi trường.',
  },
  'brown-glass': {
    name_vi: 'Thủy tinh màu nâu (Chai bia, Dược phẩm)',
    material_type: 'OTHER',
    is_recyclable: true,
    icon: 'wine_bar',
    unit: 'chai/kg',
    price_range: '500 - 1.500 đ/vỏ chai',
    recycling_tip: 'Xếp vỏ chai đứng ngay ngắn trong thùng/két nhựa hoặc thùng carton để tránh va đập vỡ gây nguy hiểm.',
    suggested_description: 'Vỏ chai thủy tinh nâu (chai bia, nước sâm) còn nguyên vẹn cần bán thanh lý cho kho tái chế.',
  },
  'green-glass': {
    name_vi: 'Thủy tinh màu xanh lá (Rượu vang, Nước giải khát)',
    material_type: 'OTHER',
    is_recyclable: true,
    icon: 'wine_bar',
    unit: 'chai/kg',
    price_range: '500 - 1.500 đ/vỏ chai',
    recycling_tip: 'Kiểm tra chai còn nguyên miệng và thân; giữ chai sạch sẽ trước khi đưa vào thùng lưu trữ.',
    suggested_description: 'Chai thủy tinh màu xanh lá các loại còn nguyên vẹn cần vựa thu gom.',
  },
  'white-glass': {
    name_vi: 'Thủy tinh trắng trong suốt',
    material_type: 'OTHER',
    is_recyclable: true,
    icon: 'wine_bar',
    unit: 'chai/kg',
    price_range: '500 - 1.500 đ/vỏ chai',
    recycling_tip: 'Rửa sạch cặn bên trong, tháo nắp kim loại/nhựa để kho vựa thu gom nhanh chóng phân loại.',
    suggested_description: 'Chai lọ thủy tinh trắng trong suốt các loại đã súc sạch nước cần thu gom.',
  },
  clothes: {
    name_vi: 'Quần áo cũ & Vải vụn',
    material_type: 'OTHER',
    is_recyclable: true,
    icon: 'checkroom',
    unit: 'kg',
    price_range: '2.000 - 5.000 đ/kg',
    recycling_tip: 'Quần áo còn lành lặn có thể tái sử dụng/từ thiện; vải rách gom vào bao sạch để bán làm nguyên liệu tái chế sợi.',
    suggested_description: 'Thanh lý quần áo cũ dọn tủ và vải vụn gia đình đã đóng thành bao tải.',
  },
  shoes: {
    name_vi: 'Giày dép cũ',
    material_type: 'OTHER',
    is_recyclable: false,
    icon: 'footwear',
    unit: 'đôi/kg',
    price_range: 'Thương lượng tùy vựa',
    recycling_tip: 'Một số loại giày da/thể thao còn lành có thể được kho mua chọn lọc; nếu hỏng nát nên bỏ rác sinh hoạt.',
    suggested_description: 'Một số đôi giày dép cũ gia đình cần thanh lý khi dọn kho.',
  },
  biological: {
    name_vi: 'Rác hữu cơ / Thức ăn thừa',
    material_type: 'OTHER',
    is_recyclable: false,
    icon: 'eco',
    unit: 'N/A',
    price_range: 'Không thu mua',
    recycling_tip: 'Rác hữu cơ dễ phân hủy sinh học. Thích hợp để ủ phân compost cho cây trồng, không thuộc diện thu gom phế liệu.',
    suggested_description: 'Rác hữu cơ sinh hoạt gia đình.',
  },
  trash: {
    name_vi: 'Rác thải hỗn hợp / Không tái chế',
    material_type: 'OTHER',
    is_recyclable: false,
    icon: 'delete_sweep',
    unit: 'N/A',
    price_range: 'Không thu mua',
    recycling_tip: 'Vật phẩm này không thuộc danh mục phế liệu tái chế của ReTrack. Vui lòng cho vào túi rác sinh hoạt đô thị.',
    suggested_description: 'Rác thải sinh hoạt thông thường cần dọn dẹp.',
  },
};

export const normalizeAiPredictions = (aiRes) => {
  if (!aiRes) return [];
  const rawList = Array.isArray(aiRes.top_predictions) && aiRes.top_predictions.length > 0
    ? aiRes.top_predictions
    : [{
        class_key: aiRes.primary_class,
        name_vi: aiRes.name_vi,
        confidence: aiRes.confidence,
        material_type: aiRes.material_type,
        is_recyclable: aiRes.is_recyclable,
      }];

  return rawList.slice(0, 5).map((item, idx) => {
    const meta = CLASS_METADATA_MAP[item.class_key] || {};
    const rank = idx + 1;
    return {
      rank,
      class_key: item.class_key || `class_${idx}`,
      name_vi: item.name_vi || meta.name_vi || item.class_key,
      confidence: Number(item.confidence ?? (idx === 0 ? aiRes.confidence : 0)),
      material_type: item.material_type || meta.material_type || 'OTHER',
      is_recyclable: item.is_recyclable ?? meta.is_recyclable ?? true,
      icon: item.icon || meta.icon || (idx === 0 ? aiRes.icon : 'category'),
      unit: item.unit || meta.unit || (idx === 0 ? aiRes.unit : 'kg'),
      price_range: item.price_range || (idx === 0 ? aiRes.price_range : meta.price_range) || 'Đang cập nhật',
      recycling_tip: item.recycling_tip || (idx === 0 ? aiRes.recycling_tip : meta.recycling_tip) || '',
      suggested_description: item.suggested_description || (idx === 0 ? aiRes.suggested_description : meta.suggested_description) || '',
      is_top1: idx === 0,
    };
  });
};

const NATURAL_WASTE_PHRASES = {
  cardboard: 'bìa carton, thùng giấy cũ',
  paper: 'giấy vụn, sách báo cũ',
  plastic: 'chai nhựa PET, đồ nhựa tái chế',
  metal: 'vỏ lon nhôm, sắt thép và kim loại vụn',
  battery: 'pin và ắc quy cũ đã qua sử dụng',
  'brown-glass': 'vỏ chai thủy tinh màu nâu',
  'green-glass': 'vỏ chai thủy tinh màu xanh lá',
  'white-glass': 'chai lọ thủy tinh trắng trong suốt',
  clothes: 'quần áo cũ, vải vụn gia đình',
  shoes: 'giày dép cũ thanh lý',
  biological: 'rác hữu cơ sinh hoạt',
  trash: 'rác sinh hoạt hỗn hợp',
};

function joinVietnameseList(items) {
  if (items.length === 0) return '';
  if (items.length === 1) return items[0];
  if (items.length === 2) return `${items[0]} và ${items[1]}`;
  return `${items.slice(0, -1).join(', ')} và ${items[items.length - 1]}`;
}

/**
 * Sinh nội dung mô tả đơn hàng tự nhiên, hợp lý từ góc nhìn người bán gửi cho kho vựa.
 * Tuyệt đối KHÔNG đưa các thông số kỹ thuật AI (% độ tin cậy, cân nặng giả định, giá AI khuyến nghị, công thức vật lý) vào mô tả đơn.
 */
export function buildLogicalOrderDescription({
  mode = 'single',
  wasteItem = null,
  detectedObjects = [],
  qualityCondition = 'STANDARD',
  fulfillmentMethod = 'DOORSTEP',
} = {}) {
  const isDropOff = fulfillmentMethod === 'DROP_OFF';

  if (mode === 'mixed_pile') {
    const uniquePhrases = [];
    const seen = new Set();
    for (const obj of detectedObjects || []) {
      if (obj?.is_recyclable === false) continue;
      const key = (obj?.class_key || '').toLowerCase();
      const phrase = NATURAL_WASTE_PHRASES[key] || (obj?.name_vi ? obj.name_vi.toLowerCase() : '');
      if (phrase && !seen.has(phrase)) {
        seen.add(phrase);
        uniquePhrases.push(phrase);
      }
    }

    const materialText = uniquePhrases.length > 0
      ? `gồm ${joinVietnameseList(uniquePhrases)}`
      : 'gồm nhiều loại vật liệu tái chế';

    let conditionNote = 'Hàng đã được gom tập trung tại điểm hẹn';
    if (qualityCondition === 'CLEAN_COMPACTED') {
      conditionNote = 'Hàng đã làm sạch sơ bộ, xếp gọn và giữ khô ráo';
    } else if (qualityCondition === 'DIRTY_MIXED') {
      conditionNote = 'Các loại phế liệu đang để chung chưa tách riêng từng loại';
    }

    const actionNote = isDropOff
      ? 'dự kiến mang trực tiếp qua kho vựa nhờ nhân viên phân loại và cân thực tế.'
      : 'nhờ nhân viên kho đến hỗ trợ phân loại chi tiết và cân đo thực tế khi thu gom.';

    return `Cần bán đợt phế liệu tổng hợp ${materialText}. ${conditionNote}, ${actionNote}`;
  }

  // Chế độ 1 vật thể đơn lẻ (Single Item)
  const key = (wasteItem?.class_key || '').toLowerCase();
  const meta = CLASS_METADATA_MAP[key] || {};

  if (wasteItem?.is_recyclable === false || meta.is_recyclable === false) {
    return meta.suggested_description || `Cần hỗ trợ xử lý ${wasteItem?.name_vi?.toLowerCase() || 'vật phẩm đã dọn dẹp'}.`;
  }

  if (key === 'battery') {
    return isDropOff
      ? 'Có pin và ắc quy cũ đã qua sử dụng, đã cất giữ khô ráo an toàn, dự kiến mang qua điểm thu gom của kho để bàn giao xử lý.'
      : 'Có pin và ắc quy cũ đã qua sử dụng, đã cất giữ khô ráo an toàn, nhờ nhân viên kho đến kiểm tra và thu gom đúng quy định.';
  }

  const itemPhrase = NATURAL_WASTE_PHRASES[key] || (wasteItem?.name_vi ? wasteItem.name_vi.toLowerCase() : 'phế liệu tái chế');

  let conditionPhrase = 'hàng đã gom sẵn gọn gàng và khô ráo';
  if (qualityCondition === 'CLEAN_COMPACTED') {
    conditionPhrase = 'hàng đã làm sạch, xếp ép gọn và giữ khô ráo';
  } else if (qualityCondition === 'DIRTY_MIXED') {
    conditionPhrase = 'hàng chưa kịp vệ sinh/phân loại kỹ';
  }

  const fulfillmentPhrase = isDropOff
    ? 'dự kiến mang trực tiếp qua kho vựa để cân và bàn giao.'
    : 'nhờ nhân viên kho đến hỗ trợ cân thực tế và thu gom.';

  return `Cần bán phế liệu ${itemPhrase}, ${conditionPhrase}, ${fulfillmentPhrase}`;
}

