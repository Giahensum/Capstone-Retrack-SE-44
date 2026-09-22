import React from 'react';
import clsx from 'clsx';

export const Table = ({ className, children, ...props }) => (
  <div className="bg-d-surface-container-lowest border border-d-border-subtle rounded-xl overflow-hidden shadow-sm">
    <div className="overflow-x-auto">
      <table className={clsx("w-full text-left border-collapse", className)} {...props}>
        {children}
      </table>
    </div>
  </div>
);

export const TableHeader = ({ className, children, ...props }) => (
  <thead className={className} {...props}>
    {children}
  </thead>
);

export const TableRow = ({ className, children, ...props }) => (
  <tr className={clsx("transition-colors hover:bg-d-surface-container/50", className)} {...props}>
    {children}
  </tr>
);

export const TableHead = ({ className, children, ...props }) => (
  <th className={clsx("py-4 px-6 font-medium font-d-label-md text-d-on-surface-variant border-b border-d-border-subtle bg-d-surface-container-low", className)} {...props}>
    {children}
  </th>
);

export const TableBody = ({ className, children, ...props }) => (
  <tbody className={clsx("font-d-body-sm text-d-on-surface divide-y divide-d-border-subtle", className)} {...props}>
    {children}
  </tbody>
);

export const TableCell = ({ className, children, ...props }) => (
  <td className={clsx("py-4 px-6", className)} {...props}>
    {children}
  </td>
);
