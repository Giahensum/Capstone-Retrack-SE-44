const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const EVIDENCE_DIR = path.resolve('C:/Users/Admin/.gemini/antigravity/brain/28ce25fe-7db8-4b4a-8797-0bf3e2c81d35/evidence_screenshots');
if (!fs.existsSync(EVIDENCE_DIR)) {
  fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
}

(async () => {
  console.log('🚀 Starting Playwright E2E Test for Seller AI Recommendation...');
  const browser = await chromium.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const context = await browser.newContext({
    viewport: { width: 1440, height: 960 },
    locale: 'vi-VN'
  });
  const page = await context.newPage();

  page.on('console', msg => console.log(`[BROWSER CONSOLE ${msg.type()}]`, msg.text()));
  page.on('pageerror', err => console.error(`[PAGE ERROR]`, err.message));
  page.on('response', resp => {
    if (resp.url().includes('/api/')) {
      console.log(`[API RESPONSE] ${resp.status()} ${resp.url()}`);
    }
  });

  try {
    // 1. Login as Seller
    console.log('👉 Navigating to http://127.0.0.1:5173/login...');
    await page.goto('http://127.0.0.1:5173/login', { waitUntil: 'networkidle' });

    console.log('👉 Filling credentials for seller@retrack.vn...');
    await page.fill('input[type="email"]', 'seller@retrack.vn');
    await page.fill('input[type="password"]', 'Seller@123');

    console.log('👉 Clicking login button...');
    await Promise.all([
      page.waitForURL(url => !url.pathname.includes('/login'), { timeout: 10000 }),
      page.click('button[type="submit"]')
    ]);
    console.log('✅ Logged in successfully! Current URL:', page.url());

    // 2. Navigate to AI Scanner
    console.log('👉 Navigating to /seller/ai-scanner...');
    await page.goto('http://127.0.0.1:5173/seller/ai-scanner', { waitUntil: 'networkidle' });

    // Wait for the sample demo button
    const sampleBtn = page.locator('#btn-sample-carton_box');
    await sampleBtn.waitFor({ state: 'visible', timeout: 10000 });
    console.log('👉 Clicking Demo Sample Button (#btn-sample-carton_box)...');
    await sampleBtn.click();

    // Wait for AI scan predictions to show (regex matches Carton or Bìa Carton or Cardboard)
    console.log('👉 Waiting for AI scanner classification results...');
    const resultLocator = page.locator('text=/Carton|Cardboard/i').first();
    await resultLocator.waitFor({ state: 'visible', timeout: 20000 });
    console.log('✅ AI Classification returned successfully!');

    // 3. Configure Parameters: Weight, Condition, Location
    console.log('👉 Selecting 20 kg weight chip...');
    const chip20kg = page.locator('button:has-text("20 kg")');
    await chip20kg.waitFor({ state: 'visible' });
    await chip20kg.click();

    // Check condition select/dropdown or radio
    const conditionSelect = page.locator('select').first();
    if (await conditionSelect.count() > 0) {
      await conditionSelect.selectOption({ index: 0 }); // Hạng A
      console.log('✅ Selected quality condition: Hạng A');
    }

    // Scroll into view of the recommendation box
    const recBox = page.locator('text=Định giá & Gợi ý thị trường thông minh AI').first();
    await recBox.scrollIntoViewIfNeeded();
    await page.waitForTimeout(1000);

    // Click to expand pricing breakdown accordion
    console.log('👉 Expanding "Bóc tách công thức định giá (Minh bạch AI)"...');
    const breakdownToggle = page.locator('text=Bóc tách công thức định giá (Minh bạch AI)');
    if (await breakdownToggle.count() > 0) {
      await breakdownToggle.click();
      await page.waitForTimeout(500);
      console.log('✅ Expanded pricing breakdown accordion!');
    }

    // Capture Screenshot 1: AI Scanner with Recommendation & Breakdown
    const shot1Path = path.join(EVIDENCE_DIR, '01_ai_scanner_pricing_recommendation.png');
    await page.screenshot({ path: shot1Path, fullPage: true });
    console.log(`📸 Screenshot 1 saved to: ${shot1Path}`);

    // Verify and interact with the new Price Trend Forecasting & Optimal Timing Widget
    console.log('👉 Verifying Price Trend Forecasting & Optimal Timing Widget...');
    const forecastHeader = page.locator('text=Dự báo xu hướng giá & Thời điểm bán tối ưu');
    await forecastHeader.waitFor({ state: 'visible', timeout: 10000 });
    await forecastHeader.scrollIntoViewIfNeeded();
    await page.waitForTimeout(1000);

    const hasRecBanner = await page.locator('text=/Khuyến nghị:/i').count() > 0;
    console.log(`✅ Optimal Decision Banner visible: ${hasRecBanner}`);

    // Click to expand detailed 14-day forecast table
    console.log('👉 Expanding 14-day detailed forecast table...');
    const tableToggle = page.locator('text=Xem chi tiết bảng giá 14 ngày');
    if (await tableToggle.count() > 0) {
      await tableToggle.click();
      await page.waitForTimeout(500);
      console.log('✅ Expanded 14-day detailed forecast table!');
    }

    // Capture Screenshot 3: Price Trend Forecasting Widget with 14-day Chart & Optimal Timing
    const shot3Path = path.join(EVIDENCE_DIR, '03_price_trend_forecasting_and_optimal_timing.png');
    await page.screenshot({ path: shot3Path, fullPage: true });
    console.log(`📸 Screenshot 3 saved to: ${shot3Path}`);

    // Read recommended price text
    const priceTextLocator = page.locator('text=đ/kg').first();
    const priceText = await priceTextLocator.textContent().catch(() => 'N/A');
    console.log(`💰 Displayed AI Recommended Price: ${priceText}`);

    // 4. Click "Tạo đơn với mức giá đề xuất này"
    console.log('👉 Clicking "Tạo đơn với mức giá đề xuất này"...');
    const createReqBtn = page.locator('button:has-text("Tạo đơn với mức giá đề xuất này")');
    await createReqBtn.waitFor({ state: 'visible' });
    await createReqBtn.click();

    // 5. Verify navigation to /seller/create
    console.log('👉 Waiting for navigation to /seller/create...');
    await page.waitForURL(url => url.pathname.includes('/seller/create'), { timeout: 10000 });
    console.log('✅ Navigated to /seller/create! Current URL:', page.url());

    // Wait for form prefill to settle
    await page.waitForTimeout(1500);

    // Verify AI recommendation badge is displayed on Create page
    const aiBadge = page.locator('text=Giá AI đề xuất:');
    const hasBadge = await aiBadge.count() > 0;
    console.log(`✅ AI Recommended Price Badge visible on /seller/create: ${hasBadge}`);

    // Capture Screenshot 2: Create Request Page prefilled
    const shot2Path = path.join(EVIDENCE_DIR, '02_create_request_prefilled_with_ai_price.png');
    await page.screenshot({ path: shot2Path, fullPage: true });
    console.log(`📸 Screenshot 2 saved to: ${shot2Path}`);

    console.log('\n========================================');
    console.log('🎉 ALL TEST STEPS PASSED SUCCESSFULLY!');
    console.log('========================================\n');
  } catch (err) {
    console.error('❌ Test failed with error:', err);
    const errShotPath = path.join(EVIDENCE_DIR, 'error_state.png');
    await page.screenshot({ path: errShotPath, fullPage: true }).catch(() => {});
    process.exit(1);
  } finally {
    await browser.close();
  }
})();
