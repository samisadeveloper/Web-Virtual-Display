import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
        server: {
                port: 3000,
        },
        build:{
                outDir: '../wwwroot',
                //#emptyOutDir: true, // This will delete all files in the output directory before building
                emptyOutDir: true, 
        },
        plugins: [react()],
})
