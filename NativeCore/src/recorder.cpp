#include "recorder.hpp"
#include <windows.h>
#include <iostream>

Recorder::Recorder() {}

Recorder::~Recorder() {
    if (output) {
        obs_output_release(output);
    }
    if (is_initialized) {
        obs_shutdown();
    }
}

bool Recorder::Initialize() {
    if (!obs_startup("en-US", nullptr, nullptr)) {
        return false;
    }

    // Set paths assuming 'bin/64bit' execution context
    // Data is copied to ./data/libobs and ./data/obs-plugins by CMake
    obs_add_data_path("./data/libobs/");
    obs_add_module_path("./obs-plugins/64bit/", "./data/obs-plugins/%module%/");

    obs_load_all_modules();
    obs_post_load_modules();

    // Reset Video/Audio to defaults (1080p 60fps)
    struct obs_video_info ovi = {};
    ovi.adapter_index = 0;
    ovi.fps_num = 60;
    ovi.fps_den = 1;
    ovi.graphics_module = "libobs-d3d11";
    ovi.base_width = 1920;
    ovi.base_height = 1080;
    ovi.output_width = 1920;
    ovi.output_height = 1080;
    ovi.output_format = VIDEO_FORMAT_NV12;
    ovi.colorspace = VIDEO_CS_DEFAULT;
    ovi.range = VIDEO_RANGE_DEFAULT;
    ovi.scale_type = OBS_SCALE_BILINEAR;

    if (obs_reset_video(&ovi) != OBS_VIDEO_SUCCESS) {
        return false;
    }

    struct obs_audio_info oai = {};
    oai.samples_per_sec = 44100;
    oai.speakers = SPEAKERS_STEREO;

    if (!obs_reset_audio(&oai)) {
        return false;
    }

    is_initialized = true;
    return true;
}

bool Recorder::StartRecording(const std::string& path) {
    if (!is_initialized) return false;

    // Create FFmpeg Muxer Output
    obs_data_t* settings = obs_data_create();
    obs_data_set_string(settings, "path", path.c_str());
    obs_data_set_string(settings, "format_name", "mp4");
    obs_data_set_string(settings, "muxer_settings", "movflags=frag_keyframe+empty_moov+default_base_moof");

    output = obs_output_create("ffmpeg_muxer", "adv_ffmpeg_output", settings, nullptr);
    obs_data_release(settings);

    if (!output) return false;

    // Create Video Encoder (x264 software fallback for safety, ideally NVENC)
    // In a real app, detect hardware support.
    obs_data_t* enc_settings = obs_data_create();
    obs_data_set_string(enc_settings, "rate_control", "CBR");
    obs_data_set_int(enc_settings, "bitrate", 2500);

    obs_encoder_t* v_enc = obs_video_encoder_create("obs_x264", "simple_h264_stream", enc_settings, nullptr);
    obs_data_release(enc_settings);

    obs_encoder_set_video(v_enc, obs_get_video());
    obs_output_set_video_encoder(output, v_enc);
    obs_encoder_release(v_enc); // Output holds reference

    // Create Audio Encoder
    obs_data_t* a_enc_settings = obs_data_create();
    obs_data_set_int(a_enc_settings, "bitrate", 160);

    obs_encoder_t* a_enc = obs_audio_encoder_create("ffmpeg_aac", "simple_aac", a_enc_settings, 0, nullptr);
    obs_data_release(a_enc_settings);

    obs_encoder_set_audio(a_enc, obs_get_audio());
    obs_output_set_audio_encoder(output, a_enc, 0);
    obs_encoder_release(a_enc);

    // Start
    return obs_output_start(output);
}

void Recorder::StopRecording() {
    if (output) {
        obs_output_stop(output);
        // Wait for stop? (Should implement signal handler)
        obs_output_release(output);
        output = nullptr;
    }
}
