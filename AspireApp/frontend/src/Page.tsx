import { useState, type ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import './Page.css';
import { apiFetch } from "./shared/functions/apiFetch";
import { LogoutButton } from './LogoutButton';


export function Page( {children} : {children: ReactNode} ) {

    const [file, setFile] = useState<File | null>(null);
    const queryClient = useQueryClient();

    const uploadMutation = useMutation({
    mutationFn: async (file: File) => {
        const formData = new FormData();
        formData.append("file", file);

        const response = await apiFetch("documents",
        {
            method: "POST",
            body: formData,
        }
        );

        if (!response.ok) {
            throw new Error(`Upload failed: ${response.status}`);
        }

        return response;
    }, onSuccess: () => queryClient.invalidateQueries({queryKey: ['docs']})
    });

    function handleSubmit() {
        if (!file) return;
        uploadMutation.mutate(file);
    }

    return (
    <>
        <header>
            <div className="topbar">
                <LogoutButton />
            </div>
            <form onSubmit={(e) => {e.preventDefault(); 
                                handleSubmit()}}>
                <input
                    type="file"
                    name="file"
                    onChange={(e) => {
                    setFile(e.target.files?.[0] ?? null);
                    }}
                />

                <button
                    type="submit"
                    disabled={!file || uploadMutation.isPending}
                >
                    {uploadMutation.isPending ? "Загрузка..." : "Загрузить файл"}
                </button>
            </form>

            {uploadMutation.isSuccess && <p>Файл успешно загружен!</p>}

            {uploadMutation.isError && (
            <p>Ошибка: {uploadMutation.error.message}</p>
            )}

            
        </header>
        <hr />
        {children}
    </>
    );
}
