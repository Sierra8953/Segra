
from playwright.sync_api import sync_playwright
import time

def verify_settings_panel():
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()

        # Navigate to sessions page
        page.goto("http://localhost:2882/sessions")

        # Wait for page to load
        time.sleep(2)

        # The Settings Panel should be visible at the bottom
        # We look for the title "Recording Settings"
        try:
            page.wait_for_selector("text=Recording Settings", timeout=5000)
            print("Settings Panel header found.")

            # Check for specific sections
            page.wait_for_selector("text=Quality")
            page.wait_for_selector("text=Encoder & Buffer")
            page.wait_for_selector("text=Audio Tracks")
            print("All settings sections found.")

            # Check for some controls
            page.wait_for_selector("text=Standard")
            page.wait_for_selector("text=GPU")
            page.wait_for_selector("input[type=range]") # Buffer slider

            # Take screenshot
            page.screenshot(path="/home/jules/verification/settings_panel.png", full_page=True)
            print("Screenshot taken.")

        except Exception as e:
            print(f"Verification failed: {e}")
            page.screenshot(path="/home/jules/verification/failed_settings.png")

        finally:
            browser.close()

if __name__ == "__main__":
    verify_settings_panel()
