#pragma once
#include <string>
#include <obs.h>

class Recorder {
public:
    Recorder();
    ~Recorder();

    bool Initialize();
    bool StartRecording(const std::string& path);
    void StopRecording();

private:
    obs_output_t* output = nullptr;
    bool is_initialized = false;
};
