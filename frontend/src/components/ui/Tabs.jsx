import React, { createContext, useContext, useState } from 'react';
import clsx from 'clsx';

const TabsContext = createContext();

export const Tabs = ({ defaultValue, className, children }) => {
  const [activeTab, setActiveTab] = useState(defaultValue);
  return (
    <TabsContext.Provider value={{ activeTab, setActiveTab }}>
      <div className={className}>{children}</div>
    </TabsContext.Provider>
  );
};

export const TabsList = ({ className, children }) => (
  <div className={clsx("inline-flex h-10 items-center justify-center rounded-md bg-surface-container-low p-1 text-outline", className)}>
    {children}
  </div>
);

export const TabsTrigger = ({ value, className, children }) => {
  const { activeTab, setActiveTab } = useContext(TabsContext);
  const isActive = activeTab === value;
  
  return (
    <button
      className={clsx(
        "inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50",
        isActive ? "bg-surface-container-lowest text-on-surface shadow-sm" : "hover:text-on-surface-variant",
        className
      )}
      onClick={() => setActiveTab(value)}
    >
      {children}
    </button>
  );
};

export const TabsContent = ({ value, className, children }) => {
  const { activeTab } = useContext(TabsContext);
  if (activeTab !== value) return null;
  return (
    <div className={clsx("mt-2 ring-offset-background focus-visible:outline-none", className)}>
      {children}
    </div>
  );
};
