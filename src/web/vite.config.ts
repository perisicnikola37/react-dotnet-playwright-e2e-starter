import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  base: process.env.VITE_BASE ?? "/",
  plugins: [react()],
  server: {
    port: 5180,
    strictPort: true,
    proxy: { "/api": process.env.API_URL ?? "http://localhost:5080" },
  },
});
