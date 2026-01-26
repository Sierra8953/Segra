from playwright.sync_api import Page, expect, sync_playwright

def verify_live_preview(page: Page):
    # 1. Arrange: Go to the app
    page.goto("http://localhost:2882")

    # 2. Act: Navigate to Live Preview
    # It's a button, not a link.
    button = page.get_by_role("button", name="Live Preview")
    button.click()

    # 3. Assert: Check for empty state
    # The empty state in LivePreview.tsx says:
    # "No Active Recording"
    # "Start a game or recording to see the live preview."
    expect(page.get_by_text("No Active Recording")).to_be_visible()

    # 4. Screenshot
    page.screenshot(path="/home/jules/verification/live-preview.png")

if __name__ == "__main__":
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        try:
            verify_live_preview(page)
        except Exception as e:
            print(f"Error: {e}")
            page.screenshot(path="/home/jules/verification/error.png")
            raise
        finally:
            browser.close()
