const API_URL = `${import.meta.env.VITE_API_URL}`;


export async function apiFetch(endpoint: string, options: RequestInit = {}){
    const url = `${API_URL}/${endpoint}`;
    return fetch(url, {...options, credentials: 'include'});
};
