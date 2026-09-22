import React from 'react';
import clsx from 'clsx';

export const Tabs = ({ className, children, ...props }) => (
  <div className={clsx("flex border-b border-d-border-subtle", className)} {...props}>
    {children}
  </div>
);

export const TabsList = ({ className, children, ...props }) => (
  <div className={clsx("flex", className)} {...props}>
    {children}
  </div>
);

export const TabsTrigger = ({ className, active, children, ...props }) => (
  <button
    className={clsx(
      "pb-3 px-6 font-d-label-md transition-colors",
      active 
        ? "text-d-primary font-bold border-b-2 border-d-primary" 
        : "text-d-on-surface-variant hover:text-d-primary border-b-2 border-transparent",
      className
    )}
    {...props}
  >
    {children}
  </button>
);

export const TabsContent = ({ className, active, children, ...props }) => {
  if (!active) return null;
  return (
    <div className={clsx("pt-6", className)} {...props}>
      {children}
    </div>
  );
};
