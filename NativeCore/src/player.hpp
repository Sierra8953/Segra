#pragma once
#include <windows.h>
#include <string>
#include <mpv/client.h>

class Player {
public:
    Player();
    ~Player();

    bool Initialize(HWND parentHwnd);
    void Load(const std::string& path);
    void Resize(int x, int y, int width, int height);

private:
    mpv_handle* mpv = nullptr;
    HWND parent_hwnd = nullptr;
};
