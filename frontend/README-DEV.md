# Development Setup

## Running the Development Server

1. **Navigate to the frontend directory:**
   ```bash
   cd frontend
   ```

2. **Install dependencies (if not already done):**
   ```bash
   npm install
   ```

3. **Start the development server:**
   ```bash
   npm run dev
   ```

4. **Access the application:**
   - The app will be available at: `http://localhost:3030/` (root deployment, default)
   - For sub-path deployment: `VITE_BASE_PATH=/starburst/ npm run dev` then access at `http://localhost:3030/starburst/`

## Troubleshooting

### Black Screen Issues

If you see a black screen:

1. **Check the browser console** for JavaScript errors:
   - Open Developer Tools (F12)
   - Check the Console tab for errors
   - Check the Network tab to see if files are loading

2. **Verify you're accessing the correct URL:**
   - Default (root): `http://localhost:3030/`
   - If using sub-path: `http://localhost:3030/starburst/` (or your configured base path)

3. **Check that config.js is loading:**
   - In the Network tab, verify `config.js` loads successfully
   - It should be served from the `public` folder

4. **Verify path aliases are working:**
   - Check console for import errors related to `@engine`, `@game`, or `@network`
   - These should resolve to `src/engine`, `src/game`, `src/network`

5. **Clear browser cache:**
   - Hard refresh: Ctrl+Shift+R (Windows) or Cmd+Shift+R (Mac)

## Common Issues

### "Cannot find module" errors
- Make sure you're running `npm run dev` from the `frontend` directory
- Verify `node_modules` exists and dependencies are installed

### Config.js not loading
- Check that `public/config.js` exists
- Verify the script tag in `index.html` points to `./config.js`

### Module resolution errors
- Check `vite.config.ts` aliases match `tsconfig.json` paths
- Restart the dev server after changing config files


