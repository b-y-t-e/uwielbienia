import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Strona startowa greysource.eu/uwielbienie/ — linki do pilota (telefon/) i panelu (panel/); ścieżki względne.
// Czcionki i ikona są wspólne ze stroną pilota (../remote).
export default defineConfig({
  base: "./",
  plugins: [react()],
  build: { target: "es2022", outDir: "dist", emptyOutDir: true },
  server: { fs: { allow: [".."] } },
});
