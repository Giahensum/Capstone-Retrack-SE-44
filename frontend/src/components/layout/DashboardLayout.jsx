import { Sidebar } from './Sidebar';
export function DashboardLayout({ children, title }) {
    return (<div className="flex h-screen overflow-hidden bg-slate-950">
      <Sidebar />
      <main className="flex-1 overflow-y-auto">
        {title && (<div className="px-6 pt-6 pb-4 border-b border-slate-800">
            <h1 className="text-xl font-bold text-slate-100">{title}</h1>
          </div>)}
        <div className="p-6">{children}</div>
      </main>
    </div>);
}
