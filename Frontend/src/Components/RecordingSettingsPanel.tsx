import React, { useMemo } from 'react';
import { useSettings, useSettingsUpdater } from '../Context/SettingsContext';
import { DeviceSetting, AudioDevice, VideoQualityPreset } from '../Models/types';
import { MdSettings, MdVideoSettings, MdAudiotrack, MdMemory } from 'react-icons/md';

interface RecordingSettingsPanelProps {
  activeGame: string | null;
}

export default function RecordingSettingsPanel({ activeGame }: RecordingSettingsPanelProps) {
  const settings = useSettings();
  const updateSettings = useSettingsUpdater();
  const { state, gameSpecificConfig } = settings;

  // --- Buffer Duration ---
  const currentBufferDuration = useMemo(() => {
    if (activeGame && gameSpecificConfig[activeGame]?.bufferDuration) {
      return gameSpecificConfig[activeGame].bufferDuration;
    }
    return settings.replayBufferDuration;
  }, [activeGame, gameSpecificConfig, settings.replayBufferDuration]);

  const handleBufferDurationChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = parseInt(e.target.value);
    if (!isNaN(val) && val > 0) {
      if (activeGame) {
        updateSettings({
          gameSpecificConfig: {
            ...gameSpecificConfig,
            [activeGame]: { ...gameSpecificConfig[activeGame], bufferDuration: val }
          }
        });
      } else {
        updateSettings({ replayBufferDuration: val });
      }
    }
  };

  // --- Video Quality ---
  const handleQualityPresetChange = (preset: VideoQualityPreset) => {
    const updates: Partial<typeof settings> = { videoQualityPreset: preset };

    // Apply presets (example values, adjust as per backend defaults if needed)
    if (preset === 'low') {
      updates.bitrate = 5;
      updates.resolution = '720p';
      updates.frameRate = 30;
    } else if (preset === 'standard') {
      updates.bitrate = 10;
      updates.resolution = '1080p';
      updates.frameRate = 60;
    } else if (preset === 'high') {
      updates.bitrate = 20;
      updates.resolution = '1080p';
      updates.frameRate = 60;
    }
    // 'custom' doesn't change values immediately, just enables editing
    updateSettings(updates);
  };

  // --- Audio Devices ---
  const isDeviceEnabled = (device: AudioDevice, type: 'input' | 'output') => {
    const list = type === 'input' ? settings.inputDevices : settings.outputDevices;
    return list.some(d => d.id === device.id);
  };

  const toggleDevice = (device: AudioDevice, type: 'input' | 'output') => {
    const listKey = type === 'input' ? 'inputDevices' : 'outputDevices';
    const currentList = settings[listKey];
    const exists = currentList.find(d => d.id === device.id);

    let newList: DeviceSetting[];
    if (exists) {
      newList = currentList.filter(d => d.id !== device.id);
    } else {
      newList = [...currentList, { id: device.id, name: device.name, volume: 1.0 }];
    }

    updateSettings({ [listKey]: newList });
  };

  // --- Codecs ---
  // Filter codecs based on selected encoder type (cpu/gpu)
  const availableCodecs = useMemo(() => {
      if (settings.encoder === 'cpu') {
          return state.codecs.filter(c => !c.isHardwareEncoder);
      } else {
          // GPU
          // Simple filter: if Nvidia, look for nvenc; AMD -> amf; Intel -> qsv
          // or just show all hardware codecs
          return state.codecs.filter(c => c.isHardwareEncoder);
      }
  }, [settings.encoder, state.codecs]);

  return (
    <div className="flex-1 bg-base-200 p-6 overflow-y-auto">
      <div className="flex items-center gap-2 mb-6">
        <MdSettings className="w-6 h-6 text-primary" />
        <h2 className="text-xl font-bold">
            Recording Settings {activeGame ? `for ${activeGame}` : '(Global)'}
        </h2>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">

        {/* Quality Settings */}
        <div className="card bg-base-100 shadow-sm border border-base-content/10">
            <div className="card-body p-4">
                <h3 className="card-title text-sm flex items-center gap-2 text-gray-400">
                    <MdVideoSettings /> Quality
                </h3>

                <div className="form-control w-full">
                    <label className="label"><span className="label-text">Preset</span></label>
                    <div className="join w-full">
                        {(['low', 'standard', 'high', 'custom'] as VideoQualityPreset[]).map((preset) => (
                             <button
                                key={preset}
                                className={`btn btn-sm join-item flex-1 ${settings.videoQualityPreset === preset ? 'btn-primary' : ''}`}
                                onClick={() => handleQualityPresetChange(preset)}
                             >
                                 {preset.charAt(0).toUpperCase() + preset.slice(1)}
                             </button>
                        ))}
                    </div>
                </div>

                {settings.videoQualityPreset === 'custom' && (
                    <>
                        <div className="form-control w-full mt-2">
                            <label className="label"><span className="label-text">Resolution</span></label>
                            <select
                                className="select select-bordered select-sm"
                                value={settings.resolution}
                                onChange={(e) => updateSettings({ resolution: e.target.value as any })}
                            >
                                <option value="720p">720p</option>
                                <option value="1080p">1080p</option>
                                <option value="1440p">1440p</option>
                                <option value="4K">4K</option>
                            </select>
                        </div>

                        <div className="form-control w-full mt-2">
                            <label className="label"><span className="label-text">FPS</span></label>
                            <select
                                className="select select-bordered select-sm"
                                value={settings.frameRate}
                                onChange={(e) => updateSettings({ frameRate: parseInt(e.target.value) })}
                            >
                                <option value="30">30</option>
                                <option value="60">60</option>
                            </select>
                        </div>

                        <div className="form-control w-full mt-2">
                             <label className="label"><span className="label-text">Bitrate (Mbps)</span></label>
                             <input
                                type="number"
                                className="input input-sm input-bordered"
                                value={settings.bitrate}
                                onChange={(e) => updateSettings({ bitrate: parseInt(e.target.value) })}
                             />
                        </div>
                    </>
                )}
            </div>
        </div>

        {/* Codec & Buffer */}
        <div className="card bg-base-100 shadow-sm border border-base-content/10">
            <div className="card-body p-4">
                <h3 className="card-title text-sm flex items-center gap-2 text-gray-400">
                    <MdMemory /> Encoder & Buffer
                </h3>

                <div className="form-control w-full">
                    <label className="label"><span className="label-text">Encoder Type</span></label>
                    <div className="join w-full">
                        <button
                            className={`btn btn-sm join-item flex-1 ${settings.encoder === 'gpu' ? 'btn-primary' : ''}`}
                            onClick={() => updateSettings({ encoder: 'gpu' })}
                        >
                            GPU ({state.gpuVendor})
                        </button>
                        <button
                            className={`btn btn-sm join-item flex-1 ${settings.encoder === 'cpu' ? 'btn-primary' : ''}`}
                            onClick={() => updateSettings({ encoder: 'cpu' })}
                        >
                            CPU
                        </button>
                    </div>
                </div>

                <div className="form-control w-full mt-2">
                    <label className="label"><span className="label-text">Codec</span></label>
                    <select
                        className="select select-bordered select-sm"
                        value={settings.codec?.internalEncoderId || ''}
                        onChange={(e) => {
                            const selected = availableCodecs.find(c => c.internalEncoderId === e.target.value);
                            updateSettings({ codec: selected || null });
                        }}
                    >
                        {availableCodecs.map(c => (
                            <option key={c.internalEncoderId} value={c.internalEncoderId}>
                                {c.friendlyName}
                            </option>
                        ))}
                    </select>
                </div>

                <div className="divider my-2"></div>

                <div className="form-control w-full">
                    <label className="label">
                        <span className="label-text flex items-center gap-2">
                             Buffer Duration (seconds)
                        </span>
                        <span className="label-text-alt">{Math.floor(currentBufferDuration / 60)}m {currentBufferDuration % 60}s</span>
                    </label>
                    <input
                        type="range"
                        min="30"
                        max="3600"
                        step="30"
                        className="range range-xs range-primary"
                        value={currentBufferDuration ?? 0}
                        onChange={handleBufferDurationChange}
                    />
                    <div className="w-full flex justify-between text-xs px-2 mt-1">
                        <span>30s</span>
                        <span>60m</span>
                    </div>
                </div>
            </div>
        </div>

        {/* Audio Tracks */}
        <div className="card bg-base-100 shadow-sm border border-base-content/10">
            <div className="card-body p-4">
                 <h3 className="card-title text-sm flex items-center gap-2 text-gray-400">
                    <MdAudiotrack /> Audio Tracks
                </h3>

                <div className="flex-1 overflow-y-auto max-h-64 pr-2">
                    <div className="text-xs font-bold text-gray-500 mb-2 uppercase tracking-wide">Output (Speakers/Headphones)</div>
                    {state.outputDevices.map(device => (
                        <label key={device.id} className="label cursor-pointer justify-start gap-3 py-1 hover:bg-base-200 rounded px-1">
                            <input
                                type="checkbox"
                                className="checkbox checkbox-sm checkbox-primary"
                                checked={isDeviceEnabled(device, 'output')}
                                onChange={() => toggleDevice(device, 'output')}
                            />
                            <span className="label-text truncate" title={device.name}>{device.name}</span>
                        </label>
                    ))}

                    <div className="divider my-1"></div>

                    <div className="text-xs font-bold text-gray-500 mb-2 uppercase tracking-wide">Input (Microphones)</div>
                    {state.inputDevices.map(device => (
                        <label key={device.id} className="label cursor-pointer justify-start gap-3 py-1 hover:bg-base-200 rounded px-1">
                            <input
                                type="checkbox"
                                className="checkbox checkbox-sm checkbox-primary"
                                checked={isDeviceEnabled(device, 'input')}
                                onChange={() => toggleDevice(device, 'input')}
                            />
                            <span className="label-text truncate" title={device.name}>{device.name}</span>
                        </label>
                    ))}
                </div>

                <div className="form-control mt-4">
                     <label className="cursor-pointer label justify-start gap-3">
                        <input
                            type="checkbox"
                            className="toggle toggle-xs toggle-primary"
                            checked={settings.enableSeparateAudioTracks}
                            onChange={(e) => updateSettings({ enableSeparateAudioTracks: e.target.checked })}
                        />
                        <span className="label-text">Record Separate Tracks</span>
                     </label>
                </div>
            </div>
        </div>

      </div>
    </div>
  );
}
