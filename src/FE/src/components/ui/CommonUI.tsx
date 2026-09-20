import { type InputHTMLAttributes, forwardRef } from 'react'
import { cn } from '@/lib/cn'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, className, ...props }, ref) => {
    return (
      <div className="flex flex-col gap-1">
        {label && (
          <label className="text-sm font-medium text-slate-300">{label}</label>
        )}
        <input
          ref={ref}
          className={cn(
            'bg-slate-900 border text-slate-100 rounded-lg px-3 py-2 text-sm transition-all outline-none',
            'focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500',
            error ? 'border-red-500' : 'border-slate-600',
            className
          )}
          {...props}
        />
        {error && <span className="text-xs text-red-400">{error}</span>}
      </div>
    )
  }
)

Input.displayName = 'Input'

// -------------------------------------------------------
// Badge component
interface BadgeProps {
  children: React.ReactNode
  className?: string
}

export function Badge({ children, className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border',
        className
      )}
    >
      {children}
    </span>
  )
}

// -------------------------------------------------------
// Card component
interface CardProps {
  children: React.ReactNode
  className?: string
  title?: string
}

export function Card({ children, className, title }: CardProps) {
  return (
    <div className={cn('bg-slate-800 border border-slate-700 rounded-xl p-5', className)}>
      {title && (
        <h3 className="text-base font-semibold text-slate-100 mb-4">{title}</h3>
      )}
      {children}
    </div>
  )
}

// -------------------------------------------------------
// Stat Card
interface StatCardProps {
  title: string
  value: string | number
  subtitle?: string
  icon?: React.ReactNode
  color?: 'emerald' | 'blue' | 'purple' | 'orange'
}

const statColors = {
  emerald: 'from-emerald-500/20 to-emerald-600/10 border-emerald-500/30 text-emerald-400',
  blue: 'from-blue-500/20 to-blue-600/10 border-blue-500/30 text-blue-400',
  purple: 'from-purple-500/20 to-purple-600/10 border-purple-500/30 text-purple-400',
  orange: 'from-orange-500/20 to-orange-600/10 border-orange-500/30 text-orange-400',
}

export function StatCard({ title, value, subtitle, icon, color = 'emerald' }: StatCardProps) {
  return (
    <div
      className={cn(
        'bg-gradient-to-br border rounded-xl p-5 flex items-start gap-4',
        statColors[color]
      )}
    >
      {icon && (
        <div className="w-10 h-10 rounded-lg bg-white/10 flex items-center justify-center flex-shrink-0">
          {icon}
        </div>
      )}
      <div>
        <p className="text-xs text-slate-400 font-medium uppercase tracking-wide">{title}</p>
        <p className="text-2xl font-bold text-slate-100 mt-0.5">{value}</p>
        {subtitle && <p className="text-xs text-slate-400 mt-1">{subtitle}</p>}
      </div>
    </div>
  )
}
