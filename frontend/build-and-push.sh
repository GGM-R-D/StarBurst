#!/bin/bash

# Build and Push Script for Huawei Cloud SWR
# Usage: ./build-and-push.sh [VERSION_TAG]

set -e

# Configuration - UPDATE THESE VALUES
SWR_REGION="cn-north-4"  # Change to your SWR region
SWR_NAMESPACE="YOUR_NAMESPACE"  # Replace with your SWR namespace
IMAGE_NAME="starburst-frontend"
SWR_REPO="swr.${SWR_REGION}.myhuaweicloud.com/${SWR_NAMESPACE}/${IMAGE_NAME}"

# Get version tag (default: latest)
VERSION_TAG=${1:-latest}
FULL_IMAGE_NAME="${SWR_REPO}:${VERSION_TAG}"

echo "=========================================="
echo "Building and Pushing to Huawei Cloud SWR"
echo "=========================================="
echo "Image: ${FULL_IMAGE_NAME}"
echo ""

# Step 1: Build the Docker image
echo "Step 1: Building Docker image..."
docker build -t ${FULL_IMAGE_NAME} -t ${SWR_REPO}:latest .
echo "✅ Build complete"
echo ""

# Step 2: Login to SWR (you'll need to authenticate first)
echo "Step 2: Logging in to Huawei Cloud SWR..."
echo "Note: Make sure you're logged in to SWR first:"
echo "  docker login swr.${SWR_REGION}.myhuaweicloud.com"
echo ""

# Step 3: Push the image
echo "Step 3: Pushing image to SWR..."
docker push ${FULL_IMAGE_NAME}
if [ "$VERSION_TAG" != "latest" ]; then
    docker push ${SWR_REPO}:latest
fi
echo "✅ Push complete"
echo ""

echo "=========================================="
echo "✅ Build and push successful!"
echo "Image: ${FULL_IMAGE_NAME}"
echo ""
echo "Next steps:"
echo "1. Update k8s/deployment.yaml with your image name"
echo "2. Apply the deployment: kubectl apply -f k8s/"
echo "=========================================="

