import React, { useEffect, useRef } from 'react';
import { MediaPlayer, MediaPlayerClass } from 'dashjs';

interface DashPlayerProps {
    src?: string;
    url?: string; // Alias for src to support legacy usage
    autoplay?: boolean;
    controls?: boolean;
    className?: string;
    onTimeUpdate?: (time: number) => void;
    onDurationChange?: (duration: number) => void;
}

const DashPlayer: React.FC<DashPlayerProps> = ({
    src,
    url,
    autoplay = true,
    controls = true,
    className,
    onTimeUpdate,
    onDurationChange
}) => {
    const videoRef = useRef<HTMLVideoElement>(null);
    const playerRef = useRef<MediaPlayerClass | null>(null);
    const source = src || url;

    useEffect(() => {
        if (!source || !videoRef.current) return;

        // Initialize Dash Player
        const player = MediaPlayer().create();
        player.initialize(videoRef.current, source, autoplay);

        // Configure for low latency live streaming
        player.updateSettings({
            streaming: {
                lowLatencyEnabled: true,
                delay: {
                    liveDelay: 2.0
                },
                liveCatchup: {
                    mode: 'liveCatchupModeLoLp'
                }
            }
        } as any);

        playerRef.current = player;

        // Native video events
        const videoEl = videoRef.current;
        const handleTimeUpdate = () => {
            if (onTimeUpdate) onTimeUpdate(videoEl.currentTime);
        };
        const handleDurationChange = () => {
             if (onDurationChange) onDurationChange(videoEl.duration);
        };

        videoEl.addEventListener('timeupdate', handleTimeUpdate);
        videoEl.addEventListener('durationchange', handleDurationChange);

        return () => {
            if (player) {
                player.destroy();
                playerRef.current = null;
            }
            videoEl.removeEventListener('timeupdate', handleTimeUpdate);
            videoEl.removeEventListener('durationchange', handleDurationChange);
        };
    }, [source, autoplay]);

    return (
        <div className={`relative w-full h-full ${className || ''}`}>
            <video
                ref={videoRef}
                controls={controls}
                className="w-full h-full object-contain bg-black"
                style={{ width: '100%', height: '100%' }}
            />
        </div>
    );
};

export default DashPlayer;
