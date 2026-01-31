import { useState, useMemo, useEffect } from 'react';
import { useSettings } from '../Context/SettingsContext';
import { Content, Selection } from '../Models/types';
import MpvPlayer from '../Components/MpvPlayer';
import { MdSearch, MdLiveTv, MdContentCut, MdSave, MdRefresh } from 'react-icons/md';
import { sendMessageToBackend } from '../Utils/MessageUtils';
import RecordingSettingsPanel from '../Components/RecordingSettingsPanel';

export default function Sessions() {
  const { state } = useSettings();
  const { recording } = state;

  // State for active game filter
  const [activeGame, setActiveGame] = useState<string | null>(null);

  // Search query for games
  const [searchQuery, setSearchQuery] = useState('');

  // Player state
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);

  // Clipping state
  const [clipStart, setClipStart] = useState<number | null>(null);
  const [clipEnd, setClipEnd] = useState<number | null>(null);
  const [isSavingClip, setIsSavingClip] = useState(false);

  // Extract unique games from content
  const games = useMemo(() => {
    const gameSet = new Set<string>();
    state.content
        .filter(c => c.type === 'Session')
        .forEach(c => gameSet.add(c.game));

    // Also add the currently recording game if it exists
    if (recording?.game) {
        gameSet.add(recording.game);
    }

    return Array.from(gameSet).sort();
  }, [state.content, recording?.game]);

  // Filter games based on search
  const filteredGames = useMemo(() => {
    return games.filter(g => g.toLowerCase().includes(searchQuery.toLowerCase()));
  }, [games, searchQuery]);

  // If no active game is selected, select the first one or the recording one
  useEffect(() => {
    if (!activeGame && games.length > 0) {
        if (recording?.game) {
            setActiveGame(recording.game);
        } else {
            setActiveGame(games[0]);
        }
    }
  }, [games, recording?.game]);

  // Reset clipping when game changes
  useEffect(() => {
      setClipStart(null);
      setClipEnd(null);
  }, [activeGame]);

  // Get content for the active game
  const activeGameContent = useMemo(() => {
    if (!activeGame) return [];
    return state.content
        .filter(c => c.type === 'Session' && c.game === activeGame)
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }, [state.content, activeGame]);

  // Determine the active video to play
  const activeVideo = useMemo(() => {
    if (!activeGame) return null;

    const isLive = recording?.game === activeGame;

    if (isLive) {
        const liveContent = activeGameContent.find(c => c.isLive);
        if (liveContent) return liveContent;
    }

    return activeGameContent.length > 0 ? activeGameContent[0] : null;
  }, [activeGame, activeGameContent, recording]);

  const getVideoUrl = (video: Content) => {
    // If it's a Dash manifest
    if (video.isLive || video.filePath.endsWith('.mpd')) {
        // Simplified URL construction for Single Session per Game
        // The file is located at .../Sessions/{Game}/{Game}.mpd
        // The backend expects /api/live/{GameFolder}/{FileName}
        // Since FileName is now just {Game}.mpd (no nested timestamp folder), we can rely on standard construction.

        // However, we still need to parse the Game name from the path properly if possible,
        // or rely on video.game if it's reliable.
        // Let's stick to parsing from path to be safe against metadata/path mismatch, using the robust logic.

        const normalizedPath = video.filePath.replace(/\\/g, '/');

        // Check for 'Full Sessions' (standard) or 'Sessions' (legacy/fallback)
        let splitIndex = normalizedPath.indexOf('/Full Sessions/');
        let segmentLength = '/Full Sessions/'.length;

        if (splitIndex === -1) {
            splitIndex = normalizedPath.indexOf('/Sessions/');
            segmentLength = '/Sessions/'.length;
        }

        // For DASH content (Live or recorded sessions), we always request the "virtual.mpd"
        // This serves a dynamically patched manifest that stitches all available segments for the game
        // into a single timeline.
        // We ignore the specific file path on disk (which points to the latest chunk) and instead
        // point to the Game Root so relative segment paths (Timestamp/chunk.m4s) resolve correctly.
        if (splitIndex !== -1) {
             const relativePath = normalizedPath.substring(splitIndex + segmentLength);
             const parts = relativePath.split('/');
             if (parts.length >= 1) {
                 const game = parts[0];
                 // Force usage of virtual.mpd at the game root level
                 return `http://localhost:2222/api/live/${encodeURIComponent(game)}/virtual.mpd?t=${refreshKey}`;
             }
        }

        // Fallback if path parsing fails
        return `http://localhost:2222/api/content?input=${encodeURIComponent(video.filePath)}&type=session`;
    }
    // Standard video file
    return `http://localhost:2222/api/content?input=${encodeURIComponent(video.filePath)}&type=session`;
  };

  const handleCreateClip = () => {
      if (clipStart === null || clipEnd === null || !activeVideo) return;

      setIsSavingClip(true);

      // Check if we are playing DASH content (Live or .mpd)
      const isDash = activeVideo.isLive || activeVideo.filePath.endsWith('.mpd');

      const selection: Selection = {
          id: Math.floor(Math.random() * 1000000),
          type: activeVideo.type,
          startTime: clipStart,
          endTime: clipEnd,
          // If DASH/Live, use "virtual" to instruct backend to stitch from the rolling buffer
          fileName: isDash ? "virtual" : activeVideo.fileName,
          game: activeVideo.game,
          title: `Clip ${new Date().toLocaleString()}`,
          isLoading: true
      };

      sendMessageToBackend('CreateClip', { Selections: [selection] });

      // Reset after a delay
      setTimeout(() => {
          setIsSavingClip(false);
          setClipStart(null);
          setClipEnd(null);
      }, 1000);
  };

  const formatTime = (time: number) => {
      const minutes = Math.floor(time / 60);
      const seconds = Math.floor(time % 60);
      return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  };

  return (
    <div className="flex h-full bg-base-200">
      {/* Sidebar - Game List */}
      <div className="w-64 bg-base-300 border-r border-base-content/10 flex flex-col transition-all duration-300">
        <div className="p-4 border-b border-base-content/10">
            <h2 className="text-xl font-bold mb-4">Games</h2>
            <div className="relative">
                <input
                    type="text"
                    placeholder="Search games..."
                    className="input input-sm input-bordered w-full pr-8"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                />
                <MdSearch className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400" />
            </div>
        </div>

        <div className="flex-1 overflow-y-auto">
            {filteredGames.map(game => (
                <button
                    key={game}
                    className={`w-full text-left px-4 py-3 flex items-center justify-between hover:bg-base-200 transition-colors ${activeGame === game ? 'bg-base-200 border-l-4 border-primary' : ''}`}
                    onClick={() => setActiveGame(game)}
                >
                    <span className="truncate font-medium">{game}</span>
                    {recording?.game === game && (
                        <span className="flex items-center gap-1 text-xs text-error animate-pulse font-bold">
                            <MdLiveTv />
                            REC
                        </span>
                    )}
                </button>
            ))}
            {filteredGames.length === 0 && (
                <div className="p-4 text-center text-gray-500 text-sm">
                    No games found
                </div>
            )}
        </div>

      </div>

      {/* Main Content */}
      <div className="flex-1 flex flex-col h-full overflow-hidden">

        {/* Player Area */}
        <div className="h-3/5 bg-black relative border-b border-base-content/10 flex flex-col">
            {activeVideo ? (
                <div className="relative flex-1 bg-black min-h-0">
                    {activeVideo.isLive || activeVideo.filePath.endsWith('.mpd') ? (
                        <MpvPlayer
                            key={`${activeVideo.filePath}-${refreshKey}`}
                            url={getVideoUrl(activeVideo)}
                            className="w-full h-full"
                            autoplay={true}
                            onTimeUpdate={setCurrentTime}
                            onDurationChange={setDuration}
                        />
                    ) : (
                        <video
                            src={getVideoUrl(activeVideo)}
                            className="w-full h-full"
                            controls
                            autoPlay={false}
                            onTimeUpdate={(e) => setCurrentTime(e.currentTarget.currentTime)}
                            onDurationChange={(e) => setDuration(e.currentTarget.duration)}
                        />
                    )}

                    {/* Overlay Info */}
                    <div className="absolute top-4 left-4 right-4 flex justify-between pointer-events-none">
                        <div className="bg-black/60 backdrop-blur-sm px-3 py-1 rounded text-white text-sm pointer-events-auto">
                            <h3 className="font-bold">{activeVideo.game}</h3>
                            <p className="text-xs opacity-80">{new Date(activeVideo.createdAt).toLocaleString()}</p>
                        </div>

                        <div className="flex items-center gap-2">
                             <button
                                className="btn btn-sm btn-ghost btn-circle bg-black/40 text-white hover:bg-black/60 pointer-events-auto"
                                onClick={() => setRefreshKey(k => k + 1)}
                                title="Refresh Player"
                            >
                                <MdRefresh className="w-5 h-5" />
                            </button>

                            {activeVideo.isLive && (
                                <div className="bg-error/90 backdrop-blur-sm px-3 py-1 rounded text-white text-sm font-bold animate-pulse flex items-center gap-2 pointer-events-auto">
                                    <MdLiveTv /> LIVE PREVIEW
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Clipping Overlay - Always visible on bottom if not fullscreen, but DashPlayer controls might hide it.
                        We put controls OUTSIDE the video element or overlay them.
                        For now, let's put them in a bar BELOW the video.
                    */}
                </div>
            ) : (
                <div className="w-full h-full flex items-center justify-center text-gray-500">
                    {activeGame ? (
                         <p>No recordings found for {activeGame}</p>
                    ) : (
                        <p>Select a game to view sessions</p>
                    )}
                </div>
            )}

            {/* Clipping Controls Bar */}
            {activeVideo && (
                <div className="bg-base-200 p-2 flex items-center justify-between border-t border-base-content/10">
                    <div className="flex items-center gap-4">
                        <div className="flex items-center gap-2">
                            <span className="text-sm font-mono bg-base-300 px-2 py-1 rounded">{formatTime(currentTime)}</span>
                            <span className="text-xs text-gray-500">/ {formatTime(duration)}</span>
                        </div>

                        <div className="h-8 w-px bg-base-content/10"></div>

                        <div className="flex items-center gap-2">
                            <button
                                className={`btn btn-sm ${clipStart !== null ? 'btn-primary' : 'btn-outline'}`}
                                onClick={() => setClipStart(currentTime)}
                            >
                                <MdContentCut className="rotate-180" /> Set Start
                                {clipStart !== null && <span className="text-xs ml-1">({formatTime(clipStart)})</span>}
                            </button>
                            <button
                                className={`btn btn-sm ${clipEnd !== null ? 'btn-primary' : 'btn-outline'}`}
                                onClick={() => setClipEnd(currentTime)}
                            >
                                Set End <MdContentCut />
                                {clipEnd !== null && <span className="text-xs ml-1">({formatTime(clipEnd)})</span>}
                            </button>
                        </div>
                    </div>

                    <button
                        className={`btn btn-sm btn-success ${isSavingClip ? 'loading' : ''}`}
                        disabled={clipStart === null || clipEnd === null || isSavingClip}
                        onClick={handleCreateClip}
                    >
                        {!isSavingClip && <MdSave />}
                        Save Clip
                    </button>
                </div>
            )}
        </div>

        {/* Settings Panel */}
        <RecordingSettingsPanel activeGame={activeGame} />
      </div>
    </div>
  );
}
