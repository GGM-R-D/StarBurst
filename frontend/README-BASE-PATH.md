# Base Path Configuration

The StarBurst frontend supports configurable base paths for deployment flexibility.

## Default Behavior (Root Deployment)

By default, the application is built to be served from the site root (`/`). All assets will be referenced with absolute paths starting from root:

- `/assets/index-*.js`
- `/images/logo.png`
- `/sounds/background-music.mp3`

## Sub-Path Deployment

To deploy the application under a sub-path (e.g., `/starburst/`), set the `VITE_BASE_PATH` environment variable during the build process.

### Building for Sub-Path

```bash
# Build with sub-path
VITE_BASE_PATH=/starburst/ npm run build

# Or export it first
export VITE_BASE_PATH=/starburst/
npm run build
```

This will generate assets with the sub-path prefix:

- `/starburst/assets/index-*.js`
- `/starburst/images/logo.png`
- `/starburst/sounds/background-music.mp3`

### Docker Build

```bash
# Root deployment (default)
docker build -t starburst-frontend .

# Sub-path deployment
docker build --build-arg VITE_BASE_PATH=/starburst/ -t starburst-frontend .
```

**Note:** You'll need to update the Dockerfile to accept the build arg:

```dockerfile
ARG VITE_BASE_PATH=/
ENV VITE_BASE_PATH=$VITE_BASE_PATH
RUN npm run build
```

## Nginx Configuration

### Root Deployment (Default)

The default `nginx.conf` is configured for root deployment. No changes needed.

### Sub-Path Deployment

If deploying under a sub-path, update `nginx.conf` to handle the base path:

```nginx
location /starburst/ {
    alias /usr/share/nginx/html/;
    try_files $uri $uri/ /starburst/index.html;
}
```

## Development Server

The development server respects the `VITE_BASE_PATH` environment variable:

```bash
# Root (default)
npm run dev
# Access at: http://localhost:3030/

# Sub-path
VITE_BASE_PATH=/starburst/ npm run dev
# Access at: http://localhost:3030/starburst/
```

## Implementation Details

- **Configuration:** `src/config/basePath.ts` - Centralized base path configuration
- **Asset Helper:** `src/game/config/assetsManifest.ts` - `getAssetUrl()` function for all asset paths
- **Vite Config:** `vite.config.ts` - Uses `VITE_BASE_PATH` environment variable (defaults to `/`)

All asset loading code uses `getAssetUrl()` to ensure consistent base path handling across:
- Images/textures
- Audio files
- Static assets (JSON, spritesheets, etc.)

