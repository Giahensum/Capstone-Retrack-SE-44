export default function Card({ children, title, action, style, bodyStyle, ...props }) {
  return (
    <section
      style={{
        background: "#ffffff",
        border: "1px solid #e2e8f0",
        borderRadius: 8,
        boxShadow: "0 1px 2px rgba(15, 23, 42, 0.05)",
        overflow: "hidden",
        ...style,
      }}
      {...props}
    >
      {(title || action) && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 16,
            padding: "16px 18px",
            borderBottom: "1px solid #e2e8f0",
          }}
        >
          {title && (
            <h2 style={{ margin: 0, color: "#0f172a", fontSize: 16, fontWeight: 800 }}>
              {title}
            </h2>
          )}
          {action}
        </div>
      )}
      <div style={{ padding: 18, ...bodyStyle }}>{children}</div>
    </section>
  );
}
