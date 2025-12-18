window.config = {
  SERVER_CONFIG: {
    SERVER_URL: 'http://localhost:5101', // Local RGS for development
    HISTORY_URL: 'https://history.your-dev-domain/dev/v1/rounds',
    REPLAY_URL:
      'https://history.your-dev-domain/dev/v1/replay/round/{sessionId}/{roundId}',
    PATHS: {
      init: '/start',
      play: '/play',
      history: '/rounds',
      balance: '/balance',
      cheats: '/play'
    },
    DOMAIN: 'your-dev-rgs-domain',
    ENVIRONMENT: 'development',
    JURISDICTION: 'MT',
    LANGUAGE: 'en-GB',
    MONEY_MODE: 'FUN',
    DEFAULT_CURRENCY: 'EUR',
    HEARTBEAT_INTERVAL: 30000,
    RESPONSE_WAIT_TIME: 15000,
    RECONNECT_INTERVAL: 5000,
    MAX_RECONNECT_ATTEMPTS: 3
  },
  GAME_CONFIG: {
    gameTitle: 'Stellar Gems',
    mobileMinFrameTime: 1000 / 30,
    minFrameTime: 1000 / 60
  },
  FEATURE_CONFIG: {
    QUICKSPIN: true,
    ALWAYS_DISPLAY_LOGO: true
  },
  JURISDICTION_CONFIG: {
    spinDelay: 0,
    market: 'EU',
    autoPlay: true,
    buyFeature: false
  }
};

