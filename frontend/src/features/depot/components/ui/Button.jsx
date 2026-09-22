import React from 'react';
import clsx from 'clsx';

export const Button = React.forwardRef(({ className, variant = 'default', size = 'default', children, ...props }, ref) => {
  const variants = {
    default: "bg-d-primary-fixed text-d-on-primary-fixed hover:bg-d-primary-container shadow-sm",
    secondary: "bg-d-surface-accent text-d-secondary border border-d-secondary/10 hover:bg-d-secondary hover:text-white",
    outline: "border border-d-border-subtle text-d-on-surface-variant hover:bg-d-surface-container",
    ghost: "text-d-on-surface-variant hover:bg-d-surface-container",
    danger: "bg-d-error-container text-d-error hover:bg-d-error/20"
  };

  const sizes = {
    default: "py-2.5 px-6 font-d-label-md",
    sm: "py-1.5 px-4 font-d-label-sm",
    icon: "p-2"
  };

  return (
    <button
      ref={ref}
      className={clsx(
        "rounded-full transition-all duration-200 flex items-center justify-center gap-2 active:opacity-80",
        variants[variant],
        sizes[size],
        "disabled:opacity-50 disabled:cursor-not-allowed disabled:transform-none disabled:shadow-none",
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
});

Button.displayName = "Button";
