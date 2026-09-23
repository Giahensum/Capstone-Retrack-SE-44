export default function Input({
  label,
  error,
  helperText,
  style,
  inputStyle,
  required = false,
  ...props
}) {
  return (
    <label style={{ display: "grid", gap: 7, color: "#0f172a", fontSize: 13, fontWeight: 700, ...style }}>
      {label && (
        <span>
          {label}
          {required && <span style={{ color: "#b91c1c" }}> *</span>}
        </span>
      )}
      <input
        required={required}
        style={{
          minHeight: 40,
          width: "100%",
          boxSizing: "border-box",
          border: `1px solid ${error ? "#ef4444" : "#cbd5e1"}`,
          borderRadius: 8,
          padding: "0 12px",
          color: "#0f172a",
          fontSize: 14,
          outline: "none",
          background: "#ffffff",
          ...inputStyle,
        }}
        {...props}
      />
      {(error || helperText) && (
        <span style={{ color: error ? "#b91c1c" : "#64748b", fontSize: 12, fontWeight: 600 }}>
          {error || helperText}
        </span>
      )}
    </label>
  );
}
