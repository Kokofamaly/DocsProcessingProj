
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import './List.css';
import { apiFetch } from './shared/functions/apiFetch';

export function List() {

    const queryClient = useQueryClient();
    const statusMap = new Map();

    statusMap.set(0, "Pending")
    statusMap.set(1, "Processing")
    statusMap.set(2, "Completed")
    statusMap.set(3, "Failed")

    const query = useQuery({queryKey: ['docs'], 
        queryFn: async () => { 
            const response = await apiFetch('documents');
            if(!response.ok) throw new Error(`Get request failed: ${response.status}`);
            
            const data = await response.json() as documentDto[];
            return data;
    }, refetchInterval: (query) => {
        const docs = query.state.data;

        const hasActiveDocuments = docs?.some(
            doc => doc.status === 0 || doc.status === 1
        );

        return hasActiveDocuments ? 100 : false;
    }});
    
    const reprocessMutation = useMutation({
        mutationFn: async (documentId: string) => {

            const response = await apiFetch(`documents/${documentId}/reprocess`, {
                method: "POST"
            });

            if(!response.ok) throw new Error("Failed to reprocess.");

            return documentId;
        },
        onSuccess: () => queryClient.invalidateQueries({queryKey: ['docs']})
    });

    return (
    <table>
        <thead>
            <tr>
                <th>File name</th>
                <th>Status</th>
                <th>Uploaded at</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            {[...(query.data ?? [])]
                .sort(
                    (a, b) =>
                        Date.parse(b.uploadedAt) - Date.parse(a.uploadedAt)
                )
                .map(i => (
                    <tr key={i.id}>
                        <td>{i.fileName}</td>
                        <td>{statusMap.get(i.status)}</td>
                        <td>{new Date(i.uploadedAt).toLocaleString()}</td>
                        <td>
                            <button
                                disabled={reprocessMutation.isPending}
                                onClick={() => reprocessMutation.mutate(i.id)}
                            >
                                Reprocess
                            </button>
                        </td>
                    </tr>
                ))}
        </tbody>
    </table>
);
}

type documentDto = {id: string, fileName: string, status: number, uploadedAt: string};