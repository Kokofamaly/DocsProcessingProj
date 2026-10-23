import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { apiFetch } from './shared/functions/apiFetch';
import { useNavigate } from "react-router-dom";
import './Auth.css';


export function RegisterPage(){
    const navigate = useNavigate();
    const [login, setLogin] = useState('');
    const [password, setPassword] = useState('');
    const queryClient = useQueryClient();
    const registerMutation = useMutation({
        mutationFn: async (registerData : {login: string, password: string}) => {
            const response = await apiFetch("register", {method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(registerData)});
            if(!response.ok) throw new Error(response.statusText);

        },
        onSuccess: () => {
            queryClient.setQueryData(['auth'], true);
            navigate('/', { replace: true});
        }
    });

    return(<>
        <form onSubmit={e => {
            e.preventDefault();
            registerMutation.mutate({ login, password });
        }}>
            <input type="text" placeholder="Enter your login" value={login} onChange={e => setLogin(e.target.value)}/>
            <input type="password" placeholder="Enter your password" value={password} onChange={e => setPassword(e.target.value)}/>
            <button type="submit">Register</button>
        </form>
    </>);
}