import React from 'react';
import clsx from 'clsx';

export const Card = ({ className, children, ...props }) => (
  <div className={clsx("bg-d-surface-container-lowest border border-d-border-subtle rounded-xl overflow-hidden shadow-sm", className)} {...props}>
    {children}
  </div>
);

export const BentoCard = ({ className, children, ...props }) => (
  <div className={clsx("d-bento-card", className)} {...props}>
    {children}
  </div>
);

export const CardHeader = ({ className, children, ...props }) => (
  <div className={clsx("px-6 py-4 border-b border-d-border-subtle flex flex-col space-y-1.5", className)} {...props}>
    {children}
  </div>
);

export const CardTitle = ({ className, children, ...props }) => (
  <h3 className={clsx("font-d-headline-lg text-[24px] text-d-on-surface", className)} {...props}>
    {children}
  </h3>
);

export const CardContent = ({ className, children, ...props }) => (
  <div className={clsx("p-6 pt-4 text-d-on-surface-variant", className)} {...props}>
    {children}
  </div>
);
