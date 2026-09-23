const tones = {
  green: { background: "#dcfce7", color: "#166534", borderColor: "#bbf7d0" },
  amber: { background: "#fef3c7", color: "#92400e", borderColor: "#fde68a" },
  blue: { background: "#dbeafe", color: "#1d4ed8", borderColor: "#bfdbfe" },
  red: { background: "#fee2e2", color: "#b91c1c", borderColor: "#fecaca" },
  slate: { background: "#f1f5f9", color: "#475569", borderColor: "#e2e8f0" },
};

export default function Badge({ children, tone = "slate", style }) {
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        minHeight: 24,
        padding: "0 10px",
        border: "1px solid",
        borderRadius: 999,
        fontSize: 12,
        fontWeight: 800,
        lineHeight: 1,
        whiteSpace: "nowrap",
        ...tones[tone],
        ...style,
      }}
    >
      {children}
    </span>
  );
}
