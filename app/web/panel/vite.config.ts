import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Panel leży na serwerze w podfolderze strony pilota (greysource.eu/uwielbienie/panel/) — ścieżki względne.
// Klient tailcat-link, czcionki i ikona są wspólne ze stroną pilota (../remote) i trafiają do paczki; tweetnacl,
// którego klient szuka, gdy nie ma globalnego, pochodzi z node_modules panelu.
export default defineConfig({
  base: "./",
  plugins: [react()],
  resolve: {
    alias: { tweetnacl: fileURLToPath(new URL("./node_modules/tweetnacl/nacl-fast.js", import.meta.url)) },
  },
  build: { target: "es2022", outDir: "dist", emptyOutDir: true },
  server: { fs: { allow: [".."] } },
});
