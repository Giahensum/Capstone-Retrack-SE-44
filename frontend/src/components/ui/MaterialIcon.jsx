import React from 'react';
import clsx from 'clsx';

export const MaterialIcon = ({ name, className, style }) => (
  <span className={clsx("material-symbols-outlined", className)} style={style}>{name}</span>
);
