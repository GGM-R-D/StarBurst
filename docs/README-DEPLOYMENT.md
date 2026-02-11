# Deployment Guide - Game Engine Host

This guide explains how to build and deploy the Game Engine Host to Kubernetes using Docker and Huawei Cloud SWR.

## Prerequisites

1. **Docker** installed and running
2. **kubectl** configured to access your Kubernetes cluster
3. **Huawei Cloud SWR** account and namespace created
4. **Docker login** credentials for SWR
5. **.NET SDK 9.0** (for local builds, not required for Docker builds)

## Project Structure

The Game Engine Host requires:
- Game configuration files from `../RGS/RGS/configs/`
- Control program manifest from `../RGS/RGS/control-program-manifest.json`

These files are automatically copied into the Docker image during build.

## Step 1: Configure SWR Settings

Edit `build-and-push.sh` and update:
- `SWR_REGION`: Your SWR region (e.g., `cn-north-4`)
- `SWR_NAMESPACE`: Your SWR namespace name

## Step 2: Login to Huawei Cloud SWR

```bash
docker login swr.cn-north-4.myhuaweicloud.com
# Enter your SWR username and password
```

## Step 3: Build and Push Docker Image

```bash
# Navigate to GameEngineHost directory
cd backend/GameEngineHost

# Make the script executable
chmod +x build-and-push.sh

# Build and push (default: latest tag)
./build-and-push.sh

# Or with a specific version tag
./build-and-push.sh v1.0.0
```

This will:
1. Build the Docker image using the Dockerfile
2. Copy game configuration files into the image
3. Tag it with your SWR repository path
4. Push it to Huawei Cloud SWR

## Step 4: Update Kubernetes Deployment

Edit `k8s/deployment.yaml` and update:
- `image`: Replace `YOUR_NAMESPACE` with your actual SWR namespace
- `replicas`: Adjust number of replicas as needed
- Resource limits/requests: Adjust based on your cluster capacity

Example:
```yaml
image: swr.cn-north-4.myhuaweicloud.com/my-namespace/game-engine-host:latest
```

## Step 5: Deploy to Kubernetes

```bash
# Apply the deployment and service
kubectl apply -f k8s/deployment.yaml

# Apply the ingress (if using)
kubectl apply -f k8s/ingress.yaml

# Check deployment status
kubectl get deployments
kubectl get pods
kubectl get services
```

## Step 6: Verify Deployment

```bash
# Check pod logs
kubectl logs -l app=game-engine-host

# Check service endpoints
kubectl get endpoints game-engine-host-service

# Test the health endpoint
kubectl port-forward svc/game-engine-host-service 8080:80
curl http://localhost:8080/health

# Test the play endpoint (example)
curl -X POST http://localhost:8080/play \
  -H "Content-Type: application/json" \
  -d '{"gameId":"starburst","sessionId":"test","bets":[...]}'
```

## Configuration

### Environment Variables

The Game Engine Host uses `appsettings.json` and `appsettings.Production.json` for configuration:

- **ConfigurationDirectory**: `/app/configs` (in container)
- **ControlProgramManifest**: `/app/control-program-manifest.json` (in container)

These paths are set in `appsettings.Production.json` and are used when `ASPNETCORE_ENVIRONMENT=Production`.

### Port Configuration

- **Container Port**: 8080 (configurable via `ASPNETCORE_URLS` environment variable)
- **Service Port**: 80 (maps to container port 8080)
- **Health Check**: `/health` endpoint

### Game Configuration Files

The following game configuration files are included in the Docker image:
- `starburst.json`
- `starburstReelsets.json`
- `JungleRelics.json`
- `JungleRelicsReelsets.json`
- `control-program-manifest.json`

These are copied from `backend/RGS/RGS/configs/` during the Docker build.

## Updating the Deployment

1. Make your code changes
2. Rebuild and push:
   ```bash
   ./build-and-push.sh v1.0.1
   ```
3. Update the deployment:
   ```bash
   kubectl set image deployment/game-engine-host \
     game-engine-host=swr.cn-north-4.myhuaweicloud.com/YOUR_NAMESPACE/game-engine-host:v1.0.1
   ```
4. Or apply the updated deployment.yaml:
   ```bash
   kubectl apply -f k8s/deployment.yaml
   kubectl rollout status deployment/game-engine-host
   ```

## Troubleshooting

### Image Pull Errors
- Verify SWR credentials: `docker login swr.cn-north-4.myhuaweicloud.com`
- Check image name in deployment.yaml matches SWR repository
- Ensure Kubernetes nodes have access to SWR

### Pod Not Starting
- Check pod logs: `kubectl logs <pod-name>`
- Verify health check endpoint: `/health`
- Check resource limits aren't too restrictive
- Verify configuration files exist in container: `kubectl exec <pod-name> -- ls -la /app/configs`

### Configuration File Errors
- Verify config files are copied during build
- Check `appsettings.Production.json` paths are correct (`/app/configs`, `/app/control-program-manifest.json`)
- Ensure config files are valid JSON

### Port/Connection Issues
- Verify service is exposing the correct port
- Check ingress configuration if using external access
- Verify firewall/security group rules allow traffic

## API Endpoints

- **Health Check**: `GET /health`
- **Play**: `POST /play`
- **Swagger UI**: `GET /swagger` (if enabled in production)

## File Structure

```
backend/GameEngineHost/
├── Dockerfile              # Multi-stage Docker build
├── .dockerignore          # Files to exclude from Docker build
├── appsettings.json       # Development configuration
├── appsettings.Production.json  # Production configuration
├── build-and-push.sh      # Script to build and push to SWR
├── k8s/
│   ├── deployment.yaml    # Kubernetes deployment and service
│   └── ingress.yaml       # Kubernetes ingress (optional)
└── README-DEPLOYMENT.md   # This file
```

## Notes

- The Docker image uses .NET 9.0 runtime (aspnet:9.0)
- Health checks are configured at `/health`
- The application runs as a non-root user for security
- Configuration files are copied into the image at build time
- The deployment includes liveness and readiness probes

