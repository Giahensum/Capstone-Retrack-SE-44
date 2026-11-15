import React from 'react';
import clsx from 'clsx';

export const Badge = ({ className, variant = 'default', children, showDot = true, ...props }) => {
  const variants = {
    default: "text-d-secondary bg-d-surface-accent border border-d-secondary/10",
    warning: "text-[#b45309] bg-[#fef3c7] border border-[#b45309]/10", // Using hardcoded colors for warning from HTML
    error: "text-d-error bg-d-error-container border border-d-error/10",
    neutral: "text-d-on-surface-variant bg-d-surface-container border border-d-border-subtle"
  };

  const dotColors = {
    default: "bg-d-secondary",
    warning: "bg-[#b45309]",
    error: "bg-d-error",
    neutral: "bg-d-on-surface-variant"
  };

  return (
    <span 
      className={clsx(
        "inline-flex items-center gap-1.5 px-3 py-1 rounded-full font-d-label-sm",
        variants[variant],
        className
      )} 
      {...props}
    >
      {showDot && <span className={clsx("w-1.5 h-1.5 rounded-full", dotColors[variant])}></span>}
      {children}
    </span>
  );
};
