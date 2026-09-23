const variants = {
  primary: {
    background: "#166534",
    borderColor: "#166534",
    color: "#ffffff",
  },
  secondary: {
    background: "#f8fafc",
    borderColor: "#cbd5e1",
    color: "#0f172a",
  },
  ghost: {
    background: "transparent",
    borderColor: "transparent",
    color: "#166534",
  },
  danger: {
    background: "#b91c1c",
    borderColor: "#b91c1c",
    color: "#ffffff",
  },
};

const sizes = {
  sm: { minHeight: 34, padding: "0 12px", fontSize: 13 },
  md: { minHeight: 40, padding: "0 16px", fontSize: 14 },
  lg: { minHeight: 46, padding: "0 20px", fontSize: 15 },
};

export default function Button({
  children,
  variant = "primary",
  size = "md",
  type = "button",
  disabled = false,
  style,
  ...props
}) {
  return (
    <button
      type={type}
      disabled={disabled}
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        gap: 8,
        border: "1px solid",
        borderRadius: 8,
        cursor: disabled ? "not-allowed" : "pointer",
        fontWeight: 700,
        opacity: disabled ? 0.55 : 1,
        transition: "background 160ms ease, border-color 160ms ease, transform 160ms ease",
        whiteSpace: "nowrap",
        ...variants[variant],
        ...sizes[size],
        ...style,
      }}
      {...props}
    >
      {children}
    </button>
  );
}
