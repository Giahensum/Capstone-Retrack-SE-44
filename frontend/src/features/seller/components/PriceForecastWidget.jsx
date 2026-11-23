import React, { useState, useEffect, useMemo } from 'react';
import {
  TrendingUp,
  TrendingDown,
  Minus,
  Calendar,
  Factory,
  Store,
  ChevronDown,
  ChevronUp,
  Clock,
  Coins,
} from 'lucide-react';
import { sellerApi } from '../api';

const QUICK_MATERIALS = [
  { key: 'CARDBOARD', label: 'Bìa Carton', icon: '📦' },
  { key: 'PET', label: 'Chai nhựa PET', icon: '🧴' },
  { key: 'ALUMINUM', label: 'Lon nhôm', icon: '🥫' },
  { key: 'IRON', label: 'Sắt phế liệu', icon: '🔩' },
  { key: 'COPPER', label: 'Đồng đỏ/vàng', icon: '🥉' },
  { key: 'PAPER', label: 'Giấy & Báo vụn', icon: '📰' },
];

export default function PriceForecastWidget({
  initialMaterial = 'CARDBOARD',
  weightKg = 15,
  onApplyPrice = null,
  className = '',
}) {
  const [selectedMaterial, setSelectedMaterial] = useState(initialMaterial);
  const [forecast, setForecast] = useState(null);
  const [loading, setLoading] = useState(true);
  const [showTable, setShowTable] = useState(false);
  const [hoveredPoint, setHoveredPoint] = useState(null);

  // Sync selected material when prop changes
  useEffect(() => {
    if (initialMaterial) {
      setSelectedMaterial(initialMaterial);
    }
  }, [initialMaterial]);

  // Fetch forecast data from API
  const fetchForecast = async (mat, weight) => {
    setLoading(true);
    try {
      const data = await sellerApi.getPriceForecast(mat, weight);
      if (data) {
        setForecast(data);
      }
    } catch (err) {
      console.warn('Could not fetch real forecast, generating client fallback:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchForecast(selectedMaterial, weightKg);
  }, [selectedMaterial, weightKg]);

  // SVG Chart Calculations
  const chartData = useMemo(() => {
    if (!forecast || !forecast.timeline || forecast.timeline.length === 0) return null;

    const points = forecast.timeline;
    const minPrice = Math.min(...points.map((p) => p.min_price || p.price)) * 0.98;
    const maxPrice = Math.max(...points.map((p) => p.max_price || p.price)) * 1.02;
    const priceRange = maxPrice - minPrice || 1;

    const width = 640;
    const height = 150;
    const padding = { top: 20, right: 30, bottom: 25, left: 45 };

    const plotWidth = width - padding.left - padding.right;
    const plotHeight = height - padding.top - padding.bottom;

    const coords = points.map((p, idx) => {
      const x = padding.left + (idx / (points.length - 1)) * plotWidth;
      const y = padding.top + plotHeight - ((p.price - minPrice) / priceRange) * plotHeight;
      const yMin = padding.top + plotHeight - (((p.min_price || p.price) - minPrice) / priceRange) * plotHeight;
      const yMax = padding.top + plotHeight - (((p.max_price || p.price) - minPrice) / priceRange) * plotHeight;
      return { ...p, x, y, yMin, yMax, idx };
    });

    // Split historical and forecast paths
    const todayIndex = coords.findIndex((c) => !c.is_forecast && c.day_label?.includes('Hôm nay')) || 7;
    const histCoords = coords.slice(0, todayIndex + 1);
    const futureCoords = coords.slice(todayIndex);

    const makePath = (items) => items.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');

    const histPath = makePath(histCoords);
    const futurePath = makePath(futureCoords);

    // Confidence area for future
    const areaUpper = futureCoords.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.yMax.toFixed(1)}`).join(' ');
    const areaLower = [...futureCoords].reverse().map((p) => `L ${p.x.toFixed(1)} ${p.yMin.toFixed(1)}`).join(' ');
    const confidenceAreaPath = `${areaUpper} ${areaLower} Z`;

    // Peak point in future
    const futureOnly = coords.filter((c) => c.is_forecast);
    const peakPoint = futureOnly.reduce((max, c) => (c.price > (max?.price || 0) ? c : max), null);

    return {
      width,
      height,
      padding,
      coords,
      histPath,
      futurePath,
      confidenceAreaPath,
      todayCoord: coords[todayIndex] || coords[0],
      peakPoint,
      minPrice,
      maxPrice,
    };
  }, [forecast]);

  if (!forecast && loading) {
    return (
      <div className={`p-5 rounded-3xl border border-d-border-subtle bg-d-surface-container-lowest animate-pulse space-y-3 ${className}`}>
        <div className="h-5 w-48 bg-d-surface-container rounded-lg" />
        <div className="h-28 bg-d-surface-container-low rounded-2xl" />
      </div>
    );
  }

  if (!forecast) return null;

  const isUpward = forecast.trend === 'UPWARD';
  const isDownward = forecast.trend === 'DOWNWARD';
  const isHold = forecast.recommendation_code === 'HOLD_ACCUMULATE';
  const isSellNow = forecast.recommendation_code === 'SELL_NOW';

  return (
    <div className={`rounded-3xl border border-d-border-subtle bg-d-surface-container-lowest p-5 space-y-5 shadow-xs transition-all ${className}`}>
      {/* ── Header & Material Switcher ── */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-d-border-subtle">
        <div className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-d-primary/10 text-d-primary flex items-center justify-center shrink-0">
            <TrendingUp size={18} />
          </div>
          <div>
            <h4 className="font-bold text-d-on-surface text-sm flex items-center gap-1.5">
              <span>Dự báo xu hướng giá & Thời điểm bán tối ưu</span>
              <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 font-bold">
                AI Econometrics
              </span>
            </h4>
            <p className="text-[11px] text-d-on-surface-variant">
              Phân tích chuỗi thời gian 14 ngày kết hợp lực cầu nhà máy và giá kho vựa
            </p>
          </div>
        </div>

        {/* Quick Material Switcher Chips */}
        <div className="flex flex-wrap items-center gap-1.5">
          {QUICK_MATERIALS.map((m) => (
            <button
              key={m.key}
              type="button"
              onClick={() => setSelectedMaterial(m.key)}
              className={`px-2.5 py-1 rounded-xl text-xs font-semibold flex items-center gap-1 transition-all cursor-pointer ${
                selectedMaterial === m.key
                  ? 'bg-d-primary text-d-on-primary shadow-xs font-bold'
                  : 'bg-d-surface-container-low text-d-on-surface-variant hover:bg-d-surface-container hover:text-d-on-surface'
              }`}
            >
              <span>{m.icon}</span>
              <span>{m.label}</span>
            </button>
          ))}
        </div>
      </div>

      {/* ── Key Decision Recommendation Banner ── */}
      <div
        className={`p-4 rounded-2xl border transition-all ${
          isHold
            ? 'bg-gradient-to-r from-amber-500/10 via-amber-500/5 to-transparent border-amber-500/30'
            : isSellNow
            ? 'bg-gradient-to-r from-emerald-500/10 via-emerald-500/5 to-transparent border-emerald-500/30'
            : 'bg-gradient-to-r from-blue-500/10 via-blue-500/5 to-transparent border-blue-500/30'
        }`}
      >
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span
                className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold uppercase tracking-wider ${
                  isHold
                    ? 'bg-amber-500/20 text-amber-800 dark:text-amber-300'
                    : isSellNow
                    ? 'bg-emerald-600/20 text-emerald-800 dark:text-emerald-300'
                    : 'bg-blue-500/20 text-blue-800 dark:text-blue-300'
                }`}
              >
                {isHold && '📦 Khuyến nghị: Gom thêm & Giữ hàng'}
                {isSellNow && '🚀 Khuyến nghị: Thời điểm vàng — Bán ngay'}
                {!isHold && !isSellNow && '⚖️ Khuyến nghị: Giá ổn định — Bán linh hoạt'}
              </span>

              {forecast.estimated_profit_increase_pct > 0 && (
                <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-700 dark:text-emerald-300">
                  +{forecast.estimated_profit_increase_pct}% lợi nhuận dự kiến
                </span>
              )}
            </div>

            <h5 className="font-bold text-d-on-surface text-sm sm:text-base">
              {forecast.recommendation_title}
            </h5>
            <p className="text-xs text-d-on-surface-variant leading-relaxed max-w-2xl">
              {forecast.recommendation_detail}
            </p>
          </div>

          <div className="shrink-0 bg-d-surface-container-lowest/80 backdrop-blur-xs p-3 rounded-xl border border-d-border-subtle text-right sm:text-center min-w-[150px]">
            <span className="text-[10px] font-bold text-d-on-surface-variant block uppercase tracking-wider">
              Khung thời gian tối ưu
            </span>
            <div className="text-xs sm:text-sm font-bold text-d-primary flex items-center justify-end sm:justify-center gap-1 mt-0.5">
              <Clock size={13} />
              <span>{forecast.optimal_time_window}</span>
            </div>
            {onApplyPrice && (
              <button
                type="button"
                onClick={() => onApplyPrice(forecast.current_price)}
                className="mt-2 text-[11px] font-bold text-d-primary hover:underline block w-full text-center"
              >
                Áp dụng mức giá này →
              </button>
            )}
          </div>
        </div>
      </div>

      {/* ── Interactive Trend Chart (SVG) ── */}
      <div className="space-y-2">
        <div className="flex items-center justify-between text-xs font-semibold text-d-on-surface-variant px-1">
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1.5">
              <span className="w-3 h-0.5 bg-d-on-surface-variant/70 rounded-full inline-block" />
              <span>7 ngày qua (Thực tế)</span>
            </span>
            <span className="flex items-center gap-1.5">
              <span className="w-3 h-0.5 border-t-2 border-dashed border-d-primary inline-block" />
              <span>7 ngày tới (Dự phóng AI)</span>
            </span>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-d-on-surface font-bold">
              Hiện tại: {forecast.current_price?.toLocaleString('vi-VN')} đ/{forecast.unit}
            </span>
            <span
              className={`font-mono font-bold flex items-center gap-0.5 text-xs ${
                isUpward
                  ? 'text-emerald-600 dark:text-emerald-400'
                  : isDownward
                  ? 'text-rose-600 dark:text-rose-400'
                  : 'text-d-on-surface-variant'
              }`}
            >
              {isUpward ? <TrendingUp size={13} /> : isDownward ? <TrendingDown size={13} /> : <Minus size={13} />}
              {forecast.projected_change_pct > 0 ? `+${forecast.projected_change_pct}` : forecast.projected_change_pct}%
            </span>
          </div>
        </div>

        {chartData && (
          <div className="relative rounded-2xl bg-d-surface-container-low/60 p-2 border border-d-border-subtle overflow-hidden">
            <svg
              viewBox={`0 0 ${chartData.width} ${chartData.height}`}
              className="w-full h-36 sm:h-44 overflow-visible select-none"
            >
              <defs>
                <linearGradient id="forecastAreaGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#10b981" stopOpacity="0.25" />
                  <stop offset="100%" stopColor="#10b981" stopOpacity="0.02" />
                </linearGradient>
                <linearGradient id="todayGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#22c55e" stopOpacity="0.4" />
                  <stop offset="100%" stopColor="#22c55e" stopOpacity="0" />
                </linearGradient>
              </defs>

              {/* Grid Lines */}
              {[0.25, 0.5, 0.75].map((ratio) => {
                const y = chartData.padding.top + (chartData.height - chartData.padding.top - chartData.padding.bottom) * ratio;
                return (
                  <line
                    key={ratio}
                    x1={chartData.padding.left}
                    y1={y}
                    x2={chartData.width - chartData.padding.right}
                    y2={y}
                    stroke="currentColor"
                    strokeOpacity="0.08"
                    strokeDasharray="4 4"
                  />
                );
              })}

              {/* Today vertical divider line */}
              {chartData.todayCoord && (
                <line
                  x1={chartData.todayCoord.x}
                  y1={chartData.padding.top}
                  x2={chartData.todayCoord.x}
                  y2={chartData.height - chartData.padding.bottom}
                  stroke="#3b82f6"
                  strokeWidth="1.5"
                  strokeDasharray="3 3"
                  opacity="0.6"
                />
              )}

              {/* Confidence Band (Min-Max Shadow) */}
              <path d={chartData.confidenceAreaPath} fill="url(#forecastAreaGrad)" />

              {/* Historical Line (Solid) */}
              <path
                d={chartData.histPath}
                fill="none"
                stroke="#64748b"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />

              {/* Forecast Line (Dashed Emerald/Primary) */}
              <path
                d={chartData.futurePath}
                fill="none"
                stroke="#10b981"
                strokeWidth="2.5"
                strokeDasharray="5 4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />

              {/* Points & Interactive Tooltips */}
              {chartData.coords.map((p, idx) => {
                const isToday = !p.is_forecast && p.day_label?.includes('Hôm nay');
                const isPeak = chartData.peakPoint && chartData.peakPoint.date === p.date;

                return (
                  <g
                    key={p.date || idx}
                    className="cursor-pointer group"
                    onMouseEnter={() => setHoveredPoint(p)}
                    onMouseLeave={() => setHoveredPoint(null)}
                  >
                    {/* Hover hotspot */}
                    <circle cx={p.x} cy={p.y} r="10" fill="transparent" />

                    {/* Point Circle */}
                    <circle
                      cx={p.x}
                      cy={p.y}
                      r={isToday || isPeak ? 4.5 : 3}
                      fill={isToday ? '#2563eb' : isPeak ? '#f59e0b' : p.is_forecast ? '#10b981' : '#64748b'}
                      stroke="#ffffff"
                      strokeWidth="1.5"
                      className="transition-transform group-hover:scale-125"
                    />

                    {/* Peak Marker Badge */}
                    {isPeak && (
                      <g transform={`translate(${p.x}, ${p.y - 14})`}>
                        <text
                          textAnchor="middle"
                          fontSize="9"
                          fontWeight="bold"
                          fill="#d97706"
                          className="font-sans select-none"
                        >
                          ★ Đỉnh
                        </text>
                      </g>
                    )}

                    {/* X-Axis Day Label (select every 2nd or key days) */}
                    {(idx % 2 === 0 || isToday || isPeak) && (
                      <text
                        x={p.x}
                        y={chartData.height - 8}
                        textAnchor="middle"
                        fontSize="9"
                        fill="currentColor"
                        opacity={isToday ? 1 : 0.6}
                        fontWeight={isToday ? 'bold' : 'normal'}
                      >
                        {isToday ? 'Hôm nay' : p.day_label?.split(' ')[0]}
                      </text>
                    )}
                  </g>
                );
              })}
            </svg>

            {/* Hover Tooltip Overlay */}
            {hoveredPoint && (
              <div
                className="absolute z-20 pointer-events-none px-3 py-1.5 rounded-xl bg-d-on-surface text-d-surface text-[11px] font-bold shadow-lg -translate-x-1/2 -translate-y-full transition-all"
                style={{
                  left: `${(hoveredPoint.x / chartData.width) * 100}%`,
                  top: `${(hoveredPoint.y / chartData.height) * 100 - 8}%`,
                }}
              >
                <div>{hoveredPoint.day_label}</div>
                <div className="text-emerald-400 font-mono text-xs">
                  {hoveredPoint.price.toLocaleString('vi-VN')} đ/{forecast.unit}
                </div>
                {hoveredPoint.is_forecast && (
                  <div className="text-[9px] opacity-80 font-normal">
                    Biên độ: {hoveredPoint.min_price?.toLocaleString('vi-VN')} - {hoveredPoint.max_price?.toLocaleString('vi-VN')} đ
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      {/* ── Market Intelligence Metrics Grid ── */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-1">
        <div className="p-3.5 rounded-2xl bg-d-surface-container-low border border-d-border-subtle flex items-center gap-3">
          <div className="w-8 h-8 rounded-xl bg-blue-500/15 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0">
            <Factory size={16} />
          </div>
          <div className="min-w-0">
            <span className="text-[10px] text-d-on-surface-variant font-semibold block uppercase tracking-wider">
              Lực cầu từ nhà máy
            </span>
            <span className="font-bold text-xs sm:text-sm text-d-on-surface truncate block">
              {forecast.factory_demand_weight_kg > 0
                ? `${(forecast.factory_demand_weight_kg / 1000).toFixed(1)} tấn đang mở`
                : 'Thị trường cân bằng'}
            </span>
          </div>
        </div>

        <div className="p-3.5 rounded-2xl bg-d-surface-container-low border border-d-border-subtle flex items-center gap-3">
          <div className="w-8 h-8 rounded-xl bg-purple-500/15 text-purple-600 dark:text-purple-400 flex items-center justify-center shrink-0">
            <Store size={16} />
          </div>
          <div className="min-w-0">
            <span className="text-[10px] text-d-on-surface-variant font-semibold block uppercase tracking-wider">
              Kho vựa thu mua
            </span>
            <span className="font-bold text-xs sm:text-sm text-d-on-surface truncate block">
              {forecast.active_depots_buying > 0
                ? `${forecast.active_depots_buying} kho vựa niêm yết`
                : 'Theo bảng giá chuẩn'}
            </span>
          </div>
        </div>

        <div className="p-3.5 rounded-2xl bg-d-surface-container-low border border-d-border-subtle flex items-center gap-3">
          <div className="w-8 h-8 rounded-xl bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
            <Coins size={16} />
          </div>
          <div className="min-w-0">
            <span className="text-[10px] text-d-on-surface-variant font-semibold block uppercase tracking-wider">
              Dự phóng giá 7 ngày
            </span>
            <span className="font-bold text-xs sm:text-sm text-d-on-surface truncate block">
              ~{forecast.projected_price_7d?.toLocaleString('vi-VN')} đ/{forecast.unit}
            </span>
          </div>
        </div>
      </div>

      {/* ── Collapsible Detailed Table ── */}
      <div className="pt-2 border-t border-d-border-subtle">
        <button
          type="button"
          onClick={() => setShowTable(!showTable)}
          className="w-full flex items-center justify-between text-xs font-bold text-d-on-surface-variant hover:text-d-primary transition-colors cursor-pointer py-1"
        >
          <span className="flex items-center gap-1.5">
            <Calendar size={13} />
            <span>Xem chi tiết bảng giá 14 ngày (Quá khứ & Dự phóng)</span>
          </span>
          {showTable ? <ChevronUp size={15} /> : <ChevronDown size={15} />}
        </button>

        {showTable && (
          <div className="mt-3 overflow-x-auto rounded-2xl border border-d-border-subtle">
            <table className="w-full text-left text-xs">
              <thead className="bg-d-surface-container text-d-on-surface-variant uppercase text-[10px] font-bold">
                <tr>
                  <th className="px-3 py-2">Thời gian</th>
                  <th className="px-3 py-2">Trạng thái</th>
                  <th className="px-3 py-2 text-right">Đơn giá ({forecast.unit})</th>
                  <th className="px-3 py-2 text-right">Biên độ tin cậy</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle bg-d-surface-container-lowest">
                {forecast.timeline.map((row) => (
                  <tr
                    key={row.date}
                    className={`hover:bg-d-surface-container-low transition-colors ${
                      row.day_label?.includes('Hôm nay') ? 'bg-blue-500/5 font-bold' : ''
                    }`}
                  >
                    <td className="px-3 py-2 text-d-on-surface">
                      {row.day_label}
                      {row.day_label?.includes('Hôm nay') && (
                        <span className="ml-1.5 text-[9px] px-1.5 py-0.2 rounded-md bg-blue-500/20 text-blue-700 dark:text-blue-300 font-bold">
                          Hiện tại
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-2">
                      <span
                        className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                          row.is_forecast
                            ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
                            : 'bg-d-surface-container text-d-on-surface-variant'
                        }`}
                      >
                        {row.is_forecast ? 'Dự phóng AI' : 'Thực tế'}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right font-mono font-bold text-d-on-surface">
                      {row.price.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="px-3 py-2 text-right text-[11px] text-d-on-surface-variant font-mono">
                      {row.is_forecast ? `${row.min_price?.toLocaleString('vi-VN')} - ${row.max_price?.toLocaleString('vi-VN')} đ` : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
