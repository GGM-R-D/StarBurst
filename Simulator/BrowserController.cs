// ═══════════════════════════════════════════════════════════════════════════════════════════════
// BrowserController.cs - Playwright Browser Automation
// ═══════════════════════════════════════════════════════════════════════════════════════════════
//
// PURPOSE:
//   Automates Chrome browser to trigger spins on the frontend game with custom grids.
//   Uses Microsoft Playwright for reliable cross-browser automation.
//
// WHAT IS PLAYWRIGHT?
//   Playwright is a browser automation library from Microsoft.
//   It can control Chrome, Firefox, and Safari programmatically.
//   Think of it like a "robot" that can click buttons, type text, and read web pages.
//
// HOW THE CUSTOM GRID FLOW WORKS:
//   1. Simulator calculates a grid that produces a specific win
//   2. BrowserController sets the grid in browser's sessionStorage
//   3. BrowserController triggers a spin (keyboard press or API call)
//   4. Frontend reads the custom grid from sessionStorage
//   5. Frontend sends it to the RGS in the play request
//   6. RGS forwards to Engine with funMode=1
//   7. Engine uses custom grid instead of random RNG
//   8. Result displays on screen!
//
// PLAYWRIGHT OBJECT HIERARCHY:
//   IPlaywright (library instance)
//       └── IBrowser (Chrome process)
//             └── IBrowserContext (like incognito window)
//                   └── IPage (a single tab/page)
//
// WHY USE PLAYWRIGHT INSTEAD OF SELENIUM?
//   - Playwright is faster and more reliable
//   - Built-in auto-waiting (no flaky tests)
//   - Better support for modern web apps (React, PixiJS)
//   - Cross-browser with single API
//
// ═══════════════════════════════════════════════════════════════════════════════════════════════

using Microsoft.Playwright;
using System.Diagnostics;

namespace Simulator;

/// <summary>
/// Controls the browser to trigger spins on the frontend game.
/// Uses Playwright to launch and control Chrome automatically.
/// 
/// Usage:
///   var controller = new BrowserController();
///   await controller.LaunchAsync("http://localhost:3030/?funMode=1");
///   await controller.SetGridAndSpinAsync(new[] { 1, 2, 3, 4, 5, ... }); // 15 elements
///   await controller.DisposeAsync();
/// 
/// Implements IAsyncDisposable for proper cleanup of browser resources.
/// </summary>
public sealed class BrowserController : IAsyncDisposable
{
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // PLAYWRIGHT OBJECTS - Hierarchy: Playwright → Browser → Context → Page
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Main Playwright instance. Created once, used to launch browsers.
    /// </summary>
    private IPlaywright? _playwright;
    
    /// <summary>
    /// The Chrome browser instance (actual browser process).
    /// </summary>
    private IBrowser? _browser;
    
    /// <summary>
    /// Browser context - like an incognito window.
    /// Each context has isolated cookies, storage, etc.
    /// </summary>
    private IBrowserContext? _context;
    
    /// <summary>
    /// The web page we're controlling (a single browser tab).
    /// This is where we navigate, click buttons, and execute JavaScript.
    /// </summary>
    private IPage? _page;
    
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    // STATE TRACKING
    // ═══════════════════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// URL of the game page. Includes funMode=1 to enable custom grids.
    /// </summary>
    private string _gameUrl = "http://localhost:3030/?funMode=1";
    
    /// <summary>
    /// True if we have a working browser connection.
    /// </summary>
    private bool _isConnected;
    
    /// <summary>
    /// True if we launched the browser (vs connecting to existing).
    /// If true, we should close the browser when disposing.
    /// </summary>
    private bool _ownsBrowser;

    /// <summary>Gets whether we have an active browser connection.</summary>
    public bool IsConnected => _isConnected;
    
    /// <summary>Gets the current game URL.</summary>
    public string GameUrl => _gameUrl;

    /// <summary>
    /// Launch a new browser and navigate to the game.
    /// This is the simplest and most reliable approach.
    /// </summary>
    public async Task<bool> LaunchAsync(string? customUrl = null)
    {
        if (!string.IsNullOrEmpty(customUrl))
        {
            _gameUrl = customUrl;
        }

        try
        {
            Console.WriteLine("  Initializing Playwright...");
            _playwright = await Playwright.CreateAsync();

            Console.WriteLine("  Launching Chrome...");
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,  // We want to see the browser
                Args = new[] { "--start-maximized" }
            });
            _ownsBrowser = true;

