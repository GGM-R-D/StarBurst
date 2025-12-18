/**
 * Utility to parse URL parameters from the game launcher URL.
 * Based on Client-RGS.txt specification:
 * https://${BASE_URL}?operatorId=${OperatorId}&gameId=${GameId}&languageId=${LanguageId}
 * &client=${Client}&funMode=${FunMode}&token=${token}&environment=${environment}
 * &lobbyUrl=${lobbyUrl}&reloadUrl=${reloadUrl}&cashierUrl=${cashierUrl}&extraParams=${extraParams}
 */

export interface GameUrlParams {
  operatorId?: string;
  gameId?: string;
  languageId?: string;
  client?: string;
  funMode?: string;
  token?: string;
  environment?: string;
  lobbyUrl?: string;
  reloadUrl?: string;
  cashierUrl?: string;
  extraParams?: string;
}

/**
 * Parse URL query parameters from the current window location.
 */
export function parseGameUrlParams(): GameUrlParams {
  const params: GameUrlParams = {};
  const urlParams = new URLSearchParams(window.location.search);

  params.operatorId = urlParams.get('operatorId') || undefined;
  params.gameId = urlParams.get('gameId') || undefined;
  params.languageId = urlParams.get('languageId') || undefined;
  params.client = urlParams.get('client') || undefined;
  params.funMode = urlParams.get('funMode') || undefined;
  params.token = urlParams.get('token') || undefined;
  params.environment = urlParams.get('environment') || undefined;
  params.lobbyUrl = urlParams.get('lobbyUrl') || undefined;
  params.reloadUrl = urlParams.get('reloadUrl') || undefined;
  params.cashierUrl = urlParams.get('cashierUrl') || undefined;
  params.extraParams = urlParams.get('extraParams') || undefined;

  return params;
}

/**
 * Get a specific URL parameter value.
 */
export function getUrlParam(name: string): string | null {
  const urlParams = new URLSearchParams(window.location.search);
  return urlParams.get(name);
}

