// Browser E2E of a *published* Faranou server against a real SQL Server (CI job "e2e").
// Required environment:
//   FARANOU_E2E_PUBLISH  publish directory (scripts/publish.sh output)
//   FARANOU_E2E_SQL      connection string of the migrated + seeded E2E database
import { defineConfig, devices } from '@playwright/test';
import { mkdirSync, readFileSync } from 'node:fs';

const publish = process.env.FARANOU_E2E_PUBLISH;
const connectionString = process.env.FARANOU_E2E_SQL;
if (!publish || !connectionString) {
  throw new Error('Set FARANOU_E2E_PUBLISH and FARANOU_E2E_SQL (see tests/e2e/README.md).');
}

const state = JSON.parse(readFileSync(new URL('./.e2e-state.json', import.meta.url), 'utf8'));
const port = 5090;
const setupPort = 5091;
// LocalApplicationData of the setup-mode instance; .NET only reports the folder when it exists.
const setupData = `${process.env.RUNNER_TEMP ?? '/tmp'}/faranou-e2e-setup`;
mkdirSync(setupData, { recursive: true });

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  // In CI the github reporter turns every failure into an annotation of the run.
  reporter: process.env.CI ? [['github'], ['list'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${port}`,
    locale: 'fa-IR',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // The application as deployed: Production environment, published files, SQL Server.
      command: 'dotnet Server.dll',
      cwd: publish,
      url: `http://localhost:${port}/Account/Login`,
      timeout: 120_000,
      reuseExistingServer: false,
      stdout: 'pipe',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Production',
        ASPNETCORE_URLS: `http://localhost:${port}`,
        AllowedHosts: 'localhost',
        ConnectionStrings__ConnectionString: connectionString,
        ApplicationSettings__MasterUsername: state.master.username,
        ApplicationSettings__MasterPassword: state.master.hash,
        // The suite signs in many times from one address.
        RateLimiting__Login__PermitLimit: '1000',
      },
    },
    {
      // A second instance without a connection string: the first-run setup page.
      command: 'dotnet Server.dll',
      cwd: publish,
      url: `http://localhost:${setupPort}/DatabaseSetup`,
      timeout: 120_000,
      reuseExistingServer: false,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Production',
        ASPNETCORE_URLS: `http://localhost:${setupPort}`,
        AllowedHosts: 'localhost',
        LOCALAPPDATA: setupData,
        XDG_DATA_HOME: setupData,
      },
    },
  ],
});
