import { MaterialIcon } from './MaterialIcon';

export default function Modal({ open, onClose, title, children, className }) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#141e18]/50 backdrop-blur-sm p-4">
      <div
        className={`bg-white w-full max-w-lg max-h-[90vh] overflow-y-auto rounded-[24px] shadow-[0_20px_40px_rgba(23,33,27,0.12)] flex flex-col ${className ?? ''}`}
      >
        <div className="flex justify-between items-center px-6 py-5 border-b border-d-border-subtle shrink-0 sticky top-0 bg-white rounded-t-[24px]">
          <h2 className="font-d-body-lg text-d-body-lg font-bold text-d-on-surface">{title}</h2>
          <button onClick={onClose} className="text-d-on-surface-variant hover:text-d-on-surface transition-colors p-2 rounded-full hover:bg-d-surface-variant/50">
            <MaterialIcon name="close" />
          </button>
        </div>
        <div className="px-6 py-6">{children}</div>
      </div>
    </div>
  );
}
