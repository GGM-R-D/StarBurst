/**
 * Base path configuration for asset URLs.
 * 
 * This controls where assets are loaded from. By default, assets are served from
 * the site root (/). For deployments under a sub-path (e.g., /starburst), set
 * the VITE_BASE_PATH environment variable during build.
 * 
 * Examples:
 * - Root deployment: VITE_BASE_PATH=/ (default)
 * - Sub-path deployment: VITE_BASE_PATH=/starburst/
 * 
 * Note: The base path should start and end with a slash for sub-paths,
 * or be "/" for root deployment.
 */

// Get base path from environment variable (set during build)
// Vite exposes env vars prefixed with VITE_ to the client
const envBasePath = import.meta.env.VITE_BASE_PATH;

// Normalize the base path
// Default to "/" if not set or empty
export const BASE_PATH: string = envBasePath 
  ? (envBasePath.endsWith('/') ? envBasePath : `${envBasePath}/`)
  : '/';

// Ensure BASE_PATH starts with / for absolute paths
export const BASE_PATH_NORMALIZED: string = BASE_PATH.startsWith('/') 
  ? BASE_PATH 
  : `/${BASE_PATH}`;

