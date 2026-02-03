#pragma once

#include <QObject>
#include <QString>
#include <QVector>
#include <QVariant>

/**
 * @brief Main controller for the StreamDVR application.
 * Handles the state machine for recording modes (Off, Background, Manual),
 * game detection, and coordinates between the UI and the recording backend (ObsManager).
 */
class ApplicationController : public QObject
{
    Q_OBJECT

    // -- Properties exposed to QML --
    Q_PROPERTY(bool isRecording READ isRecording NOTIFY isRecordingChanged)
    Q_PROPERTY(QString activeGame READ activeGame WRITE setActiveGame NOTIFY activeGameChanged)
    Q_PROPERTY(RecordingMode recordingMode READ recordingMode WRITE setRecordingMode NOTIFY recordingModeChanged)
    Q_PROPERTY(QString statusMessage READ statusMessage NOTIFY statusMessageChanged)

public:
    enum class RecordingMode {
        Off,
        Background, // Rolling buffer (fMP4)
        Manual      // Start/Stop explicitly
    };
    Q_ENUM(RecordingMode)

    explicit ApplicationController(QObject *parent = nullptr);
    ~ApplicationController();

    // -- Getters --
    bool isRecording() const;
    QString activeGame() const;
    RecordingMode recordingMode() const;
    QString statusMessage() const;

    // -- Setters --
    void setActiveGame(const QString &game);
    void setRecordingMode(RecordingMode mode);

    // -- Invokables (Called from QML) --
    Q_INVOKABLE void startRecording();
    Q_INVOKABLE void stopRecording();

    /**
     * @brief Saves a clip from the rolling buffer or current recording.
     * @param durationSeconds Duration to look back (for buffer) or clip length.
     */
    Q_INVOKABLE void saveClip(int durationSeconds);

    /**
     * @brief Toggles the live preview overlay/mode.
     */
    Q_INVOKABLE void togglePreview();

signals:
    void isRecordingChanged();
    void activeGameChanged();
    void recordingModeChanged();
    void statusMessageChanged();
    void errorOccurred(const QString &message);

private:
    bool m_isRecording = false;
    QString m_activeGame;
    RecordingMode m_recordingMode = RecordingMode::Background;
    QString m_statusMessage = "Ready";

    // TODO: Reference to ObsManager
    // ObsManager* m_obsManager;

    void updateStatus(const QString &msg);
};
