#include <windows.h>
#include <string>
#include <vector>
#include <thread>
#include "recorder.hpp"
#include "player.hpp"

// Global Variables
HINSTANCE hInst;
HWND hMainWnd;
Recorder* g_Recorder = nullptr;
Player* g_Player = nullptr;

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_CREATE:
        // Create UI controls (Buttons)
        CreateWindow(L"BUTTON", L"Start Recording",
            WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
            10, 10, 150, 30, hWnd, (HMENU)1, hInst, NULL);

        CreateWindow(L"BUTTON", L"Stop Recording",
            WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
            170, 10, 150, 30, hWnd, (HMENU)2, hInst, NULL);
        break;

    case WM_COMMAND:
        if (LOWORD(wParam) == 1) // Start
        {
            if (g_Recorder) {
                // TODO: Generate dynamic path
                g_Recorder->StartRecording("recording.mp4");
                if (g_Player) g_Player->Load("recording.mp4");
            }
        }
        else if (LOWORD(wParam) == 2) // Stop
        {
            if (g_Recorder) g_Recorder->StopRecording();
        }
        break;

    case WM_SIZE:
        {
            // Resize Player to fill bottom part of window
            int width = LOWORD(lParam);
            int height = HIWORD(lParam);
            if (g_Player) {
                // Keep top 50px for controls
                g_Player->Resize(0, 50, width, height - 50);
            }
        }
        break;

    case WM_DESTROY:
        PostQuitMessage(0);
        break;

    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    hInst = hInstance;

    // Register Window Class
    WNDCLASSEXW wcex = {0};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.hInstance      = hInstance;
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszClassName  = L"StreamDVR_Native";

    RegisterClassExW(&wcex);

    // Create Window
    hMainWnd = CreateWindowW(L"StreamDVR_Native", L"StreamDVR (Native C++)", WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, 0, 1280, 720, nullptr, nullptr, hInstance, nullptr);

    if (!hMainWnd) return FALSE;

    ShowWindow(hMainWnd, nCmdShow);
    UpdateWindow(hMainWnd);

    // Initialize Subsystems
    g_Recorder = new Recorder();
    if (!g_Recorder->Initialize()) {
        MessageBox(hMainWnd, L"Failed to initialize OBS!", L"Error", MB_OK | MB_ICONERROR);
    }

    g_Player = new Player();
    if (!g_Player->Initialize(hMainWnd)) {
        MessageBox(hMainWnd, L"Failed to initialize MPV!", L"Error", MB_OK | MB_ICONERROR);
    }

    // Main Message Loop
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    // Cleanup
    delete g_Player;
    delete g_Recorder;

    return (int) msg.wParam;
}
