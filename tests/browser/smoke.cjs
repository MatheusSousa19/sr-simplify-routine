// Optional end-to-end test. Requires Playwright + Chromium and a Release build.
// Runs a disposable local Development server; does not send external emails.
const { chromium } = require(process.env.SR_PLAYWRIGHT_MODULE || 'playwright');
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const app = path.join(root, 'src/SrSimplifyRoutine.Web');
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'sr-browser-'));
const port = process.env.SR_TEST_PORT || '5139';
const base = `http://127.0.0.1:${port}`;
const email = `browser-${crypto.randomBytes(6).toString('hex')}@example.test`;
const password = crypto.randomBytes(20).toString('base64url');
const log = fs.openSync(path.join(temporary, 'server.log'), 'w');
const server = spawn(process.env.SR_DOTNET || 'dotnet', ['bin/Release/net10.0/SrSimplifyRoutine.Web.dll'], {
  cwd: app, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: base,
    ConnectionStrings__DefaultConnection: `Data Source=${path.join(temporary, 'test.db')};Foreign Keys=True`,
    Application__KeysPath: path.join(temporary, 'keys') }, stdio: ['ignore', log, log]
});
const delay = ms => new Promise(r => setTimeout(r, ms));
const checks = [];
const passed = name => { checks.push(name); console.log('PASS ' + name); };
let browser;
(async () => {
  for (let i = 0; i < 80; i++) {
    try { if ((await fetch(base)).ok) break; } catch {}
    if (i === 79) throw Error('Local server did not start');
    await delay(250);
  }
  browser = await chromium.launch({ executablePath: process.env.SR_BROWSER_EXECUTABLE || undefined,
    headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, reducedMotion: 'reduce' });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const go = async url => { await page.goto(base + url); await page.waitForLoadState('networkidle'); };
  await go('/');
  await page.getByRole('link', { name: 'Build my routine' }).click();
  await page.locator('#Input\\.DisplayName').fill('Matheus');
  await page.locator('#Input\\.Email').fill(email);
  await page.locator('#Input\\.Password').fill(password);
  await page.locator('#Input\\.ConfirmPassword').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await page.waitForURL('**/Account/RegisterConfirmation');
  passed('Account registration');
  const outbox = path.join(app, '.dev-mail');
  const message = fs.readdirSync(outbox).map(name => fs.readFileSync(path.join(outbox, name), 'utf8')).find(body => body.includes(email));
  assert(message, 'Expected local confirmation email');
  const link = message.match(/href="([^"]+)"/)[1].replaceAll('&amp;', '&');
  await page.goto(link);
  await go('/Account/Login');
  await page.locator('#Input\\.Email').fill(email);
  await page.locator('#Input\\.Password').fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: false }).click();
  await page.waitForURL('**/dashboard');
  await page.getByRole('heading', { name: 'Make space for what matters.' }).waitFor();
  passed('Email confirmation and sign-in');
  await go('/categories');
  await page.locator('#category-name').fill('German Learning');
  await page.locator('#category-colour').fill('#4278b7');
  await page.locator('#category-kind').selectOption('Study');
  await page.locator('#category-goal').fill('120');
  await page.getByRole('button', { name: 'Create category', exact: true }).click();
  await page.getByRole('heading', { name: 'German Learning', exact: true }).waitFor();
  passed('Custom category and weekly target');
  const date = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Dublin', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  async function activity(title, category, time, repeat = false) {
    await go('/activities/new');
    await page.locator('#activity-title').fill(title);
    await page.locator('#activity-category').selectOption({ label: category });
    await page.locator('#activity-start').fill(`${date}T${time}`);
    await page.locator('#activity-duration').fill('60');
    await page.locator('#activity-notes').fill(category === 'Workouts' ? 'Squats 3 × 8 · Romanian deadlifts 3 × 10 · Finish with mobility.' : 'One focused session. Put the phone away and take a short break afterwards.');
    if (repeat) { await page.locator('#activity-repeat').selectOption('Weekly'); await page.locator('#activity-count').fill('3'); }
    await page.getByRole('button', { name: 'Save activity', exact: true }).click();
    await page.waitForURL('**/calendar');
    await page.getByRole('heading', { name: 'Your calendar.' }).waitFor();
  }
  await activity('C# · API integration', 'Studies', '09:00');
  await activity('German vocabulary practice', 'German Learning', '13:00');
  await activity('Lower body & mobility', 'Workouts', '18:00', true);
  passed('Create studies, workout and repeating activities');
  await go('/dashboard');
  await page.getByRole('button', { name: 'Complete C# · API integration', exact: true }).click();
  await page.getByRole('button', { name: 'Reopen C# · API integration', exact: true }).waitFor();
  passed('Complete an activity and update dashboard');
  const screenshots = path.join(root, 'artifacts', 'screenshots');
  fs.mkdirSync(screenshots, { recursive: true });
  await page.screenshot({ path: path.join(screenshots, 'overview-desktop.png'), fullPage: true });
  await page.getByRole('link', { name: 'German vocabulary practice', exact: true }).click();
  await page.locator('#activity-duration').fill('45');
  await page.getByRole('button', { name: 'Check for overlaps', exact: true }).click();
  await page.getByText('No overlaps found.').waitFor();
  await page.getByRole('button', { name: 'Save activity', exact: true }).click();
  await page.waitForURL('**/calendar');
  passed('Edit activity and overlap check');
  await go('/calendar');
  await page.screenshot({ path: path.join(screenshots, 'calendar-desktop.png'), fullPage: true });
  await page.getByRole('button', { name: 'week', exact: true }).click();
  await page.locator('.week-view').waitFor();
  await page.getByRole('button', { name: 'day', exact: true }).click();
  await page.locator('.calendar-day-agenda').waitFor();
  passed('Month, week and day calendar views');
  await go('/studies');
  await page.getByRole('link', { name: 'German vocabulary practice', exact: true }).waitFor();
  assert.equal(await page.getByRole('link', { name: 'Lower body & mobility', exact: true }).count(), 0);
  passed('Study section includes custom Study categories');
  await go('/workouts');
  assert.equal(await page.getByRole('link', { name: 'Lower body & mobility', exact: true }).count(), 3);
  passed('Workout section and generated occurrences');
  await go('/categories');
  let card = page.locator('article.category-card').filter({ has: page.getByRole('heading', { name: 'German Learning', exact: true }) });
  await card.getByRole('button', { name: 'Archive', exact: true }).click();
  await page.getByRole('heading', { name: 'German Learning', exact: true }).waitFor({ state: 'hidden' });
  await page.getByLabel('Show archived').check();
  await card.getByRole('button', { name: 'Restore', exact: true }).click();
  await card.getByRole('button', { name: 'Archive', exact: true }).waitFor();
  passed('Archive and restore category');
  await go('/progress');
  await page.getByRole('heading', { name: 'Your weekly balance.' }).waitFor();
  await page.screenshot({ path: path.join(screenshots, 'progress-desktop.png'), fullPage: true });
  passed('Weekly progress report');
  await activity('Temporary test activity', 'Personal', '21:00');
  await go('/planner');
  await page.getByRole('link', { name: 'Temporary test activity', exact: true }).click();
  await page.getByRole('button', { name: 'Delete this activity', exact: true }).click();
  await page.getByRole('button', { name: 'Yes, delete activity', exact: true }).click();
  await page.waitForURL('**/calendar');
  await go('/planner');
  assert.equal(await page.getByRole('link', { name: 'Temporary test activity', exact: true }).count(), 0);
  passed('Confirmed deletion of a single activity');
  await go('/preferences');
  await page.locator('#display-name').fill('Matheus');
  await page.getByRole('button', { name: 'Save preferences' }).click();
  await page.getByText('Your preferences have been saved.').waitFor();
  passed('Profile preferences');
  await page.setViewportSize({ width: 390, height: 844 });
  for (const url of ['/dashboard', '/calendar', '/categories', '/activities/new']) {
    await go(url);
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `Viewport overflow: ${url}`);
  }
  await go('/dashboard');
  await page.screenshot({ path: path.join(screenshots, 'overview-mobile.png'), fullPage: true });
  passed('Mobile layout fits a 390px viewport');
  await page.setViewportSize({ width: 1440, height: 1100 });
  await go('/dashboard');
  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.waitForURL(base + '/');
  await go('/dashboard');
  assert(page.url().includes('/Account/Login'));
  passed('Sign-out and protected route');
  assert.deepEqual(errors, [], 'Browser JavaScript errors');
  passed('No browser JavaScript errors');
  fs.writeFileSync(path.join(root, 'artifacts', 'browser-results.json'), JSON.stringify({ passed: checks.length, checks }, null, 2));
  console.log(`Browser checks passed: ${checks.length}`);
})().catch(async error => { console.error(error.message); process.exitCode = 1; })
.finally(async () => { if (browser) await browser.close(); server.kill('SIGTERM'); fs.closeSync(log); });
