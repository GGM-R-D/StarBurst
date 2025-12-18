import axios, { AxiosInstance } from 'axios';
import type {
  StartRequest,
  StartResponse,
  PlayRequest,
  PlayResponse,
  BalanceRequest,
  BalanceResponse
} from './types';
import { parseGameUrlParams } from './urlParams';

declare global {
  interface Window {
    config: {
      SERVER_CONFIG: {
        SERVER_URL: string;
        RESPONSE_WAIT_TIME: number;
        PATHS: {
          init: string;
          play: string;
          balance: string;
        };
      };
    };
  }
}

class RgsClient {
  private http: AxiosInstance;
  private urlParams = parseGameUrlParams();

  constructor() {
    const cfg = window.config.SERVER_CONFIG;
    this.http = axios.create({
      baseURL: cfg.SERVER_URL,
      timeout: cfg.RESPONSE_WAIT_TIME
    });
  }

  /**
   * Construct RGS API path according to specification:
   * /{operatorId}/{gameId}/start
   * /{operatorId}/{gameId}/play
   * /{operatorId}/player/balance
   */
  private buildPath(endpoint: string): string {
    // Use TEST as operatorId and starburst as gameId to match RGS endpoints
    const operatorId = this.urlParams.operatorId || 'TEST';
    const gameId = this.urlParams.gameId || 'starburst';
    
    if (endpoint === 'balance') {
      return `/${operatorId}/player/balance`;
    }
    return `/${operatorId}/${gameId}/${endpoint}`;
  }

  async startGame(payload: StartRequest): Promise<StartResponse> {
    // Always use buildPath with defaults (TEST/STELLAR_GEMS) if URL params not provided
    const path = this.buildPath('start');
    const fullUrl = `${this.http.defaults.baseURL}${path}`;
    
    console.info(`[RGS] Connecting to RGS at: ${fullUrl}`);
    console.info(`[RGS] Start request payload:`, payload);
    
    try {
      const { data } = await this.http.post<StartResponse>(path, payload);
      console.info(`[RGS] ✅ Start game successful:`, {
        statusCode: data.statusCode,
        sessionId: data.player?.sessionId,
        balance: data.player?.balance
      });
      return data;
    } catch (error: any) {
      console.error(`[RGS] ❌ Start game failed:`, {
        url: fullUrl,
        error: error.message,
        status: error.response?.status,
        statusText: error.response?.statusText,
        data: error.response?.data
      });
      throw new Error(`Failed to connect to RGS: ${error.message || 'Unknown error'}`);
    }
  }

  async play(payload: PlayRequest): Promise<PlayResponse> {
    // Always use buildPath with defaults (TEST/STELLAR_GEMS) if URL params not provided
    const path = this.buildPath('play');
    const fullUrl = `${this.http.defaults.baseURL}${path}`;
    
    console.info(`[RGS] Sending play request to: ${fullUrl}`);
    console.info(`[RGS] Play request payload:`, {
      sessionId: payload.sessionId,
      baseBet: payload.baseBet,
      betMode: payload.betMode,
      betsCount: payload.bets?.length
    });
    
    try {
      const { data } = await this.http.post<PlayResponse>(path, payload);
      console.info(`[RGS] ✅ Play request successful:`, {
        statusCode: data.statusCode,
        win: data.player?.win,
        balance: data.player?.balance,
        hasResults: !!data.game?.results
      });
      return data;
    } catch (error: any) {
      console.error(`[RGS] ❌ Play request failed:`, {
        url: fullUrl,
        error: error.message,
        status: error.response?.status,
        statusText: error.response?.statusText,
        data: error.response?.data,
        requestPayload: payload
      });
      
      // Provide more specific error messages
      if (error.response?.status === 401) {
        throw new Error('RGS Authentication failed. Session may have expired.');
      } else if (error.response?.status === 404) {
        throw new Error('RGS endpoint not found. Check if RGS service is running.');
      } else if (error.response?.status === 500) {
        throw new Error('RGS internal server error. Check RGS logs for details.');
      } else if (error.code === 'ECONNREFUSED' || error.message?.includes('Network Error')) {
        throw new Error('Cannot connect to RGS. Is the RGS service running?');
      }
      
      throw new Error(`RGS play request failed: ${error.message || 'Unknown error'}`);
    }
  }

  async fetchBalance(payload: BalanceRequest): Promise<BalanceResponse> {
    // Always use buildPath with defaults (TEST) if URL params not provided
    const path = this.buildPath('balance');
    
    const { data } = await this.http.post<BalanceResponse>(path, payload);
    return data;
  }
}

export const rgsClient = new RgsClient();

