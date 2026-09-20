// Simple cn utility (like clsx + tailwind-merge)
export function cn(...classes) {
    return classes.filter(Boolean).join(' ');
}
