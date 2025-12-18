# Deployment Guide - Starburst Frontend

This guide explains how to build and deploy the Starburst frontend to Kubernetes using Docker and Huawei Cloud SWR.

## Prerequisites

1. **Docker** installed and running
2. **kubectl** configured to access your Kubernetes cluster
3. **Huawei Cloud SWR** account and namespace created
4. **Docker login** credentials for SWR

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
# Make the script executable
chmod +x build-and-push.sh

# Build and push (default: latest tag)
./build-and-push.sh

# Or with a specific version tag
./build-and-push.sh v1.0.0
```

This will:
1. Build the Docker image using the Dockerfile
2. Tag it with your SWR repository path
3. Push it to Huawei Cloud SWR

## Step 4: Update Kubernetes Deployment

Edit `k8s/deployment.yaml` and update:
- `image`: Replace `YOUR_NAMESPACE` with your actual SWR namespace
- `replicas`: Adjust number of replicas as needed
- Resource limits/requests: Adjust based on your cluster capacity

Example:
```yaml
image: swr.cn-north-4.myhuaweicloud.com/my-namespace/starburst-frontend:latest
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
kubectl logs -l app=starburst-frontend

# Check service endpoints
kubectl get endpoints starburst-frontend-service

# Test the health endpoint
kubectl port-forward svc/starburst-frontend-service 8080:80
curl http://localhost:8080/health
```

## Configuration

### Environment Variables

The frontend uses `config.js` for configuration. Make sure this file is included in the build (it's in the `public` folder and will be copied automatically).

### Base Path

The app defaults to root deployment (`/`). For sub-path deployments (e.g., `/starburst/`), set the `VITE_BASE_PATH` environment variable during build:

```bash
docker build --build-arg VITE_BASE_PATH=/starburst/ -t starburst-frontend .
```

See `README-BASE-PATH.md` for detailed configuration options.

## Updating the Deployment

1. Make your code changes
2. Rebuild and push:
   ```bash
   ./build-and-push.sh v1.0.1
   ```
3. Update the deployment:
   ```bash
   kubectl set image deployment/starburst-frontend \
     starburst-frontend=swr.cn-north-4.myhuaweicloud.com/YOUR_NAMESPACE/starburst-frontend:v1.0.1
   ```
4. Or apply the updated deployment.yaml:
   ```bash
   kubectl apply -f k8s/deployment.yaml
   kubectl rollout status deployment/starburst-frontend
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

### 404 Errors
- Verify nginx.conf is correctly serving index.html for all routes
- Check base path configuration matches ingress path
- Verify all assets are included in the build

## File Structure

```
.
├── Dockerfile              # Multi-stage Docker build
├── .dockerignore          # Files to exclude from Docker build
├── nginx.conf             # Nginx configuration for serving static files
├── build-and-push.sh      # Script to build and push to SWR
├── k8s/
│   ├── deployment.yaml    # Kubernetes deployment and service
│   └── ingress.yaml       # Kubernetes ingress (optional)
└── README-DEPLOYMENT.md   # This file
```

## Notes

- The Docker image uses nginx:alpine for a small image size
- Health checks are configured at `/health`
- Static assets are cached for 1 year
- HTML files are not cached to allow updates
- The deployment includes liveness and readiness probes

