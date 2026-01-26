from playwright.sync_api import Page, expect, sync_playwright

def verify_menu(page: Page):
    # 1. Arrange: Go to the app
    page.goto("http://localhost:2882")

    # 2. Act: Check for "Live Preview" button (should NOT exist)
    # We use query locator instead of get_by_role to check for absence safely
    live_preview = page.get_by_role("button", name="Live Preview")

    # 3. Assert: Live Preview should be hidden/gone
    expect(live_preview).not_to_be_visible()

    # 4. Check "Session" button exists
    session_btn = page.get_by_role("button", name="Session")
    expect(session_btn).to_be_visible()

    # 5. Screenshot
    page.screenshot(path="/home/jules/verification/menu_check.png")

if __name__ == "__main__":
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        try:
            verify_menu(page)
        except Exception as e:
            print(f"Error: {e}")
            page.screenshot(path="/home/jules/verification/menu_error.png")
            raise
        finally:
            browser.close()