            Console.WriteLine("  Creating browser context...");
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = ViewportSize.NoViewport  // Use full window size
            });

            Console.WriteLine($"  Opening game at {_gameUrl}...");
            _page = await _context.NewPageAsync();
            await _page.GotoAsync(_gameUrl, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000 
            });

            _isConnected = true;
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("  ✓ BROWSER LAUNCHED AND CONNECTED!");
            Console.WriteLine($"  ✓ Game loaded at: {_page.Url}");
            Console.ResetColor();
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Failed to launch browser: {ex.Message}");
            
            if (ex.Message.Contains("Executable doesn't exist"))
            {
                Console.WriteLine();
                Console.WriteLine("  Playwright browsers need to be installed. Run this command:");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("    playwright install chromium");
                Console.ResetColor();
            }
            
            return false;
        }
    }

    /// <summary>
    /// Try to connect to an existing Chrome instance running with remote debugging.
    /// Falls back to launching a new browser if connection fails.
    /// </summary>
    public async Task<bool> ConnectOrLaunchAsync(string? customUrl = null)
    {
        if (!string.IsNullOrEmpty(customUrl))
        {
            _gameUrl = customUrl;
        }

        // First try to connect to existing Chrome
        Console.WriteLine("  Checking for existing Chrome with remote debugging...");
        
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await httpClient.GetAsync("http://localhost:9222/json");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("  Found Chrome on port 9222, connecting...");
                return await ConnectToExistingAsync();
            }
        }
        catch
        {
            // Chrome not running with remote debugging, that's fine
        }

        // Launch new browser
        Console.WriteLine("  No existing Chrome found, launching new browser...");
        return await LaunchAsync(customUrl);
    }

    /// <summary>
    /// Connect to an existing Chrome instance (must be started with --remote-debugging-port=9222)
    /// </summary>
    private async Task<bool> ConnectToExistingAsync()
    {
        try
        {
            _playwright ??= await Playwright.CreateAsync();

            _browser = await _playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
            _ownsBrowser = false;  // Don't close when we disconnect

            var contexts = _browser.Contexts;
            if (contexts.Count > 0)
            {
                _context = contexts[0];
                var pages = _context.Pages;
                
                // Find the game page or create a new one
                _page = pages.FirstOrDefault(p => 
                    p.Url.Contains("localhost:3030") || 
                    p.Url.Contains("localhost:5173"));
                    
                if (_page == null)
                {
                    _page = pages.FirstOrDefault() ?? await _context.NewPageAsync();
                    await _page.GotoAsync(_gameUrl, new PageGotoOptions 
                    { 
                        WaitUntil = WaitUntilState.NetworkIdle 
                    });
                }
            }
            else
            {
                _context = await _browser.NewContextAsync();
                _page = await _context.NewPageAsync();
                await _page.GotoAsync(_gameUrl);
            }

            _isConnected = true;
            Console.WriteLine($"  ✓ Connected to existing Chrome: {_page.Url}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Failed to connect: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Set the custom grid in sessionStorage and trigger a spin.
    /// </summary>
    public async Task<bool> SetGridAndSpinAsync(int[] gridOneBased)
    {
        if (_page == null || !_isConnected)
        {
            Console.WriteLine("  ❌ Not connected to browser");
            return false;
        }

        try
        {
            // Make sure we're on the game page with funMode
            var currentUrl = _page.Url;
            if (!currentUrl.Contains("localhost:3030") && !currentUrl.Contains("localhost:5173"))
            {
                Console.WriteLine($"  Navigating to game...");
                await _page.GotoAsync(_gameUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            }
            else if (!currentUrl.Contains("funMode=1"))
            {
                // Add funMode parameter
                var newUrl = currentUrl.Contains("?") 
                    ? currentUrl + "&funMode=1" 
                    : currentUrl + "?funMode=1";
                Console.WriteLine($"  Enabling funMode...");
                await _page.GotoAsync(newUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            }

            // Set the custom grid in sessionStorage
            var gridJson = $"[{string.Join(",", gridOneBased)}]";
            Console.WriteLine($"  Setting custom grid: {gridJson}");
            
            await _page.EvaluateAsync($"sessionStorage.setItem('customFunModeGrid', '{gridJson}')");
            Console.WriteLine("  ✓ Grid saved to sessionStorage");

            // Small delay to ensure storage is set
            await Task.Delay(200);

            // Try to click the spin button
            Console.WriteLine("  Looking for spin button...");
            
            var spinClicked = await _page.EvaluateAsync<bool>(@"() => {
                // For PixiJS canvas games, we need special handling
                const canvas = document.querySelector('canvas');
                
                // Method 1: Try to find global game instance and call spin directly
                if (window.gameApp && typeof window.gameApp.spin === 'function') {
                    console.log('Found gameApp.spin(), calling directly');
                    window.gameApp.spin();
                    return true;
                }
                if (window.game && typeof window.game.spin === 'function') {
                    console.log('Found game.spin(), calling directly');
                    window.game.spin();
                    return true;
                }
                if (window.PIXI && window.PIXI.app && window.PIXI.app.spin) {
                    console.log('Found PIXI.app.spin(), calling directly');
                    window.PIXI.app.spin();
                    return true;
                }
                
                // Method 2: Common spin button selectors (HTML buttons)
                const selectors = [
                    '[data-testid=""spin-button""]',
                    '.spin-button',
                    '#spin-button',
                    '#spinButton',
                    '.spinButton',
                    'button.spin',
                    '[class*=""spin""][class*=""button""]',
                    '[class*=""Spin""][class*=""Button""]',
                ];
                
                for (const selector of selectors) {
                    const el = document.querySelector(selector);
                    if (el && el.offsetParent !== null) {
                        el.click();
                        console.log('Clicked spin button:', selector);
                        return true;
                    }
                }
                
                // Method 3: Try keyboard events on canvas (PixiJS games often use keyboard)
                if (canvas) {
                    // Focus the canvas first
                    canvas.focus();
                    
                    // Send spacebar event to canvas
                    canvas.dispatchEvent(new KeyboardEvent('keydown', { 
                        code: 'Space', 
                        key: ' ',
                        keyCode: 32,
                        which: 32,
                        bubbles: true,
                        cancelable: true
                    }));
                    canvas.dispatchEvent(new KeyboardEvent('keyup', { 
                        code: 'Space', 
                        key: ' ',
                        keyCode: 32,
                        which: 32,
                        bubbles: true,
                        cancelable: true
                    }));
                    console.log('Sent keyboard events to canvas');
                }
                
                // Method 4: Send keyboard event to document
                document.dispatchEvent(new KeyboardEvent('keydown', { 
                    code: 'Space', 
                    key: ' ',
                    keyCode: 32,
                    which: 32,
                    bubbles: true 
                }));
                document.dispatchEvent(new KeyboardEvent('keyup', { 
                    code: 'Space', 
                    key: ' ',
                    keyCode: 32,
                    which: 32,
                    bubbles: true 
                }));
                
                // Method 5: Simulate mouse click on canvas (bottom center where spin usually is)
                if (canvas) {
                    const rect = canvas.getBoundingClientRect();
                    // Spin button is typically at bottom center
                    const x = rect.width / 2;
                    const y = rect.height - 80; // 80px from bottom
                    
                    const clickEvent = new MouseEvent('click', {
                        clientX: rect.left + x,
                        clientY: rect.top + y,
                        bubbles: true,
                        cancelable: true,
                        view: window
                    });
                    canvas.dispatchEvent(clickEvent);
                    console.log('Sent click to canvas at:', x, y);
                }
                
                console.log('Attempted multiple spin methods');
                return false;
            }");

            if (spinClicked)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✓ Spin triggered via game API!");
                Console.ResetColor();
            }
            else
            {
                // Use Playwright's keyboard API as a more reliable fallback
                Console.WriteLine("  Sending keyboard input via Playwright...");
                try
                {
                    // Focus the page and press Space
                    await _page.Keyboard.PressAsync("Space");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✓ Spacebar pressed via Playwright!");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ Keyboard press failed: {ex.Message}");
                    Console.WriteLine("    You may need to click SPIN manually in the browser");
                    Console.ResetColor();
                }
            }

            // Wait for spin animation to start
            await Task.Delay(500);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Just set the grid without spinning (user will click spin manually).
    /// </summary>
    public async Task<bool> SetGridOnlyAsync(int[] gridOneBased)
    {
        if (_page == null || !_isConnected)
        {
            Console.WriteLine("  ❌ Not connected to browser");
            return false;
        }

        try
        {
            var gridJson = $"[{string.Join(",", gridOneBased)}]";
            Console.WriteLine($"  Setting custom grid: {gridJson}");
            
            await _page.EvaluateAsync($"sessionStorage.setItem('customFunModeGrid', '{gridJson}')");
            Console.WriteLine("  ✓ Grid saved to sessionStorage");
            Console.WriteLine("  → Click SPIN on the game to see the result");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Take a screenshot of the current game state.
    /// </summary>
    public async Task<string?> TakeScreenshotAsync(string filename = "spin_result.png")
    {
        if (_page == null || !_isConnected)
        {
            return null;
        }

        try
        {
            var path = Path.Combine(Environment.CurrentDirectory, filename);
            await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path });
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Bring the browser window to front.
    /// </summary>
    public async Task BringToFrontAsync()
    {
        if (_page != null)
        {
            try
            {
                await _page.BringToFrontAsync();
            }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null && _ownsBrowser)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
        _isConnected = false;
    }
}
