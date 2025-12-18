import { defineConfig } from 'vite';
import { fileURLToPath, URL } from 'node:url';
import { readFileSync, copyFileSync, existsSync } from 'fs';
import { join } from 'path';

// Base path configuration - defaults to '/' for root deployment
// All asset paths in the built index.html will be root-relative: /assets/..., /images/..., etc.
// To deploy under a sub-path (e.g., /starburst/), set VITE_BASE_PATH=/starburst/ during build
const basePath = process.env.VITE_BASE_PATH || '/';

// Normalize base path for Vite (must end with /)
// When basePath is '/', normalizedBasePath remains '/'
// When basePath is '/starburst', normalizedBasePath becomes '/starburst/'
const normalizedBasePath = basePath === '/' ? '/' : (basePath.endsWith('/') ? basePath : `${basePath}/`);

// Plugin to handle config.js based on environment
function configPlugin() {
  return {
    name: 'config-plugin',
    configureServer(server) {
      // In dev mode, serve config.dev.js as config.js
      server.middlewares.use((req, res, next) => {
        if (req.url === '/config.js') {
          const configPath = join(__dirname, 'public', 'config.dev.js');
          if (existsSync(configPath)) {
            const content = readFileSync(configPath, 'utf-8');
            res.setHeader('Content-Type', 'application/javascript');
            res.end(content);
            return;
          }
        }
        next();
      });
    },
    closeBundle() {
      // In build mode, copy config.prod.js to dist/config.js
      const prodConfigPath = join(__dirname, 'public', 'config.prod.js');
      const distConfigPath = join(__dirname, 'dist', 'config.js');
      if (existsSync(prodConfigPath)) {
        copyFileSync(prodConfigPath, distConfigPath);
        console.log('✅ Copied config.prod.js to dist/config.js');
      }
    }
  };
}

export default defineConfig({
  root: '.',
  base: normalizedBasePath,
  server: {
    port: 3030
  },
  resolve: {
    alias: {
      '@engine': fileURLToPath(new URL('./src/engine', import.meta.url)),
      '@game': fileURLToPath(new URL('./src/game', import.meta.url)),
      '@network': fileURLToPath(new URL('./src/network', import.meta.url))
    }
  },
  build: {
    outDir: 'dist',
    rollupOptions: {
      input: 'index.html'
    }
  },
  publicDir: 'public',
  plugins: [configPlugin()]
});
