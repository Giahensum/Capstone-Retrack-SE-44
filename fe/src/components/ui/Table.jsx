export default function Table({ columns = [], data = [], emptyText = "No data", getRowKey }) {
  return (
    <div style={{ width: "100%", overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", minWidth: 680 }}>
        <thead>
          <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
            {columns.map((column) => (
              <th
                key={column.key}
                style={{
                  padding: "12px 14px",
                  textAlign: column.align || "left",
                  color: "#475569",
                  fontSize: 12,
                  fontWeight: 800,
                  textTransform: "uppercase",
                  letterSpacing: 0,
                  whiteSpace: "nowrap",
                }}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.length === 0 ? (
            <tr>
              <td colSpan={columns.length || 1} style={{ padding: 24, textAlign: "center", color: "#64748b" }}>
                {emptyText}
              </td>
            </tr>
          ) : (
            data.map((row, index) => (
              <tr
                key={getRowKey ? getRowKey(row) : row.id || index}
                style={{ borderBottom: "1px solid #e2e8f0" }}
              >
                {columns.map((column) => (
                  <td
                    key={column.key}
                    style={{
                      padding: "13px 14px",
                      color: "#0f172a",
                      fontSize: 14,
                      textAlign: column.align || "left",
                      verticalAlign: "middle",
                    }}
                  >
                    {column.render ? column.render(row) : row[column.key]}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
