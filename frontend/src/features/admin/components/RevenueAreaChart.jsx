import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { formatCurrency } from '@/lib/utils';

function ChartTooltip({ active, payload, label }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="bg-white border border-d-border-subtle rounded-lg px-3 py-2 shadow-lg">
      <p className="text-xs text-d-on-surface-variant mb-1">{label}</p>
      <p className="text-sm font-semibold text-d-secondary">{formatCurrency(payload[0].value)}</p>
    </div>
  );
}

const tickFormatter = (v) => (v >= 1_000_000 ? `${(v / 1_000_000).toFixed(1)}tr` : v >= 1000 ? `${Math.round(v / 1000)}k` : v);

export default function RevenueAreaChart({ points, height = 240 }) {
  const data = points.map((p) => ({ label: p.periodLabel, amount: p.amount }));

  return (
    <div style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <defs>
            <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#006c49" stopOpacity={0.28} />
              <stop offset="100%" stopColor="#006c49" stopOpacity={0.02} />
            </linearGradient>
          </defs>
          <CartesianGrid strokeDasharray="3 3" stroke="#e5f1e7" vertical={false} />
          <XAxis dataKey="label" tick={{ fontSize: 11, fill: '#727a64' }} axisLine={{ stroke: '#dae5dc' }} tickLine={false} />
          <YAxis tick={{ fontSize: 11, fill: '#727a64' }} axisLine={false} tickLine={false} width={56} tickFormatter={tickFormatter} />
          <Tooltip content={<ChartTooltip />} cursor={{ stroke: '#c2cab0', strokeWidth: 1 }} />
          <Area type="monotone" dataKey="amount" stroke="#006c49" strokeWidth={2.5} fill="url(#revenueFill)" />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
