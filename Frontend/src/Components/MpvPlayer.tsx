import React, { useEffect, useRef, useState, useLayoutEffect } from 'react';
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

  // Reference to the placeholder element for tracking coordinates
  const containerRef = useRef<HTMLDivElement>(null);

  // Initialize MPV
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

  // Listen for backend events (state updates from MPV)
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

  // Window Embedding Logic: Track position and update backend
  useEffect(() => {
      const updatePosition = () => {
          if (containerRef.current) {
              const rect = containerRef.current.getBoundingClientRect();

              // We need screen coordinates, not just viewport coordinates.
              // window.screenX/Y gives the window position.
              // But in a WebView, client coordinates are relative to the viewport.
              // We need to account for DPI scaling if necessary, but usually ClientRect matches logical pixels.
              // We assume backend handles DPI awareness or we send logical pixels.
              // Note: Photino/WebView2 might have its own offset.
              // Simplest approach: Send client rect relative to window, plus window position?
              // Actually, MPV's SetWindowPos expects SCREEN coordinates.
              // window.screenX + rect.left might work if browser zoom is 100%.

              // Since we can't easily get exact physical screen coords from inside the webview reliably across all DPIs without help,
              // we will try window.screenX + rect.left.

              // Important: This assumes the browser window frame is included in screenX/Y or not?
              // window.screenX is typically the left edge of the browser window.
              // rect.left is relative to the viewport.
              // We also need to account for window decorations (title bar height) if not fullscreen.
              // This is tricky in pure JS inside WebView.
              // HOWEVER, Photino provides the window position to the backend natively.
              // The backend knows the main window position.
              // So we just need to send the position RELATIVE TO THE WEBVIEW (Client Area).
              // The backend can then add (AppWindowX + TitleBarOffset + ClientX).
              // BUT, the backend MpvService.UpdateBounds receives (x, y, w, h).
              // Let's change the strategy: Send CLIENT coordinates (x, y relative to viewport) and let Backend add window offset?
              // Or try to approximate here.

              // Let's send the Client Rect. The backend "WindowUtils" or MpvService can GetWindowRect of the main window and add these offsets.
              // But for now, let's assume we send Screen Coordinates best effort.
              // window.screenX/Y in Electron/WebView usually works.
              // BUT we also need `window.outerHeight - window.innerHeight` to estimate titlebar?

              // Refined Strategy:
              // Send `rect` (x, y, width, height) relative to the document body.
              // Backend will use `ClientToScreen` API to map these to screen coordinates.
              // Wait, backend doesn't know *where* in the DOM the element is.
              // It only knows the window handle.
              // If we send `rect.left` and `rect.top`, that is relative to the client area of the WebView HWND.
              // So Backend: `ClientToScreen(hwnd, point(rect.left, rect.top))` -> ScreenX, ScreenY.
              // This is the most robust way.

              sendMessageToBackend('UpdateMpvBounds', {
                  x: Math.round(rect.left),
                  y: Math.round(rect.top),
                  width: Math.round(rect.width),
                  height: Math.round(rect.height)
              });
          }
      };

      // Interval for frequent updates (handling drag/resize)
      const intervalId = setInterval(updatePosition, 16); // ~60fps

      // Also update on scroll/resize events
      window.addEventListener('resize', updatePosition);
      window.addEventListener('scroll', updatePosition);

      // Initial update
      updatePosition();

      return () => {
          clearInterval(intervalId);
          window.removeEventListener('resize', updatePosition);
          window.removeEventListener('scroll', updatePosition);
      };
  }, []);

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
      {/*
          Placeholder DIV for MPV Overlay.
          The backend will position the MPV window exactly over this element.
          We make it black so it looks like a background if MPV lags.
      */}
      <div
        ref={containerRef}
        className="flex-1 bg-black w-full h-full"
      >
        {/* Optional: Loading spinner that is visible only if MPV isn't covering it yet */}
        <div className="w-full h-full flex items-center justify-center text-white/20">
            <span className="loading loading-spinner loading-md"></span>
        </div>
      </div>

      {/* Controls Bar */}
      {/* We set z-index high to ensure controls (if we overlay them later) are visible,
          though MPV "always on top" might obscure them if we don't position MPV strictly *under* controls
          or use a separate window for controls.
          Current design has controls *below* the video rect, so no overlap issues.
      */}
      <div className="bg-base-300 p-2 flex items-center gap-4 z-10">
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
