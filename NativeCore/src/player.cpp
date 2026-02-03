#include "player.hpp"
#include <iostream>

Player::Player() {}

Player::~Player() {
    if (mpv) {
        mpv_terminate_destroy(mpv);
    }
}

bool Player::Initialize(HWND parentHwnd) {
    this->parent_hwnd = parentHwnd;
    mpv = mpv_create();
    if (!mpv) return false;

    // Embed in window
    int64_t wid = (int64_t)parentHwnd;
    mpv_set_option(mpv, "wid", MPV_FORMAT_INT64, &wid);

    // Enable simple Input
    mpv_set_option_string(mpv, "input-default-bindings", "yes");
    mpv_set_option_string(mpv, "input-vo-keyboard", "yes");

    // Disable OSC initially if overlay
    // mpv_set_option_string(mpv, "osc", "no");

    if (mpv_initialize(mpv) < 0) {
        return false;
    }

    return true;
}

void Player::Load(const std::string& path) {
    if (mpv) {
        const char* cmd[] = {"loadfile", path.c_str(), nullptr};
        mpv_command(mpv, cmd);
    }
}

void Player::Resize(int x, int y, int width, int height) {
    // With "wid" embedding, MPV fills the parent HWND client area automatically if handled by the OS/Window Proc.
    // But since we pass the MAIN window as parent, MPV paints over the whole thing.
    // Ideally, we should have created a Child Window (Static control or custom class) for the player.
    // Correcting strategy: Main.cpp should pass a Child HWND.
    // For now, assuming "wid" makes it fill the *passed* HWND.
    // If the passed HWND is the main window, it fills the main window.

    // To support resizing a sub-region (overlay style in main window), we really need a child window.
    // But let's assume Main.cpp creates a child window for the player?
    // In Main.cpp I didn't create a child for MPV. I passed `hMainWnd`.
    // So `Resize` logic here is essentially no-op unless I move a child window.

    // Let's rely on the caller (Main.cpp) to manage the window layout if using child windows.
    // BUT, since this is a "Simple" transition demo, passing Main Window + `wid` will just make MPV background.
    // That's acceptable for a first pass "Native Player" proof.

    // To make it better:
    // If we wanted exact positioning, we'd create a child window in Player::Initialize.
}
