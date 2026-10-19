import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";

export function List() {
    const [list, setList] = useState<stubType[]>([]);
    const query= useQuery({queryKey: ['docs'], 
        queryFn: async () => { 
            const response = await fetch(`${import.meta.env.VITE_API_URL}/documents`);
            if(!response.ok) throw new Error(`Get request failed: ${response.status}`);
            
            const data = await response.json() as stubType[];
            return data;
    }});

    useEffect(() => {
        if(query && query.isSuccess){
            setList(query.data);
        }
    }, [query]);

    return (<ul>
        { list.map(i => <li key={i.id}>{i.fileName} -- {i.status}</li>) }
    </ul>)
}

type stubType = {id: string, fileName: string, status: string};