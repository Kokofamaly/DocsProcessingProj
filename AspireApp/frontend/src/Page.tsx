import { useState } from "react";
import { useMutation } from "@tanstack/react-query";


export function Page() {

    const [file, setFile] = useState<File | null>(null);

    const uploadMutation = useMutation({
    mutationFn: async (file: File) => {
        const formData = new FormData();
        formData.append("file", file);

        const response = await fetch(
        `http://localhost:5398/documents`,
        {
            method: "POST",
            body: formData,
        }
        );

        if (!response.ok) {
        throw new Error(`Upload failed: ${response.status}`);
        }

        return response.json();
    },
    });

    function handleSubmit() {
    if (!file) return;

    uploadMutation.mutate(file);
    }

    return (
    <>
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
    </>
    );
}
