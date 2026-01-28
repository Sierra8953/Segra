import React, { useEffect, useState } from 'react';
import { MdPlayArrow, MdPause, MdVolumeUp, MdVolumeOff, MdVolumeDown } from 'react-icons/md';
import { sendMessageToBackend } from '../Utils/MessageUtils';

interface MpvPlayerProps {
  url: string;
  autoplay?: boolean;
  className?: string;
  onTimeUpdate?: (time: number) => void;
  onDurationChange?: (duration: number) => void;
}

export default function MpvPlayer({ url, autoplay = true, className, onTimeUpdate, onDurationChange }: MpvPlayerProps) {
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(100);

  // Send start command on mount
  useEffect(() => {
    sendMessageToBackend('StartMpv', {});

    // Slight delay to ensure MPV starts before loading
    const timer = setTimeout(() => {
        sendMessageToBackend('MpvLoad', { url });
        if (!autoplay) {
            sendMessageToBackend('MpvPause', {});
        } else {
            setIsPlaying(true);
        }
    }, 500);

    return () => {
      clearTimeout(timer);
      sendMessageToBackend('StopMpv', {});
    };
  }, []); // Only runs on mount/unmount

  // Handle URL changes
  useEffect(() => {
      if (url) {
          sendMessageToBackend('MpvLoad', { url });
          if (autoplay) setIsPlaying(true);
      }
  }, [url]);

  // Listen for backend events
  useEffect(() => {
    const handleWebSocketMessage = (event: CustomEvent<any>) => {
      const data = event.detail;
      if (data.method === 'MpvEvent') {
        const { name, value } = data.content;

        if (name === 'time-pos' && typeof value === 'number') {
            setCurrentTime(value);
            if (onTimeUpdate) onTimeUpdate(value);
        } else if (name === 'duration' && typeof value === 'number') {
            setDuration(value);
            if (onDurationChange) onDurationChange(value);
        } else if (name === 'pause') {
            setIsPlaying(!value);
        } else if (name === 'volume') {
            setVolume(value);
        }
      }
    };

    window.addEventListener('websocket-message', handleWebSocketMessage as EventListener);
    return () => window.removeEventListener('websocket-message', handleWebSocketMessage as EventListener);
  }, [onTimeUpdate, onDurationChange]);

  const togglePlay = () => {
    if (isPlaying) {
      sendMessageToBackend('MpvPause', {});
    } else {
      sendMessageToBackend('MpvPlay', {});
    }
    // Optimistic update
    setIsPlaying(!isPlaying);
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const time = parseFloat(e.target.value);
    setCurrentTime(time);
    sendMessageToBackend('MpvSeek', { time });
  };

  const handleVolume = (e: React.ChangeEvent<HTMLInputElement>) => {
    const vol = parseFloat(e.target.value);
    setVolume(vol);
    sendMessageToBackend('MpvSetVolume', { volume: vol });
  };

  const formatTime = (time: number) => {
    if (!time || isNaN(time)) return "0:00";
    const minutes = Math.floor(time / 60);
    const seconds = Math.floor(time % 60);
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  };

  return (
    <div className={`flex flex-col bg-black relative ${className || ''}`}>
      {/* Placeholder for where the video window effectively "is" (even if separate window) */}
      <div className="flex-1 flex items-center justify-center bg-black/50 text-white flex-col gap-2">
        <div className="loading loading-spinner loading-lg text-primary"></div>
        <p className="text-sm font-bold">Playing in external MPV window...</p>
        <p className="text-xs opacity-70">Controls below control the external player</p>
      </div>

      {/* Controls Bar */}
      <div className="bg-base-300 p-2 flex items-center gap-4">
        <button onClick={togglePlay} className="btn btn-circle btn-sm btn-ghost">
          {isPlaying ? <MdPause className="w-6 h-6" /> : <MdPlayArrow className="w-6 h-6" />}
        </button>

        <span className="text-xs font-mono">{formatTime(currentTime)}</span>

        <input
          type="range"
          min={0}
          max={duration || 100}
          value={currentTime}
          onChange={handleSeek}
          className="range range-xs range-primary flex-1"
        />

        <span className="text-xs font-mono">{formatTime(duration)}</span>

        <div className="flex items-center gap-2">
            {volume === 0 ? <MdVolumeOff /> : volume < 50 ? <MdVolumeDown /> : <MdVolumeUp />}
            <input
                type="range"
                min={0}
                max={100}
                value={volume}
                onChange={handleVolume}
                className="range range-xs w-20"
            />
        </div>
      </div>
    </div>
  );
}
