import React, { useMemo } from 'react';
import { useSettings } from '../Context/SettingsContext';
import DashPlayer from '../Components/DashPlayer';
import { MdLiveTv, MdError } from 'react-icons/md';

export default function LivePreview() {
    const { state } = useSettings();
    const { recording } = state;

    // Find the active live content
    const activeLiveContent = useMemo(() => {
        // First check if there's an active recording in state
        if (!recording) return null;

        // Try to find the matching content in the content list that is marked as live
        // The backend creates metadata for the live session with IsLive=true
        const liveContent = state.content.find(c =>
            c.type === 'Session' &&
            c.game === recording.game &&
            (c.isLive || c.filePath.endsWith('.mpd'))
        );

        return liveContent;
    }, [state.content, recording]);

    const getVideoUrl = (filePath: string) => {
        // Backend expects /api/live/{Game}/{FileName} for DASH handling
        // or we can use the generic content endpoint if it handles Range requests properly for chunks

        // Use the same logic as Sessions.tsx
        const normalizedPath = filePath.replace(/\\/g, '/');
        // Check for 'Full Sessions' or 'Sessions'
        let splitIndex = normalizedPath.indexOf('/Full Sessions/');
        let segmentLength = '/Full Sessions/'.length;

        if (splitIndex === -1) {
            splitIndex = normalizedPath.indexOf('/Sessions/');
            segmentLength = '/Sessions/'.length;
        }

        if (splitIndex !== -1) {
             const relativePath = normalizedPath.substring(splitIndex + segmentLength);
             const parts = relativePath.split('/');
             // Expect Game/Timestamp/Timestamp.mpd (3 parts)
             // OR Game/Game.mpd (2 parts) if using master
             // Since we switched to direct timestamped files: Game/Timestamp/Timestamp.mpd
             // But the API might expect Game/File

             // If we use /api/live/{Game}/{Path...} it might be easier
             // Let's assume the backend 'LiveController' (if it exists) handles serving.
             // If not, we might need to rely on static file serving or 'api/content'.

             // The backend logic in Sessions.tsx used /api/live/{Game}/{FileName}
             // If we have Game/Timestamp/Timestamp.mpd, we need to handle that.

             if (parts.length >= 2) {
                 const game = parts[0];
                 // Encode each part of the path separately to preserve directory structure for relative DASH chunks
                 const fileParts = parts.slice(1);
                 const encodedPath = fileParts.map(p => encodeURIComponent(p)).join('/');
                 return `http://localhost:2222/api/live/${encodeURIComponent(game)}/${encodedPath}`;
             }
        }

        // Fallback
        return `http://localhost:2222/api/content?input=${encodeURIComponent(filePath)}&type=session`;
    };

    if (!recording) {
        return (
            <div className="flex flex-col items-center justify-center h-full bg-base-300 text-gray-500 gap-4">
                <MdError className="w-16 h-16 opacity-50" />
                <h2 className="text-xl font-bold">No Active Recording</h2>
                <p>Start a game or recording to see the live preview.</p>
            </div>
        );
    }

    if (!activeLiveContent) {
        return (
            <div className="flex flex-col items-center justify-center h-full bg-base-300 text-gray-500 gap-4">
                <span className="loading loading-spinner loading-lg"></span>
                <h2 className="text-xl font-bold">Initializing Preview...</h2>
                <p>Waiting for recording session to appear...</p>
                <div className="text-sm opacity-75">
                    Game: {recording.game} <br/>
                    File: {recording.fileName}
                </div>
            </div>
        );
    }

    return (
        <div className="flex flex-col h-full bg-black">
            <div className="flex-1 relative overflow-hidden">
                <DashPlayer
                    url={getVideoUrl(activeLiveContent.filePath)}
                    autoplay={true}
                    controls={true}
                    className="w-full h-full"
                />

                {/* Overlay */}
                <div className="absolute top-4 left-4 pointer-events-none">
                    <div className="bg-error/90 backdrop-blur-sm px-4 py-2 rounded-lg text-white font-bold animate-pulse flex items-center gap-2 shadow-lg">
                        <MdLiveTv className="w-5 h-5" />
                        LIVE PREVIEW
                    </div>
                </div>

                <div className="absolute top-4 right-4 pointer-events-none">
                     <div className="bg-black/60 backdrop-blur-sm px-4 py-2 rounded-lg text-white text-sm shadow-lg text-right">
                        <h3 className="font-bold text-lg">{activeLiveContent.game}</h3>
                        <p className="opacity-80 font-mono text-xs">{new Date(activeLiveContent.createdAt).toLocaleTimeString()}</p>
                    </div>
                </div>
            </div>
        </div>
    );
}
