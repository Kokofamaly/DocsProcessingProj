import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { apiFetch } from "./shared/functions/apiFetch";

export function LogoutButton() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const logoutMutation = useMutation({
    mutationFn: async () => {
      const response = await apiFetch("logout", { method: "POST" });
      if (!response.ok) throw new Error(`Logout failed: ${response.status}`);
    },
    onSuccess: () => {
      queryClient.clear();                       // выбросить кэш прошлого пользователя
      queryClient.setQueryData(["auth"], false); // ProtectedRoute теперь знает, что входа нет
      navigate("/login", { replace: true });
    },
  });

  return (
    <button onClick={() => logoutMutation.mutate()} disabled={logoutMutation.isPending}>
      Logout
    </button>
  );
}