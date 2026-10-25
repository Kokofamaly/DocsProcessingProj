import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Routes, Route } from "react-router-dom";
// import './index.css'
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import App from './App.tsx'
import { LoginPage } from './LoginPage.tsx'
import { RegisterPage } from './RegisterPage.tsx'
import { ProtectedRoute } from './ProtectedRoute.tsx'

const queryClient = new QueryClient();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />}/>
          <Route path="/register" element={<RegisterPage />}/>
          <Route element={<ProtectedRoute />}>
            <Route path="/" element={<App />}/>
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
