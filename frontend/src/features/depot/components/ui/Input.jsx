import React from 'react';
import clsx from 'clsx';

export const Input = React.forwardRef(({ className, type = "text", ...props }, ref) => {
  return (
    <input
      type={type}
      className={clsx(
        "w-full bg-d-surface-container-lowest border border-d-border-subtle rounded-xl py-2.5 px-4 font-d-body-sm text-d-on-surface focus:outline-none focus:border-d-secondary focus:ring-1 focus:ring-d-secondary transition-colors",
        className
      )}
      ref={ref}
      {...props}
    />
  );
});

Input.displayName = "Input";
