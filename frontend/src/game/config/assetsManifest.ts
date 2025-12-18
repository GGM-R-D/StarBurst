export type SymbolAnimationConfig = {
  id: string;
  frames: string[];
  loop?: boolean;
  animationSpeed?: number;
};

import { BASE_PATH_NORMALIZED } from '../../config/basePath';

/**
 * Helper to get asset URL with base path support.
 * 
 * @param path - Asset path (e.g., '/images/logo.png' or 'images/logo.png')
 * @returns Full URL with base path prepended (e.g., '/images/logo.png' or '/starburst/images/logo.png')
 * 
 * Examples:
 * - getAssetUrl('/images/logo.png') -> '/images/logo.png' (root) or '/starburst/images/logo.png' (sub-path)
 * - getAssetUrl('images/logo.png') -> '/images/logo.png' (root) or '/starburst/images/logo.png' (sub-path)
 */
export const getAssetUrl = (path: string): string => {
  // Ensure path starts with /
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  
  // Combine base path with normalized path
  // BASE_PATH_NORMALIZED already ends with / for sub-paths, so we need to remove leading / from path
  if (BASE_PATH_NORMALIZED === '/') {
    // Root deployment - return path as-is
    return normalizedPath;
  } else {
    // Sub-path deployment - combine base path with path (removing leading / from path)
    return `${BASE_PATH_NORMALIZED}${normalizedPath.slice(1)}`;
  }
};

// Using existing background asset temporarily
export const backgroundTextureUrl = getAssetUrl('/images/backround/bg.png');

// Stellar Gems symbol set - exactly 8 symbols matching Starburst-style rules
// Order: Wild (expanding), Bar, Seven, Orange, Green, Red, Blue, Purple
export const symbolAnimations: SymbolAnimationConfig[] = [
  {
    id: 'SYM_WILD',
    frames: [
      getAssetUrl('/images/symbols/Wild.webp'),
      getAssetUrl('/images/symbols/Wild.webp'),
      getAssetUrl('/images/symbols/Wild.webp'),
      getAssetUrl('/images/symbols/Wild.webp')
    ],
    loop: true,
    animationSpeed: 0.7
  },
  {
    id: 'SYM_BAR',
    frames: [
      getAssetUrl('/images/symbols/Bar.webp'),
      getAssetUrl('/images/symbols/Bar.webp'),
      getAssetUrl('/images/symbols/Bar.webp'),
      getAssetUrl('/images/symbols/Bar.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_SEVEN',
    frames: [
      getAssetUrl('/images/symbols/7.webp'),
      getAssetUrl('/images/symbols/7.webp'),
      getAssetUrl('/images/symbols/7.webp'),
      getAssetUrl('/images/symbols/7.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_RED',
    frames: [
      getAssetUrl('/images/symbols/Red.webp'),
      getAssetUrl('/images/symbols/Red.webp'),
      getAssetUrl('/images/symbols/Red.webp'),
      getAssetUrl('/images/symbols/Red.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_PURPLE',
    frames: [
      getAssetUrl('/images/symbols/Purple.webp'),
      getAssetUrl('/images/symbols/Purple.webp'),
      getAssetUrl('/images/symbols/Purple.webp'),
      getAssetUrl('/images/symbols/Purple.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_BLUE',
    frames: [
      getAssetUrl('/images/symbols/Blue.webp'),
      getAssetUrl('/images/symbols/Blue.webp'),
      getAssetUrl('/images/symbols/Blue.webp'),
      getAssetUrl('/images/symbols/Blue.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_GREEN',
    frames: [
      getAssetUrl('/images/symbols/Green.webp'),
      getAssetUrl('/images/symbols/Green.webp'),
      getAssetUrl('/images/symbols/Green.webp'),
      getAssetUrl('/images/symbols/Green.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  },
  {
    id: 'SYM_ORANGE',
    frames: [
      getAssetUrl('/images/symbols/Orange.webp'),
      getAssetUrl('/images/symbols/Orange.webp'),
      getAssetUrl('/images/symbols/Orange.webp'),
      getAssetUrl('/images/symbols/Orange.webp')
    ],
    loop: true,
    animationSpeed: 0.4
  }
];

