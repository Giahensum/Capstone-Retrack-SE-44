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
  { key: 'COPPER', label: 'Đồng', icon: '🥉' },
  { key: 'PAPER', label: 'Giấy & Báo', icon: '📰' },
];

export default function PriceForecastWidget({
  initialMaterial = 'CARDBOARD',
  weightKg = 15,
  _onApplyPrice = null,
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
      console.warn('Could not fetch real forecast:', err);
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

    const width = 500;
    const height = 140;
    const padding = { top: 22, right: 24, bottom: 28, left: 36 };

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
      <div className={`p-4 rounded-3xl border border-d-border-subtle bg-d-surface-container-lowest animate-pulse space-y-3 ${className}`}>
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
    <div className={`rounded-3xl border border-d-border-subtle bg-d-surface-container-lowest p-4 sm:p-5 space-y-4 shadow-xs transition-all overflow-hidden ${className}`}>
      {/* ── Header: Title & AI Econometrics Badge ── */}
      <div className="space-y-2.5 pb-3 border-b border-d-border-subtle">
        <div className="flex items-start gap-2.5">
          <div className="w-8 h-8 rounded-xl bg-d-primary/10 text-d-primary flex items-center justify-center shrink-0 mt-0.5">
            <TrendingUp size={16} />
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-1.5 flex-wrap">
              <h4 className="font-bold text-d-on-surface text-sm leading-snug">
                Dự báo xu hướng giá & Thời điểm bán
              </h4>
              <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 font-bold shrink-0">
                AI Econometrics
              </span>
            </div>
            <p className="text-[11px] text-d-on-surface-variant leading-relaxed mt-0.5">
              Chuỗi thời gian 14 ngày kết hợp lực cầu nhà máy và giá kho vựa
            </p>
          </div>
        </div>

        {/* Quick Material Switcher Chips (Horizontal Scrollable) */}
        <div className="flex items-center gap-1.5 overflow-x-auto no-scrollbar py-0.5 -mx-1 px-1">
          {QUICK_MATERIALS.map((m) => (
            <button
              key={m.key}
              type="button"
              onClick={() => setSelectedMaterial(m.key)}
              className={`px-2.5 py-1 rounded-xl text-xs font-semibold flex items-center gap-1 transition-all cursor-pointer whitespace-nowrap shrink-0 ${
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
        className={`p-3.5 sm:p-4 rounded-2xl border transition-all space-y-2.5 ${
          isHold
            ? 'bg-gradient-to-r from-amber-500/10 via-amber-500/5 to-transparent border-amber-500/30'
            : isSellNow
            ? 'bg-gradient-to-r from-emerald-500/10 via-emerald-500/5 to-transparent border-emerald-500/30'
            : 'bg-gradient-to-r from-blue-500/10 via-blue-500/5 to-transparent border-blue-500/30'
        }`}
      >
        <div className="flex items-center gap-1.5 flex-wrap">
          <span
            className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold uppercase tracking-wider ${
              isHold
                ? 'bg-amber-500/20 text-amber-800 dark:text-amber-300'
                : isSellNow
                ? 'bg-emerald-600/20 text-emerald-800 dark:text-emerald-300'
                : 'bg-blue-500/20 text-blue-800 dark:text-blue-300'
            }`}
          >
            {isHold && '📦 Gom thêm & Giữ hàng'}
            {isSellNow && '🚀 Thời điểm vàng — Bán ngay'}
            {!isHold && !isSellNow && '⚖️ Giá ổn định — Bán linh hoạt'}
          </span>

          {forecast.estimated_profit_increase_pct > 0 && (
            <span className="text-[11px] font-bold px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-700 dark:text-emerald-300 shrink-0">
              +{forecast.estimated_profit_increase_pct}% lợi nhuận
            </span>
          )}
        </div>

        <h5 className="font-bold text-d-on-surface text-sm leading-snug">
          {forecast.recommendation_title}
        </h5>

        <p className="text-xs text-d-on-surface-variant leading-relaxed">
          {forecast.recommendation_detail}
        </p>

        {/* Optimal Time Window Pill */}
        <div className="flex items-center justify-between p-2.5 rounded-xl bg-d-surface-container-lowest/90 border border-d-border-subtle flex-wrap gap-1.5">
          <span className="text-[11px] font-semibold text-d-on-surface-variant flex items-center gap-1">
            <Clock size={13} className="text-d-primary" />
            <span>Khung thời gian tối ưu:</span>
          </span>
          <span className="text-xs font-bold text-d-primary font-mono">
            {forecast.optimal_time_window}
          </span>
        </div>
      </div>

      {/* ── Interactive Trend Chart (SVG) ── */}
      <div className="space-y-2">
        <div className="flex items-center justify-between text-xs font-semibold text-d-on-surface-variant px-0.5 flex-wrap gap-1.5">
          <div className="flex items-center gap-3 text-[11px]">
            <span className="flex items-center gap-1">
              <span className="w-2.5 h-0.5 bg-d-on-surface-variant/70 rounded-full inline-block" />
              <span>7 ngày trước</span>
            </span>
            <span className="flex items-center gap-1">
              <span className="w-2.5 h-0.5 border-t-2 border-dashed border-emerald-500 inline-block" />
              <span>7 ngày tới (Dự phóng)</span>
            </span>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="text-d-on-surface font-bold text-xs">
              {forecast.current_price?.toLocaleString('vi-VN')} đ/{forecast.unit}
            </span>
            <span
              className={`font-mono font-bold flex items-center text-xs ${
                isUpward
                  ? 'text-emerald-600 dark:text-emerald-400'
                  : isDownward
                  ? 'text-rose-600 dark:text-rose-400'
                  : 'text-d-on-surface-variant'
              }`}
            >
              {isUpward ? <TrendingUp size={12} /> : isDownward ? <TrendingDown size={12} /> : <Minus size={12} />}
              {forecast.projected_change_pct > 0 ? `+${forecast.projected_change_pct}` : forecast.projected_change_pct}%
            </span>
          </div>
        </div>

        {chartData && (
          <div className="relative rounded-2xl bg-d-surface-container-low/60 p-2 border border-d-border-subtle overflow-hidden">
            <svg
              viewBox={`0 0 ${chartData.width} ${chartData.height}`}
              className="w-full h-32 sm:h-36 overflow-hidden select-none"
            >
              <defs>
                <linearGradient id="forecastAreaGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#10b981" stopOpacity="0.25" />
                  <stop offset="100%" stopColor="#10b981" stopOpacity="0.02" />
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

              {/* Clean Non-Overlapping X-Axis Labels: Start, Today, Peak, End */}
              {(() => {
                const total = chartData.coords.length;
                const todayIdx = chartData.coords.findIndex((c) => !c.is_forecast && c.day_label?.includes('Hôm nay'));
                const peakIdx = chartData.peakPoint ? chartData.coords.findIndex((c) => c.date === chartData.peakPoint.date) : -1;
                
                // Key indices to show
                const displayIndices = new Set([0, todayIdx, total - 1]);
                if (peakIdx >= 0) displayIndices.add(peakIdx);

                return Array.from(displayIndices).map((idx) => {
                  const p = chartData.coords[idx];
                  if (!p) return null;
                  const isToday = idx === todayIdx;
                  const isPeak = idx === peakIdx;

                  return (
                    <text
                      key={`axis-${p.date || idx}`}
                      x={p.x}
                      y={chartData.height - 8}
                      textAnchor="middle"
                      fontSize="9"
                      fill="currentColor"
                      opacity={isToday ? 1 : 0.65}
                      fontWeight={isToday || isPeak ? 'bold' : 'normal'}
                      className="font-sans select-none"
                    >
                      {isToday ? 'Hôm nay' : isPeak ? '★ Đỉnh' : p.day_label?.split(' ')[1]?.replace(/[()]/g, '') || p.day_label}
                    </text>
                  );
                });
              })()}

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
                  </g>
                );
              })}
            </svg>

            {/* Hover Tooltip Overlay */}
            {hoveredPoint && (
              <div
                className="absolute z-20 pointer-events-none px-3 py-1.5 rounded-xl bg-d-on-surface text-d-surface text-[11px] font-bold shadow-lg -translate-x-1/2 -translate-y-full transition-all whitespace-nowrap"
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

      {/* ── Market Intelligence Metrics List (Never Squished) ── */}
      <div className="space-y-2 pt-1">
        <div className="p-2.5 rounded-xl bg-d-surface-container-low border border-d-border-subtle flex items-center justify-between gap-2.5">
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-7 h-7 rounded-lg bg-blue-500/15 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0">
              <Factory size={14} />
            </div>
            <span className="text-xs text-d-on-surface-variant font-medium truncate">
              Lực cầu từ nhà máy
            </span>
          </div>
          <span className="font-bold text-xs text-d-on-surface shrink-0 font-mono">
            {forecast.factory_demand_weight_kg > 0
              ? `${(forecast.factory_demand_weight_kg / 1000).toFixed(1)} tấn đang mở`
              : 'Thị trường cân bằng'}
          </span>
        </div>

        <div className="p-2.5 rounded-xl bg-d-surface-container-low border border-d-border-subtle flex items-center justify-between gap-2.5">
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-7 h-7 rounded-lg bg-purple-500/15 text-purple-600 dark:text-purple-400 flex items-center justify-center shrink-0">
              <Store size={14} />
            </div>
            <span className="text-xs text-d-on-surface-variant font-medium truncate">
              Kho vựa thu mua
            </span>
          </div>
          <span className="font-bold text-xs text-d-on-surface shrink-0">
            {forecast.active_depots_buying > 0
              ? `${forecast.active_depots_buying} kho vựa niêm yết`
              : 'Theo bảng giá chuẩn'}
          </span>
        </div>

        <div className="p-2.5 rounded-xl bg-d-surface-container-low border border-d-border-subtle flex items-center justify-between gap-2.5">
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-7 h-7 rounded-lg bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
              <Coins size={14} />
            </div>
            <span className="text-xs text-d-on-surface-variant font-medium truncate">
              Dự phóng giá 7 ngày
            </span>
          </div>
          <span className="font-bold text-xs text-emerald-600 dark:text-emerald-400 shrink-0 font-mono">
            ~{forecast.projected_price_7d?.toLocaleString('vi-VN')} đ/{forecast.unit}
          </span>
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
            <span>Xem chi tiết bảng giá 14 ngày</span>
          </span>
          {showTable ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
        </button>

        {showTable && (
          <div className="mt-2.5 overflow-x-auto rounded-xl border border-d-border-subtle">
            <table className="w-full text-left text-[11px]">
              <thead className="bg-d-surface-container text-d-on-surface-variant uppercase text-[9px] font-bold">
                <tr>
                  <th className="px-2.5 py-1.5">Ngày</th>
                  <th className="px-2 py-1.5">Loại</th>
                  <th className="px-2.5 py-1.5 text-right">Đơn giá</th>
                  <th className="px-2.5 py-1.5 text-right">Biên độ</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-d-border-subtle bg-d-surface-container-lowest">
                {forecast.timeline.map((row) => (
                  <tr
                    key={row.date}
                    className={`hover:bg-d-surface-container-low transition-colors ${
                      row.day_label?.includes('Hôm nay') ? 'bg-blue-500/10 font-bold' : ''
                    }`}
                  >
                    <td className="px-2.5 py-1.5 text-d-on-surface whitespace-nowrap">
                      {row.day_label}
                    </td>
                    <td className="px-2 py-1.5">
                      <span
                        className={`text-[9px] font-semibold px-1.5 py-0.2 rounded-full ${
                          row.is_forecast
                            ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300'
                            : 'bg-d-surface-container text-d-on-surface-variant'
                        }`}
                      >
                        {row.is_forecast ? 'Dự báo' : 'Thực'}
                      </span>
                    </td>
                    <td className="px-2.5 py-1.5 text-right font-mono font-bold text-d-on-surface whitespace-nowrap">
                      {row.price.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="px-2.5 py-1.5 text-right text-[10px] text-d-on-surface-variant font-mono whitespace-nowrap">
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
