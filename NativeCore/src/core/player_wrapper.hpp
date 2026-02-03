#pragma once
#include <string>
#include <mpv/client.h>

// Helper wrapper if we need non-Qt access
class PlayerWrapper {
public:
    PlayerWrapper();
    ~PlayerWrapper();
    // Logic moved to MpvWidget for Qt integration,
    // but this class can hold shared non-UI logic (config, playlists).
};
