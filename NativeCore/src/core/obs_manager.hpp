#pragma once
#include <string>
#include <obs.h>

class ObsManager {
public:
    ObsManager();
    ~ObsManager();

    bool Initialize();
    bool StartRecording(const std::string& path);
    void StopRecording();

private:
    obs_output_t* output = nullptr;
    bool is_initialized = false;
};
