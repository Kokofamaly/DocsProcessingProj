import { useQuery } from "@tanstack/react-query";
import { Navigate, Outlet, useNavigate } from "react-router-dom";
import { apiFetch} from './shared/functions/apiFetch';

export function ProtectedRoute(){

    const query = useQuery({queryKey: ['auth'], 
        queryFn: async () => {
            const response = await apiFetch("me");

            return response.ok;
        }, retry: false });
        
    if(query.isLoading) return null;
    if(!query.data){
        return <Navigate to="/login" replace={true} />
    }
    return <Outlet />;
}