import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { ROLE_LABEL } from '@/lib/utils';

export const ROLE_COLORS = {
  ADMIN: '#ba1a1a',
  SELLER: '#006c49',
  DEPOT_OWNER: '#446900',
  DEPOT_EMPLOYEE: '#a3e635',
  DRIVER: '#f59e0b',
  FACTORY: '#0ea5e9',
};

function DonutTooltip({ active, payload }) {
  if (!active || !payload?.length) return null;
  const p = payload[0];
  return (
    <div className="bg-white border border-d-border-subtle rounded-lg px-3 py-2 shadow-lg">
      <p className="text-sm font-semibold text-d-on-surface">{p.name}: {p.value}</p>
    </div>
  );
}

export default function RoleDonutChart({ usersByRole, height = 200 }) {
  const data = Object.entries(usersByRole)
    .filter(([, count]) => count > 0)
    .map(([role, count]) => ({ name: ROLE_LABEL[role] ?? role, value: count, role }));

  return (
    <div style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie data={data} dataKey="value" nameKey="name" innerRadius="58%" outerRadius="85%" paddingAngle={3} strokeWidth={0}>
            {data.map((d) => <Cell key={d.role} fill={ROLE_COLORS[d.role] ?? '#727a64'} />)}
          </Pie>
          <Tooltip content={<DonutTooltip />} />
        </PieChart>
      </ResponsiveContainer>
    </div>
  );
}
