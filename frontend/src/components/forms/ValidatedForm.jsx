import { createContext, useContext, useId, useRef, useState, useEffect } from 'react';
import { CircleAlert } from 'lucide-react';
import { collectProblems, focusField } from './formValidation';
import './validation.css';

const ValidationContext = createContext(null);

export function Form({ onSubmit, validate, children, onChangeCapture, ...props }) {
  const [errors, setErrors] = useState([]);
  const attempted = useRef(false);
  const submitter = useRef(null);
  const formRef = useRef(null), summaryRef = useRef(null), frame = useRef(null);
  const validateRef = useRef(validate);
  validateRef.current = validate;
  useEffect(() => () => cancelAnimationFrame(frame.current), []);
  useEffect(() => {
    if (!errors.length || !formRef.current) return;
    const active = new Set(Array.from(formRef.current.elements).filter(c => c.willValidate).map(c => c.id));
    if (errors.some(error => !active.has(error.id))) setErrors(previous => previous.filter(error => active.has(error.id)));
  }, [children, errors]);
  const refresh = () => {
    if (attempted.current && formRef.current) setErrors(collectProblems(formRef.current, validateRef.current, submitter.current));
  };
  return <ValidationContext.Provider value={errors}>
    <form {...props} className={`validated-form ${props.className || ''}`} ref={formRef} noValidate onChangeCapture={event => {
      onChangeCapture?.(event);
      // Chỉ kiểm tra lại sau lần gửi đầu; đợi React cập nhật điều kiện/field động.
      cancelAnimationFrame(frame.current); frame.current = requestAnimationFrame(refresh);
    }} onSubmit={event => {
      attempted.current = true;
      submitter.current = event.nativeEvent.submitter;
      const found = collectProblems(event.currentTarget, validateRef.current, submitter.current);
      setErrors(found);
      if (found.length) {
        event.preventDefault();
        requestAnimationFrame(() => { summaryRef.current?.focus(); summaryRef.current?.scrollIntoView({ block: 'nearest' }); });
        return;
      }
      attempted.current = false;
      onSubmit?.(event);
    }}>
      {errors.length > 0 && <div ref={summaryRef} tabIndex={-1} role="alert" aria-label="Thông tin cần kiểm tra" className="col-span-full rounded-2xl border-2 border-d-error bg-white p-4 text-d-on-surface outline-none focus-visible:ring-2 focus-visible:ring-d-error">
        <p className="flex items-center gap-2 font-bold text-d-error"><CircleAlert size={20} aria-hidden="true" />Kiểm tra lại {errors.length} thông tin</p>
        <ul className="mt-2 space-y-1 text-sm">{errors.map(error => <li key={error.id}><a href={`#${error.id}`} className="text-d-error underline underline-offset-2" onClick={event => { event.preventDefault(); focusField(formRef.current, error.id); }}>{error.message}</a></li>)}</ul>
      </div>}
      {children}
    </form>
  </ValidationContext.Provider>;
}

function controlComponent(Tag) {
  return function ValidationControl({ id, className, ...props }) {
    const generated = useId();
    const controlId = id || `field-${generated}`;
    const errors = useContext(ValidationContext);
    const error = errors?.find(item => item.id === controlId);
    const description = [props['aria-describedby'], error ? `${controlId}-error` : null].filter(Boolean).join(' ') || undefined;
    return <>
      <Tag {...props} id={controlId} className={`${className || ''}${error ? ' !border-d-error outline-d-error' : ''}`}
        aria-label={props['aria-label'] || props['data-field-label'] || error?.label} aria-invalid={error ? true : props['aria-invalid']}
        aria-describedby={description} />
      {error && <p id={`${controlId}-error`} data-field-error className="mt-1 flex items-start gap-1.5 text-sm font-medium text-d-error"><CircleAlert size={16} className="mt-0.5 shrink-0" aria-hidden="true" /><span>{error.message}</span></p>}
    </>;
  };
}

export const Input = controlComponent('input');
export const Select = controlComponent('select');
export const Textarea = controlComponent('textarea');
