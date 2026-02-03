#include "ApplicationController.hpp"
#include <iostream>

ApplicationController::ApplicationController(QObject *parent)
    : QObject(parent)
{
    // Initialize OBS manager here
}

ApplicationController::~ApplicationController()
{
}

bool ApplicationController::isRecording() const { return m_isRecording; }
QString ApplicationController::activeGame() const { return m_activeGame; }
ApplicationController::RecordingMode ApplicationController::recordingMode() const { return m_recordingMode; }
QString ApplicationController::statusMessage() const { return m_statusMessage; }

void ApplicationController::setActiveGame(const QString &game)
{
    if (m_activeGame != game) {
        m_activeGame = game;
        emit activeGameChanged();
    }
}

void ApplicationController::setRecordingMode(RecordingMode mode)
{
    if (m_recordingMode != mode) {
        m_recordingMode = mode;
        emit recordingModeChanged();
        // Logic to stop/start recording based on new mode would go here
    }
}

void ApplicationController::startRecording()
{
    if (m_isRecording) return;

    // Call ObsManager->StartRecording(...)
    m_isRecording = true;
    updateStatus("Recording Started");
    emit isRecordingChanged();
}

void ApplicationController::stopRecording()
{
    if (!m_isRecording) return;

    // Call ObsManager->StopRecording()
    m_isRecording = false;
    updateStatus("Recording Stopped");
    emit isRecordingChanged();
}

void ApplicationController::saveClip(int durationSeconds)
{
    updateStatus("Saving Clip...");
    // Trigger ClipService logic
}

void ApplicationController::togglePreview()
{
    // Toggle MPV Widget visibility or source
}

void ApplicationController::updateStatus(const QString &msg)
{
    m_statusMessage = msg;
    emit statusMessageChanged();
}
