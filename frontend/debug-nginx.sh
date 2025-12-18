#!/bin/bash
# Debug script to check nginx configuration and file structure in container

echo "=== Checking nginx configuration ==="
kubectl exec -it deployment/starburst-frontend -- nginx -t

echo ""
echo "=== Listing files in /usr/share/nginx/html ==="
kubectl exec -it deployment/starburst-frontend -- ls -la /usr/share/nginx/html/

echo ""
echo "=== Checking if assets directory exists ==="
kubectl exec -it deployment/starburst-frontend -- ls -la /usr/share/nginx/html/assets/ 2>&1 || echo "Assets directory not found or empty"

echo ""
echo "=== Checking for index.js files ==="
kubectl exec -it deployment/starburst-frontend -- find /usr/share/nginx/html/assets -name "index-*.js" 2>&1

echo ""
echo "=== Nginx error log (last 20 lines) ==="
kubectl exec -it deployment/starburst-frontend -- tail -20 /var/log/nginx/error.log 2>&1 || echo "Error log not accessible"

echo ""
echo "=== Nginx access log (last 10 lines) ==="
kubectl exec -it deployment/starburst-frontend -- tail -10 /var/log/nginx/access.log 2>&1 || echo "Access log not accessible"

