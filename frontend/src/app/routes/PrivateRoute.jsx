import { Navigate } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
export function PrivateRoute({ children, allowedRoles }) {
    const { isAuthenticated, role } = useAuth();
    if (!isAuthenticated) {
        return <Navigate to="/login" replace/>;
    }
    if (allowedRoles && role && !allowedRoles.includes(role)) {
        return <Navigate to="/unauthorized" replace/>;
    }
    return <>{children}</>;
}
