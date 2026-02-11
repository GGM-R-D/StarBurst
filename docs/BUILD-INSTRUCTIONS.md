# Docker Build Instructions

## Important: Build Context

The Dockerfile **must** be run from the `backend/` directory, not from `GameEngineHost/`. This is because the Dockerfile needs access to both:
- `GameEngineHost/` directory (for the application code)
- `RGS/RGS/configs/` directory (for game configuration files)

## Correct Build Command

**From the `backend/` directory:**

```bash
cd backend
docker build -f GameEngineHost/Dockerfile -t starburst-engine:1 .
```

**NOT from `GameEngineHost/` directory:**
```bash
# ❌ This will fail - can't access RGS/RGS/configs
cd backend/GameEngineHost
docker build -t starburst-engine:1 .
```

## Using the Build Script

The easiest way is to use the provided build script:

```bash
cd backend/GameEngineHost
chmod +x build-and-push.sh
./build-and-push.sh
```

This script automatically changes to the `backend/` directory before building.

## Manual Build Steps

1. **Navigate to backend directory:**
   ```bash
   cd backend
   ```

2. **Build the image:**
   ```bash
   docker build -f GameEngineHost/Dockerfile -t starburst-engine:1 .
   ```

3. **Verify config files are included:**
   ```bash
   docker run --rm starburst-engine:1 ls -la /app/configs
   ```

## Why This Is Required

Docker's build context determines what files are available to COPY commands. When you run:
- `docker build -f GameEngineHost/Dockerfile .` from `backend/` → context is `backend/`, can access `GameEngineHost/` and `RGS/`
- `docker build .` from `GameEngineHost/` → context is `GameEngineHost/`, **cannot** access `../RGS/`

Docker COPY commands cannot use `../` to go outside the build context for security reasons.

